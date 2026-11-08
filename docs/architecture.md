# 实现架构

本文说明 v0.2.0 汉化补丁的完整实现。代码对应 [`src/LGTSChinese/`](../src/LGTSChinese/)。

## 总览

游戏是 **Unity 2021.3.24f1 + Mono**，对 BepInEx 5 完全兼容。补丁不修改任何游戏资源文件，
而是在运行时做三层注入：

1. **Gridly 数据层**：给游戏的本地化表内存注入 `zhCN` 语言列；
2. **文本显示层**：用 Harmony 拦截 TMP 文本/字幕/硬编码字符串，用字典做精确翻译；
3. **字体层**：运行时用随包字体生成 TMP 动态字体，挂到所有字体资产的 fallback 链。

```
┌──────────────────────── 启动加载链 ────────────────────────┐
│ LGTS.exe                                                  │
│  └─ winhttp.dll（BepInEx 的 UnityDoorstop 代理）            │
│      └─ BepInEx.Preloader → Chainloader → LGTSChinese.dll │
└──────────────────────────┬─────────────────────────────────┘
                           ▼
┌──────────────────── LGTSChinese（运行时） ──────────────────┐
│ Awake()                                                   │
│  ├─ LocData 读取 plugins/LGTSChinese/*.tsv                │
│  ├─ 按配置注册 Harmony 补丁                                │
│  └─ 订阅 SceneManager.sceneLoaded                         │
└──────────────────────────┬─────────────────────────────────┘
                           ▼
┌──────────────────── 游戏自身的本地化/字体系统 ─────────────┐
│ Gridly：Project → grids → records → columns[语言]          │
│ TMP 文本（静态 SDF 图集）、FontLocalization、Timeline 字幕 │
│ 剧本 TextAsset（只有 key）、硬编码 UI/代码字符串           │
└───────────────────────────────────────────────────────────┘
```

## 1. 数据注入：Gridly.Project

游戏的本地化总表是 `resources.assets` 里的 `Gridly.Project` ScriptableObject
（1.44 MB，19 张表 / 17,196 条记录 / 每条记录只有 `enUS` 列）。

`Plugin.ApplyProject()` 在 `Gridly.Project.Init()` 的 Harmony Postfix 中执行：

- 遍历所有 `grids → records`，把 `zh_gridly.tsv` 里的译文追加为 `zhCN` 列；
  没有译文的记录回退英文原文（避免游戏返回 `"..."`）；
- 往 `project.langSupports` 的第 0 位插入 `zhCN` 语言项；
- 通过反射设置私有的 `chosenLangCodeName = "zhCN"`。

为什么用「注入列」而不是「拦截取文本函数」：

- 走游戏原生 `GridlyLocal.GetStringData` 链路，所有 `Gridly.Translator` 组件、
  对话窗口、字幕、任务、商店等自动生效，不需要逐个组件打补丁；
- 游戏内部依赖 `targetLanguage` 的逻辑（语言相关刷新等）也能看到中文；
- 语言切换（将来接入）只需要调整 `langSupports` 顺序即可。

> 注意：`Project.Init()` 会被游戏反复调用（`Resources.Load` 未就绪时返回 null），
> 所以在 Postfix 里必须通过反射读取私有静态字段 `_singleton`，**不能调用 `Project.singleton`**
> （会递归触发 Init，实测导致原生栈溢出崩溃）。

## 2. 文本兜底：Gridly 之外的文本

游戏里还有几类文本不走 Gridly，分别由 `Patches.cs` 中的补丁处理：

| 文本来源 | 例子 | 补丁 |
|---|---|---|
| 场景/预制体中序列化的 TMP 文本 | 菜单标签、HUD、按钮 | 场景加载后多次扫描 `FindObjectsOfType<TMP_Text>`，按 `zh_static.tsv` 精确替换 |
| 运行时赋值的 TMP 文本 | 动态提示、弹窗 | `TMP_Text.set_text` setter 前缀：值命中字典就替换 |
| 旧版 UI Text | 少量 `UnityEngine.UI.Text` | `UnityEngine.UI.Text.set_text` 前缀 |
| 剧本里直写的英文句子 | 少量未进 Gridly 的台词 | `LocalizationMediator.GetTranslation`（含 `ref string` 重载） |
| Timeline 过场字幕 | 绘本旁白、PV 歌词 | `SubtitleTrackMixer` 每帧写 `TMP_Text.text` → 被 setter 钩子截获翻译 |
| 角色名/称谓 | Elise、Father Hans、Old Hag… | `EntityDataManager.GetEntityAlias` 后缀 + 别名表 |
| 字体选择 | 对话/UI 按语言选字体 | `FontLocalization.GetFont` 前缀返回中文字体 |

### 补丁组开关

`[Debug] PatchGroups` 可按组开关，便于排查：

```
proj    Gridly 注入          trans   LocalizationMediator 钩子
alias   角色名钩子            menu    设置菜单语言选项
lang    语言切换              font    字体选择
text    TMP/Text setter 钩子   all/none
```

## 3. 字体

游戏所有 TMP 字体都是 **静态 SDF 图集，没有中文字形**，且正式版构建剥离了 TypeTree，
无法离线往字体资产里加字形。因此：

```csharp
var font = new Font(@".../fonts/NotoSerifSC-Regular.otf");   // 从文件路径加载
var asset = TMP_FontAsset.CreateFontAsset(
    font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
    AtlasPopulationMode.Dynamic, true);                       // 动态多图集
// 挂到所有已加载字体资产的 fallbackFontAssetTable
```

- `FontLocalization.GetFont` 补丁让对话/UI 直接使用思源宋体；
- 其他字体资产通过 fallback 兜底，**英文原文仍然使用游戏原字体**（保留原版排版观感）；
- 字体文件随包分发（`data/fonts/NotoSerifSC-Regular.otf`，SIL OFL 1.1）。

## 4. 语言切换

设置界面原本只有 1 个语言选项（英文构建的 `SettingsManager.Language` 枚举只有一项）。
`ConfigMenuHome.Start` 后置补丁会往语言选择控件追加「简体中文」选项，`SettingsManager.SetLanguage`
前缀负责切到 `zhCN` / 恢复 `enUS`；配置 `EnableChinese=false` 则强制英文模式。

当前正式发布仍以配置文件切换为主，游戏内切换是 P1 待办（见 TODO）。

## 5. 调试/QA 工具（插件内）

`AutoTest.cs` / `AutoScript.cs` / `InputSim.cs` 是开发期使用的自动化 QA：

- `AutoTest`：用 Unity InputSystem 模拟按键 + `ScreenCapture` 定时截图；
- `AutoScript`：自动从标题界面打开设置菜单截图、开新游戏、按键推进剧情并截图；
- 默认配置关闭，不影响正常玩家。

`Trace()` 调试日志默认不写文件，`[Debug] TraceToFile = true` 时写入插件目录 `trace.log`。

## 6. 设计取舍

| 方案 | 评价 |
|---|---|
| 运行时 BepInEx 插件（本方案） | ✅ 体积小、可迭代、可切换、不碰 6.5 GB 资源包 |
| 重打包 `data.unity3d` 替换文本 | 补丁极大、重建风险高，不采用 |
| XUnity 运行时机翻 | 需要联网翻译服务、译文质量不可控，不采用 |

## 7. 兼容性风险

- 补丁大量依赖游戏方法签名（`GridlyLocal.GetStringData`、`Project.Init`、TMP 内部方法等），
  游戏更新后可能失效；插件日志和 `PatchGroups` 可辅助定位。
- 字体替换依赖游戏 `FontLocalization` 结构；若更新后字段变化需要同步调整。
- 只针对 Steam 版 build 20802495 测试。
