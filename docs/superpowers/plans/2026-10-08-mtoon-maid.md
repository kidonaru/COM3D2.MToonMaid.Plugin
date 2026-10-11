# COM3D25.MToonMaid.Plugin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 指定したメイドの Toony 系シェーダーを、MToon10 をフォークした `COM3D25/MToon10` へ置き換える BepInEx プラグインを新規に作る。

**Architecture:** フォークしたシェーダーを Unity 2022.3.62 でバンドル化して DLL に埋め込む。メイドごとの Controller が、非 busy の間に定期的にマテリアルを走査して変換・再変換する（案 A：監視のみ、フック不使用）。設定は GUID ごとの XML に永続化し、IMGUI ウィンドウで編集する。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / Unity 2022.3.62（シェーダービルド用）/ xunit（net48）

**Spec:** `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md`

## Global Constraints

- 対象は COM3D2.5 のみ（`Application.unityVersion` が `2022.` で始まらなければ何もしない。ただし 2.0 には `UnityEngine.CoreModule` が無く、Awake の前にロードで失敗しうる。その場合は BepInEx のロードエラーになるだけで許容する）
- URP が有効（`GraphicsSettings.currentRenderPipeline != null`）なら何もしない。MToon10 の built-in 版は URP で描画されない
- 「UI で ON にしたメイドだけ」を毎 Tick で保証する。Controller は作成時の GUID を持ち、GUID が変わったか設定が OFF になったら元へ戻して外す
- シェーダー名は `COM3D25/MToon10`。プロパティ名はゲームと意味が同じ `_ShadowTex` だけを改名し、`_ShadowColor` / `_RimPower` / `_RimShift` は揃えない
- 置き換え対象は `CM3D2/Toony_Lighted` で始まり `Trans` を含まないシェーダーだけ
- マテリアルは `sharedMaterials` だけを読む（`materials` は複製を作るので禁止）
- 変換後の renderQueue は元の値へ戻す
- 設定の保存先は `BepInEx/config/COM3D25.MToonMaid/<GUID>.xml`。値の変更から約 1 秒後にまとめて書く
- コードのコメント・ログは日本語。UniVRM（MIT）のライセンス表記を同梱する
- `debug.bat` / `source/<Plugin>/build.bat` は**ゲームフォルダへ DLL をコピーする**。ゲーム停止中に実行すると実機へ反映されるため、各タスクのビルド確認は `dotnet build` を直接実行する
- `deploy.bat` は作らない・実行しない
- git worktree は使わない。作業は `W:\COM3D2_5\work\COM3D25.MToonMaid.Plugin` の main で行う
- Git Bash から MSBuild / dotnet に `/p:` 引数を渡すときは `MSYS_NO_PATHCONV=1` を付ける

## Review Focus

- **同じ Maid インスタンスへの別キャラの入れ替え**（ストック Maid の使い回しで `status.guid` が変わる）: ON でないキャラを変換し続けないよう、Controller の GUID と現在の GUID・`enabled` を毎 Tick 照合し、ずれたら `RestoreAll` して外す → Task 8 の実装
- **変換の途中で例外**: シェーダーだけ MToon になった半端な状態を残さない。`Convert` 内で失敗したら元へ戻してから再 throw する → Task 6 の実装
- **他者が変換後にシェーダーを変えたマテリアルの OFF**（SE で NPR へ変えた、ゲームが Toony へ戻して次の走査前）: `Restore` は現在のシェーダーが `COM3D25/MToon10` のときだけ実行し、それ以外は控えを捨てるだけにする → Task 6 / Task 10 Step 9
- **一時的に非表示になったメイド**（`Visible=false` の後に再表示）: Controller を可視性で捨てると、MToon のまま控えを失い、OFF にしても戻せなくなる。Controller は Maid インスタンスの生存（Unity の null 判定）で管理し、非表示の間は走査だけ止める → Task 8 の実装と Task 10 Step 6 の実機手順で確認する（Maid を伴うため単体テスト不可）
- **名前が同じプロパティの上書き**: `_OutlineWidth` / `_RimColor` / `_OutlineColor` は Toony と MToon で同名。変換時に書き換えるので、OFF で元の値へ戻さないとゲームのアウトライン幅やリム色が壊れる → Task 6 の控えに含め、Task 10 の実機手順で確認する
- **menu の `shader` コマンドによる Toony への巻き戻し**: キャッシュ済みのマテリアルが Toony に戻ったら、控えは残したまま再変換する。そのときの renderQueue とシェーダーはゲームの新しい値を控え直す → Task 7 の分岐と Task 10 の実機手順
- **壊れた設定ファイル**: 読めない XML で例外を出さず、`.bak` へ退避して OFF 扱いにする → Task 3 のテストで固定する
- **ウィンドウ操作中のカメラ**: スライダーのドラッグやホイールでカメラが動かない。カーソルがウィンドウに入った瞬間にだけ `SetControl(false)` を呼び、出たときに自分が無効化した場合だけ戻す（SE の仲裁と争わない）→ Task 9 の実装と Task 10 の実機手順

---

## ファイル構成

```
COM3D25.MToonMaid.Plugin/
├─ .gitignore / .gitattributes / .env.sample / .gitmodules
├─ README.md / LICENSE-UniVRM.txt
├─ debug.bat / release.bat / bump-version.bat / bump-version.ps1 / build-bundle.bat
├─ MTEUtils/                         (submodule。GearMenu.cs だけをコンパイルに含める)
├─ UnityProject2022/                 (Unity 2022.3.62。シェーダーのバンドル化専用)
│  ├─ Assets/Shaders/MToon10/*.shader, *.hlsl
│  └─ Assets/Editor/BuildMToonBundle.cs
└─ source/
   ├─ COM3D25.MToonMaid.Plugin/
   │  ├─ COM3D25.MToonMaid.Plugin.csproj / build.bat
   │  ├─ PluginInfo.cs                 メタ情報
   │  ├─ MToonMaidPlugin.cs            エントリポイント・Controller の管理
   │  ├─ ToonMaterialClassifier.cs     対象判定と部位グループ（純粋）
   │  ├─ PartGroup.cs                  部位グループ enum
   │  ├─ Settings/ColorData.cs         XML 用の色
   │  ├─ Settings/GroupSettings.cs     1 グループ分の値と既定値
   │  ├─ Settings/MaidSettings.cs      1 メイド分（XML ルート）
   │  ├─ Settings/MaidSettingsStore.cs 読み書き・遅延保存
   │  ├─ Rendering/MToonProps.cs       プロパティ名とキーワードの定数
   │  ├─ Rendering/MToonRenderState.cs 描画ステートの決定（純粋）
   │  ├─ Rendering/MToonShaderProvider.cs 埋め込みバンドルの読み込み
   │  ├─ Rendering/MaterialBackup.cs   変換前の控え
   │  ├─ Rendering/MaterialConverter.cs 変換・再変換・グループ適用・復元
   │  ├─ MaidMToonController.cs        1 メイド分の走査
   │  ├─ UI/MToonMaidWindow.cs         IMGUI ウィンドウ
   │  └─ Resources/mtoon_bundle        build-bundle.bat の成果物（埋め込みリソース）
   └─ COM3D25.MToonMaid.Plugin.Tests/
      ├─ COM3D25.MToonMaid.Plugin.Tests.csproj
      ├─ ToonMaterialClassifierTests.cs
      ├─ MaidSettingsStoreTests.cs
      └─ MToonRenderStateTests.cs
```

---

### Task 1: リポジトリの雛形とビルド

**Files:**
- Create: `.gitignore`, `.gitattributes`, `.env.sample`, `README.md`, `debug.bat`, `release.bat`, `bump-version.bat`, `bump-version.ps1`
- Create: `source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj`, `build.bat`, `PluginInfo.cs`, `MToonMaidPlugin.cs`
- Create: `MTEUtils/`（submodule）

**Interfaces:**
- Produces: `PluginInfo.PluginGuid` / `PluginName` / `PluginFullName` / `PluginVersion`、`MToonMaidPlugin.Log`（`ManualLogSource`）、名前空間 `COM3D25.MToonMaid.Plugin`

- [ ] **Step 1: 雛形ファイルを CostumeFix からコピーして名前を置換する**

```bash
cd /w/COM3D2_5/work/COM3D25.MToonMaid.Plugin
SRC=/w/COM3D2_5/work/COM3D25.CostumeFix.Plugin
cp $SRC/.gitignore $SRC/.gitattributes $SRC/debug.bat $SRC/release.bat $SRC/bump-version.bat $SRC/bump-version.ps1 .
mkdir -p source/COM3D25.MToonMaid.Plugin
cp $SRC/source/COM3D25.CostumeFix.Plugin/build.bat source/COM3D25.MToonMaid.Plugin/
sed -i 's/COM3D25\.CostumeFix\.Plugin/COM3D25.MToonMaid.Plugin/g; s/COM3D25\.CostumeFix/COM3D25.MToonMaid/g' debug.bat release.bat source/COM3D25.MToonMaid.Plugin/build.bat
grep -n "CostumeFix" bump-version.ps1
```

`bump-version.ps1` に `CostumeFix` が出たら同様に `MToonMaid` へ置換する。

- [ ] **Step 2: `.env.sample` を作る**

```
# ゲームのインストール先 (build.bat が読む)
COM3D25_DIR=W:\COM3D2_5
# シェーダーのバンドル化に使う Unity 2022.3.62 (build-bundle.bat が読む)
UNITY_2022_EXE=C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe
```

`.env.sample` を `.env` にコピーする（`.env` は `.gitignore` 済み）。

- [ ] **Step 3: MTEUtils を submodule として追加する**

```bash
cd /w/COM3D2_5/work/COM3D25.MToonMaid.Plugin
git submodule add -b master https://github.com/kidonaru/COM3D2.MTEUtils.git MTEUtils
```

SDK 形式の csproj はプロジェクトフォルダ以下の `.cs` を自動で含めるため、submodule はリポジトリ直下に置き、`GearMenu.cs` だけを明示的に含める。

- [ ] **Step 4: csproj を作る**

`source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net471</TargetFramework>
    <AssemblyName>COM3D25.MToonMaid</AssemblyName>
    <RootNamespace>COM3D25.MToonMaid.Plugin</RootNamespace>
    <LangVersion>7.3</LangVersion>
    <!-- バージョンの単一ソースは PluginInfo.cs（bump-version.bat が書き換える） -->
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <!-- ゲームのインストール先。build.bat が .env から /p:COM3D25_DIR= で渡す -->
    <COM3D25_DIR Condition=" '$(COM3D25_DIR)' == '' ">W:\COM3D2_5</COM3D25_DIR>
    <GameDir>$(COM3D25_DIR)</GameDir>
    <Managed>$(GameDir)\COM3D2x64_Data\Managed</Managed>
  </PropertyGroup>
  <ItemGroup>
    <!-- MTEUtils からはギアメニューだけを使う -->
    <Compile Include="..\..\MTEUtils\GearMenu.cs" Link="MTEUtils\GearMenu.cs" />
    <EmbeddedResource Include="Resources\mtoon_bundle" LogicalName="mtoon_bundle" Condition="Exists('Resources\mtoon_bundle')" />
    <InternalsVisibleTo Include="COM3D25.MToonMaid.Plugin.Tests" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="Assembly-CSharp"><HintPath>$(Managed)\Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Assembly-CSharp-firstpass"><HintPath>$(Managed)\Assembly-CSharp-firstpass.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine"><HintPath>$(Managed)\UnityEngine.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(Managed)\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.IMGUIModule"><HintPath>$(Managed)\UnityEngine.IMGUIModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.AssetBundleModule"><HintPath>$(Managed)\UnityEngine.AssetBundleModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.ImageConversionModule"><HintPath>$(Managed)\UnityEngine.ImageConversionModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.InputLegacyModule"><HintPath>$(Managed)\UnityEngine.InputLegacyModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.TextRenderingModule"><HintPath>$(Managed)\UnityEngine.TextRenderingModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="BepInEx"><HintPath>$(GameDir)\BepInEx\core\BepInEx.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
```

ビルドで `CS0012`（型が未参照のアセンブリにある）が出たら、エラーが示す `UnityEngine.*Module.dll` を同じ形で追加する。

- [ ] **Step 5: PluginInfo.cs と最小のエントリポイントを書く**

`PluginInfo.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// プラグインのメタ情報。PluginVersion はリリース時のバージョンの単一ソースで、
    /// bump-version.bat / release.bat がここを参照する
    /// </summary>
    internal static class PluginInfo
    {
        public const string PluginGuid = "com.kidonaru.com3d25.mtoonmaid";
        public const string PluginName = "MToonMaid";
        public const string PluginFullName = "COM3D25." + PluginName + ".Plugin";
        public const string PluginVersion = "0.1.0.0";
    }
}
```

`MToonMaidPlugin.cs`:

```csharp
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    [BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginFullName, PluginInfo.PluginVersion)]
    public class MToonMaidPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            // MToon10 は Unity 5.6 (COM3D2 2.0) ではコンパイルできないため 2.5 専用
            if (!Application.unityVersion.StartsWith("2022.", System.StringComparison.Ordinal))
            {
                Log.LogError("MToonMaid は COM3D2.5 専用です。Unity " + Application.unityVersion + " では動作しません");
                enabled = false;
                return;
            }

            Log.LogInfo("MToonMaid 有効化");
        }
    }
}
```

- [ ] **Step 6: README.md の雛形を書く**

```markdown
# COM3D25.MToonMaid.Plugin

指定したメイドのトゥーンシェーダーを MToon (VRM) ベースのシェーダーに置き換える COM3D2.5 用プラグインです。

## 動作環境

- COM3D2.5 + BepInEx 5

## ライセンス

シェーダーは [UniVRM](https://github.com/vrm-c/UniVRM) の MToon10 を改変したものです（MIT License、`LICENSE-UniVRM.txt`）。
```

- [ ] **Step 7: ビルドが通ることを確認する**

Run:
```bash
cd /w/COM3D2_5/work/COM3D25.MToonMaid.Plugin
dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug
```
Expected: `ビルドに成功しました` 相当（`Build succeeded`）、`source/COM3D25.MToonMaid.Plugin/bin/Debug/COM3D25.MToonMaid.dll` ができる。

- [ ] **Step 8: Commit**

```bash
git add .gitignore .gitattributes .env.sample .gitmodules MTEUtils README.md debug.bat release.bat bump-version.bat bump-version.ps1 source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj source/COM3D25.MToonMaid.Plugin/build.bat source/COM3D25.MToonMaid.Plugin/PluginInfo.cs source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs
git commit -m "chore: プラグインの雛形を追加"
```

---

### Task 2: テストプロジェクトと ToonMaterialClassifier

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin.Tests/COM3D25.MToonMaid.Plugin.Tests.csproj`
- Create: `source/COM3D25.MToonMaid.Plugin/PartGroup.cs`, `ToonMaterialClassifier.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs`

**Interfaces:**
- Produces: `enum PartGroup { Skin, Face, Hair, Costume }`、`ToonMaterialClassifier.IsTarget(string shaderName) : bool`、`HasOutline(string) : bool`、`IsCutout(string) : bool`、`GetGroup(string slotCategory) : PartGroup`

- [ ] **Step 1: テストプロジェクトを作る**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <!-- テストは Unity のネイティブ呼び出しを含まない型だけを扱う -->
    <ProjectReference Include="..\COM3D25.MToonMaid.Plugin\COM3D25.MToonMaid.Plugin.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 失敗するテストを書く**

`ToonMaterialClassifierTests.cs`:

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class ToonMaterialClassifierTests
    {
        [Theory]
        [InlineData("CM3D2/Toony_Lighted", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Hair_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Cutout_AtC", true)]
        [InlineData("CM3D2/Toony_Lighted_Trans", false)]
        [InlineData("CM3D2/Toony_Lighted_Trans_NoZ", false)]
        [InlineData("CM3D2/Lighted_Trans", false)]
        [InlineData("com3d2mod/ToonyLighted", false)]
        [InlineData("COM3D25/MToon10", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsTarget(string shaderName, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsTarget(shaderName));
        }

        [Theory]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Hair_Outline", true)]
        [InlineData("CM3D2/Toony_Lighted", false)]
        [InlineData("CM3D2/Toony_Lighted_Cutout_AtC", false)]
        public void HasOutline(string shaderName, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.HasOutline(shaderName));
        }

        [Theory]
        [InlineData("CM3D2/Toony_Lighted_Cutout_AtC", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline", false)]
        public void IsCutout(string shaderName, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsCutout(shaderName));
        }

        [Theory]
        [InlineData("body", PartGroup.Skin)]
        [InlineData("head", PartGroup.Face)]
        [InlineData("hairF", PartGroup.Hair)]
        [InlineData("hairR", PartGroup.Hair)]
        [InlineData("hairS", PartGroup.Hair)]
        [InlineData("hairT", PartGroup.Hair)]
        [InlineData("hairAho", PartGroup.Hair)]
        [InlineData("onepiece", PartGroup.Costume)]
        [InlineData("chikubi", PartGroup.Costume)]
        [InlineData("accHat", PartGroup.Costume)]
        [InlineData(null, PartGroup.Costume)]
        public void GetGroup(string category, PartGroup expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.GetGroup(category));
        }
    }
}
```

- [ ] **Step 3: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`ToonMaterialClassifier` / `PartGroup` が無い）

- [ ] **Step 4: 実装する**

`PartGroup.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>調整値をまとめる部位の単位。TBodySkin.Category (スロット名) から決める</summary>
    public enum PartGroup
    {
        Skin,
        Face,
        Hair,
        Costume,
    }
}
```

`ToonMaterialClassifier.cs`:

```csharp
using System;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// 置き換え対象の判定と部位グループの決定。
    /// 半透明 (_Trans / _Trans_NoZ) は描画順が崩れやすいため対象外にする
    /// </summary>
    public static class ToonMaterialClassifier
    {
        private const string ToonyPrefix = "CM3D2/Toony_Lighted";

        public static bool IsTarget(string shaderName)
        {
            return !string.IsNullOrEmpty(shaderName)
                && shaderName.StartsWith(ToonyPrefix, StringComparison.Ordinal)
                && shaderName.IndexOf("Trans", StringComparison.Ordinal) < 0;
        }

        public static bool HasOutline(string shaderName)
        {
            return shaderName != null && shaderName.IndexOf("Outline", StringComparison.Ordinal) >= 0;
        }

        public static bool IsCutout(string shaderName)
        {
            return shaderName != null && shaderName.IndexOf("Cutout", StringComparison.Ordinal) >= 0;
        }

        public static PartGroup GetGroup(string slotCategory)
        {
            if (slotCategory == "body")
            {
                return PartGroup.Skin;
            }
            if (slotCategory == "head")
            {
                return PartGroup.Face;
            }
            if (slotCategory != null && slotCategory.StartsWith("hair", StringComparison.OrdinalIgnoreCase))
            {
                return PartGroup.Hair;
            }
            return PartGroup.Costume;
        }
    }
}
```

- [ ] **Step 5: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（全件）

- [ ] **Step 6: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin.Tests source/COM3D25.MToonMaid.Plugin/PartGroup.cs source/COM3D25.MToonMaid.Plugin/ToonMaterialClassifier.cs
git commit -m "feat: 置き換え対象の判定と部位グループを追加"
```

---

### Task 3: 設定モデルと MaidSettingsStore

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Settings/ColorData.cs`, `GroupSettings.cs`, `MaidSettings.cs`, `MaidSettingsStore.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/MaidSettingsStoreTests.cs`

**Interfaces:**
- Consumes: `PartGroup`
- Produces:
  - `ColorData { float r, g, b, a }`（`ColorData(float r, float g, float b, float a)`、引数なしコンストラクタあり）
  - `GroupSettings`（フィールドは下記コード）、`GroupSettings.CreateDefault(PartGroup) : GroupSettings`
  - `MaidSettings { int version; bool enabled; GroupSettings skin, face, hair, costume }`、`Get(PartGroup) : GroupSettings`、`Set(PartGroup, GroupSettings)`、`FillMissing()`、`const int CurrentVersion = 1`
  - `MaidSettingsStore(string directory, Action<string> warn)`、`Get(string guid) : MaidSettings`、`MarkDirty(string guid, float now)`、`FlushIfDue(float now)`、`FlushAll()`、`const float SaveDelay = 1f`
  - `internal static MaidSettings MaidSettingsStore.Load(string path, Action<string> warn)`、`internal static void Save(string path, MaidSettings settings)`

- [ ] **Step 1: 失敗するテストを書く**

`MaidSettingsStoreTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class MaidSettingsStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public MaidSettingsStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        [Fact]
        public void 保存して読み直すと同じ値になる()
        {
            var path = Path.Combine(_dir, "a.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.enabled = true;
            s.hair.shadingShift = 0.3f;
            s.hair.rimColorOverride = true;
            s.hair.rimColor = new ColorData(0.1f, 0.2f, 0.3f, 1f);

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.True(loaded.enabled);
            Assert.Equal(0.3f, loaded.hair.shadingShift);
            Assert.True(loaded.hair.rimColorOverride);
            Assert.Equal(0.2f, loaded.hair.rimColor.g);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void ファイルが無ければOFFの既定値()
        {
            var store = new MaidSettingsStore(_dir, _warnings.Add);
            var s = store.Get("missing-guid");

            Assert.False(s.enabled);
            Assert.NotNull(s.skin);
            Assert.NotNull(s.costume.shadeColor);
        }

        [Fact]
        public void 欠けた項目は既定値で補う()
        {
            var path = Path.Combine(_dir, "b.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"1\"><enabled>true</enabled>" +
                "<hair><shadingShift>0.5</shadingShift></hair></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.True(s.enabled);
            Assert.Equal(0.5f, s.hair.shadingShift);
            Assert.Equal(0.95f, s.hair.shadingToony);          // フィールド初期値
            Assert.NotNull(s.hair.shadeColor);                  // null だった参照型は補う
            Assert.Equal(GroupSettings.CreateDefault(PartGroup.Skin).shadeColor.g, s.skin.shadeColor.g);
        }

        [Fact]
        public void 壊れたファイルはbakへ退避してOFF()
        {
            var path = Path.Combine(_dir, "c.xml");
            File.WriteAllText(path, "<MToonMaid><enabled>tr");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.False(s.enabled);
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".bak"));
            Assert.Single(_warnings);
        }

        [Fact]
        public void 変更から1秒経つまで書かない()
        {
            var store = new MaidSettingsStore(_dir, _warnings.Add);
            var s = store.Get("g1");
            s.enabled = true;

            store.MarkDirty("g1", 10f);
            store.FlushIfDue(10.5f);
            Assert.False(File.Exists(Path.Combine(_dir, "g1.xml")));

            store.FlushIfDue(11.1f);
            Assert.True(File.Exists(Path.Combine(_dir, "g1.xml")));
        }

        [Fact]
        public void ファイル名に使えない文字はGUIDから除く()
        {
            var store = new MaidSettingsStore(_dir, _warnings.Add);
            store.Get("a/b");
            store.MarkDirty("a/b", 0f);
            store.FlushAll();

            Assert.True(File.Exists(Path.Combine(_dir, "a_b.xml")));
        }

        [Fact]
        public void 読み込んでいないGUIDのMarkDirtyは無視する()
        {
            var store = new MaidSettingsStore(_dir, _warnings.Add);
            store.MarkDirty("never-loaded", 0f);
            store.FlushAll();

            Assert.False(File.Exists(Path.Combine(_dir, "never-loaded.xml")));
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 上書き保存で一時ファイルが残らない()
        {
            var path = Path.Combine(_dir, "d.xml");
            var s = new MaidSettings();
            s.FillMissing();
            MaidSettingsStore.Save(path, s);
            s.enabled = true;
            MaidSettingsStore.Save(path, s);

            Assert.True(MaidSettingsStore.Load(path, _warnings.Add).enabled);
            Assert.False(File.Exists(path + ".tmp"));
        }
    }
}
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`MaidSettings` 等が無い）

- [ ] **Step 3: 設定モデルを実装する**

`Settings/ColorData.cs`:

```csharp
using System.Xml.Serialization;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>XML 用の色。UnityEngine.Color は読み取り専用プロパティを持ち XmlSerializer に向かないため別に持つ</summary>
    public class ColorData
    {
        [XmlAttribute] public float r;
        [XmlAttribute] public float g;
        [XmlAttribute] public float b;
        [XmlAttribute] public float a = 1f;

        public ColorData()
        {
        }

        public ColorData(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public ColorData Clone()
        {
            return new ColorData(r, g, b, a);
        }
    }
}
```

`Settings/GroupSettings.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// 部位グループ 1 つ分の調整値。フィールド初期値が XML に欠けた項目の既定値になる。
    /// 参照型 (色) はグループで既定が違うため null のまま読み、MaidSettings.FillMissing で補う
    /// </summary>
    public class GroupSettings
    {
        // 陰影
        public ColorData shadeColor;
        public float shadingShift = -0.05f;
        public float shadingToony = 0.95f;
        public float giEqualization = 0.9f;

        // リム。上書きしなければゲームの _RimColor を使う
        public bool rimColorOverride;
        public ColorData rimColor;
        public float rimFresnelPower = 5f;
        public float rimLift;
        public float rimLightingMix = 1f;

        // アウトライン。元のシェーダーにアウトラインがあるマテリアルにだけ効く
        public bool outlineEnabled = true;
        public float outlineWidth = 0.0015f;
        public float outlineLightingMix = 1f;
        public bool outlineColorOverride;
        public ColorData outlineColor;

        public static GroupSettings CreateDefault(PartGroup group)
        {
            var s = new GroupSettings();
            s.FillMissing(group);
            return s;
        }

        /// <summary>null の参照型をグループの既定で埋める</summary>
        public void FillMissing(PartGroup group)
        {
            if (shadeColor == null)
            {
                // 肌だけ影をわずかに赤みへ寄せる (ゲームの肌ランプの暖色寄りの暗部に近づける)
                shadeColor = group == PartGroup.Skin
                    ? new ColorData(1f, 0.93f, 0.93f, 1f)
                    : new ColorData(1f, 1f, 1f, 1f);
            }
            if (rimColor == null)
            {
                rimColor = new ColorData(0f, 0f, 0f, 1f);
            }
            if (outlineColor == null)
            {
                outlineColor = new ColorData(0f, 0f, 0f, 1f);
            }
        }
    }
}
```

`Settings/MaidSettings.cs`:

```csharp
using System;
using System.Xml.Serialization;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>メイド 1 人分の設定。GUID ごとに 1 ファイルで保存する</summary>
    [XmlRoot("MToonMaid")]
    public class MaidSettings
    {
        public const int CurrentVersion = 1;

        [XmlAttribute] public int version = CurrentVersion;
        public bool enabled;
        public GroupSettings skin;
        public GroupSettings face;
        public GroupSettings hair;
        public GroupSettings costume;

        public GroupSettings Get(PartGroup group)
        {
            switch (group)
            {
                case PartGroup.Skin: return skin;
                case PartGroup.Face: return face;
                case PartGroup.Hair: return hair;
                case PartGroup.Costume: return costume;
                default: throw new ArgumentOutOfRangeException(nameof(group), group, null);
            }
        }

        public void Set(PartGroup group, GroupSettings value)
        {
            switch (group)
            {
                case PartGroup.Skin: skin = value; break;
                case PartGroup.Face: face = value; break;
                case PartGroup.Hair: hair = value; break;
                case PartGroup.Costume: costume = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(group), group, null);
            }
        }

        /// <summary>欠けたグループと、各グループの null の参照型を既定で埋める</summary>
        public void FillMissing()
        {
            foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
            {
                var g = Get(group);
                if (g == null)
                {
                    Set(group, GroupSettings.CreateDefault(group));
                }
                else
                {
                    g.FillMissing(group);
                }
            }
        }
    }
}
```

- [ ] **Step 4: ストアを実装する**

`Settings/MaidSettingsStore.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// GUID ごとの設定ファイルの読み書き。読み込みはメイドごとに初回だけ、
    /// 書き込みはスライダー操作中に毎フレーム書かないよう変更から SaveDelay 秒後にまとめる
    /// </summary>
    public class MaidSettingsStore
    {
        public const float SaveDelay = 1f;

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(MaidSettings));

        private readonly string _directory;
        private readonly Action<string> _warn;
        private readonly Dictionary<string, MaidSettings> _cache = new Dictionary<string, MaidSettings>();
        private readonly HashSet<string> _dirty = new HashSet<string>();
        private float _lastChange;

        public MaidSettingsStore(string directory, Action<string> warn)
        {
            _directory = directory;
            _warn = warn;
        }

        public MaidSettings Get(string guid)
        {
            MaidSettings settings;
            if (!_cache.TryGetValue(guid, out settings))
            {
                settings = Load(GetPath(guid), _warn);
                _cache[guid] = settings;
            }
            return settings;
        }

        public void MarkDirty(string guid, float now)
        {
            _dirty.Add(guid);
            _lastChange = now;
        }

        public void FlushIfDue(float now)
        {
            if (_dirty.Count > 0 && now - _lastChange >= SaveDelay)
            {
                FlushAll();
            }
        }

        public void FlushAll()
        {
            if (_dirty.Count == 0)
            {
                return;
            }
            Directory.CreateDirectory(_directory);
            var failed = new List<string>();
            foreach (var guid in _dirty)
            {
                MaidSettings settings;
                if (!_cache.TryGetValue(guid, out settings))
                {
                    continue;
                }
                try
                {
                    Save(GetPath(guid), settings);
                }
                catch (Exception e)
                {
                    // 次の FlushIfDue で再試行する
                    failed.Add(guid);
                    _warn("設定の保存に失敗しました: " + guid + " " + e.Message);
                }
            }
            _dirty.Clear();
            foreach (var guid in failed)
            {
                _dirty.Add(guid);
            }
        }

        private string GetPath(string guid)
        {
            var name = guid;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return Path.Combine(_directory, name + ".xml");
        }

        internal static MaidSettings Load(string path, Action<string> warn)
        {
            MaidSettings settings = null;
            if (File.Exists(path))
            {
                try
                {
                    using (var stream = File.OpenRead(path))
                    {
                        settings = (MaidSettings)Serializer.Deserialize(stream);
                    }
                }
                catch (Exception e)
                {
                    // 壊れたファイルを次の保存で上書きしないよう退避し、このメイドは OFF で始める
                    var backup = path + ".bak";
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }
                    File.Move(path, backup);
                    warn("設定ファイルを読めないため " + backup + " へ退避しました: " + e.Message);
                    settings = null;
                }
            }

            if (settings == null)
            {
                settings = new MaidSettings();
            }
            settings.FillMissing();
            return settings;
        }

        internal static void Save(string path, MaidSettings settings)
        {
            settings.version = MaidSettings.CurrentVersion;
            // 書き込み中に落ちても既存のファイルを壊さないよう一時ファイルへ書いてから置き換える
            var temp = path + ".tmp";
            using (var stream = File.Create(temp))
            {
                Serializer.Serialize(stream, settings);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);
        }
    }
}
```

- [ ] **Step 5: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（全件）

- [ ] **Step 6: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/Settings source/COM3D25.MToonMaid.Plugin.Tests/MaidSettingsStoreTests.cs
git commit -m "feat: メイド単位の設定と保存を追加"
```

---

### Task 4: プロパティ定数と描画ステートの決定

UniVRM の `MToonValidator` のうち、このプラグインが使う不透明・Cutout の分岐とキーワード判定を移植する。判定は純粋な関数にしてテストし、Material への書き込みは Task 6 の Converter が行う。

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`, `Rendering/MToonRenderState.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/MToonRenderStateTests.cs`

**Interfaces:**
- Produces:
  - `MToonProps` の定数（下記コード。プロパティ名・キーワード・`AlphaModeOpaque = 0` / `AlphaModeCutout = 1`・`OutlineModeNone = 0` / `OutlineModeWorld = 1`）
  - `struct MToonRenderState { int srcBlend, dstBlend, zWrite, alphaToMask, cullMode; string renderType; List<string> enabledKeywords, disabledKeywords }`
  - `MToonRenderState.Resolve(int alphaMode, bool doubleSided, int outlineMode, bool hasParameterMap) : MToonRenderState`

- [ ] **Step 1: 失敗するテストを書く**

`MToonRenderStateTests.cs`:

```csharp
using System;
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class MToonRenderStateTests
    {
        [Fact]
        public void 不透明()
        {
            var s = MToonRenderState.Resolve(MToonProps.AlphaModeOpaque, false, MToonProps.OutlineModeNone, false);

            Assert.Equal(1, s.srcBlend);   // BlendMode.One
            Assert.Equal(0, s.dstBlend);   // BlendMode.Zero
            Assert.Equal(1, s.zWrite);
            Assert.Equal(0, s.alphaToMask);
            Assert.Equal(2, s.cullMode);   // Back
            Assert.Equal("Opaque", s.renderType);
            Assert.Contains(MToonProps.KeywordAlphaTest, s.disabledKeywords);
            Assert.Contains(MToonProps.KeywordOutlineWorld, s.disabledKeywords);
        }

        [Fact]
        public void Cutoutは両面でアルファテスト()
        {
            var s = MToonRenderState.Resolve(MToonProps.AlphaModeCutout, true, MToonProps.OutlineModeNone, false);

            Assert.Equal(1, s.alphaToMask);
            Assert.Equal(0, s.cullMode);   // Off
            Assert.Equal("TransparentCutout", s.renderType);
            Assert.Contains(MToonProps.KeywordAlphaTest, s.enabledKeywords);
        }

        [Fact]
        public void アウトラインとパラメーターマップのキーワード()
        {
            var s = MToonRenderState.Resolve(MToonProps.AlphaModeOpaque, false, MToonProps.OutlineModeWorld, true);

            Assert.Contains(MToonProps.KeywordOutlineWorld, s.enabledKeywords);
            Assert.Contains(MToonProps.KeywordOutlineScreen, s.disabledKeywords);
            Assert.Contains(MToonProps.KeywordParameterMap, s.enabledKeywords);
        }

        [Fact]
        public void 未対応のアルファモードは例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MToonRenderState.Resolve(2, false, MToonProps.OutlineModeNone, false));
        }
    }
}
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー

- [ ] **Step 3: 実装する**

`Rendering/MToonProps.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// COM3D25/MToon10 のプロパティ名とキーワード。
    /// 本家 MToon10 の _ShadeTex はゲームに合わせて _ShadowTex へ改名している
    /// </summary>
    public static class MToonProps
    {
        public const string ShaderName = "COM3D25/MToon10";

        // ゲームの Toony と同名 (変換で上書きするものは MaterialBackup に控える)
        public const string MainTex = "_MainTex";
        public const string Color = "_Color";
        public const string ShadowTex = "_ShadowTex";
        public const string RimColor = "_RimColor";
        public const string OutlineColor = "_OutlineColor";
        public const string OutlineWidth = "_OutlineWidth";
        public const string OutlineWidthTex = "_OutlineWidthTex";

        // MToon 固有
        public const string AlphaMode = "_AlphaMode";
        public const string DoubleSided = "_DoubleSided";
        public const string ShadeColor = "_ShadeColor";
        public const string ShadingShiftFactor = "_ShadingShiftFactor";
        public const string ShadingToonyFactor = "_ShadingToonyFactor";
        public const string GiEqualization = "_GiEqualization";
        public const string RimFresnelPower = "_RimFresnelPower";
        public const string RimLift = "_RimLift";
        public const string RimLightingMix = "_RimLightingMix";
        public const string OutlineWidthMode = "_OutlineWidthMode";
        public const string OutlineLightingMix = "_OutlineLightingMix";
        public const string ShadingShiftTex = "_ShadingShiftTex";
        public const string UvAnimMaskTex = "_UvAnimMaskTex";

        // 描画ステート (シェーダーの Pass が参照する)
        public const string SrcBlend = "_M_SrcBlend";
        public const string DstBlend = "_M_DstBlend";
        public const string ZWrite = "_M_ZWrite";
        public const string AlphaToMask = "_M_AlphaToMask";
        public const string CullMode = "_M_CullMode";

        public const string RenderTypeTag = "RenderType";

        public const string KeywordAlphaTest = "_ALPHATEST_ON";
        public const string KeywordAlphaBlend = "_ALPHABLEND_ON";
        public const string KeywordOutlineWorld = "_MTOON_OUTLINE_WORLD";
        public const string KeywordOutlineScreen = "_MTOON_OUTLINE_SCREEN";
        public const string KeywordParameterMap = "_MTOON_PARAMETERMAP";

        public const int AlphaModeOpaque = 0;
        public const int AlphaModeCutout = 1;
        public const int OutlineModeNone = 0;
        public const int OutlineModeWorld = 1;
    }
}
```

`Rendering/MToonRenderState.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// UniVRM MToonValidator (MIT) の移植。AlphaMode 等からブレンド・カリング・キーワードを決める。
    /// 置き換え対象は不透明・Cutout だけなので半透明の分岐は持たない。
    /// renderQueue は元の値へ戻す方針のためここでは決めない
    /// </summary>
    public struct MToonRenderState
    {
        // UnityEngine.Rendering.BlendMode / CullMode の数値
        private const int BlendOne = 1;
        private const int BlendZero = 0;
        private const int CullOff = 0;
        private const int CullBack = 2;

        public int srcBlend;
        public int dstBlend;
        public int zWrite;
        public int alphaToMask;
        public int cullMode;
        public string renderType;
        public List<string> enabledKeywords;
        public List<string> disabledKeywords;

        public static MToonRenderState Resolve(int alphaMode, bool doubleSided, int outlineMode, bool hasParameterMap)
        {
            var s = new MToonRenderState
            {
                srcBlend = BlendOne,
                dstBlend = BlendZero,
                zWrite = 1,
                cullMode = doubleSided ? CullOff : CullBack,
                enabledKeywords = new List<string>(),
                disabledKeywords = new List<string>(),
            };

            switch (alphaMode)
            {
                case MToonProps.AlphaModeOpaque:
                    s.alphaToMask = 0;
                    s.renderType = "Opaque";
                    break;
                case MToonProps.AlphaModeCutout:
                    s.alphaToMask = 1;
                    s.renderType = "TransparentCutout";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(alphaMode), alphaMode, "不透明・Cutout 以外は未対応");
            }

            s.Toggle(MToonProps.KeywordAlphaTest, alphaMode == MToonProps.AlphaModeCutout);
            s.Toggle(MToonProps.KeywordAlphaBlend, false);
            s.Toggle(MToonProps.KeywordOutlineWorld, outlineMode == MToonProps.OutlineModeWorld);
            s.Toggle(MToonProps.KeywordOutlineScreen, false);
            s.Toggle(MToonProps.KeywordParameterMap, hasParameterMap);
            return s;
        }

        private void Toggle(string keyword, bool enabled)
        {
            (enabled ? enabledKeywords : disabledKeywords).Add(keyword);
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（全件）

- [ ] **Step 5: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs source/COM3D25.MToonMaid.Plugin/Rendering/MToonRenderState.cs source/COM3D25.MToonMaid.Plugin.Tests/MToonRenderStateTests.cs
git commit -m "feat: MToon の描画ステートの決定を移植"
```

---

### Task 5: シェーダーのフォーク・バンドル化・読み込み

**Files:**
- Create: `UnityProject2022/Assets/Shaders/MToon10/`（UniVRM からコピーして改変）、`UnityProject2022/Assets/Editor/BuildMToonBundle.cs`
- Create: `build-bundle.bat`、`LICENSE-UniVRM.txt`
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonShaderProvider.cs`、`source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`（生成物）
- Modify: `.gitignore`、`release.bat`

**Interfaces:**
- Consumes: `MToonProps.ShaderName`、`MToonMaidPlugin.Log`
- Produces: `MToonShaderProvider.Load(ManualLogSource log) : Shader`（失敗時は null を返し、エラーログを出す）

- [ ] **Step 1: シェーダーをコピーしてフォークする**

```bash
cd /w/COM3D2_5/work/COM3D25.MToonMaid.Plugin
U=/w/COM3D2_5/work/UniVRM/Packages/VRM10/MToon10/Shaders
D=UnityProject2022/Assets/Shaders/MToon10
mkdir -p $D UnityProject2022/Assets/Editor
cp $U/vrmc_materials_mtoon.shader $U/*.hlsl $D/
cp /w/COM3D2_5/work/UniVRM/LICENSE.txt LICENSE-UniVRM.txt
# シェーダー名を変え、_ShadeTex をゲームと同じ _ShadowTex へ改名する
sed -i 's#Shader "VRM10/MToon10"#Shader "COM3D25/MToon10"#' $D/vrmc_materials_mtoon.shader
sed -i 's/\b_ShadeTex\b/_ShadowTex/g' $D/vrmc_materials_mtoon.shader $D/vrmc_materials_mtoon_input.hlsl $D/vrmc_materials_mtoon_lighting_mtoon.hlsl
grep -rn "_ShadeTex\|_ShadowTex\|Shader \"" $D
```

Expected: `_ShadeTex` が 1 件も出ず、`_ShadowTex` が `.shader` の Properties・`input.hlsl`・`lighting_mtoon.hlsl` の 3 か所に出る。シェーダー名は `COM3D25/MToon10`。

`vrmc_materials_mtoon.shader` の先頭に改変の注記を入れる:

```
// UniVRM (https://github.com/vrm-c/UniVRM) の VRM10/MToon10 を COM3D2.5 向けに改変 (MIT License)
// - シェーダー名を COM3D25/MToon10 に変更
// - _ShadeTex をゲームの Toony シェーダーと同じ _ShadowTex へ改名
```

- [ ] **Step 2: バンドルを作る Editor スクリプトを書く**

`UnityProject2022/Assets/Editor/BuildMToonBundle.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>COM3D25/MToon10 を mtoon_bundle にまとめる。build-bundle.bat から batchmode で呼ぶ</summary>
public static class BuildMToonBundle
{
    private const string OutputDir = "Bundles";

    public static void Run()
    {
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "mtoon_bundle",
                assetNames = new[] { "Assets/Shaders/MToon10/vrmc_materials_mtoon.shader" },
            },
        };
        Directory.CreateDirectory(OutputDir);
        BuildPipeline.BuildAssetBundles(OutputDir, builds, BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        Debug.Log("mtoon_bundle を作成しました");
    }
}
```

- [ ] **Step 3: build-bundle.bat を書く**

```bat
@echo off
chcp 65001 >nul
setlocal

cd /d %~dp0

rem COM3D25/MToon10 を Unity 2022.3.62 (COM3D2.5 本体と同じ版) でバンドル化し、埋め込みリソースへコピーする
rem 注意: Unity エディタで UnityProject2022 を開いていると batchmode がロックで失敗する

set ENV_FILE=%~dp0.env
if exist "%ENV_FILE%" (
    for /f "usebackq eol=# tokens=1,* delims==" %%a in ("%ENV_FILE%") do set "%%a=%%b"
)
if "%UNITY_2022_EXE%"=="" set "UNITY_2022_EXE=C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"
if not exist "%UNITY_2022_EXE%" (
    echo Unity が見つかりません: %UNITY_2022_EXE%
    echo .env に UNITY_2022_EXE を設定してください
    exit /b 1
)

set PROJECT_DIR=%~dp0UnityProject2022
set LOG_FILE=%PROJECT_DIR%\Logs\build-bundle.log
if not exist "%PROJECT_DIR%\Logs" mkdir "%PROJECT_DIR%\Logs"

"%UNITY_2022_EXE%" -batchmode -nographics -quit -projectPath "%PROJECT_DIR%" -executeMethod BuildMToonBundle.Run -logFile "%LOG_FILE%"
if %ERRORLEVEL% neq 0 (
    echo バンドルのビルドに失敗しました。ログ: %LOG_FILE%
    exit /b 1
)
findstr /c:"Shader error" "%LOG_FILE%" >nul
if %ERRORLEVEL% equ 0 (
    echo シェーダーのコンパイルエラーがあります。ログ: %LOG_FILE%
    exit /b 1
)

set SRC=%PROJECT_DIR%\Bundles\mtoon_bundle
set DST=%~dp0source\COM3D25.MToonMaid.Plugin\Resources\mtoon_bundle
if not exist "%SRC%" (
    echo 生成物が見つかりません: %SRC%
    exit /b 1
)
if not exist "%~dp0source\COM3D25.MToonMaid.Plugin\Resources" mkdir "%~dp0source\COM3D25.MToonMaid.Plugin\Resources"
copy /y "%SRC%" "%DST%" >nul
echo コピーしました: %DST%
exit /b 0
```

- [ ] **Step 4: .gitignore に Unity の生成物を追加する**

`.gitignore` の末尾に追加:

```
# Unity 2022 プロジェクトの生成物
UnityProject2022/Library/
UnityProject2022/Temp/
UnityProject2022/Logs/
UnityProject2022/UserSettings/
UnityProject2022/Bundles/
UnityProject2022/obj/
UnityProject2022/*.csproj
UnityProject2022/*.sln
```

- [ ] **Step 5: バンドルをビルドする**

Run: `cmd //c build-bundle.bat`（初回は Unity がプロジェクトを生成するため数分かかる）
Expected: `コピーしました: ...\Resources\mtoon_bundle`。`UnityProject2022/Logs/build-bundle.log` に `Shader error` が無い。`UnityProject2022/ProjectSettings/` と `Assets/**/*.meta` が生成される。

- [ ] **Step 6: 読み込み処理を書く**

`Rendering/MToonShaderProvider.cs`:

```csharp
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>DLL に埋め込んだ mtoon_bundle から COM3D25/MToon10 を読む</summary>
    public static class MToonShaderProvider
    {
        private const string ResourceName = "mtoon_bundle";

        /// <summary>読めない・今のグラフィックス API で描けないときは null。シェーダーは読み込んだまま保持する</summary>
        public static Shader Load(ManualLogSource log)
        {
            byte[] bytes;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    log.LogError("埋め込みリソース " + ResourceName + " が見つかりません。build-bundle.bat の実行漏れです");
                    return null;
                }
                bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
            }

            var bundle = AssetBundle.LoadFromMemory(bytes);
            if (bundle == null)
            {
                log.LogError(ResourceName + " を AssetBundle として読めませんでした");
                return null;
            }

            Shader found = null;
            foreach (var shader in bundle.LoadAllAssets<Shader>())
            {
                if (shader.name == MToonProps.ShaderName)
                {
                    found = shader;
                }
            }
            // 読み込んだシェーダーは残し、バンドルのファイル領域だけ解放する
            bundle.Unload(false);

            if (found == null)
            {
                log.LogError(ResourceName + " に " + MToonProps.ShaderName + " が含まれていません");
                return null;
            }
            if (!found.isSupported)
            {
                log.LogError(MToonProps.ShaderName + " は現在のグラフィックス API (" + SystemInfo.graphicsDeviceType + ") で使えません");
                return null;
            }
            return found;
        }
    }
}
```

- [ ] **Step 7: release.bat にライセンスを同梱する**

`release.bat` の dll コピーの直後に追加:

```bat
copy /y "LICENSE-UniVRM.txt" "output\%PLUGIN_NAME%\" >nul
```

- [ ] **Step 8: 埋め込みを含めてビルドが通ることを確認する**

Run:
```bash
dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug
powershell -NoProfile -Command "[Reflection.Assembly]::LoadFile('W:\COM3D2_5\work\COM3D25.MToonMaid.Plugin\source\COM3D25.MToonMaid.Plugin\bin\Debug\COM3D25.MToonMaid.dll').GetManifestResourceNames()"
```
Expected: ビルド成功。リソース名に `mtoon_bundle` が出る。

- [ ] **Step 9: Commit**

```bash
git add UnityProject2022/Assets UnityProject2022/ProjectSettings UnityProject2022/Packages build-bundle.bat LICENSE-UniVRM.txt .gitignore release.bat source/COM3D25.MToonMaid.Plugin/Rendering/MToonShaderProvider.cs source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle
git commit -m "feat: MToon10 をフォークしたシェーダーとバンドルの読み込みを追加"
```

---

### Task 6: MaterialConverter と控え

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialBackup.cs`, `Rendering/MaterialConverter.cs`

**Interfaces:**
- Consumes: `MToonProps`、`MToonRenderState.Resolve`、`GroupSettings`、`ColorData`、`PartGroup`、`ToonMaterialClassifier.HasOutline` / `IsCutout`
- Produces:
  - `MaterialBackup { Shader shader; int renderQueue; string[] keywords; PartGroup group; bool hadOutline; bool isCutout; float outlineWidth; Color rimColor; Color outlineColor }`
  - `MaterialConverter(Shader mtoonShader)`、`Shader MToonShader { get; }`
  - `MaterialBackup Convert(Material m, PartGroup group, GroupSettings settings)`
  - `void Reconvert(Material m, MaterialBackup backup, GroupSettings settings)`
  - `void ApplyGroup(Material m, MaterialBackup backup, GroupSettings settings)`
  - `bool Restore(Material m, MaterialBackup backup)`（現在のシェーダーが MToon のときだけ戻し、戻したら true）
  - `Convert` は途中で例外が出たらマテリアルを元へ戻してから再 throw する

Material を扱うため単体テストはできない。Task 10 の実機検証で確かめる。

- [ ] **Step 1: 控えを書く**

`Rendering/MaterialBackup.cs`:

```csharp
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// 変換前のマテリアルの状態。OFF で元へ戻すために使う。
    /// _OutlineWidth / _RimColor / _OutlineColor は Toony と MToon で同名のため変換で上書きされる。
    /// 戻さないとゲームのアウトライン幅やリム色が壊れるので値も控える
    /// </summary>
    public sealed class MaterialBackup
    {
        public Shader shader;
        public int renderQueue;
        public string[] keywords;
        public PartGroup group;
        public bool hadOutline;
        public bool isCutout;
        public float outlineWidth;
        public Color rimColor;
        public Color outlineColor;
    }
}
```

- [ ] **Step 2: Converter を書く**

`Rendering/MaterialConverter.cs`:

```csharp
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>Toony → COM3D25/MToon10 の変換と、元へ戻す処理</summary>
    public sealed class MaterialConverter
    {
        public Shader MToonShader { get; private set; }

        public MaterialConverter(Shader mtoonShader)
        {
            MToonShader = mtoonShader;
        }

        public MaterialBackup Convert(Material m, PartGroup group, GroupSettings settings)
        {
            var shaderName = m.shader.name;
            var backup = new MaterialBackup
            {
                shader = m.shader,
                renderQueue = m.renderQueue,
                keywords = m.shaderKeywords,
                group = group,
                hadOutline = ToonMaterialClassifier.HasOutline(shaderName),
                isCutout = ToonMaterialClassifier.IsCutout(shaderName),
                outlineWidth = m.HasProperty(MToonProps.OutlineWidth) ? m.GetFloat(MToonProps.OutlineWidth) : 0f,
                rimColor = m.HasProperty(MToonProps.RimColor) ? m.GetColor(MToonProps.RimColor) : Color.black,
                outlineColor = m.HasProperty(MToonProps.OutlineColor) ? m.GetColor(MToonProps.OutlineColor) : Color.black,
            };

            var hasShadowTex = m.HasProperty(MToonProps.ShadowTex) && m.GetTexture(MToonProps.ShadowTex) != null;
            var mainTex = m.GetTexture(MToonProps.MainTex);

            try
            {
                SwitchShader(m, backup, settings);

                // 影側のテクスチャを持たないシェーダーはメインを影にも使う
                if (!hasShadowTex)
                {
                    m.SetTexture(MToonProps.ShadowTex, mainTex);
                }
            }
            catch
            {
                // シェーダーだけ MToon になった半端な状態を残さない (呼び出し側は控えを持たないため戻せない)
                RestoreUnchecked(m, backup);
                throw;
            }
            return backup;
        }

        /// <summary>
        /// ゲーム (menu の shader コマンド等) が Toony へ戻したマテリアルを再変換する。
        /// 戻すときにゲームの最新の選択へ戻れるよう、シェーダーと renderQueue だけ控え直す
        /// </summary>
        public void Reconvert(Material m, MaterialBackup backup, GroupSettings settings)
        {
            backup.shader = m.shader;
            backup.renderQueue = m.renderQueue;
            backup.keywords = m.shaderKeywords;
            backup.hadOutline = ToonMaterialClassifier.HasOutline(m.shader.name);
            backup.isCutout = ToonMaterialClassifier.IsCutout(m.shader.name);
            SwitchShader(m, backup, settings);
        }

        public void ApplyGroup(Material m, MaterialBackup backup, GroupSettings settings)
        {
            m.SetColor(MToonProps.ShadeColor, ToColor(settings.shadeColor));
            m.SetFloat(MToonProps.ShadingShiftFactor, settings.shadingShift);
            m.SetFloat(MToonProps.ShadingToonyFactor, settings.shadingToony);
            m.SetFloat(MToonProps.GiEqualization, settings.giEqualization);

            m.SetColor(MToonProps.RimColor, settings.rimColorOverride ? ToColor(settings.rimColor) : backup.rimColor);
            m.SetFloat(MToonProps.RimFresnelPower, settings.rimFresnelPower);
            m.SetFloat(MToonProps.RimLift, settings.rimLift);
            m.SetFloat(MToonProps.RimLightingMix, settings.rimLightingMix);

            var outline = backup.hadOutline && settings.outlineEnabled;
            var outlineMode = outline ? MToonProps.OutlineModeWorld : MToonProps.OutlineModeNone;
            m.SetInt(MToonProps.OutlineWidthMode, outlineMode);
            m.SetFloat(MToonProps.OutlineWidth, settings.outlineWidth);
            m.SetFloat(MToonProps.OutlineLightingMix, settings.outlineLightingMix);
            m.SetColor(MToonProps.OutlineColor, settings.outlineColorOverride ? ToColor(settings.outlineColor) : backup.outlineColor);

            // アウトラインの有無でキーワードが変わるため描画ステートも決め直す
            ApplyRenderState(m, backup, outlineMode);
        }

        /// <summary>
        /// 元へ戻す。変換後に他者 (SE の NPR 化、ゲームの Toony への巻き戻し) がシェーダーを変えていたら
        /// その選択を優先して何もしない。戻したら true
        /// </summary>
        public bool Restore(Material m, MaterialBackup backup)
        {
            if (m.shader != MToonShader)
            {
                return false;
            }
            RestoreUnchecked(m, backup);
            return true;
        }

        private static void RestoreUnchecked(Material m, MaterialBackup backup)
        {
            m.shader = backup.shader;
            m.shaderKeywords = backup.keywords;
            m.SetOverrideTag(MToonProps.RenderTypeTag, "");
            m.SetFloat(MToonProps.OutlineWidth, backup.outlineWidth);
            m.SetColor(MToonProps.RimColor, backup.rimColor);
            m.SetColor(MToonProps.OutlineColor, backup.outlineColor);
            // シェーダーの代入で renderQueue は既定へ戻るため最後に書く
            m.renderQueue = backup.renderQueue;
        }

        private void SwitchShader(Material m, MaterialBackup backup, GroupSettings settings)
        {
            m.shader = MToonShader;
            m.SetInt(MToonProps.AlphaMode, backup.isCutout ? MToonProps.AlphaModeCutout : MToonProps.AlphaModeOpaque);
            // Cutout はレースや髪の房など裏面も見える用途が多いため両面にする
            m.SetInt(MToonProps.DoubleSided, backup.isCutout ? 1 : 0);
            ApplyGroup(m, backup, settings);
            // 前髪の 980 など、ゲームが決めた描画順を保つ
            m.renderQueue = backup.renderQueue;
        }

        private static void ApplyRenderState(Material m, MaterialBackup backup, int outlineMode)
        {
            var hasParameterMap = m.GetTexture(MToonProps.ShadingShiftTex) != null
                || m.GetTexture(MToonProps.OutlineWidthTex) != null
                || m.GetTexture(MToonProps.UvAnimMaskTex) != null;
            var s = MToonRenderState.Resolve(
                backup.isCutout ? MToonProps.AlphaModeCutout : MToonProps.AlphaModeOpaque,
                backup.isCutout,
                outlineMode,
                hasParameterMap);

            m.SetOverrideTag(MToonProps.RenderTypeTag, s.renderType);
            m.SetInt(MToonProps.SrcBlend, s.srcBlend);
            m.SetInt(MToonProps.DstBlend, s.dstBlend);
            m.SetInt(MToonProps.ZWrite, s.zWrite);
            m.SetInt(MToonProps.AlphaToMask, s.alphaToMask);
            m.SetInt(MToonProps.CullMode, s.cullMode);
            foreach (var k in s.enabledKeywords)
            {
                m.EnableKeyword(k);
            }
            foreach (var k in s.disabledKeywords)
            {
                m.DisableKeyword(k);
            }
        }

        private static Color ToColor(ColorData c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
    }
}
```

- [ ] **Step 3: ビルドが通ることを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: ビルド成功

- [ ] **Step 4: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/Rendering/MaterialBackup.cs source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs
git commit -m "feat: Toony と MToon の相互変換を追加"
```

---

### Task 7: MaidMToonController（1 メイド分の走査）

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs`

**Interfaces:**
- Consumes: `MaterialConverter`（`Convert` / `Reconvert` / `ApplyGroup` / `Restore` / `MToonShader`）、`ToonMaterialClassifier`、`MaidSettings`、`MaterialBackup`
- Produces:
  - `MaidMToonController(Maid maid, string guid, MaterialConverter converter, Action<string> warn)`
  - `Maid Maid { get; }`、`string Guid { get; }`（作成時の GUID）、`int ConvertedCount { get; }`、`int ScanInterval { get; set; }`（既定 10）
  - `void Tick(MaidSettings settings)`、`void ApplyGroup(PartGroup group, GroupSettings settings)`、`void RestoreAll()`

- [ ] **Step 1: Controller を書く**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の監視。非 busy の間に定期走査し、Toony を変換・再変換する。
    /// フックを使わず状態を揃える方式のため、作り直し直後の数フレームはバニラの見た目が出うる
    /// </summary>
    public sealed class MaidMToonController
    {
        private sealed class Entry
        {
            public Material material;
            public MaterialBackup backup;
        }

        private readonly MaterialConverter _converter;
        private readonly Action<string> _warn;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        // 変換で例外になったマテリアル。毎回同じ警告を出さないよう以後は飛ばす
        private readonly HashSet<int> _failed = new HashSet<int>();
        // 控えに無いのに MToon になっているマテリアル (renderer.materials による複製等)。OFF で戻せないため 1 回だけ知らせる
        private readonly HashSet<int> _unknownReported = new HashSet<int>();
        private readonly List<int> _removeBuffer = new List<int>();
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        // sharedMaterials は呼ぶたびに配列を作るため、再利用バッファへ受ける
        private readonly List<Material> _materialBuffer = new List<Material>();

        private bool _wasBusy = true;
        private int _framesSinceScan;

        public Maid Maid { get; private set; }
        public string Guid { get; private set; }
        public int ScanInterval { get; set; }
        public int ConvertedCount => _entries.Count;

        public MaidMToonController(Maid maid, string guid, MaterialConverter converter, Action<string> warn)
        {
            Maid = maid;
            Guid = guid;
            _converter = converter;
            _warn = warn;
            ScanInterval = 10;
        }

        public void Tick(MaidSettings settings)
        {
            if (Maid.IsBusy || !Maid.Visible || Maid.body0 == null)
            {
                _wasBusy = true;
                return;
            }
            if (!_wasBusy && ++_framesSinceScan < ScanInterval)
            {
                return;
            }
            _wasBusy = false;
            _framesSinceScan = 0;
            Scan(settings);
        }

        public void ApplyGroup(PartGroup group, GroupSettings settings)
        {
            foreach (var e in _entries.Values)
            {
                if (e.material != null && e.backup.group == group && e.material.shader == _converter.MToonShader)
                {
                    _converter.ApplyGroup(e.material, e.backup, settings);
                }
            }
        }

        public void RestoreAll()
        {
            foreach (var e in _entries.Values)
            {
                if (e.material != null)
                {
                    _converter.Restore(e.material, e.backup);
                }
            }
            _entries.Clear();
            _failed.Clear();
            _unknownReported.Clear();
        }

        private void Scan(MaidSettings settings)
        {
            RemoveDestroyed();

            var slots = Maid.body0.goSlot;
            for (var i = 0; i < slots.Count; i++)
            {
                for (var j = 0; j < slots.CountChildren(i); j++)
                {
                    var skin = slots[i, j];
                    if (skin == null || skin.obj == null)
                    {
                        continue;
                    }
                    var group = ToonMaterialClassifier.GetGroup(skin.Category);
                    skin.obj.GetComponentsInChildren(true, _rendererBuffer);
                    foreach (var renderer in _rendererBuffer)
                    {
                        // materials は複製を作るため sharedMaterials だけを読む
                        renderer.GetSharedMaterials(_materialBuffer);
                        foreach (var m in _materialBuffer)
                        {
                            if (m != null)
                            {
                                ScanMaterial(m, group, settings.Get(group));
                            }
                        }
                    }
                }
            }
        }

        private void ScanMaterial(Material m, PartGroup group, GroupSettings groupSettings)
        {
            var id = m.GetInstanceID();
            if (_failed.Contains(id))
            {
                return;
            }

            try
            {
                Entry entry;
                if (_entries.TryGetValue(id, out entry))
                {
                    // menu の shader コマンド等で Toony へ戻された
                    if (m.shader != _converter.MToonShader && ToonMaterialClassifier.IsTarget(m.shader.name))
                    {
                        _converter.Reconvert(m, entry.backup, groupSettings);
                    }
                }
                else if (ToonMaterialClassifier.IsTarget(m.shader.name))
                {
                    var backup = _converter.Convert(m, group, groupSettings);
                    _entries[id] = new Entry { material = m, backup = backup };
                }
                else if (m.shader == _converter.MToonShader && _unknownReported.Add(id))
                {
                    _warn("控えに無い MToon のマテリアルがあります (OFF で戻せません): " + m.name);
                }
            }
            catch (Exception e)
            {
                _failed.Add(id);
                _warn("マテリアルの変換に失敗したため以後は飛ばします: " + m.name + " " + e.Message);
            }
        }

        private void RemoveDestroyed()
        {
            _removeBuffer.Clear();
            foreach (var pair in _entries)
            {
                // Unity の == は破棄済みオブジェクトを null と判定する
                if (pair.Value.material == null)
                {
                    _removeBuffer.Add(pair.Key);
                }
            }
            foreach (var id in _removeBuffer)
            {
                _entries.Remove(id);
            }
        }
    }
}
```

- [ ] **Step 2: ビルドが通ることを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: ビルド成功

- [ ] **Step 3: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs
git commit -m "feat: メイドごとのマテリアル走査を追加"
```

---

### Task 8: プラグイン本体（Controller の管理と保存）

Controller は Maid インスタンスに結び付け、メイドが一時的に非表示になっても捨てない（Review Focus 1）。Maid が破棄されたときだけ外す。

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`
- Create: `source/COM3D25.MToonMaid.Plugin/IMToonMaidHost.cs`

**Interfaces:**
- Consumes: `MToonShaderProvider.Load`、`MaterialConverter`、`MaidSettingsStore`、`MaidMToonController`
- Produces: `interface IMToonMaidHost`（ウィンドウが使う）
  - `bool IsShaderAvailable { get; }`
  - `List<Maid> GetActiveMaids()`
  - `MaidSettings GetSettings(Maid maid)`
  - `void SetEnabled(Maid maid, bool enabled)`
  - `void NotifyGroupChanged(Maid maid, PartGroup group)`
  - `int GetConvertedCount(Maid maid)`

- [ ] **Step 1: ホストのインターフェースを書く**

`IMToonMaidHost.cs`:

```csharp
using System.Collections.Generic;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>ウィンドウからプラグイン本体への操作</summary>
    public interface IMToonMaidHost
    {
        bool IsShaderAvailable { get; }
        List<Maid> GetActiveMaids();
        MaidSettings GetSettings(Maid maid);
        void SetEnabled(Maid maid, bool enabled);
        void NotifyGroupChanged(Maid maid, PartGroup group);
        int GetConvertedCount(Maid maid);
    }
}
```

- [ ] **Step 2: MToonMaidPlugin を書き直す**

```csharp
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    [BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginFullName, PluginInfo.PluginVersion)]
    public class MToonMaidPlugin : BaseUnityPlugin, IMToonMaidHost
    {
        internal static ManualLogSource Log;

        private ConfigEntry<int> _scanInterval;
        private MaterialConverter _converter;
        private MaidSettingsStore _store;
        // Maid インスタンスごと。非表示の間も控えを失わないよう可視性では外さない
        private readonly Dictionary<Maid, MaidMToonController> _controllers = new Dictionary<Maid, MaidMToonController>();
        private readonly List<Maid> _removeBuffer = new List<Maid>();
        private readonly List<Maid> _activeMaids = new List<Maid>();

        public bool IsShaderAvailable => _converter != null;

        private void Awake()
        {
            Log = Logger;

            // MToon10 は Unity 5.6 (COM3D2 2.0) ではコンパイルできないため 2.5 専用
            if (!Application.unityVersion.StartsWith("2022.", System.StringComparison.Ordinal))
            {
                Log.LogError("MToonMaid は COM3D2.5 専用です。Unity " + Application.unityVersion + " では動作しません");
                enabled = false;
                return;
            }

            _scanInterval = Config.Bind("General", "ScanInterval", 10,
                "マテリアルを走査する間隔 (フレーム)。小さいほど着替え直後の追従が速く、負荷が増える");
            _store = new MaidSettingsStore(Path.Combine(Paths.ConfigPath, "COM3D25.MToonMaid"), Log.LogWarning);

            // MToon10 の built-in 版は URP では描画されない (ゲーム側に URP の切り替えがある)
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                Log.LogError("URP が有効なため MToonMaid は動作しません");
                return;
            }

            var shader = MToonShaderProvider.Load(Log);
            if (shader != null)
            {
                _converter = new MaterialConverter(shader);
                Log.LogInfo("MToonMaid 有効化");
            }
        }

        private void Update()
        {
            if (_converter == null)
            {
                return;
            }

            AttachControllers();
            TickControllers();
            _store.FlushIfDue(Time.realtimeSinceStartup);
        }

        private void OnApplicationQuit()
        {
            _store?.FlushAll();
        }

        /// <summary>表示中で ON のメイドに Controller を付ける</summary>
        private void AttachControllers()
        {
            foreach (var maid in GetActiveMaids())
            {
                // 生成直後は body0 / status が揃っていないことがあるため、status より先に body0 を見る
                if (_controllers.ContainsKey(maid) || maid.body0 == null || maid.status == null)
                {
                    continue;
                }
                var guid = maid.status.guid;
                if (_store.Get(guid).enabled)
                {
                    _controllers[maid] = new MaidMToonController(maid, guid, _converter, Log.LogWarning);
                }
            }
        }

        private void TickControllers()
        {
            _removeBuffer.Clear();
            foreach (var pair in _controllers)
            {
                // Unity の == は破棄済みの Maid を null と判定する。マテリアルも一緒に破棄されるので戻さない
                if (pair.Key == null)
                {
                    _removeBuffer.Add(pair.Key);
                    continue;
                }

                // ストック Maid の使い回しで別キャラが入った、または OFF にされた。ON のキャラ以外を変換しない
                var status = pair.Key.status;
                var settings = _store.Get(pair.Value.Guid);
                if (status == null || status.guid != pair.Value.Guid || !settings.enabled)
                {
                    pair.Value.RestoreAll();
                    _removeBuffer.Add(pair.Key);
                    continue;
                }

                pair.Value.ScanInterval = Mathf.Max(1, _scanInterval.Value);
                pair.Value.Tick(settings);
            }
            foreach (var maid in _removeBuffer)
            {
                _controllers.Remove(maid);
            }
        }

        public List<Maid> GetActiveMaids()
        {
            _activeMaids.Clear();
            var mgr = GameMain.Instance != null ? GameMain.Instance.CharacterMgr : null;
            if (mgr == null)
            {
                return _activeMaids;
            }
            for (var i = 0; i < mgr.GetMaidCount(); i++)
            {
                var maid = mgr.GetMaid(i);
                if (maid != null && maid.Visible)
                {
                    _activeMaids.Add(maid);
                }
            }
            return _activeMaids;
        }

        public MaidSettings GetSettings(Maid maid)
        {
            return _store.Get(maid.status.guid);
        }

        public void SetEnabled(Maid maid, bool value)
        {
            var settings = GetSettings(maid);
            if (settings.enabled == value)
            {
                return;
            }
            settings.enabled = value;
            _store.MarkDirty(maid.status.guid, Time.realtimeSinceStartup);

            MaidMToonController controller;
            if (!value && _controllers.TryGetValue(maid, out controller))
            {
                controller.RestoreAll();
                _controllers.Remove(maid);
            }
            // ON は次の Update の AttachControllers で付く
        }

        public void NotifyGroupChanged(Maid maid, PartGroup group)
        {
            _store.MarkDirty(maid.status.guid, Time.realtimeSinceStartup);
            MaidMToonController controller;
            if (_controllers.TryGetValue(maid, out controller))
            {
                controller.ApplyGroup(group, GetSettings(maid).Get(group));
            }
        }

        public int GetConvertedCount(Maid maid)
        {
            MaidMToonController controller;
            return _controllers.TryGetValue(maid, out controller) ? controller.ConvertedCount : 0;
        }
    }
}
```

`UnityEngine.Object` の `GetHashCode` / `Equals` はインスタンス ID で比べるため、破棄済みの Maid でもキーとして `Remove` できる。`pair.Key == null` は Unity の破棄判定。

- [ ] **Step 3: ビルドが通ることを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: ビルド成功

- [ ] **Step 4: テストが引き続き通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/IMToonMaidHost.cs source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs
git commit -m "feat: メイドごとの Controller の管理と設定の保存を追加"
```

---

### Task 9: ウィンドウ・ギアメニュー・ホットキー

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`

**Interfaces:**
- Consumes: `IMToonMaidHost`、`GroupSettings`、`ColorData`、`PartGroup`、`COM3D2.MotionTimelineEditor.GearMenu.Buttons`（`IsReady` / `Add(string name, string label, byte[] png, Action<GameObject>)`（png は null 可）/ `SetFrameColor`）
- Produces: `MToonMaidWindow(IMToonMaidHost host)`、`bool Visible { get; set; }`、`void Draw()`、`void UpdateCameraLock()`

- [ ] **Step 1: ウィンドウを書く**

`UI/MToonMaidWindow.cs`:

```csharp
using System;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>メイドの選択・ON/OFF・グループごとの調整値の IMGUI ウィンドウ</summary>
    public sealed class MToonMaidWindow
    {
        private const int WindowId = 0x4D546F6E; // "MTon"
        private const float LabelWidth = 120f;

        private static readonly string[] GroupLabels = { "肌", "顔", "髪", "衣装" };

        private readonly IMToonMaidHost _host;
        private Rect _rect = new Rect(20f, 20f, 380f, 600f);
        private Vector2 _scroll;
        private int _maidIndex;
        private PartGroup _group = PartGroup.Skin;
        // 自分がカメラ操作を止めたか。他プラグインが止めた状態を勝手に戻さないため
        private bool _cameraLockedByMe;

        public bool Visible { get; set; }

        public MToonMaidWindow(IMToonMaidHost host)
        {
            _host = host;
        }

        public void Draw()
        {
            if (Visible)
            {
                _rect = GUILayout.Window(WindowId, _rect, DrawContents, "MToonMaid");
            }
        }

        /// <summary>
        /// カーソルがウィンドウ上にある間はカメラ操作を止める。
        /// SE などと争わないよう、出入りした瞬間にだけ切り替える
        /// </summary>
        public void UpdateCameraLock()
        {
            var camera = GameMain.Instance != null ? GameMain.Instance.MainCamera : null;
            if (camera == null)
            {
                return;
            }
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            var over = Visible && _rect.Contains(mouse);
            if (over && !_cameraLockedByMe && camera.GetControl())
            {
                camera.SetControl(false);
                _cameraLockedByMe = true;
            }
            else if (!over && _cameraLockedByMe)
            {
                camera.SetControl(true);
                _cameraLockedByMe = false;
            }
            if (over)
            {
                // ホイールでのカメラ拡縮を止める
                Input.ResetInputAxes();
            }
        }

        private void DrawContents(int id)
        {
            if (!_host.IsShaderAvailable)
            {
                GUILayout.Label("シェーダーを読み込めないか、URP が有効なため動作しません。BepInEx のログを確認してください");
                GUI.DragWindow();
                return;
            }

            var maids = _host.GetActiveMaids();
            if (maids.Count == 0)
            {
                GUILayout.Label("表示中のメイドがいません");
                GUI.DragWindow();
                return;
            }

            _maidIndex = Mathf.Clamp(_maidIndex, 0, maids.Count - 1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30f)))
            {
                _maidIndex = (_maidIndex + maids.Count - 1) % maids.Count;
            }
            GUILayout.Label(maids[_maidIndex].status.fullNameJpStyle);
            if (GUILayout.Button(">", GUILayout.Width(30f)))
            {
                _maidIndex = (_maidIndex + 1) % maids.Count;
            }
            GUILayout.EndHorizontal();

            var maid = maids[_maidIndex];
            var settings = _host.GetSettings(maid);
            var enabled = GUILayout.Toggle(settings.enabled, "MToon にする");
            if (enabled != settings.enabled)
            {
                _host.SetEnabled(maid, enabled);
            }
            if (settings.enabled)
            {
                GUILayout.Label("変換済みマテリアル: " + _host.GetConvertedCount(maid));
            }

            _group = (PartGroup)GUILayout.Toolbar((int)_group, GroupLabels);

            _scroll = GUILayout.BeginScrollView(_scroll);
            var g = settings.Get(_group);
            GUI.changed = false;
            DrawGroup(g);
            if (GUILayout.Button("このグループを既定に戻す"))
            {
                settings.Set(_group, GroupSettings.CreateDefault(_group));
                GUI.changed = true;
            }
            if (GUI.changed)
            {
                _host.NotifyGroupChanged(maid, _group);
            }
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private static void DrawGroup(GroupSettings g)
        {
            GUILayout.Label("陰影");
            ColorRows("影色", g.shadeColor);
            g.shadingShift = Slider("境界の位置", g.shadingShift, -1f, 1f);
            g.shadingToony = Slider("境界の硬さ", g.shadingToony, 0f, 1f);
            g.giEqualization = Slider("環境光の均一化", g.giEqualization, 0f, 1f);

            GUILayout.Label("リム");
            g.rimColorOverride = GUILayout.Toggle(g.rimColorOverride, "色を上書きする");
            if (g.rimColorOverride)
            {
                ColorRows("リム色", g.rimColor);
            }
            g.rimFresnelPower = Slider("鋭さ", g.rimFresnelPower, 0f, 100f);
            g.rimLift = Slider("持ち上げ", g.rimLift, 0f, 1f);
            g.rimLightingMix = Slider("ライト色の影響", g.rimLightingMix, 0f, 1f);

            GUILayout.Label("アウトライン");
            g.outlineEnabled = GUILayout.Toggle(g.outlineEnabled, "アウトラインを描く");
            g.outlineWidth = Slider("幅", g.outlineWidth, 0f, 0.01f);
            g.outlineLightingMix = Slider("ライト色の影響", g.outlineLightingMix, 0f, 1f);
            g.outlineColorOverride = GUILayout.Toggle(g.outlineColorOverride, "色を上書きする");
            if (g.outlineColorOverride)
            {
                ColorRows("線の色", g.outlineColor);
            }
        }

        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(value.ToString("0.####"), GUILayout.Width(50f));
            GUILayout.EndHorizontal();
            return value;
        }

        private static void ColorRows(string label, ColorData c)
        {
            GUILayout.Label(label);
            c.r = Slider("  R", c.r, 0f, 1f);
            c.g = Slider("  G", c.g, 0f, 1f);
            c.b = Slider("  B", c.b, 0f, 1f);
        }
    }
}
```

- [ ] **Step 2: プラグイン本体にウィンドウ・ホットキー・ギアメニューをつなぐ**

`MToonMaidPlugin.cs` に以下を加える（既存のメンバーはそのまま）:

```csharp
// フィールド
private ConfigEntry<KeyboardShortcut> _toggleKey;
private MToonMaidWindow _window;
private GameObject _gearIcon;
```

`Awake` の `_scanInterval = ...` の直後:

```csharp
_toggleKey = Config.Bind("General", "ToggleWindow", new KeyboardShortcut(KeyCode.M, KeyCode.LeftControl, KeyCode.LeftShift),
    "ウィンドウの開閉キー");
_window = new MToonMaidWindow(this);
```

`Update` の先頭（`_converter == null` の判定より前。シェーダーが読めなくてもウィンドウで理由を見せるため）:

```csharp
if (_toggleKey.Value.IsDown())
{
    _window.Visible = !_window.Visible;
}
EnsureGearIcon();
_window.UpdateCameraLock();
```

メソッドを追加:

```csharp
private void OnGUI()
{
    _window?.Draw();
}

/// <summary>ギアメニューのアイコン。シーン遷移で破棄されるため無ければ作り直す</summary>
private void EnsureGearIcon()
{
    if (_gearIcon == null && COM3D2.MotionTimelineEditor.GearMenu.Buttons.IsReady)
    {
        // アイコン画像は null でシステムアイコンになる
        _gearIcon = COM3D2.MotionTimelineEditor.GearMenu.Buttons.Add(
            PluginInfo.PluginName, PluginInfo.PluginName, null,
            go => _window.Visible = !_window.Visible);
    }
    if (_gearIcon != null)
    {
        COM3D2.MotionTimelineEditor.GearMenu.Buttons.SetFrameColor(_gearIcon, _window.Visible ? Color.blue : Color.white);
    }
}
```

- [ ] **Step 3: ビルドが通ることを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: ビルド成功

- [ ] **Step 4: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs
git commit -m "feat: 設定ウィンドウとギアメニューを追加"
```

---

### Task 10: 実機検証と仕上げ

ゲームが DLL を掴んでいるため、反映には再起動が要る。**restart-verify スキル**（`com3d25-devbridge:restart-verify`）の手順で、ゲーム終了 → `debug.bat` で DLL を配置 → 起動 → セーブのロード → デイリー画面で検証する（撮影モードは非対応）。

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`（アウトライン幅の既定値。必要なときだけ）
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md`（未確定事項の結論）

- [ ] **Step 1: DLL を配置して起動する**

restart-verify スキルに従う。配置は `debug.bat`（ゲーム停止中に実行する）。起動後に devbridge の `tail_log` で `MToonMaid 有効化` が出ていること、`埋め込みリソース` / `AssetBundle` のエラーが無いことを確認する。

- [ ] **Step 2: ON にして変換対象を確かめる**

ウィンドウ（Ctrl+Shift+M）でメイドを ON にし、devbridge の `eval_csharp` で確認する:

プラグインと同じく `goSlot` を列挙する（`m_trBones` 配下だけでは取りこぼしうる）:

```csharp
System.Collections.Generic.IEnumerable<UnityEngine.Material> Mats(Maid maid) {
  var slots = maid.body0.goSlot;
  for (int i = 0; i < slots.Count; i++) for (int j = 0; j < slots.CountChildren(i); j++) {
    var sk = slots[i, j]; if (sk == null || sk.obj == null) continue;
    foreach (var r in sk.obj.GetComponentsInChildren<UnityEngine.Renderer>(true))
      foreach (var m in r.sharedMaterials) if (m != null) yield return m;
  }
}
var sb = new System.Text.StringBuilder();
foreach (var m in Mats(GameMain.Instance.CharacterMgr.GetMaid(0)))
  sb.Append(m.name + " " + m.shader.name + " q=" + m.renderQueue + "\n");
sb.ToString()
```

Expected: 不透明・Cutout は `COM3D25/MToon10` で、renderQueue は元の値（前髪 980 など）。`_Trans` / `_Trans_NoZ` / `Lighted_Trans` はそのまま。`screenshot` で描画が崩れていないことを見る。

- [ ] **Step 3: 無限色の追従を確かめる**

エディット画面で髪の色（無限色）を変え、`screenshot` で MToon のまま色が変わることを確認する（`_ShadowTex` 改名の効果）。

- [ ] **Step 4: 着替えと menu の再処理を確かめる**

衣装を着替える。`ScanInterval` フレーム以内に新しい衣装が `COM3D25/MToon10` になることを Step 2 のコードで確認する。shader コマンドを持つ menu（例: 夜伽で衣装が変わる場面）で Toony へ戻されても、再変換されることを確認する。

- [ ] **Step 5: OFF で元へ戻ることを確かめる**

ON にする前に、対象マテリアルのシェーダー・renderQueue・`shaderKeywords`・`_OutlineWidth`・`_RimColor`・`_OutlineColor` を記録しておく:

Step 2 の `Mats` を使う。キーワードは順序差で偽陽性にならないよう並べ替えて比べる:

```csharp
string Snap(UnityEngine.Material m) {
  var kw = (string[])m.shaderKeywords.Clone(); System.Array.Sort(kw, System.StringComparer.Ordinal);
  return m.shader.name + "|" + m.renderQueue + "|" + string.Join(",", kw)
    + "|" + (m.HasProperty("_OutlineWidth") ? m.GetFloat("_OutlineWidth") : -1)
    + "|" + (m.HasProperty("_RimColor") ? m.GetColor("_RimColor").ToString() : "")
    + "|" + (m.HasProperty("_OutlineColor") ? m.GetColor("_OutlineColor").ToString() : "");
}
var snap = new System.Collections.Generic.Dictionary<int, string>();
foreach (var m in Mats(GameMain.Instance.CharacterMgr.GetMaid(0))) snap[m.GetInstanceID()] = Snap(m);
snap.Count
```

ON → OFF の後に差分を数える:

```csharp
var diff = new System.Collections.Generic.List<string>();
foreach (var m in Mats(GameMain.Instance.CharacterMgr.GetMaid(0)))
  if (snap.ContainsKey(m.GetInstanceID()) && snap[m.GetInstanceID()] != Snap(m)) diff.Add(m.name + ": " + snap[m.GetInstanceID()] + " -> " + Snap(m));
string.Join("\n", diff.ToArray())
```

Expected: 空文字列。差分があれば Task 6 の Restore を直す。

- [ ] **Step 6: 一時的な非表示から戻ったメイドを確かめる**

ON のメイドを非表示にしてから再表示し（スタジオ等で表示を切り替える）、その後 OFF にして Step 5 と同じく元へ戻ることを確認する。

- [ ] **Step 7: アウトライン幅の単位を決める**

同じメイドの OFF（バニラ）と ON（既定幅 0.0015）の `screenshot` を同じ構図で撮り、線の太さを比べる。ほぼ同じなら既定値 0.0015 を維持する。大きく違えば `GroupSettings.outlineWidth` の既定値と UI の上限（0.01）を見直す。ゲームの `_OutlineWidthTex` を `eval_csharp` で読み出し（`GetPixels` で R と G が等しいか）、チャンネル構成も確認する。結論を spec の「未確定事項」に書く。

- [ ] **Step 8: ウィンドウ操作中のカメラを確かめる**

ウィンドウ上でスライダーをドラッグし、ホイールを回して、カメラが動かないことを確認する。ウィンドウ外へ出たらカメラが操作できることも確認する。SE を開いた状態でも同様に確認する。

- [ ] **Step 9: SE が NPR へ変えたマテリアルに触らないことを確かめる**

SE のマテリアル編集で、あるマテリアルを NPRShader（`com3d2mod/`）へ変えてから ON にし、そのマテリアルが NPR のままであることを確認する。逆に、ON にした後で SE が MToon のマテリアルを NPR へ変え、OFF にしても NPR のまま残る（`Restore` が他者の選択を上書きしない）ことも確認する。

- [ ] **Step 10: README に使い方を書く**

`README.md` に以下の節を追加する:

```markdown
## 導入

zip を解凍し、`BepInEx` フォルダをゲームのフォルダへ上書きコピーしてください。

## 使い方

1. ギアメニューの MToonMaid アイコン、または Ctrl+Shift+M でウィンドウを開きます
2. `<` `>` で対象のメイドを選び、「MToon にする」にチェックを入れます
3. 肌・顔・髪・衣装のタブで、影色・境界・リム・アウトラインを調整します

設定はメイドごとに `BepInEx/config/COM3D25.MToonMaid/` へ保存され、次回以降もどのシーンでも適用されます。

## 制限

- 目・まつ毛などの半透明マテリアルは置き換えません
- 髪の天使の輪 (`_HiTex`) とトゥーンランプ (`_ToonRamp`) は MToon に対応するものが無いため反映されません
- 着替えの直後、数フレームだけ元の見た目が出ることがあります
- 筋肉表現などの派生 Toony シェーダー（`Toony_Lighted_Muscle` 系など）も置き換えるため、その専用表現は失われます
- URP を有効にしている場合は動作しません
```

- [ ] **Step 11: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-08-mtoon-maid-design.md source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs
git commit -m "docs: 実機検証の結果と使い方を追加"
```

## レビュー却下メモ

- `Reconvert` で `_RimColor` / `_OutlineWidth` / `_OutlineColor` も控え直す — 巻き戻し時点の値は変換で上書きした自前の値（menu の `shader` コマンドはシェーダーだけを替える）なので、控え直すと元の値を失う
- アウトライン幅を元のマテリアルごとの値で引き継ぐトグル — 仕様の決定（グループ値）どおり。Task 10 Step 7 の結果次第で次版に回す
- UnityProject2022 の初回生成に `-createProject` が要る — 事前検証で `Assets` だけの状態から `-projectPath` の batchmode ビルドが成功済み（誤検知）
- `.env.sample` の Unity 版 `2022.3.62f2` が推測 — `C:\Program Files\Unity\Hub\Editor\2022.3.62f2` の存在を確認済み（誤検知）
