# COM3D25.MToonMaid.Plugin 陰影の濃さ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** グループ設定に「陰影の濃さ」（0〜1、既定 1）を足し、主光源の陰影（`_ShadowRateToon` / 境界の位置・硬さで決まる陰、落ち影、前髪の影、ランプの暗い色）をまとめて光側へ寄せて薄くできるようにする。

**Architecture:** MToon10 フォークに `_ShadeStrength` を足し、ForwardBase で `shade` を `lerp(1, shade, _ShadeStrength)`、ランプの色を `lerp(ランプの光側の端, ランプの色, _ShadeStrength)` にする。リムも同じ `shade` / `rampColor` を受けるため一緒に薄くなる。C# は `GroupSettings.shadeStrength` を `MaterialConverter.ApplyGroup` で `Mathf.Clamp01` して書き、UI の陰影の節に「陰影の濃さ」スライダーを足す。シェーダーは `build-bundle.bat` で埋め込みバンドルを作り直す。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（バンドル）/ MTEUtils（submodule、編集しない）

**Spec:** 本計画（ユーザー要望「陰影全体の濃さを調整したい」）。Task 3 で `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md` に「改訂 2026-10-10 (6): 陰影の濃さ」節として写す

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / バンドル: `build-bundle.bat`（成否は `UnityProject2022/Logs/build-bundle.log` に `Shader error` が無いことと `Resources/mtoon_bundle` の更新で見る）

## Global Constraints

- 今のブランチ（`feat/face-shadow-move`）に積む（ユーザー決定）。worktree は使わない。本計画ファイルは Task 1 のコミットに含める
- コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- 設定ファイルの版（`MaidSettings.CurrentVersion`）は上げない。要素が無ければフィールド初期値 1 で読み、個別（overrides）にしない
- 継承の単位キー: `shadeStrength`（フィールド `shadeStrength`）。設定ファイルに書くため値を変えない
- 効くのは ForwardBase（主光源とアウトラインのパス）だけ。ForwardAdd（追加ライト）は変えない（影側は既存の「追加ライトの影側」で決める）。環境光（GI）・エミッションも変えない
- 濃さ 1 で今と完全に同じ見た目（式が恒等になる）。濃さ 0 で主光源の分は光側一色（ランプは N·L = 1 の座標の色。光側の面でもランプに勾配があれば少し明るく・色味が変わる）
- 落ち影は影マップの減衰（`directLightAttenuation`）で、周囲の物がメイドに落とす影も含めて薄くなる

## Review Focus

- **濃さ 1（既定）で見た目が変わらない**: 既存ユーザーの絵が変わらない → Task 3 Step 1〜2 で変更前後のスクリーンショットを比べる
- **ランプ ON（「ゲームの陰影」ON でランプあり）で濃さ 0**: 肌の境界の赤い帯も消え、光側の色だけになる（影側が暗いまま残らない） → Task 3 Step 3
- **「ゲームの陰影」OFF / ランプの無い MOD**: 2 色の近似の経路でも薄くなる → Task 3 Step 3（OFF に切り替えて見る）
- **手編集で範囲外（2、-1、NaN）の値**: 0〜1 へ丸める（NaN は `Mathf.Clamp01` で NaN のまま残るため 1 にする） → Task 2 の `ShadowRateParams.ClampShadeStrength` テスト
- **旧設定ファイル（要素なし）**: 1 で読み、個別にならない → Task 2 のテスト
- **追加ライト・リム・アウトライン**: 追加ライトの見た目は濃さで変わらない。リムの影側・線の陰影は一緒に薄くなる → Task 3 Step 3

---

### Task 1: シェーダーに `_ShadeStrength` を足す

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`（先頭の改変履歴コメント、Properties の `_ShadowRateShift` の後）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl`（CBUFFER）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl`（`GetMToonLighting_ToonRamp` / `GetMToonLighting`）
- Generated: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

**Interfaces:**
- Produces: マテリアルのプロパティ `_ShadeStrength`（Range(0, 1)、既定 1）。Task 2 が `MToonProps.ShadeStrength` で書く

- [ ] **Step 1: Properties** — 改変履歴の最後に `// - _ShadeStrength で ForwardBase の陰影 (落ち影・前髪の影・ランプの暗さを含む) を光側へ寄せる` を足す。`_ShadowRateShift` の行の後に次を足す:

```
        // COM3D25: 主光源の陰影の濃さ。0 で光側一色、1 で元の陰影 (プラグインが設定する)
        _ShadeStrength ("Shade Strength", Range(0, 1)) = 1
```

- [ ] **Step 2: 宣言** — `input.hlsl` の CBUFFER の `half _ShadowRateShift;` の後に `half _ShadeStrength;` を足す

- [ ] **Step 3: ランプの光側の端** — `lighting_mtoon.hlsl` の `GetMToonLighting_ToonRamp` の前に次を足し、`GetMToonLighting_ToonRamp` に引数 `const half shadeStrength` を足して戻り値を寄せる:

```hlsl
// COM3D25: 陰影の濃さ 0 のときのランプの色。座標の u を 1 (N·L = 1) にする。
// 影側の色 (_ToonRampDarkColor) の逆として固定の白にすると、ランプの光側が暗い髪 (黒髪で 0.58) が明るくなりすぎる
inline half3 GetMToonLighting_ToonRampLit()
{
    const float2 uv = MToon_ClampToTexels(float2(1, _ShadowRateToonV < 0 ? 1 : _ShadowRateToonV), _ToonRamp_TexelSize);
    return _ToonRamp.SampleLevel(sampler_ShadowRateToon, uv, 0).rgb;
}
```

```hlsl
inline half3 GetMToonLighting_ToonRamp(const UnityLighting lighting, const MToonInput input, const half dotNL, const half hairShadow, const half shadeStrength)
{
    // (既存の 4 行はそのまま)
    return lerp(GetMToonLighting_ToonRampLit(), lerp(_ToonRampDarkColor.rgb, ramp, unshadowed), shadeStrength);
}
```

- [ ] **Step 4: `GetMToonLighting` で寄せる** — `shade` と `rampColor` の行を次にする（`shadow` の行は変えない）:

```hlsl
    // COM3D25: 追加ライトの影側は _AdditionalLightShade で決めるため、陰影の濃さは ForwardBase だけに掛ける
    const half shadeStrength = MToon_IsForwardBasePass() ? _ShadeStrength : 1;
    const half shade = lerp(1, GetMToonLighting_Shade(unityLight, input, dotNL, hairShadow), shadeStrength);
    const half shadow = GetMToonLighting_Shadow(unityLight, dotNL, shade);
    const half3 rampColor = MToon_IsForwardBasePass() && _UseToonRamp > 0.5 ? GetMToonLighting_ToonRamp(unityLight, input, dotNL, hairShadow, shadeStrength) : half3(1, 1, 1);
```

- [ ] **Step 5: バンドルを作る** — Run: `build-bundle.bat` / Expected: `rg -c "Shader error" UnityProject2022/Logs/build-bundle.log` が何も出さず（exit 1）、`git status` に `Resources/mtoon_bundle` の変更が出る。コンパイルエラーなら `rg -n -A1 "Shader error"` で行番号を見て直す

- [ ] **Step 6: コミット** — シェーダー 3 ファイルと `mtoon_bundle`: `feat: MToon に主光源の陰影の濃さ (_ShadeStrength) を足す`

（実機での見た目の確認は C# から値を書けるようになる Task 3 で行う）

---

### Task 2: 設定・変換・UI に「陰影の濃さ」を足す

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`（フィールド、`CopyField`、`FieldEquals`）
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`（定数、`All`）
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/ShadowRateParams.cs`（`ClampShadeStrength`）
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs`（`ApplyGroup`）
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（`DrawSettings` の陰影の節）
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`、`source/COM3D25.MToonMaid.Plugin.Tests/ShadowRateParamsTests.cs`

**Interfaces:**
- Consumes: `_ShadeStrength`（Task 1）
- Produces: `GroupSettings.shadeStrength`（float、既定 1f）、`GroupSettingKeys.ShadeStrength = "shadeStrength"`、`ShadowRateParams.ClampShadeStrength(float) : float`、`MToonProps.ShadeStrength = "_ShadeStrength"`

- [ ] **Step 1: 失敗するテストを書く** — `CommonSettingsTests.cs` の `ChangeAll` に `g.shadeStrength = 0.4f;` を `g.shadeColor.r = 0.5f;` の次に足し、`追加ライトの影側は既定1で…` の後に次を足す:

```csharp
        [Fact]
        public void 陰影の濃さは既定1で要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.ShadeStrength, GroupSettingKeys.All);
            Assert.Equal(1f, GroupSettings.CreateDefault().shadeStrength);

            var v2 = Path.Combine(_dir, "v2ss.xml");
            File.WriteAllText(v2,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><hair><shadingShift>0.2</shadingShift></hair></MToonMaid>");
            var s2 = MaidSettingsStore.Load(v2, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s2.hair.overrides);
            Assert.Equal(1f, s2.Resolve(PartGroup.Hair).shadeStrength);

            var v3 = Path.Combine(_dir, "v3ss.xml");
            File.WriteAllText(v3,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\"><common><shadingShift>0.1</shadingShift></common></MToonMaid>");
            var s3 = MaidSettingsStore.Load(v3, _warnings.Add);

            Assert.Equal(1f, s3.common.shadeStrength);
            Assert.Equal(1f, s3.Resolve(PartGroup.Skin).shadeStrength);
        }
```

`ShadowRateParamsTests.cs` に次を足す:

```csharp
        [Theory]
        [InlineData(0.4f, 0.4f)]
        [InlineData(2f, 1f)]
        [InlineData(-1f, 0f)]
        [InlineData(float.NaN, 1f)]
        [InlineData(float.PositiveInfinity, 1f)]
        public void 陰影の濃さは0から1へ丸め非有限は1にする(float value, float expected)
        {
            Assert.Equal(expected, ShadowRateParams.ClampShadeStrength(value));
        }
```

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: `shadeStrength` / `ShadeStrength` / `ClampShadeStrength` が無くビルドエラー

- [ ] **Step 3: 設定** — `GroupSettings.cs` の `public ColorData shadeColor;` の後に:

```csharp
        // 主光源の陰影 (落ち影・前髪の影・ランプの暗さを含む) の濃さ (0〜1)。0 で光側一色。追加ライトには効かない
        public float shadeStrength = 1f;
```

`CopyField` の `ShadeColor` の次に `case GroupSettingKeys.ShadeStrength: shadeStrength = from.shadeStrength; break;`、`FieldEquals` の `ShadeColor` の次に `case GroupSettingKeys.ShadeStrength: return FloatEquals(shadeStrength, other.shadeStrength);` を足す。

`GroupSettingKeys.cs` の `ShadeColor` の次に `public const string ShadeStrength = "shadeStrength";`、`All` の `ShadeColor,` の次に `ShadeStrength,` を足す。

- [ ] **Step 4: 丸め** — `ShadowRateParams.cs` の末尾（クラス内）に:

```csharp
        /// <summary>_ShadeStrength に書く値。手編集の範囲外を 0〜1 へ丸める</summary>
        public static float ClampShadeStrength(float value)
        {
            // Mathf.Clamp01 は NaN をそのまま返し、シェーダーの lerp が NaN になって真っ黒になる
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 1f;
            }
            return System.Math.Max(0f, System.Math.Min(1f, value));
        }
```

（テストは UnityEngine を参照しないため `Mathf` は使わない。`ShadowRateParams.cs` には `using System;` が無いため完全修飾で書く）

- [ ] **Step 5: 書き込み** — `MToonProps.cs` の `ShadowRateShift` の次に `public const string ShadeStrength = "_ShadeStrength";`。`MaterialConverter.ApplyGroup` の `m.SetFloat(MToonProps.ShadowRateShift, settings.shadowRateShift);` の次に:

```csharp
            m.SetFloat(MToonProps.ShadeStrength, ShadowRateParams.ClampShadeStrength(settings.shadeStrength));
```

- [ ] **Step 6: UI** — `MToonMaidWindow.DrawSettings` の影色の `ColorRow` の次に:

```csharp
            Slider(view, GroupSettingKeys.ShadeStrength, "陰影の濃さ", shown.shadeStrength, 0f, 1f, def.shadeStrength, (s, v) => s.shadeStrength = v);
```

- [ ] **Step 7: テストとビルド** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: 全件 PASS（`ChangeAll` を使う既存テストが新項目も既定と違うことを確かめる）。Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / Expected: `0 エラー`

- [ ] **Step 8: コミット** — `feat: 陰影の濃さの設定とスライダーを足す`

---

### Task 3: 実機で確かめ、ドキュメントに書く

**Files:**
- Modify: `README.md`（制限の節、「ゲームの陰影」の行の後）
- Modify: `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md`（1 メイド分の中身の陰影の行、末尾に改訂節）

- [ ] **Step 1: 変更前を撮る** — 変更前の DLL のゲームで、MToon にしたメイドを固定の構図・ライトにして `screenshot` で撮る（ゲームが起動していなければ restart-verify スキルで起動してから撮る）

- [ ] **Step 2: 実機へ反映して濃さ 1 で比べる** — `com3d25-devbridge:restart-verify` スキルで DLL を入れ替えて同じセーブを読み、Step 1 と同じ構図で撮り比べて差が無いことを見る

- [ ] **Step 3: 濃さを動かす** — 共通タブで「陰影の濃さ」を 0.5 → 0 にして `screenshot`。確かめること:
  - 1 → 0.9 → 0.5 → 0 と動かして、見た目が段差なく連続して変わる
  - 0 で肌・髪・衣装の主光源の陰（肌の境界の赤い帯、落ち影、前髪の影を含む）が消え、光側の色だけになる。黒髪が明るくなりすぎない
  - 「ゲームの陰影」を OFF にしても 0 で陰が消える
  - リムの影側・アウトラインの陰影も一緒に薄くなる
  - ポイントライトを置き、追加ライトの見た目が濃さで変わらない
  - 顔タブだけ個別にして 0 にすると顔だけ薄くなる

- [ ] **Step 4: README** — 制限の節の「ポイント・スポットなどの追加ライトは…」の行の前に足す:

```markdown
- 主光源の陰影の濃さは「陰影の濃さ」で変えられます（1 で今までどおり、0 で陰影が消えて光側の色だけになります）。周囲の物や体が落とす影・前髪の影・トゥーンランプの暗さもまとめて薄くなります。追加ライトの影側には効きません（「追加ライトの影側」で変えてください）
```

- [ ] **Step 5: 設計書** — 「1 メイド分の中身」の陰影の行に `_ShadeStrength`（改訂 2026-10-10 (6)）を足し、末尾に次の節を足す:

```markdown
## 改訂 2026-10-10 (6): 陰影の濃さ

### 背景

影色は既定が白で、暗さはランプから入るため、影色ではランプより薄くできなかった。陰影全体をまとめて薄くする項目が無かった。

### 仕様

- グループ設定に `shadeStrength`（0〜1、既定 1、継承の単位キー `shadeStrength`）を足し、`_ShadeStrength` に書く。手編集の範囲外は 0〜1 へ丸め、非有限は 1 にする
- ForwardBase（主光源とアウトラインのパス）で、陰影の割合（`_ShadowRateToon` または境界の位置・硬さ × 落ち影 × (1 - 前髪の影)）を `lerp(1, 割合, 濃さ)`、ランプの色を `lerp(ランプの光側の端, ランプの色, 濃さ)` にする。光側の端は u = 1（N·L = 1、Muscle は v = 0.75 のまま）で読む。落ち影（影マップの減衰）は周囲の物の影も含めて薄くなる。リムも同じ値を使う
- ForwardAdd（追加ライト）・環境光・エミッションには効かない
- 濃さ 1 で従来と同じ見た目
```

- [ ] **Step 6: コミット** — `docs: 陰影の濃さを README と設計書に書く`

## レビュー却下メモ

- PBR 補正モードで濃さ 0 でも N·L<0 が黒くなる — `MToon_IsPbrCorrectOn()` は `vrmc_materials_mtoon_define.hlsl:29` で常に false のため誤検知
- `additionalLightShade` の丸め方（`Mathf.Clamp01`）も NaN 対策に揃える — 既存項目の変更でスコープ外
