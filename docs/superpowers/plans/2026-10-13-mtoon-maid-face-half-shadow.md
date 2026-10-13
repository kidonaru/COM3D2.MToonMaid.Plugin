# 顔の半面切り替えの影（faceHalfShadow）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 顔グループで「半面切り替えの影」を ON にすると、顔の自分の影が N·L ではなく主光源の水平の角度で「影なし → 光と反対側の半面 → 全面 → 半面 → 影なし」と切り替わり、その間は半面・全面が一様にフェードする。

**Architecture:** シェーダーが `directLightDirection` と、プラグインが毎フレーム入れる頭の中心・正面・頭の上（`_FaceHalfCenter` / `_FaceHalfForward` / `_FaceHalfUp`）から影の濃さ `amount` を出し、ForwardBase の `_ShadowRateToon` とランプの N·L による読み込みの代わりに `faceLit = 1 − amount` を使う。設定は顔専用（`IsGroupOnly`）の 5 項目で、角度・幅・ぼかしは `MaterialConverter.ApplyGroup` が純粋関数 `FaceHalfShadowParams` で丸めてから書く。頭の向きの計算は前髪の影と共有する `HeadFrame` へ移す。

**Tech Stack:** C# (.NET 3.5 / 4.7.1)、xUnit (net48)、Unity ShaderLab/HLSL（Unity 2022.3 と 5.6 でバンドル化）

**Spec:** `docs/superpowers/specs/2026-10-13-face-half-shadow-design.md`

## Global Constraints

- コードのコメント・ログは日本語。コメントは「なぜ他の自然な選択肢を採らなかったか」だけ（`<summary>` は名前から読めない契約に限り What 可）。周りのコードのコメント密度・書き方に合わせる
- 設定キー文字列は設定ファイルに書くので変えない: `faceHalfShadow` / `faceHalfAngle` / `faceFullAngle` / `faceHalfFadeWidth` / `faceHalfSoftness`
- 既定: `faceHalfShadow` false、`faceHalfAngle` 45（0〜180 度）、`faceFullAngle` 135（0〜180 度）、`faceHalfFadeWidth` 30（0〜90 度）、`faceHalfSoftness` 0.005（0〜0.02 m、UI は cm で 0〜2）
- 仰角で薄める範囲 60°〜90° はシェーダーの定数
- `MaidSettings.CurrentVersion` は上げない（要素の無いファイルはフィールド初期値で読む）
- ビルド確認は dotnet build（ゲームフォルダへ反映しない）。`debug.bat` / `deploy.*` は実行しない
  - net471: `dotnet build source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj -c Debug -f net471 -p:COM3D25_DIR=W:/COM3D2_5`
  - net35: `dotnet build source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj -c Debug -f net35 -p:COM3D2_DIR=W:/COM3D2`
- テスト（Git Bash）: `MSYS_NO_PATHCONV=1 dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:/COM3D2_5`
- シェーダーの変更後は `build-bundle.bat all` で `Resources/mtoon_bundle` と `mtoon_bundle_com3d2` を作り直す（Unity エディタで UnityProject2022 を開いているとロックで失敗する。5.6 側は UnityProject2022/Assets を robocopy してから作る）
- git worktree は使わない。ブランチ `feat/flat-shadow-ramp` のまま進める

## Review Focus

- 光が真正面・真後ろをまたぐ（L·R の符号が変わる）とき、左右の半面の影が入れ替わって飛ばないこと（半面角が小さい・全面角が大きい設定でも） → Task 2 のフェード幅の縮めのテストと Task 6 の Step 4（既定値と、半面 10°・全面 170°・幅 30° で 0°・180° の前後 ±5° を撮る）
- 光が真上・真下、または平行光が無い（`directLightDirection` が normalize(0) の NaN）とき、顔が真っ黒・真っ白にならず影なしになること → Task 3 のシェーダーのガードと Task 6 の Step 5
- 手編集の設定ファイルで全面角 < 半面角、NaN・無限大、範囲外の値でも、シェーダーへ有限で順序の正しい値が入ること → Task 2 のテスト
- OFF に戻す、または頭が見つからない（head スロットを MToon の対象外にした等）とき、今の N·L の影へ戻ること → Task 4 の `UpdateFaceHalfShadow` の 0 書き戻しと Task 6 の Step 6
- 「ゲームの陰影」OFF（ランプを読まない）の顔でも ON が効き、影色が `_ShadowTex × _ShadeColor` になること → Task 6 の Step 7

---

### Task 1: 設定項目（5 キー・顔専用）

**Files:**
- Modify: `source/COM3D2.MToonMaid.Plugin/Settings/GroupSettings.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/ToonMaterialClassifier.cs`
- Modify: `UnityInjector/Config/MToonMaid/Presets/デフォルト.xml`
- Test: `source/COM3D2.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`

**Interfaces:**
- Produces:
  - `GroupSettingKeys.FaceHalfShadow` / `FaceHalfAngle` / `FaceFullAngle` / `FaceHalfFadeWidth` / `FaceHalfSoftness`（string 定数）
  - `GroupSettings.faceHalfShadow`（bool）、`faceHalfAngle` / `faceFullAngle` / `faceHalfFadeWidth` / `faceHalfSoftness`（float）
  - `GroupSettings.MaxFaceHalfAngle = 180f`、`MaxFaceHalfFadeWidth = 90f`、`MaxFaceHalfSoftness = 0.02f`
  - `ToonMaterialClassifier.UsesFaceHalfShadow(PartGroup group)`（顔だけ true）

- [ ] **Step 1: 失敗するテストを書く**

`CommonSettingsTests.cs` の `ChangeAll` の末尾（`g.flatShadowRamp = true;` の後）に足す:

```csharp
            g.faceHalfShadow = true;
            g.faceHalfAngle = 30f;
            g.faceFullAngle = 150f;
            g.faceHalfFadeWidth = 10f;
            g.faceHalfSoftness = 0.01f;
```

`前髪の影は既定で濃さ1固定右へ4mmずらし` の `Assert.Equal(26, GroupSettingKeys.All.Length);` を `Assert.Equal(31, GroupSettingKeys.All.Length);` に変える。

`影側を一色にの要素が無いファイルはOFFで読む` の後にテストを足す:

```csharp
        private static readonly string[] FaceHalfKeys =
        {
            GroupSettingKeys.FaceHalfShadow, GroupSettingKeys.FaceHalfAngle, GroupSettingKeys.FaceFullAngle,
            GroupSettingKeys.FaceHalfFadeWidth, GroupSettingKeys.FaceHalfSoftness,
        };

        [Fact]
        public void 半面切り替えの影は既定でOFFの顔専用の項目()
        {
            foreach (var key in FaceHalfKeys)
            {
                Assert.Contains(key, GroupSettingKeys.All);
                Assert.True(GroupSettingKeys.IsGroupOnly(key));
                Assert.True(GroupSettingKeys.AppliesTo(key, PartGroup.Face));
                Assert.False(GroupSettingKeys.AppliesTo(key, PartGroup.Skin));
                Assert.False(GroupSettingKeys.AppliesTo(key, PartGroup.Hair));
                Assert.False(GroupSettingKeys.AppliesTo(key, PartGroup.Costume));
            }
            var def = GroupSettings.CreateDefault();
            Assert.False(def.faceHalfShadow);
            Assert.Equal(45f, def.faceHalfAngle);
            Assert.Equal(135f, def.faceFullAngle);
            Assert.Equal(30f, def.faceHalfFadeWidth);
            Assert.Equal(0.005f, def.faceHalfSoftness);
            Assert.Equal(180f, GroupSettings.MaxFaceHalfAngle);
            Assert.Equal(90f, GroupSettings.MaxFaceHalfFadeWidth);
            Assert.Equal(0.02f, GroupSettings.MaxFaceHalfSoftness);
        }

        [Fact]
        public void 半面切り替えの影は共通の値を継承しない()
        {
            var s = Filled();
            // 共通タブには出ないが、手編集で共通に値があっても顔へ流れないこと
            s.common.faceHalfShadow = true;
            s.common.faceHalfAngle = 10f;

            Assert.False(s.Resolve(PartGroup.Face).faceHalfShadow);
            Assert.Equal(45f, s.Resolve(PartGroup.Face).faceHalfAngle);
        }

        [Fact]
        public void 半面切り替えの影を保存して読み直せる()
        {
            var path = Path.Combine(_dir, "facehalf.xml");
            var s = Filled();
            foreach (var key in FaceHalfKeys)
            {
                s.SetOverride(PartGroup.Face, key, true);
            }
            s.face.faceHalfShadow = true;
            s.face.faceHalfAngle = 50f;
            s.face.faceFullAngle = 120f;
            s.face.faceHalfFadeWidth = 0f;
            s.face.faceHalfSoftness = 0.012f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            var face = loaded.Resolve(PartGroup.Face);
            Assert.True(face.faceHalfShadow);
            Assert.Equal(50f, face.faceHalfAngle);
            Assert.Equal(120f, face.faceFullAngle);
            Assert.Equal(0f, face.faceHalfFadeWidth);
            Assert.Equal(0.012f, face.faceHalfSoftness);
            Assert.False(loaded.Resolve(PartGroup.Skin).faceHalfShadow);
        }

        [Fact]
        public void 半面切り替えの影の要素が無いファイルは既定で読む()
        {
            var path = Path.Combine(_dir, "old-facehalf.xml");
            MaidSettingsStore.Save(path, Filled());
            var xml = File.ReadAllText(path);
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"\s*<(faceHalf\w*|faceFullAngle)>[^<]*</\1>", "");
            Assert.DoesNotContain("faceHalf", xml);
            Assert.DoesNotContain("faceFullAngle", xml);
            File.WriteAllText(path, xml);

            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            var face = loaded.Resolve(PartGroup.Face);
            Assert.False(face.faceHalfShadow);
            Assert.Equal(45f, face.faceHalfAngle);
            Assert.Equal(135f, face.faceFullAngle);
            Assert.Equal(30f, face.faceHalfFadeWidth);
            Assert.Equal(0.005f, face.faceHalfSoftness);
        }
```

- [ ] **Step 2: テストが失敗する（コンパイルエラー）ことを確かめる**

Run: `MSYS_NO_PATHCONV=1 dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:/COM3D2_5`
Expected: FAIL（`GroupSettingKeys.FaceHalfShadow` などが無いコンパイルエラー）

- [ ] **Step 3: 実装する**

`GroupSettings.cs` の `castShadows` の後（`MaxHairShadowOffset` の前）に足す:

```csharp
        // 顔の自分の影を N·L ではなく主光源の水平の角度で「影なし → 光と反対側の半面 → 全面」と切り替える。顔にだけ効く
        public bool faceHalfShadow;
        // 頭の正面から光までの水平の角度 (度) が、これを超えると半面・全面が影になる
        public float faceHalfAngle = 45f;
        public float faceFullAngle = 135f;
        // 切り替えをなめらかにする角度の幅 (度)。0 で一瞬で切り替わる
        public float faceHalfFadeWidth = 30f;
        // 左右の半面の境目のぼかしの幅 (m)
        public float faceHalfSoftness = 0.005f;
```

定数の並び（`MaxShadeStrength` の後）に足す:

```csharp
        public const float MaxFaceHalfAngle = 180f;
        public const float MaxFaceHalfFadeWidth = 90f;
        public const float MaxFaceHalfSoftness = 0.02f;
```

`CopyField` の `case GroupSettingKeys.CastShadows: ...` の後に足す:

```csharp
                case GroupSettingKeys.FaceHalfShadow: faceHalfShadow = from.faceHalfShadow; break;
                case GroupSettingKeys.FaceHalfAngle: faceHalfAngle = from.faceHalfAngle; break;
                case GroupSettingKeys.FaceFullAngle: faceFullAngle = from.faceFullAngle; break;
                case GroupSettingKeys.FaceHalfFadeWidth: faceHalfFadeWidth = from.faceHalfFadeWidth; break;
                case GroupSettingKeys.FaceHalfSoftness: faceHalfSoftness = from.faceHalfSoftness; break;
```

`GroupSettingKeys.cs`:
- `CastShadows` の定数の後に:

```csharp
        public const string FaceHalfShadow = "faceHalfShadow";
        public const string FaceHalfAngle = "faceHalfAngle";
        public const string FaceFullAngle = "faceFullAngle";
        public const string FaceHalfFadeWidth = "faceHalfFadeWidth";
        public const string FaceHalfSoftness = "faceHalfSoftness";
```

- `All` の `ReceiveShadows, CastShadows,` の行の後に `FaceHalfShadow, FaceHalfAngle, FaceFullAngle, FaceHalfFadeWidth, FaceHalfSoftness,` を足す
- `IsGroupOnly` の `case HighlightScale:` の前に 5 つの `case` を足す（`return true` の群）
- `AppliesTo` に足す:

```csharp
                case FaceHalfShadow:
                case FaceHalfAngle:
                case FaceFullAngle:
                case FaceHalfFadeWidth:
                case FaceHalfSoftness:
                    return ToonMaterialClassifier.UsesFaceHalfShadow(group);
```

`ToonMaterialClassifier.cs` の `UsesRendererShadow` の後に足す:

```csharp
        /// <summary>半面切り替えの影を出すグループ。頭の中心と正面で影を決めるため顔だけ</summary>
        public static bool UsesFaceHalfShadow(PartGroup group)
        {
            return group == PartGroup.Face;
        }
```

`デフォルト.xml` の 5 つの `<castShadows>` 行（common・skin・face・hair・costume）の後に既定値の要素を足す（保存ファイルと同じ並び。CRLF を保つ）:

```bash
python - <<'EOF'
import re
p = 'UnityInjector/Config/MToonMaid/Presets/デフォルト.xml'
s = open(p, encoding='utf-8', newline='').read()
def add(m):
    ind = m.group(1)
    names = [('faceHalfShadow', 'false'), ('faceHalfAngle', '45'), ('faceFullAngle', '135'),
             ('faceHalfFadeWidth', '30'), ('faceHalfSoftness', '0.005')]
    return m.group(0) + ''.join('\r\n%s<%s>%s</%s>' % (ind, n, v, n) for n, v in names)
s, n = re.subn(r'( *)<castShadows>\w+</castShadows>', add, s)
assert n == 5, n
open(p, 'w', encoding='utf-8', newline='').write(s)
EOF
git diff --stat -- UnityInjector/Config/MToonMaid/Presets/デフォルト.xml
```

Expected: `25 insertions(+)`、BOM は付かない（`head -c 3` が `<?x`）

- [ ] **Step 4: テストが通ることを確かめる**

Run: `MSYS_NO_PATHCONV=1 dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:/COM3D2_5`
Expected: PASS（全件。`部位専用の項目は一部のグループにだけ出し他の項目は全グループに出す`・`ChangeAll` を使う全項目のテスト・`MaidPresetTests` も通る）

- [ ] **Step 5: コミット**

```bash
git add source/COM3D2.MToonMaid.Plugin/Settings/GroupSettings.cs source/COM3D2.MToonMaid.Plugin/Settings/GroupSettingKeys.cs \
  source/COM3D2.MToonMaid.Plugin/ToonMaterialClassifier.cs "UnityInjector/Config/MToonMaid/Presets/デフォルト.xml" \
  source/COM3D2.MToonMaid.Plugin.Tests/CommonSettingsTests.cs
git commit -m "feat(settings): 顔の半面切り替えの影の設定 5 項目を足す" -m "顔が N·L で立体的に陰るのを、光の水平の角度で半面・全面を切り替えるアニメ塗りにできるようにするため。
顔専用の項目で共通から継承しない。既定 OFF なので今の見た目は変わらない。"
```

（コミットメッセージの末尾にセッションの attribution 行を付ける）

---

### Task 2: シェーダーへ書く値の変換と ApplyGroup

**Files:**
- Create: `source/COM3D2.MToonMaid.Plugin/Rendering/FaceHalfShadowParams.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Rendering/MToonProps.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Rendering/MaterialConverter.cs`（`ApplyGroup`）
- Test: `source/COM3D2.MToonMaid.Plugin.Tests/FaceHalfShadowParamsTests.cs`

**Interfaces:**
- Consumes: Task 1 の `GroupSettings.faceHalf*` / `Max*`、`ToonMaterialClassifier.UsesFaceHalfShadow`
- Produces:
  - `FaceHalfShadowParams.Enabled(bool faceHalfShadow, PartGroup group) : bool`
  - `FaceHalfShadowParams.HalfAngle(float degrees) : float`（ラジアン）
  - `FaceHalfShadowParams.FullAngle(float halfDegrees, float fullDegrees) : float`（ラジアン、半面角以上）
  - `FaceHalfShadowParams.FadeWidth(float halfDegrees, float fullDegrees, float widthDegrees) : float`（ラジアン。θ=0 と θ=180° で半面・全面の濃さがそろうよう、2×半面角・2×(180° − 全面角) 以下に縮める）
  - `FaceHalfShadowParams.Softness(float meters) : float`（m）
  - `FaceHalfShadowParams.CentimetersPerMeter = 100f`（UI の cm 表示用）
  - `MToonProps.FaceHalfShadow` / `FaceHalfAngle` / `FaceFullAngle` / `FaceHalfFadeWidth` / `FaceHalfSoftness` / `FaceHalfCenter` / `FaceHalfForward` / `FaceHalfUp`

- [ ] **Step 1: 失敗するテストを書く**

`FaceHalfShadowParamsTests.cs`:

```csharp
using System;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class FaceHalfShadowParamsTests
    {
        private const double Deg = Math.PI / 180.0;

        [Theory]
        [InlineData(true, PartGroup.Face, true)]
        [InlineData(false, PartGroup.Face, false)]
        [InlineData(true, PartGroup.Skin, false)]
        [InlineData(true, PartGroup.Hair, false)]
        [InlineData(true, PartGroup.Costume, false)]
        public void 顔のグループでONのときだけ有効にする(bool on, PartGroup group, bool expected)
        {
            Assert.Equal(expected, FaceHalfShadowParams.Enabled(on, group));
        }

        [Theory]
        [InlineData(45f, 45.0)]
        [InlineData(0f, 0.0)]
        [InlineData(180f, 180.0)]
        [InlineData(-10f, 0.0)]
        [InlineData(200f, 180.0)]
        [InlineData(float.NaN, 45.0)]
        [InlineData(float.PositiveInfinity, 45.0)]
        public void 半面の角度はラジアンにし0から180度へ丸め非有限は既定にする(float degrees, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.HalfAngle(degrees), 5);
        }

        [Theory]
        [InlineData(45f, 135f, 135.0)]
        [InlineData(90f, 60f, 90.0)]
        [InlineData(45f, float.NaN, 135.0)]
        [InlineData(150f, float.NaN, 150.0)]
        [InlineData(float.NaN, 30f, 45.0)]
        [InlineData(45f, 300f, 180.0)]
        public void 全面の角度は半面の角度より小さくしない(float half, float full, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.FullAngle(half, full), 5);
        }

        [Theory]
        [InlineData(30f, 30.0)]
        [InlineData(0f, 0.0)]
        [InlineData(120f, 90.0)]
        [InlineData(-5f, 0.0)]
        [InlineData(float.NaN, 30.0)]
        public void フェード幅はラジアンにし0から90度へ丸める(float degrees, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.FadeWidth(90f, 90f, degrees), 5);
        }

        // 正面 (θ=0) で光と反対側の半面が少しでも影だと、光が正面をまたいで左右が入れ替わる瞬間に影が飛ぶ
        [Theory]
        [InlineData(10f, 135f, 30f, 20.0)]
        [InlineData(0f, 135f, 30f, 0.0)]
        [InlineData(45f, 135f, 30f, 30.0)]
        public void フェード幅は半面の角度の2倍までに縮める(float half, float full, float width, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.FadeWidth(half, full, width), 5);
        }

        // 真後ろ (θ=180°) で全面が濃さ 1 に届かないと、光が真後ろをまたぐ瞬間に影が飛ぶ
        [Theory]
        [InlineData(45f, 170f, 30f, 20.0)]
        [InlineData(45f, 180f, 30f, 0.0)]
        [InlineData(90f, 60f, 90f, 90.0)]
        public void フェード幅は180度から全面の角度までの2倍までに縮める(float half, float full, float width, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.FadeWidth(half, full, width), 5);
        }

        [Theory]
        [InlineData(0.005f, 0.005f)]
        [InlineData(0f, 0f)]
        [InlineData(0.05f, 0.02f)]
        [InlineData(-1f, 0f)]
        [InlineData(float.NaN, 0.005f)]
        [InlineData(float.NegativeInfinity, 0.005f)]
        public void ぼかしは0から2cmへ丸め非有限は既定にする(float meters, float expected)
        {
            Assert.Equal(expected, FaceHalfShadowParams.Softness(meters), 6);
        }
    }
}
```

- [ ] **Step 2: テストが失敗する（コンパイルエラー）ことを確かめる**

Run: `MSYS_NO_PATHCONV=1 dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:/COM3D2_5`
Expected: FAIL（`FaceHalfShadowParams` が無い）

- [ ] **Step 3: 実装する**

`FaceHalfShadowParams.cs`:

```csharp
using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>半面切り替えの影の設定値を、シェーダーへ書く値 (角度・幅はラジアン、ぼかしは m) にする</summary>
    public static class FaceHalfShadowParams
    {
        public const float CentimetersPerMeter = 100f;
        private const float DegreesToRadians = (float)(Math.PI / 180.0);
        // 手編集で壊れた値の戻し先。書き換えない
        private static readonly GroupSettings Defaults = new GroupSettings();

        /// <summary>_FaceHalfShadow を 1 にするか</summary>
        public static bool Enabled(bool faceHalfShadow, PartGroup group)
        {
            // 手編集で顔以外を個別に ON にしても、頭の中心で左右を分ける影は顔以外の形に合わない
            return faceHalfShadow && ToonMaterialClassifier.UsesFaceHalfShadow(group);
        }

        public static float HalfAngle(float degrees)
        {
            return ClampFinite(degrees, GroupSettings.MaxFaceHalfAngle, Defaults.faceHalfAngle) * DegreesToRadians;
        }

        /// <summary>全面になる角度。半面になる角度より小さいときは半面になる角度にそろえる</summary>
        public static float FullAngle(float halfDegrees, float fullDegrees)
        {
            // 全面が半面より先に始まると、光側の半面だけが影になり光と反対側が明るい逆の見た目になる
            var half = ClampFinite(halfDegrees, GroupSettings.MaxFaceHalfAngle, Defaults.faceHalfAngle);
            var full = ClampFinite(fullDegrees, GroupSettings.MaxFaceHalfAngle, Defaults.faceFullAngle);
            return Math.Max(half, full) * DegreesToRadians;
        }

        /// <summary>フェードの幅。正面と真後ろで左右が入れ替わっても影が飛ばないよう、半面は正面より、全面は真後ろより内側でフェードさせる</summary>
        public static float FadeWidth(float halfDegrees, float fullDegrees, float widthDegrees)
        {
            var half = ClampFinite(halfDegrees, GroupSettings.MaxFaceHalfAngle, Defaults.faceHalfAngle);
            var full = Math.Max(half, ClampFinite(fullDegrees, GroupSettings.MaxFaceHalfAngle, Defaults.faceFullAngle));
            var width = ClampFinite(widthDegrees, GroupSettings.MaxFaceHalfFadeWidth, Defaults.faceHalfFadeWidth);
            return Math.Min(width, Math.Min(2f * half, 2f * (GroupSettings.MaxFaceHalfAngle - full))) * DegreesToRadians;
        }

        public static float Softness(float meters)
        {
            return ClampFinite(meters, GroupSettings.MaxFaceHalfSoftness, Defaults.faceHalfSoftness);
        }

        private static float ClampFinite(float value, float max, float fallback)
        {
            // Math.Min / Max だけだと NaN を通し、シェーダーの smoothstep が NaN になって顔が真っ黒になる
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }
            return Math.Max(0f, Math.Min(max, value));
        }
    }
}
```

`MToonProps.cs` の「前髪の影」の群の後に足す:

```csharp
        // 顔の半面切り替えの影 (顔の MToon が読む)。中心・正面・頭の上はプラグインが毎フレーム入れる
        public const string FaceHalfShadow = "_FaceHalfShadow";
        public const string FaceHalfAngle = "_FaceHalfAngle";
        public const string FaceFullAngle = "_FaceFullAngle";
        public const string FaceHalfFadeWidth = "_FaceHalfFadeWidth";
        public const string FaceHalfSoftness = "_FaceHalfSoftness";
        public const string FaceHalfCenter = "_FaceHalfCenter";
        public const string FaceHalfForward = "_FaceHalfForward";
        public const string FaceHalfUp = "_FaceHalfUp";
```

`MaterialConverter.ApplyGroup` の `m.SetFloat(MToonProps.FlatShadowRamp, ...)` の後に足す:

```csharp
            m.SetFloat(MToonProps.FaceHalfShadow, FaceHalfShadowParams.Enabled(settings.faceHalfShadow, backup.group) ? 1f : 0f);
            m.SetFloat(MToonProps.FaceHalfAngle, FaceHalfShadowParams.HalfAngle(settings.faceHalfAngle));
            m.SetFloat(MToonProps.FaceFullAngle, FaceHalfShadowParams.FullAngle(settings.faceHalfAngle, settings.faceFullAngle));
            m.SetFloat(MToonProps.FaceHalfFadeWidth, FaceHalfShadowParams.FadeWidth(settings.faceHalfAngle, settings.faceFullAngle, settings.faceHalfFadeWidth));
            m.SetFloat(MToonProps.FaceHalfSoftness, FaceHalfShadowParams.Softness(settings.faceHalfSoftness));
```

- [ ] **Step 4: テストとビルドが通ることを確かめる**

Run: `MSYS_NO_PATHCONV=1 dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:/COM3D2_5`
Expected: PASS
Run: net35 のビルド（Global Constraints のコマンド）
Expected: エラー 0

- [ ] **Step 5: コミット**

```bash
git add source/COM3D2.MToonMaid.Plugin/Rendering/FaceHalfShadowParams.cs source/COM3D2.MToonMaid.Plugin/Rendering/MToonProps.cs \
  source/COM3D2.MToonMaid.Plugin/Rendering/MaterialConverter.cs source/COM3D2.MToonMaid.Plugin.Tests/FaceHalfShadowParamsTests.cs
git commit -m "feat(rendering): 半面切り替えの影の角度とぼかしを顔のマテリアルへ書く" -m "手編集の範囲外・NaN でシェーダーが NaN にならないよう、丸めてから書く。"
```

---

### Task 3: シェーダー（半面・全面の濃さと差し替え）

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl`
- Rebuild: `source/COM3D2.MToonMaid.Plugin/Resources/mtoon_bundle`、`mtoon_bundle_com3d2`

**Interfaces:**
- Consumes: Task 2 の MToonProps のプロパティ名（シェーダーで同名に宣言する）
- Produces: `_FaceHalfForward` が 0 の間は今と同じ描画（Task 4 が毎フレーム値を入れる）

- [ ] **Step 1: プロパティを足す**

`vrmc_materials_mtoon.shader` の先頭の改変一覧で `_FlatShadowRamp` の行の後に足す:

```
// - _FaceHalfShadow が 1 で頭の向き (_FaceHalfForward) が入っている間、ForwardBase の自分の影を N·L ではなく主光源の水平の角度で「影なし → 光と反対側の半面 → 全面」と切り替える
```

Properties の `_HairShadowSoftness` の行の後に足す:

```
        // COM3D2: 顔の半面切り替えの影。角度・幅はラジアン、ぼかしは m (プラグインが設定する)
        _FaceHalfShadow ("Face Half Shadow", Float) = 0
        _FaceHalfAngle ("Face Half Angle (rad)", Float) = 0.7853982
        _FaceFullAngle ("Face Full Angle (rad)", Float) = 2.3561945
        _FaceHalfFadeWidth ("Face Half Fade Width (rad)", Float) = 0.5235988
        _FaceHalfSoftness ("Face Half Softness (m)", Float) = 0.005
        // COM3D2: 頭の中心・正面・頭の上 (ワールド)。正面が 0 の間は N·L の影で描く (プラグインが毎フレーム設定する)
        _FaceHalfCenter ("Face Half Center", Vector) = (0, 0, 0, 0)
        _FaceHalfForward ("Face Half Forward", Vector) = (0, 0, 0, 0)
        _FaceHalfUp ("Face Half Up", Vector) = (0, 0, 0, 0)
```

`vrmc_materials_mtoon_input.hlsl` の CBUFFER の `float4 _ScleraRect;` の後に:

```hlsl
float4 _FaceHalfCenter;
float4 _FaceHalfForward;
float4 _FaceHalfUp;
```

`half _FlatShadowRamp;` の後に:

```hlsl
half _FaceHalfShadow;
float _FaceHalfAngle;
float _FaceFullAngle;
float _FaceHalfFadeWidth;
float _FaceHalfSoftness;
```

- [ ] **Step 2: 濃さの関数を足す**

`vrmc_materials_mtoon_lighting_mtoon.hlsl` の `GetMToonLighting_ToonRampLit` の後（ランプの関数の前のコメントブロックより前）に足す:

```hlsl
// COM3D2: 顔の半面切り替えの影。仰角 60°〜90° で影を薄めて消す (ラジアン)
#define MTOON_FACE_HALF_ELEVATION_FADE_START 1.0471976
#define MTOON_FACE_HALF_ELEVATION_FADE_END 1.5707963

// 幅 0 で smoothstep にすると 0 除算になる
inline half MToon_SmoothStepOrStep(const float edge0, const float edge1, const float x)
{
    return edge1 > edge0 ? smoothstep(edge0, edge1, x) : step(edge0, x);
}

inline bool MToon_IsFaceHalfShadowOn()
{
    return _FaceHalfShadow > 0.5 && dot(_FaceHalfForward.xyz, _FaceHalfForward.xyz) >= 1e-6;
}

// COM3D2: 半面切り替えの影の光側の割合 (1 で影なし)。
// 主光源の向きをプラグインから角度で渡すと、前髪の影で起きたように、プラグインが選ぶライトとシェーダーを照らすライトがずれる。
// 半面を法線の向きで分けると、鼻や頬の凹凸で境目がまだらになる。頭の中心を通る左右の境目の面からの距離で分ける
inline half GetMToonLighting_FaceHalfLit(const UnityLighting lighting, const MToonInput input)
{
    const float3 l = lighting.directLightDirection;
    // 平行光が無いと光の向きが normalize(0) の NaN になり、そのまま角度を出すと顔が真っ黒になる (NaN との比較は false)
    if (!(dot(l, l) > 0.5))
    {
        return 1;
    }
    const float3 forward = normalize(_FaceHalfForward.xyz);
    const float3 up = normalize(_FaceHalfUp.xyz);
    const float3 right = cross(up, forward);
    const float lf = dot(l, forward);
    const float lr = dot(l, right);
    const float horizontal = sqrt(lf * lf + lr * lr);
    // 真上・真下の光で atan2(0, 0) にすると NaN になりうる (そのとき仰角で影は消えるので値は使わない)
    const float theta = horizontal > 1e-5 ? atan2(abs(lr), lf) : 0;
    const float elevation = atan2(abs(dot(l, up)), max(horizontal, 1e-5));
    const half k = 1 - smoothstep(MTOON_FACE_HALF_ELEVATION_FADE_START, MTOON_FACE_HALF_ELEVATION_FADE_END, elevation);
    const float halfWidth = _FaceHalfFadeWidth * 0.5;
    // near / far は処理系によって予約語扱いされうるため名前に使わない
    const half farSide = MToon_SmoothStepOrStep(_FaceHalfAngle - halfWidth, _FaceHalfAngle + halfWidth, theta) * k;
    const half nearSide = MToon_SmoothStepOrStep(_FaceFullAngle - halfWidth, _FaceFullAngle + halfWidth, theta) * k;
    const float side = dot(input.positionWS - _FaceHalfCenter.xyz, right) * sign(lr);
    const half t = MToon_SmoothStepOrStep(-_FaceHalfSoftness, _FaceHalfSoftness, side);
    return 1 - lerp(farSide, nearSide, t);
}
```

- [ ] **Step 3: ランプと陰影の割合を差し替える**

`GetMToonLighting_ToonRamp` を次にする（引数に `faceHalf` / `faceLit` を足す）:

```hlsl
inline half3 GetMToonLighting_ToonRamp(const UnityLighting lighting, const MToonInput input, const half dotNL, const half hairShadow, const half shadeStrength, const bool faceHalf, const half faceLit)
{
    const float2 uv = MToon_ClampToTexels(GetMToonLighting_ToonUv(dotNL), _ToonRamp_TexelSize);
    // 半面切り替えの影でランプを N·L の位置で読むと、光側の境目近くのグラデーションが顔に残る
    const half3 ramp = faceHalf ? GetMToonLighting_ToonRampLit() : _ToonRamp.SampleLevel(sampler_ShadowRateToon, uv, 0).rgb;
    const half castShadow = MToon_IsOutlinePass() ? 1 : lighting.directLightAttenuation.x;
    const half shadowRate = faceHalf ? faceLit : GetMToonLighting_ShadowRate(dotNL);
    // 影側を常に一色にすると、ゲームの Toony と同じく影の中もランプで陰影を付けたい衣装・髪までグラデーションが消える。
    // 半面切り替えの影で一色にしないと、光側の色で読んだランプのまま影側が暗くならない
    const half flatShadowRamp = faceHalf ? 1 : _FlatShadowRamp;
    const half unshadowed = lerp(1, castShadow * (1 - hairShadow), shadowRate) * lerp(1, shadowRate, flatShadowRamp);
    return max(0, lerp(GetMToonLighting_ToonRampLit(), lerp(_ToonRampDarkColor.rgb, ramp, unshadowed), shadeStrength));
}
```

`GetMToonLighting_Shade` の引数に `const bool faceHalf, const half faceLit` を足し、ForwardBase の分岐の `toonShade` を次にする:

```hlsl
        // [branch] の if で OFF のときの読み込みを省こうとすると、シェーダーコンパイラが内部エラーで止まる
        const half toonShade = faceHalf
            ? faceLit
            : _UseShadowRateToon > 0.5
                ? GetMToonLighting_ShadowRate(dotNL)
                : mtoon_linearstep(-1.0 + shadeToony, +1.0 - shadeToony, shadeInput + shadeShift);
```

`GetMToonLighting` の `hairShadow` の行の後に足し、2 つの呼び出しへ渡す:

```hlsl
    // COM3D2: 追加ライト (ForwardAdd) は N·L の影のまま
    const bool faceHalf = MToon_IsForwardBasePass() && MToon_IsFaceHalfShadowOn();
    const half faceLit = faceHalf ? GetMToonLighting_FaceHalfLit(unityLight, input) : 1;
```

```hlsl
    const half shade = lerp(1, GetMToonLighting_Shade(unityLight, input, dotNL, hairShadow, faceHalf, faceLit), min(shadeStrength, 1));
```

```hlsl
    const half3 rampColor = MToon_IsForwardBasePass() && _UseToonRamp > 0.5 ? GetMToonLighting_ToonRamp(unityLight, input, dotNL, hairShadow, shadeStrength, faceHalf, faceLit) : half3(1, 1, 1);
```

- [ ] **Step 4: バンドルを作り直す**

Run: `cmd //c build-bundle.bat all`
Expected: 終了コード 0、`Shader error` の出力なし（2022 と 5.6 の両方）。`git status` で `mtoon_bundle` と `mtoon_bundle_com3d2` が変わっている

- [ ] **Step 5: コミット**

```bash
git add UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl \
  UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl \
  source/COM3D2.MToonMaid.Plugin/Resources/mtoon_bundle source/COM3D2.MToonMaid.Plugin/Resources/mtoon_bundle_com3d2
git commit -m "feat(shader): 顔の影を光の水平の角度で半面・全面に切り替える _FaceHalfShadow を足す" -m "N·L の影は境界が顔の上を動いて立体的に見えるため。主光源の向きはシェーダーの directLightDirection から取る。
シェーダーを変えたので mtoon_bundle / mtoon_bundle_com3d2 を build-bundle.bat all で作り直した。"
```

---

### Task 4: 頭の向きの共有と毎フレームの書き込み

**Files:**
- Create: `source/COM3D2.MToonMaid.Plugin/Rendering/HeadFrame.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Rendering/HairShadowRenderer.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/MaidMToonController.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/MToonMaidPlugin.cs`（`OnCameraPreCull`）

**Interfaces:**
- Consumes: Task 2 の `MToonProps.FaceHalfCenter` / `FaceHalfForward` / `FaceHalfUp`、Task 1 の `GroupSettings.faceHalfShadow`
- Produces:
  - `HeadFrame.FaceForward(Transform) / HeadTop(Transform) / MaidRight(Transform) : Vector3`
  - `HeadFrame.FindHeadRenderer(TBody) : SkinnedMeshRenderer`（無ければ null）
  - `HeadFrame.BoundsSpace(SkinnedMeshRenderer) : Transform`、`HeadFrame.Center(SkinnedMeshRenderer) : Vector3`
  - `MaidMToonController.UpdateFaceHalfShadow(GroupSettings face)`

このタスクは Unity の型に依存するためユニットテストは無い。前髪の影は振る舞いを変えない移動だけで、実機で Task 6 に確かめる。

- [ ] **Step 1: HeadFrame を作る**

`HeadFrame.cs`:

```csharp
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 頭の向きと中心 (ワールド)。COM3D2 の Bip01 Head は顔の正面が up、頭のてっぺんが -right、メイドから見て右が -forward
    /// </summary>
    public static class HeadFrame
    {
        public static Vector3 FaceForward(Transform head)
        {
            return head.up;
        }

        public static Vector3 HeadTop(Transform head)
        {
            return -head.right;
        }

        public static Vector3 MaidRight(Transform head)
        {
            return -head.forward;
        }

        /// <summary>head スロットの SkinnedMeshRenderer。無ければ null</summary>
        public static SkinnedMeshRenderer FindHeadRenderer(TBody body)
        {
            var skin = body != null ? body.GetSlot(ToonMaterialClassifier.HeadSlot) : null;
            return skin != null && skin.obj != null ? skin.obj.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        }

        /// <summary>SkinnedMeshRenderer の localBounds はルートボーンの空間にある</summary>
        public static Transform BoundsSpace(SkinnedMeshRenderer r)
        {
            return r.rootBone != null ? r.rootBone : r.transform;
        }

        /// <summary>head の Renderer のバウンズの中心</summary>
        public static Vector3 Center(SkinnedMeshRenderer r)
        {
            // ワールドの AABB (Renderer.bounds) は頭の回転で大きさが変わり、中心も揺れる
            return BoundsSpace(r).TransformPoint(r.localBounds.center);
        }
    }
}
```

- [ ] **Step 2: HairShadowRenderer を HeadFrame へ寄せる**

`HairShadowRenderer.cs`:
- `private const string HeadSlot = "head";` を消す
- `Render` の `var headRenderer = FindRenderer(body, HeadSlot);` を `var headRenderer = HeadFrame.FindHeadRenderer(body);` にする
- `var boundsSpace = BoundsSpace(headRenderer);` を `var boundsSpace = HeadFrame.BoundsSpace(headRenderer);` にする
- `var center = boundsSpace.TransformPoint(headRenderer.localBounds.center);` を `var center = HeadFrame.Center(headRenderer);` にする（直前の「頭のボーンの原点は首寄りにあるため…」のコメントは残す）
- `HairShadowProjection.Create(..., ToArray(HeadTop(head)), ToArray(FaceForward(head)), radius)` を `HeadFrame.HeadTop(head)` / `HeadFrame.FaceForward(head)` にする
- `ToLight` の `FaceForward(head)` を `HeadFrame.FaceForward(head)` にする
- `WorldOffset` の `MaidRight(head)` / `HeadTop(head)` を `HeadFrame.MaidRight(head)` / `HeadFrame.HeadTop(head)` にする
- 「COM3D2 の Bip01 Head は…」のコメントと `FaceForward` / `HeadTop` / `MaidRight` / `FindRenderer` / `BoundsSpace` の private メソッドを消す
- 「ワールドの AABB は頭の回転で大きさが変わり影マップの解像度が脈動するため…」のコメントは半径の計算の理由なので残す

Run: `rg -n "FindRenderer|BoundsSpace\(|FaceForward\(|HeadTop\(|MaidRight\(" source/COM3D2.MToonMaid.Plugin/Rendering/HairShadowRenderer.cs`
Expected: すべて `HeadFrame.` 付き

- [ ] **Step 3: UpdateFaceHalfShadow を足す**

`MaidMToonController.cs` のフィールド（`_headRenderers` の後）に足す:

```csharp
        private static readonly int FaceHalfCenterId = Shader.PropertyToID(MToonProps.FaceHalfCenter);
        private static readonly int FaceHalfForwardId = Shader.PropertyToID(MToonProps.FaceHalfForward);
        private static readonly int FaceHalfUpId = Shader.PropertyToID(MToonProps.FaceHalfUp);
        // 顔へ頭の向きを入れたか。OFF にしたとき 1 回だけ 0 に戻すため (毎フレーム全マテリアルへ 0 を書かない)
        private bool _faceHalfWritten;
```

`RenderHairShadow` の前に足す:

```csharp
        /// <summary>
        /// カメラの描画の前に呼ぶ (フレームに 1 回)。半面切り替えの影が ON の間、顔へ頭の中心と向きを入れる。
        /// OFF や頭が無いときは向きを 0 にし、シェーダーを N·L の影へ戻す
        /// </summary>
        public void UpdateFaceHalfShadow(GroupSettings face)
        {
            var body = Maid.body0;
            var head = body != null ? body.trsHead : null;
            var headRenderer = face.faceHalfShadow ? HeadFrame.FindHeadRenderer(body) : null;
            if (head == null || headRenderer == null)
            {
                if (_faceHalfWritten)
                {
                    CollectFaceMaterials();
                    foreach (var m in _faceMaterials)
                    {
                        m.SetVector(FaceHalfForwardId, Vector4.zero);
                    }
                    _faceHalfWritten = false;
                }
                return;
            }
            CollectFaceMaterials();
            var center = HeadFrame.Center(headRenderer);
            var forward = HeadFrame.FaceForward(head);
            var up = HeadFrame.HeadTop(head);
            foreach (var m in _faceMaterials)
            {
                m.SetVector(FaceHalfCenterId, center);
                m.SetVector(FaceHalfForwardId, forward);
                m.SetVector(FaceHalfUpId, up);
            }
            _faceHalfWritten = true;
        }
```

- [ ] **Step 4: カメラの描画の前に呼ぶ**

`MToonMaidPlugin.cs`:
- フィールド `private bool _eyeGazeWarned;` の後に `private bool _faceHalfShadowWarned;` を足す
- `OnCameraPreCull` の `<summary>` の「前髪の影と瞳の視線を更新する」を「前髪の影・顔の半面切り替えの影・瞳の視線を更新する」にする
- `RenderHairShadow` の try/catch の後（`UpdateEyeGaze` の try の前）に足す:

```csharp
                try
                {
                    pair.Value.UpdateFaceHalfShadow(_store.Get(pair.Value.Guid).Resolve(PartGroup.Face));
                }
                catch (System.Exception e)
                {
                    if (!_faceHalfShadowWarned)
                    {
                        _faceHalfShadowWarned = true;
                        Log.LogWarning("顔の半面切り替えの影の更新に失敗しました: " + e);
                    }
                }
```

- [ ] **Step 5: ビルドとテスト**

Run: net471 と net35 のビルド（Global Constraints のコマンド）
Expected: どちらもエラー 0
Run: `MSYS_NO_PATHCONV=1 dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:/COM3D2_5`
Expected: PASS

- [ ] **Step 6: コミット（リファクタと機能を分ける）**

```bash
git add source/COM3D2.MToonMaid.Plugin/Rendering/HeadFrame.cs source/COM3D2.MToonMaid.Plugin/Rendering/HairShadowRenderer.cs
git commit -m "refactor(rendering): 頭の向きと中心の計算を HeadFrame へ移す" -m "顔の半面切り替えの影でも同じ頭の向き・中心を使うため。前髪の影の振る舞いは変えない。"
git add source/COM3D2.MToonMaid.Plugin/MaidMToonController.cs source/COM3D2.MToonMaid.Plugin/MToonMaidPlugin.cs
git commit -m "feat(rendering): 半面切り替えの影へ頭の中心と向きを毎フレーム入れる" -m "揺れや首の動きが LateUpdate で決まった後の頭で左右を分けるため、前髪の影と同じくカメラの描画の直前に入れる。
OFF や頭が無いときは向きを 0 にしてシェーダーを N·L の影へ戻す。"
```

---

### Task 5: UI と README

**Files:**
- Modify: `source/COM3D2.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（`DrawSettings` と新しい `DrawFaceHalfShadowSettings`）
- Modify: `README.md`（「顔の影」の節）

**Interfaces:**
- Consumes: Task 1 の設定・キー・`Max*`、Task 2 の `FaceHalfShadowParams.CentimetersPerMeter`

- [ ] **Step 1: UI を足す**

`DrawSettings` の `if (Shows(GroupSettingKeys.ReceiveShadows)) { ... }` の後（前髪の影の前）に足す:

```csharp
            if (Shows(GroupSettingKeys.FaceHalfShadow))
            {
                DrawFaceHalfShadowSettings(view, shown, groupOnlyDef);
            }
```

`DrawHairShadowSettings` の前に足す:

```csharp
        private void DrawFaceHalfShadowSettings(GUIView view, GroupSettings shown, GroupSettings def)
        {
            Toggle(view, GroupSettingKeys.FaceHalfShadow, "半面切り替えの影",
                "顔の自分の影を、面の向きではなく光の水平方向の角度で切り替える\n正面寄りの光で影なし、横からで光と反対側の半面、後ろ寄りで顔全体が影になる\n落ち影・前髪の影・追加ライトは今どおり",
                shown.faceHalfShadow, (s, v) => s.faceHalfShadow = v);
            // OFF の間に行を消すと、トグルの切り替えで入力欄キャッシュの番号がずれる
            var editable = shown.faceHalfShadow;
            Slider(view, GroupSettingKeys.FaceHalfAngle, "半面になる角度",
                "頭の正面から光までの水平の角度 (度) がこれを超えると、光と反対側の半面が影になる",
                shown.faceHalfAngle, 0f, GroupSettings.MaxFaceHalfAngle, def.faceHalfAngle, (s, v) => s.faceHalfAngle = v, editable: editable);
            Slider(view, GroupSettingKeys.FaceFullAngle, "全面になる角度",
                "頭の正面から光までの水平の角度 (度) がこれを超えると、顔全体が影になる\n半面になる角度より小さいときは半面になる角度として扱う",
                shown.faceFullAngle, 0f, GroupSettings.MaxFaceHalfAngle, def.faceFullAngle, (s, v) => s.faceFullAngle = v, editable: editable);
            Slider(view, GroupSettingKeys.FaceHalfFadeWidth, "フェード幅",
                "影を切り替える角度の幅 (度)\nこの幅の間で影がなめらかに濃くなり、0 で一瞬で切り替わる\n半面になる角度の 2 倍、(180 − 全面になる角度) の 2 倍より広い分は効かない",
                shown.faceHalfFadeWidth, 0f, GroupSettings.MaxFaceHalfFadeWidth, def.faceHalfFadeWidth, (s, v) => s.faceHalfFadeWidth = v, editable: editable);
            // m のままでは 0.005 刻みの調整が既定の書式 (小数 2 桁) で見えない
            Slider(view, GroupSettingKeys.FaceHalfSoftness, "境目のぼかし",
                "左右の半面の境目をぼかす幅 (cm)\n0 でくっきり分かれる",
                shown.faceHalfSoftness * FaceHalfShadowParams.CentimetersPerMeter,
                0f, GroupSettings.MaxFaceHalfSoftness * FaceHalfShadowParams.CentimetersPerMeter,
                def.faceHalfSoftness * FaceHalfShadowParams.CentimetersPerMeter,
                (s, v) => s.faceHalfSoftness = v / FaceHalfShadowParams.CentimetersPerMeter, editable: editable);
        }
```

Run: net471 と net35 のビルド
Expected: エラー 0

- [ ] **Step 2: README を直す**

「#### 顔の影」の節の冒頭の「顔（`head` スロット）の落ち影の扱いを切り替えます。」を「顔（`head` スロット）の影の扱いを切り替えます。」にし、表に 5 行を足す:

```markdown
| 半面切り替えの影 | ON / OFF | OFF | ON の間、顔の自分の影は面の向き（N·L）ではなく、光の水平方向の角度で切り替わる。正面寄りの光で影なし、横寄りで光と反対側の半面、後ろ寄りで顔全体が影になり、その間はなめらかにフェードする |
| 半面になる角度 | 0〜180 度 | 45 度 | 頭の正面から光までの水平の角度がこれを超えると、光と反対側の半面が影になる |
| 全面になる角度 | 0〜180 度 | 135 度 | これを超えると顔全体が影になる。半面になる角度より小さいときは半面になる角度として扱う |
| フェード幅 | 0〜90 度 | 30 度 | 影を切り替える角度の幅。0 で一瞬で切り替わる。光が正面・真後ろをまたぐときに影が飛ばないよう、半面になる角度の 2 倍、(180 − 全面になる角度) の 2 倍より広い分は効かない |
| 境目のぼかし | 0〜2 cm | 0.5 cm | 左右の半面の境目をぼかす幅。0 でくっきり分かれる |
```

表の後の箇条書きに足す:

```markdown
- 「半面切り替えの影」が ON の間も、落ち影・前髪の影・追加ライトは今どおりです。顔の主光源の影には「境界のずらし」「境界の硬さ」「影側を一色に」が効かなくなります（影色は「影側を一色に」が ON のときと同じ）
- 光が真上・真下に近い（仰角 60° 以上）ほど影は薄くなり、真上で影なしになります
```

- [ ] **Step 3: コミット**

```bash
git add source/COM3D2.MToonMaid.Plugin/UI/MToonMaidWindow.cs README.md
git commit -m "feat(ui): 顔タブに半面切り替えの影のトグルと角度のスライダーを足す" -m "トグルが OFF の間は角度のスライダーが効かないため無効表示にする。README に項目と、ON の間に効かなくなる項目を書く。"
```

---

### Task 6: 実機での確認（devbridge）

**Files:** なし（確認のみ。見つかった不具合は該当タスクへ戻って直し、別コミットにする）

- [ ] **Step 1: 反映して起動し直す**

`com3d25-devbridge:restart-verify` スキルで、ゲームを止めて `powershell -File source/COM3D2.MToonMaid.Plugin/build.ps1 -Config debug -Target com3d25` で反映し、起動し直す。ログに MToonMaid の警告・`Shader error` が無いことを `tail_log` で見る。

- [ ] **Step 2: 顔を ON にする（後で戻す）**

`eval_csharp` で、メイド 0 の顔の設定を ON にする（`IsGroupOnly` なので個別にして値を入れる）。元の値を控えておく:

```csharp
var plugin = UnityEngine.Object.FindObjectOfType<COM3D2.MToonMaid.Plugin.MToonMaidPlugin>();
var maid = GameMain.Instance.CharacterMgr.GetMaid(0);
var store = (COM3D2.MToonMaid.Plugin.MaidSettingsStore)typeof(COM3D2.MToonMaid.Plugin.MToonMaidPlugin)
    .GetField("_store", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(plugin);
var s = store.Get(maid.status.guid);
var before = s.face.faceHalfShadow + " " + string.Join(",", s.face.overrides.ToArray());
s.SetOverride(COM3D2.MToonMaid.Plugin.PartGroup.Face, "faceHalfShadow", true);
s.face.faceHalfShadow = true;
plugin.NotifyGroupChanged(maid, COM3D2.MToonMaid.Plugin.PartGroup.Face);
before
```

（`MToonMaidPlugin` / `MaidSettingsStore.Get` の実際の名前と公開範囲は `eval_csharp` で確かめてから使う）

- [ ] **Step 3: 光を水平に回した連続画像を撮る**

Step 2 とは別の `eval_csharp` で撮る（`OnCameraPreCull` はフレームに 1 回しか頭の向きを入れないため、ON にした同じフレームで撮ると N·L の影のまま写る）。1 回の `eval_csharp` の中で、主光源を頭の水平方向に 0° → 180°（15° 刻み、13 枚）回し、顔の正面に置いた一時カメラで 1 枚の RenderTexture のタイルへ描く。光は `try/finally` で必ず戻す。頭の向きは同じ eval の中で取る（メイドが動いても撮影中の頭に合う）:

```csharp
var maid = GameMain.Instance.CharacterMgr.GetMaid(0);
var head = maid.body0.trsHead;
var fwd = head.up; var top = -head.right; var right = UnityEngine.Vector3.Cross(top, fwd);
var r = maid.body0.GetSlot("head").obj.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
var space = r.rootBone != null ? r.rootBone : r.transform;
var center = space.TransformPoint(r.localBounds.center);
var light = GameMain.Instance.MainLight.GetComponent<UnityEngine.Light>();
var saved = light.transform.rotation;
var rt = new UnityEngine.RenderTexture(7 * 256, 2 * 256, 24) { name = "FHS_Sweep" };
var go = new UnityEngine.GameObject("FHS_Cam");
var cam = go.AddComponent<UnityEngine.Camera>();
cam.enabled = false; cam.targetTexture = rt; cam.fieldOfView = 25f; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = UnityEngine.Color.gray;
cam.transform.position = center + fwd * 0.6f; cam.transform.rotation = UnityEngine.Quaternion.LookRotation(-fwd, top);
try
{
    for (var i = 0; i <= 12; i++)
    {
        var a = i * 15f * UnityEngine.Mathf.Deg2Rad;
        var toLight = fwd * UnityEngine.Mathf.Cos(a) + right * UnityEngine.Mathf.Sin(a);
        light.transform.rotation = UnityEngine.Quaternion.LookRotation(-toLight, top);
        cam.rect = new UnityEngine.Rect((i % 7) / 7f, (1 - i / 7) / 2f, 1 / 7f, 0.5f);
        cam.Render();
    }
}
finally
{
    light.transform.rotation = saved;
    UnityEngine.Object.Destroy(go);
}
"ok"
```

`capture` で `rt:FHS_Sweep` を撮って見る。確かめること:
- 0°・15° は影なし、30°〜60° で光と反対側の半面がフェードイン、75°〜105° は半面、120°〜150° で全面へフェード、165°・180° は全面
- 半面の境目が鼻筋のあたりで縦にまっすぐ（まだらでない）、ぼかし 0.5 cm 程度
- アウトラインの色も同じ半面で暗くなる

撮り終えたら RT を `UnityEngine.Object.Destroy` する（名前で `Resources.FindObjectsOfTypeAll<UnityEngine.RenderTexture>()` から探す）。

- [ ] **Step 4: 左右の対称と、正面・真後ろをまたぐ所**

Step 3 の `right` を `-right` にして撮り、左右が鏡写しになることを見る。続けて角度を -5°, 0°, 5°, 175°, 180°, 185° にして撮り、0° と 180° の前後で左右の影が飛ばない（-5° と 5° がほぼ同じ濃さの影なし、175° と 185° がほぼ同じ全面）ことを見る。

同じ 6 枚を、顔の設定を半面 10°・全面 170°・幅 30°（`s.face.faceHalfAngle` などを書いて `NotifyGroupChanged`。別の eval で書いてから撮る）でも撮り、同じく飛ばないことを見る。撮った後は 45 / 135 / 30 に戻す。

- [ ] **Step 5: 真上・真下の光**

光を `toLight = top`（真上）と `-top`（真下）、仰角 45°・75°（`fwd * cos + top * sin` を 90° の水平角で）にして撮る。真上・真下で影なし（真っ黒・真っ白の NaN が無い）、75° で薄い半面、45° で 90° と同じ半面であることを見る。

続けて主光源を `light.enabled = false` にして（`try/finally` で戻す）撮り、顔が真っ黒にならない（環境光だけの明るさ）ことを見る。

- [ ] **Step 6: OFF で今の見た目に戻ること**

Step 2 の逆で `s.face.faceHalfShadow = false` にして `NotifyGroupChanged` し、Step 3 と同じ撮影で N·L の影（今の見た目）に戻ることを見る。顔の MToon マテリアルの `_FaceHalfForward` が `(0,0,0,0)` に戻っていることを `GetVector` で見る。

- [ ] **Step 7: 「ゲームの陰影」OFF の顔**

もう一度 ON にし、顔の `useGameShadowRate` を個別に OFF にして（`SetOverride(Face, "gameShadowRate", true)` → `s.face.useGameShadowRate = false` → `NotifyGroupChanged`）、90° で半面が影色（`_ShadowTex × _ShadeColor`）になることを撮って見る。

- [ ] **Step 8: 後片付け**

- 顔の設定を Step 2 で控えた状態へ戻す（`faceHalfShadow` と `gameShadowRate` の個別を外し、`NotifyGroupChanged`）
- 一時カメラ・RT が残っていないことを確かめる（`UnityEngine.GameObject.Find("FHS_Cam") == null`）
- 主光源の向きが元のままであることを確かめる

- [ ] **Step 9: 2.0 の確認**

Task 3 の `build-bundle.bat all` が 5.6 でもエラーなしだったことと、net35 のビルドが通ることを結果に書く（2.0 の実機確認は今回の範囲外として報告する）。

## レビュー却下メモ

- UpdateFaceHalfShadow に Maid.IsBusy のガードを入れる・CollectFaceMaterials の走査を前髪の影と共有する — 着替え中は破棄済みの Renderer を Unity の null 判定で弾けて、残るのは 1 フレーム程度の古い頭の向きだけで害が無い。走査の重複は前髪の影と同じ規模（メイドごとに _entries を 1 周）で、共有のための結合を増やすほどではない
- PbrCorrect の経路で半面切り替えが効かないことを書く — MToon_IsPbrCorrectOn をプラグインが有効にする経路が無い（rg で define.hlsl と lighting の分岐以外に参照なし）
- 半面角 0°・幅 0 で正面をまたぐと影が一瞬で入れ替わる — 幅 0 は「一瞬で切り替える」設定で、半面角 0° は影なしの状態を持たない設定のため、ユーザーの選んだ見た目どおり（フェード幅の縮めで防げるのは幅が残る場合だけ）
- (code-review) FaceHalfShadowParams.Enabled が UsesFaceHalfShadow の && だけで薄い — 手編集で顔以外を ON にしても書かない意図をテストで固定しており、レビュアーも変更必須ではないとした
- (code-review) シェーダーの「追加ライト (ForwardAdd) は N·L の影のまま」が What — このファイルの既存の COM3D2: コメントの慣習どおり
- (code-review) 全面角 180°・半面角 0° で正面・真後ろをまたぐと左右が一瞬で入れ替わる — 0°/180° は影なし・全面の状態を持たない設定で避けようがないため、UI のツールチップと README に書くだけにした
