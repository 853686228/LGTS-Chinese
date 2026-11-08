"""导出 resources.assets 里的 1,267 个剧本 TextAsset。

剧本文件（例如 DLG_7_4_OLH_01）本身是 JSON 结构，其中：
  - Conversation 的节点描述演出/分支（Author、Action、Next…）
  - "Text" 字段是 Gridly key（例如 DLG_7_4_OLH_01_001），真正的台词在
    work/gridly_all.tsv（由 dump_project.py 生成）里。

输出：work/textassets/<名字>.txt
"""
import os

import paths
import unity_helper as UH


def main():
    paths.ensure_dirs()
    env = UH.make_env()
    count = 0
    total_bytes = 0
    for _, sf in env.files.items():
        for _, obj in (getattr(sf, "objects", None) or {}).items():
            if obj.type.name != "TextAsset":
                continue
            ta = obj.read()
            text = ta.m_Script or ""
            data = text.encode("utf-8") if isinstance(text, str) else text
            name = (ta.m_Name or "unnamed").replace("/", "_").replace("\\", "_")
            with open(os.path.join(paths.TEXTASSETS, name + ".txt"), "wb") as fh:
                fh.write(data)
            count += 1
            total_bytes += len(data)
    print("已导出 %d 个剧本 TextAsset 到 %s（%.1f MB）" % (count, paths.TEXTASSETS, total_bytes / 1024 / 1024))


if __name__ == "__main__":
    main()
