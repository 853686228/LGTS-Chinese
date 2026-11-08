"""仓库内所有工具共用的路径与配置。

约定：
  - 游戏安装目录通过环境变量 ``LGTS_GAME_DIR`` 指定（未设置时用默认 Steam 路径）
  - 从游戏提取的中间数据放在仓库根目录的 ``work/``（已被 .gitignore 忽略）
  - 构建产物放在 ``build/`` / ``dist/``（同样被忽略）
"""
import os
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
WORK = REPO / "work"
BUILD = REPO / "build"
DIST = REPO / "dist"

DATA = REPO / "data"
TRANSLATIONS = DATA / "translations"
FONTS = DATA / "fonts"
RESOURCES = REPO / "resources"

DEFAULT_GAME_DIR = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Little Goody Two Shoes")

UNPACKED = WORK / "unpacked"
TEXTASSETS = WORK / "textassets"


def game_dir() -> Path:
    """游戏安装目录（包含 LGTS.exe）。"""
    env = os.environ.get("LGTS_GAME_DIR")
    return Path(env) if env else DEFAULT_GAME_DIR


def data_dir() -> Path:
    return game_dir() / "LGTS_Data"


def unity3d() -> Path:
    return data_dir() / "data.unity3d"


def managed_dir() -> Path:
    return data_dir() / "Managed"


def bepinex_dir() -> Path:
    env = os.environ.get("LGTS_BEPINEX_DIR")
    return Path(env) if env else game_dir() / "BepInEx"


def ensure_dirs():
    for p in (WORK, BUILD, DIST, UNPACKED, TEXTASSETS):
        p.mkdir(parents=True, exist_ok=True)


def add_offline_pylibs():
    """把仓库内可选的 ``pylibs/``（离线依赖目录）加入 sys.path。"""
    candidates = [REPO / "pylibs"]
    env = os.environ.get("LGTS_PYLIBS")
    if env:
        candidates.append(Path(env))
    for p in candidates:
        if p.exists() and str(p) not in sys.path:
            sys.path.insert(0, str(p))


def check_game():
    """检查游戏目录是否可用，不可用时给出明确提示。"""
    u3d = unity3d()
    if not u3d.exists():
        raise SystemExit(
            "找不到游戏数据文件：\n  %s\n"
            "请设置环境变量 LGTS_GAME_DIR 指向《Little Goody Two Shoes》安装目录（包含 LGTS.exe）。\n"
            "例如：set LGTS_GAME_DIR=C:\\Program Files (x86)\\Steam\\steamapps\\common\\Little Goody Two Shoes"
            % u3d
        )
    if not (managed_dir() / "Assembly-CSharp.dll").exists():
        raise SystemExit("找不到 Managed/Assembly-CSharp.dll，游戏目录可能不完整：%s" % managed_dir())
    return u3d
