@echo off
chcp 65001 >nul
setlocal

set "DEFAULT=C:\Program Files (x86)\Steam\steamapps\common\Little Goody Two Shoes"
set /p GAME=请输入游戏安装目录（直接回车 = 默认 Steam 目录）:
if "%GAME%"=="" set "GAME=%DEFAULT%"

if not exist "%GAME%\LGTS.exe" (
    echo [错误] 在以下路径没有找到 LGTS.exe：%GAME%
    pause
    exit /b 1
)

echo 正在卸载汉化补丁（只删除补丁文件，不动游戏本体）...
del /F /Q "%GAME%\winhttp.dll" 2>nul
del /F /Q "%GAME%\doorstop_config.ini" 2>nul
del /F /Q "%GAME%\.doorstop_version" 2>nul
if exist "%GAME%\BepInEx" rmdir /S /Q "%GAME%\BepInEx"

echo.
echo [完成] 补丁已卸载，游戏恢复原版。
pause
exit /b 0
