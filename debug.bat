@echo off
chcp 65001
setlocal

rem 引数: %1 = com3d2/com3d25/all (既定 com3d25)
set TARGET=%~1
if "%TARGET%"=="" set TARGET=com3d25

call .\source\COM3D2.MToonMaid.Plugin\build.bat debug %TARGET%
if %ERRORLEVEL% neq 0 (
    echo ビルドに失敗しました
    exit /b 1
)

echo ビルドに成功しました
exit /b 0
