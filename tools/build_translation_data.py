"""把 data/translations/ 下的翻译源文件合并成插件运行时需要的两个 TSV。

输入（data/translations/）：
  zh_ui.tsv / zh_prologue.tsv / zh_prologue_extra.tsv / zh_1_5.tsv / zh_videosubs.tsv
      grid, recordID, zh
  zh_static.tsv        count, enUS, zh        （硬编码 UI）
  zh_code.tsv          enUS, zh               （代码内文本）
  zh_aliases.tsv       enUS, zh               （角色名/称谓）
  zh_hud.tsv           enUS, zh               （操作提示等）
  zh_subtitles.tsv     enUS, zh               （Timeline 过场字幕）

输出（build/）：
  zh_gridly.tsv   grid, recordID, zh   —— 注入 Gridly.Project 的中文列
  zh_static.tsv   en, zh               —— 英文原文精确匹配 → 中文

运行：python tools/build_translation_data.py
"""
import csv

import paths

GRID_FILES = [
    "zh_ui.tsv",
    "zh_prologue.tsv",
    "zh_prologue_extra.tsv",
    "zh_1_5.tsv",
    "zh_videosubs.tsv",
]
STATIC_FILES = [
    # (文件名, en 列下标, zh 列下标)
    ("zh_static.tsv", 1, 2),
    ("zh_code.tsv", 0, 1),
    ("zh_aliases.tsv", 0, 1),
    ("zh_hud.tsv", 0, 1),
    ("zh_subtitles.tsv", 0, 1),
]


def clean(value: str) -> str:
    if value is None:
        return ""
    return value.replace("\t", " ").replace("\r", "").replace("\n", "\\n")


def read_rows(path):
    with open(path, encoding="utf-8-sig", newline="") as fh:
        reader = csv.reader(fh, delimiter="\t")
        try:
            header = next(reader)
        except StopIteration:
            return []
        rows = []
        for parts in reader:
            if not parts or not any(p.strip() for p in parts):
                continue
            rows.append(parts)
        return header, rows


def build_gridly(out):
    rows = []
    for name in GRID_FILES:
        path = paths.TRANSLATIONS / name
        if not path.exists():
            print("  [跳过] 缺少 %s" % name)
            continue
        _header, data = read_rows(path)
        file_rows = []
        for parts in data:
            if len(parts) < 3:
                continue
            file_rows.append((clean(parts[0]), clean(parts[1]), clean(parts[2])))
        print("  %-24s %5d 行" % (name, len(file_rows)))
        rows.extend(file_rows)

    seen = set()
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("grid\trecordID\tzh\n")
        for grid, record_id, zh in rows:
            key = (grid, record_id)
            if key in seen:
                continue
            seen.add(key)
            fh.write("%s\t%s\t%s\n" % (grid, record_id, zh))
    print("  -> %s：%d 条" % (out.name, len(seen)))


def build_static(out):
    rows = []
    for name, en_idx, zh_idx in STATIC_FILES:
        path = paths.TRANSLATIONS / name
        if not path.exists():
            print("  [跳过] 缺少 %s" % name)
            continue
        _header, data = read_rows(path)
        file_rows = []
        for parts in data:
            if len(parts) <= max(en_idx, zh_idx):
                continue
            en = clean(parts[en_idx])
            zh = clean(parts[zh_idx])
            if not en and not zh:
                continue
            file_rows.append((en, zh))
        print("  %-24s %5d 行" % (name, len(file_rows)))
        rows.extend(file_rows)

    seen = set()
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("en\tzh\n")
        for en, zh in rows:
            if en in seen:
                continue
            seen.add(en)
            fh.write("%s\t%s\n" % (en, zh))
    print("  -> %s：%d 条" % (out.name, len(seen)))


def main():
    paths.ensure_dirs()
    if not paths.TRANSLATIONS.exists():
        raise SystemExit("找不到翻译目录：%s" % paths.TRANSLATIONS)
    print("合并 Gridly 文本：")
    build_gridly(paths.BUILD / "zh_gridly.tsv")
    print("合并静态文本：")
    build_static(paths.BUILD / "zh_static.tsv")


if __name__ == "__main__":
    main()
