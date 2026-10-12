<#
.SYNOPSIS
    COM3D2.5 版 (net471) と COM3D2 (2.0) 版 (net35) の DLL をビルドし、リポジトリ内とゲームへコピーする。build.bat から呼ばれる。

.DESCRIPTION
    ゲームのインストール先はリポジトリ直下の .env (COM3D25_DIR / COM3D2_DIR) から読む。
    リポジトリ内のコピー先は 2.5 版が UnityInjector (COM3D2.5)、2.0 版が UnityInjector。
    ゲームへのコピーは、ゲーム起動中は DLL がロックされるため失敗しても続行する。
#>
param(
    [ValidateSet('debug', 'release')]
    [string]$Config = 'release',
    [ValidateSet('com3d25', 'com3d2', 'all')]
    [string]$Target = 'all'
)
$ErrorActionPreference = 'Stop'

$projectDir = $PSScriptRoot
$repoDir = Resolve-Path (Join-Path $projectDir '..\..')
$assemblyName = 'COM3D2.MToonMaid.Plugin'
$configName = if ($Config -eq 'debug') { 'Debug' } else { 'Release' }

$envFile = Join-Path $repoDir '.env'
if (-not (Test-Path $envFile)) {
    Write-Error ".env が見つかりません: $envFile`n.env.sample をコピーして .env を作成し、パスを設定してください"
}
$envValues = @{}
foreach ($line in Get-Content $envFile -Encoding UTF8) {
    if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
    $key, $value = $line -split '=', 2
    $envValues[$key.Trim()] = $value.Trim()
}

# ビルド対象に必要な変数だけ検証する
$builds = @()
if ($Target -ne 'com3d2') {
    $builds += @{ Framework = 'net471'; Name = 'COM3D2.5'; DirKey = 'COM3D25_DIR'; RepoOut = 'UnityInjector (COM3D2.5)' }
}
if ($Target -ne 'com3d25') {
    $builds += @{ Framework = 'net35'; Name = 'COM3D2'; DirKey = 'COM3D2_DIR'; RepoOut = 'UnityInjector' }
}
foreach ($b in $builds) {
    $gameDir = $envValues[$b.DirKey]
    if ([string]::IsNullOrEmpty($gameDir)) {
        Write-Error ".env に $($b.DirKey) が設定されていません"
    }
    $b.GameDir = $gameDir
    Write-Host "$($b.DirKey): $gameDir"
}

foreach ($b in $builds) {
    Write-Host "=== ビルド中 ($($b.Name) / $configName) ==="
    $buildArgs = @('build', (Join-Path $projectDir 'COM3D2.MToonMaid.Plugin.csproj'), '-c', $configName, '-f', $b.Framework, "-p:$($b.DirKey)=$($b.GameDir)")
    # Release は配布物になるため常にクリーンビルドする
    if ($configName -eq 'Release') { $buildArgs += '--no-incremental' }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$($b.Name) 版のビルドに失敗しました"
    }

    $dll = Join-Path $projectDir "bin\$configName\$($b.Framework)\$assemblyName.dll"
    $repoOut = Join-Path $repoDir $b.RepoOut
    New-Item -ItemType Directory -Path $repoOut -Force | Out-Null
    Copy-Item $dll $repoOut -Force

    # フォルダだけを宛先にすると、UnityInjector が無いときに UnityInjector という名前のファイルとしてコピーされる
    $gameOut = Join-Path $b.GameDir "Sybaris\UnityInjector\$assemblyName.dll"
    try {
        if (-not (Test-Path (Split-Path $gameOut))) {
            throw "フォルダがありません: $(Split-Path $gameOut)"
        }
        Copy-Item $dll $gameOut -Force
        Write-Host "$($b.Name) へデプロイしました"
    }
    catch {
        Write-Host "警告: $($b.Name) へのデプロイに失敗しました (ゲーム起動中?): $($_.Exception.Message)"
    }
}
