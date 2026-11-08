"""解码游戏里的 Gridly 本地化总表（Gridly.Project）。

输出：
  work/project_typetree.json  完整结构化数据（19 张表 / 17,196 条记录 / enUS 列）
  work/gridly_all.tsv         全部英文原文（grid、recordID、enUS），供翻译/校对使用

前置条件：
  1. 已完成 pip install -r requirements.txt
  2. 已运行 python tools/extract_bundle.py extract-small

背景：游戏的对话/过场剧本 TextAsset 里只有 key（如 DLG_7_4_OLH_01_001），
      真正的文本保存在 resources.assets 里的 Gridly.Project 这个 ScriptableObject 中。
      正式版构建剥离了 TypeTree，所以需要 TypeTreeGeneratorAPI 从 Assembly-CSharp.dll
      重新生成字段类型树。
"""
import json
import re

import paths
import unity_helper as UH


def find_project_object(env):
    """在 resources.assets 里找到 Gridly.Project（MonoBehaviour，m_Name == "Project"）。

    注意：只解析 MonoBehaviour 头（m_Name），不依赖 m_Script 解引用；
    有些文件的 m_Script 指向其他序列化文件（如 globalgamemanagers），
    因此最好先运行 ``extract_bundle.py extract-small`` 把所有小节点都解出来。
    """
    sf = UH.find_file(env, "resources.assets")
    candidates = []
    skipped = 0
    for path_id, obj in sf.objects.items():
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            mb = obj.parse_monobehaviour_head()
        except Exception:
            skipped += 1
            continue
        if (getattr(mb, "m_Name", "") or "") == "Project":
            candidates.append((path_id, obj))
    if not candidates:
        raise SystemExit(
            "没有在 resources.assets 里找到 Gridly.Project（m_Name=Project）。\n"
            "请先解出全部小节点再重试：\n"
            "  python tools/extract_bundle.py extract-small\n"
            "（跳过 %d 个无法解析的 MonoBehaviour；若仍失败，请确认游戏版本为 build 20802495）" % skipped
        )
    return candidates[0]


def main():
    paths.ensure_dirs()
    if not paths.UNPACKED.exists() or not any(paths.UNPACKED.iterdir()):
        raise SystemExit("work/unpacked 为空，请先运行：python tools/extract_bundle.py extract-small")

    env = UH.make_env()
    path_id, obj = find_project_object(env)
    print("找到 Gridly.Project：pathID=%s，大小=%d 字节" % (path_id, obj.byte_size))

    try:
        tt = obj.read_typetree()
    except Exception as exc:
        raise SystemExit(
            "解码 Gridly.Project 失败：%s\n"
            "请确认已运行 python tools/extract_bundle.py extract-small，且游戏版本为 build 20802495。" % exc
        )
    grids = tt.get("grids") or []
    total_records = 0
    total_words = 0
    rows = []
    for grid in grids:
        for record in grid.get("records") or []:
            cols = {c.get("columnID"): c.get("text", "") for c in (record.get("columns") or [])}
            en = cols.get("enUS", "") or ""
            rows.append((grid.get("nameGrid", ""), record.get("recordID", ""), en))
            total_records += 1
            total_words += len(re.findall(r"[A-Za-z']+", en))

    json_path = paths.WORK / "project_typetree.json"
    with open(json_path, "w", encoding="utf-8") as fh:
        json.dump(tt, fh, ensure_ascii=False, default=str, indent=1)
    print("结构化数据已写入 %s" % json_path)

    tsv_path = paths.WORK / "gridly_all.tsv"
    with open(tsv_path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("grid\trecordID\tenUS\n")
        for grid, record_id, en in rows:
            fh.write("%s\t%s\t%s\n" % (grid, record_id, en.replace("\t", " ").replace("\n", "\\n")))
    print("英文原文已写入 %s" % tsv_path)

    print("表数量: %d，记录数: %d，英文词数: %d" % (len(grids), total_records, total_words))

    print("\n各表记录数：")
    for grid in grids:
        print("  %-16s %5d" % (grid.get("nameGrid", ""), len(grid.get("records") or [])))

    print("\n语言支持：")
    for lang in tt.get("langSupports") or []:
        print("  %s" % (lang,))


if __name__ == "__main__":
    main()
