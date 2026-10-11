# COM3D25.MToonMaid.Plugin 前髪の影 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** メイドの前髪（`hairF`）を影を落とす向きから 512² の深度へ投影し、MToon にした顔のシェーダーがそれと比べて前髪の陰を影側へ倒す。向きは固定（頭のボーン基準）とライト追従を切り替えられる。

**Architecture:** 計算は Unity に依存しない `HairShadowProjection`（ワールド → 影マップの UV と深度の行列）と `MainDirectionalLightSelector`（主光源の選択）に置いて単体テストする。`HairShadowRenderer` がメイドごとの RT・CommandBuffer を持ち、プラグインが `Camera.onPreCull` でフレームに 1 回、各メイドの `MaidMToonController` 経由で描かせる。前髪は専用シェーダー `Hidden/COM3D25/MToonHairShadowCaster` で深度を書き、顔の MToon マテリアルへ `_HairShadowTex` / `_HairShadowMatrix` / `_HairShadowStrength` を入れる。MToon シェーダーは ForwardBase の陰影にだけ前髪の影を掛ける。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（シェーダーのバンドル）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-hair-shadow-design.md`（前提として v2.7 `2026-10-09-mtoon-maid-remove-sphere-design.md`）

## Global Constraints

- v2.7 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-remove-sphere.md`）の Global Constraints をすべて引き継ぐ。特に: COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は実行しない、worktree 不使用、main で作業、MTEUtils は編集しない、`debug.bat` は実機検証のタスクでだけ使う、`build-bundle.bat` は Unity エディタを閉じて実行する、過去の spec・計画は書き換えない
- **ゲームを終了する前に、ユーザーへシーンの保存の有無を明示的に確認し、回答を ledger に書く**（状況からの推測で承認とみなさない）
- 設定ファイルの版は 3 のまま。要素の無いファイルは既定（前髪の影 0・固定・30°）で読む
- 継承の単位は 18 項目（v2.7 の 15 項目 + `hairShadow` / `hairShadowMode` / `hairShadowAngle`）
- 効くのは顔のグループだけ（`ToonMaterialClassifier.UsesHairShadow`）。肌・髪・衣装のタブでは 3 行とも個別のトグルを含めて無効表示
- 影を落とすのは `hairF` スロットだけ。そのスロットを MToon にしているかに関係なく使う
- 固定値: 影マップ 512²・RFloat・深度 16bit・Point・Clamp。投影の半径 = head スロットのレンダラーの `localBounds.extents.magnitude × ルートボーンの lossyScale の最大 × 1.2`（ワールドの AABB は頭の回転で大きさが変わるため使わない。0 以下・無限なら描かない）。失敗の後は 300 フレームあけてもう一度試す。メイドが busy の間は描かない。深度のバイアス 0.004（影マップの深度 0〜1 の単位。半径 0.3m で約 2.4mm）。角度の範囲 0〜80°
- 頭のボーン（`maid.body0.trsHead`）は顔の正面が `head.up`、頭のてっぺんが `-head.right`
- GUIView の入力欄キャッシュは描画順の番号で引かれる。スライダー・トグルは全タブで同じ位置に描き、効かないタブでは無効表示にする（行を消さない）
- 実機検証では、ユーザーが今の設定に入れている値を控えてから変え、最後に戻す。ライトを動かしたら元の値へ戻す

## Review Focus

- **前髪の影 0 の不変**: 0 なら RT を作らず描かず、顔のマテリアルの `_HairShadowStrength` は 0（シェーダーはテクスチャを読まない）。0 に戻したら RT を解放する → Task 5 Step 3（`ShouldDraw` のテスト）、Task 7 Step 4
- **RT の解放漏れ**: OFF・顔のグループ OFF・メイドの破棄・プラグインの終了で RT と CommandBuffer を解放する（着替えやシーン遷移で溜まらない） → Task 5 Step 4・5、Task 7 Step 6
- **投影の上下・逆 Z**: `GL.GetGPUProjectionMatrix(…, true)` で描いた RT を、同じ行列の UV で読んだときに上下が反転しない。深度の書き込みと比較が同じ行列から出る → Task 2 のテスト（行列）、Task 7 Step 3（実機で前髪の下に影が出る）
- **光と上向きが平行**: 真上からの光（ライト追従）で投影の基底が潰れない（予備の上向きへ切り替える） → Task 2 Step 1 のテスト
- **主光源のずれ**: sun と、より明るい別の平行光があるとき、Unity が ForwardBase に選ぶ光と同じものを選ぶ → Task 3 のテスト、Task 7 Step 5（実機で色の違う 2 灯）

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/HairShadowMode.cs              (Create: 固定 / ライト追従)
├─ Settings/GroupSettingKeys.cs            (Modify: 3 キー)
├─ Settings/GroupSettings.cs               (Modify: 3 フィールド、CopyField / FieldEquals)
├─ ToonMaterialClassifier.cs               (Modify: UsesHairShadow)
├─ Rendering/HairShadowProjection.cs       (Create: 行列の計算)
├─ Rendering/MainDirectionalLightSelector.cs (Create: 主光源の選択)
├─ Rendering/HairShadowRenderer.cs         (Create: RT・CommandBuffer・顔への受け渡し)
├─ Rendering/MToonShaderProvider.cs        (Modify: 前髪のシェーダーも読む)
├─ Rendering/MToonProps.cs                 (Modify: プロパティ名)
├─ Rendering/MaterialConverter.cs          (Modify: 戻すときに _HairShadowTex を外す)
├─ MaidMToonController.cs                  (Modify: 前髪の影を描く・解放する)
├─ MToonMaidPlugin.cs                      (Modify: Camera.onPreCull、IsHairShadowAvailable)
├─ IMToonMaidHost.cs                       (Modify: IsHairShadowAvailable)
└─ UI/MToonMaidWindow.cs                   (Modify: 3 行)
source/COM3D25.MToonMaid.Plugin.Tests/
├─ CommonSettingsTests.cs                  (Modify)
├─ ToonMaterialClassifierTests.cs          (Modify)
├─ HairShadowProjectionTests.cs            (Create)
└─ MainDirectionalLightSelectorTests.cs    (Create)
UnityProject2022/
├─ Assets/Shaders/MToon10/vrmc_materials_mtoon.shader            (Modify: Properties)
├─ Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl        (Modify: 宣言)
├─ Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl (Modify: 前髪の影)
├─ Assets/Shaders/MToon10/vrmc_materials_mtoon_forward_fragment.hlsl (Modify: positionWS を渡す)
├─ Assets/Shaders/MToonMaid/HairShadowCaster.shader             (Create)
└─ Assets/Editor/BuildMToonBundle.cs                            (Modify: バンドルに足す)
README.md / spec                                                (Modify)
```

テストとビルドのコマンド（リポジトリ直下で実行）:

- テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
- ビルド（実機へ反映しない）: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
- バンドル: `build-bundle.bat`（Unity エディタを閉じて実行。成功すると `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle` が更新される）

---

### Task 1: 設定に前髪の影の 3 項目を足す

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Settings/HairShadowMode.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`, `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`, `source/COM3D25.MToonMaid.Plugin/ToonMaterialClassifier.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`, `source/COM3D25.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs`

**Interfaces:**
- Produces: `enum HairShadowMode { Fixed, Light }`、`GroupSettings.hairShadow`（float、既定 0）/ `hairShadowMode`（既定 `Fixed`）/ `hairShadowAngle`（float、既定 30）、`GroupSettingKeys.HairShadow = "hairShadow"` / `HairShadowMode = "hairShadowMode"` / `HairShadowAngle = "hairShadowAngle"`、`ToonMaterialClassifier.UsesHairShadow(PartGroup) : bool`

- [ ] **Step 1: 失敗するテストを書く**

`CommonSettingsTests.ChangeAll` の `g.faceBumpLift = 0.7f;` の次に足す:

```csharp
            g.hairShadow = 0.6f;
            g.hairShadowMode = HairShadowMode.Light;
            g.hairShadowAngle = 50f;
```

`CommonSettingsTests` の末尾（`解決の結果は…` の後）に足す:

```csharp
        [Fact]
        public void 前髪の影は既定0固定30度で要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.HairShadow, GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.HairShadowMode, GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.HairShadowAngle, GroupSettingKeys.All);
            Assert.Equal(18, GroupSettingKeys.All.Length);
            var def = GroupSettings.CreateDefault();
            Assert.Equal(0f, def.hairShadow);
            Assert.Equal(HairShadowMode.Fixed, def.hairShadowMode);
            Assert.Equal(30f, def.hairShadowAngle);

            var v2 = Path.Combine(_dir, "v2s.xml");
            File.WriteAllText(v2,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><face><shadingShift>0.2</shadingShift></face></MToonMaid>");
            var s2 = MaidSettingsStore.Load(v2, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s2.face.overrides);
            Assert.Equal(0f, s2.Resolve(PartGroup.Face).hairShadow);

            var v3 = Path.Combine(_dir, "v3s2.xml");
            File.WriteAllText(v3,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\"><common><shadingShift>0.1</shadingShift></common></MToonMaid>");
            var s3 = MaidSettingsStore.Load(v3, _warnings.Add);

            Assert.Equal(0f, s3.common.hairShadow);
            Assert.Equal(HairShadowMode.Fixed, s3.common.hairShadowMode);
            Assert.Equal(30f, s3.common.hairShadowAngle);
        }

        [Fact]
        public void 前髪の影の向きを保存して読み直せる()
        {
            var path = Path.Combine(_dir, "v3hs.xml");
            var s = Filled();
            s.SetOverride(PartGroup.Face, GroupSettingKeys.HairShadowMode, true);
            s.face.hairShadowMode = HairShadowMode.Light;
            s.common.hairShadow = 0.8f;
            s.common.hairShadowAngle = 45f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(HairShadowMode.Light, loaded.Resolve(PartGroup.Face).hairShadowMode);
            Assert.Equal(HairShadowMode.Fixed, loaded.Resolve(PartGroup.Skin).hairShadowMode);
            Assert.Equal(0.8f, loaded.Resolve(PartGroup.Face).hairShadow);
            Assert.Equal(45f, loaded.Resolve(PartGroup.Face).hairShadowAngle);
        }
```

`ToonMaterialClassifierTests` の末尾に足す（既存の `UsesFaceBumpMask` のテストと同じ書き方）:

```csharp
        [Theory]
        [InlineData(PartGroup.Face, true)]
        [InlineData(PartGroup.Skin, false)]
        [InlineData(PartGroup.Hair, false)]
        [InlineData(PartGroup.Costume, false)]
        public void 前髪の影を受けるのは顔だけ(PartGroup group, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.UsesHairShadow(group));
        }
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`hairShadow` / `HairShadowMode` / `UsesHairShadow` が無い）

- [ ] **Step 3: 実装する**

`Settings/HairShadowMode.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>前髪の影を落とす向き。設定ファイルに名前で書くため名前を変えない</summary>
    public enum HairShadowMode
    {
        // 頭のボーンの正面から上へ hairShadowAngle だけ傾けた向き (ライトによらない)
        Fixed,
        // 顔を照らす主光源の向き
        Light,
    }
}
```

`GroupSettingKeys.cs` に定数を足し、`All` の `FaceBumpLift` の後へ入れる:

```csharp
        public const string HairShadow = "hairShadow";
        public const string HairShadowMode = "hairShadowMode";
        public const string HairShadowAngle = "hairShadowAngle";
```

```csharp
            ShadeColor, ShadingShift, ShadingToony, GiEqualization, AdditionalLightShade, FaceBumpLift,
            HairShadow, HairShadowMode, HairShadowAngle, HighlightScale,
```

`GroupSettings.cs` の `faceBumpLift` の宣言の後に足す:

```csharp
        // 前髪 (hairF) が顔へ落とす影の濃さ (0〜1)。0 なら影マップを作らない。顔にだけ効く
        public float hairShadow;
        public HairShadowMode hairShadowMode = HairShadowMode.Fixed;
        // 固定のとき、頭の正面から上へ傾ける角度 (0〜80 度)
        public float hairShadowAngle = 30f;
```

`CopyField` の `FaceBumpLift` の case の後:

```csharp
                case GroupSettingKeys.HairShadow: hairShadow = from.hairShadow; break;
                case GroupSettingKeys.HairShadowMode: hairShadowMode = from.hairShadowMode; break;
                case GroupSettingKeys.HairShadowAngle: hairShadowAngle = from.hairShadowAngle; break;
```

`FieldEquals` の `FaceBumpLift` の case の後:

```csharp
                case GroupSettingKeys.HairShadow: return FloatEquals(hairShadow, other.hairShadow);
                case GroupSettingKeys.HairShadowMode: return hairShadowMode == other.hairShadowMode;
                case GroupSettingKeys.HairShadowAngle: return FloatEquals(hairShadowAngle, other.hairShadowAngle);
```

`ToonMaterialClassifier.cs` の `UsesFaceBumpMask` の後:

```csharp
        /// <summary>前髪の影を受けるグループ。前髪が落とす先は顔だけ</summary>
        public static bool UsesHairShadow(PartGroup group)
        {
            return group == PartGroup.Face;
        }
```

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS（`全項目を個別にすると…` も新しい 3 項目を含めて通る）

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.MToonMaid.Plugin/Settings/HairShadowMode.cs source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs source/COM3D25.MToonMaid.Plugin/ToonMaterialClassifier.cs source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs source/COM3D25.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs
git commit -m "feat: 前髪の影の設定を追加"
```

---

### Task 2: 影マップの投影の行列

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/HairShadowProjection.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/HairShadowProjectionTests.cs`

**Interfaces:**
- Produces（ベクトルは `float[3]`、行列は行優先の `float[16]`）:
  - `HairShadowProjection.FixedToLight(float[] faceForward, float[] headTop, float angleDegrees) : float[]` — 顔から光へ向かう単位ベクトル
  - `HairShadowProjection.Create(float[] center, float[] toLight, float[] upHint, float[] fallbackUp, float radius) : float[]` — ワールド → (u, v, 深度, 1)。中心が 0.5、光に近いほど深度が小さい
  - `HairShadowProjection.ToClip(float[] m) : float[]` — 上の行列を OpenGL 流のクリップ空間（xyz が -1〜1）へ写す行列
  - `HairShadowProjection.Apply(float[] m, float[] p) : float[]` — 点を写した xyz

- [ ] **Step 1: 失敗するテストを書く**

`HairShadowProjectionTests.cs`:

```csharp
using System;
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class HairShadowProjectionTests
    {
        private const int Precision = 4;
        private static readonly float[] Center = { 1f, 2f, 3f };
        private static readonly float[] Up = { 0f, 1f, 0f };
        private static readonly float[] Forward = { 0f, 0f, 1f };

        private static float[] Offset(float[] p, float[] dir, float scale)
        {
            return new[] { p[0] + dir[0] * scale, p[1] + dir[1] * scale, p[2] + dir[2] * scale };
        }

        [Fact]
        public void 中心は影マップの真ん中に写る()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);
            var p = HairShadowProjection.Apply(m, Center);

            Assert.Equal(0.5f, p[0], Precision);
            Assert.Equal(0.5f, p[1], Precision);
            Assert.Equal(0.5f, p[2], Precision);
        }

        [Fact]
        public void 光に近いほど深度が小さく半径の端で0と1になる()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);

            Assert.Equal(0f, HairShadowProjection.Apply(m, Offset(Center, Forward, 0.3f))[2], Precision);
            Assert.Equal(1f, HairShadowProjection.Apply(m, Offset(Center, Forward, -0.3f))[2], Precision);
        }

        [Fact]
        public void 上向きの半径の端はvが1になる()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);
            var p = HairShadowProjection.Apply(m, Offset(Center, Up, 0.3f));

            Assert.Equal(0.5f, p[0], Precision);
            Assert.Equal(1f, p[1], Precision);
            Assert.Equal(0.5f, p[2], Precision);
        }

        [Fact]
        public void 光と上向きが平行なら予備の上向きを使う()
        {
            // 真上からの光。上向きの候補 (頭のてっぺん) と平行になる
            var m = HairShadowProjection.Create(Center, Up, Up, Forward, 0.3f);
            var p = HairShadowProjection.Apply(m, Offset(Center, Forward, 0.3f));

            Assert.False(float.IsNaN(p[0]));
            Assert.Equal(1f, p[1], Precision);
            Assert.Equal(0f, HairShadowProjection.Apply(m, Offset(Center, Up, 0.3f))[2], Precision);
        }

        [Fact]
        public void 固定の角度0は正面80は上寄り()
        {
            var front = HairShadowProjection.FixedToLight(Forward, Up, 0f);
            Assert.Equal(0f, front[1], Precision);
            Assert.Equal(1f, front[2], Precision);

            var high = HairShadowProjection.FixedToLight(Forward, Up, 80f);
            Assert.Equal((float)Math.Sin(80 * Math.PI / 180), high[1], Precision);
            Assert.Equal((float)Math.Cos(80 * Math.PI / 180), high[2], Precision);
        }

        [Fact]
        public void クリップ空間では0から1が負1から1になる()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);
            var clip = HairShadowProjection.ToClip(m);

            var c = HairShadowProjection.Apply(clip, Center);
            Assert.Equal(0f, c[0], Precision);
            Assert.Equal(0f, c[1], Precision);
            Assert.Equal(0f, c[2], Precision);

            var top = HairShadowProjection.Apply(clip, Offset(Center, Up, 0.3f));
            Assert.Equal(1f, top[1], Precision);
            var near = HairShadowProjection.Apply(clip, Offset(Center, Forward, 0.3f));
            Assert.Equal(-1f, near[2], Precision);
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`HairShadowProjection` が無い）

- [ ] **Step 3: 実装する**

`Rendering/HairShadowProjection.cs`:

```csharp
using System;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// 前髪の影マップの正射影。ワールド位置を (u, v, 深度) の 0〜1 へ写す。光に近いほど深度が小さい。
    /// テストの net48 では Unity 2022 の UnityEngine.CoreModule を読めないため、
    /// ベクトルは float[3]、行列は行優先の float[16] で扱う
    /// </summary>
    public static class HairShadowProjection
    {
        // 光の向きと上向きの候補がこれ以上平行なら、基底が潰れないよう予備の上向きを使う
        private const float ParallelThreshold = 0.99f;

        /// <summary>頭の正面を、頭のてっぺんの側へ angleDegrees だけ傾けた向き (顔から光へ向かう単位ベクトル)</summary>
        public static float[] FixedToLight(float[] faceForward, float[] headTop, float angleDegrees)
        {
            var rad = angleDegrees * Math.PI / 180.0;
            return Normalize(Add(Scale(faceForward, (float)Math.Cos(rad)), Scale(headTop, (float)Math.Sin(rad))));
        }

        /// <summary>
        /// center を中心に半径 radius の立方体を影マップへ写す行列。toLight は顔から光へ向かう向き。
        /// v は upHint の向きに増える (toLight と平行なら fallbackUp を使う)
        /// </summary>
        public static float[] Create(float[] center, float[] toLight, float[] upHint, float[] fallbackUp, float radius)
        {
            // 深度は光から遠ざかる向きに増やす
            var forward = Normalize(Scale(toLight, -1f));
            var up = Math.Abs(Dot(Normalize(upHint), forward)) < ParallelThreshold ? upHint : fallbackUp;
            var right = Normalize(Cross(up, forward));
            var trueUp = Cross(forward, right);
            var s = 0.5f / radius;
            return new[]
            {
                right[0] * s, right[1] * s, right[2] * s, 0.5f - Dot(right, center) * s,
                trueUp[0] * s, trueUp[1] * s, trueUp[2] * s, 0.5f - Dot(trueUp, center) * s,
                forward[0] * s, forward[1] * s, forward[2] * s, 0.5f - Dot(forward, center) * s,
                0f, 0f, 0f, 1f,
            };
        }

        /// <summary>0〜1 の (u, v, 深度) を -1〜1 のクリップ空間 (OpenGL 流。GL.GetGPUProjectionMatrix へ渡す) へ写す行列</summary>
        public static float[] ToClip(float[] m)
        {
            var clip = new float[16];
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    // 2 × 行 - 最後の行 (= 2x - 1)
                    clip[row * 4 + col] = 2f * m[row * 4 + col] - m[12 + col];
                }
            }
            Array.Copy(m, 12, clip, 12, 4);
            return clip;
        }

        public static float[] Apply(float[] m, float[] p)
        {
            var r = new float[3];
            for (var row = 0; row < 3; row++)
            {
                r[row] = m[row * 4] * p[0] + m[row * 4 + 1] * p[1] + m[row * 4 + 2] * p[2] + m[row * 4 + 3];
            }
            return r;
        }

        private static float Dot(float[] a, float[] b)
        {
            return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        private static float[] Cross(float[] a, float[] b)
        {
            return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        }

        private static float[] Add(float[] a, float[] b)
        {
            return new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
        }

        private static float[] Scale(float[] a, float s)
        {
            return new[] { a[0] * s, a[1] * s, a[2] * s };
        }

        private static float[] Normalize(float[] a)
        {
            var len = (float)Math.Sqrt(Dot(a, a));
            return Scale(a, 1f / len);
        }
    }
}
```

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.MToonMaid.Plugin/Rendering/HairShadowProjection.cs source/COM3D25.MToonMaid.Plugin.Tests/HairShadowProjectionTests.cs
git commit -m "feat: 前髪の影マップの投影の行列を計算する"
```

---

### Task 3: 主光源の選択

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/MainDirectionalLightSelector.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/MainDirectionalLightSelectorTests.cs`

**Interfaces:**
- Produces: `struct DirectionalLightInfo { bool enabled; int cullingMask; bool notImportant; float intensity; float r, g, b; bool isSun; }`、`MainDirectionalLightSelector.Select(IList<DirectionalLightInfo> lights, int layer) : int`（見つからなければ -1）

- [ ] **Step 1: 失敗するテストを書く**

`MainDirectionalLightSelectorTests.cs`:

```csharp
using System.Collections.Generic;
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class MainDirectionalLightSelectorTests
    {
        private const int FaceLayer = 10;

        private static DirectionalLightInfo Light(float intensity, int mask = -1, bool sun = false)
        {
            return new DirectionalLightInfo
            {
                enabled = true, cullingMask = mask, intensity = intensity, r = 1f, g = 1f, b = 1f, isSun = sun,
            };
        }

        [Fact]
        public void 最も明るい平行光を選ぶ()
        {
            var lights = new List<DirectionalLightInfo> { Light(0.5f), Light(1.2f), Light(0.8f) };
            Assert.Equal(1, MainDirectionalLightSelector.Select(lights, FaceLayer));
        }

        [Fact]
        public void 明るさは強さと色の輝度の積()
        {
            var red = Light(1f);
            red.g = 0f;
            red.b = 0f;
            var gray = Light(0.5f);
            // 赤 1.0 の輝度 0.299 < 白 0.5
            Assert.Equal(1, MainDirectionalLightSelector.Select(new List<DirectionalLightInfo> { red, gray }, FaceLayer));
        }

        [Fact]
        public void 顔のレイヤーを含まない光と無効な光と頂点ライトは選ばない()
        {
            var otherLayer = Light(5f, mask: 1 << 11);
            var disabled = Light(4f);
            disabled.enabled = false;
            var vertex = Light(3f);
            vertex.notImportant = true;
            var lights = new List<DirectionalLightInfo> { otherLayer, disabled, vertex, Light(0.1f) };

            Assert.Equal(3, MainDirectionalLightSelector.Select(lights, FaceLayer));
        }

        [Fact]
        public void sunが候補にあればより明るい光があってもsunを選ぶ()
        {
            var lights = new List<DirectionalLightInfo> { Light(3f), Light(0.8f, sun: true) };
            Assert.Equal(1, MainDirectionalLightSelector.Select(lights, FaceLayer));
        }

        [Fact]
        public void sunでも顔のレイヤーを含まなければ明るい光を選ぶ()
        {
            var lights = new List<DirectionalLightInfo> { Light(3f), Light(0.8f, mask: 1 << 11, sun: true) };
            Assert.Equal(0, MainDirectionalLightSelector.Select(lights, FaceLayer));
        }

        [Fact]
        public void 候補が無ければ負1()
        {
            Assert.Equal(-1, MainDirectionalLightSelector.Select(new List<DirectionalLightInfo>(), FaceLayer));
            Assert.Equal(-1, MainDirectionalLightSelector.Select(new List<DirectionalLightInfo> { Light(1f, mask: 0) }, FaceLayer));
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`DirectionalLightInfo` / `MainDirectionalLightSelector` が無い）

- [ ] **Step 3: 実装する**

`Rendering/MainDirectionalLightSelector.cs`:

```csharp
using System.Collections.Generic;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>平行光 1 つ分の、主光源の選択に要る値 (テストの net48 で Unity の Light を使えないため写す)</summary>
    public struct DirectionalLightInfo
    {
        public bool enabled;
        public int cullingMask;
        // LightRenderMode.ForceVertex (Not Important)。ForwardBase のピクセルライトに選ばれない
        public bool notImportant;
        public float intensity;
        public float r;
        public float g;
        public float b;
        // RenderSettings.sun
        public bool isSun;
    }

    /// <summary>
    /// Unity のビルトインのフォワード描画が ForwardBase に選ぶ平行光を真似る。
    /// 顔のレイヤーを照らす候補のうち sun があればそれ、無ければ強さ × 色の輝度が最大のもの
    /// </summary>
    public static class MainDirectionalLightSelector
    {
        public static int Select(IList<DirectionalLightInfo> lights, int layer)
        {
            var best = -1;
            var bestBrightness = float.NegativeInfinity;
            for (var i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (!l.enabled || l.notImportant || (l.cullingMask & (1 << layer)) == 0)
                {
                    continue;
                }
                if (l.isSun)
                {
                    return i;
                }
                var brightness = l.intensity * (0.299f * l.r + 0.587f * l.g + 0.114f * l.b);
                if (brightness > bestBrightness)
                {
                    best = i;
                    bestBrightness = brightness;
                }
            }
            return best;
        }
    }
}
```

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.MToonMaid.Plugin/Rendering/MainDirectionalLightSelector.cs source/COM3D25.MToonMaid.Plugin.Tests/MainDirectionalLightSelectorTests.cs
git commit -m "feat: 顔を照らす主光源を選ぶ"
```

---

### Task 4: シェーダー（MToon の前髪の影・前髪の深度）とバンドル

**Files:**
- Create: `UnityProject2022/Assets/Shaders/MToonMaid/HairShadowCaster.shader`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`, `vrmc_materials_mtoon_input.hlsl`, `vrmc_materials_mtoon_lighting_mtoon.hlsl`, `vrmc_materials_mtoon_forward_fragment.hlsl`, `UnityProject2022/Assets/Editor/BuildMToonBundle.cs`, `source/COM3D25.MToonMaid.Plugin/Rendering/MToonShaderProvider.cs`, `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`, `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`
- 生成物: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

**Interfaces:**
- Produces:
  - MToon のプロパティ `_HairShadowTex`（2D、既定 white）/ `_HairShadowStrength`（Float、既定 0）/ `_HairShadowMatrix`（float4x4、Properties には書かず `Material.SetMatrix` で入れる）
  - 前髪のシェーダー `Hidden/COM3D25/MToonHairShadowCaster`。グローバル `_MToonHairShadowCasterTex` / `_MToonHairShadowCasterTex_ST` / `_MToonHairShadowCasterCutoff`（負なら切り抜かない）/ `_MToonHairShadowCasterMatrix` を読み、深度を R に書く
  - `MToonProps.HairShadowTex` / `HairShadowStrength` / `HairShadowMatrix` / `HairShadowCasterShaderName` / `CasterTex` / `CasterTexSt` / `CasterCutoff` / `CasterMatrix`
  - `MToonShaders { Shader mtoon; Shader hairShadowCaster; }`（`hairShadowCaster` は読めなければ null）、`MToonShaderProvider.Load(ManualLogSource) : MToonShaders`（MToon が読めなければ null）
  - `MToonMaidPlugin` のフィールド `_hairShadowCaster : Shader`

- [ ] **Step 1: MToon のプロパティと宣言を足す**

`vrmc_materials_mtoon.shader` の先頭の改変の一覧に 1 行足す:

```
// - プラグインが前髪だけを投影した深度 (_HairShadowTex / _HairShadowMatrix / _HairShadowStrength) で、ForwardBase の陰影に前髪の影を掛ける
```

Properties の `_AdditionalLightShade` の行の後に足す:

```
        // COM3D25: 前髪の影 (プラグインが設定する。行列 _HairShadowMatrix は Properties に書けないため SetMatrix で入れる)
        _HairShadowTex ("Hair Shadow Depth", 2D) = "white" {}
        _HairShadowStrength ("Hair Shadow Strength", Range(0, 1)) = 0
```

`vrmc_materials_mtoon_input.hlsl` のテクスチャ宣言の `_UvAnimMaskTex` の後に足す:

```hlsl
// COM3D25: 前髪の影。深度を比べるため float で読む
MTOON_DECLARE_TEX2D_FLOAT(_HairShadowTex);
```

CBUFFER の `float4 _MainTex_ST;` の後と、`half _AdditionalLightShade;` の後に足す:

```hlsl
float4 _HairShadowTex_TexelSize;
float4x4 _HairShadowMatrix;
```

```hlsl
half _HairShadowStrength;
```

- [ ] **Step 2: 前髪の影を陰影に掛ける**

`vrmc_materials_mtoon_lighting_mtoon.hlsl` の `struct MToonInput` に `float3 positionWS;` を足す（`half3 normalWS;` の前）。

`GetMToonLighting_Shade` の直前に足す:

```hlsl
// COM3D25: 前髪の影。プラグインが前髪だけを光の向きから投影した深度 (_HairShadowTex) と比べ、前髪の陰の割合を返す。
// 書き込み側も同じ _HairShadowMatrix で深度を出すため、逆 Z の差を受けない。4 点を比べて縁を 1 テクセル分なじませる
#define MTOON_HAIR_SHADOW_BIAS 0.004

inline half GetMToonLighting_HairShadow(const MToonInput input)
{
    if (_HairShadowStrength <= 0 || MToon_IsOutlinePass())
    {
        return 0;
    }
    const float3 p = mul(_HairShadowMatrix, float4(input.positionWS, 1)).xyz;
    if (any(p.xy < 0) || any(p.xy > 1))
    {
        return 0;
    }
    const float2 t = _HairShadowTex_TexelSize.xy * 0.5;
    const float d = p.z - MTOON_HAIR_SHADOW_BIAS;
    // 分岐の中で読むため、勾配の要らない LOD 0 の読み出しにする (影マップはミップマップなし)
    half s = 0;
    s += step(_HairShadowTex.SampleLevel(sampler_HairShadowTex, p.xy + float2(-t.x, -t.y), 0).r, d);
    s += step(_HairShadowTex.SampleLevel(sampler_HairShadowTex, p.xy + float2(t.x, -t.y), 0).r, d);
    s += step(_HairShadowTex.SampleLevel(sampler_HairShadowTex, p.xy + float2(-t.x, t.y), 0).r, d);
    s += step(_HairShadowTex.SampleLevel(sampler_HairShadowTex, p.xy + float2(t.x, t.y), 0).r, d);
    return s * 0.25 * _HairShadowStrength;
}
```

`GetMToonLighting_Shade` の ForwardBase の分岐の return を次へ変える:

```hlsl
        const half hairShadow = GetMToonLighting_HairShadow(input);
        return mtoon_linearstep(-1.0 + shadeToony, +1.0 - shadeToony, shadeInput + shadeShift) * lighting.directLightAttenuation.x * (1 - hairShadow);
```

`vrmc_materials_mtoon_forward_fragment.hlsl` の `mtoonInput.uv = uv;` の後に足す:

```hlsl
    mtoonInput.positionWS = input.positionWS;
```

- [ ] **Step 3: 前髪の深度のシェーダーを作る**

`UnityProject2022/Assets/Shaders/MToonMaid/HairShadowCaster.shader`:

```hlsl
// MToonMaid: 前髪の影マップへ前髪の深度を書く。値はプラグインが CommandBuffer のグローバルで渡す。
// 元のマテリアルの値を拾わないよう Properties は持たない (Properties に書いた名前はマテリアルの値が優先される)
Shader "Hidden/COM3D25/MToonHairShadowCaster"
{
    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MToonHairShadowCasterTex;
            float4 _MToonHairShadowCasterTex_ST;
            // 負なら切り抜かない (Cutout でないマテリアル)
            float _MToonHairShadowCasterCutoff;
            // ワールド → (u, v, 深度)。顔のシェーダーの _HairShadowMatrix と同じ値
            float4x4 _MToonHairShadowCasterMatrix;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float depth : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                const float4 positionWS = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, positionWS);
                o.uv = v.texcoord.xy * _MToonHairShadowCasterTex_ST.xy + _MToonHairShadowCasterTex_ST.zw;
                o.depth = mul(_MToonHairShadowCasterMatrix, positionWS).z;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                if (_MToonHairShadowCasterCutoff >= 0)
                {
                    clip(tex2D(_MToonHairShadowCasterTex, i.uv).a - _MToonHairShadowCasterCutoff);
                }
                return i.depth;
            }
            ENDCG
        }
    }
}
```

`BuildMToonBundle.cs` の `assetNames` に足す:

```csharp
                assetNames = new[]
                {
                    "Assets/Shaders/MToon10/vrmc_materials_mtoon.shader",
                    "Assets/Shaders/MToonMaid/HairShadowCaster.shader",
                },
```

- [ ] **Step 4: バンドルを作る**

Unity エディタで `UnityProject2022` を開いていないことを確かめてから、Run: `build-bundle.bat`
Expected: 「コピーしました: …\Resources\mtoon_bundle」。失敗したら `UnityProject2022/Logs/build-bundle.log` の `Shader error` の行を読んで直す。Unity が作った `UnityProject2022/Assets/Shaders/MToonMaid.meta` と `HairShadowCaster.shader.meta` もコミットに含める

- [ ] **Step 5: プラグインで前髪のシェーダーも読む**

`MToonProps.cs` に足す:

```csharp
        // 前髪の影 (顔の MToon が読む)
        public const string HairShadowTex = "_HairShadowTex";
        public const string HairShadowStrength = "_HairShadowStrength";
        public const string HairShadowMatrix = "_HairShadowMatrix";

        // 前髪の深度を書くシェーダーと、そのグローバル値 (ゲームのグローバルと衝突しない専用名)
        public const string HairShadowCasterShaderName = "Hidden/COM3D25/MToonHairShadowCaster";
        public const string CasterTex = "_MToonHairShadowCasterTex";
        public const string CasterTexSt = "_MToonHairShadowCasterTex_ST";
        public const string CasterCutoff = "_MToonHairShadowCasterCutoff";
        public const string CasterMatrix = "_MToonHairShadowCasterMatrix";
```

`MToonShaderProvider.cs` を次の形へ変える（読み込みの前半は今のまま）:

```csharp
    /// <summary>バンドルから読んだシェーダー。前髪のシェーダーは読めなければ null (前髪の影だけを無効にする)</summary>
    public sealed class MToonShaders
    {
        public Shader mtoon;
        public Shader hairShadowCaster;
    }

    /// <summary>DLL に埋め込んだ mtoon_bundle から COM3D25/MToon10 と前髪の影のシェーダーを読む</summary>
    public static class MToonShaderProvider
    {
        private const string ResourceName = "mtoon_bundle";

        /// <summary>MToon を読めない・今のグラフィックス API で描けないときは null。シェーダーは読み込んだまま保持する</summary>
        public static MToonShaders Load(ManualLogSource log)
        {
            // (stream の読み出しと AssetBundle.LoadFromMemory は今のまま)

            Shader found = null;
            Shader caster = null;
            foreach (var shader in bundle.LoadAllAssets<Shader>())
            {
                if (shader.name == MToonProps.ShaderName)
                {
                    found = shader;
                }
                else if (shader.name == MToonProps.HairShadowCasterShaderName)
                {
                    caster = shader;
                }
            }
            bundle.Unload(false);

            // (found の null / isSupported の確認と LogError・return null は今のまま)

            if (caster == null)
            {
                log.LogError(ResourceName + " に " + MToonProps.HairShadowCasterShaderName + " が含まれていないため、前髪の影は使えません");
            }
            else if (!caster.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
            {
                log.LogError("前髪の影のシェーダーか RFloat の RenderTexture が現在のグラフィックス API (" + SystemInfo.graphicsDeviceType + ") で使えないため、前髪の影は使えません");
                caster = null;
            }
            return new MToonShaders { mtoon = found, hairShadowCaster = caster };
        }
    }
```

`MToonMaidPlugin.LoadShader` を変える:

```csharp
        private void LoadShader()
        {
            _shaderLoadPending = false;
            var shaders = MToonShaderProvider.Load(Log);
            if (shaders != null)
            {
                _converter = new MaterialConverter(shaders.mtoon, new ToonRampReader(Log.LogWarning));
                _hairShadowCaster = shaders.hairShadowCaster;
                Log.LogInfo("MToonMaid 有効化");
            }
        }
```

フィールドに足す（`_converter` の後）:

```csharp
        // 前髪の深度を書くシェーダー。読めなければ null で前髪の影を使わない
        private Shader _hairShadowCaster;
```

- [ ] **Step 6: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（`_hairShadowCaster` が未使用の警告 CS0414 は Task 5 で消える）、テスト全件 PASS

- [ ] **Step 7: コミット**

```bash
git add UnityProject2022/Assets/Shaders UnityProject2022/Assets/Editor/BuildMToonBundle.cs source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle source/COM3D25.MToonMaid.Plugin/Rendering/MToonShaderProvider.cs source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs
git commit -m "feat: シェーダーに前髪の影を追加し、前髪の深度を書くシェーダーをバンドルに入れる"
```

---

### Task 5: 影マップを描いて顔へ渡す

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/HairShadowRenderer.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs`, `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`, `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/HairShadowProjectionTests.cs`（`ShouldDraw` は純粋な判定として `HairShadowProjection` に置く）

**Interfaces:**
- Consumes: Task 1 の設定、Task 2 の `HairShadowProjection.*`、Task 3 の `MainDirectionalLightSelector.Select` / `DirectionalLightInfo`、Task 4 の `MToonProps.*` と `_hairShadowCaster`
- Produces:
  - `HairShadowProjection.ShouldDraw(float strength, bool casterAvailable, bool failed) : bool`
  - `HairShadowRenderer(Shader casterShader)`、`bool Render(Maid maid, GroupSettings face, List<Material> faceMaterials)`（描けたら true。描けない状態は false で、呼び出し側が顔の強さを 0 にする）、`void Dispose()`
  - `MaidMToonController(Maid, string, MaterialConverter, Shader hairShadowCaster, Action<string>)`（引数を 1 つ足す）、`void RenderHairShadow(GroupSettings face)`、`void ReleaseHairShadow()`、`ApplyGroup` で顔のときに失敗フラグを戻す

- [ ] **Step 1: 描くかどうかの判定のテストを書く**

`HairShadowProjectionTests` に足す:

```csharp
        [Theory]
        [InlineData(0f, true, false, false)]
        [InlineData(0.5f, true, false, true)]
        [InlineData(0.5f, false, false, false)]
        [InlineData(0.5f, true, true, false)]
        [InlineData(-1f, true, false, false)]
        public void 強さが0より大きくシェーダーがあり失敗していなければ描く(float strength, bool caster, bool failed, bool expected)
        {
            Assert.Equal(expected, HairShadowProjection.ShouldDraw(strength, caster, failed));
        }
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`ShouldDraw` が無い）

- [ ] **Step 3: 判定を実装する**

`HairShadowProjection` に足す:

```csharp
        /// <summary>前髪の影を描くか。描かないときは影マップを作らず、顔の強さを 0 にする</summary>
        public static bool ShouldDraw(float strength, bool casterAvailable, bool failed)
        {
            return strength > 0f && casterAvailable && !failed;
        }
```

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` → 全件 PASS

- [ ] **Step 4: HairShadowRenderer を作る**

`Rendering/HairShadowRenderer.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の前髪の影マップ。前髪 (hairF) を影を落とす向きから正射影で描いて深度を書き、
    /// 顔の MToon マテリアルへ影マップと行列を入れる
    /// </summary>
    public sealed class HairShadowRenderer : IDisposable
    {
        private const int Resolution = 512;
        private const int DepthBits = 16;
        // head スロットのバウンズの対角の半分に掛ける余白 (前髪の毛先が頭のバウンズからはみ出しても収まるように)
        private const float RadiusScale = 1.2f;
        private const string HeadSlot = "head";
        private const string HairFrontSlot = "hairF";
        // シーン中のライトを取り直す間隔 (フレーム)。SceneEditor でライトを足してから効くまでの遅れになる
        private const int LightRefreshInterval = 30;

        private static Light[] _lights;
        private static int _lightsFrame;

        private static readonly int CasterTexId = Shader.PropertyToID(MToonProps.CasterTex);
        private static readonly int CasterTexStId = Shader.PropertyToID(MToonProps.CasterTexSt);
        private static readonly int CasterCutoffId = Shader.PropertyToID(MToonProps.CasterCutoff);
        private static readonly int CasterMatrixId = Shader.PropertyToID(MToonProps.CasterMatrix);
        private static readonly int HairShadowTexId = Shader.PropertyToID(MToonProps.HairShadowTex);
        private static readonly int HairShadowMatrixId = Shader.PropertyToID(MToonProps.HairShadowMatrix);
        private static readonly int HairShadowStrengthId = Shader.PropertyToID(MToonProps.HairShadowStrength);
        private static readonly int MainTexId = Shader.PropertyToID(MToonProps.MainTex);
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int AlphaModeId = Shader.PropertyToID(MToonProps.AlphaMode);

        private readonly Material _casterMaterial;
        private readonly CommandBuffer _commands = new CommandBuffer { name = "MToonMaid_HairShadow" };
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        private readonly List<Material> _materialBuffer = new List<Material>();
        private readonly List<DirectionalLightInfo> _lightInfos = new List<DirectionalLightInfo>();
        // _lightInfos と同じ順の平行光
        private readonly List<Light> _lightCandidates = new List<Light>();
        private RenderTexture _texture;

        public HairShadowRenderer(Shader casterShader)
        {
            _casterMaterial = new Material(casterShader) { name = "MToonMaid_HairShadowCaster", hideFlags = HideFlags.DontSave };
        }

        /// <summary>描けたら true。頭・前髪・(ライト追従で) 主光源が無いときは false で、何も描かない</summary>
        public bool Render(Maid maid, GroupSettings face, List<Material> faceMaterials)
        {
            var body = maid.body0;
            var head = body != null ? body.trsHead : null;
            var headRenderer = FindRenderer(body, HeadSlot);
            var hairSlot = body != null ? body.GetSlot(HairFrontSlot) : null;
            if (head == null || headRenderer == null || hairSlot == null || hairSlot.obj == null || faceMaterials.Count == 0)
            {
                return false;
            }

            // COM3D2 の Bip01 Head は顔の正面が up、頭のてっぺんが -right
            var faceForward = head.up;
            var headTop = -head.right;
            float[] toLight;
            if (face.hairShadowMode == HairShadowMode.Light)
            {
                var light = FindMainLight(headRenderer.gameObject.layer);
                if (light == null)
                {
                    return false;
                }
                toLight = ToArray(-light.transform.forward);
            }
            else
            {
                toLight = HairShadowProjection.FixedToLight(ToArray(faceForward), ToArray(headTop), Mathf.Clamp(face.hairShadowAngle, 0f, 80f));
            }

            // ワールドの AABB は頭の回転で大きさが変わり影マップの解像度が脈動するため、回転によらないローカルのバウンズから取る
            var radius = headRenderer.localBounds.extents.magnitude * MaxAbs(BoundsSpace(headRenderer).lossyScale) * RadiusScale;
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                return false;
            }
            var projection = HairShadowProjection.Create(ToArray(head.position), toLight, ToArray(headTop), ToArray(faceForward), radius);
            var worldToShadow = ToMatrix(projection);
            var clip = ToMatrix(HairShadowProjection.ToClip(projection));

            EnsureTexture();
            _commands.Clear();
            _commands.SetRenderTarget(_texture);
            // 何も無い所は最も遠い (1) にして影にしない
            _commands.ClearRenderTarget(true, true, Color.white);
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(clip, true));
            _commands.SetGlobalMatrix(CasterMatrixId, worldToShadow);
            hairSlot.obj.GetComponentsInChildren(true, _rendererBuffer);
            foreach (var renderer in _rendererBuffer)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                renderer.GetSharedMaterials(_materialBuffer);
                for (var i = 0; i < _materialBuffer.Count; i++)
                {
                    var m = _materialBuffer[i];
                    if (m == null)
                    {
                        continue;
                    }
                    var tex = m.HasProperty(MainTexId) ? m.GetTexture(MainTexId) : null;
                    var scale = tex != null ? m.GetTextureScale(MainTexId) : Vector2.one;
                    var offset = tex != null ? m.GetTextureOffset(MainTexId) : Vector2.zero;
                    _commands.SetGlobalTexture(CasterTexId, tex != null ? tex : Texture2D.whiteTexture);
                    _commands.SetGlobalVector(CasterTexStId, new Vector4(scale.x, scale.y, offset.x, offset.y));
                    _commands.SetGlobalFloat(CasterCutoffId, IsCutout(m) && m.HasProperty(CutoffId) ? m.GetFloat(CutoffId) : -1f);
                    _commands.DrawRenderer(renderer, _casterMaterial, i, 0);
                }
            }
            Graphics.ExecuteCommandBuffer(_commands);

            var strength = Mathf.Clamp01(face.hairShadow);
            foreach (var m in faceMaterials)
            {
                m.SetTexture(HairShadowTexId, _texture);
                m.SetMatrix(HairShadowMatrixId, worldToShadow);
                m.SetFloat(HairShadowStrengthId, strength);
            }
            return true;
        }

        /// <summary>顔のマテリアルから影を外す (MToon のまま前髪の影だけを止めるとき)</summary>
        public static void Clear(List<Material> faceMaterials)
        {
            foreach (var m in faceMaterials)
            {
                m.SetFloat(HairShadowStrengthId, 0f);
                m.SetTexture(HairShadowTexId, null);
            }
        }

        public void Dispose()
        {
            _commands.Release();
            if (_texture != null)
            {
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
            UnityEngine.Object.Destroy(_casterMaterial);
        }

        private void EnsureTexture()
        {
            if (_texture != null && _texture.IsCreated())
            {
                return;
            }
            if (_texture == null)
            {
                _texture = new RenderTexture(Resolution, Resolution, DepthBits, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                {
                    name = "MToonMaid_HairShadow",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    hideFlags = HideFlags.DontSave,
                };
            }
            // デバイスの喪失などで中身が消えたら作り直す
            _texture.Create();
        }

        private Light FindMainLight(int layer)
        {
            // Light.GetLights は Unity 2022 で非推奨。FindObjectsOfType は重いため、全メイドで共有して間隔をあけて取り直す
            if (_lights == null || Time.frameCount - _lightsFrame >= LightRefreshInterval)
            {
                _lights = UnityEngine.Object.FindObjectsOfType<Light>();
                _lightsFrame = Time.frameCount;
            }
            var sun = RenderSettings.sun;
            _lightInfos.Clear();
            _lightCandidates.Clear();
            foreach (var l in _lights)
            {
                // 取り直すまでの間に破棄されたライト (Unity の == は破棄済みを null と判定する)
                if (l == null || l.type != LightType.Directional)
                {
                    continue;
                }
                _lightCandidates.Add(l);
                var c = l.color;
                _lightInfos.Add(new DirectionalLightInfo
                {
                    enabled = l.isActiveAndEnabled,
                    cullingMask = l.cullingMask,
                    notImportant = l.renderMode == LightRenderMode.ForceVertex,
                    intensity = l.intensity,
                    r = c.r,
                    g = c.g,
                    b = c.b,
                    isSun = l == sun,
                });
            }
            var index = MainDirectionalLightSelector.Select(_lightInfos, layer);
            return index >= 0 ? _lightCandidates[index] : null;
        }

        private static bool IsCutout(Material m)
        {
            // MToon にした前髪は名前から判断できないため、変換時に入れた _AlphaMode を見る
            if (m.HasProperty(AlphaModeId) && m.shader.name == MToonProps.ShaderName)
            {
                return m.GetInt(AlphaModeId) == MToonProps.AlphaModeCutout;
            }
            return ToonMaterialClassifier.IsCutout(m.shader.name);
        }

        private static SkinnedMeshRenderer FindRenderer(TBody body, string slot)
        {
            var skin = body != null ? body.GetSlot(slot) : null;
            return skin != null && skin.obj != null ? skin.obj.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        }

        /// <summary>SkinnedMeshRenderer の localBounds はルートボーンの空間にある</summary>
        private static Transform BoundsSpace(SkinnedMeshRenderer r)
        {
            return r.rootBone != null ? r.rootBone : r.transform;
        }

        private static float MaxAbs(Vector3 v)
        {
            return Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
        }

        private static float[] ToArray(Vector3 v)
        {
            return new[] { v.x, v.y, v.z };
        }

        private static Matrix4x4 ToMatrix(float[] m)
        {
            var r = new Matrix4x4();
            for (var row = 0; row < 4; row++)
            {
                r.SetRow(row, new Vector4(m[row * 4], m[row * 4 + 1], m[row * 4 + 2], m[row * 4 + 3]));
            }
            return r;
        }
    }
}
```

- [ ] **Step 5: コントローラーとプラグインにつなぐ**

`MaidMToonController`:

- コンストラクタに `Shader hairShadowCaster` を足し（`converter` の後）、次のフィールドを足す:

```csharp
        // 前髪の影の描画が例外になったら、この間隔 (フレーム) をあけてもう一度試す (着替え中の破棄済みレンダラー等の一過性の失敗で止めきらない)
        private const int HairShadowRetryFrames = 300;

        private readonly Shader _hairShadowCaster;
        private HairShadowRenderer _hairShadow;
        // このフレームより前は描かない (失敗の後の待ち)
        private int _hairShadowRetryFrame;
        // 失敗の警告は 1 回だけ出す
        private bool _hairShadowWarned;
        private readonly List<Material> _faceMaterials = new List<Material>();
```

- メソッドを足す:

```csharp
        /// <summary>
        /// カメラの描画の前に呼ぶ (フレームに 1 回)。前髪の影を描いて顔へ入れる。
        /// 描かないときは影マップを解放し、顔の強さを 0 にする
        /// </summary>
        public void RenderHairShadow(GroupSettings face)
        {
            CollectFaceMaterials();
            var waiting = Time.frameCount < _hairShadowRetryFrame;
            if (!HairShadowProjection.ShouldDraw(face.hairShadow, _hairShadowCaster != null, waiting) || _faceMaterials.Count == 0)
            {
                ReleaseHairShadow();
                return;
            }
            // 着替えなどの最中はレンダラーが作り直されるため描かない (影マップは次に描くまで残す)
            if (Maid.IsBusy)
            {
                HairShadowRenderer.Clear(_faceMaterials);
                return;
            }
            try
            {
                if (_hairShadow == null)
                {
                    _hairShadow = new HairShadowRenderer(_hairShadowCaster);
                }
                if (!_hairShadow.Render(Maid, face, _faceMaterials))
                {
                    HairShadowRenderer.Clear(_faceMaterials);
                }
            }
            catch (Exception ex)
            {
                // 毎フレーム同じ例外を出さないよう、間をあけてもう一度試す
                _hairShadowRetryFrame = Time.frameCount + HairShadowRetryFrames;
                ReleaseHairShadow();
                if (!_hairShadowWarned)
                {
                    _hairShadowWarned = true;
                    _warn("前髪の影を描けないため一時的に止めます (しばらくして、または設定を変えるともう一度試します): " + ex.Message);
                }
            }
        }

        /// <summary>影マップを解放し、MToon のままの顔から影を外す</summary>
        public void ReleaseHairShadow()
        {
            if (_hairShadow == null)
            {
                return;
            }
            CollectFaceMaterials();
            HairShadowRenderer.Clear(_faceMaterials);
            _hairShadow.Dispose();
            _hairShadow = null;
        }

        private void CollectFaceMaterials()
        {
            _faceMaterials.Clear();
            foreach (var e in _entries.Values)
            {
                if (e.material != null && e.material.shader == _converter.MToonShader
                    && ToonMaterialClassifier.UsesHairShadow(e.backup.group))
                {
                    _faceMaterials.Add(e.material);
                }
            }
        }
```

- `ApplyGroup` の先頭に足す（設定を変えたらもう一度試す）:

```csharp
            if (ToonMaterialClassifier.UsesHairShadow(group))
            {
                _hairShadowRetryFrame = 0;
            }
```

- `RestoreAll` の先頭と `DestroyOwnedTextures` の先頭に `ReleaseHairShadow();` を足す（`DestroyOwnedTextures` ではマテリアルが破棄済みのため `Clear` は何もしない。`RenderTexture` だけが解放される）

`MaterialConverter.RestoreUnchecked` の `m.SetTexture(MToonProps.ShadingShiftTex, null);` の後に足す:

```csharp
            // 解放する影マップを Toony に残さない
            m.SetTexture(MToonProps.HairShadowTex, null);
```

`MToonMaidPlugin`:

- `AttachControllers` の `new MaidMToonController(maid, guid, _converter, Log.LogWarning)` を `new MaidMToonController(maid, guid, _converter, _hairShadowCaster, Log.LogWarning)` にする
- フィールド `private int _hairShadowFrame = -1;` を足す
- `Awake` の `SceneManager.sceneLoaded += OnSceneLoaded;` の後に `Camera.onPreCull += OnCameraPreCull;`、`OnDestroy` に `Camera.onPreCull -= OnCameraPreCull;` と全コントローラーの `ReleaseHairShadow()` を足す
- メソッドを足す:

```csharp
        /// <summary>
        /// 揺れもの (前髪の物理) が LateUpdate で動いた後に描くため、カメラの描画の直前に前髪の影を描く。
        /// カメラが複数あってもフレームに 1 回にする
        /// </summary>
        private void OnCameraPreCull(Camera camera)
        {
            if (_converter == null || _hairShadowFrame == Time.frameCount)
            {
                return;
            }
            _hairShadowFrame = Time.frameCount;
            foreach (var pair in _controllers)
            {
                // 破棄済みや別キャラは次の Update の TickControllers が片付ける
                if (pair.Key == null || pair.Key.status == null || pair.Key.status.guid != pair.Value.Guid)
                {
                    continue;
                }
                pair.Value.RenderHairShadow(_store.Get(pair.Value.Guid).Resolve(PartGroup.Face));
            }
        }
```

- [ ] **Step 6: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 警告なしでビルド成功（`_hairShadowCaster` の CS0414 も消える）、テスト全件 PASS

- [ ] **Step 7: コミット**

```bash
git add source/COM3D25.MToonMaid.Plugin/Rendering/HairShadowRenderer.cs source/COM3D25.MToonMaid.Plugin/Rendering/HairShadowProjection.cs source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs source/COM3D25.MToonMaid.Plugin.Tests/HairShadowProjectionTests.cs
git commit -m "feat: 前髪の影マップを描いて顔の MToon へ渡す"
```

---

### Task 6: ウィンドウに 3 行を足す

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/IMToonMaidHost.cs`, `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`, `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`

**Interfaces:**
- Consumes: Task 1 の設定とキー、Task 4 の `_hairShadowCaster`
- Produces: `IMToonMaidHost.IsHairShadowAvailable : bool`

- [ ] **Step 1: ホストに足す**

`IMToonMaidHost` に足す:

```csharp
        /// <summary>前髪の影のシェーダーを読めたか。false ならウィンドウの前髪の影の行を無効表示にする</summary>
        bool IsHairShadowAvailable { get; }
```

`MToonMaidPlugin` に足す:

```csharp
        public bool IsHairShadowAvailable => _hairShadowCaster != null;
```

- [ ] **Step 2: ウィンドウに行を足す**

`MToonMaidWindow.Toggle` に `bool editable = true` の引数を足し、`Slider` と同じく前後で `view.SetEnabled(_baseEnabled && editable);` / `view.SetEnabled(_baseEnabled);` で囲む。

`DrawSettings` の「凹凸の影を消す」の行の後に足す:

```csharp
            // 顔以外と、前髪のシェーダーを読めないときは個別のトグルを含めて無効表示にする (行を消すと入力欄キャッシュの番号がずれる)
            var hairShadowEditable = _host.IsHairShadowAvailable
                && (!_editGroup.HasValue || ToonMaterialClassifier.UsesHairShadow(_editGroup.Value));
            if (!_host.IsHairShadowAvailable)
            {
                view.DrawLabel("前髪の影のシェーダーを読めないため使えません (BepInEx のログを確認してください)", -1, RowHeight);
            }
            Slider(view, GroupSettingKeys.HairShadow, "前髪の影", shown.hairShadow, 0f, 1f, def.hairShadow, (s, v) => s.hairShadow = v, editable: hairShadowEditable);
            Toggle(view, GroupSettingKeys.HairShadowMode, "前髪の影をライトの向きに", shown.hairShadowMode == HairShadowMode.Light,
                (s, v) => s.hairShadowMode = v ? HairShadowMode.Light : HairShadowMode.Fixed, hairShadowEditable);
            Slider(view, GroupSettingKeys.HairShadowAngle, "前髪の影の角度", shown.hairShadowAngle, 0f, 80f, def.hairShadowAngle, (s, v) => s.hairShadowAngle = v, editable: hairShadowEditable);
```

（`DrawLabel` は入力欄キャッシュを使わないため、条件つきで描いても番号はずれない）

- [ ] **Step 3: ビルド**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 成功、全件 PASS

- [ ] **Step 4: コミット**

```bash
git add source/COM3D25.MToonMaid.Plugin/IMToonMaidHost.cs source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs
git commit -m "feat: ウィンドウに前髪の影の設定を追加"
```

---

### Task 7: 実機検証と文書

**Files:**
- Modify: `README.md`, `docs/superpowers/specs/2026-10-09-mtoon-maid-hair-shadow-design.md`（末尾に「実機検証の結果」）

- [ ] **Step 1: 実機へ反映する**

`com3d25-devbridge:restart-verify` スキルに従う。ゲームを終了する前に、ユーザーへシーンの保存の有無を確認して ledger に書く。ゲーム停止中に `debug.bat` を実行して DLL を反映し、起動してセーブをロードする。BepInEx のログに「前髪の影は使えません」が出ていないことを確かめる（`tail_log`）。

- [ ] **Step 2: 今の設定を控える**

`eval_csharp` でメイド 0 の設定ファイル（`BepInEx/config/COM3D25.MToonMaid/<guid>.xml`）の中身を ledger に写す。

- [ ] **Step 3: 固定の向きで前髪の下に影が出る**

ウィンドウの共通タブで「前髪の影」を 1、角度 30 にして `screenshot` を撮る。前髪の真下（額・まぶた）に前髪の形の影が出て、上下や左右が反転していないことを確かめる。`eval_csharp` で顔のマテリアルの `_HairShadowStrength` が 1、`_HairShadowTex` が `MToonMaid_HairShadow` であることを確かめる。角度を 0 と 80 にして撮り、影が下へ伸び・縮むことを確かめる。

- [ ] **Step 4: 0 に戻すと解放される**

「前髪の影」を 0 にし、`eval_csharp` で `UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.RenderTexture>()` に `MToonMaid_HairShadow` が無いこと、顔の `_HairShadowStrength` が 0 であることを確かめる。

- [ ] **Step 5: ライト追従と主光源の選択**

「前髪の影をライトの向きに」を ON にする。`eval_csharp` で次を行い、各状態で撮る（ライトの元の向き・強さ・色を控えてから変え、最後に戻す）:

1. 主光源（`RenderSettings.sun`、無ければ最も明るい平行光）を顔の右上・左上へ回し、前髪の影が光と反対側へずれることを確かめる
2. sun を赤・左から、別の平行光を青・右からにし、青の強さを sun より大きくする。前髪の影の部分から抜ける色（前髪の影は ForwardBase の陰影だけを下げるため、抜ける色が Unity の選んだ主光源）が、プラグインの選んだ光（sun）と一致するかを見る。一致しなければ `MainDirectionalLightSelector` の sun の優先を外し（`if (l.isSun) return i;` と `isSun` を消し、テスト `sunが候補にあれば…` / `sunでも…` を「最も明るい光を選ぶ」形へ直す）、Task 3 からやり直してコミットする

- [ ] **Step 6: 前髪の揺れ・複数メイド・前髪の無い髪型・負荷**

- モーションを再生し、前髪の影が前髪の揺れについてくることを撮って確かめる（1 フレーム遅れて見えないか）。遅れる・初回に欠ける場合は、`MToonMaidPlugin` の購読を `Camera.onPreCull` から `Camera.onPreRender` へ替えて（フレームに 1 回の見張りはそのまま）もう一度撮る。それでも遅れるなら、spec に「1 フレーム遅れる」と書いて範囲外にし、ユーザーに伝える
- メイドを 2 人にし、それぞれの影が自分の顔にだけ出ることを撮る
- 前髪スロットを外し（`maid.DelProp(MPN.hairf)` などで）、例外が出ず影が消えることを確かめる。戻す
- `profile_add` で `MToonMaidPlugin.OnCameraPreCull` を測り、1 人あたりの時間を記録する
- メイドの「MToon にする」を OFF にし、`MToonMaid_HairShadow` の RenderTexture が残らないことを確かめる。ON に戻す

- [ ] **Step 7: 設定を戻し、文書を書く**

Step 2 で控えた値へ戻す。`README.md` の使い方（「凹凸の影を消す」の行の後）に足す:

```
- 前髪が顔に影を落とすようにするには、顔タブ（または共通タブ）の「前髪の影」を上げます。前髪（前髪スロット）を顔へ投影して、その部分を影側にします。既定は頭の正面から「前髪の影の角度」だけ上から当てた向きで、ライトによらず同じ形の影になります。「前髪の影をライトの向きに」を入れると、顔を照らす平行光の向きから当てます
```

制限に足す:

```
- 前髪の影は前髪スロットだけが落とします（横髪・後ろ髪・帽子は落としません）。目（白目・瞳）には落ちません。ポイント・スポットライトからの前髪の影はありません
```

spec の末尾に「## 実機検証の結果」を足し、Step 3〜6 の結果（日付・セーブ・メイド、各撮影で見えたもの、主光源の選択の結論、計測した時間）を書く。

- [ ] **Step 8: コミット**

```bash
git add README.md docs/superpowers/specs/2026-10-09-mtoon-maid-hair-shadow-design.md
git commit -m "docs: 前髪の影の使い方と実機検証の結果を追加"
```

---

### 仕上げ: コードレビュー

全タスクの後、ユーザーに提示する前に `code-review` スキルでブランチ全体をレビューし、指摘の取捨と修正はそのスキルに従う（executing-plans の Final Review の代わり）。

---

## レビュー却下メモ

- 前髪のキャスターを AlphaMode によらず常に `_MainTex` のαで切り抜く — 2026-10-09 に実機で hairF が `_AlphaMode=0`（不透明）・`_MainTex` なしと確かめた。ゲームでも不透明に描かれる形なので、影も同じ不透明の形が見た目と一致する。Cutout の前髪だけ切り抜く今の判定のままにする
- 主光源の規則を差し替えやすくする — `MainDirectionalLightSelector` の 1 か所に閉じていて対処済み
- PbrCorrect の分岐に前髪の影を掛ける — このプラグインは PbrCorrect を使わない（変換で有効にしない）。要件外
- `Resolve` を毎フレーム呼ぶ複製を避ける — `MaidSettings.Resolve` はグループごとの実体を使い回し、割り当てをしない（項目の写しだけ）。`HairShadowProjection` の小さな配列は影を描くメイドだけで、許容する
