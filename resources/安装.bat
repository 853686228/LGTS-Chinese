@echo off
chcp 65001 >nul
setlocal

echo ============================================================
echo  Little Goody Two Shoes 简体中文汉化补丁 安装程序
echo ============================================================
echo.

set "DEFAULT=C:\Program Files (x86)\Steam\steamapps\common\Little Goody Two Shoes"
set /p GAME=请输入游戏安装目录（含 LGTS.exe，直接回车 = 默认 Steam 目录）:
if "%GAME%"=="" set "GAME=%DEFAULT%"

if not exist "%GAME%\LGTS.exe" (
    echo [错误] 在以下路径没有找到 LGTS.exe：
    echo        %GAME%
    echo 请确认游戏已安装，或手动输入正确目录。
    pause
    exit /b 1
)

echo.
echo 目标目录：%GAME%
echo 开始复制文件（如提示权限不足，请右键本文件“以管理员身份运行”）...
echo.

copy /Y "%~dp0winhttp.dll" "%GAME%\" >nul || goto :fail
copy /Y "%~dp0doorstop_config.ini" "%GAME%\" >nul || goto :fail
copy /Y "%~dp0.doorstop_version" "%GAME%\" >nul || goto :fail
if not exist "%GAME%\BepInEx" mkdir "%GAME%\BepInEx"
xcopy /E /I /Y "%~dp0BepInEx\core" "%GAME%\BepInEx\core\" >nul || goto :fail
xcopy /E /I /Y "%~dp0BepInEx\plugins" "%GAME%\BepInEx\plugins\" >nul || goto :fail
if not exist "%GAME%\BepInEx\config" mkdir "%GAME%\BepInEx\config"
copy /Y "%~dp0BepInEx\config\com.syukuu.lgts.zhcn.cfg" "%GAME%\BepInEx\config\" >nul || goto :fail

echo.
echo [成功] 汉化补丁安装完成！启动游戏即可。
echo.
echo   切换回英文：用记事本打开
echo   %GAME%\BepInEx\config\com.syukuu.lgts.zhcn.cfg
echo   把 EnableChinese = true 改为 false，重启游戏。
echo.
pause
exit /b 0

:fail
echo.
echo [失败] 复制文件时出错，请右键本文件“以管理员身份运行”后重试。
pause
exit /b 1
