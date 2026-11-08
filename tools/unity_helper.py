"""UnityEngine 资源读取辅助：UnityPy + TypeTreeGenerator。

正式版游戏构建剥离了 MonoBehaviour 的 TypeTree，因此必须用
TypeTreeGeneratorAPI 从游戏自带的 Assembly-CSharp.dll 生成字段类型树，
才能把 Gridly.Project / TMP / Timeline 等行为组件的字段解码出来。

另外 TypeTreeGenerator 生成的容器节点（如 List<string>）的 m_Type 是元素类型，
UnityPy 原版 read_value 会把它当单值读取。本模块修正了该行为。
"""
from __future__ import annotations

import sys
from pathlib import Path

import paths  # noqa: F401  (tools/ 在 sys.path 中；由脚本直接运行)

paths.add_offline_pylibs()

import UnityPy  # noqa: E402
from UnityPy.helpers import TypeTreeHelper as TTH  # noqa: E402
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator  # noqa: E402

_orig_read_value = TTH.read_value


def _is_container_node(node) -> bool:
    if not node.m_Children:
        return False
    if node.m_Children[0].m_Type != "Array":
        return False
    array_children = node.m_Children[0].m_Children
    if array_children is None or len(array_children) < 2:
        return False
    return array_children[1].m_Type != "char"


def _read_value_fixed(node, reader, config):
    align = TTH.metaflag_is_aligned(node.m_MetaFlag)

    if _is_container_node(node):
        if TTH.metaflag_is_aligned(node.m_Children[0].m_MetaFlag):
            align = True
        size = reader.read_int()
        if size < 0:
            raise ValueError("Negative length read from TypeTree")
        subtype = node.m_Children[0].m_Children[1]
        if TTH.metaflag_is_aligned(subtype.m_MetaFlag):
            value = TTH.read_value_array(subtype, reader, config, size)
        else:
            value = [TTH.read_value(subtype, reader, config) for _ in range(size)]
        if align:
            reader.align_stream()
        return value

    return _orig_read_value(node, reader, config)


TTH.read_value = _read_value_fixed
# 编译版 boost 读取器对生成出来的节点类型分发有 bug，这里禁用走纯 Python 实现
TTH.read_typetree_boost = None


def make_env(unpack_dir: Path | str | None = None):
    """加载解包目录（默认 work/unpacked），并挂上 TypeTreeGenerator。"""
    unpack_dir = Path(unpack_dir) if unpack_dir else paths.UNPACKED
    required = ["resources.assets", "globalgamemanagers.assets"]
    missing = [name for name in required if not (unpack_dir / name).exists()]
    if missing:
        raise SystemExit(
            "work/unpacked 缺少 %s，脚本无法解析 MonoBehaviour 的脚本类型。\n"
            "请先解出全部小节点：\n  python tools/extract_bundle.py extract-small"
            % "、".join(missing)
        )
    env = UnityPy.load(str(unpack_dir))
    gen = TypeTreeGenerator("2021.3.24f1")
    gen.load_local_dll_folder(str(paths.managed_dir()))
    env.typetree_generator = gen
    return env


def find_file(env, suffix: str):
    """按文件名后缀（如 'resources.assets'）取序列化文件。"""
    for key, value in env.files.items():
        if key.replace("\\", "/").endswith(suffix):
            return value
    raise SystemExit("在解包目录中找不到 %s，请先运行 tools/extract_bundle.py" % suffix)


def script_fullname(mb):
    """返回 (程序集名, 完整类型名)。"""
    script = mb.m_Script.deref_parse_as_object()
    namespace = script.m_Namespace or ""
    return script.m_AssemblyName, (namespace + "." if namespace else "") + script.m_ClassName
