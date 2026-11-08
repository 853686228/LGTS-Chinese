# 构建与翻译指南

## 环境要求

- Windows 10/11
- [.NET SDK](https://dotnet.microsoft.com/download) 8.0 或更高（编译 net472 目标）
- Python 3.9+（提取数据/合并翻译/打包）；提取工具还需要 UnityPy 等依赖：`pip install -r requirements.txt`
- 已安装游戏《Little Goody Two Shoes》（Steam）
- BepInEx 5.4.23.6 x64（打包时需要）

## 设置游戏目录

所有工具通过环境变量 `LGTS_GAME_DIR` 找游戏：

```powershell
$env:LGTS_GAME_DIR = "C:\Program Files (x86)\Steam\steamapps\common\Little Goody Two Shoes"
$env:LGTS_BEPINEX_DIR = "$env:LGTS_GAME_DIR\BepInEx"   # 可选，编译插件时需要
```

不设置时默认使用上面的 Steam 路径。

## 编译插件

```powershell
dotnet build src\LGTSChinese\LGTSChinese.csproj -c Release
# 输出：src\LGTSChinese\bin\Release\LGTSChinese.dll
```

插件引用游戏 `Managed/` 下的程序集和 BepInEx 的 DLL，所以二者必须存在。
`csproj` 里 `FrameworkPathOverride` 指向 Windows 自带的 .NET Framework 4.x 引用程序集
（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319`），无需联网还原 NuGet 包。

## 准备 BepInEx（打包用）

从 BepInEx 官方 release 下载 `BepInEx_win_x64_5.4.23.6.zip`，解压到：

```
vendor/bepinex/
├─ winhttp.dll
├─ doorstop_config.ini
├─ .doorstop_version
└─ BepInEx/core/...
```

`vendor/` 已在 `.gitignore` 中忽略，不会提交到仓库。

## 一键构建

```powershell
pwsh -File scripts\build.ps1
```

脚本依次执行：

1. `dotnet build` 编译插件，并把 DLL 复制到 `build\LGTSChinese.dll`；
2. `tools/build_translation_data.py` 合并 `data/translations/*.tsv` →
   `build\zh_gridly.tsv`、`build\zh_static.tsv`；
3. `tools/check_translations.py` 自检（不阻断）；
4. `tools/build_patch.py` 组装补丁并输出 `dist\LGTS_Chinese_v0.2.0.zip`。

## 从游戏提取数据（贡献翻译/维护时需要）

```powershell
pip install -r requirements.txt
pwsh -File scripts\extract_all.ps1
```

产物（`work/` 已忽略，不会提交）：

| 文件 | 说明 |
|---|---|
| `work/project_typetree.json` | Gridly 总表的完整结构化导出 |
| `work/gridly_all.tsv` | 全部英文原文：`grid, recordID, enUS` |
| `work/textassets/` | 1,267 个剧本文件（其中 `Text` 是 key） |
| `work/static_texts.tsv` | 场景/预制体里的 TMP/UI Text 文本 |
| `work/subtitles.tsv` | Timeline 过场字幕 |

## 翻译文件格式

见 [data/translations/README.md](../data/translations/README.md)。核心规则：

- 文件是 **UTF-8（带/不带 BOM 均可）、Tab 分隔**；
- 保留原文里的所有富文本标签（`<b>`、`<i>`、`<color=...>`、`<sprite=...>`、`<shake>` 等）、
  `{0}`/`{1}` 占位符和 `\n` 换行数量；
- 译文中的真实换行请写成字面量 `\n`（两字符），不要写文件换行；
- 人名/术语先查 [`glossary_zh.md`](../data/translations/glossary_zh.md)，
  新增术语请补进去。

## 翻译自检

```powershell
python tools\build_translation_data.py
python tools\check_translations.py            # 查看问题列表
python tools\check_translations.py --strict   # CI 用，有问题返回 1
```

如果 `work/gridly_all.tsv` 存在（先跑过提取），自检会把每条译文和英文原文对比
富文本标签、`<sprite>`、`\n` 数量、占位符。

## 实机验证

1. 安装补丁后启动游戏；
2. 插件目录下 `BepInEx\plugins\LGTSChinese\trace.log` 可查看注入统计
   （需要配置 `[Debug] TraceToFile = true`）；
3. 游戏日志：`BepInEx\LogOutput.log`、`%USERPROFILE%\AppData\LocalLow\AstralShift\Little Goody Two Shoes\Player.log`；
4. 开发期可用插件内 `AutoTest` / `AutoScript` 自动截图（默认关闭，见 `[Debug]` 配置）。

## 常见问题

| 现象 | 处理 |
|---|---|
| 编译报找不到 `Assembly-CSharp.dll` | 设置 `LGTS_GAME_DIR`，确认 `LGTS_Data\Managed` 存在 |
| 编译报找不到 `BepInEx.dll` | 先在游戏目录安装 BepInEx，或设置 `LGTS_BEPINEX_DIR` |
| `python tools\...` 报 `ModuleNotFoundError: UnityPy` | `pip install -r requirements.txt` |
| 打包报缺少 BepInEx | 把 BepInEx 压缩包解压到 `vendor/bepinex/` |
| 游戏启动黑屏/崩溃 | 把 `[Debug] PatchGroups` 改为 `none` 逐步排查；查看 BepInEx 日志 |
| 界面仍显示英文 | 确认 `EnableChinese = true`；确认翻译数据在插件目录；查看 trace 统计 |
