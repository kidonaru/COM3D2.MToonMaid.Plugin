@echo off
chcp 65001
setlocal

set PLUGIN_NAME=COM3D2.MToonMaid.Plugin
set ASSEMBLY_NAME=COM3D2.MToonMaid

call .\source\%PLUGIN_NAME%\build.bat release all
if %ERRORLEVEL% neq 0 (
    echo ビルドに失敗しました
    exit /b 1
)

for /f "tokens=*" %%i in ('powershell -NoProfile -Command "$content = Get-Content 'source/%PLUGIN_NAME%/PluginInfo.cs'; $version = [regex]::Match($content, 'PluginVersion = \""(.*?)\""').Groups[1].Value; echo $version"') do set VERSION=%%i
if "%VERSION%"=="" (
    echo PluginInfo.cs から PluginVersion を読み取れませんでした
    exit /b 1
)
echo VERSION: %VERSION%

if exist output rmdir /s /q output

rem ============ 配布パッケージ (ゲームごとのフォルダの中身をゲームのルートに解凍する構成) ============
rem 片方を zip 直下の BepInEx に置くと、もう一方のゲームの利用者がいつもの手順で違う版を入れてしまう
md output\%PLUGIN_NAME%\COM3D2.5\BepInEx\plugins
copy /y "BepInEx\plugins\%ASSEMBLY_NAME%.dll" "output\%PLUGIN_NAME%\COM3D2.5\BepInEx\plugins\" >nul
if %ERRORLEVEL% neq 0 (
    echo dll のコピーに失敗しました
    exit /b 1
)
md output\%PLUGIN_NAME%\COM3D2\BepInEx\plugins
copy /y "BepInEx (COM3D2)\plugins\%ASSEMBLY_NAME%.dll" "output\%PLUGIN_NAME%\COM3D2\BepInEx\plugins\" >nul
if %ERRORLEVEL% neq 0 (
    echo COM3D2 版の dll のコピーに失敗しました
    exit /b 1
)
for %%G in (COM3D2.5 COM3D2) do (
    xcopy /e /i /y /q "BepInEx\config" "output\%PLUGIN_NAME%\%%G\BepInEx\config\" >nul
    if errorlevel 1 (
        echo %%G 版のプリセットのコピーに失敗しました
        exit /b 1
    )
)
copy /y "LICENSE-UniVRM.txt" "output\%PLUGIN_NAME%\" >nul

set README_TXT=output\%PLUGIN_NAME%\README.txt
echo このテキストはWeb上で見ることを推奨しています。 > %README_TXT%
echo https://github.com/kidonaru/%PLUGIN_NAME%/blob/main/README.md >> %README_TXT%
echo. >> %README_TXT%
echo. >> %README_TXT%
type README.md >> %README_TXT%

powershell -NoProfile Compress-Archive -Path "output\%PLUGIN_NAME%" -DestinationPath "output\%PLUGIN_NAME%-v%VERSION%.zip" -Force
if %ERRORLEVEL% neq 0 (
    echo zip の作成に失敗しました
    exit /b 1
)

rmdir /s /q output\%PLUGIN_NAME%

echo ビルドに成功しました
exit /b 0
