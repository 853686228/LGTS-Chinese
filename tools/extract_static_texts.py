"""统计所有序列化文件里的 TMP / UI.Text 文本组件（用于硬编码 UI 翻译）。

输出：work/static_texts.tsv
  列：class、file、path_id、text（换行转义为 \\n）

注意：硬编码 UI 也可能是运行时代码拼接出来的，本工具只负责“序列化在场景/预制体里”的那部分。
完整扫描 12 万个 MonoBehaviour 可能需要几分钟。
"""
import os

import paths
import unity_helper as UH

TMP_CLASSES = {"TMPro.TextMeshProUGUI", "TMPro.TextMeshPro"}
LEGACY_CLASS = "UnityEngine.UI.Text"


def main():
    paths.ensure_dirs()
    env = UH.make_env()
    rows = []
    counts = {}
    for file_key, sf in env.files.items():
        base = os.path.basename(file_key)
        objects = getattr(sf, "objects", None) or {}
        for path_id, obj in objects.items():
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                mb = obj.parse_monobehaviour_head()
                _, full = UH.script_fullname(mb)
            except Exception:
                continue
            if full not in TMP_CLASSES and full != LEGACY_CLASS:
                continue
            try:
                tt = obj.read_typetree()
            except Exception:
                continue
            text = tt.get("m_text") if full in TMP_CLASSES else tt.get("m_Text")
            if not text or not text.strip():
                continue
            rows.append((full, base, path_id, text))
            counts[full] = counts.get(full, 0) + 1

    out = paths.WORK / "static_texts.tsv"
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("class\tfile\tpath_id\ttext\n")
        for cls, base, path_id, text in rows:
            fh.write("%s\t%s\t%s\t%s\n" % (cls, base, path_id, text.replace("\t", " ").replace("\n", "\\n")))
    print("已导出 %d 条序列化文本 -> %s" % (len(rows), out))
    for cls, n in sorted(counts.items(), key=lambda x: -x[1]):
        print("  %-30s %d" % (cls, n))


if __name__ == "__main__":
    main()
