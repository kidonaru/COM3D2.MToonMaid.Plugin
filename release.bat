@echo off
chcp 65001 >nul
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

rem ============ 配布パッケージ ============
rem zip 直下の BepInEx を 2.5 用にしないのは、PostEffects (UnityInjector = 2.0 用 / UnityInjector (COM3D2.5) = 2.5 用) と対応をそろえるため
rem リポジトリ内のビルド出力は名前の対応が逆 (BepInEx = 2.5 用) なので、コピー元とコピー先を並べて取り違えを防ぐ
set SRC_COM3D2=BepInEx (COM3D2)
set SRC_COM3D25=BepInEx
set PKG_COM3D2=output\%PLUGIN_NAME%\BepInEx
set PKG_COM3D25=output\%PLUGIN_NAME%\BepInEx (COM3D2.5)
md "%PKG_COM3D2%\plugins"
copy /y "%SRC_COM3D2%\plugins\%ASSEMBLY_NAME%.dll" "%PKG_COM3D2%\plugins\" >nul
if %ERRORLEVEL% neq 0 (
    echo COM3D2 版の dll のコピーに失敗しました
    exit /b 1
)
md "%PKG_COM3D25%\plugins"
copy /y "%SRC_COM3D25%\plugins\%ASSEMBLY_NAME%.dll" "%PKG_COM3D25%\plugins\" >nul
if %ERRORLEVEL% neq 0 (
    echo COM3D2.5 版の dll のコピーに失敗しました
    exit /b 1
)
for %%D in ("%PKG_COM3D2%" "%PKG_COM3D25%") do (
    xcopy /e /i /y /q "BepInEx\config" "%%~D\config\" >nul
    if errorlevel 1 (
        echo %%~D へのプリセットのコピーに失敗しました
        exit /b 1
    )
)
rem 2 つの dll は同名なので、バイト比較ではフォルダとの取り違え (2.5 版に 2.0 用が入る等) を見分けられない
call :check_runtime "%PKG_COM3D2%\plugins\%ASSEMBLY_NAME%.dll" v2.
if %ERRORLEVEL% neq 0 exit /b 1
call :check_runtime "%PKG_COM3D25%\plugins\%ASSEMBLY_NAME%.dll" v4.
if %ERRORLEVEL% neq 0 exit /b 1

copy /y "LICENSE-UniVRM.txt" "output\%PLUGIN_NAME%\" >nul

set README_TXT=output\%PLUGIN_NAME%\README.txt
echo このテキストはWeb上で見ることを推奨しています。 > %README_TXT%
echo https://github.com/kidonaru/%PLUGIN_NAME%/blob/main/README.md >> %README_TXT%
echo. >> %README_TXT%
echo. >> %README_TXT%
type README.md >> %README_TXT%

rem zip 名は deploy.ps1 が参照するため、変更するときは両方合わせること
powershell -NoProfile Compress-Archive -Path "output\%PLUGIN_NAME%" -DestinationPath "output\%PLUGIN_NAME%-v%VERSION%.zip" -Force
if %ERRORLEVEL% neq 0 (
    echo zip の作成に失敗しました
    exit /b 1
)

rmdir /s /q output\%PLUGIN_NAME%

echo ビルドに成功しました
exit /b 0

:check_runtime
powershell -NoProfile -Command "$v = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Resolve-Path '%~1')).ImageRuntimeVersion; if (-not $v.StartsWith('%~2')) { Write-Host ('%~1 のランタイムが ' + $v + ' です (%~2 を想定)'); exit 1 }"
if %ERRORLEVEL% neq 0 (
    echo dll とゲームのフォルダが対応していません
    exit /b 1
)
exit /b 0
