# 翻译数据说明

本目录是汉化补丁的全部翻译源文件。构建时由 `tools/build_translation_data.py` 合并为
插件运行时需要的 `zh_gridly.tsv`（Gridly 中文列）和 `zh_static.tsv`（英文精确匹配字典）。

## 文件列表

| 文件 | 列格式 | 内容 |
|---|---|---|
| `zh_ui.tsv` | `grid, recordID, zh` | 全部 UI 类网格（UI/Common/Items/Shops/Tutorials/Love_Prompt/Pickup/Hints/Quests/World_Map/Images） |
| `zh_prologue.tsv` | `grid, recordID, zh` | 第 2.1 章（周日清晨） |
| `zh_prologue_extra.tsv` | `grid, recordID, zh` | 第 2.1 章补充/废案记录 |
| `zh_1_5.tsv` | `grid, recordID, zh` | 第 1.5 章（开场序章） |
| `zh_videosubs.tsv` | `grid, recordID, zh` | 开场 PV 字幕（Video_Subs） |
| `zh_static.tsv` | `count, enUS, zh` | 序列化 UI 文本（菜单/按钮/教程等） |
| `zh_code.tsv` | `enUS, zh` | 写在 C# 代码里的玩家可见文本 |
| `zh_aliases.tsv` | `enUS, zh` | 角色名/称谓（Speaker 名字） |
| `zh_hud.tsv` | `enUS, zh` | 操作提示等运行时 HUD 文本 |
| `zh_subtitles.tsv` | `enUS, zh` | Timeline 过场字幕（绘本旁白等，按原文精确匹配） |
| `glossary_zh.md` | Markdown | 主术语表（人名/地名/专有名词） |
| `glossary_prologue_add.md` | Markdown | 序章新增术语与说明 |

> `zh_needed_1_5.tsv`、`zh_prologue_static.tsv` 等中间文件不提交到仓库。

## 硬性规则

1. **UTF-8 编码，Tab 分隔**（BOM 可有可无）。
2. **不要改动 key 列**（`grid`、`recordID`、`enUS`）；只填中文列。
3. **保留原文的富文本标签**，且顺序/数量一致，例如：
   - `<b>…</b>`、`<i>…</i>`、`<u>…</u>`
   - `<color=#RRGGBB>…</color>`、`<size=…>`
   - `<sprite=…>` / `<sprite="…" name="…">`
   - `<shake>…</shake>`、`<wave>…</wave>`
4. **保留换行数量**：原文的 `\n`（字面量反斜杠+n）在译文中保持同样数量，不要写成真实换行。
5. **保留占位符**：`{0}`、`{1}`、`%s`、`%d` 等必须原样保留（数量/顺序一致）。
6. 译文中的 Tab 会被替换为空格，文件里的真实换行会被转义成 `\n`。
7. 人名/地名先查术语表；新术语请补充进 `glossary_zh.md`。

## 添加一条翻译

**Gridly 文本**（有 `grid, recordID` 的）：

1. 从 `work/gridly_all.tsv` 找到要翻译的 `grid` 和 `recordID`（先按
   `docs/build-and-translate.md` 提取数据）；
2. 在对应章节文件（如 `zh_2_1.tsv`）追加一行：`grid<TAB>recordID<TAB>译文`；
3. 运行 `python tools/build_translation_data.py` 与 `python tools/check_translations.py`；
4. 安装补丁实机验证。

**硬编码/字幕文本**（按英文原文匹配）：

1. 在对应文件（`zh_static.tsv` / `zh_subtitles.tsv` / `zh_code.tsv` / `zh_hud.tsv`）追加
   `英文原文<TAB>译文`；
2. 同上运行构建与自检。

## 当前进度

- 已译：UI 全部、第 1.5 章、第 2.1 章、开场 PV 字幕、主要硬编码文本；
- 待译：第 2~8 章、恋爱线、诅咒记忆、结局等，见根目录 [TODO.md](../../TODO.md)。

## 许可

翻译文本以 **CC BY-NC-SA 4.0** 提供，仅供非商业粉丝汉化使用。
游戏原文版权归 AstralShift / Square Enix Collective 所有。
