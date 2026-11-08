# 一键构建：编译插件 -> 合并翻译 -> 打包补丁 zip
#
# 用法：
#   pwsh -File scripts/build.ps1
#   pwsh -File scripts/build.ps1 -GameDir "D:\Games\Little Goody Two Shoes"
#   pwsh -File scripts/build.ps1 -SkipData
#
# 环境要求：Windows + .NET SDK 8（或更高）+ Python 3.9+
# 首次打包前需要把 BepInEx_win_x64_5.4.23.6.zip 解压到 vendor/bepinex/
param(
    [string]$GameDir = $env:LGTS_GAME_DIR,
    [switch]$SkipData,
    [switch]$SkipPatch
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

if (-not $GameDir) {
    $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Little Goody Two Shoes"
}
if (-not (Test-Path (Join-Path $GameDir "LGTS.exe"))) {
    Write-Error "找不到游戏：$GameDir（请用 -GameDir 指定，或设置 LGTS_GAME_DIR）"
    exit 1
}
$env:LGTS_GAME_DIR = $GameDir

$env:PYTHONUTF8 = "1"

$py = if ($env:PYTHON) { $env:PYTHON } else { "python" }

Write-Host "==> [1/3] 编译插件 (LGTSChinese.csproj)"
dotnet build (Join-Path $repo "src\LGTSChinese\LGTSChinese.csproj") -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$buildDir = Join-Path $repo "build"
New-Item -ItemType Directory -Force $buildDir | Out-Null
Copy-Item (Join-Path $repo "src\LGTSChinese\bin\Release\LGTSChinese.dll") (Join-Path $buildDir "LGTSChinese.dll") -Force
Write-Host "    插件 DLL 已复制到 build\LGTSChinese.dll"

if (-not $SkipData) {
    Write-Host "==> [2/3] 合并翻译数据"
    & $py (Join-Path $repo "tools\build_translation_data.py")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "==> [2.5/3] 翻译数据自检（不阻断构建）"
    & $py (Join-Path $repo "tools\check_translations.py")
}

if ($SkipPatch) {
    Write-Host "已跳过打包（-SkipPatch）"
    exit 0
}

$bepinex = Join-Path $repo "vendor\bepinex"
if (-not (Test-Path (Join-Path $bepinex "winhttp.dll"))) {
    Write-Host "提示：未找到 vendor\bepinex，跳过打包。"
    Write-Host "      请下载 BepInEx_win_x64_5.4.23.6.zip 并解压到 vendor\bepinex\（内含 winhttp.dll 与 BepInEx\core）"
    exit 0
}

Write-Host "==> [3/3] 打包补丁"
& $py (Join-Path $repo "tools\build_patch.py")
exit $LASTEXITCODE
