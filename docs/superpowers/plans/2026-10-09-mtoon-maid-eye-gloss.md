# COM3D25.MToonMaid.Plugin 瞳の光沢 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** head スロットの瞳（マテリアル番号 0 / 1）を専用シェーダー `COM3D25/MToonMaidEye` へ差し替え、上まぶたの影・下側の明るさときらめき・画面基準で動くハイライト 3 個を描く。`eye_hi`（`SkinHi`）は何も描かないシェーダーへ差し替えて消せる。設定はウィンドウの新しい「目」タブに置く。

**Architecture:** 計算のうち Unity に依存しない部分（設定の補完と丸め・マテリアルの判定・瞳の外接矩形）は単体テストする。`EyeConverter` がシェーダーの差し替え・戻し・値の設定・外接矩形の読み戻しを担い、メイドごとの `MaidEyeGloss` が走査（`MaidMToonController.Scan` から）と視線の更新（プラグインの `Camera.onPreCull` から）を行う。MToon の `_entries` とは別に控えを持ち、既存の MToon の処理には混ぜない。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（シェーダーのバンドル）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-eye-gloss-design.md`（前提として v2.8 `2026-10-09-mtoon-maid-hair-shadow-design.md`）

## Global Constraints

- v2.8 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-hair-shadow.md`）の Global Constraints をすべて引き継ぐ。特に: COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は実行しない、worktree 不使用、main で作業、MTEUtils は編集しない、`debug.bat` は実機検証のタスクでだけ使う、`build-bundle.bat` は Unity エディタを閉じて実行する、過去の spec・計画は書き換えない（今回の spec は Task 7 で実装に合わせて直してよい）
- **ゲームを終了する前に、ユーザーへシーンの保存の有無を明示的に確認し、回答を ledger に書く**（状況からの推測で承認とみなさない）
- 設定ファイルの版は 3 のまま。`eye` 要素の無いファイルは既定（OFF）で読む
- 瞳の判定: スロット `head`、マテリアル番号 0（左目、`trsEyeL`）/ 1（右目、`trsEyeR`）、シェーダー名がちょうど `CM3D2/Toony_Lighted_Trans`（`_NoZ` などの派生は描画状態が違うため対象外）
- ハイライトの板の判定: スロット `head`、番号 2。番号 7 は `MPN.eye_hi_r` のファイル名が空でないときだけ（ゲームは `eye_hi_r` の「テクスチャ変更」を番号 7 へ向け直す。古い顔では番号 7 は `bukkake` なので消さない）。どちらもシェーダー名がちょうど `CM3D2/Lighted_Trans`（SkinHi）であること（顔 MOD で番号 2 が別のマテリアルのとき・古い顔に `eye_hi_r` の値だけが残っているときに、歯や bukkake を消さないため）。消したあとは控えの元のシェーダー名で判定する
- 瞳の機能が効くのは、メイドの「MToon にする」が ON で、かつ `eye.enabled` のとき。顔グループの ON / OFF・スロットの除外とは独立
- 目のボーンは外向きに傾けて置かれ、正面は `-right` に近い。視線はボーンの軸を使わず、`eye.rotation * Inverse(eye.parent.rotation * quaDefEye)` を顔の正面 `trsHead.up` に掛けて求める（2026-10-09 に実機とゲームの `TBody` のコードで確かめた）
- 固定値: 外接矩形の読み戻しは 64×64、アルファの閾値 128（これを超える画素を瞳とみなす）。外接矩形が見つからなければ UV 全体を入れ、鍵を記録せず次の走査で求め直す（無限色の RT がまだ描かれていない場合に備える）。見つからないまま 5 回続いたら鍵を記録して止める（全面透明のテクスチャで毎走査 GPU から読み戻さないため）
- 追加ライトのパス（ForwardAdd）では上まぶたの影と下側の明るさだけを描き、ハイライトときらめきは ForwardBase だけで描く（追加ライトの数だけ白が重なって飛ぶため）
- 視差は視線のビュー座標の横と縦の両方に効かせる（目の上下の動きでもハイライトが滑る）
- head スロットの sub スロットは 1 つだけ（2026-10-09 に実機で `goSlot.CountChildren` = 1）。`GetSlot("head")` だけを見る
- プラグインの終了時には瞳を戻さない（既存の MToon と同じ。終了時はゲームも終わるため）。spec の記述を Task 7 で直す
- 「目」タブの行は条件で増減させない（GUIView の入力欄キャッシュは描画順の番号で引かれる。無効表示にする）
- 実機検証では、ユーザーが今の設定に入れている値を控えてから変え、最後に戻す

## Review Focus

- **OFF で完全に元へ戻る**: 目タブの OFF・メイドの MToon の OFF・eye_hi を消すの OFF で、瞳と `SkinHi` のシェーダーと renderQueue が元に戻り、ゲームの戻し先（`m_dicBackupShader`）に自分のシェーダーが残らない → Task 5 の `RestoreEntry`、Task 7 Step 6
- **SkinHi 以外を消さない**: `eye_hi_r` を設定していないメイドの番号 7、古い顔に `eye_hi_r` の値だけが残っているときの番号 7（bukkake、`_NoZ`）、顔 MOD で番号 2 が別のシェーダーのマテリアルを差し替えない → Task 2 のテスト（番号・`hasRightHighlight`・シェーダー名の組み合わせ）
- **外接矩形が UV 全体のまま固定されない**: 初回の走査で瞳の RT がまだ描かれていなくても、次の走査で求め直す → Task 3 のテスト（見つからなければ false）、Task 5 の `ScanEye`、Task 7 Step 2
- **手編集の範囲外の値・ハイライトの数の過不足**: 読み込み時に範囲へ丸め、3 個にそろえる → Task 1 のテスト
- **派生シェーダーの瞳を触らない**: `Toony_Lighted_Trans_NoZ` の瞳は差し替えない → Task 2 のテスト
- **瞳の MOD を替えたときの外接矩形**: `_MainTex` の実体か `MPN.eye` のファイル名が変わったら求め直す → Task 5 の `ScanEye`、Task 7 Step 5

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/EyeSettings.cs                 (Create: 瞳の設定とハイライト 1 個分)
├─ Settings/MaidSettings.cs                (Modify: eye 要素)
├─ ToonMaterialClassifier.cs               (Modify: 瞳とハイライトの板の判定)
├─ Rendering/IrisBounds.cs                 (Create: 外接矩形の計算)
├─ Rendering/EyeConverter.cs               (Create: 差し替え・戻し・値・読み戻し)
├─ Rendering/MToonProps.cs                 (Modify: 瞳のプロパティ名)
├─ Rendering/MToonShaderProvider.cs        (Modify: 瞳と非表示のシェーダーも読む)
├─ MaidEyeGloss.cs                         (Create: メイド 1 人分の瞳の管理)
├─ MaidMToonController.cs                  (Modify: MaidEyeGloss を持つ)
├─ MToonMaidPlugin.cs                      (Modify: EyeConverter、視線の更新、NotifyEyeChanged)
├─ IMToonMaidHost.cs                       (Modify: IsEyeGlossAvailable、NotifyEyeChanged)
└─ UI/MToonMaidWindow.cs                   (Modify: 目タブ)
source/COM3D25.MToonMaid.Plugin.Tests/
├─ EyeSettingsTests.cs                     (Create)
├─ ToonMaterialClassifierTests.cs          (Modify)
└─ IrisBoundsTests.cs                      (Create)
UnityProject2022/
├─ Assets/Shaders/MToonMaid/MToonMaidEye.shader     (Create)
├─ Assets/Shaders/MToonMaid/MToonMaidHidden.shader  (Create)
└─ Assets/Editor/BuildMToonBundle.cs                (Modify: バンドルに足す)
README.md / spec                                     (Modify)
```

テストとビルドのコマンド（リポジトリ直下で実行）:

- テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
- ビルド（実機へ反映しない）: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
- バンドル: `build-bundle.bat`（Unity エディタを閉じて実行。成功すると `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle` が更新される）

---

### Task 1: 瞳の設定を足す

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Settings/EyeSettings.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/MaidSettings.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/EyeSettingsTests.cs`

**Interfaces:**
- Produces: `class EyeHighlightSettings { float strength, x, y, size, softness; }`、`class EyeSettings`（フィールドは下のコード、`const int HighlightCount = 3`、`const float MaxHighlightOffset = 0.5f`、`const float MaxHighlightSize = 0.5f`、`static EyeSettings CreateDefault()`、`void FillMissing()`）、`MaidSettings.eye`（`EyeSettings`、`FillMissing` 後は非 null）

- [ ] **Step 1: 失敗するテストを書く**

`source/COM3D25.MToonMaid.Plugin.Tests/EyeSettingsTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class EyeSettingsTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public EyeSettingsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidEyeTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        private MaidSettings LoadXml(string xml)
        {
            var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, xml);
            return MaidSettingsStore.Load(path, _warnings.Add);
        }

        [Fact]
        public void eye要素の無いファイルはOFFの既定()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><enabled>true</enabled></MToonMaid>");

            Assert.NotNull(s.eye);
            Assert.False(s.eye.enabled);
            Assert.True(s.eye.hideEyeHighlight);
            Assert.Equal(0.5f, s.eye.upperShadow);
            Assert.NotNull(s.eye.upperShadowColor);
            Assert.NotNull(s.eye.lowerGlowColor);
            Assert.Equal(EyeSettings.HighlightCount, s.eye.highlights.Count);
            Assert.Equal(1f, s.eye.highlights[0].strength);
            Assert.Equal(0.14f, s.eye.highlights[0].size);
            Assert.Equal(0.6f, s.eye.highlights[2].strength);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void ハイライトの不足は既定で埋め多すぎる分は捨てる()
        {
            var few = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><eye><highlights>"
                + "<EyeHighlightSettings><strength>0.3</strength><x>0.1</x><y>0.1</y><size>0.2</size><softness>0.5</softness></EyeHighlightSettings>"
                + "</highlights></eye></MToonMaid>");

            Assert.Equal(3, few.eye.highlights.Count);
            Assert.Equal(0.3f, few.eye.highlights[0].strength);
            Assert.Equal(0.8f, few.eye.highlights[1].strength);

            var item = "<EyeHighlightSettings><strength>0.1</strength></EyeHighlightSettings>";
            var many = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><eye><highlights>"
                + item + item + item + item + "</highlights></eye></MToonMaid>");

            Assert.Equal(3, many.eye.highlights.Count);
        }

        [Fact]
        public void 範囲外の値は丸める()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><eye>"
                + "<upperShadow>5</upperShadow><lowerGlowHeight>-1</lowerGlowHeight><parallax>2</parallax>"
                + "<highlights><EyeHighlightSettings><strength>3</strength><x>-3</x><y>9</y><size>4</size><softness>-2</softness></EyeHighlightSettings></highlights>"
                + "</eye></MToonMaid>");

            Assert.Equal(1f, s.eye.upperShadow);
            Assert.Equal(0f, s.eye.lowerGlowHeight);
            Assert.Equal(1f, s.eye.parallax);
            var h = s.eye.highlights[0];
            Assert.Equal(1f, h.strength);
            Assert.Equal(-EyeSettings.MaxHighlightOffset, h.x);
            Assert.Equal(EyeSettings.MaxHighlightOffset, h.y);
            Assert.Equal(EyeSettings.MaxHighlightSize, h.size);
            Assert.Equal(0f, h.softness);
        }

        [Fact]
        public void 保存して読み直すと同じ値になる()
        {
            var path = Path.Combine(_dir, "eye.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.eye.enabled = true;
            s.eye.hideEyeHighlight = false;
            s.eye.sparkle = 0.7f;
            s.eye.lowerGlowColor = new ColorData(0.1f, 0.2f, 0.3f, 1f);
            s.eye.highlights[1].x = 0.25f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.True(loaded.eye.enabled);
            Assert.False(loaded.eye.hideEyeHighlight);
            Assert.Equal(0.7f, loaded.eye.sparkle);
            Assert.Equal(0.2f, loaded.eye.lowerGlowColor.g);
            Assert.Equal(3, loaded.eye.highlights.Count);
            Assert.Equal(0.25f, loaded.eye.highlights[1].x);
            Assert.Empty(_warnings);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルドエラー（`EyeSettings` / `MaidSettings.eye` が無い）

- [ ] **Step 3: 実装する**

`source/COM3D25.MToonMaid.Plugin/Settings/EyeSettings.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>瞳のハイライト 1 個分。位置と大きさは瞳の幅を 1 とした値で、位置は瞳の中心から画面の右・上がプラス</summary>
    public class EyeHighlightSettings
    {
        public float strength;
        public float x;
        public float y;
        public float size = 0.1f;
        public float softness = 0.2f;

        public EyeHighlightSettings()
        {
        }

        public EyeHighlightSettings(float strength, float x, float y, float size, float softness)
        {
            this.strength = strength;
            this.x = x;
            this.y = y;
            this.size = size;
            this.softness = softness;
        }
    }

    /// <summary>
    /// メイド 1 人分の瞳の光沢の設定。共通の値の継承には乗せない。
    /// 参照型 (色・ハイライト) は null のまま読み、FillMissing で補う
    /// </summary>
    public class EyeSettings
    {
        public const int HighlightCount = 3;
        public const float MaxHighlightOffset = 0.5f;
        public const float MaxHighlightSize = 0.5f;

        public bool enabled;
        public bool hideEyeHighlight = true;

        public float upperShadow = 0.5f;
        public ColorData upperShadowColor;
        public float upperShadowHeight = 0.45f;
        public float upperShadowSoftness = 0.4f;

        public float lowerGlow = 0.5f;
        public ColorData lowerGlowColor;
        public float lowerGlowHeight = 0.45f;
        public float lowerGlowSoftness = 0.5f;
        public float sparkle = 0.3f;

        // 初期値を入れておくと XmlSerializer が読んだ要素を後ろへ足し、既定と二重になる
        public List<EyeHighlightSettings> highlights;

        public float parallax = 0.3f;

        public static EyeSettings CreateDefault()
        {
            var s = new EyeSettings();
            s.FillMissing();
            return s;
        }

        /// <summary>null の参照型を既定で埋め、ハイライトを 3 個にそろえ、値を範囲へ丸める</summary>
        public void FillMissing()
        {
            if (upperShadowColor == null)
            {
                upperShadowColor = new ColorData(0x40 / 255f, 0x30 / 255f, 0x6A / 255f, 1f);
            }
            if (lowerGlowColor == null)
            {
                lowerGlowColor = new ColorData(0x9F / 255f, 0xD8 / 255f, 1f, 1f);
            }
            if (highlights == null)
            {
                highlights = new List<EyeHighlightSettings>();
            }
            highlights.RemoveAll(h => h == null);
            if (highlights.Count > HighlightCount)
            {
                highlights.RemoveRange(HighlightCount, highlights.Count - HighlightCount);
            }
            while (highlights.Count < HighlightCount)
            {
                highlights.Add(CreateDefaultHighlight(highlights.Count));
            }
            ClampValues();
        }

        public static EyeHighlightSettings CreateDefaultHighlight(int index)
        {
            switch (index)
            {
                case 0: return new EyeHighlightSettings(1f, -0.18f, 0.22f, 0.14f, 0.2f);
                case 1: return new EyeHighlightSettings(0.8f, 0.05f, 0.28f, 0.05f, 0.2f);
                case 2: return new EyeHighlightSettings(0.6f, 0.2f, -0.25f, 0.04f, 0.2f);
                default: throw new ArgumentOutOfRangeException(nameof(index), index, null);
            }
        }

        private void ClampValues()
        {
            upperShadow = Clamp01(upperShadow);
            upperShadowHeight = Clamp01(upperShadowHeight);
            upperShadowSoftness = Clamp01(upperShadowSoftness);
            lowerGlow = Clamp01(lowerGlow);
            lowerGlowHeight = Clamp01(lowerGlowHeight);
            lowerGlowSoftness = Clamp01(lowerGlowSoftness);
            sparkle = Clamp01(sparkle);
            parallax = Clamp01(parallax);
            foreach (var h in highlights)
            {
                h.strength = Clamp01(h.strength);
                h.x = Clamp(h.x, -MaxHighlightOffset, MaxHighlightOffset);
                h.y = Clamp(h.y, -MaxHighlightOffset, MaxHighlightOffset);
                h.size = Clamp(h.size, 0f, MaxHighlightSize);
                h.softness = Clamp01(h.softness);
            }
        }

        private static float Clamp01(float v)
        {
            return Clamp(v, 0f, 1f);
        }

        private static float Clamp(float v, float min, float max)
        {
            return Math.Max(min, Math.Min(max, v));
        }
    }
}
```

`MaidSettings.cs` の `public List<string> excludedSlots = …;` の次に足す:

```csharp
        // 瞳の光沢。メイドの enabled が親スイッチ
        public EyeSettings eye;
```

`MaidSettings.FillMissing` の `if (excludedSlots == null) { … }` の後に足す:

```csharp
            if (eye == null)
            {
                eye = EyeSettings.CreateDefault();
            }
            else
            {
                eye.FillMissing();
            }
```

- [ ] **Step 4: テストが通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS（既存のテストを含む）

- [ ] **Step 5: コミットする**

commit スキルで `feat: 瞳の光沢の設定を追加` としてコミットする（`EyeSettings.cs` / `MaidSettings.cs` / `EyeSettingsTests.cs`）。

---

### Task 2: 瞳とハイライトの板を判定する

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/ToonMaterialClassifier.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs`

**Interfaces:**
- Produces: `ToonMaterialClassifier.IsEyeMaterial(string slotCategory, int materialIndex, string shaderName) : bool`、`ToonMaterialClassifier.IsEyeShader(string shaderName) : bool`、`ToonMaterialClassifier.IsEyeHighlightMaterial(string slotCategory, int materialIndex, bool hasRightHighlight, string shaderName) : bool`、`ToonMaterialClassifier.HeadSlot = "head"`、`ToonMaterialClassifier.LeftEyeMaterialIndex = 0`

- [ ] **Step 1: 失敗するテストを書く**

`ToonMaterialClassifierTests` の末尾に足す:

```csharp
        [Theory]
        [InlineData("head", 0, "CM3D2/Toony_Lighted_Trans", true)]
        [InlineData("head", 1, "CM3D2/Toony_Lighted_Trans", true)]
        [InlineData("head", 2, "CM3D2/Toony_Lighted_Trans", false)]
        [InlineData("head", 0, "CM3D2/Toony_Lighted_Trans_NoZ", false)]
        [InlineData("head", 0, "CM3D2/Lighted_Trans", false)]
        [InlineData("head", 0, "COM3D25/MToonMaidEye", false)]
        [InlineData("head", 0, null, false)]
        [InlineData("body", 0, "CM3D2/Toony_Lighted_Trans", false)]
        [InlineData(null, 0, "CM3D2/Toony_Lighted_Trans", false)]
        public void 瞳は顔の番号0と1の半透明トゥーンだけ(string slot, int index, string shader, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsEyeMaterial(slot, index, shader));
        }

        [Theory]
        [InlineData("head", 2, false, "CM3D2/Lighted_Trans", true)]
        [InlineData("head", 2, true, "CM3D2/Lighted_Trans", true)]
        [InlineData("head", 7, true, "CM3D2/Lighted_Trans", true)]
        [InlineData("head", 7, false, "CM3D2/Lighted_Trans", false)]
        // 古い顔に eye_hi_r の値だけが残っているときの bukkake
        [InlineData("head", 7, true, "CM3D2/Toony_Lighted_Trans_NoZ", false)]
        // 顔 MOD で番号 2 が SkinHi でない
        [InlineData("head", 2, false, "CM3D2/Toony_Lighted", false)]
        [InlineData("head", 2, false, null, false)]
        [InlineData("head", 3, true, "CM3D2/Lighted_Trans", false)]
        [InlineData("head", 0, true, "CM3D2/Lighted_Trans", false)]
        [InlineData("body", 2, false, "CM3D2/Lighted_Trans", false)]
        public void ハイライトの板は顔の番号2と右目用の7のSkinHi(string slot, int index, bool hasRight, string shader, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsEyeHighlightMaterial(slot, index, hasRight, shader));
        }
```

- [ ] **Step 2: テストが失敗することを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルドエラー（`IsEyeMaterial` / `IsEyeHighlightMaterial` が無い）

- [ ] **Step 3: 実装する**

`ToonMaterialClassifier` の `private const string ToonyPrefix …` の次に足す:

```csharp
        public const string HeadSlot = "head";
        // eye の menu は顔によらず head の番号 0 を左目 (EYE_L)、1 を右目 (EYE_R) として書く
        public const int LeftEyeMaterialIndex = 0;
        private const int RightEyeMaterialIndex = 1;
        // eye_hi は番号 2。ゲームは eye_hi_r の「テクスチャ変更」を番号 7 へ向け直す (Menu.ProcScript)
        private const int EyeHighlightMaterialIndex = 2;
        private const int RightEyeHighlightMaterialIndex = 7;
        // 前方一致にすると _NoZ (深度を比べない) の瞳まで深度を比べるシェーダーへ差し替え、描画順が変わる
        private const string EyeShaderName = "CM3D2/Toony_Lighted_Trans";
        // 番号だけで判定すると、顔 MOD の番号 2 や古い顔の番号 7 (bukkake) まで消す
        private const string EyeHighlightShaderName = "CM3D2/Lighted_Trans";
```

`UsesHairShadow` の後に足す:

```csharp
        public static bool IsEyeShader(string shaderName)
        {
            return shaderName == EyeShaderName;
        }

        /// <summary>瞳の光沢のシェーダーへ差し替える瞳のマテリアル</summary>
        public static bool IsEyeMaterial(string slotCategory, int materialIndex, string shaderName)
        {
            return slotCategory == HeadSlot
                && (materialIndex == LeftEyeMaterialIndex || materialIndex == RightEyeMaterialIndex)
                && IsEyeShader(shaderName);
        }

        /// <summary>
        /// eye_hi の板 (SkinHi)。hasRightHighlight は MPN.eye_hi_r にファイルがあるか
        /// (古い顔の番号 7 は別のマテリアルのため、右目用のハイライトを使うときだけ板とみなす)。
        /// shaderName は差し替える前のシェーダー名 (消した後は控えの名前を渡す)
        /// </summary>
        public static bool IsEyeHighlightMaterial(string slotCategory, int materialIndex, bool hasRightHighlight, string shaderName)
        {
            return slotCategory == HeadSlot
                && shaderName == EyeHighlightShaderName
                && (materialIndex == EyeHighlightMaterialIndex
                    || (hasRightHighlight && materialIndex == RightEyeHighlightMaterialIndex));
        }
```

- [ ] **Step 4: テストが通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミットする**

commit スキルで `feat: 瞳と eye_hi の板のマテリアルを判定する` としてコミットする。

---

### Task 3: 瞳の外接矩形を求める

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/IrisBounds.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/IrisBoundsTests.cs`

**Interfaces:**
- Produces: `struct IrisRect { float x, y, width, height; static readonly IrisRect Full; }`、`IrisBounds.TryCompute(byte[] alpha, int width, int height, byte threshold, out IrisRect rect) : bool`（見つからなければ false で `rect` は `Full`）、`IrisBounds.SampleSize = 64`、`IrisBounds.Threshold = 128`

- [ ] **Step 1: 失敗するテストを書く**

`source/COM3D25.MToonMaid.Plugin.Tests/IrisBoundsTests.cs`:

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class IrisBoundsTests
    {
        // 8×8。0 行目が v = 0
        private static byte[] Image(int x0, int y0, int x1, int y1, byte value)
        {
            var a = new byte[64];
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    a[y * 8 + x] = value;
                }
            }
            return a;
        }

        [Fact]
        public void 不透明な画素の外接矩形をUVで返す()
        {
            IrisRect r;
            Assert.True(IrisBounds.TryCompute(Image(2, 1, 5, 6, 255), 8, 8, 128, out r));

            Assert.Equal(2f / 8, r.x, 5);
            Assert.Equal(1f / 8, r.y, 5);
            Assert.Equal(4f / 8, r.width, 5);
            Assert.Equal(6f / 8, r.height, 5);
        }

        [Fact]
        public void 閾値ちょうどの画素は含めない()
        {
            var a = Image(2, 2, 3, 3, 255);
            a[7 * 8 + 7] = 128;

            IrisRect r;
            Assert.True(IrisBounds.TryCompute(a, 8, 8, 128, out r));

            Assert.Equal(2f / 8, r.x, 5);
            Assert.Equal(2f / 8, r.width, 5);
        }

        [Fact]
        public void 全面透明なら見つからずUV全体()
        {
            IrisRect r;
            Assert.False(IrisBounds.TryCompute(new byte[64], 8, 8, 128, out r));

            Assert.Equal(IrisRect.Full.x, r.x);
            Assert.Equal(IrisRect.Full.y, r.y);
            Assert.Equal(IrisRect.Full.width, r.width);
            Assert.Equal(IrisRect.Full.height, r.height);
        }

        [Fact]
        public void 端に接する矩形()
        {
            IrisRect r;
            Assert.True(IrisBounds.TryCompute(Image(0, 0, 7, 7, 200), 8, 8, 128, out r));

            Assert.Equal(0f, r.x, 5);
            Assert.Equal(0f, r.y, 5);
            Assert.Equal(1f, r.width, 5);
            Assert.Equal(1f, r.height, 5);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルドエラー（`IrisBounds` が無い）

- [ ] **Step 3: 実装する**

`source/COM3D25.MToonMaid.Plugin/Rendering/IrisBounds.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>瞳の外接矩形 (UV)。x, y が最小の角</summary>
    public struct IrisRect
    {
        public static readonly IrisRect Full = new IrisRect(0f, 0f, 1f, 1f);

        public float x;
        public float y;
        public float width;
        public float height;

        public IrisRect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    /// <summary>
    /// 瞳のテクスチャのアルファから外接矩形を求める。Unity の型を使わない (テストの net48 では UnityEngine.CoreModule を読めないため)
    /// </summary>
    public static class IrisBounds
    {
        public const int SampleSize = 64;
        public const byte Threshold = 128;

        /// <summary>
        /// alpha は行優先で 0 行目が v = 0 (Texture2D.GetPixels32 と同じ並び)。threshold を超える画素を瞳とみなす。
        /// 見つからなければ false で rect は UV 全体
        /// </summary>
        public static bool TryCompute(byte[] alpha, int width, int height, byte threshold, out IrisRect rect)
        {
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (alpha[y * width + x] <= threshold)
                    {
                        continue;
                    }
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0)
            {
                rect = IrisRect.Full;
                return false;
            }
            rect = new IrisRect(
                (float)minX / width, (float)minY / height,
                (float)(maxX - minX + 1) / width, (float)(maxY - minY + 1) / height);
            return true;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミットする**

commit スキルで `feat: 瞳の外接矩形を求める` としてコミットする。

---

### Task 4: 瞳と非表示のシェーダーを作ってバンドルに入れる

**Files:**
- Create: `UnityProject2022/Assets/Shaders/MToonMaid/MToonMaidEye.shader`, `UnityProject2022/Assets/Shaders/MToonMaid/MToonMaidHidden.shader`
- Modify: `UnityProject2022/Assets/Editor/BuildMToonBundle.cs`, `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`, `source/COM3D25.MToonMaid.Plugin/Rendering/MToonShaderProvider.cs`
- 生成物: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

**Interfaces:**
- Produces: `MToonShaders.eye` / `MToonShaders.hidden`（どちらかが読めなければ両方 null）、`MToonProps` の瞳の定数（下のコード）。シェーダーのプロパティ名は `MToonProps` と一致させる

- [ ] **Step 1: 瞳のシェーダーを書く**

`UnityProject2022/Assets/Shaders/MToonMaid/MToonMaidEye.shader`:

```hlsl
// MToonMaid: 瞳の光沢。元の CM3D2/Toony_Lighted_Trans と同じ描画順・ブレンドで、影なしに明るさだけライトへ追従させ、
// 上まぶたの影・下側の明るさときらめき・画面基準のハイライトを重ねる。値はプラグインが入れる
Shader "COM3D25/MToonMaidEye"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _IrisRect ("Iris Rect", Vector) = (0, 0, 1, 1)
        _UpperShadow ("Upper Shadow", Range(0, 1)) = 0.5
        _UpperShadowColor ("Upper Shadow Color", Color) = (0.251, 0.188, 0.416, 1)
        _UpperShadowHeight ("Upper Shadow Height", Range(0, 1)) = 0.45
        _UpperShadowSoftness ("Upper Shadow Softness", Range(0, 1)) = 0.4
        _LowerGlow ("Lower Glow", Range(0, 1)) = 0.5
        _LowerGlowColor ("Lower Glow Color", Color) = (0.624, 0.847, 1, 1)
        _LowerGlowHeight ("Lower Glow Height", Range(0, 1)) = 0.45
        _LowerGlowSoftness ("Lower Glow Softness", Range(0, 1)) = 0.5
        _Sparkle ("Sparkle", Range(0, 1)) = 0.3
        _Highlight0 ("Highlight 0 (x, y, size, softness)", Vector) = (-0.18, 0.22, 0.14, 0.2)
        _Highlight1 ("Highlight 1 (x, y, size, softness)", Vector) = (0.05, 0.28, 0.05, 0.2)
        _Highlight2 ("Highlight 2 (x, y, size, softness)", Vector) = (0.2, -0.25, 0.04, 0.2)
        _HighlightStrength ("Highlight Strength", Vector) = (1, 0.8, 0.6, 0)
        _Parallax ("Parallax", Range(0, 1)) = 0.3
        _EyeGaze ("Eye Gaze (world)", Vector) = (0, 0, 0, 0)
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Lighting.cginc"
    #include "AutoLight.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    half4 _Color;
    float4 _IrisRect;
    half _UpperShadow;
    half4 _UpperShadowColor;
    half _UpperShadowHeight;
    half _UpperShadowSoftness;
    half _LowerGlow;
    half4 _LowerGlowColor;
    half _LowerGlowHeight;
    half _LowerGlowSoftness;
    half _Sparkle;
    float4 _Highlight0;
    float4 _Highlight1;
    float4 _Highlight2;
    half4 _HighlightStrength;
    half _Parallax;
    float4 _EyeGaze;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 worldPos : TEXCOORD1;
        float3 viewPos : TEXCOORD2;
        UNITY_LIGHTING_COORDS(3, 4)
    };

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        o.viewPos = UnityObjectToViewPos(v.vertex);
        UNITY_TRANSFER_LIGHTING(o, v.uv);
        return o;
    }

    float2 Hash22(float2 p)
    {
        float3 a = frac(p.xyx * float3(123.34, 234.34, 345.65));
        a += dot(a, a + 34.45);
        return frac(float2(a.x * a.y, a.y * a.z));
    }

    // q (瞳の座標) を、ビューの右・上を軸にした座標へ写す。
    // テクスチャの向きのまま置くと、左右の目で UV が鏡像のときや頭を傾けたとき、ハイライトが画面上で別の側へ出る。
    // 画面の 1 画素あたりの q とビュー座標の変化の比から写像を求めるので、画面の y の向きの差は打ち消し合う
    float2 ToViewAligned(float2 q, float3 viewPos)
    {
        const float2 qx = ddx(q);
        const float2 qy = ddy(q);
        const float2 px = ddx(viewPos.xy);
        const float2 py = ddy(viewPos.xy);
        const float detP = px.x * py.y - py.x * px.y;
        if (abs(detP) < 1e-14)
        {
            return q;
        }
        // M = [qx qy] * inverse([px py]) の列。ビューの右・上が q のどちら向きか
        const float2 right = (qx * py.y - qy * px.y) / detP;
        const float2 up = (qy * px.x - qx * py.x) / detP;
        const float handed = (right.x * up.y - right.y * up.x) < 0 ? -1 : 1;
        const float2 r = right * rsqrt(max(dot(right, right), 1e-12));
        const float2 u = float2(-r.y, r.x) * handed;
        return float2(dot(q, r), dot(q, u));
    }

    half Circle(float2 p, float4 h, half strength)
    {
        if (strength <= 0 || h.z <= 0)
        {
            return 0;
        }
        const float inner = min(h.z * (1 - h.w), h.z - 1e-4);
        return (1 - smoothstep(inner, h.z, length(p - h.xy))) * strength;
    }

    // shine はハイライトときらめきの倍率。追加ライトのパスで 0 にする (追加ライトの数だけ白が重なって飛ぶため)
    half3 ApplyGloss(half3 albedo, float2 uv, float3 viewPos, half shine)
    {
        // 0〜1 に正規化した瞳の座標 (上下はテクスチャの向き = 頭の上下)
        const float2 n = (uv - _IrisRect.xy) / max(_IrisRect.zw, 1e-4);
        // 瞳の幅を 1 とした中心からの座標。高さを幅で割り、丸が瞳の縦横比で潰れないようにする
        const float2 q = (n - 0.5) * float2(1, _IrisRect.w / max(_IrisRect.z, 1e-4));
        // ddx / ddy は分岐より前で求める
        const float2 s = ToViewAligned(q, viewPos);

        const half upperEdge = 1 - _UpperShadowHeight;
        const half upperMask = smoothstep(upperEdge, upperEdge + max(_UpperShadowSoftness * _UpperShadowHeight, 1e-4), n.y) * _UpperShadow;
        albedo = lerp(albedo, albedo * _UpperShadowColor.rgb, upperMask);

        const half lowerMask = 1 - smoothstep(_LowerGlowHeight - max(_LowerGlowSoftness * _LowerGlowHeight, 1e-4), _LowerGlowHeight, n.y);
        albedo = 1 - (1 - albedo) * (1 - _LowerGlowColor.rgb * (lowerMask * _LowerGlow));

        // きらめき: 瞳の幅の 0.08 ごとのセルに 1 つ、半分ほどのセルにだけ小さな点を置く
        const float cell = 0.08;
        const float2 id = floor(q / cell);
        const float2 center = (id + 0.2 + Hash22(id) * 0.6) * cell;
        const half sparkleDot = (1 - smoothstep(0.004, 0.012, length(q - center))) * step(0.55, Hash22(id + 17.0).x);
        albedo += sparkleDot * _Sparkle * lowerMask * shine;

        // 視差: 目がビューの右 (上) を向くと、カメラ側の光の映り込みは瞳の中で左 (下) へ寄る
        const float2 gazeVS = mul((float3x3)UNITY_MATRIX_V, _EyeGaze.xyz).xy;
        const float2 p = s + gazeVS * _Parallax;
        albedo += (Circle(p, _Highlight0, _HighlightStrength.x)
            + Circle(p, _Highlight1, _HighlightStrength.y)
            + Circle(p, _Highlight2, _HighlightStrength.z)) * shine;
        return albedo;
    }
    ENDCG

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBase
            #pragma multi_compile_fwdbase

            half4 fragBase(v2f i) : SV_Target
            {
                const half4 tex = tex2D(_MainTex, i.uv) * _Color;
                const half3 albedo = ApplyGloss(tex.rgb, i.uv, i.viewPos, 1);
                // 顔の MToon の光側と同じく、主光源の色に上下で均した環境光を足す (光と影の境界は入れない)
                const half3 ambient = (ShadeSH9(half4(0, 1, 0, 1)) + ShadeSH9(half4(0, -1, 0, 1))) * 0.5;
                return half4(albedo * (_LightColor0.rgb + ambient), tex.a);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAdd
            #pragma multi_compile_fwdadd

            half4 fragAdd(v2f i) : SV_Target
            {
                const half4 tex = tex2D(_MainTex, i.uv) * _Color;
                const half3 albedo = ApplyGloss(tex.rgb, i.uv, i.viewPos, 0);
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                return half4(albedo * _LightColor0.rgb * atten, tex.a);
            }
            ENDCG
        }
    }
}
```

- [ ] **Step 2: 非表示のシェーダーを書く**

`UnityProject2022/Assets/Shaders/MToonMaid/MToonMaidHidden.shader`:

```hlsl
// MToonMaid: 何も描かない。eye_hi の板 (SkinHi) を瞳の光沢と二重にしないよう、マテリアルや menu に触らずに消す
Shader "Hidden/COM3D25/MToonMaidHidden"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Pass
        {
            ZWrite Off
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                clip(-1);
                return 0;
            }
            ENDCG
        }
    }
}
```

- [ ] **Step 3: バンドルに足す**

`BuildMToonBundle.cs` の `assetNames` の `"Assets/Shaders/MToonMaid/HairShadowCaster.shader",` の次に足し、`<summary>` を「COM3D25/MToon10・前髪の影・瞳の光沢のシェーダーを mtoon_bundle にまとめる」に直す:

```csharp
                    "Assets/Shaders/MToonMaid/MToonMaidEye.shader",
                    "Assets/Shaders/MToonMaid/MToonMaidHidden.shader",
```

- [ ] **Step 4: プロパティ名を足す**

`MToonProps.cs` の `CasterMatrix` の次に足す:

```csharp
        // 瞳の光沢 (MToonMaidEye) と、eye_hi の板を消すシェーダー
        public const string EyeShaderName = "COM3D25/MToonMaidEye";
        public const string HiddenShaderName = "Hidden/COM3D25/MToonMaidHidden";
        public const string EyeIrisRect = "_IrisRect";
        public const string EyeUpperShadow = "_UpperShadow";
        public const string EyeUpperShadowColor = "_UpperShadowColor";
        public const string EyeUpperShadowHeight = "_UpperShadowHeight";
        public const string EyeUpperShadowSoftness = "_UpperShadowSoftness";
        public const string EyeLowerGlow = "_LowerGlow";
        public const string EyeLowerGlowColor = "_LowerGlowColor";
        public const string EyeLowerGlowHeight = "_LowerGlowHeight";
        public const string EyeLowerGlowSoftness = "_LowerGlowSoftness";
        public const string EyeSparkle = "_Sparkle";
        public static readonly string[] EyeHighlights = { "_Highlight0", "_Highlight1", "_Highlight2" };
        public const string EyeHighlightStrength = "_HighlightStrength";
        public const string EyeParallax = "_Parallax";
        public const string EyeGaze = "_EyeGaze";
```

- [ ] **Step 5: シェーダーを読む**

`MToonShaderProvider.cs` の `MToonShaders` に足す:

```csharp
        // 瞳の光沢と eye_hi を消すシェーダー。どちらかが読めなければ両方 null (瞳の機能だけを無効にする)
        public Shader eye;
        public Shader hidden;
```

`Load` の `Shader caster = null;` の次に `Shader eye = null; Shader hidden = null;` を足し、`foreach` の `else if (shader.name == MToonProps.HairShadowCasterShaderName) { … }` の後に足す:

```csharp
                else if (shader.name == MToonProps.EyeShaderName)
                {
                    eye = shader;
                }
                else if (shader.name == MToonProps.HiddenShaderName)
                {
                    hidden = shader;
                }
```

`return new MToonShaders { … };` の前に足し、戻り値に `eye = eye, hidden = hidden` を加える:

```csharp
            if (eye == null || hidden == null)
            {
                log.LogError(ResourceName + " に瞳の光沢のシェーダーが含まれていないため、瞳の光沢は使えません");
                eye = null;
                hidden = null;
            }
            else if (!eye.isSupported || !hidden.isSupported)
            {
                log.LogError("瞳の光沢のシェーダーが現在のグラフィックス API (" + SystemInfo.graphicsDeviceType + ") で使えないため、瞳の光沢は使えません");
                eye = null;
                hidden = null;
            }
```

`MToonShaderProvider` の `<summary>` を「DLL に埋め込んだ mtoon_bundle から COM3D25/MToon10・前髪の影・瞳の光沢のシェーダーを読む」に直す。

- [ ] **Step 6: バンドルを作る**

Unity エディタで `UnityProject2022` を開いていないことを確かめてから、Run: `build-bundle.bat`
Expected: 成功し、`source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle` が更新される。`UnityProject2022/Logs/build-bundle.log` に `Shader error` が無い。エラーがあれば、ログの文面と行番号を ledger に残してからシェーダーを直し、作り直す

- [ ] **Step 7: ビルドする**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: 成功（警告の増加なし）

- [ ] **Step 8: コミットする**

commit スキルで `feat: 瞳の光沢と eye_hi を消すシェーダーを追加` としてコミットする（シェーダー 2 本と .meta、`BuildMToonBundle.cs`、`MToonProps.cs`、`MToonShaderProvider.cs`、`mtoon_bundle`）。

---

### Task 5: 瞳のマテリアルを差し替え・戻す

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/EyeConverter.cs`, `source/COM3D25.MToonMaid.Plugin/MaidEyeGloss.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs`, `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`, `source/COM3D25.MToonMaid.Plugin/IMToonMaidHost.cs`

**Interfaces:**
- Consumes: Task 1 の `EyeSettings`、Task 2 の `ToonMaterialClassifier.IsEyeMaterial` / `IsEyeShader` / `IsEyeHighlightMaterial` / `HeadSlot` / `LeftEyeMaterialIndex`、Task 3 の `IrisBounds` / `IrisRect`、Task 4 の `MToonShaders.eye` / `hidden` と `MToonProps.Eye*`
- Produces: `EyeConverter(Shader eye, Shader hidden)`（`EyeShader` / `HiddenShader`、`ToEye(Material) : EyeMaterialBackup`、`ToHidden(Material) : EyeMaterialBackup`、`Restore(Material, EyeMaterialBackup)`、`static Apply(Material, EyeSettings)`、`static SetIrisRect(Material, IrisRect)`、`static SetGaze(Material, Vector3)`、`TryReadIrisRect(Texture, out IrisRect) : bool`、`Dispose()`）、`MaidEyeGloss(Maid, EyeConverter, Action<string>)`（`Scan(EyeSettings)`、`Apply(EyeSettings)`、`UpdateGaze()`、`RestoreAll()`、`Forget()`）、`MaidMToonController.ApplyEye(EyeSettings)` / `UpdateEyeGaze()`、`IMToonMaidHost.IsEyeGlossAvailable` / `NotifyEyeChanged(Maid)`

Unity の型に依存するため単体テストは無い。ビルドと Task 7 の実機検証で確かめる。

- [ ] **Step 1: EyeConverter を書く**

`source/COM3D25.MToonMaid.Plugin/Rendering/EyeConverter.cs`:

```csharp
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>差し替える前の瞳・eye_hi の板の状態。戻すときに使う</summary>
    public sealed class EyeMaterialBackup
    {
        public Shader shader;
        // シェーダーを代入すると renderQueue がシェーダーの既定へ戻るため控える
        public int renderQueue;
    }

    /// <summary>瞳の光沢のシェーダーへの差し替え・戻し・値の設定と、瞳の外接矩形の読み戻し</summary>
    public sealed class EyeConverter
    {
        private Texture2D _readback;
        private byte[] _alpha;

        public Shader EyeShader { get; private set; }
        public Shader HiddenShader { get; private set; }

        public EyeConverter(Shader eye, Shader hidden)
        {
            EyeShader = eye;
            HiddenShader = hidden;
        }

        public EyeMaterialBackup ToEye(Material m)
        {
            return Replace(m, EyeShader);
        }

        public EyeMaterialBackup ToHidden(Material m)
        {
            return Replace(m, HiddenShader);
        }

        private static EyeMaterialBackup Replace(Material m, Shader shader)
        {
            var backup = new EyeMaterialBackup { shader = m.shader, renderQueue = m.renderQueue };
            m.shader = shader;
            m.renderQueue = backup.renderQueue;
            return backup;
        }

        public void Restore(Material m, EyeMaterialBackup backup)
        {
            m.shader = backup.shader;
            m.renderQueue = backup.renderQueue;
        }

        public static void Apply(Material m, EyeSettings s)
        {
            m.SetFloat(MToonProps.EyeUpperShadow, s.upperShadow);
            m.SetColor(MToonProps.EyeUpperShadowColor, ToColor(s.upperShadowColor));
            m.SetFloat(MToonProps.EyeUpperShadowHeight, s.upperShadowHeight);
            m.SetFloat(MToonProps.EyeUpperShadowSoftness, s.upperShadowSoftness);
            m.SetFloat(MToonProps.EyeLowerGlow, s.lowerGlow);
            m.SetColor(MToonProps.EyeLowerGlowColor, ToColor(s.lowerGlowColor));
            m.SetFloat(MToonProps.EyeLowerGlowHeight, s.lowerGlowHeight);
            m.SetFloat(MToonProps.EyeLowerGlowSoftness, s.lowerGlowSoftness);
            m.SetFloat(MToonProps.EyeSparkle, s.sparkle);
            var strength = Vector4.zero;
            for (var i = 0; i < EyeSettings.HighlightCount; i++)
            {
                var h = s.highlights[i];
                m.SetVector(MToonProps.EyeHighlights[i], new Vector4(h.x, h.y, h.size, h.softness));
                strength[i] = h.strength;
            }
            m.SetVector(MToonProps.EyeHighlightStrength, strength);
            m.SetFloat(MToonProps.EyeParallax, s.parallax);
        }

        public static void SetIrisRect(Material m, IrisRect r)
        {
            m.SetVector(MToonProps.EyeIrisRect, new Vector4(r.x, r.y, r.width, r.height));
        }

        public static void SetGaze(Material m, Vector3 gaze)
        {
            m.SetVector(MToonProps.EyeGaze, gaze);
        }

        /// <summary>GPU から同期で読み戻す。テクスチャか瞳の menu が変わったときだけ呼ぶ。見つからなければ false で rect は UV 全体</summary>
        public bool TryReadIrisRect(Texture texture, out IrisRect rect)
        {
            if (texture == null)
            {
                rect = IrisRect.Full;
                return false;
            }
            const int size = IrisBounds.SampleSize;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                if (_readback == null)
                {
                    _readback = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                    _alpha = new byte[size * size];
                }
                _readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                var pixels = _readback.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                {
                    _alpha[i] = pixels[i].a;
                }
                return IrisBounds.TryCompute(_alpha, size, size, IrisBounds.Threshold, out rect);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        public void Dispose()
        {
            if (_readback != null)
            {
                Object.Destroy(_readback);
                _readback = null;
            }
        }

        private static Color ToColor(ColorData c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
    }
}
```

- [ ] **Step 2: MaidEyeGloss を書く**

`source/COM3D25.MToonMaid.Plugin/MaidEyeGloss.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の瞳と eye_hi の板の差し替え。MToon の控え (MaidMToonController._entries) とは別に持ち、
    /// ランプや凹凸のマスクの処理に混ぜない
    /// </summary>
    public sealed class MaidEyeGloss
    {
        private sealed class Entry
        {
            public Material material;
            public EyeMaterialBackup backup;
            public TBodySkin skin;
            public int materialIndex;
            // true なら瞳、false なら eye_hi の板
            public bool isEye;
            // 外接矩形を求めたときの _MainTex と瞳の menu。どちらかが変わったら求め直す
            public int irisTextureId;
            public string irisEyeFile;
            // 外接矩形が見つからず鍵を記録しなかった回数
            public int irisMisses;
        }

        // 外接矩形が見つからないまま求め直す回数の上限。全面透明のテクスチャで毎走査 GPU から読み戻さないため
        private const int MaxIrisMisses = 5;

        private readonly Maid _maid;
        private readonly EyeConverter _converter;
        private readonly Action<string> _warn;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        // 例外になったマテリアル。毎回同じ警告を出さないよう以後は飛ばす
        private readonly HashSet<int> _failed = new HashSet<int>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _removeBuffer = new List<int>();
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        private readonly List<Material> _materialBuffer = new List<Material>();

        public MaidEyeGloss(Maid maid, EyeConverter converter, Action<string> warn)
        {
            _maid = maid;
            _converter = converter;
            _warn = warn;
        }

        public void Scan(EyeSettings settings)
        {
            RemoveDestroyed();
            if (!settings.enabled)
            {
                RestoreAll();
                return;
            }
            var skin = _maid.body0.GetSlot(ToonMaterialClassifier.HeadSlot);
            if (skin == null || skin.obj == null)
            {
                return;
            }
            var eyeFile = _maid.GetProp(MPN.eye).strFileName;
            var hasRightHighlight = !string.IsNullOrEmpty(_maid.GetProp(MPN.eye_hi_r).strFileName);

            _seen.Clear();
            skin.obj.GetComponentsInChildren(true, _rendererBuffer);
            foreach (var renderer in _rendererBuffer)
            {
                renderer.GetSharedMaterials(_materialBuffer);
                for (var k = 0; k < _materialBuffer.Count; k++)
                {
                    var m = _materialBuffer[k];
                    if (m == null)
                    {
                        continue;
                    }
                    var id = m.GetInstanceID();
                    _seen.Add(id);
                    if (_failed.Contains(id))
                    {
                        continue;
                    }
                    try
                    {
                        ScanMaterial(m, id, skin, k, settings, eyeFile, hasRightHighlight);
                    }
                    catch (Exception e)
                    {
                        _failed.Add(id);
                        _warn("瞳のマテリアルの差し替えに失敗したため以後は飛ばします: " + m.name + " " + e.Message);
                    }
                }
            }
            // 頭の付け替えで外れたマテリアル。破棄されていなければ元へ戻す
            RestoreWhere(e => !_seen.Contains(e.material.GetInstanceID()));
        }

        private void ScanMaterial(Material m, int id, TBodySkin skin, int index, EyeSettings settings, string eyeFile, bool hasRightHighlight)
        {
            Entry entry;
            _entries.TryGetValue(id, out entry);
            var category = skin.Category;
            if (ToonMaterialClassifier.IsEyeMaterial(category, index, m.shader.name) || (entry != null && entry.isEye))
            {
                ScanEye(m, id, entry, skin, index, settings, eyeFile);
                return;
            }
            // 消した後は今のシェーダーが Hidden なので、控えの元のシェーダー名で判定する
            var originalShader = entry != null ? entry.backup.shader : m.shader;
            var hide = settings.hideEyeHighlight && originalShader != null
                && ToonMaterialClassifier.IsEyeHighlightMaterial(category, index, hasRightHighlight, originalShader.name);
            if (hide)
            {
                if (entry == null)
                {
                    _entries[id] = new Entry { material = m, backup = _converter.ToHidden(m), skin = skin, materialIndex = index };
                }
                // menu の shader コマンド等で戻された
                else if (m.shader != _converter.HiddenShader)
                {
                    entry.backup = _converter.ToHidden(m);
                }
            }
            else if (entry != null)
            {
                RestoreEntry(entry);
                _entries.Remove(id);
            }
        }

        private void ScanEye(Material m, int id, Entry entry, TBodySkin skin, int index, EyeSettings settings, string eyeFile)
        {
            if (entry == null)
            {
                entry = new Entry { material = m, backup = _converter.ToEye(m), skin = skin, materialIndex = index, isEye = true };
                _entries[id] = entry;
                EyeConverter.Apply(m, settings);
            }
            else if (m.shader != _converter.EyeShader)
            {
                // menu の shader コマンドで元の瞳のシェーダーへ戻された。別のシェーダーにされたらそれに従い、控えから外す
                if (!ToonMaterialClassifier.IsEyeShader(m.shader.name))
                {
                    _entries.Remove(id);
                    return;
                }
                entry.backup = _converter.ToEye(m);
                EyeConverter.Apply(m, settings);
            }

            var texture = m.GetTexture(MToonProps.MainTex);
            var textureId = texture != null ? texture.GetInstanceID() : 0;
            if (textureId != entry.irisTextureId || eyeFile != entry.irisEyeFile)
            {
                IrisRect rect;
                var found = _converter.TryReadIrisRect(texture, out rect);
                EyeConverter.SetIrisRect(m, rect);
                // 無限色の RT がまだ描かれていないと全面透明に見える。鍵を記録せず次の走査で求め直す
                if (found || ++entry.irisMisses >= MaxIrisMisses)
                {
                    entry.irisTextureId = textureId;
                    entry.irisEyeFile = eyeFile;
                    entry.irisMisses = 0;
                }
            }
        }

        /// <summary>ウィンドウで値を変えた。差し替えの有無は次の走査で合わせる</summary>
        public void Apply(EyeSettings settings)
        {
            foreach (var e in _entries.Values)
            {
                if (e.isEye && e.material != null && e.material.shader == _converter.EyeShader)
                {
                    EyeConverter.Apply(e.material, settings);
                }
            }
        }

        /// <summary>カメラの描画の前に呼ぶ (フレームに 1 回)。視線をワールドで瞳へ入れる</summary>
        public void UpdateGaze()
        {
            var body = _maid.body0;
            if (_entries.Count == 0 || body == null)
            {
                return;
            }
            foreach (var e in _entries.Values)
            {
                if (e.isEye && e.material != null && e.material.shader == _converter.EyeShader)
                {
                    EyeConverter.SetGaze(e.material, ComputeGaze(body, e.materialIndex));
                }
            }
        }

        /// <summary>目のボーンが無ければ 0 (シェーダーでは視差なし)</summary>
        private static Vector3 ComputeGaze(TBody body, int materialIndex)
        {
            var left = materialIndex == ToonMaterialClassifier.LeftEyeMaterialIndex;
            var eye = left ? body.trsEyeL : body.trsEyeR;
            if (eye == null || eye.parent == null || body.trsHead == null)
            {
                return Vector3.zero;
            }
            var rest = left ? body.quaDefEyeL : body.quaDefEyeR;
            // 目のボーンの軸をそのまま使うと、外向きに傾けて置かれた分だけ視線が常に外へずれる。既定の向きからの回転だけを顔の正面に掛ける
            var delta = eye.rotation * Quaternion.Inverse(eye.parent.rotation * rest);
            return delta * body.trsHead.up;
        }

        public void RestoreAll()
        {
            foreach (var e in _entries.Values)
            {
                RestoreEntry(e);
            }
            _entries.Clear();
            _failed.Clear();
        }

        /// <summary>メイドごと破棄された。マテリアルも破棄済みなので戻さずに控えだけを捨てる</summary>
        public void Forget()
        {
            _entries.Clear();
            _failed.Clear();
        }

        private void RestoreEntry(Entry e)
        {
            if (e.material == null)
            {
                return;
            }
            var current = e.material.shader;
            _converter.Restore(e.material, e.backup);
            GameShaderBackup.Replace(e.skin, e.materialIndex, current, e.backup.shader);
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

        private void RestoreWhere(Func<Entry, bool> predicate)
        {
            _removeBuffer.Clear();
            foreach (var pair in _entries)
            {
                if (predicate(pair.Value))
                {
                    _removeBuffer.Add(pair.Key);
                }
            }
            foreach (var id in _removeBuffer)
            {
                RestoreEntry(_entries[id]);
                _entries.Remove(id);
            }
        }
    }
}
```

- [ ] **Step 3: コントローラーから呼ぶ**

`MaidMToonController.cs`:

- フィールドに足す: `private readonly MaidEyeGloss _eyeGloss;`（`// 瞳のシェーダーを読めなければ null`）
- コンストラクタを `(Maid maid, string guid, MaterialConverter converter, Shader hairShadowCaster, EyeConverter eyeConverter, Action<string> warn)` にし（`warn` の前に `eyeConverter`。`eyeConverter` は null 可）、本体の末尾で `_eyeGloss = eyeConverter != null ? new MaidEyeGloss(maid, eyeConverter, warn) : null;`
- `Scan(MaidSettings settings)` のスロットの二重ループの後に `_eyeGloss?.Scan(settings.eye);`
- `RestoreAll()` の `ReleaseHairShadow();` の次に `_eyeGloss?.RestoreAll();`
- `DestroyOwnedTextures()` の `ReleaseHairShadow();` の次に `_eyeGloss?.Forget();`
- `RequestScan()` の後に足す:

```csharp
        /// <summary>瞳の設定を変えた。値はすぐ入れ、差し替えの有無 (ON/OFF・eye_hi) は間隔を待たずに走査で合わせる</summary>
        public void ApplyEye(EyeSettings settings)
        {
            _eyeGloss?.Apply(settings);
            _scanRequested = true;
        }

        /// <summary>カメラの描画の前に呼ぶ (フレームに 1 回)。目の動きに合わせて視線を入れる</summary>
        public void UpdateEyeGaze()
        {
            _eyeGloss?.UpdateGaze();
        }
```

- [ ] **Step 4: プラグインとホストに足す**

`IMToonMaidHost.cs` の `IsHairShadowAvailable` の次と末尾に足す:

```csharp
        /// <summary>瞳の光沢のシェーダーを読めたか。false ならウィンドウの目タブを無効表示にする</summary>
        bool IsEyeGlossAvailable { get; }
```

```csharp
        /// <summary>瞳の設定を変えた。保存し、値を入れて間隔を待たずに走査させる</summary>
        void NotifyEyeChanged(Maid maid);
```

`MToonMaidPlugin.cs`:

- フィールド `_hairShadowCaster` の次に `// 瞳の光沢。シェーダーを読めなければ null` `private EyeConverter _eyeConverter;`
- `_hairShadowFrame` を `_preCullFrame` に改名し、コメントを「前髪の影と瞳の視線を更新したフレーム。カメラが複数あってもフレームに 1 回にする」に直す（使っている 2 か所も直す）
- `public bool IsEyeGlossAvailable => _eyeConverter != null;` を `IsHairShadowAvailable` の次に足す
- `LoadShader` の `_hairShadowCaster = shaders.hairShadowCaster;` の次に:

```csharp
                if (shaders.eye != null)
                {
                    _eyeConverter = new EyeConverter(shaders.eye, shaders.hidden);
                }
```

- `OnDestroy` の `foreach` の後に `_eyeConverter?.Dispose();`
- `OnCameraPreCull` の `<summary>` を「揺れもの (前髪の物理) と目の動きが LateUpdate で決まった後に描くため、カメラの描画の直前に前髪の影と瞳の視線を更新する。カメラが複数あってもフレームに 1 回にする」に直し、`try` の中の `RenderHairShadow` の**後**に `pair.Value.UpdateEyeGaze();` を足す（前に置くと、視線の例外で同じフレームの前髪の影が描かれない）。警告の文面を「前髪の影・瞳の視線の更新に失敗しました: 」に直す
- `AttachControllers` の `new MaidMToonController(maid, guid, _converter, _hairShadowCaster, Log.LogWarning)` を `new MaidMToonController(maid, guid, _converter, _hairShadowCaster, _eyeConverter, Log.LogWarning)` に直す
- `NotifyTargetsChanged` の後に足す:

```csharp
        public void NotifyEyeChanged(Maid maid)
        {
            _store.MarkDirty(maid.status.guid, Time.realtimeSinceStartup);
            MaidMToonController controller;
            if (_controllers.TryGetValue(maid, out controller))
            {
                controller.ApplyEye(GetSettings(maid).eye);
            }
        }
```

- [ ] **Step 5: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: 成功（`MToonMaidWindow` は `IMToonMaidHost` の新しいメンバーを使わないのでそのまま通る）

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 6: コミットする**

commit スキルで `feat: 瞳を光沢のシェーダーへ差し替え、eye_hi の板を消す` としてコミットする。

---

### Task 6: ウィンドウに目タブを足す

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`

**Interfaces:**
- Consumes: Task 1 の `EyeSettings` / `EyeHighlightSettings`、Task 5 の `IMToonMaidHost.IsEyeGlossAvailable` / `NotifyEyeChanged`

- [ ] **Step 1: タブと状態を足す**

- `TabWidth` を `52f` にする（6 個で最小幅 380 に収める）
- `TabLabels` を `{ "共通", "肌", "顔", "髪", "衣装", "目" }` にし、`CommonTab` の次に `// 最後のタブは瞳。PartGroup に対応しない` `private const int EyeTab = 5;`
- `_default` の次に `private readonly EyeSettings _eyeDefault = EyeSettings.CreateDefault();`
- `_targetsChanged` の次に `private bool _eyeChanged;`

- [ ] **Step 2: DrawMain を分ける**

`DrawMain` のタブから `EndScrollView` までを次に置き換える:

```csharp
            // string[] のままだと汎用版 DrawTabs<T>(T currentTab, ...) が選ばれるため IList<string> で渡す
            _tab = view.DrawTabs((IList<string>)TabLabels, _tab, TabWidth, RowHeight);
            var eyeTab = _tab == EyeTab;
            _editGroup = _tab == CommonTab || eyeTab ? (PartGroup?)null : (PartGroup)(_tab - 1);
            if (eyeTab)
            {
                var eye = _settings.eye;
                view.DrawToggle("瞳を光沢のシェーダーにする", eye.enabled, -1, RowHeight, _host.IsEyeGlossAvailable && _baseEnabled, v =>
                {
                    eye.enabled = v;
                    _eyeChanged = true;
                });
            }
            else if (_editGroup.HasValue)
            {
                var g = _settings.Get(_editGroup.Value);
                view.DrawToggle("このグループを MToon にする", g.enabled, -1, RowHeight, v =>
                {
                    g.enabled = v;
                    _targetsChanged = true;
                });
            }

            view.DrawHorizontalLine(Color.gray);
            view.AddSpace(ScrollTopSpace);
            view.BeginScrollView();
            if (eyeTab)
            {
                DrawEyeSettings(view);
                DrawEyeResetButton(view);
            }
            else
            {
                DrawSettings(view);
                DrawResetButton(view);
                if (_editGroup.HasValue)
                {
                    // スロットの数はメイドや着替えで変わるため、入力欄キャッシュの番号がずれないようスライダーより後に描く
                    DrawSlots(view, maid, _editGroup.Value);
                }
            }
            view.EndScrollView();
```

- [ ] **Step 3: 目タブの行を描く**

`DrawSlots` の後に足す:

```csharp
        /// <summary>瞳の行。ON/OFF やシェーダーの有無で行を増減させず無効表示にする (入力欄キャッシュの番号がずれるため)</summary>
        private void DrawEyeSettings(GUIView view)
        {
            var eye = _settings.eye;
            var def = _eyeDefault;
            var editable = _host.IsEyeGlossAvailable && eye.enabled;
            if (!_host.IsEyeGlossAvailable)
            {
                // DrawLabel は入力欄キャッシュを使わないため、条件つきで描いても番号はずれない
                view.DrawLabel("瞳のシェーダーを読めないため使えません (BepInEx のログを確認してください)", -1, RowHeight);
            }
            EyeToggle(view, "eye_hi を消す", eye.hideEyeHighlight, v => eye.hideEyeHighlight = v, editable);

            view.DrawLabel("上まぶたの影", -1, RowHeight);
            EyeSlider(view, "強さ", eye.upperShadow, 0f, 1f, def.upperShadow, v => eye.upperShadow = v, editable);
            EyeColorRow(view, "目/上まぶたの影の色", "色", eye.upperShadowColor, def.upperShadowColor, editable);
            EyeSlider(view, "高さ", eye.upperShadowHeight, 0f, 1f, def.upperShadowHeight, v => eye.upperShadowHeight = v, editable);
            EyeSlider(view, "ぼかし", eye.upperShadowSoftness, 0f, 1f, def.upperShadowSoftness, v => eye.upperShadowSoftness = v, editable);

            view.DrawLabel("下側の明るさ", -1, RowHeight);
            EyeSlider(view, "強さ", eye.lowerGlow, 0f, 1f, def.lowerGlow, v => eye.lowerGlow = v, editable);
            EyeColorRow(view, "目/下側の明るさの色", "色", eye.lowerGlowColor, def.lowerGlowColor, editable);
            EyeSlider(view, "高さ", eye.lowerGlowHeight, 0f, 1f, def.lowerGlowHeight, v => eye.lowerGlowHeight = v, editable);
            EyeSlider(view, "ぼかし", eye.lowerGlowSoftness, 0f, 1f, def.lowerGlowSoftness, v => eye.lowerGlowSoftness = v, editable);
            EyeSlider(view, "きらめき", eye.sparkle, 0f, 1f, def.sparkle, v => eye.sparkle = v, editable);

            for (var i = 0; i < EyeSettings.HighlightCount; i++)
            {
                var h = eye.highlights[i];
                var hd = def.highlights[i];
                view.DrawLabel("ハイライト " + (i + 1), -1, RowHeight);
                EyeSlider(view, "強さ", h.strength, 0f, 1f, hd.strength, v => h.strength = v, editable);
                EyeSlider(view, "左右", h.x, -EyeSettings.MaxHighlightOffset, EyeSettings.MaxHighlightOffset, hd.x, v => h.x = v, editable);
                EyeSlider(view, "上下", h.y, -EyeSettings.MaxHighlightOffset, EyeSettings.MaxHighlightOffset, hd.y, v => h.y = v, editable);
                EyeSlider(view, "大きさ", h.size, 0f, EyeSettings.MaxHighlightSize, hd.size, v => h.size = v, editable);
                EyeSlider(view, "ぼかし", h.softness, 0f, 1f, hd.softness, v => h.softness = v, editable);
            }

            view.DrawLabel("ハイライトの動き", -1, RowHeight);
            EyeSlider(view, "視差", eye.parallax, 0f, 1f, def.parallax, v => eye.parallax = v, editable);
        }

        private void DrawEyeResetButton(GUIView view)
        {
            if (view.DrawButton("目の調整値を既定に戻す", ResetButtonWidth, RowHeight))
            {
                // ON/OFF は調整値ではないため残す
                var enabled = _settings.eye.enabled;
                _settings.eye = EyeSettings.CreateDefault();
                _settings.eye.enabled = enabled;
                _eyeChanged = true;
            }
        }

        private void EyeSlider(GUIView view, string label, float value, float min, float max, float defaultValue, Action<float> set, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = LabelWidth,
                width = -1,
                min = min,
                max = max,
                defaultValue = defaultValue,
                value = value,
                onChanged = v =>
                {
                    set(v);
                    _eyeChanged = true;
                },
            });
            view.SetEnabled(_baseEnabled);
        }

        private void EyeToggle(GUIView view, string label, bool value, Action<bool> set, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            view.DrawToggle(label, value, -1, RowHeight, v =>
            {
                set(v);
                _eyeChanged = true;
            });
            view.SetEnabled(_baseEnabled);
        }

        /// <summary>colorKey はカラーピッカーの同定に使うため、他のタブの色の行と重複させない</summary>
        private void EyeColorRow(GUIView view, string colorKey, string label, ColorData color, ColorData defaultColor, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            view.DrawColor(view.GetColorFieldCache(colorKey, false), ToColor(color), ToColor(defaultColor), c =>
            {
                color.r = c.r;
                color.g = c.g;
                color.b = c.b;
                _eyeChanged = true;
            }, label);
            view.SetEnabled(_baseEnabled);
        }
```

- [ ] **Step 4: 変更を本体へ伝える**

`Commit` の `if (_targetsChanged) { … }` の後に足す:

```csharp
            if (_eyeChanged)
            {
                _eyeChanged = false;
                _host.NotifyEyeChanged(maid);
            }
```

クラスの `<summary>` を「メイドの選択・ON/OFF・共通と部位グループごとの調整値・対象スロット・瞳の光沢のウィンドウ」に直す。

- [ ] **Step 5: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: 成功

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 6: コミットする**

commit スキルで `feat: ウィンドウに目タブを追加` としてコミットする。

---

### Task 7: 実機で確かめ、既定値と文書を合わせる

**Files:**
- Modify: 実機で直す必要が出たファイル、`README.md`、`docs/superpowers/specs/2026-10-09-mtoon-maid-eye-gloss-design.md`

`com3d25-devbridge:restart-verify` スキルの手順で DLL を反映して起動する。**ゲームを終了する前に、ユーザーへシーンの保存の有無を明示的に確認し、回答を ledger に書く。** 設定を変える前に、そのメイドの今の `eye` の値を控え、最後に戻す（新しい機能なので既定 OFF のはず）。

- [ ] **Step 1: 反映して起動する**

`debug.bat com3d25` でビルドしてゲームへコピーし（ゲーム停止中に実行）、ゲームを起動してセーブをロードする。`tail_log` で `瞳の光沢` を含むエラーが無いこと、`MToonMaid 有効化` が出ていることを確かめる。

- [ ] **Step 2: 差し替えを確かめる**

ウィンドウの目タブで「瞳を光沢のシェーダーにする」を ON にし、`eval_csharp` で head スロットのマテリアルを列挙する（Explore 段階で使ったのと同じ式: `maid.body0.goSlot[(int)TBody.hashSlotName["head"], 0].obj.GetComponentsInChildren<UnityEngine.Renderer>(true)` の `sharedMaterials` の `name` / `shader.name` / `renderQueue`）。
Expected: 番号 0 / 1 が `COM3D25/MToonMaidEye` で renderQueue 3000、番号 2 が `Hidden/COM3D25/MToonMaidHidden` で renderQueue 3000（元の値。2026-10-09 の実機では SkinHi も 3000）、他は変わらない（番号 7 の `bukkake` もそのまま）。瞳の `_IrisRect` が UV 全体（0, 0, 1, 1）ではない。セーブのロード直後に ON のままのメイドでも、数回の走査のうちに UV 全体でなくなる

- [ ] **Step 3: 見た目と描画順を確かめる**

`screenshot` で顔を撮り、ユーザーに見てもらう。確かめること:
- 瞳が描かれている（消えていれば `Cull Back` を `Cull Off` にしてバンドルを作り直す）
- まつ毛・眉・涙（表情で涙を出す）が元と同じく瞳の手前に出る
- 上まぶたの影・下側の明るさ・きらめき・ハイライト 3 個が瞳の内側にだけ出る
- ハイライトが左右の目で画面上の同じ側（既定で左上が大きい丸）に出る。カメラを回して頭を傾けた角度から見ても崩れない

- [ ] **Step 4: 視線と視差を確かめる**

`eval_csharp` で左目のマテリアル（番号 0）の `_EyeGaze` を読み、`body0.trsHead.up` とほぼ同じ向き（目が正面のとき内積 0.95 以上）になることを確かめる。顔をカメラに向けたまま、目線の設定（夜伽・エディットの「目線」）で目を左右に動かし、ハイライトが瞳の中で反対側へ寄ることを確かめる。左右で逆に動いたら、番号 0 と `trsEyeL` の対応（`LeftEyeMaterialIndex`）を見直す。

- [ ] **Step 5: 外接矩形の求め直しと eye_hi を確かめる**

- エディットで瞳（`eye`）の種類を替え、次の走査で `_IrisRect` が変わり、ハイライトが新しい瞳の中に収まることを確かめる
- 「eye_hi を消す」を OFF にすると番号 2 が元のシェーダー（`CM3D2/Lighted_Trans`）と renderQueue へ戻り、ON で再び消えることを確かめる

- [ ] **Step 6: OFF で元へ戻ることを確かめる**

- 目タブの OFF で、番号 0 / 1 / 2 が Step 2 の前と同じシェーダー・renderQueue に戻る
- 目タブを ON に戻してから、メイドの「MToon にする」を OFF にしても同じく戻る
- `eval_csharp` で head の `TBodySkin` の `m_dicBackupShader`（リフレクション）に `MToonMaidEye` / `MToonMaidHidden` が残っていない

- [ ] **Step 7: 既定値を詰める**

ユーザーと一緒に参考画像に近づけるよう既定値を調整する。決まった値を `EyeSettings`（フィールド初期値と `CreateDefaultHighlight`）・`MToonMaidEye.shader` の Properties の既定・`EyeSettingsTests` の期待値へ反映し、テストを通す。変えたら spec の表も直す。最後にユーザーの元の設定へ戻す。

- [ ] **Step 8: 文書を直す**

`README.md`:
- 使い方の 4 の後に足す: 「6. 「目」タブで「瞳を光沢のシェーダーにする」を入れると、瞳に上まぶたの影・下側の明るさときらめき・ハイライト（3 個）を重ねます。ハイライトはどの角度から見ても瞳の同じ側（画面基準）に出て、目の動きに合わせて少し滑ります（「視差」）。「eye_hi を消す」でエディットのハイライト（eye_hi）を外せます」（既存の 5 は番号を繰り下げない。6 として末尾に足す）
- 制限の「目・まつ毛などの半透明マテリアルは置き換えません」を「まつ毛・眉などの半透明マテリアルは置き換えません。瞳は目タブで光沢のシェーダーにできます（`CM3D2/Toony_Lighted_Trans` の瞳だけ。`_NoZ` などの派生シェーダーの瞳は対象外）」に直す

spec（`2026-10-09-mtoon-maid-eye-gloss-design.md`）を実装に合わせて直す:
- ハイライトの板の判定: 番号 7 は `MPN.eye_hi_r` のファイル名が空でないときだけ（ゲームが `eye_hi_r` の「テクスチャ変更」を番号 7 へ向け直す。古い顔の番号 7 は `bukkake`）
- 瞳の判定はシェーダー名の完全一致（`_NoZ` を含めない）
- 視線: 目のボーンは外向きに傾けて置かれるため、`quaDefEye` からの回転を `trsHead.up` に掛けて求める（実機で確かめた）
- ハイライトの板の判定に、元のシェーダー名が `CM3D2/Lighted_Trans` であることを足す（顔 MOD の番号 2 や、古い顔に `eye_hi_r` の値だけが残っているときの番号 7 を消さない）
- 値の丸めは読み込み時（`FillMissing`）に行う
- 外接矩形が見つからなければ鍵を記録せず次の走査で求め直す（5 回まで）
- 視差は視線のビュー座標の横と縦の両方に効く。シェーダーへ渡す名前は `_EyeForward` ではなく `_EyeGaze`（既定の向きからの回転を掛けた視線）
- 追加ライトのパスでは上まぶたの影と下側の明るさだけを描き、ハイライトときらめきは ForwardBase だけで描く
- 後片付けからプラグインの終了時を外す（既存の MToon と同じく、終了時は戻さない）。縮小用の RT は求めるたびに一時確保して返し、読み戻し用の Texture2D はプラグインの終了時に破棄する
- 既定値を Step 7 で変えたらその値

- [ ] **Step 9: コミットする**

commit スキルで、実機で直したコードは `fix: …`、既定値は `feat: 瞳の光沢の既定値を実機に合わせる`、文書は `docs: 瞳の光沢の使い方と実機検証の結果を追加` として分けてコミットする。

## レビュー却下メモ

- head スロットを `GetSlot("head")`（sub 0）だけ走査する点 — 2026-10-09 に実機で head の `goSlot.CountChildren` が 1 と確かめたため、全 sub の走査は不要（Global Constraints に記載）
