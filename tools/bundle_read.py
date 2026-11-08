"""Random-access reader for a large UnityFS bundle (data.unity3d).

Only decompresses the 128 KB blocks that overlap the requested ranges.
"""
import struct, os, sys, bisect

import paths

paths.add_offline_pylibs()

import lz4.block
import lzma



class Bundle:
    def __init__(self, path):
        self.path = path
        self.f = open(path, "rb")
        self._parse_header()
        self._cache = {}
        self._cache_order = []
        self._cache_limit = 512  # 512 * 128KB = 64MB

    def _read(self, n):
        return self.f.read(n)

    def _parse_header(self):
        f = self.f
        f.seek(0)
        sig = self._read(8)
        if sig != b"UnityFS\0":
            raise ValueError("not a UnityFS bundle: " + repr(sig))
        self.version = struct.unpack(">I", self._read(4))[0]
        vp = b""
        while True:
            c = self._read(1)
            if c == b"\0":
                break
            vp += c
        ve = b""
        while True:
            c = self._read(1)
            if c == b"\0":
                break
            ve += c
        self.version_player = vp.decode()
        self.version_engine = ve.decode()
        self.size = struct.unpack(">q", self._read(8))[0]
        cbi = struct.unpack(">I", self._read(4))[0]
        ubi = struct.unpack(">I", self._read(4))[0]
        self.flags = struct.unpack(">I", self._read(4))[0]
        pos = f.tell()
        if self.version >= 7:
            pos += (-pos) % 16
            f.seek(pos)
        self.blocksinfo_offset = pos
        if self.flags & 0x80:
            f.seek(self.size - cbi)
        info_raw = self._read(cbi)
        self.blocksinfo_compressed = cbi
        self.blocksinfo_uncompressed = ubi
        ctype = self.flags & 0x3F
        if ctype == 0:
            info = info_raw
        elif ctype == 1:
            props, dict_size = struct.unpack("<BI", info_raw[:5])
            lc = props % 9
            rem = props // 9
            pb = rem // 5
            lp = rem % 5
            dec = lzma.LZMADecompressor(format=lzma.FORMAT_RAW, filters=[{"id": lzma.FILTER_LZMA1, "dict_size": dict_size, "lc": lc, "lp": lp, "pb": pb}])
            info = dec.decompress(info_raw[5:])
        elif ctype in (2, 3):
            info = lz4.block.decompress(info_raw, uncompressed_size=ubi)
        else:
            raise ValueError("blocksinfo compression %r not supported" % ctype)
        off = 0
        self.hash16 = info[off:off + 16]; off += 16
        blockcount = struct.unpack(">i", info[off:off + 4])[0]; off += 4
        self.blocks = []
        for _ in range(blockcount):
            us, cs, fl = struct.unpack(">IIH", info[off:off + 10]); off += 10
            self.blocks.append((us, cs, fl))
        nodecount = struct.unpack(">i", info[off:off + 4])[0]; off += 4
        self.nodes = {}
        self.node_order = []
        for _ in range(nodecount):
            o, s, fl = struct.unpack(">qqI", info[off:off + 20]); off += 20
            start = off
            while info[off] != 0:
                off += 1
            path = info[start:off].decode("utf-8", "replace"); off += 1
            self.nodes[path] = (o, s, fl)
            self.node_order.append(path)
        data_start = self.blocksinfo_offset + cbi
        align = 16 if (self.flags & 0x200) else 1
        data_start += (-data_start) % align
        self.data_start = data_start
        # cumulative offsets
        self.block_file_offsets = []
        self.block_uncomp_offsets = []
        fo = data_start
        uo = 0
        for us, cs, fl in self.blocks:
            self.block_file_offsets.append(fo)
            self.block_uncomp_offsets.append(uo)
            fo += cs
            uo += us
        self.uncompressed_size = uo

    def _load_block(self, idx):
        if idx in self._cache:
            return self._cache[idx]
        us, cs, fl = self.blocks[idx]
        self.f.seek(self.block_file_offsets[idx])
        raw = self.f.read(cs)
        ctype = fl & 0x3F
        if ctype == 0:
            data = raw
        elif ctype == 1:
            props, dict_size = struct.unpack("<BI", raw[:5])
            lc = props % 9
            rem = props // 9
            pb = rem // 5
            lp = rem % 5
            dec = lzma.LZMADecompressor(format=lzma.FORMAT_RAW, filters=[{"id": lzma.FILTER_LZMA1, "dict_size": dict_size, "lc": lc, "lp": lp, "pb": pb}])
            data = dec.decompress(raw[5:])
        elif ctype in (2, 3):
            data = lz4.block.decompress(raw, uncompressed_size=us)
        else:
            raise ValueError("block compression %r not supported" % ctype)
        self._cache[idx] = data
        self._cache_order.append(idx)
        if len(self._cache_order) > self._cache_limit:
            old = self._cache_order.pop(0)
            self._cache.pop(old, None)
        return data

    def read_range(self, offset, size):
        if size <= 0:
            return b""
        end = offset + size
        idx = bisect.bisect_right(self.block_uncomp_offsets, offset) - 1
        parts = []
        while offset < end:
            # guard
            if idx < 0 or idx >= len(self.blocks):
                raise ValueError("offset out of range")
            boff = self.block_uncomp_offsets[idx]
            block = self._load_block(idx)
            lo = offset - boff
            hi = min(len(block), end - boff)
            if lo >= len(block):
                break
            parts.append(block[lo:hi])
            offset = boff + hi
            idx += 1
        return b"".join(parts)

    def read_node(self, name):
        o, s, fl = self.nodes[name]
        return self.read_range(o, s)

    def extract(self, name, out_path):
        o, s, fl = self.nodes[name]
        data = self.read_node(name)
        os.makedirs(os.path.dirname(out_path), exist_ok=True)
        with open(out_path, "wb") as fh:
            fh.write(data)
        return len(data)

    def extract_small(self, out_dir, max_size=16 * 1024 * 1024):
        os.makedirs(out_dir, exist_ok=True)
        done = []
        for name in self.node_order:
            o, s, fl = self.nodes[name]
            if s > max_size:
                continue
            safe = name.replace("/", "--")
            data = self.read_node(name)
            with open(os.path.join(out_dir, safe), "wb") as fh:
                fh.write(data)
            done.append((name, s))
        return done


if __name__ == "__main__":
    path = sys.argv[1]
    b = Bundle(path)
    print("engine", b.version_engine, "blocks", len(b.blocks), "nodes", len(b.nodes), "uncompressed", b.uncompressed_size)
    print("data_start", b.data_start)
    if len(sys.argv) > 2 and sys.argv[2] == "list":
        for n in b.node_order:
            print(n, b.nodes[n][1])
    elif len(sys.argv) > 2 and sys.argv[2] == "test":
        name = sys.argv[3]
        data = b.read_node(name)
        print("read", name, len(data), data[:16].hex())
