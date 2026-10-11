# COM3D25.MToonMaid.Plugin ゲームの線の色を引き継ぐ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MToon にしたとき、`*_Outline_Tex` 系のマテリアルではゲームの `_OutlineTex`（線用テクスチャ・無限色）を線の色に使い、ゲームと同じ色の輪郭線を描く。「ライト色の影響」の既定を 0.5 にする。

**Architecture:** MToon10 フォークの輪郭パスに `_OutlineTex` と `_UseOutlineTex` を足す。C# 側は `ToonMaterialClassifier.UsesOutlineTex` でシェーダー名から判定して `MaterialBackup` に控える。線の色の出どころ（上書き色・テクスチャ・ゲームの `_OutlineColor`）は Unity に依存しない `OutlineParams.Source` で決めて単体テストし、`MaterialConverter.ApplyGroup` で `_OutlineColor` / `_UseOutlineTex` を書く。シェーダーは `build-bundle.bat` で埋め込みバンドルを作り直す。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（バンドル）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md` の「改訂 2026-10-10 (3): ゲームの線の色を引き継ぐ」節

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / バンドル: `build-bundle.bat`

## Global Constraints

- main で作業する。コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- 設定ファイルの版（`MaidSettings.CurrentVersion` = 3）は上げない。保存済みの `outlineLightingMix` は書き換えない
- 判定: シェーダー名に `Outline_Tex` を含めば線の色をテクスチャから取る（`Toony_Lighted_Outline_Tex` / `Toony_Lighted_Hair_Outline_Tex` / `Toony_Lighted_Outline_Tex_Texwidth` 等）
- `_OutlineTex` はプラグインが書かない（ゲームの値をそのまま使う）。戻す処理にも足さない
- 輪郭の色: `_OutlineColor.rgb × lerp(1, _OutlineTex.rgb(uv), _UseOutlineTex) × lerp(1, baseCol, _OutlineLightingMix)`

## Review Focus

- **Tex 系で上書き OFF**: `_OutlineColor` 白・`_UseOutlineTex` 1 → Task 3 実機で線がゲームの色になる
- **上書き ON**: Tex 系でも `_UseOutlineTex` 0 で指定色だけ → Task 3 実機
- **Tex なしの `*_Outline`**: 従来どおり控えの `_OutlineColor`・`_UseOutlineTex` 0
- **無限色で輪郭色を変える**: RenderTexture の中身が変わるだけなので、MToon のまま追従する → Task 3 実機
- **ゲームが Toony へ戻して再変換**: `Reconvert` で `outlineFromTex` を判定し直す → Task 2 Step 3。`outlineColor` は読み直さない（MToon が書いた白や上書き色を控えてしまうため。Tex 系 ⇔ Tex なしの切り替えで控えが古い値になるのは許容）
- **上書き × Tex 系の 4 通り**: `OutlineParams.Source` のテスト → Task 2 Step 1
- **OFF で戻す**: `_OutlineColor` は控えへ戻る。`_UseOutlineTex` は Toony に無く描画に影響しない

---

### Task 1: シェーダーに `_OutlineTex` / `_UseOutlineTex` を足す

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`（先頭の改変履歴コメント、Properties の Outline 節）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl`（`GetMToonLighting` の輪郭の分岐）
- Generated: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

- [ ] **Step 1: Properties** — 改変履歴に `// - ゲームの線用テクスチャ (_OutlineTex) を、_UseOutlineTex が 1 のとき輪郭の色に掛ける` を足す。Outline 節の `_OutlineColor` の後に次を足す:

```
        // COM3D25: ゲームの *_Outline_Tex 系の線の色。ゲームと同名で、シェーダーを差し替えても値 (無限色の RenderTexture を含む) が引き継がれる
        _OutlineTex ("Outline Texture", 2D) = "white" {}
        _UseOutlineTex ("Use Outline Texture", Float) = 0
```

`_OutlineLightingMix` の既定（Properties の `= 1`）は変えない（プラグインが常に書く）。

- [ ] **Step 2: 宣言** — `input.hlsl` の `MTOON_DECLARE_TEX2D(_OutlineWidthTex);` の後に `MTOON_DECLARE_TEX2D(_OutlineTex);`、`half _OutlineLightingMix;` の後に `half _UseOutlineTex;` を足す

- [ ] **Step 3: 輪郭の色** — `lighting_mtoon.hlsl` の輪郭の分岐を次にする:

```hlsl
    if (MToon_IsOutlinePass())
    {
        // COM3D25: ゲームの *_Outline_Tex 系は _OutlineColor ではなく _OutlineTex の色で線を描く
        const half3 outlineTex = lerp(half3(1, 1, 1), MTOON_SAMPLE_TEXTURE2D(_OutlineTex, input.uv).rgb, _UseOutlineTex);
        const half3 outlineCol = _OutlineColor.rgb * outlineTex * lerp(half3(1, 1, 1), baseCol, _OutlineLightingMix);
        return half4(outlineCol, input.alpha);
    }
```

- [ ] **Step 4: バンドルを作る** — Run: `build-bundle.bat` / Expected: `コピーしました: ...mtoon_bundle`（Unity エディタでプロジェクトを開いていないこと）

- [ ] **Step 5: コミット** — `feat: MToon の輪郭パスでゲームの線用テクスチャを使えるようにする`

### Task 2: 変換で線の色の出どころを切り替え、既定を 0.5 にする

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/ToonMaterialClassifier.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialBackup.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`
- Modify: `README.md`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs`, `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`

**Interfaces:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/OutlineParams.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/OutlineParamsTests.cs`
- Produces: `bool ToonMaterialClassifier.UsesOutlineTex(string shaderName)`、`enum OutlineColorSource { Override, Texture, Game }`、`OutlineColorSource OutlineParams.Source(bool fromTex, bool colorOverride)`、`MToonProps.UseOutlineTex`（`"_UseOutlineTex"`）、`MaterialBackup.outlineFromTex`

- [ ] **Step 1: 失敗するテストを書く** — `ToonMaterialClassifierTests` に:

```csharp
        [Theory]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Hair_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex_Texwidth", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline", false)]
        [InlineData("CM3D2/Toony_Lighted_Hair_Outline", false)]
        [InlineData("CM3D2/Toony_Lighted", false)]
        [InlineData(null, false)]
        public void UsesOutlineTex(string shaderName, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.UsesOutlineTex(shaderName));
        }
```

`OutlineParamsTests`:

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class OutlineParamsTests
    {
        [Theory]
        [InlineData(true, false, OutlineColorSource.Texture)]
        [InlineData(false, false, OutlineColorSource.Game)]
        [InlineData(true, true, OutlineColorSource.Override)]
        [InlineData(false, true, OutlineColorSource.Override)]
        public void 線の色の出どころ(bool fromTex, bool colorOverride, OutlineColorSource expected)
        {
            Assert.Equal(expected, OutlineParams.Source(fromTex, colorOverride));
        }
    }
}
```

`CommonSettingsTests` に（既存の既定値テストがあればそこへ足す）:

```csharp
        [Fact]
        public void ライト色の影響の既定は半分()
        {
            Assert.Equal(0.5f, GroupSettings.CreateDefault().outlineLightingMix);
        }
```

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: `UsesOutlineTex` / `OutlineParams` が無くビルドエラー

- [ ] **Step 3: 実装**
  - `ToonMaterialClassifier.UsesOutlineTex`: `shaderName != null && shaderName.IndexOf("Outline_Tex", StringComparison.Ordinal) >= 0`
  - `MToonProps` の MToon 固有に `public const string UseOutlineTex = "_UseOutlineTex";`
  - `MaterialBackup` に `public bool outlineFromTex;`（`hadOutline` の後。コメント: `*_Outline_Tex` 系はゲームが `_OutlineColor` を使わず `_OutlineTex` で線を描く）
  - `MaterialConverter.Convert` の初期化子と `Reconvert` に `outlineFromTex = ToonMaterialClassifier.UsesOutlineTex(shaderName)`（`Reconvert` は `m.shader.name`）
  - `OutlineParams.cs`: `public enum OutlineColorSource { Override, Texture, Game }` と `public static class OutlineParams { public static OutlineColorSource Source(bool fromTex, bool colorOverride) }`。上書きなら `Override`（Tex 系でもテクスチャを掛けずに指定色だけを出す）、でなければ `fromTex` で `Texture` / `Game`
  - `ApplyGroup` の `_OutlineColor` の行を次へ置き換える:

```csharp
            var source = OutlineParams.Source(backup.outlineFromTex, settings.outlineColorOverride);
            m.SetFloat(MToonProps.UseOutlineTex, source == OutlineColorSource.Texture ? 1f : 0f);
            m.SetColor(MToonProps.OutlineColor,
                source == OutlineColorSource.Override ? ToColor(settings.outlineColor)
                : source == OutlineColorSource.Texture ? Color.white
                : backup.outlineColor);
```

  - `GroupSettings.outlineLightingMix` の初期値を `0.5f` にする
  - README「使い方」3 のリムの説明の後に「アウトラインの色はマテリアルごとにゲームの線の色（線用テクスチャ・無限色）を引き継ぎます」を足す。既定の「ライト色の影響」が 0.5 になったことと、保存済みのメイドは 1 のままなので変えるなら行の [R]（既定に戻す）を使うことも同じ箇所に書く

- [ ] **Step 4: テストとビルド** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: 全件 PASS。Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / Expected: 成功

- [ ] **Step 5: コミット** — `fix: MToon にしたときゲームの線の色を引き継ぐ`

### Task 3: 実機確認

- [ ] **Step 1: DLL を反映** — `com3d25-devbridge:restart-verify` スキルに従い、ゲームを終了 → `debug.bat com3d25` → 起動・セーブをロード
- [ ] **Step 2: 色の確認** — `eval_csharp` で MToon 化したマテリアルの `_UseOutlineTex` / `_OutlineColor` / `_OutlineTex` を列挙する。Tex 系（skin / face / hair / onep 等）は 1・白・テクスチャあり、Tex なし（mouth 等）は 0・控えの色。`_OutlineTex` が null の Tex 系が無いかも見る（あれば Properties の既定 white で、ゲームの null 時のほぼ白と合う）
- [ ] **Step 3: 見た目の確認** — MToon ON/OFF でスクリーンショットを撮り、線の色がゲームに近いことを見る（ライト色の影響 0.5 の分だけ暗い）。髪の無限色の輪郭色を変えて、MToon のまま線の色が変わることを見る。「線の色を上書き」ON で指定色になることを見る

## レビュー却下メモ

- `_OutlineTex` を `_MainTex_ST` 変換後の UV で読むとずれる — 実機の全メイドで `_MainTex` / `_OutlineTex` の ST がすべて既定 (1,1,0,0) だったため、自前の ST は持たない
- SceneEditor 向けに README へ「Tex 系の `_OutlineColor` は乗算」と書く — `_OutlineColor` は次の `ApplyGroup` で上書きされる点は従来と同じで、ユーザー向け README には不要。仕様に一行残す
