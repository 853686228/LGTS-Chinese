# 从玩家自己的游戏安装中提取汉化工作所需的全部中间数据到 work/
#
# 需要先安装 Python 依赖：
#   pip install -r requirements.txt
#
# 用法：
#   pwsh -File scripts/extract_all.ps1
#   pwsh -File scripts/extract_all.ps1 -GameDir "D:\Games\Little Goody Two Shoes"
param(
    [string]$GameDir = $env:LGTS_GAME_DIR
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Little Goody Two Shoes"
}
if (-not (Test-Path (Join-Path $GameDir "LGTS_Data\data.unity3d"))) {
    Write-Error "找不到游戏数据：$GameDir（请用 -GameDir 指定，或设置 LGTS_GAME_DIR）"
    exit 1
}
$env:LGTS_GAME_DIR = $GameDir

$env:PYTHONUTF8 = "1"

$py = if ($env:PYTHON) { $env:PYTHON } else { "python" }

function Invoke-Step {
    param([string]$Title, [string]$Script, [string[]]$Args = @())
    Write-Host "==> $Title"
    & $py (Join-Path $repo "tools\$Script") @Args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Invoke-Step "解包 data.unity3d（≤16MB 的节点）" "extract_bundle.py" @("extract-small")
Invoke-Step "解码 Gridly 文本总表" "dump_project.py"
Invoke-Step "导出剧本 TextAsset" "extract_textassets.py"
Invoke-Step "统计硬编码 UI 文本" "extract_static_texts.py"
Invoke-Step "导出 Timeline 字幕" "extract_subtitles.py"

Write-Host ""
Write-Host "完成。产出在 work/："
Write-Host "  work/gridly_all.tsv      全部英文原文（翻译/校对用）"
Write-Host "  work/project_typetree.json"
Write-Host "  work/textassets/         1267 个剧本（含 Gridly key）"
Write-Host "  work/static_texts.tsv    序列化 UI 文本清单"
Write-Host "  work/subtitles.tsv       Timeline 过场字幕"
