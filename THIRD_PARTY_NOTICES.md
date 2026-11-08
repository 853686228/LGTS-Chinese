# 第三方组件与许可

本仓库代码以 MIT 许可发布（见 [LICENSE](LICENSE)）。仓库/补丁包中包含或依赖以下第三方组件：

## BepInEx 5.4.23.6

- 用途：Unity Mono 游戏的插件加载器（补丁包内分发 `winhttp.dll`、`doorstop_config.ini`、`BepInEx/core/`）
- 许可：LGPL-2.1
- 源码：https://github.com/BepInEx/BepInEx
- 说明：BepInEx 以 LGPL-2.1 分发，补丁包中未修改其二进制；如需替换/调试可从官方仓库获取对应源码。

## HarmonyX（0Harmony）

- 用途：运行时 Harmony 补丁（随 BepInEx 分发）
- 许可：MIT
- 源码：https://github.com/BepInEx/HarmonyX

## Noto Serif SC（思源宋体）

- 用途：汉化中文字体（`data/fonts/NotoSerifSC-Regular.otf`，随补丁包分发）
- 许可：SIL Open Font License 1.1（见 `data/fonts/OFL.txt`）
- 来源：https://github.com/notofonts/noto-cjk

## 开发期工具依赖（不随补丁分发）

以下 Python 包仅用于从游戏提取数据/维护翻译，遵循各自许可证：

| 包 | 用途 | 许可 |
|---|---|---|
| UnityPy | 读取 Unity 序列化文件 | MIT |
| TypeTreeGeneratorAPI | 从游戏程序集生成 TypeTree | 见其仓库（AssetRipper 项目） |
| lz4 | UnityFS 块解压 | BSD-3-Clause |
| brotli | UnityFS 块解压（可选） | MIT |
| Pillow | 截图像素处理/文档图片 | HPND |
| dnfile | .NET 元数据读取（可选） | MIT |

## 游戏版权

《Little Goody Two Shoes》及其全部文本、美术、音频版权归 **AstralShift / Square Enix Collective** 所有。

本仓库**不包含**游戏本体资源（`data.unity3d`、程序集、贴图等），只包含：
自制插件代码、自制翻译文本、开源字体、以及从公开渠道获取的 BepInEx 加载器（仅在补丁包中分发）。
汉化补丁为免费开源作品，仅供学习交流，请支持正版游戏，禁止任何形式的倒卖。
