@echo off
chcp 65001 >nul
setlocal

rem シェーダーバンドルのビルド (実体は build-bundle.ps1)。引数 = com3d25 / com3d2 / all (既定 all)
set TARGET=%~1
if "%TARGET%"=="" set TARGET=all
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-bundle.ps1" -Target %TARGET%
if %ERRORLEVEL% neq 0 (
    echo シェーダーバンドルのビルドに失敗しました
    exit /b 1
)

exit /b 0
