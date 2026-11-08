"""从反编译出的 Assembly-CSharp 源码里提取“写在代码里的玩家可见文本”。

游戏有不少弹窗/成就描述/提示直接写在 C# 字符串里（如 "Save these settings?"），
它们不在场景序列化数据里，需要单独找出来翻译（随后加入 data/translations/zh_code.tsv，
插件的 TMP setter 钩子会按精确匹配翻译）。

用法：
  python tools/extract_code_literals.py --src work/decompiled
（需要先用 ILSpy/ilspycmd 把 LGTS_Data/Managed/Assembly-CSharp.dll 反编译到该目录）

输出：work/code_literals.tsv（enUS、出现次数、位置）
"""
import argparse
import os
import re
from collections import defaultdict

import paths

STR_RE = re.compile(r'(?<![\\w"])@?"((?:[^"\\\\]|\\\\.)*)"')
LETTER_RE = re.compile(r"[A-Za-z]{2,}")
KEY_RE = re.compile(r"^[A-Z][A-Z0-9_]{2,}$")
EXCLUDE_LINE = (
    "Debug.Log", "LogWarning", "LogError", "Resources.Load", "Shader.Find",
    "GetComponent<", "AddComponent<", "typeof(", "StartCoroutine", "PlayerPrefs",
    "Application.Quit", "SceneManager.", "Path.", "Invoke(", "case \"",
)
TECH_PREFIX = ("Assets/", "Resources/", "event:/", "grid:", "http", "com.", "vca:/", "bus:/", "bank:/")
GAME_DIRS = ("LGTS/", "LGTS\\", "Assets/Scripts/", "Assets\\Scripts\\")


def iter_lines(src: str):
    for root, _dirs, files in os.walk(src):
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(root, name)
            rel = os.path.relpath(path, src)
            try:
                lines = open(path, encoding="utf-8-sig", errors="replace").read().split("\n")
            except Exception:
                continue
            for line_no, line in enumerate(lines, 1):
                yield rel, line_no, line


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--src", required=True, help="反编译源码目录")
    args = parser.parse_args()
    if not os.path.isdir(args.src):
        raise SystemExit("源码目录不存在：%s" % args.src)

    candidates = defaultdict(list)
    for rel, line_no, line in iter_lines(args.src):
        if rel.replace("\\", "/").find("/") >= 0:
            pass
        if not (rel.startswith("LGTS/") or rel.startswith("LGTS\\")
                or rel.startswith("Assets/Scripts/") or rel.startswith("Assets\\Scripts\\")):
            continue
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("*") or stripped.startswith("/*"):
            continue
        if any(marker in line for marker in EXCLUDE_LINE):
            continue
        for match in STR_RE.finditer(line):
            literal = match.group(1)
            if len(literal) < 4 or not LETTER_RE.search(literal):
                continue
            if KEY_RE.match(literal) or literal.startswith(TECH_PREFIX):
                continue
            words = re.findall(r"[A-Za-z][A-Za-z'\\-]*", literal)
            if len(words) < 2 and not literal.rstrip().endswith(("?", "!", ".", ":")):
                continue
            if not any(w[:1].islower() for w in words[1:]):
                continue
            candidates[literal].append("%s:%d" % (rel.replace("\\", "/"), line_no))

    rows = sorted(candidates.items(), key=lambda kv: (-len(kv[1]), kv[0]))
    out = paths.WORK / "code_literals.tsv"
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("enUS\toccurrences\tcontext\n")
        for text, locs in rows:
            fh.write("%s\t%d\t%s\n" % (text.replace("\t", " ").replace("\n", "\\n"), len(locs), locs[0]))
    print("已提取 %d 条代码内文本 -> %s" % (len(rows), out))


if __name__ == "__main__":
    main()
