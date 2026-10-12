@echo off
chcp 65001 >nul
setlocal

rem 引数はそのまま deploy.ps1 に渡す (-SkipBuild / -DryRun)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1" %*
if %ERRORLEVEL% neq 0 (
    echo デプロイに失敗しました
    exit /b 1
)

exit /b 0
