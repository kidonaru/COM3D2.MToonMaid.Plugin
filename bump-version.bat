@echo off
chcp 65001 >nul
setlocal

rem 引数: %1 = major/minor/patch/build
rem   major : 1.7.0.1 -> 2.0.0.0
rem   minor : 1.7.0.1 -> 1.8.0.0
rem   patch : 1.7.0.1 -> 1.7.1.0
rem   build : 1.7.0.1 -> 1.7.0.2

rem set と展開は必ずクォートする ※&や|を含む引数が cmd に解釈されて実行されるのを防ぐ
set "PART=%~1"
if "%PART%"=="" (
    echo 使い方: bump-version.bat [major^|minor^|patch^|build]
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0bump-version.ps1" "%PART%"
if %ERRORLEVEL% neq 0 (
    echo バージョンの更新に失敗しました
    exit /b 1
)

exit /b 0
