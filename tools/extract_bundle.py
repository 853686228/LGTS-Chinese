"""列出 / 提取 LGTS_Data/data.unity3d 这个 UnityFS 包里的节点。

用法：
  python tools/extract_bundle.py list                 # 列出 341 个节点（名称、大小）
  python tools/extract_bundle.py extract resources.assets
  python tools/extract_bundle.py extract-small --max-size 16   # 解出所有 <=16MB 的节点到 work/unpacked

说明：data.unity3d 是 6.48 GB 的单个 UnityFS 包（LZ4 压缩），
游戏的所有场景/资源都在里面，而不是散落在 LGTS_Data 目录下。
"""
import argparse

import paths
from bundle_read import Bundle


def cmd_list(args):
    b = Bundle(str(paths.check_game()))
    print("引擎版本: %s" % b.version_engine)
    print("块数量: %d  节点数量: %d  解压后大小: %d" % (len(b.blocks), len(b.nodes), b.uncompressed_size))
    print()
    print("%-40s %12s" % ("节点", "大小"))
    for name in b.node_order:
        print("%-40s %12d" % (name, b.nodes[name][1]))


def cmd_extract(args):
    b = Bundle(str(paths.check_game()))
    if args.name not in b.nodes:
        raise SystemExit("节点不存在：%s（可用 extract_bundle.py list 查看）" % args.name)
    out = paths.UNPACKED / args.name.replace("/", "--")
    size = b.extract(args.name, str(out))
    print("已提取 %s -> %s (%d 字节)" % (args.name, out, size))


def cmd_extract_small(args):
    b = Bundle(str(paths.check_game()))
    paths.ensure_dirs()
    max_size = int(args.max_size * 1024 * 1024)
    done = b.extract_small(str(paths.UNPACKED), max_size=max_size)
    total = sum(size for _, size in done)
    print("已提取 %d 个节点到 %s（共 %.1f MB）" % (len(done), paths.UNPACKED, total / 1024 / 1024))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p_list = sub.add_parser("list", help="列出包内节点")
    p_list.set_defaults(func=cmd_list)

    p_ext = sub.add_parser("extract", help="提取单个节点")
    p_ext.add_argument("name", help="节点名，例如 resources.assets")
    p_ext.set_defaults(func=cmd_extract)

    p_small = sub.add_parser("extract-small", help="提取所有小于阈值的节点")
    p_small.add_argument("--max-size", type=float, default=16, help="阈值（MB），默认 16")
    p_small.set_defaults(func=cmd_extract_small)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
