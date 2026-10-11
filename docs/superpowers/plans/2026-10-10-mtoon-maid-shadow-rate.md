# COM3D25.MToonMaid.Plugin ゲームの影の境界（_ShadowRateToon）を使う Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MToon の主光源の陰影の境界を、ゲームの Toony と同じくマテリアルごとの `_ShadowRateToon` で決める。グループ設定に「ゲームの影の境界」（既定 ON）と「境界のずらし」を足す。

**Architecture:** MToon10 フォークに `_ShadowRateToon`（ゲームと同名）と `_UseShadowRateToon` / `_ShadowRateToonV` / `_ShadowRateShift` を足し、ForwardBase の陰影を `_ShadowRateToon((N·L + 凹凸のマスクのずれ + ずらし) × 0.5 + 0.5, v).r` にする。C# 側はシェーダー名から読む行（Muscle は v=0.75、他は対角線）を `ToonMaterialClassifier.ShadowRateToonV` で決めて `MaterialBackup` に控え、`MaterialConverter.ApplyGroup` で設定とテクスチャの有無からフラグを書く。シェーダーは `build-bundle.bat` で埋め込みバンドルを作り直す。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（バンドル）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md` の「改訂 2026-10-10 (4): ゲームの影の境界（`_ShadowRateToon`）を使う」節

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / バンドル: `build-bundle.bat`

## Global Constraints

- main で作業する。コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- 設定ファイルの版（`MaidSettings.CurrentVersion` = 3）は上げない。新しい項目は要素が無ければフィールド初期値（ON・0）で読む
- `_ShadowRateToon` はプラグインが書かない（ゲームの値をそのまま使う）。戻す処理にも足さない
- Toony の式（実測）: `u = N·L × 0.5 + 0.5`。通常は `(u, u)`、Muscle（シェーダー名に `Muscle`）は `(u, 0.75)` で読み、R を割合にする。追加ライトには使わない
- ForwardBase の陰影（ON かつテクスチャあり）: `_ShadowRateToon(clamp(uv, 半テクセル, 1 - 半テクセル)).r × 影マップの減衰 × (1 - 前髪の影)`。`uv.x = saturate((clamp(N·L) + _ShadingShiftTex.r × _ShadingShiftTexScale + _ShadowRateShift) × 0.5 + 0.5)`。`_ShadingShiftFactor` / `_ShadingToonyFactor` は使わない
- ForwardAdd の陰影は変えない（「境界の位置」「境界の硬さ」のまま）
- 継承の単位キー: `gameShadowRate`（`useGameShadowRate`）、`shadowRateShift`（`shadowRateShift`）。値は設定ファイルに書くため変えない

## Review Focus

- **`_ShadowRateToon` が null のマテリアル（MOD）**: ON でも `_UseShadowRateToon` 0 で従来の式 → Task 2 の `ShadowRateParams` テスト、Task 3 Step 2 で列挙して確認（プロパティの無い Toony から MToon へ差し替えた直後の `GetTexture` は null。計画レビュー時に実機で確認済み）
- **Muscle の body**: `_ShadowRateToonV` 0.75 → Task 2 の分類テスト、Task 3 Step 2（muscle の body が無ければ分類テストだけで済ませ、その旨を記録）
- **MToon 中に `_ShadowRateToon` の有無が変わる**: null ⇔ 非 null のどちらでも次の走査で `ApplyGroup` し直す → Task 2 Step 3 の `IsShadowRatePresenceChanged`
- **Repeat のランプ（端の画素の混ざり）**: N·L = 1 付近が暗くならない → Task 1 Step 3 のクランプ、Task 3 Step 3 の球の実測で N·L 0.9 が 1 に近いこと
- **顔の凹凸のマスクと前髪の影**: ON でも効く → Task 3 Step 4 で「凹凸の影を消す」「前髪の影」を上げて見る
- **ゲームが Toony へ戻して再変換**: `Reconvert` で `shadowRateToonV` を判定し直す → Task 2 Step 3。MToon 中に menu が `_ShadowRateToon` を別のテクスチャへ差し替えても、シェーダーが直接読むため追従する
- **OFF にしたとき**: 「境界の位置」「境界の硬さ」で従来と同じ見た目 → Task 3 Step 4

---

### Task 1: シェーダーで `_ShadowRateToon` を読んで陰影にする

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`（先頭の改変履歴コメント、Properties の Lighting 節）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl`（`GetMToonLighting_Reflectance_ShadingShift` / `GetMToonLighting_Shade`）
- Generated: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

- [ ] **Step 1: Properties** — 改変履歴の最後に `// - _UseShadowRateToon が 1 のとき、ForwardBase の陰影をゲームの _ShadowRateToon (Toony と同じ N·L の座標) で決める` を足す。`_ShadingToonyFactor` の行の後に次を足す:

```
        // COM3D25: ゲームの Toony の影の割合 (陰影の境界)。ゲームと同名で、シェーダーを差し替えても値 (無限色の RenderTexture を含む) が引き継がれる
        _ShadowRateToon ("Shadow Rate Toon", 2D) = "white" {}
        // COM3D25: 1 なら ForwardBase の陰影を _ShadowRateToon で決める。V が負なら u と同じ値で読む。ずらしは N·L へ足す (プラグインが設定する)
        _UseShadowRateToon ("Use Shadow Rate Toon", Float) = 0
        _ShadowRateToonV ("Shadow Rate Toon V", Float) = -1
        _ShadowRateShift ("Shadow Rate Shift", Range(-1, 1)) = 0
```

- [ ] **Step 2: 宣言** — `input.hlsl` の `MTOON_DECLARE_TEX2D(_ShadingShiftTex);` の後に `MTOON_DECLARE_TEX2D(_ShadowRateToon);`、CBUFFER の `float4 _HairShadowTex_TexelSize;` の後に `float4 _ShadowRateToon_TexelSize;`、`half _ShadingToonyFactor;` の後に次を足す:

```hlsl
half _UseShadowRateToon;
half _ShadowRateToonV;
half _ShadowRateShift;
```

- [ ] **Step 3: 陰影** — `lighting_mtoon.hlsl` の `GetMToonLighting_Reflectance_ShadingShift` を次の 2 関数へ置き換え、その後ろ（`GetMToonLighting_DotNL` の前）に `GetMToonLighting_ShadowRate` を足す:

```hlsl
// COM3D25: 凹凸のマスクによる境界のずれ。ゲームの影の境界 (_ShadowRateToon) にも同じずれを足すため分ける
inline half GetMToonLighting_ShadingShiftTex(const MToonInput input)
{
    if (MToon_IsParameterMapOn())
    {
        return MTOON_SAMPLE_TEXTURE2D(_ShadingShiftTex, input.uv).r * _ShadingShiftTexScale;
    }
    return 0;
}

inline half GetMToonLighting_Reflectance_ShadingShift(const MToonInput input)
{
    return GetMToonLighting_ShadingShiftTex(input) + _ShadingShiftFactor;
}

// COM3D25: 実測した Toony の陰影。u = N·L * 0.5 + 0.5 で、通常は (u, u)、Muscle は (u, 0.75) の R が _ShadowTex → _MainTex の割合
// 座標が N·L で飛ぶため、ミップの選択を避けて SampleLevel で読む
inline half GetMToonLighting_ShadowRate(const MToonInput input, const half dotNL)
{
    const float u = saturate((dotNL + GetMToonLighting_ShadingShiftTex(input) + _ShadowRateShift) * 0.5 + 0.5);
    const float v = _ShadowRateToonV < 0 ? u : _ShadowRateToonV;
    // 半テクセルより外を読むと、Repeat のランプは反対の端の画素と混ざる
    const float2 halfTexel = _ShadowRateToon_TexelSize.xy * 0.5;
    const float2 uv = clamp(float2(u, v), halfTexel, 1 - halfTexel);
    return _ShadowRateToon.SampleLevel(sampler_ShadowRateToon, uv, 0).r;
}
```

`GetMToonLighting_Shade` の `MToon_IsForwardBasePass()` の分岐を次にする（PbrCorrect と ForwardAdd の分岐は変えない）:

```hlsl
    if (MToon_IsForwardBasePass())
    {
        const half shadeInput = lerp(-1, 1, mtoon_linearstep(-1, 1, dotNL));
        const half hairShadow = GetMToonLighting_HairShadow(input);
        const half toonShade = _UseShadowRateToon > 0.5
            ? GetMToonLighting_ShadowRate(input, shadeInput)
            : mtoon_linearstep(-1.0 + shadeToony, +1.0 - shadeToony, shadeInput + shadeShift);
        return toonShade * lighting.directLightAttenuation.x * (1 - hairShadow);
    }
```

- [ ] **Step 4: バンドルを作る** — Run: `build-bundle.bat` / Expected: `コピーしました: ...mtoon_bundle`（Unity エディタでプロジェクトを開いていないこと）。シェーダーのコンパイルエラーがあればログに出るので直す

- [ ] **Step 5: コミット** — シェーダー 3 ファイルと、作り直した `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`（埋め込みリソース。入れないと Task 2 のビルドが古いシェーダーのまま通る）をコミットする: `feat: MToon の主光源の陰影をゲームの _ShadowRateToon で決められるようにする`

### Task 2: 設定・変換・UI でゲームの影の境界を切り替える

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/ToonMaterialClassifier.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialBackup.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs`（`ScanMaterial` の既存エントリの分岐）
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（`DrawSettings` の陰影の節）
- Modify: `README.md`
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/ShadowRateParams.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs`, `CommonSettingsTests.cs`, `MaidSettingsStoreTests.cs`
- Create (Test): `source/COM3D25.MToonMaid.Plugin.Tests/ShadowRateParamsTests.cs`

**Interfaces:**
- Consumes: Task 1 のシェーダープロパティ `_ShadowRateToon` / `_UseShadowRateToon` / `_ShadowRateToonV` / `_ShadowRateShift`
- Produces: `GroupSettingKeys.GameShadowRate`（`"gameShadowRate"`）/ `GroupSettingKeys.ShadowRateShift`（`"shadowRateShift"`）、`GroupSettings.useGameShadowRate`（bool、既定 true）/ `GroupSettings.shadowRateShift`（float、既定 0）、`float ToonMaterialClassifier.ShadowRateToonV(string shaderName)`、`ToonMaterialClassifier.DiagonalShadowRateV`（-1）、`bool ShadowRateParams.Use(bool gameShadowRate, bool hasTexture)`、`MaterialBackup.shadowRateToonV` / `MaterialBackup.hasShadowRateToon`、`bool MaterialConverter.IsShadowRatePresenceChanged(Material m, MaterialBackup backup)`、`MToonProps.ShadowRateToon` / `UseShadowRateToon` / `ShadowRateToonV` / `ShadowRateShift`

- [ ] **Step 1: 失敗するテストを書く** — `ToonMaterialClassifierTests` に:

```csharp
        [Theory]
        [InlineData("CM3D2/Toony_Lighted_Muscle_Outline", 0.75f)]
        [InlineData("CM3D2/Toony_Lighted_Muscle_Outline_Tex", 0.75f)]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex", ToonMaterialClassifier.DiagonalShadowRateV)]
        [InlineData("CM3D2/Toony_Lighted", ToonMaterialClassifier.DiagonalShadowRateV)]
        [InlineData(null, ToonMaterialClassifier.DiagonalShadowRateV)]
        public void 影の割合を読む行はMuscleだけ固定(string shaderName, float expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.ShadowRateToonV(shaderName));
        }
```

`ShadowRateParamsTests`:

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class ShadowRateParamsTests
    {
        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        public void ゲームの影の境界はONでテクスチャがあるときだけ使う(bool gameShadowRate, bool hasTexture, bool expected)
        {
            Assert.Equal(expected, ShadowRateParams.Use(gameShadowRate, hasTexture));
        }
    }
}
```

`CommonSettingsTests` の `ChangeAll` の `g.shadeColor.r = 0.5f;` の後に `g.useGameShadowRate = false;` と `g.shadowRateShift = 0.2f;` を足し（全項目のテストが新しいキーも確かめる）、`ライト色の影響の既定は半分` の後に:

```csharp
        [Fact]
        public void ゲームの影の境界は既定でONでずらしは0()
        {
            var d = GroupSettings.CreateDefault();
            Assert.True(d.useGameShadowRate);
            Assert.Equal(0f, d.shadowRateShift);
        }
```

`MaidSettingsStoreTests.保存して読み直すと同じ値になる` に `s.hair.useGameShadowRate = false;` `s.hair.shadowRateShift = -0.2f;` と `Assert.False(loaded.hair.useGameShadowRate);` `Assert.Equal(-0.2f, loaded.hair.shadowRateShift);` を足す。`欠けた項目は既定値で補う` に `Assert.True(s.hair.useGameShadowRate);`（版 1 のファイルに要素が無くても ON）を足す

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: `useGameShadowRate` / `ShadowRateToonV` が無くビルドエラー

- [ ] **Step 3: 実装**
  - `GroupSettingKeys`: `public const string GameShadowRate = "gameShadowRate";` と `public const string ShadowRateShift = "shadowRateShift";` を `ShadeColor` の後に足し、`All` を `ShadeColor, GameShadowRate, ShadowRateShift, ShadingShift, ...` の順にする
  - `GroupSettings`: `shadeColor` の後に次を足し、`CopyField` / `FieldEquals` に 2 キーの case を足す（`FieldEquals` の bool は `==`、float は `FloatEquals`）

```csharp
        // 主光源の陰影の境界をゲームの _ShadowRateToon で決める。ON の間、shadingShift / shadingToony は追加ライトにだけ効く
        public bool useGameShadowRate = true;
        // ゲームの境界をずらす量 (N·L に足す、-1〜1)。プラスで光側が広がる
        public float shadowRateShift;
```

  - `ToonMaterialClassifier`: 次を足す

```csharp
        public const float DiagonalShadowRateV = -1f;
        // 実測で Muscle は v=0.75 の行を読む。肌の種類の toonSkin_shadow_* は下半分だけが違うため、どれも同じ形になる
        private const float MuscleShadowRateV = 0.75f;

        /// <summary>_ShadowRateToon を読む v。DiagonalShadowRateV (負) なら u と同じ値で読む</summary>
        public static float ShadowRateToonV(string shaderName)
        {
            return shaderName != null && shaderName.IndexOf("Muscle", StringComparison.Ordinal) >= 0
                ? MuscleShadowRateV
                : DiagonalShadowRateV;
        }
```

  - `MToonProps`: ゲームと同名の節の `ToonRamp` の後に `// ゲームの Toony の影の割合。プラグインは書かず、MToon がそのまま読む` と `public const string ShadowRateToon = "_ShadowRateToon";`。フォークの節（`AdditionalLightShade` の後）に `UseShadowRateToon = "_UseShadowRateToon"` / `ShadowRateToonV = "_ShadowRateToonV"` / `ShadowRateShift = "_ShadowRateShift"`
  - `ShadowRateParams.cs`（`OutlineParams.cs` と同じ置き場所・作法）:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    public static class ShadowRateParams
    {
        /// <summary>主光源の陰影を _ShadowRateToon で決めるか。hasTexture は MToon にした後のマテリアルの _ShadowRateToon が null でないか</summary>
        public static bool Use(bool gameShadowRate, bool hasTexture)
        {
            // テクスチャの無い MOD などで使うと、シェーダーの既定 (white) で影が消える
            return gameShadowRate && hasTexture;
        }
    }
}
```

  - `MaterialBackup`: `isCutout` の後に次を足す

```csharp
        // _ShadowRateToon を読む v (ToonMaterialClassifier.ShadowRateToonV)
        public float shadowRateToonV;
        // 最後に ApplyGroup したときの _ShadowRateToon の有無。menu の差し替えで変わったら ApplyGroup し直す
        public bool hasShadowRateToon;
```

  - `MaterialConverter.Convert` の初期化子に `shadowRateToonV = ToonMaterialClassifier.ShadowRateToonV(shaderName),`、`Reconvert` の `backup.isCutout = ...` の後に `backup.shadowRateToonV = ToonMaterialClassifier.ShadowRateToonV(m.shader.name);`
  - `MaterialConverter.ApplyGroup` の `ShadingToonyFactor` の行の後に:

```csharp
            backup.hasShadowRateToon = HasShadowRateToon(m);
            m.SetFloat(MToonProps.UseShadowRateToon, ShadowRateParams.Use(settings.useGameShadowRate, backup.hasShadowRateToon) ? 1f : 0f);
            m.SetFloat(MToonProps.ShadowRateToonV, backup.shadowRateToonV);
            m.SetFloat(MToonProps.ShadowRateShift, settings.shadowRateShift);
```

  - `MaterialConverter` に次を足す（`IsRampReplaced` の後と、`GetRamp` の後）:

```csharp
        /// <summary>menu のテクスチャ変更などで、最後に ApplyGroup したときと _ShadowRateToon の有無が変わったか</summary>
        public bool IsShadowRatePresenceChanged(Material m, MaterialBackup backup)
        {
            return HasShadowRateToon(m) != backup.hasShadowRateToon;
        }
```

```csharp
        private static bool HasShadowRateToon(Material m)
        {
            return m.HasProperty(MToonProps.ShadowRateToon) && m.GetTexture(MToonProps.ShadowRateToon) != null;
        }
```

  - `MaidMToonController.ScanMaterial` の `IsRampReplaced` の `else if` の後に次を足す（ランプと同時に変わった場合は次の走査で拾う）:

```csharp
                    else if (m.shader == _converter.MToonShader && _converter.IsShadowRatePresenceChanged(m, entry.backup))
                    {
                        _converter.ApplyGroup(m, entry.backup, groupSettings);
                    }
```

  - `MToonMaidWindow.DrawSettings` の影色の `ColorRow` の後に（「境界の位置」の前）:

```csharp
            Toggle(view, GroupSettingKeys.GameShadowRate, "ゲームの影の境界", shown.useGameShadowRate, (s, v) => s.useGameShadowRate = v);
            Slider(view, GroupSettingKeys.ShadowRateShift, "境界のずらし", shown.shadowRateShift, -1f, 1f, def.shadowRateShift, (s, v) => s.shadowRateShift = v, editable: shown.useGameShadowRate);
            if (shown.useGameShadowRate)
            {
                // 表示名に足すとラベル幅 (110) に収まらない。DrawLabel は入力欄キャッシュを使わないため、条件つきで描いても番号はずれない
                view.DrawLabel("境界の位置・硬さは、ゲームの影の境界が ON の間は追加ライトにだけ効きます", -1, RowHeight);
            }
```

  - README「使い方」3 の最後に「主光源の影の境界は、部位ごとにゲームと同じ位置（`_ShadowRateToon`）になります（「ゲームの影の境界」。既定 ON）。「境界のずらし」でゲームの境界からずらせます。OFF にすると以前の版と同じく「境界の位置」「境界の硬さ」で決まります（ON の間、この 2 つは追加ライトにだけ効きます）」を足す。「制限」の `トゥーンランプ` の行を「トゥーンランプ (`_ToonRamp`) は光側と影側の色だけを使います（境界の帯の色までは再現しません）」にする

- [ ] **Step 4: テストとビルド** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: 全件 PASS。Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / Expected: 成功

- [ ] **Step 5: コミット** — `feat: 主光源の影の境界をゲームの _ShadowRateToon に合わせる`

### Task 3: 実機確認

- [ ] **Step 1: DLL を反映** — `com3d25-devbridge:restart-verify` スキルに従い、ゲームを終了 → `debug.bat com3d25` → 起動・セーブをロード
- [ ] **Step 2: フラグの確認** — `eval_csharp` で MToon 化したマテリアルごとに、変換前のシェーダー名（`MaterialBackup.shader.name`。控えに届かなければ同名の Toony を `Shader.Find` で付けた複製から読む）・`_UseShadowRateToon`・`_ShadowRateToonV`・`_ShadowRateToon`（名前・RenderTexture か）を列挙する。肌・顔・髪・衣装は 1・-1・テクスチャあり。シェーダー名に `Muscle` を含むものだけ 0.75（無ければ記録だけ）。`_ShadowRateToon` が null のものは 0。対角線とした Cutout・Hair・Outline_Tex 系の名前が出ていれば、そのうち 1 つを Step 3 の球で Toony と比べて対角線で読むことを確かめる
- [ ] **Step 3: 式の確認** — 調査時と同じ試験用の球（主光源のみ、y=500、Charactor レイヤー、受影なし）に `COM3D25/MToon10`（既存の MToon マテリアルの `shader` から取る。`Shader.Find` では見つからない）を付け、`_MainTex` 白・`_ShadowTex` 黒・`_ShadeColor` 白・`_ToonRampLitColor` 白・`_RimColor` 黒・`_UseShadowRateToon` 1・`_ShadowRateToonV` -1 にする。GI と色の置き換えの影響を除くため、明るさではなく補間率で比べる: `_ShadowRateToon` を白（全面光側）・黒（全面影側）にした 2 回で N·L ごとの基準 `lit` / `dark` を取り、`ToonSkin_Shadow.tex` を入れたときの値 `x` から `(x - dark) / (lit - dark)` を出す。これが同じテクスチャの対角線の値（調査時: N·L -0.1 で約 0.73、0.0 以上で 1、-0.4 以下で約 0.02）と 0.03 以内で一致すれば合格。N·L 0.9 の補間率が 1（端の混ざりが無い）こと。Step 2 で選んだ対角線以外の系統があれば、同じ手順で Toony と比べる。終わったら試験用の物体を消す
- [ ] **Step 4: 見た目の確認** — MToon ON/OFF でスクリーンショットを撮り、顔・肌・髪の影の範囲がゲームに近いことを見る。「ゲームの影の境界」OFF で以前と同じ見た目に戻ること、「境界のずらし」で境界が動くこと、ON のまま「凹凸の影を消す」「前髪の影」が効くことを見る。ON の間の注意書きの行がウィンドウ幅で切れずに読めることも見る

## レビュー却下メモ

- `_ShadowRateToon` の無い Toony から差し替えると `GetTexture` が既定の white 実体を返し、MOD で影が消える — 誤検知。計画レビュー時に実機で、Toony に無い `_HairShadowTex`（既定 white）が差し替え直後に null を返すことを確認した。Task 3 Step 2 の列挙は残す
- 既定 ON で保存済みのメイドの見た目が変わることをリリースノートに書く — 計画の範囲外。README と注意書きで触れており、リリースノートは release-prep で書く
