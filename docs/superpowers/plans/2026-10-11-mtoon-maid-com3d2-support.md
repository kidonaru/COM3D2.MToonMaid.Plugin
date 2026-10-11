# COM3D2 (2.0) 対応 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 同じソースから COM3D2.5 版と COM3D2 (2.0) 版の DLL を作り、2.0 でも MToonMaid が動くようにする。配布 zip には両方を入れる。

**Architecture:** csproj を `net471`（COM3D2.5）と `net35`（COM3D2）のマルチターゲットにし、ターゲットごとに参照するゲーム（`COM3D25_DIR` / `COM3D2_DIR`）と定義シンボル（`COM3D25` / `COM3D2`）を切り替える。2 つのバージョンで形の違う API は新しい `GameCompat` に集め、`#if COM3D2` はそこだけに書く。シェーダーは 1 つのソースに互換マクロを足し、Unity 2022.3（2.5 用）と Unity 5.6.4f1（2.0 用）でそれぞれバンドルを作って、ターゲットごとに `mtoon_bundle` の名前で埋め込む。

**Tech Stack:** C# 7.3（.NET 4.7.1 / .NET 3.5）、SDK 形式の csproj（`Microsoft.NETFramework.ReferenceAssemblies` で net35 をビルド）、HLSL（MToon10 フォーク）、Unity 2022.3.62f2 と 5.6.4f1 の batchmode、xUnit、バッチファイル。

**Spec:** ユーザーの指示「2.0 対応の計画を作って」と、前回の調査の結論（memory `com3d2-20-support-feasibility`）:
- 2.0 は Unity 5.6.4 / .NET 3.5 相当 / BepInEx 5.4.23（`W:\COM3D2\BepInEx` に導入済み）。`UnityEngine.dll` は分割されていない
- 2.0 の DLL でコンパイルすると、エラーは次の 4 種だけ: `TBody.goSlot` が `List<TBodySkin>`（`[i, j]` と `CountChildren` が無い）、`Renderer.GetSharedMaterials(List)` が無い、`GraphicsSettings.currentRenderPipeline` が無い、`IReadOnlyList<T>` が無い
- シェーダーは Unity 5.6.4f1 で、`UNITY_LIGHTING_COORDS` → `LIGHTING_COORDS`、`UNITY_TRANSFER_LIGHTING` → `TRANSFER_VERTEX_TO_FRAGMENT`、`UnpackNormalWithScale` → `UnpackScaleNormal` にし、URP の include を消し、d3d9 の頂点での `_OutlineWidthTex` の読み取りを直せばエラー 0
- 両対応のやり方は `COM3D2.PostEffects.Plugin` にならう（DLL 名は両方で同じ。同名なら間違えて入れても上書きになり、二重ロードしない）
- 未確認: 2.0 の Toony の見た目（陰影・リム・線の太さ）が、2.5 で測った式と同じか

## Global Constraints

- コードのコメントとログは日本語。コメントは原則「却下した案と、それで起きる問題」だけを書く（`<summary>` は名前で分からない契約に限り可）
- `#if COM3D2` / `#if COM3D25` は `GameCompat.cs` の中だけに書く。ほかのファイルは `GameCompat` を呼ぶ
- DLL 名 `COM3D25.MToonMaid.dll`、プラグイン GUID `com.kidonaru.com3d25.mtoonmaid`、設定フォルダ `BepInEx\config\COM3D25.MToonMaid` は 2 つのバージョンで同じにする（変えない）
- シェーダーの改変箇所には、今のやり方どおり `COM3D25:` の注記を付け、`vrmc_materials_mtoon.shader` の冒頭の改変一覧に 1 行足す
- Unity 5.6.4f1 の既定のパスは `C:\Program Files\Unity\Hub\Editor\5.6.4f1\Editor\Unity.exe`（`.env` の `UNITY_56_EXE` で上書きできる）
- `deploy.bat` / `deploy.ps1` は実行しない（このリポジトリには無いが、CLAUDE.md の禁止に従う）
- `debug.bat` はゲーム停止中だとゲームへ DLL をコピーする。実機へ入れたくないときは `dotnet build` を直接叩く
- `build-bundle.bat` は Unity エディタで `UnityProject2022` を開いていると失敗する
- `.env` の中身は読まない（権限で拒否される）。`COM3D2_DIR` の追記はユーザーに頼む

## Review Focus

1. **2.5 版が今までどおり動くこと**（シェーダーの互換マクロ・csproj のマルチターゲット化で 2.5 の見た目や読み込みが変わらない）→ Task 2 Step 1 / Step 8 で 2022 のバンドルのログの警告数を前後で比べ、Task 4 Step 1 で 2.5 の実機を Task 1 Step 0 のスクリーンショットと比べる
2. **`COM3D2_DIR` が無い開発環境**でも 2.5 版のビルド・`debug.bat`・テストが通ること → Task 1 Step 8（`dotnet test` は net471 だけを作る）、Task 3 Step 5（`debug.bat` の既定は com3d25）
3. **2.0 でシェーダーが使えない**場合（キーワード数の上限 256 を超える、グラフィックス API 非対応）は、クラッシュせずログにエラーが出るか、見た目の崩れで分かること → Task 4 Step 3 で 2.0 の `BepInEx\LogOutput.log` を検索し、ログが無くてもピンク・陰影なしでないかを見る
4. **間違ったバージョンの DLL を入れたとき**は、どちら向きでもゲームが落ちず、毎フレームの例外も出ないこと → Task 1 Step 5 の Unity のバージョンの判定（テストは Task 1 Step 3）、Task 4 Step 4 で両方向を一度ずつ確かめ、README の「インストール方法」に書く
5. **2.0 の Toony と見た目が合わない**（陰影の境界・リムの明るさ・線の太さ・影の受け方）→ Task 4 Step 5 で MToon にする前後を見比べ、ずれていれば README の「制限」に書いて、別の計画にする（この計画では式を直さない）

---

### Task 1: csproj をマルチターゲットにし、API の違いを GameCompat に集める

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj`
- Create: `source/COM3D25.MToonMaid.Plugin/GameCompat.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs:58-64`（Unity のバージョンの判定）、`:80`（URP の判定）、`:393-411`（`GetSlotCategories`）
- Create: `source/COM3D25.MToonMaid.Plugin.Tests/GameCompatTests.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs:44`（フィールド）、`:158-198`（`Scan` のスロットのループ）
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/HairShadowRenderer.cs:40`（フィールド）、`:159-185`（`CollectHairRenderers`）、`:204`（`DrawCasters`）
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidEyeGloss.cs:77`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/PresetStore.cs:51,69`
- Test: 既存の `source/COM3D25.MToonMaid.Plugin.Tests/EyePresetStoreTests.cs`・`MaidPresetTests.cs`（`Names` を使う）

**Interfaces:**
- Produces:
  - `internal static class GameCompat`
    - `void CollectSkins(TBody body, List<TBodySkin> buffer)` — 全スロット（CRC のサブスロットを含む）の、`obj` が読み込まれた `TBodySkin` を buffer へ入れる（先に Clear）
    - `void CollectSkins(TBody body, int slotNo, List<TBodySkin> buffer)` — 1 スロット分（2.5 はそのサブスロット全部）
    - `void GetSharedMaterials(Renderer renderer, List<Material> buffer)`
    - `bool IsScriptableRenderPipelineActive()`
    - `const string GameName` — `"COM3D2.5"` / `"COM3D2 (2.0)"`（ログに出す名前）
    - `bool IsExpectedUnityVersion(string unityVersion)` — この DLL が対象とするゲームの Unity か（2.5 版は `"2022."`、2.0 版は `"5."` で始まるか）
  - `PresetStore<T>.Names` の型が `ReadOnlyCollection<string>` になる
  - ビルド出力: `bin\<Config>\net471\COM3D25.MToonMaid.dll`（2.5）、`bin\<Config>\net35\COM3D25.MToonMaid.dll`（2.0）
  - 埋め込むバンドル: net471 は `Resources\mtoon_bundle`、net35 は `Resources\mtoon_bundle_com3d2`（どちらも LogicalName は `mtoon_bundle`）

`CollectSkins` / `GetSharedMaterials` / `IsScriptableRenderPipelineActive` はゲームの型（`TBody`）と Unity のネイティブ呼び出しの包み直しなので、xUnit のテストは書かない（テストはネイティブを呼ばない型だけを扱う方針。`Tests.csproj` のコメント参照）。確認はビルドが両方で通ることで行う。文字列だけを見る `IsExpectedUnityVersion` はテストを書く（テストは net471 = 2.5 版に対して走る）。

- [ ] **Step 0: 2.5 の変更前のスクリーンショットを撮る**（Task 4 Step 1 で見比べる基準）

2.5 が起動していれば、変更前の DLL のまま、MToon にしたメイドの全身と顔のアップを devbridge の `screenshot` で撮り、カメラ位置とメイド・シーンをメモしておく。起動していなければユーザーに起動を頼む。

- [ ] **Step 1: 変更前に、2.0 向けにはビルドが通らないことを確かめる**

`COM3D2_DIR` を渡して net35 だけビルドする（csproj を変える前なので、まだ net35 のターゲットが無いことを確かめる）。

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -f net35 "/p:COM3D2_DIR=W:\COM3D2"`
Expected: FAIL（`net35` がターゲットに無い、という NETSDK のエラー）

- [ ] **Step 2: csproj をマルチターゲットにする**

`COM3D25.MToonMaid.Plugin.csproj` の先頭の `PropertyGroup` から `<Reference>` の `ItemGroup` の終わりまでを、次のように書き換える（MTEUtils の `<Compile>` 一覧と `gear_icon.png`・`InternalsVisibleTo` はそのまま残す）。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- net471 = COM3D2.5 (Unity 2022.3)、net35 = COM3D2 (2.0, Unity 5.6)。build.bat が -f で片方ずつ作る -->
    <TargetFrameworks>net471;net35</TargetFrameworks>
    <AssemblyName>COM3D25.MToonMaid</AssemblyName>
    <RootNamespace>COM3D25.MToonMaid.Plugin</RootNamespace>
    <LangVersion>7.3</LangVersion>
    <!-- バージョンの単一ソースは PluginInfo.cs（bump-version.bat が書き換える） -->
    <!-- ゲームのインストール先。build.bat が .env から /p:COM3D25_DIR= / /p:COM3D2_DIR= で渡す -->
    <COM3D25_DIR Condition=" '$(COM3D25_DIR)' == '' ">W:\COM3D2_5</COM3D25_DIR>
    <!-- COM3D2_DIR に 2.5 の既定値を流用すると、2.0 版が 2.5 のアセンブリを参照して通ってしまうため、既定値を置かない -->
  </PropertyGroup>
  <PropertyGroup Condition=" '$(TargetFramework)' == 'net471' ">
    <GameVersion>COM3D25</GameVersion>
    <GameDir>$(COM3D25_DIR)</GameDir>
    <ShaderBundle>Resources\mtoon_bundle</ShaderBundle>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(TargetFramework)' == 'net35' ">
    <GameVersion>COM3D2</GameVersion>
    <GameDir>$(COM3D2_DIR)</GameDir>
    <ShaderBundle>Resources\mtoon_bundle_com3d2</ShaderBundle>
  </PropertyGroup>
  <PropertyGroup>
    <Managed>$(GameDir)\COM3D2x64_Data\Managed</Managed>
    <DefineConstants>$(DefineConstants);$(GameVersion)</DefineConstants>
  </PropertyGroup>
```

続けて、既存の `<EmbeddedResource Include="Resources\mtoon_bundle" ... />` の行を次に置き換える。

```xml
    <EmbeddedResource Include="$(ShaderBundle)" LogicalName="mtoon_bundle" Condition="Exists('$(ShaderBundle)')" />
```

`CheckShaderBundle` のターゲットを次に置き換え、`GameDir` が空のときのエラーも足す。

```xml
  <!-- 配布物にシェーダーが無いと実行時に何もできないため、Release ではバンドル欠落をビルドエラーにする -->
  <Target Name="CheckShaderBundle" BeforeTargets="BeforeBuild" Condition=" '$(Configuration)' == 'Release' And !Exists('$(ShaderBundle)') ">
    <Error Text="$(ShaderBundle) がありません。build-bundle.bat を実行してください" />
  </Target>
  <Target Name="CheckGameDir" BeforeTargets="BeforeBuild" Condition=" '$(GameDir)' == '' ">
    <Error Text="$(GameVersion) のゲームのインストール先が指定されていません。.env に COM3D2_DIR を設定し、build.bat からビルドしてください" />
  </Target>
```

`<Reference>` の `ItemGroup` を次の 3 つに分ける。

```xml
  <ItemGroup>
    <Reference Include="Assembly-CSharp"><HintPath>$(Managed)\Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Assembly-CSharp-firstpass"><HintPath>$(Managed)\Assembly-CSharp-firstpass.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine"><HintPath>$(Managed)\UnityEngine.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="BepInEx"><HintPath>$(GameDir)\BepInEx\core\BepInEx.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
  <!-- Unity 2022 の UnityEngine はモジュールに分かれている (5.6 は UnityEngine.dll 1 つ) -->
  <ItemGroup Condition=" '$(TargetFramework)' == 'net471' ">
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(Managed)\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.IMGUIModule"><HintPath>$(Managed)\UnityEngine.IMGUIModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.AssetBundleModule"><HintPath>$(Managed)\UnityEngine.AssetBundleModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.ImageConversionModule"><HintPath>$(Managed)\UnityEngine.ImageConversionModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.InputLegacyModule"><HintPath>$(Managed)\UnityEngine.InputLegacyModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.TextRenderingModule"><HintPath>$(Managed)\UnityEngine.TextRenderingModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.AnimationModule"><HintPath>$(Managed)\UnityEngine.AnimationModule.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
  <!-- Developer Pack の無い net35 を SDK 形式でビルドするための参照アセンブリ -->
  <ItemGroup Condition=" '$(TargetFramework)' == 'net35' ">
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>
```

`<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>` の行は消す（2 つの DLL が同じ `bin\<Config>\` に上書きし合うため）。

- [ ] **Step 3: Unity のバージョンの判定のテストを書く**

`source/COM3D25.MToonMaid.Plugin.Tests/GameCompatTests.cs`:

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class GameCompatTests
    {
        // テストは net471 (COM3D2.5 版) に対して走るため、2.5 版の判定だけを確かめる
        [Theory]
        [InlineData("2022.3.62f2", true)]
        [InlineData("5.6.4f1", false)]
        [InlineData("2018.4.0f1", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsExpectedUnityVersion(string unityVersion, bool expected)
        {
            Assert.Equal(expected, GameCompat.IsExpectedUnityVersion(unityVersion));
        }
    }
}
```

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests/COM3D25.MToonMaid.Plugin.Tests.csproj --filter GameCompatTests`
Expected: FAIL（`GameCompat` が無いというコンパイルエラー）

- [ ] **Step 4: GameCompat.cs を作る**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>COM3D2 (2.0) と COM3D2.5 で形の違うゲーム・Unity の API をそろえる</summary>
    internal static class GameCompat
    {
#if COM3D2
        public const string GameName = "COM3D2 (2.0)";
        private const string UnityVersionPrefix = "5.";
#else
        public const string GameName = "COM3D2.5";
        private const string UnityVersionPrefix = "2022.";
#endif

        /// <summary>この DLL が対象とするゲームの Unity か。unityVersion は Application.unityVersion</summary>
        public static bool IsExpectedUnityVersion(string unityVersion)
        {
            return unityVersion != null && unityVersion.StartsWith(UnityVersionPrefix, StringComparison.Ordinal);
        }

        /// <summary>全スロット (2.5 は CRC のサブスロットを含む) の、obj が読み込まれた TBodySkin を buffer へ入れる。buffer は先に空にする</summary>
        public static void CollectSkins(TBody body, List<TBodySkin> buffer)
        {
            buffer.Clear();
            var slots = body.goSlot;
#if COM3D2
            foreach (var skin in slots)
            {
                AddIfLoaded(skin, buffer);
            }
#else
            for (var i = 0; i < slots.Count; i++)
            {
                for (var j = 0; j < slots.CountChildren(i); j++)
                {
                    AddIfLoaded(slots[i, j], buffer);
                }
            }
#endif
        }

        /// <summary>slotNo のスロット (2.5 はそのサブスロット全部) の、obj が読み込まれた TBodySkin を buffer へ入れる。buffer は先に空にする</summary>
        public static void CollectSkins(TBody body, int slotNo, List<TBodySkin> buffer)
        {
            buffer.Clear();
#if COM3D2
            AddIfLoaded(body.goSlot[slotNo], buffer);
#else
            var slots = body.goSlot;
            for (var j = 0; j < slots.CountChildren(slotNo); j++)
            {
                AddIfLoaded(slots[slotNo, j], buffer);
            }
#endif
        }

        private static void AddIfLoaded(TBodySkin skin, List<TBodySkin> buffer)
        {
            if (skin != null && skin.obj != null)
            {
                buffer.Add(skin);
            }
        }

        public static void GetSharedMaterials(Renderer renderer, List<Material> buffer)
        {
#if COM3D2
            // Unity 5.6 には List を受ける GetSharedMaterials が無い。materials で取ると複製を作るため、配列を毎回作っても sharedMaterials を読む
            buffer.Clear();
            buffer.AddRange(renderer.sharedMaterials);
#else
            renderer.GetSharedMaterials(buffer);
#endif
        }

        public static bool IsScriptableRenderPipelineActive()
        {
#if COM3D2
            // GraphicsSettings.currentRenderPipeline は Unity 5.6 に無い (2.0 に URP の切り替えも無い)
            return false;
#else
            return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
#endif
        }
    }
}
```

- [ ] **Step 5: 呼び出し側を GameCompat へ置き換える**

`MToonMaidPlugin.cs:58-64`（`// MToon10 は Unity 5.6 …` のコメントから `return; }` まで）を次にする。今のコメントは「5.6 ではコンパイルできない」で、この計画で事実でなくなる。

```csharp
            // 2.5 版と 2.0 版は DLL 名が同じなので、取り違えて入れると、ゲームの型の違い (goSlot など) で毎フレーム例外になる
            if (!GameCompat.IsExpectedUnityVersion(Application.unityVersion))
            {
                Log.LogError("この MToonMaid は " + GameCompat.GameName + " 用です。Unity " + Application.unityVersion + " では動作しません。ゲームに合った版を入れてください");
                enabled = false;
                return;
            }
```

`MToonMaidPlugin.cs:80`:

```csharp
            if (GameCompat.IsScriptableRenderPipelineActive())
```

`MToonMaidPlugin.cs` の `GetSlotCategories` のスロットのループ（`var slots = maid.body0.goSlot;` から外側の `for` の閉じ括弧まで）を次にする。`_slotCategories` のフィールドの隣に `private readonly List<TBodySkin> _skinBuffer = new List<TBodySkin>();` を足す。

```csharp
            GameCompat.CollectSkins(maid.body0, _skinBuffer);
            foreach (var skin in _skinBuffer)
            {
                AddSlotCategory(skin.Category, group);
            }
```

`MaidMToonController.cs:44` の `_materialBuffer` の次の行に `private readonly List<TBodySkin> _skinBuffer = new List<TBodySkin>();` を足し、`Scan` の二重ループの頭（`var slots = Maid.body0.goSlot;` から `if (skin == null || skin.obj == null) { continue; }` まで）を次にする。外側のループの閉じ括弧を 1 つ減らし、中身のインデントを 1 段戻す。

```csharp
            GameCompat.CollectSkins(Maid.body0, _skinBuffer);
            foreach (var skin in _skinBuffer)
            {
                var group = ToonMaterialClassifier.GetGroup(skin.Category);
```

同じ `Scan` の `renderer.GetSharedMaterials(_materialBuffer);` を `GameCompat.GetSharedMaterials(renderer, _materialBuffer);` にする。

`HairShadowRenderer.cs:40` の `_materialBuffer` の次の行に `private readonly List<TBodySkin> _skinBuffer = new List<TBodySkin>();` を足し、`CollectHairRenderers` を次にする（`<summary>` の「CRC の goSlot[i, subNo]」は GameCompat に移ったので「サブスロット」に直す）。

```csharp
        /// <summary>前髪スロットの全サブスロットのレンダラーを集める。無ければ false</summary>
        private bool CollectHairRenderers(TBody body)
        {
            _rendererBuffer.Clear();
            if (!body.IsSlotNo(HairFrontSlot))
            {
                return false;
            }
            GameCompat.CollectSkins(body, body.GetSlotNo(HairFrontSlot), _skinBuffer);
            foreach (var skin in _skinBuffer)
            {
                // GetComponentsInChildren(list) はリストを上書きするため、サブスロットごとに足していく
                foreach (var r in skin.obj.GetComponentsInChildren<Renderer>(true))
                {
                    _rendererBuffer.Add(r);
                }
            }
            return _skinBuffer.Count > 0;
        }
```

`HairShadowRenderer.cs:204` と `MaidEyeGloss.cs:77` の `renderer.GetSharedMaterials(_materialBuffer);` を `GameCompat.GetSharedMaterials(renderer, _materialBuffer);` にする。

置き換え漏れが無いことを確かめる。

Run: `rg -n "goSlot|GetSharedMaterials\(_|currentRenderPipeline" source/COM3D25.MToonMaid.Plugin -g '*.cs' -g '!obj/**'`
Expected: `GameCompat.cs` の行だけが出る

- [ ] **Step 6: PresetStore.Names を .NET 3.5 にある型へ変える**

`PresetStore.cs` の先頭に `using System.Collections.ObjectModel;` を足し、`_names` のフィールドの次の行に読み取り専用の包みを足す。

```csharp
        private readonly List<string> _names = new List<string>();
        private readonly ReadOnlyCollection<string> _readOnlyNames;
```

コンストラクタの最後に `_readOnlyNames = _names.AsReadOnly();` を足し、69 行目を次にする。

```csharp
        public ReadOnlyCollection<string> Names => _readOnlyNames;
```

- [ ] **Step 7: 両方のターゲットでビルドする**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -f net471 "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: エラー 0（警告が増えていないこと）

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -f net35 "/p:COM3D2_DIR=W:\COM3D2"`
Expected: エラー 0。`bin\Debug\net35\COM3D25.MToonMaid.dll` ができる（バンドルはまだ無いので Debug では埋め込まれない）。事前調査はゲームの mscorlib で通したので、参照アセンブリでは .NET 3.5 に無い API のエラーが出るかもしれない。出たら、その箇所を .NET 3.5 にある API に書き換える（ゲームのバージョンで分かれる処理なら `GameCompat` に集める）

`COM3D2_DIR` を渡さないときのエラーを確かめる。

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -f net35`
Expected: FAIL「COM3D2 のゲームのインストール先が指定されていません。…」

- [ ] **Step 8: 既存のテストを通す**

`COM3D2_DIR` を渡さずに実行し、テストが net471 だけを作ることも確かめる。

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests/COM3D25.MToonMaid.Plugin.Tests.csproj`
Expected: 全件 PASS（Step 3 の `GameCompatTests` を含む）。テストがビルド出力のパス（`bin\Debug\` 直下）を直書きしていないことも、`rg -n "bin[/\\\\]" source/COM3D25.MToonMaid.Plugin.Tests -g '*.cs'` が何も出さないことで確かめる

- [ ] **Step 9: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin
git commit -m "feat(compat): COM3D2 (2.0) 向けに net35 でもビルドできるようにする"
```

---

### Task 2: シェーダーを Unity 5.6 でもコンパイルできるようにし、2.0 用のバンドルを作る

**Files:**
- Create: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_compat.hlsl`（と、Unity が作る `.meta`）
- Delete: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_depthnormals_vertex.hlsl`・`_depthnormals_fragment.hlsl`・`_depthonly_vertex.hlsl`・`_depthonly_fragment.hlsl`（と各 `.meta`）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_render_pipeline.hlsl:4-14`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_define.hlsl:4-7`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader:1-13`（改変一覧）
- Modify: `UnityProject2022/Assets/Shaders/MToonMaid/MToonMaidEye.shader:32-34`
- Modify: `build-bundle.bat`
- Modify: `.gitignore`
- Regenerate: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`
- Create: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle_com3d2`

**Interfaces:**
- Consumes: Task 1 の `ShaderBundle`（net35 は `Resources\mtoon_bundle_com3d2` を埋め込む）
- Produces: `build-bundle.bat [com3d25|com3d2|all]`（既定 all）。Unity 5.6 のプロジェクトは `UnityProject56\`（git で追跡しない。毎回 `UnityProject2022\Assets` から写す）

- [ ] **Step 1: 変更前に、2022 の警告数を記録し、5.6 でエラーになることを確かめる**

今の `build-bundle.bat`（2022 だけを作る）を一度実行し、警告数を基準として記録する（Step 8 で比べる）。

Run: `cmd //c build-bundle.bat` → `rg -c "Shader warning" UnityProject2022/Logs/build-bundle.log`
Expected: 数が出る（0 なら「何も出ない」）。この数をメモする。生成された `Resources\mtoon_bundle` は `git checkout -- source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle` で戻す

Step 6 の `build-bundle.bat` ができるまでは、手で一時プロジェクトを作って確かめる（前回の調査と同じ手順）。

```bash
S="$TEMP/mtoon56-check"; rm -rf "$S"; mkdir -p "$S/Assets"
cp -r UnityProject2022/Assets/Shaders UnityProject2022/Assets/Editor "$S/Assets/"
"/c/Program Files/Unity/Hub/Editor/5.6.4f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath "$(cygpath -w "$S")" -executeMethod BuildMToonBundle.Run -logFile "$(cygpath -w "$S")\\build.log"
rg "Shader error" "$S/build.log" | sort -u
```

Expected: `UnpackNormalWithScale`・`UNITY_LIGHTING_COORDS`・`Packages/com.unity.render-pipelines.universal` の include のエラーが出る

- [ ] **Step 2: 互換マクロのファイルを作る**

`vrmc_materials_mtoon_compat.hlsl`:

```hlsl
#ifndef VRMC_MATERIALS_MTOON_COMPAT_INCLUDED
#define VRMC_MATERIALS_MTOON_COMPAT_INCLUDED

// COM3D25: COM3D2 (2.0) の Unity 5.6 に無い名前を旧名で補う。
// 本体を旧名へ書き換えると、2022 で UNITY_TRANSFER_LIGHTING の lightmap UV の受け渡しが無くなり、2.5 の描画が変わる
#if UNITY_VERSION < 201700
#define UNITY_LIGHTING_COORDS(idx1, idx2) LIGHTING_COORDS(idx1, idx2)
#define UNITY_TRANSFER_LIGHTING(a, coord) TRANSFER_VERTEX_TO_FRAGMENT(a)
#define UnpackNormalWithScale(packednormal, bumpScale) UnpackScaleNormal(packednormal, bumpScale)
#endif

#endif
```

（`TRANSFER_VERTEX_TO_FRAGMENT` は頂点入力の変数名 `v` と `v.vertex` を前提にする。MToon10 の `vrmc_materials_mtoon_forward_vertex.hlsl` も `MToonMaidEye.shader` の `vert` も `v` / `v.vertex` なのでそのまま使える）

- [ ] **Step 3: URP の include を消し、互換マクロを読み込む**

`vrmc_materials_mtoon_render_pipeline.hlsl` の 4〜14 行目を次にする。

```hlsl
// Include
// COM3D25: URP の include は消した。Unity 5.6 は #ifdef の中でも include を探してエラーにする (プラグインは URP では動かない)
#ifndef MTOON_URP
#include <UnityCG.cginc>
#include <AutoLight.cginc>
#include <Lighting.cginc>
#include <UnityShaderVariables.cginc>
#include "./vrmc_materials_mtoon_compat.hlsl"
#endif
```

URP 専用で、どのシェーダーからも読み込まれていない depth 系の 4 ファイル（と `.meta`）を消す。Unity 5.6 は Assets にある .hlsl を単独でも調べ、中の URP の include でエラーにするため。

```bash
git rm UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_depthnormals_vertex.hlsl* \
       UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_depthnormals_fragment.hlsl* \
       UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_depthonly_vertex.hlsl* \
       UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_depthonly_fragment.hlsl*
```

消す前に、どこからも読まれていないことを確かめる。

Run: `rg -n -i "depthonly|depthnormals" UnityProject2022/Assets/Shaders -g '!*depthonly*' -g '!*depthnormals*'`
Expected: 何も出ない

`MToonMaidEye.shader` の `CGINCLUDE` の include の後（34 行目の `#include "AutoLight.cginc"` の次）に足す。

```hlsl
    #include "../MToon10/vrmc_materials_mtoon_compat.hlsl"
```

- [ ] **Step 4: d3d9 の頂点でのテクスチャ読み取りを直す**

`vrmc_materials_mtoon_define.hlsl` の 4〜7 行目を次にする。

```hlsl
// TEX2D with LOD
#ifndef UNITY_SAMPLE_TEX2D_LOD
    #if defined(SHADER_API_D3D9)
        // COM3D25: Unity 5.6 の d3d9 では UNITY_DECLARE_TEX2D が sampler2D だけを宣言し、sampler##tex が無いため SampleLevel を使えない
        #define UNITY_SAMPLE_TEX2D_LOD(tex,coord,lod) tex2Dlod (tex, float4 (coord, 0, lod))
    #else
        #define UNITY_SAMPLE_TEX2D_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord,lod)
    #endif
#endif
```

- [ ] **Step 5: 改変一覧に追記する**

`vrmc_materials_mtoon.shader` の冒頭の改変一覧の最後（`Shader "COM3D25/MToon10"` の直前の行）に足す。

```hlsl
// - COM3D2 (2.0) の Unity 5.6 でもコンパイルできるよう、URP の include と URP 専用の depth 系のファイルを消し、5.6 に無いマクロを vrmc_materials_mtoon_compat.hlsl で補う
```

- [ ] **Step 6: build-bundle.bat を 2 つの Unity に対応させる**

`build-bundle.bat` を次に置き換える。

```bat
@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

cd /d %~dp0

rem COM3D25/MToon10 などのシェーダーをバンドル化し、埋め込みリソースへコピーする
rem   com3d25: Unity 2022.3.62 (COM3D2.5 本体と同じ版) で UnityProject2022 から mtoon_bundle を作る
rem   com3d2 : Unity 5.6.4f1 (COM3D2 本体と同じ版) で UnityProject56 から mtoon_bundle_com3d2 を作る
rem 注意: Unity エディタで UnityProject2022 を開いていると batchmode がロックで失敗する
rem 引数: %1 = com3d25/com3d2/all (既定 all)

set TARGET=%~1
if "%TARGET%"=="" set TARGET=all
if /i not "%TARGET%"=="com3d2" if /i not "%TARGET%"=="com3d25" if /i not "%TARGET%"=="all" (
    echo 不正なターゲットです: %TARGET% ^(com3d2 / com3d25 / all のいずれかを指定してください^)
    exit /b 1
)

set ENV_FILE=%~dp0.env
if exist "%ENV_FILE%" (
    for /f "usebackq eol=# tokens=1,* delims==" %%a in ("%ENV_FILE%") do set "%%a=%%b"
)
if "%UNITY_2022_EXE%"=="" set "UNITY_2022_EXE=C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"
if "%UNITY_56_EXE%"=="" set "UNITY_56_EXE=C:\Program Files\Unity\Hub\Editor\5.6.4f1\Editor\Unity.exe"

set RESOURCES_DIR=%~dp0source\COM3D25.MToonMaid.Plugin\Resources
if not exist "%RESOURCES_DIR%" mkdir "%RESOURCES_DIR%"

if /i not "%TARGET%"=="com3d2" (
    call :build "%UNITY_2022_EXE%" UNITY_2022_EXE "%~dp0UnityProject2022" mtoon_bundle
    if !ERRORLEVEL! neq 0 exit /b 1
)
if /i not "%TARGET%"=="com3d25" (
    rem シェーダーの原本は UnityProject2022 だけに置く。.meta は 2022 の形式なので写さず、5.6 に作り直させる
    robocopy "%~dp0UnityProject2022\Assets" "%~dp0UnityProject56\Assets" /MIR /XF *.meta /NFL /NDL /NJH /NJS /NP >nul
    if !ERRORLEVEL! geq 8 (
        echo UnityProject56 へのコピーに失敗しました
        exit /b 1
    )
    call :build "%UNITY_56_EXE%" UNITY_56_EXE "%~dp0UnityProject56" mtoon_bundle_com3d2
    if !ERRORLEVEL! neq 0 exit /b 1
)
exit /b 0

rem 引数: %1 = Unity.exe, %2 = .env の変数名, %3 = プロジェクト, %4 = コピー先のファイル名
:build
if not exist "%~1" (
    echo Unity が見つかりません: %~1
    echo .env に %~2 を設定してください
    exit /b 1
)
set PROJECT_DIR=%~3
set LOG_FILE=%PROJECT_DIR%\Logs\build-bundle.log
if not exist "%PROJECT_DIR%\Logs" mkdir "%PROJECT_DIR%\Logs"

"%~1" -batchmode -nographics -quit -projectPath "%PROJECT_DIR%" -executeMethod BuildMToonBundle.Run -logFile "%LOG_FILE%"
if !ERRORLEVEL! neq 0 (
    echo バンドルのビルドに失敗しました。ログ: %LOG_FILE%
    exit /b 1
)
rem Unity はシェーダーのコンパイルエラーがあっても終了コード 0 で抜けるため、ログを検査する
findstr /c:"Shader error" "%LOG_FILE%" >nul
if !ERRORLEVEL! equ 0 (
    echo シェーダーのコンパイルエラーがあります。ログ: %LOG_FILE%
    exit /b 1
)

set SRC=%PROJECT_DIR%\Bundles\mtoon_bundle
if not exist "%SRC%" (
    echo 生成物が見つかりません: %SRC%
    exit /b 1
)
copy /y "%SRC%" "%RESOURCES_DIR%\%~4" >nul
echo コピーしました: %RESOURCES_DIR%\%~4
exit /b 0
```

`.gitignore` の「Unity 2022 プロジェクトの生成物」の段の後に足す。

```gitignore

# Unity 5.6 プロジェクト。build-bundle.bat が UnityProject2022\Assets から毎回作る
UnityProject56/
```

- [ ] **Step 7: 両方のバンドルを作る**

Unity エディタで `UnityProject2022` を開いていないことを確かめてから実行する。

Run: `cmd //c build-bundle.bat all`
Expected: 「コピーしました: …\mtoon_bundle」「コピーしました: …\mtoon_bundle_com3d2」の 2 行。`Resources\mtoon_bundle_com3d2` ができる

- [ ] **Step 8: 2.5 側のバンドルに影響が無いことを確かめる**

Run: `rg -c "Shader warning" UnityProject2022/Logs/build-bundle.log`
Expected: Step 1 でメモした数と同じ。増えていれば、増えた警告を読んで原因を直す

Run: `rg "Shader warning|Shader error" UnityProject56/Logs/build-bundle.log | sort -u`
Expected: 何も出ない

- [ ] **Step 9: Release で両方のバンドルが埋め込まれることを確かめる**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Release -f net35 "/p:COM3D2_DIR=W:\COM3D2"`
Expected: エラー 0（`CheckShaderBundle` が通る）

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Release -f net471 "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: エラー 0

- [ ] **Step 10: Commit**

```bash
git add .gitignore build-bundle.bat UnityProject2022/Assets/Shaders source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle_com3d2
git commit -m "feat(shader): Unity 5.6 でもコンパイルできるようにし、COM3D2 用のバンドルを作る"
```

---

### Task 3: ビルド・配布のスクリプトを 2 つのバージョンに対応させる

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/build.bat`
- Modify: `debug.bat`
- Modify: `release.bat`
- Modify: `.env.sample`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: Task 1 のビルド出力 `bin\<Config>\net471\` / `bin\<Config>\net35\`
- Produces:
  - `build.bat <debug|release> [com3d25|com3d2|all]`（第 2 引数の既定 all）
  - `debug.bat [com3d25|com3d2|all]`（既定 com3d25）
  - リポジトリ内の配置: 2.5 版 `BepInEx\plugins\COM3D25.MToonMaid.dll`、2.0 版 `BepInEx (COM3D2)\plugins\COM3D25.MToonMaid.dll`
  - 配布 zip `output\COM3D25.MToonMaid.Plugin-v<版>.zip` の中に `BepInEx`（2.5 用）と `BepInEx (COM3D2)`（2.0 用）

- [ ] **Step 1: build.bat を書き換える**

`source/COM3D25.MToonMaid.Plugin/build.bat` を次に置き換える。

```bat
@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

cd /d %~dp0

set PLUGIN_NAME=COM3D25.MToonMaid.Plugin
set ASSEMBLY_NAME=COM3D25.MToonMaid
set REPO_DIR=%~dp0..\..

rem 引数: %1 = debug/release (既定 release), %2 = com3d2/com3d25/all (既定 all)
set CONFIG=Release
if /i "%~1"=="debug" set CONFIG=Debug

set TARGET=%~2
if "%TARGET%"=="" set TARGET=all
if /i not "%TARGET%"=="com3d2" if /i not "%TARGET%"=="com3d25" if /i not "%TARGET%"=="all" (
    echo 不正なターゲットです: %TARGET% ^(com3d2 / com3d25 / all のいずれかを指定してください^)
    exit /b 1
)

rem .env からゲームのインストール先を読み込む ※開発者ごとの設定
set ENV_FILE=%REPO_DIR%\.env
if not exist "%ENV_FILE%" (
    echo .env が見つかりません: %ENV_FILE%
    echo .env.sample をコピーして .env を作成し、パスを設定してください
    exit /b 1
)
for /f "usebackq eol=# tokens=1,* delims==" %%a in ("%ENV_FILE%") do set "%%a=%%b"

rem ビルド対象に必要な変数だけ検証する
if /i not "%TARGET%"=="com3d2" (
    if "%COM3D25_DIR%"=="" (
        echo .env に COM3D25_DIR が設定されていません
        exit /b 1
    )
    echo COM3D25_DIR: %COM3D25_DIR%
)
if /i not "%TARGET%"=="com3d25" (
    if "%COM3D2_DIR%"=="" (
        echo .env に COM3D2_DIR が設定されていません
        exit /b 1
    )
    echo COM3D2_DIR: %COM3D2_DIR%
)

if /i not "%TARGET%"=="com3d2" (
    call :build net471 "COM3D2.5" "/p:COM3D25_DIR=%COM3D25_DIR%" "%REPO_DIR%\BepInEx\plugins" "%COM3D25_DIR%"
    if !ERRORLEVEL! neq 0 exit /b 1
)
if /i not "%TARGET%"=="com3d25" (
    call :build net35 "COM3D2" "/p:COM3D2_DIR=%COM3D2_DIR%" "%REPO_DIR%\BepInEx (COM3D2)\plugins" "%COM3D2_DIR%"
    if !ERRORLEVEL! neq 0 exit /b 1
)
exit /b 0

rem 引数: %1 = TargetFramework, %2 = 表示名, %3 = ゲームの場所の MSBuild 引数, %4 = リポジトリ内のコピー先, %5 = ゲームのインストール先
:build
echo === ビルド中 ^(%~2 / %CONFIG%^) ===
rem Release は配布物になるため常にクリーンビルドする
set BUILD_OPTIONS=
if "%CONFIG%"=="Release" set BUILD_OPTIONS=--no-incremental

dotnet build %PLUGIN_NAME%.csproj -c %CONFIG% -f %~1 %BUILD_OPTIONS% "%~3"
if !ERRORLEVEL! neq 0 (
    echo %~2 版のビルドに失敗しました
    exit /b 1
)

rem リリースパッケージ用に リポジトリ内へコピー
if not exist "%~4" mkdir "%~4"
copy /y bin\%CONFIG%\%~1\%ASSEMBLY_NAME%.dll "%~4\" >nul
if !ERRORLEVEL! neq 0 (
    echo dllのコピーに失敗しました
    exit /b 1
)

rem ゲームへのデプロイ ※ゲーム起動中は BepInEx が dll をロックするため失敗しても続行
copy /y bin\%CONFIG%\%~1\%ASSEMBLY_NAME%.dll "%~5\BepInEx\plugins\" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: %~2 へのデプロイに失敗しました ^(ゲーム起動中?^)
) else (
    echo %~2 へデプロイしました
)
exit /b 0
```

- [ ] **Step 2: debug.bat に対象の引数を足す**

`debug.bat` を次に置き換える。

```bat
@echo off
chcp 65001
setlocal

rem 引数: %1 = com3d2/com3d25/all (既定 com3d25)
set TARGET=%~1
if "%TARGET%"=="" set TARGET=com3d25

call .\source\COM3D25.MToonMaid.Plugin\build.bat debug %TARGET%
if %ERRORLEVEL% neq 0 (
    echo ビルドに失敗しました
    exit /b 1
)

echo ビルドに成功しました
exit /b 0
```

- [ ] **Step 3: release.bat で 2.0 版も zip に入れる**

`release.bat` の `call .\source\%PLUGIN_NAME%\build.bat release` を `call .\source\%PLUGIN_NAME%\build.bat release all` にし、2.5 版の dll をコピーする段（`copy /y "BepInEx\plugins\%ASSEMBLY_NAME%.dll" ...` の `if` ブロック）の後に足す。

```bat
md "output\%PLUGIN_NAME%\BepInEx (COM3D2)\plugins"
copy /y "BepInEx (COM3D2)\plugins\%ASSEMBLY_NAME%.dll" "output\%PLUGIN_NAME%\BepInEx (COM3D2)\plugins\" >nul
if %ERRORLEVEL% neq 0 (
    echo COM3D2 版の dll のコピーに失敗しました
    exit /b 1
)
```

- [ ] **Step 4: .env.sample と .gitignore を更新する**

`.env.sample` を Read で開き、`COM3D25_DIR=` の行の次に足す（Read が権限で拒否されたら、ユーザーに次の 2 行の追記を頼む）。

```
# COM3D2 (2.0) のインストール先。2.0 版をビルドしないなら空のままでよい
COM3D2_DIR=
```

`.gitignore` の `BepInEx/plugins/*.dll` の次の行に足す。

```gitignore
BepInEx (COM3D2)/plugins/*.dll
```

ユーザーに、手元の `.env` へ `COM3D2_DIR=W:\COM3D2` を足してもらう（`.env` は読めない）。

- [ ] **Step 5: 2.5 版だけのビルドが今までどおり通ることを確かめる**

ゲームへのコピーを避けるため、ゲームが起動中か、実機に入れてよいかをユーザーに確かめてから実行する。

Run: `cmd //c debug.bat`
Expected: 「COM3D2.5 版」のビルドだけが走り、「ビルドに成功しました」。`COM3D2_DIR` の検証は出ない

- [ ] **Step 6: 配布 zip を作って中身を確かめる**

Run: `cmd //c release.bat`
Expected: 「ビルドに成功しました」

Run: `powershell -NoProfile -c "Add-Type -A System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::OpenRead((Resolve-Path 'output\*.zip')).Entries.FullName"`
Expected: `COM3D25.MToonMaid.Plugin/BepInEx/plugins/COM3D25.MToonMaid.dll` と `COM3D25.MToonMaid.Plugin/BepInEx (COM3D2)/plugins/COM3D25.MToonMaid.dll` の両方がある

- [ ] **Step 7: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/build.bat debug.bat release.bat .env.sample .gitignore
git commit -m "build: COM3D2 (2.0) 版もビルドして配布 zip に入れる"
```

---

### Task 4: 実機で確かめ、README を更新する

**Files:**
- Modify: `README.md`（概要・インストール方法・各ファイルの説明・動作環境・制限・プラグイン開発者向け）

**Interfaces:**
- Consumes: Task 3 の `BepInEx (COM3D2)\plugins\COM3D25.MToonMaid.dll` と `BepInEx\plugins\COM3D25.MToonMaid.dll`

2.0 には devbridge が無いので、2.0 の確認はユーザーがゲームを操作し、Claude は `W:\COM3D2\BepInEx\LogOutput.log` を読む。2.5 は devbridge と `com3d25-devbridge:restart-verify` スキルで確かめる。

- [ ] **Step 1: 2.5 で見た目が変わっていないことを確かめる**

`restart-verify` スキルで新しい 2.5 版の DLL を入れて起動し、Task 1 Step 0 と同じメイド・カメラで撮って見比べる。`tail_log` で `MToonMaid` のエラーが出ていないことも見る。
Expected: 陰影・リム・線・前髪の影・瞳の光沢が Task 1 Step 0 と同じ

- [ ] **Step 2: 2.0 に入れて起動してもらう**

前提: Task 3 Step 4 でユーザーが `.env` に `COM3D2_DIR` を足していること（`debug.bat com3d2` に要る）。

ユーザーに、`BepInEx (COM3D2)\plugins\COM3D25.MToonMaid.dll` を `W:\COM3D2\BepInEx\plugins\` へ入れて（`debug.bat com3d2` をゲーム停止中に実行してもよい）、2.0 を起動し、エディット画面でメイドを MToon にしてもらう。

- [ ] **Step 3: 2.0 のログと見た目の崩れを確かめる**

Run: `rg -n "MToonMaid|keyword|Shader" W:/COM3D2/BepInEx/LogOutput.log`
Expected: `MToonMaid` の読み込みのログがあり、エラー（「埋め込みリソース…」「…で使えません」「Maximum number … of shader keywords」など）が無い

キーワード数の上限を超えるとログが出ずに一部のキーワードが効かなくなることがあるため、ログに何も無くても、MToon にしたメイドがピンク・真っ黒・陰影なし・線なしになっていないことをユーザーに見てもらう。

エラーや崩れがあれば原因を調べて直し、Task 1〜2 の該当箇所に戻る。

- [ ] **Step 4: 間違ったバージョンの DLL を入れたときを両方向で確かめる**

1. ユーザーに、2.5 版の `BepInEx\plugins\COM3D25.MToonMaid.dll` を一度だけ 2.0 に入れて起動してもらう。

   Run: `rg -n "MToonMaid" W:/COM3D2/BepInEx/LogOutput.log`
   Expected: ゲームは落ちない。BepInEx の読み込み失敗か、`この MToonMaid は COM3D2.5 用です` のどちらかのログが出る（どちらだったかを Step 6 の README に使う）

2. 2.0 版の DLL を一度だけ 2.5 に入れて起動する（`restart-verify` スキル）。

   Run: devbridge の `tail_log` で `MToonMaid` を探す
   Expected: ゲームは落ちず、`この MToonMaid は COM3D2 (2.0) 用です` のログが 1 行だけ出る（毎フレームの例外が出ない）

確かめたら、それぞれ正しい版の DLL に戻す。

- [ ] **Step 5: 2.0 の見た目を確かめてもらう**

ユーザーに、2.0 で次を MToon にする前後で見比べてもらう（スクリーンショットを貰えれば Claude も見る）。
- 肌・顔・髪の陰影の境界の位置
- リムの明るさ
- 線の太さ（近くと遠く）
- 影を受けるか（自分の腕の影・足元の影）
- 前髪の影・天使の輪・瞳の光沢

ずれがあれば、内容を README の「制限」に書き、式の合わせ込みは別の計画にする（この計画では直さない）。

- [ ] **Step 6: README を更新する**

`README.md` を次のように直す。
- 概要: 「COM3D2.5 用プラグイン」→「COM3D2.5 / COM3D2 (2.0) 用プラグイン」
- インストール方法: 2.5 は今までどおり `BepInEx` フォルダを、2.0 は `BepInEx (COM3D2)` フォルダの中身をゲームの `BepInEx` フォルダへ上書きコピーする、と書く。違うバージョンのものを入れたときに起きること（Step 4 で確かめた内容）を書く
- 各ファイルの説明: `BepInEx (COM3D2)/plugins/COM3D25.MToonMaid.dll`（COM3D2 (2.0) 用の本体）を足す
- 動作環境: 「COM3D2 (2.0) では動作しません」を「COM3D2 (2.0) + BepInEx 5」にする
- 制限: Step 5 で見つかったずれがあれば書く
- プラグイン開発者向け（3〜4 の手順）: `.env` の `COM3D2_DIR`（2.0 版をビルドするときだけ要る。`release.bat` は両方を作るので必須）、`debug.bat com3d2|com3d25|all`、バンドルの作り直しに Unity 2022.3.62f2 と 5.6.4f1 が要ること（`build-bundle.bat com3d2|com3d25|all`）を書く

- [ ] **Step 7: Commit**

```bash
git add README.md
git commit -m "docs: COM3D2 (2.0) への対応を README に書く"
```

---

## レビュー却下メモ

- Task 2 Step 1 の基準の警告数は、既存の `UnityProject2022/Logs/build-bundle.log` から取れば往復が要らない — 今あるログが今のシェーダーのソースで作られたものか保証できないため、実行し直して取る手順のままにする
- Task 3 Step 5 は `debug.bat` ではなく MSBuild を直接叩くほうが安全 — この Step は書き換えた `debug.bat` 自体の確認なので直接は叩けない。実機へのコピーはユーザーに確かめてから実行する手順で防ぐ
- `robocopy /MIR /XF *.meta` が、5.6 が作った `.meta` を毎回消してフルインポートになる — `/XF` で除いたファイルは `/MIR` の削除の対象にもならないため起きない（robocopy の仕様。実行して確かめてはいない）。`Library` も `Assets` の外なので残る
- 2.0 の `_RimColor` などの値の範囲・色空間（Gamma / Linear）の差 — Task 4 Step 5 の見た目の見比べで分かり、ずれは別の計画にする方針なので、この計画では個別に調べない
