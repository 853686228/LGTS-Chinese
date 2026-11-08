"""打包汉化补丁：把插件、运行时翻译数据、字体和 BepInEx 加载器组装成可安装 zip。

前置：
  1. 编译插件：dotnet build src/LGTSChinese/LGTSChinese.csproj -c Release
     生成的 LGTSChinese.dll 在 src/LGTSChinese/bin/Release/（或复制到 build/）
  2. 合并翻译：python tools/build_translation_data.py
  3. 准备 BepInEx：下载 BepInEx 5.4.23.6 x64 压缩包并解压到 vendor/bepinex/
     （目录内应有 winhttp.dll、doorstop_config.ini、BepInEx/core/...）

用法：
  python tools/build_patch.py
  python tools/build_patch.py --bepinex vendor/bepinex --plugin build/LGTSChinese.dll
"""
import argparse
import hashlib
import os
import shutil
import zipfile
from pathlib import Path

import paths

VERSION = "0.2.0"
PACKAGE_NAME = "LGTS_Chinese_v%s" % VERSION


def require(path, message):
    if not path.exists():
        raise SystemExit("%s\n  缺少：%s" % (message, path))


def copy_file(src, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(str(src), str(dst))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--bepinex", default=str(paths.REPO / "vendor" / "bepinex"),
                        help="BepInEx 解压目录（含 winhttp.dll 与 BepInEx/core）")
    parser.add_argument("--plugin", default=str(paths.BUILD / "LGTSChinese.dll"),
                        help="编译出的 LGTSChinese.dll")
    parser.add_argument("--out-dir", default=str(paths.DIST), help="输出目录")
    args = parser.parse_args()

    bepinex = Path(args.bepinex) if os.path.isabs(args.bepinex) else paths.REPO / args.bepinex
    plugin = Path(args.plugin) if os.path.isabs(args.plugin) else paths.REPO / args.plugin
    out_dir = Path(args.out_dir) if os.path.isabs(args.out_dir) else paths.REPO / args.out_dir

    for name, p, msg in [
        ("BepInEx", bepinex, "没有找到 BepInEx 目录，请把 BepInEx_win_x64_5.4.23.6 解压到 vendor/bepinex/"),
        ("BepInEx/core", bepinex / "BepInEx" / "core", "BepInEx 目录结构不完整"),
        ("插件 DLL", plugin, "请先编译插件（scripts/build.ps1 或 dotnet build）"),
        ("zh_gridly.tsv", paths.BUILD / "zh_gridly.tsv", "请先运行 python tools/build_translation_data.py"),
        ("zh_static.tsv", paths.BUILD / "zh_static.tsv", "请先运行 python tools/build_translation_data.py"),
        ("字体", paths.FONTS / "NotoSerifSC-Regular.otf", "缺少 data/fonts/NotoSerifSC-Regular.otf"),
        ("安装说明", paths.RESOURCES / "安装说明.txt", "缺少 resources/安装说明.txt"),
        ("安装脚本", paths.RESOURCES / "安装.bat", "缺少 resources/安装.bat"),
        ("卸载脚本", paths.RESOURCES / "卸载.bat", "缺少 resources/卸载.bat"),
        ("配置模板", paths.RESOURCES / "config.cfg", "缺少 resources/config.cfg"),
    ]:
        require(p, msg)

    out_dir.mkdir(parents=True, exist_ok=True)
    stage = paths.BUILD / "package" / PACKAGE_NAME
    if stage.exists():
        shutil.rmtree(stage)
    stage.mkdir(parents=True)

    # BepInEx 加载器
    copy_file(bepinex / "winhttp.dll", stage / "winhttp.dll")
    copy_file(bepinex / "doorstop_config.ini", stage / "doorstop_config.ini")
    if (bepinex / ".doorstop_version").exists():
        copy_file(bepinex / ".doorstop_version", stage / ".doorstop_version")
    shutil.copytree(str(bepinex / "BepInEx" / "core"), str(stage / "BepInEx" / "core"))

    # 插件与数据
    plugin_dir = stage / "BepInEx" / "plugins" / "LGTSChinese"
    plugin_dir.mkdir(parents=True)
    copy_file(plugin, plugin_dir / "LGTSChinese.dll")
    copy_file(paths.BUILD / "zh_gridly.tsv", plugin_dir / "zh_gridly.tsv")
    copy_file(paths.BUILD / "zh_static.tsv", plugin_dir / "zh_static.tsv")
    copy_file(paths.FONTS / "NotoSerifSC-Regular.otf", plugin_dir / "fonts" / "NotoSerifSC-Regular.otf")
    copy_file(paths.FONTS / "OFL.txt", plugin_dir / "fonts" / "OFL.txt")

    # 配置与脚本
    copy_file(paths.RESOURCES / "config.cfg", stage / "BepInEx" / "config" / "com.syukuu.lgts.zhcn.cfg")
    copy_file(paths.RESOURCES / "安装.bat", stage / "安装.bat")
    copy_file(paths.RESOURCES / "卸载.bat", stage / "卸载.bat")
    copy_file(paths.RESOURCES / "安装说明.txt", stage / "安装说明.txt")

    # 打 zip
    zip_path = out_dir / (PACKAGE_NAME + ".zip")
    if zip_path.exists():
        zip_path.unlink()
    file_count = 0
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for root, _dirs, files in os.walk(stage):
            for name in files:
                full = Path(root) / name
                rel = full.relative_to(stage)
                zf.write(str(full), str(rel))
                file_count += 1

    digest = hashlib.sha256(zip_path.read_bytes()).hexdigest()
    print("补丁包：%s" % zip_path)
    print("文件数：%d，大小：%.1f MB" % (file_count, zip_path.stat().st_size / 1024 / 1024))
    print("SHA-256：%s" % digest)


if __name__ == "__main__":
    main()
