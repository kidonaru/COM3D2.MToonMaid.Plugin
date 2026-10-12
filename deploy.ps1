<#
.SYNOPSIS
    リリース用 zip を作成して GitHub Releases に公開する。deploy.bat から呼ばれる。

.DESCRIPTION
    以下を順に実行する。
      - リリース前チェック（作業ツリーが綺麗か / origin に push 済みか / 同バージョンのリリースが無いか）
      - release.bat によるビルドと zip 作成
      - README.md の変更履歴から Release Note を生成して gh release create でアップロード
    release.bat のビルドは .env のゲームフォルダにも Release 版の dll をコピーする（ゲーム起動中は警告のみ）。
#>
param(
    # ビルド済みの zip をそのまま使う（Release Note だけ作り直したいときなど）
    [switch]$SkipBuild,
    # チェックと Release Note の生成だけ行い、ビルドとアップロードはしない
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoDir = $PSScriptRoot
$pluginName = 'COM3D2.MToonMaid.Plugin'
$pluginInfoPath = Join-Path $repoDir "source\$pluginName\PluginInfo.cs"
$readmePath = Join-Path $repoDir 'README.md'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
function Read-TextFile([string]$path) {
    if (-not (Test-Path $path)) { throw "ファイルが見つかりません: $path" }
    return [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
}

# PowerShell 5.1 は EA=Stop だと native コマンドの stderr 出力だけで停止するので、呼び出し中は Continue に戻す
function Invoke-NativeRaw([string]$exe, [string[]]$exeArgs) {
    $backup = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $exe @exeArgs 2>&1
    }
    finally {
        $ErrorActionPreference = $backup
    }
    return [PSCustomObject]@{ Output = $output; ExitCode = $LASTEXITCODE }
}

# git / gh は終了コードで失敗を判定する（$ErrorActionPreference では拾えないため）
function Invoke-Native([string]$exe, [string[]]$exeArgs, [string]$errorMessage) {
    $result = Invoke-NativeRaw $exe $exeArgs
    if ($result.ExitCode -ne 0) {
        throw "$errorMessage`n$($result.Output -join "`n")"
    }
    return $result.Output
}

# 失敗が想定内のもの（リリースの存在チェックなど）用
function Test-NativeSucceeded([string]$exe, [string[]]$exeArgs) {
    return (Invoke-NativeRaw $exe $exeArgs).ExitCode -eq 0
}

# ============ バージョンを取得 ============
$pluginInfo = Read-TextFile $pluginInfoPath
$versionMatch = [regex]::Match($pluginInfo, 'PluginVersion\s*=\s*"([\d.]+)"')
if (-not $versionMatch.Success) {
    throw "PluginInfo.cs から PluginVersion を読み取れませんでした: $pluginInfoPath"
}
$version = $versionMatch.Groups[1].Value
$tag = "v$version"
# release.bat が出力する zip 名と一致させること（片方だけ変えると zip が見つからず失敗する）
$zipName = "$pluginName-v$version.zip"
$zipPath = Join-Path $repoDir "output\$zipName"

Write-Host "リリース対象: $tag"

# git / gh / release.bat はカレントディレクトリ基準で動くため、リポジトリ直下へ移動する
# （別リポジトリのフォルダから起動されると、そちらを対象にリリースしてしまうのを防ぐ）
# ※ deploy.bat から専用プロセスで起動される想定なので元に戻さない
Set-Location $repoDir

# ============ リリース前チェック ============
# 公開してから間違いに気付くのを避けるため、ビルド前にまとめて検証する

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh コマンドが見つかりません。GitHub CLI をインストールしてください"
}
Invoke-Native 'gh' @('auth', 'status') 'gh の認証が済んでいません。gh auth login を実行してください' | Out-Null

# submodule (MTEUtils) 内の未コミット変更も拾うため --ignore-submodules=none を付ける
$status = Invoke-Native 'git' @('status', '--porcelain', '--ignore-submodules=none') 'git status に失敗しました'
if ($status) {
    throw "コミットされていない変更があります。コミットしてから実行してください`n$($status -join "`n")"
}

# @{u} は upstream 未設定だと失敗するので、その場合もリリースを中止する
$unpushed = Invoke-Native 'git' @('rev-list', '@{u}..HEAD') 'upstream が設定されていません。git push -u origin <branch> を実行してください'
if ($unpushed) {
    throw "origin に push されていないコミットがあります ($($unpushed.Count) 件)。push してから実行してください"
}

# 親リポジトリの push だけでは、未 push の MTEUtils コミットを指したタグになり clone から再現できない
$submoduleRemote = Invoke-Native 'git' @('-C', 'MTEUtils', 'branch', '-r', '--contains', 'HEAD') 'MTEUtils のリモートブランチの確認に失敗しました'
if (-not $submoduleRemote) {
    throw "MTEUtils の今のコミットが origin に push されていません。MTEUtils を push してから実行してください"
}

if (Test-NativeSucceeded 'gh' @('release', 'view', $tag)) {
    throw "$tag のリリースが既に存在します。二重実行の可能性があるため中止します"
}

# gh release create はタグが無いとき既定でデフォルトブランチの先頭にタグを打つため、
# ビルドしたコミットを --target で明示する
$headSha = (Invoke-Native 'git' @('rev-parse', 'HEAD') 'HEAD の取得に失敗しました') -join ''

# ============ Release Note を組み立てる ============
$readme = Read-TextFile $readmePath

# 変更履歴の該当バージョンの本文（次の見出しの手前まで）を抜き出す
# 見出しは bump-version.ps1 に合わせ、「### v1.0.0.0」「### 2026/10/12 v1.0.0.0」のどちらの形式も許容する
$entryPattern = "(?m)^### (?:\d{4}/\d{2}/\d{2} )?v$([regex]::Escape($version))[ \t]*\r?$"
# README 全体から探すと他の節の似た見出しを拾うため、bump-version.ps1 と同じく変更履歴節から後ろだけを見る
$sectionMatch = [regex]::Match($readme, '(?m)^## 変更履歴[ \t]*\r?$')
if (-not $sectionMatch.Success) {
    throw "README.md に変更履歴の見出し (## 変更履歴) が見つかりませんでした"
}
$history = $readme.Substring($sectionMatch.Index + $sectionMatch.Length)
$entryMatch = [regex]::Match($history, $entryPattern)
if (-not $entryMatch.Success) {
    throw "README.md の変更履歴に $tag のエントリが見つかりませんでした。bump-version.bat を実行しましたか?"
}
$rest = $history.Substring($entryMatch.Index + $entryMatch.Length)
$nextMatch = [regex]::Match($rest, '(?m)^#{2,3} ')
$changesRaw = if ($nextMatch.Success) { $rest.Substring(0, $nextMatch.Index) } else { $rest }
$changes = $changesRaw.Trim()
if (-not $changes) {
    throw "README.md の $tag の変更履歴が空です"
}
if ($changes -match 'TODO') {
    throw "README.md の $tag の変更履歴に TODO が残っています。内容を記載してから実行してください"
}

$repoUrl = (Invoke-Native 'gh' @('repo', 'view', '--json', 'url', '-q', '.url') 'リポジトリ情報の取得に失敗しました') -join ''
$downloadUrl = "$repoUrl/releases/download/$tag/$zipName"
# main を指すと、後のバージョンで変わった説明を古いリリースから読むことになる
$readmeUrl = "$repoUrl/blob/$tag/README.md"

# 二重引用符付き here-string 内ではバックティックがエスケープ文字になるため、
# Markdown のインラインコード用バックティックは `` と 2 つ重ねて書く
$notes = @"
### ダウンロード
[$zipName]($downloadUrl)

zip を解凍し、お使いのゲームに対応したフォルダの中身を、ゲームのインストールフォルダの ``BepInEx`` フォルダへ上書きコピーしてください。

| 対象 | 同梱フォルダ |
|---|---|
| **COM3D2.5** | ``BepInEx (COM3D2.5)`` |
| **COM3D2 (2.0)** | ``BepInEx`` |

COM3D2.5 では zip 直下の ``BepInEx`` フォルダ (2.0 用) は使いません。

2 つの dll は同じ名前で、ゲームに合わない方を入れると動作しません。
使い方は [README]($readmeUrl) をご覧ください。

### 変更点
$changes
"@

if ($DryRun) {
    Write-Host ''
    Write-Host '--- Release Note (dry run) ---'
    Write-Host $notes
    Write-Host '--- ここまで。ビルドとアップロードは行いません ---'
    exit 0
}

# ============ ビルド ============
if ($SkipBuild) {
    # 既存 zip が今の HEAD より古い可能性があるので、判断できるよう生成日時を出す
    $zipTime = if (Test-Path $zipPath) { (Get-Item $zipPath).LastWriteTime } else { '(未生成)' }
    Write-Host "ビルドをスキップします (既存 zip の生成日時: $zipTime)"
}
else {
    Write-Host 'release.bat を実行します...'
    & (Join-Path $repoDir 'release.bat')
    if ($LASTEXITCODE -ne 0) {
        throw 'release.bat に失敗しました'
    }
}

if (-not (Test-Path $zipPath)) {
    if ($SkipBuild) {
        throw "zip が見つかりません: $zipPath`n-SkipBuild を指定したためビルドしていません。先に -SkipBuild なしで実行してください"
    }
    throw "zip が見つかりません: $zipPath"
}

# ============ アップロード ============
# gh は改行を含む --notes を扱いづらいのでファイル渡しにする
# ※ BOM があると Release Note の先頭に余分な文字が出るため BOM なしで書き出す
$notesPath = Join-Path ([System.IO.Path]::GetTempPath()) "$pluginName-$tag-notes.md"
[System.IO.File]::WriteAllText($notesPath, $notes, $utf8NoBom)
try {
    Invoke-Native 'gh' @('release', 'create', $tag, $zipPath, '--title', $tag, '--notes-file', $notesPath, '--target', $headSha) 'リリースの作成に失敗しました' | Write-Host
}
finally {
    Remove-Item $notesPath -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host "$tag をリリースしました"
