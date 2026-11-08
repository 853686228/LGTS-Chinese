"""翻译数据自检：结构、重复 key、标签/占位符/换行一致性。

用法：
  python tools/check_translations.py            # 检查 data/translations
  python tools/check_translations.py --strict   # 发现问题时返回非 0

如果 work/gridly_all.tsv 存在（由 dump_project.py 生成），会额外把每条译文与
英文原文做富文本标签 / 占位符 / 换行数量的对比。
"""
import argparse
import csv
import re
import sys
from collections import Counter

import paths
import build_translation_data as B

TAG_RE = re.compile(r"</?[a-zA-Z][^>]*>")
SPRITE_RE = re.compile(r'<sprite="([^"]+)" name="([^"]+)"')
PLACEHOLDER_RE = re.compile(r"\{[^}]*\}|%[sd]")


def load_english_map():
    path = paths.WORK / "gridly_all.tsv"
    if not path.exists():
        return None
    result = {}
    with open(path, encoding="utf-8-sig", newline="") as fh:
        reader = csv.reader(fh, delimiter="\t")
        next(reader, None)
        for parts in reader:
            if len(parts) >= 3:
                result[(parts[0], parts[1])] = parts[2]
    return result


def compare(en, zh):
    """对比译文与原文的标签/占位符/换行。

    标签与占位符按“多重集合”比较而不是顺序：中文语序会改变<b>等标签的位置，
    但只要开闭标签数量、<sprite>、占位符种类/数量一致，就不会破坏富文本渲染。
    """
    issues = []
    if Counter(TAG_RE.findall(en)) != Counter(TAG_RE.findall(zh)):
        issues.append("富文本标签不一致")
    if Counter(SPRITE_RE.findall(en)) != Counter(SPRITE_RE.findall(zh)):
        issues.append("sprite 标签不一致")
    if en.count("\\n") != zh.count("\\n"):
        issues.append("换行数量不一致")
    if Counter(PLACEHOLDER_RE.findall(en)) != Counter(PLACEHOLDER_RE.findall(zh)):
        issues.append("占位符不一致")
    return issues


def check_grid_files(en_map, issues, stats):
    for name in B.GRID_FILES:
        path = paths.TRANSLATIONS / name
        if not path.exists():
            issues.append("%s：文件缺失" % name)
            continue
        _h, rows = B.read_rows(path)
        keys = set()
        for row in rows:
            if len(row) < 3:
                issues.append("%s：列数不足 %r" % (name, row))
                continue
            grid, record_id, zh = row[0], row[1], row[2]
            stats["rows"] += 1
            if (grid, record_id) in keys:
                issues.append("%s：重复 key %s/%s" % (name, grid, record_id))
            keys.add((grid, record_id))
            if not zh.strip():
                stats["empty"] += 1
            if en_map is not None:
                en = en_map.get((grid, record_id))
                if en is None:
                    stats["unknown_key"] += 1
                elif zh.strip():
                    for msg in compare(en, zh):
                        issues.append("%s/%s：%s" % (grid, record_id, msg))


def check_static_files(issues, stats):
    for name, en_idx, zh_idx in B.STATIC_FILES:
        path = paths.TRANSLATIONS / name
        if not path.exists():
            issues.append("%s：文件缺失" % name)
            continue
        _h, rows = B.read_rows(path)
        en_to_zh = {}
        for row in rows:
            if len(row) <= max(en_idx, zh_idx):
                issues.append("%s：列数不足 %r" % (name, row))
                continue
            en, zh = row[en_idx], row[zh_idx]
            stats["rows"] += 1
            if not zh.strip():
                stats["empty"] += 1
            if en.strip() and zh.strip() and en != zh:
                en_to_zh.setdefault(en, set()).add(zh)
                for msg in compare(en, zh):
                    issues.append("%s：%r — %s" % (name, en[:40], msg))
        for en, zh_set in en_to_zh.items():
            if len(zh_set) > 1:
                issues.append("%s：同一英文对应多个译文 %r" % (name, en[:40]))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--strict", action="store_true", help="发现问题时返回非 0")
    args = parser.parse_args()

    issues = []
    stats = {"rows": 0, "empty": 0, "unknown_key": 0}
    en_map = load_english_map()
    if en_map is None:
        print("提示：未找到 work/gridly_all.tsv，跳过与英文原文的标签对比。")
        print("      可先运行 python tools/extract_bundle.py extract-small && python tools/dump_project.py")
    check_grid_files(en_map, issues, stats)
    check_static_files(issues, stats)

    print("检查完成：%d 行，空译文 %d，英文 key 未匹配 %d，问题 %d"
          % (stats["rows"], stats["empty"], stats["unknown_key"], len(issues)))
    for msg in issues[:80]:
        print("  - " + msg)
    if len(issues) > 80:
        print("  ...（仅显示前 80 条）")
    if issues and args.strict:
        sys.exit(1)


if __name__ == "__main__":
    main()
