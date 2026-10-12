<#
.SYNOPSIS
    プラグインのバージョンを更新する。bump-version.bat から呼ばれる。

.DESCRIPTION
    以下を一括で書き換える。
      - source\COM3D2.MToonMaid.Plugin\PluginInfo.cs の PluginVersion
      - README.md 冒頭のバージョン表記
      - README.md 変更履歴の見出し（内容は TODO のまま。後から手で書く）
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('major', 'minor', 'patch', 'build')]
    [string]$Part
)

$ErrorActionPreference = 'Stop'

$repoDir = $PSScriptRoot
$pluginName = 'COM3D2.MToonMaid.Plugin'
$pluginInfoPath = Join-Path $repoDir "source\$pluginName\PluginInfo.cs"
$readmePath = Join-Path $repoDir 'README.md'

# ファイルは UTF-8 (BOM なし) なので、読み書きで維持する
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
function Read-TextFile([string]$path) {
    if (-not (Test-Path $path)) { throw "ファイルが見つかりません: $path" }
    return [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
}
function Write-TextFile([string]$path, [string]$text) {
    [System.IO.File]::WriteAllText($path, $text, $utf8NoBom)
}

# ============ 現在のバージョンを取得 ============
$pluginInfo = Read-TextFile $pluginInfoPath
$versionPattern = 'PluginVersion\s*=\s*"(\d+)\.(\d+)\.(\d+)\.(\d+)"'
$versionMatch = [regex]::Match($pluginInfo, $versionPattern)
if (-not $versionMatch.Success) {
    throw "PluginInfo.cs から PluginVersion を読み取れませんでした: $pluginInfoPath"
}

$major = [int]$versionMatch.Groups[1].Value
$minor = [int]$versionMatch.Groups[2].Value
$patch = [int]$versionMatch.Groups[3].Value
$build = [int]$versionMatch.Groups[4].Value
$oldVersion = "$major.$minor.$patch.$build"

switch ($Part) {
    'major' { $major++; $minor = 0; $patch = 0; $build = 0 }
    'minor' { $minor++; $patch = 0; $build = 0 }
    'patch' { $patch++; $build = 0 }
    'build' { $build++ }
}
$newVersion = "$major.$minor.$patch.$build"

$date = Get-Date -Format 'yyyy/MM/dd'

Write-Host "バージョン: $oldVersion -> $newVersion ($date)"

# ============ 書き換え内容を組み立てる ============
# 片方だけ書き換わった中途半端な状態にならないよう、両方の検証が通ってから書き込む

# 置換は先頭 1 件だけに限定したいので、件数を指定できる Regex インスタンスの Replace を使う
# (-replace 演算子や [regex]::Replace の静的版には件数指定が無い)
$newPluginInfo = (New-Object regex $versionPattern).Replace($pluginInfo, "PluginVersion = `"$newVersion`"", 1)

$readme = Read-TextFile $readmePath
# 既存ファイルの改行に合わせて挿入する
$nl = if ($readme -match "`r`n") { "`r`n" } else { "`n" }

# 冒頭のバージョン表記 ※改行を巻き込まないよう行末は先読みで判定する
# 変更履歴の位置を取る前に置換する (後で置換すると文字数の変わる bump で挿入位置がずれる)
# 確認と置換は同じ Regex で行う (-notmatch は大文字小文字を無視し、置換だけ空振りする)
$headerRegex = New-Object regex "(?m)^v$([regex]::Escape($oldVersion))(?=[ \t]*\r?$)"
if (-not $headerRegex.IsMatch($readme)) {
    throw "README.md 冒頭に v$oldVersion の行が見つかりませんでした"
}
$readme = $headerRegex.Replace($readme, "v$newVersion", 1)

# 変更履歴の起点。README の他セクションに似た行があっても誤挿入しないよう、
# 挿入位置の検索はここから後ろだけを対象にする
$sectionPattern = '(?m)^## 変更履歴[ \t]*\r?$'
$sectionMatch = [regex]::Match($readme, $sectionPattern)
if (-not $sectionMatch.Success) {
    throw "README.md に変更履歴の見出し (## 変更履歴) が見つかりませんでした"
}

# 既存エントリは日付なし (### v1.0.0.0) の形式もあるため、日付は任意扱いにする
$entryPattern = '(?m)^### (?:\d{4}/\d{2}/\d{2} )?v([\d.]+)[ \t]*\r?$'
$entryRegex = New-Object regex $entryPattern

$duplicated = $entryRegex.Matches($readme) | Where-Object { $_.Groups[1].Value -eq $newVersion }
if ($duplicated) {
    throw "README.md の変更履歴に v$newVersion が既にあります。二重実行の可能性があるため中止します"
}

$entryBody = "### $date v$newVersion$nl- TODO: 変更内容を記載"

# 既存の先頭エントリの上に挿入する。まだ 1 件も無いときは見出しの直後に置く
# (見出しの直後には元の改行が残るので、そちら側には改行を足さない)
$entryMatch = $entryRegex.Match($readme, $sectionMatch.Index)
if ($entryMatch.Success) {
    $readme = $readme.Insert($entryMatch.Index, "$entryBody$nl$nl")
}
else {
    $insertIndex = $sectionMatch.Index + $sectionMatch.Length
    $readme = $readme.Insert($insertIndex, "$nl$nl$entryBody")
}

# ============ 書き込み ============
# README の書き込みに失敗したら PluginInfo.cs を元に戻す (バージョンだけ進んだ状態を残さない)
Write-TextFile $pluginInfoPath $newPluginInfo
try {
    Write-TextFile $readmePath $readme
}
catch {
    Write-TextFile $pluginInfoPath $pluginInfo
    throw
}
Write-Host "  更新: source\$pluginName\PluginInfo.cs"
Write-Host "  更新: README.md (冒頭 / 変更履歴)"

Write-Host ''
Write-Host "README.md の変更履歴に『TODO: 変更内容を記載』を挿入しました。内容を書いてから release.bat を実行してください"
