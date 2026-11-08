"""导出烘焙在 Timeline 资产里的过场字幕（SubtitleClip.subtitleText）。

游戏的过场字幕不是 Gridly 文本：它们被直接序列化在 Timeline 资产里
（sharedassets*.assets 中 LGTS.Cinematics.Subtitles.SubtitleClip 的 subtitleText 字段）。
运行时会由 SubtitleTrackMixer 每帧写入 TMP 文本，插件通过 TMP setter 钩子按精确匹配翻译。

输出：work/subtitles.tsv（enUS、出现文件）
"""
import os

import paths
import unity_helper as UH


def main():
    paths.ensure_dirs()
    env = UH.make_env()
    seen = {}
    for file_key, sf in env.files.items():
        base = os.path.basename(file_key)
        for _, obj in (getattr(sf, "objects", None) or {}).items():
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                mb = obj.parse_monobehaviour_head()
                _, full = UH.script_fullname(mb)
            except Exception:
                continue
            if not full.endswith("SubtitleClip"):
                continue
            try:
                tt = obj.read_typetree()
            except Exception:
                continue
            text = tt.get("subtitleText") or ""
            if not text.strip():
                continue
            seen.setdefault(text, set()).add(base)

    out = paths.WORK / "subtitles.tsv"
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("enUS\tfiles\n")
        for text in sorted(seen):
            fh.write("%s\t%s\n" % (text.replace("\t", " ").replace("\n", "\\n"), ";".join(sorted(seen[text]))))
    print("已导出 %d 条 Timeline 字幕 -> %s" % (len(seen), out))


if __name__ == "__main__":
    main()
