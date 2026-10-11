# COM3D25.MToonMaid.Plugin ゲームのランプ（_ToonRamp）を読む Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 「ゲームの陰影」（旧「ゲームの影の境界」）が ON のとき、MToon でもゲームの `_ToonRamp` を Toony と同じ座標で読み、主光源の分とリムに掛ける。環境光の分にランプの光側の色を掛けているずれを直す。

**Architecture:** MToon10 フォークで `_ToonRamp` を `_ShadowRateToon` のサンプラーで読み（座標の計算を共通化）、`_UseToonRamp` が 1 なら ForwardBase の直接光とリムに掛ける。前髪の影の分は `_ToonRampDarkColor` へ寄せる。環境光は `MToonInput.baseColor`（ランプを掛ける前の色）で計算する。C# は 2 色の近似を外すかどうかを `ShadowRateParams` の純関数で決め、`MaterialConverter.ApplyGroup` で書く。シェーダーは `build-bundle.bat` で埋め込みバンドルを作り直す。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（バンドル）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md` の「改訂 2026-10-10 (5): ゲームのランプ（`_ToonRamp`）を読む」節（前提: 同 (4) 節）

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / バンドル: `build-bundle.bat`（成否は `UnityProject2022/Logs/build-bundle.log` に `Shader error` が無いことと `Resources/mtoon_bundle` の更新で見る）

## Global Constraints

- main で作業する。コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- 設定ファイルの版は上げない。設定のキー（`gameShadowRate` / `useGameShadowRate`）も変えない。変えるのは UI の表示名だけ（「ゲームの陰影」）
- Toony の式（実測）: 主光源 `lerp(_ShadowTex, _MainTex × _Color, SRT.r) × _ToonRamp × ライト色`、環境光 `_MainTex × 環境光`、追加ライト `_MainTex × ライト色 × 減衰`。ランプの座標は `_ShadowRateToon` と同じ
- ランプは ForwardBase の直接光（ハイライト込みの陰影後の色）とリムの主光源の分にだけ掛ける。環境光・自己発光・追加ライトのパスには掛けない
- 前髪の影の分は `lerp(ランプの色, _ToonRampDarkColor, 前髪の影)`
- ON かつランプと `_ShadowRateToon` の両方あり: `_UseToonRamp` 1・`_ToonRampLitColor` 白・`_ShadeColor` = 設定の影色。それ以外: `_UseToonRamp` 0・従来の 2 色。`_ToonRampDarkColor` は常にランプの暗い色
- 環境光は ON/OFF によらず `_MainTex × _Color × 環境光`（OFF の見た目も髪などで少し明るくなる）
- `[branch]` は使わない（前回、Unity のシェーダーコンパイラが内部エラーで止まった）

## Review Focus

- **髪の無限色のランプ（RenderTexture）**: シェーダーが直接読むため、無限色を変えると MToon のまま追従する。`_ToonRampDarkColor` は既存の読み直し（`RefreshRampsIfPartsColorChanged`）で更新される → Task 3 Step 4
- **ランプか `_ShadowRateToon` の無いマテリアル（MOD）**: ON でも `_UseToonRamp` 0 で従来の 2 色（ランプが無ければ 2 色は白）→ Task 2 の `ShadowRateParams` テスト、Task 3 Step 2。MToon 中に `_ShadowRateToon` の有無が変わったときは、既存の `IsShadowRatePresenceChanged` → `ApplyGroup` で決め直される
- **Repeat のランプ**（`toonskin_SKB.tex` は Repeat）: 端で反対側の画素と混ざらない → Task 3 Step 3 の N·L 0.9 の色
- **前髪の影**: 影の中が影側の色（肌の赤み）になる → Task 3 Step 4
- **サンプラーの数**: `_ToonRamp` に専用のサンプラーを作らない（D3D11 の上限 16 に近いため）→ Task 1 Step 2、バンドルのビルドで確認

---

### Task 1: シェーダーでランプを読み、環境光からランプの光側を外す

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`（改変履歴コメント、Properties の Lighting 節）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_forward_fragment.hlsl`
- Generated: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

- [ ] **Step 1: Properties** — 改変履歴の最後に `// - _UseToonRamp が 1 のとき、ゲームの _ToonRamp を _ShadowRateToon と同じ座標で読み、主光源の分とリムに掛ける。環境光にはランプの光側の色を掛けない` を足す。`_ToonRamp` の 2 行を次に置き換え、`_ToonRampLitColor` の後に `_ToonRampDarkColor` を足す:

```
        // COM3D25: ゲームの Toony のランプ。ゲームと同名で、シェーダーを差し替えても値 (無限色の RenderTexture を含む) が引き継がれる
        _ToonRamp ("Toon Ramp", 2D) = "white" {}
        // COM3D25: 1 なら _ToonRamp を読んで主光源の分に掛ける (プラグインが設定する)
        _UseToonRamp ("Use Toon Ramp", Float) = 0
```

```
        // COM3D25: _ToonRamp の最も暗い画素の色。前髪の影の分をこの色へ寄せる (プラグインが設定する)
        _ToonRampDarkColor ("ToonRamp Dark Color", Color) = (1, 1, 1, 1)
```

- [ ] **Step 2: 宣言** — `input.hlsl` の `MTOON_DECLARE_TEX2D(_ShadowRateToon);` の後に次を足す。CBUFFER の `float4 _ShadowRateToon_TexelSize;` の後に `float4 _ToonRamp_TexelSize;`、`half4 _ToonRampLitColor;` の後に `half4 _ToonRampDarkColor;`、`half _ShadowRateShift;` の後に `half _UseToonRamp;` を足す

```hlsl
// COM3D25: 専用のサンプラーを作ると D3D11 の上限 (16) に近づくため、_ShadowRateToon のサンプラーで読む
UNITY_DECLARE_TEX2D_NOSAMPLER(_ToonRamp);
```

- [ ] **Step 3: 座標の共通化とランプ** — `lighting_mtoon.hlsl` の `GetMToonLighting_ShadowRate`（とその直前の 2 行のコメント）を次へ置き換える:

```hlsl
// COM3D25: 実測した Toony の陰影の座標。u = N·L * 0.5 + 0.5 で、通常は (u, u)、Muscle は (u, 0.75)。_ShadowRateToon とランプが同じ座標を読む
inline float2 GetMToonLighting_ToonUv(const MToonInput input, const half dotNL)
{
    const float u = saturate((dotNL + GetMToonLighting_ShadingShiftTex(input) + _ShadowRateShift) * 0.5 + 0.5);
    const float v = _ShadowRateToonV < 0 ? u : _ShadowRateToonV;
    return float2(u, v);
}

// 半テクセルより外を読むと、Repeat のランプは反対の端の画素と混ざる
inline float2 MToon_ClampToTexels(const float2 uv, const float4 texelSize)
{
    const float2 halfTexel = texelSize.xy * 0.5;
    return clamp(uv, halfTexel, 1 - halfTexel);
}

// R が _ShadowTex → _MainTex の割合。座標が N·L で飛ぶため、ミップの選択を避けて SampleLevel で読む
inline half GetMToonLighting_ShadowRate(const MToonInput input, const half dotNL)
{
    const float2 uv = MToon_ClampToTexels(GetMToonLighting_ToonUv(input, dotNL), _ShadowRateToon_TexelSize);
    return _ShadowRateToon.SampleLevel(sampler_ShadowRateToon, uv, 0).r;
}
```

`GetMToonLighting_HairShadow` の定義の後（`GetMToonLighting_Shade` の前）に足す:

```hlsl
// COM3D25: ゲームの Toony が主光源の分に掛けるランプの色。前髪の影は N·L の座標では光側のままになるため、影の分を影側の色へ寄せる
inline half3 GetMToonLighting_ToonRamp(const MToonInput input, const half dotNL, const half hairShadow)
{
    const float2 uv = MToon_ClampToTexels(GetMToonLighting_ToonUv(input, dotNL), _ToonRamp_TexelSize);
    const half3 ramp = _ToonRamp.SampleLevel(sampler_ShadowRateToon, uv, 0).rgb;
    return lerp(ramp, _ToonRampDarkColor.rgb, hairShadow);
}
```

`GetMToonLighting_Shade` の引数の最後に `const half hairShadow` を足し、ForwardBase の分岐の `const half hairShadow = GetMToonLighting_HairShadow(input);` の行を消す（引数を使う）。前髪の影の PCF（最大 4×4 のループ）を陰影とランプで 2 回回さないため、`GetMToonLighting` で 1 回だけ求めて両方へ渡す

- [ ] **Step 4: 直接光・環境光・リム** — `lighting_mtoon.hlsl`:
  - `MToonInput` に `half3 litColor;` の後へ `half3 baseColor;` を足す（コメント: `// COM3D25: _MainTex × _Color。Toony の環境光はランプを掛けないため、光側の色を掛ける前の色を持つ`）
  - `GetMToonLighting_DirectLighting` の引数の最後に `const half3 rampColor` を足し、戻り値を `albedo * rampColor * unityLight.directLightColor * shadow` にする
  - `GetMToonLighting_GlobalIllumination` の `input.litColor` を `input.baseColor` にする
  - `GetMToonLighting_Rim` の引数の最後に `const half3 rampColor` を足し、`toonyDirect` を `directLightingFactor * lerp(_ShadeColor.rgb, _ToonRampLitColor.rgb, shade) * rampColor * MTOON_TOONY_RIM_DIRECT_SCALE` にする
  - `GetMToonLighting` の `dotNL` の行の後に `const half hairShadow = MToon_IsForwardBasePass() ? GetMToonLighting_HairShadow(input) : 0;` を足し、`shade` の行を `GetMToonLighting_Shade(unityLight, input, dotNL, hairShadow)` にする（`GetMToonLighting_HairShadow` は強さ 0 とアウトラインでは 0 を返すが ForwardAdd は見ていないため、パスの判定で ForwardAdd の PCF を消す。従来も ForwardAdd では呼んでいない）
  - `GetMToonLighting` の `shadow` の行の後に次を足し、`GetMToonLighting_DirectLighting(unityLight, input, shade, shadow, rampColor)` と `GetMToonLighting_Rim(unityLight, input, shade, shadow, rampColor)` に変える:

```hlsl
    // COM3D25: ゲームの Toony は主光源の分にだけランプを掛ける (環境光・追加ライトには掛けない)。アウトラインのパスも ForwardBase なので、線に混ぜる陰影にも掛かる
    const half3 rampColor = MToon_IsForwardBasePass() && _UseToonRamp > 0.5 ? GetMToonLighting_ToonRamp(input, dotNL, hairShadow) : half3(1, 1, 1);
```

  - `#ifdef MTOON_URP` 内の `GetMToonURPAdditionalLighting` の `GetMToonLighting_DirectLighting(unityLight, input, shade, shadow)` を `GetMToonLighting_DirectLighting(unityLight, input, shade, shadow, half3(1, 1, 1))` にする

- [ ] **Step 5: 環境光の色を渡す** — `forward_fragment.hlsl` の `mtoonInput.litColor = ...` の後に `mtoonInput.baseColor = litColor.rgb;` を足す

- [ ] **Step 6: バンドルを作る** — Run: `build-bundle.bat` / Expected: `rg -c "Shader error" UnityProject2022/Logs/build-bundle.log` が何も出さず（exit 1）、`git status` に `Resources/mtoon_bundle` の変更が出る。コンパイルエラーなら `rg -n -A1 "Shader error"` で行番号を見て直す

- [ ] **Step 7: コミット** — シェーダー 4 ファイルと `mtoon_bundle`: `feat: MToon でゲームのランプを読んで主光源の陰影に掛けられるようにする`

### Task 2: ON のときランプを読み、2 色の近似を外す

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/ShadowRateParams.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs`（`ApplyGroup` の先頭 2 行）
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（「ゲームの影の境界」のトグルの表示名）
- Modify: `README.md`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/ShadowRateParamsTests.cs`

**Interfaces:**
- Consumes: Task 1 のプロパティ `_UseToonRamp` / `_ToonRampDarkColor`、既存の `ShadowRateParams.Use(bool, bool)`、`MaterialBackup.ramp`（`ToonRampColors`）/ `MaterialBackup.rampTextureId`（0 はランプなし）
- Produces: `ColorData ShadowRateParams.RampLitColor(bool useRamp, ToonRampColors ramp)`、`ColorData ShadowRateParams.RampShadeMultiplier(bool useRamp, ToonRampColors ramp)`、`MToonProps.UseToonRamp`（`"_UseToonRamp"`）/ `MToonProps.ToonRampDarkColor`（`"_ToonRampDarkColor"`）

- [ ] **Step 1: 失敗するテストを書く** — `ShadowRateParamsTests` に足す（`ToonRampColors.FromRow` は RGBA を並べた 1 行。右端が光側、輝度の最も低い画素が影側）:

```csharp
        private static ToonRampColors Ramp()
        {
            // 影側 (0.4, 0.2, 0.2)、光側 (0.9, 0.8, 0.7)
            return ToonRampColors.FromRow(new[] { 0.4f, 0.2f, 0.2f, 1f, 0.9f, 0.8f, 0.7f, 1f }, 2);
        }

        [Fact]
        public void ランプを読むときは2色の近似を白にする()
        {
            var lit = ShadowRateParams.RampLitColor(true, Ramp());
            var shade = ShadowRateParams.RampShadeMultiplier(true, Ramp());
            Assert.Equal(new[] { 1f, 1f, 1f }, new[] { lit.r, lit.g, lit.b });
            Assert.Equal(new[] { 1f, 1f, 1f }, new[] { shade.r, shade.g, shade.b });
        }

        [Fact]
        public void ランプを読まないときは光側と影側の色を使う()
        {
            var lit = ShadowRateParams.RampLitColor(false, Ramp());
            var shade = ShadowRateParams.RampShadeMultiplier(false, Ramp());
            Assert.Equal(new[] { 0.9f, 0.8f, 0.7f }, new[] { lit.r, lit.g, lit.b });
            Assert.Equal(new[] { 0.4f, 0.2f, 0.2f }, new[] { shade.r, shade.g, shade.b });
        }
```

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: `RampLitColor` が無くビルドエラー

- [ ] **Step 3: 実装**
  - `ShadowRateParams` に足す:

```csharp
        /// <summary>_ToonRampLitColor に入れる色。ランプを読むときは白 (シェーダーがランプの色を掛ける)</summary>
        public static ColorData RampLitColor(bool useRamp, ToonRampColors ramp)
        {
            // ランプを読みながら 2 色も掛けると、ランプの色が二重に掛かる
            return useRamp ? ToonRampColors.White.Lit : ramp.Lit;
        }

        /// <summary>設定の影色に掛けて _ShadeColor にする色。ランプを読むときは白</summary>
        public static ColorData RampShadeMultiplier(bool useRamp, ToonRampColors ramp)
        {
            return useRamp ? ToonRampColors.White.Dark : ramp.Dark;
        }
```

  - `MToonProps` のフォークの節（`ToonRampLitColor` の後）に `public const string ToonRampDarkColor = "_ToonRampDarkColor";`、`ShadowRateShift` の後に `public const string UseToonRamp = "_UseToonRamp";`
  - `MaterialConverter.ApplyGroup` の先頭 2 行（コメント `// Toony はランプの色で…` と `_ToonRampLitColor` / `_ShadeColor` の SetColor）を消し、既存の `_ShadowRateShift` の SetFloat の行（`backup.hasShadowRateToon` を求めた後）の直後に次を置く:

```csharp
            // ランプを読まないときは、Toony がランプの色で陰影を掛ける分を光側・影側の 2 色で近似する。
            // _ShadowRateToon の無いマテリアルでランプだけを読むと、従来の境界の陰影にランプの暗さが重なる
            var useRamp = ShadowRateParams.Use(settings.useGameShadowRate, backup.rampTextureId != 0 && backup.hasShadowRateToon);
            m.SetFloat(MToonProps.UseToonRamp, useRamp ? 1f : 0f);
            m.SetColor(MToonProps.ToonRampLitColor, ToColor(ShadowRateParams.RampLitColor(useRamp, backup.ramp)));
            m.SetColor(MToonProps.ShadeColor, ToColor(settings.shadeColor) * ToColor(ShadowRateParams.RampShadeMultiplier(useRamp, backup.ramp)));
            m.SetColor(MToonProps.ToonRampDarkColor, ToColor(backup.ramp.Dark));
```

  - `MToonMaidWindow.DrawSettings` のトグルの表示名 `"ゲームの影の境界"` を `"ゲームの陰影"` にし、注意書きを `"ON の間、境界の位置・硬さは追加ライトと影の境界を持たない MOD にだけ効きます"` のままにする
  - README「使い方」3 の「主光源の影の境界は、部位ごとにゲームと同じ位置（`_ShadowRateToon`）になります（「ゲームの影の境界」。既定 ON）。」を「主光源の陰影は、部位ごとにゲームと同じ影の境界（`_ShadowRateToon`）とランプの色（`_ToonRamp`）になります（「ゲームの陰影」。既定 ON）。」に、「OFF にすると以前の版と同じく「境界の位置」「境界の硬さ」で決まります」を「OFF にすると以前の版と同じく「境界の位置」「境界の硬さ」と、ランプの光側・影側の 2 色で決まります」にする。「制限」の `トゥーンランプ` の行を「「ゲームの陰影」が OFF のとき、トゥーンランプ (`_ToonRamp`) は光側と影側の色だけを使います（境界の帯の色までは再現しません）」にする。「制限」に「環境光の明るさは、以前の版よりランプの光側の色の分だけ明るくなります（ゲームと同じく環境光にランプを掛けません。黒髪などで目立ちます）」を足す

- [ ] **Step 4: テストとビルド** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: 全件 PASS。Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / Expected: `0 エラー`

- [ ] **Step 5: コミット** — `feat: ゲームの陰影が ON のときランプの色をゲームと同じ座標で掛ける`

### Task 3: 実機確認

- [ ] **Step 1: DLL を反映** — `com3d25-devbridge:restart-verify` スキルに従い、ゲームを終了 → `debug.bat com3d25` → 起動・最新のセーブをロード → `reset_evaluator`
- [ ] **Step 2: フラグの確認** — `eval_csharp` で MToon 化したマテリアルごとに `_UseToonRamp`・`_ToonRampLitColor`・`_ShadeColor`・`_ToonRampDarkColor`・`_ToonRamp`（名前・RenderTexture か・`_ToonRamp_TexelSize`）を列挙する。RenderTexture のランプでも `_ToonRamp_TexelSize` が (1/幅, 1/高さ, 幅, 高さ) になっていること。ランプのあるものは 1・白・設定の影色（既定白）。ランプが無ければ 0
- [ ] **Step 3: 式の確認（試験用の球）** — 前回と同じ球（主光源のみ、y=500、Charactor レイヤー、受影なし。MToon のシェーダーは既存の MToon マテリアルの `shader` から取る）で、Toony（`CM3D2/Toony_Lighted`）と MToon を同じ条件にする: `_MainTex` 白・`_ShadowTex` 白・`_ShadowRateToon` に `ToonSkin_Shadow.tex`・`_ToonRamp` に `toonskin_SKB.tex`（Repeat のまま）・リム黒。MToon は `_UseShadowRateToon` 1・`_UseToonRamp` 1・`_ShadowRateToonV` -1・`_ToonRampLitColor` 白・`_ShadeColor` 白。N·L ごとの RGB の差が 0.03 以内で、N·L 0.9 が (0.95, 0.95, 0.95) 付近（端の混ざりなし。`toonskin_SKB.tex` の右端は白で、ライトの強さが 0.95 のため）なら合格。差が出たら `ToonSkin_Shadow.tex` と `toonskin_SKB.tex` の `filterMode` を出す（ランプは `_ShadowRateToon` のサンプラーで読むため、フィルターが違うと差になる）。続けてリムを比べる: 両方の `_MainTex` / `_ShadowTex` を黒、リム色を白（MToon は `_RimColor` 白・`_RimFresnelPower` / `_RimLift` を Toony の `_RimPower` / `_RimShift` と同じ値・`_RimLightingMix` 1）にし、N·L ごとのリムの出力が 0.03 以内で一致すること（リムの係数 0.5 がランプ全体を掛ける式でも合うか）。続けて環境光（`RenderSettings.ambientMode` Flat・0.5、主光源の強さ 0、MToon は `_GiEqualization` 0）で、MToon に `_ToonRampLitColor` (0.58, 0.58, 0.58) を入れても出力が 0.5 付近（ランプを掛けない）であること。ポイントライトだけ（主光源 0）で、MToon の `_ToonRamp` を白・黒にして出力が変わらないこと。終わったら試験用の物体を消し、ライト・環境光を戻す
- [ ] **Step 4: 見た目の確認** — 横後ろからの光（主光源の回転を一時的に変える）で、MToon（ON）・MToon（`_UseToonRamp` / `_UseShadowRateToon` を 0）・ゲームの Toony（プラグインを一時的に無効にして元のシェーダーへ戻す。戻したら控えの renderQueue も戻す）を撮り比べ、肌の境界の赤い帯がゲームに近いことを見る。顔タブで前髪の影が出ている状態で、影の中が影側の色（肌の赤み）になることを見る。髪の無限色を変えて、MToon のまま髪の色が追従することを見る（`maid.Parts` の髪色を変えるか、エディット画面で変える。変えたら元に戻す）。最後に主光源の回転を戻す

## レビュー却下メモ

- `UNITY_DECLARE_TEX2D_NOSAMPLER` を `MTOON_DECLARE_TEX2D_NOSAMPLER` で包んで URP にも対応する — URP は非対応（README の制限）で、バンドルも Built-in だけを作るため見送り
- ランプの読み取りに失敗した（`ToonRampReader` が白を返した）とき `_ToonRampDarkColor` が白になり、前髪の影が白へ寄る — 既存の 2 色の近似も同じく白になる失敗時の扱いで、警告ログも出るため許容
