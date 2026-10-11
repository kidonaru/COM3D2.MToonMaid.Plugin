@echo off
chcp 65001 >nul
setlocal

rem ビルドとコピー (実体は build.ps1)。引数 1 = debug / release (既定 release)、引数 2 = com3d25 / com3d2 / all (既定 all)
set CONFIG=%~1
if "%CONFIG%"=="" set CONFIG=release
set TARGET=%~2
if "%TARGET%"=="" set TARGET=all
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Config %CONFIG% -Target %TARGET%
if %ERRORLEVEL% neq 0 (
    echo ビルドに失敗しました
    exit /b 1
)

exit /b 0
