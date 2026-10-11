<#
.SYNOPSIS
    COM3D2/MToon10 などのシェーダーをバンドル化し、埋め込みリソースへコピーする。build-bundle.bat から呼ばれる。

.DESCRIPTION
    com3d25: Unity 2022.3.62 (COM3D2.5 本体と同じ版) で UnityProject2022 から mtoon_bundle を作る
    com3d2 : Unity 5.6.4f1 (COM3D2 本体と同じ版) で UnityProject56 から mtoon_bundle_com3d2 を作る
    Unity の場所は .env の UNITY_2022_EXE / UNITY_56_EXE で上書きできる。
    Unity エディタで UnityProject2022 を開いていると batchmode がロックで失敗する。
#>
param(
    [ValidateSet('com3d25', 'com3d2', 'all')]
    [string]$Target = 'all'
)
$ErrorActionPreference = 'Stop'

$repoDir = $PSScriptRoot
$resourcesDir = Join-Path $repoDir 'source\COM3D2.MToonMaid.Plugin\Resources'

$envValues = @{}
$envFile = Join-Path $repoDir '.env'
if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile -Encoding UTF8) {
        if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
        $key, $value = $line -split '=', 2
        $envValues[$key.Trim()] = $value.Trim()
    }
}

function Get-UnityExe([string]$name, [string]$default) {
    if ($envValues.ContainsKey($name) -and $envValues[$name]) { return $envValues[$name] }
    return $default
}

function Build-Bundle([string]$unityExe, [string]$envName, [string]$projectDir, [string]$dstName) {
    if (-not (Test-Path $unityExe)) {
        Write-Error "Unity が見つかりません: $unityExe`n.env に $envName を設定してください"
    }
    $logDir = Join-Path $projectDir 'Logs'
    $logFile = Join-Path $logDir 'build-bundle.log'
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null

    Write-Host "=== シェーダーバンドルをビルド中 ($dstName) ==="
    $unityArgs = @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', "`"$projectDir`"",
        '-executeMethod', 'BuildMToonBundle.Run',
        '-logFile', "`"$logFile`""
    )
    $proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
    if ($proc.ExitCode -ne 0) {
        Write-Error "バンドルのビルドに失敗しました (終了コード $($proc.ExitCode))。ログ: $logFile"
    }
    # Unity はシェーダーのコンパイルエラーがあっても終了コード 0 で抜けるため、ログを検査する
    $shaderErrors = Select-String -Path $logFile -Pattern 'Shader error' -SimpleMatch | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
    if ($shaderErrors) {
        $shaderErrors | ForEach-Object { Write-Host $_ }
        Write-Error "シェーダーのコンパイルエラーがあります。ログ: $logFile"
    }

    $src = Join-Path $projectDir 'Bundles\mtoon_bundle'
    if (-not (Test-Path $src)) {
        Write-Error "生成物が見つかりません: $src"
    }
    New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null
    $dst = Join-Path $resourcesDir $dstName
    Copy-Item $src $dst -Force
    Write-Host "コピーしました: $dst"
}

if ($Target -ne 'com3d2') {
    $unity2022 = Get-UnityExe 'UNITY_2022_EXE' 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
    Build-Bundle $unity2022 'UNITY_2022_EXE' (Join-Path $repoDir 'UnityProject2022') 'mtoon_bundle'
}
if ($Target -ne 'com3d25') {
    # シェーダーの原本は UnityProject2022 だけに置く。.meta は 2022 の形式なので写さず、5.6 に作り直させる
    $project56 = Join-Path $repoDir 'UnityProject56'
    & robocopy (Join-Path $repoDir 'UnityProject2022\Assets') (Join-Path $project56 'Assets') /MIR /XF *.meta /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        Write-Error "UnityProject56 へのコピーに失敗しました (robocopy の終了コード $LASTEXITCODE)"
    }
    $unity56 = Get-UnityExe 'UNITY_56_EXE' 'C:\Program Files\Unity\Hub\Editor\5.6.4f1\Editor\Unity.exe'
    Build-Bundle $unity56 'UNITY_56_EXE' $project56 'mtoon_bundle_com3d2'
}
