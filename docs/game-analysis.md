# 游戏数据分析：文本在哪里、怎么取出来

本文记录汉化前对游戏的逆向分析结论，供后续维护者参考。所有结论都来自对本地游戏文件的实测，
不包含游戏资源文件本体。

## 基本信息

| 项目 | 值 |
|---|---|
| 游戏 | Little Goody Two Shoes（Steam AppID **1812370**） |
| 引擎 | Unity **2021.3.24f1**，Mono x64 |
| 游戏构建 | Steam build **20802495** |
| 语言构建 | 英文（`enUS`） |
| 数据包 | `LGTS_Data/data.unity3d`，**6,476,759,553 字节** |

## 游戏目录结构

```
Little Goody Two Shoes/
├─ LGTS.exe
├─ LGTS_Data/
│  ├─ data.unity3d              ← 6.48 GB，所有场景/资源都在这个 UnityFS 包里
│  ├─ resources.resource        ← FMOD 音频 bank（文件头 FSB5）
│  ├─ sharedassets31.resource   ← WebM 视频数据（约 577 MB）
│  ├─ sharedassets51.resource   ← WebM 视频数据（约 58 MB）
│  ├─ Managed/                  ← Assembly-CSharp.dll 等托管程序集
│  └─ StreamingAssets/AssetBundles/  ← 额外 AssetBundle（贴图等）
└─ ...
```

注意：**没有** `resources.assets` / `level0` 这类散装文件——这个游戏把它们全部打包进了
`data.unity3d`（UnityFS v8，LZ4 压缩，146,126 个 128KB 块，解压后 19.15 GB，341 个节点）。

## 数据包内部

`data.unity3d` 的主要节点：

| 节点 | 大小 | 内容 |
|---|---:|---|
| `globalgamemanagers` | 1.32 MB | 全局配置、BuildSettings |
| `resources.assets` | **21,317,824 B** | **Gridly 文本总表 + 1,267 个剧本 TextAsset** |
| `resources.assets.resS` | 618 MB | resources 的贴图/音频流 |
| `level0` … `level114` | 共 115 个 | 场景 |
| `sharedassets*.assets` | 多个 | 各场景共享资源 |
| `sharedassets31.assets` / `sharedassets51.assets` | 0.35 / 0.47 MB | 过场 Timeline，含烘焙字幕 |

`resources.assets` 在未压缩流中的偏移是 **18,513,494,945**（大小 21,317,824），
基本在包的末尾。

## 文本的四种来源

### 1. Gridly 本地化总表（正文，17,196 条）

`resources.assets` → pathID **13625** 的 MonoBehaviour（`m_Name = "Project"`，**1,443,264 字节**，
脚本类 `Gridly.Project`）保存了全部结构化文本：

```
Project
 ├─ ProjectID
 ├─ grids: List<Grid>
 │   └─ Grid { nameGrid, records: List<Record> }
 │       └─ Record { recordID, columns: List<Column> }
 │           └─ Column { columnID ("enUS"/"ptPT"/"jaJP"…), text }
 └─ langSupports: List<LangSupport>
```

共 **19 张表 / 17,196 条记录 / 125,701 个英文词**：

| Grid | 记录数 | Grid | 记录数 |
|---|---:|---|---:|
| Dialogue | 7,702 | Items | 131 |
| Cutscenes | 4,384 | Quests | 106 |
| Chores | 1,656 | Love_Prompt | 54 |
| Love_Event | 1,361 | Tutorials | 51 |
| Flavour | 993 | World_Map | 42 |
| Cursed_Memories | 420 | Video_Subs | 27 |
| UI | 134 | Shops / Common | 22 / 22 |
| Hints / Images / Endings / Pickup | 10 / 1 / 20 / 60 | **合计** | **17,196** |

记录在二进制里是紧凑排列的：`recordID` 长度+字节 → 列数 → 每列 `columnID` 长度+字节 →
`text` 长度+字节。所以用十六进制工具在 `resources.assets` 里可以直接看到连续的英文句子，
但需要按结构解析才能得到「key → 文本」映射。

### 2. 剧本 TextAsset（只有 key）

`resources.assets` 里还有 **1,267 个 TextAsset**（3.33 MB），文件名如 `DLG_7_4_OLH_01`、
`CUT_...`、`FLV_...`，内容是 JSON 格式的演出/分支描述，其中的 `Text` 字段是 **Gridly key**
（如 `DLG_7_4_OLH_01_001`），真正的台词在 `Gridly.Project` 里按 key 查表。
因此**只解包 TextAsset 是看不到正文的**。

### 3. Timeline 过场字幕（烘焙在资产里）

绘本旁白、PV 歌词等字幕不在 Gridly 里，而是序列化在 Timeline 资产中：
`sharedassets31.assets` / `sharedassets51.assets` 里 `LGTS.Cinematics.Subtitles.SubtitleClip`
的 `subtitleText` 字段（共 50 条唯一文本，运行时会由 `SubtitleTrackMixer` 写入 TMP）。
这类文本需要按原文精确匹配翻译（见 `tools/extract_subtitles.py`）。

### 4. 硬编码 UI / 代码字符串

- 序列化在场景/预制体里的 TMP / `UnityEngine.UI.Text` 组件：全游戏共 **657 条唯一文本**
  （`tools/extract_static_texts.py` 可导出），大部分是制作人员名单、零散 UI；
- 写在 C# 里的玩家可见字符串：`Managed/Assembly-CSharp.dll`（1,386 个类型），
  反编译后可提取约 190 条候选（`tools/extract_code_literals.py`）。

## 字体系统

- 所有 TMP 字体都是 **静态 SDF 图集**（英文构建），不包含中文字形；
- `FontLocalization.GetFont(Languages, FontType)` 按语言返回字体；
- 正式版构建**剥离了 MonoBehaviour 的 TypeTree**，无法离线解析/修改字体资产里的字形表；
- 因此中文字形只能**运行时**用字体文件动态生成 TMP 字体（本仓库采用方案），
  或在 Unity Editor 里重做整套字体资产（不可行，因为拿不到工程）。

## 为什么 AssetStudio 里看不到文本

1. 文本不是 TextAsset，而是 `Gridly.Project` 这个 MonoBehaviour；
   在 AssetStudio 的 Asset List 里按 MonoBehaviour 搜 `Project` 可以看到它（大小 1,443,264），
   但 **TypeTree 被剥离**，工具无法展开 `grids → records → columns` 字段；
2. 1,267 个剧本 TextAsset 里只有 key，没有正文；
3. `data.unity3d` 是 UnityFS v8 特殊包，部分版本的 AssetStudio 对 v8 支持不佳；
4. `*.resource` 文件是音频（FSB5）和视频（WebM），不是文本。

要看正文，需要「TypeTreeGenerator 重新生成类型树 → 按类型树解码」，也就是本仓库
`tools/unity_helper.py` + `tools/dump_project.py` 做的事。

## 复现步骤

```powershell
# 1) 解包（仅解 ≤16MB 的节点，包括 resources.assets）
python tools\extract_bundle.py extract-small

# 2) 解码 Gridly 总表 → work\project_typetree.json + work\gridly_all.tsv
python tools\dump_project.py

# 3) 导出剧本、硬编码 UI、Timeline 字幕
python tools\extract_textassets.py
python tools\extract_static_texts.py
python tools\extract_subtitles.py
```

更完整的构建/翻译流程见 [build-and-translate.md](build-and-translate.md)。
