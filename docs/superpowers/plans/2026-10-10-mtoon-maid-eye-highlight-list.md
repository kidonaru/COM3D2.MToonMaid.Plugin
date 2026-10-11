# COM3D25.MToonMaid.Plugin 瞳のハイライトの追加・削除と映り込み Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 瞳のハイライトを 0〜8 個の可変のリストにし（位置 X/Y・大きさ W/H・ロール・個別のにじみの色と幅）、白い芯をくっきり描いてその外側を色付きでぼかす。ハイライトは白目へはみ出してよい。きらめきを削除し、半透明の映り込み（角の丸い四角）を足す。目タブを PostEffects のパラフィンのデータ番号の行と同じ作りにする。

**Architecture:** 設定（`EyeSettings` / `EyeHighlightSettings` / 新しい `EyeReflectionSettings`）の補完と丸めは単体テストする。シェーダーはハイライトを固定長 8 の配列（`SetVectorArray`）と個数で受ける。`EyeConverter.Apply` が配列を詰める。目タブは自前の 2 値の行（ドラッグラベル + 入力欄 ×2）とデータ番号の行を描く。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（シェーダーのバンドル）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-eye-gloss-design.md` の「改訂 2026-10-10」節（それより前の節のハイライト・きらめきの記述より優先）

## Global Constraints

- v2.9 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-eye-gloss.md`）の Global Constraints を引き継ぐ（main で作業、コメント・ログは日本語、`deploy.bat` は実行しない、MTEUtils は編集しない、`build-bundle.bat` は Unity エディタを閉じて実行、ゲームを終了する前にシーンの保存の有無をユーザーに確認する）
- ハイライトの上限は 8（`EyeSettings.MaxHighlightCount`）。シェーダーの配列長と一致させる
- `highlights` 要素が無いファイルは既定の 3 個、空の要素は 0 個として読む（XmlSerializer は空要素から空のリストを作るので、null のときだけ既定で埋める）
- 旧形式（`size` / `softness` / `highlightGlowColor` / `sparkle`）からの移行は作らない
- 目タブの行は条件で増減させない（ハイライトが 0 個でも、選んだハイライトの行を無効表示で描く）
- ハイライトは瞳のアルファで切り抜かない（白目へはみ出す）。ForwardBase の出力アルファ = `瞳のアルファ + ハイライトの濃さ × (1 - 瞳のアルファ)`、色 = `(瞳の色 × 瞳のアルファ + ハイライトの色) / アルファ`
- 視差は -1〜1（マイナスで目の向きと同じ側へ滑る）。NaN は 0
- 範囲: 位置 ±0.5、ハイライトの W/H 0〜1、ロール ±180、にじみの幅 0〜0.3、映り込みの W/H 0〜1、その他 0〜1。NaN は下限に丸める（既存の `Clamp` を使う）

## Review Focus

- **ハイライト 0 個**: 0 個でシェーダーが何も描かず、UI の行が消えず（無効表示）、[削除] が無効 → Task 1 のテスト（空要素で 0 個）、Task 3 Step 2
- **8 個の上限**: 9 個以上のファイルは 8 個に切り詰め、[追加] は 8 個で無効。シェーダーの配列長を超えない → Task 1 のテスト、Task 2 の `Apply`
- **配列の長さの固定**: `SetVectorArray` は最初に渡した長さでシェーダー側の配列が決まるため、個数が少なくても常に 8 要素で渡す → Task 2 の `Apply`
- **選択中の番号のずれ**: 削除・個数が減るファイルの読み直し・メイドの切り替えで、選択中の番号が範囲外にならない（番号の行より前で寄せる。範囲外のまま [削除] を押すと `RemoveAt` が例外になる） → Task 3 の `ClampHighlightIndex` の呼び出し位置
- **全削除の保存（Mono）**: 0 個を保存して再起動しても 0 個のまま（ゲームの XmlSerializer は Mono 実装で、テストの .NET Framework と空要素の扱いが違うかもしれない） → Task 1 のテスト、Task 4 Step 2
- **白目へのはみ出し**: 瞳の外でハイライトが白目の上に出て、瞳の中の見た目（アルファ 1 の所）は変わらない。まぶたで隠れる → Task 2 Step 2、Task 4 Step 3
- **くっきりした芯**: 芯の縁は約 1 画素ぶんだけなめらかで、拡大しても縁がぼけない（`fwidth` は分岐の外で求める） → Task 2、Task 4 の実機確認

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/EyeSettings.cs        (Modify: ハイライトの値の作り直し・映り込み・きらめきの削除)
├─ Rendering/EyeConverter.cs      (Modify: 配列と映り込みを入れる)
├─ Rendering/MToonProps.cs        (Modify: プロパティ名)
└─ UI/MToonMaidWindow.cs          (Modify: 映り込みの行・ハイライトのデータ番号の行・2 値の行)
source/COM3D25.MToonMaid.Plugin.Tests/EyeSettingsTests.cs (Modify)
UnityProject2022/Assets/Shaders/MToonMaid/MToonMaidEye.shader (Modify)
README.md                                                  (Modify)
```

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / バンドル: `build-bundle.bat`

---

### Task 1: 設定の形を変える

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/EyeSettings.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/EyeSettingsTests.cs`

**Interfaces:**
- Produces: `EyeHighlightSettings { float strength, x, y, width, height, roll, glowWidth; ColorData glowColor; }`、`EyeReflectionSettings { float strength, x, y, width, height, roundness, softness; ColorData color; }`、`EyeSettings.reflection`、`EyeSettings.MaxHighlightCount = 8`、`EyeSettings.MaxHighlightOffset = 0.5f`、`EyeSettings.MaxHighlightSize = 1f`、`EyeSettings.MaxHighlightRoll = 180f`、`EyeSettings.MaxGlowWidth = 0.3f`、`EyeSettings.MaxParallax = 1f`（視差は ±1）、`EyeSettings.CreateNewHighlight()`（[追加] で足す既定。行のリセット先にも使う）。既定の 3 個は `FillMissing` の中だけで作る（`CreateDefaultHighlight` は private）。`sparkle` / `highlightGlowColor` / `HighlightCount` は削除

- [ ] **Step 1: テストを書き直す（RED）**

`EyeSettingsTests` を次の内容にする（ヘルパー `LoadXml` と `Dispose` はそのまま）:

```csharp
        [Fact]
        public void eye要素の無いファイルはOFFの既定()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><enabled>true</enabled></MToonMaid>");

            Assert.NotNull(s.eye);
            Assert.False(s.eye.enabled);
            Assert.True(s.eye.hideEyeHighlight);
            Assert.Equal(0.75f, s.eye.upperShadow);
            Assert.NotNull(s.eye.upperShadowColor);
            Assert.NotNull(s.eye.lowerGlowColor);
            Assert.NotNull(s.eye.reflection);
            Assert.NotNull(s.eye.reflection.color);
            Assert.Equal(0.25f, s.eye.reflection.strength);
            Assert.Equal(3, s.eye.highlights.Count);
            Assert.All(s.eye.highlights, h => Assert.NotNull(h.glowColor));
            Assert.Equal(1f, s.eye.highlights[0].strength);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 空のhighlightsは0個のまま()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><eye><highlights /></eye></MToonMaid>");

            Assert.Empty(s.eye.highlights);
            s.eye.FillMissing();
            Assert.Empty(s.eye.highlights);
        }

        [Fact]
        public void 0個を保存して読み直しても0個()
        {
            var path = Path.Combine(_dir, "empty.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.eye.highlights.Clear();

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(loaded.eye.highlights);
        }

        [Fact]
        public void 既定のハイライトと映り込みは範囲内で丸めても変わらない()
        {
            var def = EyeSettings.CreateDefault();
            var before = def.highlights.ConvertAll(h => new[] { h.strength, h.x, h.y, h.width, h.height, h.roll, h.glowWidth });

            def.FillMissing();

            for (var i = 0; i < before.Count; i++)
            {
                var h = def.highlights[i];
                Assert.Equal(before[i], new[] { h.strength, h.x, h.y, h.width, h.height, h.roll, h.glowWidth });
            }
        }

        [Fact]
        public void 色の無い映り込みは既定の色で埋める()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><eye><reflection><strength>0.4</strength></reflection></eye></MToonMaid>");

            Assert.Equal(0.4f, s.eye.reflection.strength);
            Assert.NotNull(s.eye.reflection.color);
        }

        [Fact]
        public void 上限を超えるハイライトは捨て欠けた値は新規の既定で埋める()
        {
            var item = "<EyeHighlightSettings><x>0.1</x></EyeHighlightSettings>";
            var xml = "<?xml version=\"1.0\"?><MToonMaid version=\"3\"><eye><highlights>";
            for (var i = 0; i < EyeSettings.MaxHighlightCount + 2; i++)
            {
                xml += item;
            }
            var s = LoadXml(xml + "</highlights></eye></MToonMaid>");

            Assert.Equal(EyeSettings.MaxHighlightCount, s.eye.highlights.Count);
            var h = s.eye.highlights[0];
            var def = EyeSettings.CreateNewHighlight();
            Assert.Equal(0.1f, h.x);
            Assert.Equal(def.width, h.width);
            Assert.Equal(def.glowWidth, h.glowWidth);
            Assert.NotNull(h.glowColor);
        }

        [Fact]
        public void 範囲外の値とNaNは丸める()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><eye>"
                + "<upperShadow>5</upperShadow><parallax>NaN</parallax>"
                + "<reflection><strength>3</strength><width>-1</width><roundness>NaN</roundness></reflection>"
                + "<highlights><EyeHighlightSettings><x>-3</x><width>4</width><roll>400</roll><glowWidth>1</glowWidth></EyeHighlightSettings></highlights>"
                + "</eye></MToonMaid>");

            Assert.Equal(1f, s.eye.upperShadow);
            // 視差の NaN は下限 (-1) ではなく 0 (視差なし)
            Assert.Equal(0f, s.eye.parallax);
            Assert.Equal(1f, s.eye.reflection.strength);
            Assert.Equal(0f, s.eye.reflection.width);
            Assert.Equal(0f, s.eye.reflection.roundness);
            var h = s.eye.highlights[0];
            Assert.Equal(-EyeSettings.MaxHighlightOffset, h.x);
            Assert.Equal(EyeSettings.MaxHighlightSize, h.width);
            Assert.Equal(EyeSettings.MaxHighlightRoll, h.roll);
            Assert.Equal(EyeSettings.MaxGlowWidth, h.glowWidth);
        }

        [Theory]
        [InlineData("-0.5", -0.5f)]
        [InlineData("-3", -1f)]
        [InlineData("3", 1f)]
        public void 視差はマイナスを許し範囲へ丸める(string value, float expected)
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"3\"><eye><parallax>" + value + "</parallax></eye></MToonMaid>");

            Assert.Equal(expected, s.eye.parallax);
        }

        [Fact]
        public void 保存して読み直すと同じ値になる()
        {
            var path = Path.Combine(_dir, "eye.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.eye.enabled = true;
            s.eye.reflection.y = -0.3f;
            s.eye.highlights[1].roll = 45f;
            s.eye.highlights[1].glowColor = new ColorData(0.4f, 0.5f, 0.6f, 1f);
            s.eye.highlights.RemoveAt(2);

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.True(loaded.eye.enabled);
            Assert.Equal(-0.3f, loaded.eye.reflection.y);
            Assert.Equal(2, loaded.eye.highlights.Count);
            Assert.Equal(45f, loaded.eye.highlights[1].roll);
            Assert.Equal(0.5f, loaded.eye.highlights[1].glowColor.g);
            Assert.Empty(_warnings);
        }
```

（旧テスト `ハイライトの不足は既定で埋め多すぎる分は捨てる` / `範囲外の値は丸める` / `NaNは範囲の下限にする` は上の 3 件に置き換えて削除する）

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルドエラー（`reflection` / `MaxHighlightCount` / `CreateNewHighlight` / `glowColor` などが無い）

- [ ] **Step 2: 実装する**

`EyeSettings.cs` を次の内容に置き換える:

```csharp
using System;
using System.Collections.Generic;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// 瞳のハイライト 1 個分。位置・大きさは瞳の幅を 1 とした値で、位置は瞳の中心から画面の右・上がプラス。
    /// 大きさは楕円の直径、ロールは度 (画面上で反時計回りがプラス)、にじみの幅は楕円の縁から外への幅
    /// </summary>
    public class EyeHighlightSettings
    {
        // XML で要素が欠けたときの値。新しく作るときの既定は EyeSettings.CreateDefaultHighlight / CreateNewHighlight
        public float strength = 1f;
        public float x;
        public float y;
        public float width = 0.1f;
        public float height = 0.1f;
        public float roll;
        public ColorData glowColor;
        public float glowWidth = 0.05f;
    }

    /// <summary>瞳の映り込み (角の丸い四角の半透明の面)。位置・大きさは瞳の幅を 1 とした値</summary>
    public class EyeReflectionSettings
    {
        public float strength = 0.25f;
        public ColorData color;
        public float x;
        public float y = -0.22f;
        public float width = 0.6f;
        public float height = 0.28f;
        public float roundness = 1f;
        public float softness = 0.5f;
    }

    /// <summary>
    /// メイド 1 人分の瞳の光沢の設定。共通の値の継承には乗せない。
    /// 参照型 (色・ハイライト・映り込み) は null のまま読み、FillMissing で補う
    /// </summary>
    public class EyeSettings
    {
        // シェーダーの配列の長さと揃える
        public const int MaxHighlightCount = 8;
        private const int DefaultHighlightCount = 3;
        public const float MaxHighlightOffset = 0.5f;
        public const float MaxHighlightSize = 1f;
        public const float MaxHighlightRoll = 180f;
        public const float MaxGlowWidth = 0.3f;
        public const float MaxParallax = 1f;

        public bool enabled;
        public bool hideEyeHighlight = true;

        public float upperShadow = 0.75f;
        public ColorData upperShadowColor;
        public float upperShadowHeight = 0.5f;
        public float upperShadowSoftness = 0.6f;

        public float lowerGlow = 0.55f;
        public ColorData lowerGlowColor;
        public float lowerGlowHeight = 0.5f;
        public float lowerGlowSoftness = 0.7f;

        public EyeReflectionSettings reflection;

        // 初期値を入れておくと XmlSerializer が読んだ要素を後ろへ足し、既定と二重になる
        public List<EyeHighlightSettings> highlights;

        public float parallax = 0.3f;

        public static EyeSettings CreateDefault()
        {
            var s = new EyeSettings();
            s.FillMissing();
            return s;
        }

        /// <summary>null の参照型を既定で埋め、ハイライトを上限へ切り詰め、値を範囲へ丸める</summary>
        public void FillMissing()
        {
            if (upperShadowColor == null)
            {
                upperShadowColor = new ColorData(0x40 / 255f, 0x30 / 255f, 0x6A / 255f, 1f);
            }
            if (lowerGlowColor == null)
            {
                lowerGlowColor = new ColorData(0xC7 / 255f, 0xD6 / 255f, 1f, 1f);
            }
            if (reflection == null)
            {
                reflection = new EyeReflectionSettings();
            }
            if (reflection.color == null)
            {
                reflection.color = new ColorData(0xE6 / 255f, 0xF0 / 255f, 1f, 1f);
            }
            // 空の要素は 0 個として残す (全部削除した状態を保存し直しても既定へ戻さない)
            if (highlights == null)
            {
                highlights = new List<EyeHighlightSettings>();
                for (var i = 0; i < DefaultHighlightCount; i++)
                {
                    highlights.Add(CreateDefaultHighlight(i));
                }
            }
            highlights.RemoveAll(h => h == null);
            if (highlights.Count > MaxHighlightCount)
            {
                highlights.RemoveRange(MaxHighlightCount, highlights.Count - MaxHighlightCount);
            }
            foreach (var h in highlights)
            {
                if (h.glowColor == null)
                {
                    h.glowColor = DefaultGlowColor();
                }
            }
            ClampValues();
        }

        /// <summary>ファイルに highlights が無いときの 3 個 (0〜2)</summary>
        private static EyeHighlightSettings CreateDefaultHighlight(int index)
        {
            switch (index)
            {
                case 0: return Highlight(1f, -0.12f, 0.1f, 0.12f, 0.12f, 0f, DefaultGlowColor(), 0.08f);
                case 1: return Highlight(0.9f, 0.08f, 0.16f, 0.04f, 0.04f, 0f, new ColorData(0xC8 / 255f, 0xD2 / 255f, 1f, 1f), 0.03f);
                case 2: return Highlight(0.7f, 0.2f, -0.14f, 0.08f, 0.04f, -20f, new ColorData(1f, 0xC8 / 255f, 0xE6 / 255f, 1f), 0.04f);
                default: throw new ArgumentOutOfRangeException(nameof(index), index, null);
            }
        }

        /// <summary>[追加] で足すハイライト。瞳の中心の小さな丸</summary>
        public static EyeHighlightSettings CreateNewHighlight()
        {
            var h = new EyeHighlightSettings();
            h.glowColor = DefaultGlowColor();
            return h;
        }

        private static EyeHighlightSettings Highlight(float strength, float x, float y, float width, float height, float roll, ColorData glowColor, float glowWidth)
        {
            return new EyeHighlightSettings
            {
                strength = strength, x = x, y = y, width = width, height = height, roll = roll, glowColor = glowColor, glowWidth = glowWidth,
            };
        }

        private static ColorData DefaultGlowColor()
        {
            return new ColorData(0xA0 / 255f, 0xB4 / 255f, 1f, 1f);
        }

        private void ClampValues()
        {
            upperShadow = Clamp01(upperShadow);
            upperShadowHeight = Clamp01(upperShadowHeight);
            upperShadowSoftness = Clamp01(upperShadowSoftness);
            lowerGlow = Clamp01(lowerGlow);
            lowerGlowHeight = Clamp01(lowerGlowHeight);
            lowerGlowSoftness = Clamp01(lowerGlowSoftness);
            // 下限 (-1) に寄せると、壊れた値で視差が最大の逆向きになる
            parallax = float.IsNaN(parallax) ? 0f : Clamp(parallax, -MaxParallax, MaxParallax);
            reflection.strength = Clamp01(reflection.strength);
            reflection.x = Clamp(reflection.x, -MaxHighlightOffset, MaxHighlightOffset);
            reflection.y = Clamp(reflection.y, -MaxHighlightOffset, MaxHighlightOffset);
            reflection.width = Clamp01(reflection.width);
            reflection.height = Clamp01(reflection.height);
            reflection.roundness = Clamp01(reflection.roundness);
            reflection.softness = Clamp01(reflection.softness);
            foreach (var h in highlights)
            {
                h.strength = Clamp01(h.strength);
                h.x = Clamp(h.x, -MaxHighlightOffset, MaxHighlightOffset);
                h.y = Clamp(h.y, -MaxHighlightOffset, MaxHighlightOffset);
                h.width = Clamp(h.width, 0f, MaxHighlightSize);
                h.height = Clamp(h.height, 0f, MaxHighlightSize);
                h.roll = Clamp(h.roll, -MaxHighlightRoll, MaxHighlightRoll);
                h.glowWidth = Clamp(h.glowWidth, 0f, MaxGlowWidth);
            }
        }

        private static float Clamp01(float v)
        {
            return Clamp(v, 0f, 1f);
        }

        private static float Clamp(float v, float min, float max)
        {
            // Math.Min / Max は NaN をそのまま返し、手編集の NaN がシェーダーまで届いて瞳が破綻する
            return float.IsNaN(v) ? min : Math.Max(min, Math.Min(max, v));
        }
    }
}
```


- [ ] **Step 3: 本体のビルドを通す**

テストのプロジェクトは本体を `ProjectReference` で参照するため、本体が通らないとテストもビルドできない。Task 1 では本体の古い参照を削除するだけにし、正式な内容は Task 2・3 で入れる（Task 1 の時点では、目タブにハイライトの行が出ず、シェーダーのハイライトは旧プロパティの既定のまま。バンドルは Task 2 まで旧のまま）:

- `MToonProps.cs`: `EyeSparkle` / `EyeHighlights` / `EyeHighlightStrength` / `EyeHighlightGlowColor` を削除
- `EyeConverter.Apply`: `EyeSparkle` の行と、`var strength = Vector4.zero;` から `EyeHighlightGlowColor` の行までを削除
- `MToonMaidWindow.DrawEyeSettings`: 「きらめき」の行、ハイライトの `for` ループ、「ハイライト共通」の見出しと「にじみの色」の行を削除し、見出し「ハイライトの動き」と「視差」の行を残す

- [ ] **Step 4: テストを通す（GREEN）**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 0 エラー / 全件 PASS

- [ ] **Step 5: コミット**

commit スキルで `feat: 瞳のハイライトを可変のリストにし、映り込みの設定を足す` としてコミットする。

---

### Task 2: シェーダーと値の受け渡し

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToonMaid/MToonMaidEye.shader`, `source/COM3D25.MToonMaid.Plugin/Rendering/EyeConverter.cs`, `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`
- 生成物: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

**Interfaces:**
- Consumes: Task 1 の `EyeSettings` / `EyeHighlightSettings` / `EyeReflectionSettings` / `MaxHighlightCount`
- Produces: シェーダーのプロパティ `_HighlightCount`（float）、配列 `_HighlightShape[8]`（x, y, width, height）・`_HighlightParams[8]`（strength, roll のラジアン, glowWidth, 0）・`_HighlightGlow[8]`（rgb, 0）、映り込み `_ReflectionStrength` / `_ReflectionColor` / `_ReflectionShape`（x, y, width, height）/ `_ReflectionParams`（roundness, softness, 0, 0）

- [ ] **Step 1: MToonProps を直す**

（旧定数は Task 1 で削除済み）次を足す:

```csharp
        public const string EyeHighlightCount = "_HighlightCount";
        public const string EyeHighlightShape = "_HighlightShape";
        public const string EyeHighlightParams = "_HighlightParams";
        public const string EyeHighlightGlow = "_HighlightGlow";
        public const string EyeReflectionStrength = "_ReflectionStrength";
        public const string EyeReflectionColor = "_ReflectionColor";
        public const string EyeReflectionShape = "_ReflectionShape";
        public const string EyeReflectionParams = "_ReflectionParams";
```

- [ ] **Step 2: シェーダーを直す**

Properties から `_Sparkle` / `_Highlight0〜2` / `_HighlightStrength` / `_HighlightGlowColor` を消し、次を足す（配列は Properties に書けないため宣言だけ）:

```hlsl
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.25
        _ReflectionColor ("Reflection Color", Color) = (0.902, 0.941, 1, 1)
        _ReflectionShape ("Reflection (x, y, width, height)", Vector) = (0, -0.22, 0.6, 0.28)
        _ReflectionParams ("Reflection (roundness, softness)", Vector) = (1, 0.5, 0, 0)
        _HighlightCount ("Highlight Count", Float) = 0
```

`_Parallax` の範囲を `Range(-1, 1)` に直す（マイナスは目の向きと同じ側へ滑る）。

CGINCLUDE の変数宣言を次へ置き換える（`_Sparkle` 以降の Highlight 系を消す）:

```hlsl
    half _ReflectionStrength;
    half4 _ReflectionColor;
    float4 _ReflectionShape;
    float4 _ReflectionParams;
    // 長さはプラグインの EyeSettings.MaxHighlightCount と揃える
    #define MAX_HIGHLIGHTS 8
    float _HighlightCount;
    float4 _HighlightShape[MAX_HIGHLIGHTS];
    float4 _HighlightParams[MAX_HIGHLIGHTS];
    float4 _HighlightGlow[MAX_HIGHLIGHTS];
```

`Highlight` 関数を次に置き換え、映り込みの関数を足す:

```hlsl
    // 白い芯は縁を pixel (約 1 画素ぶんの瞳の座標) だけなめらかにしてくっきり描き、外側をにじみの色で薄めていく。
    // 楕円の縁までの正確な距離は重いため、(r - 1) × 短い方の半径で近似する
    half3 Highlight(float2 p, float4 shape, float4 params, half3 glowColor, float pixel)
    {
        const float2 radii = max(shape.zw * 0.5, 1e-4);
        float2 d = p - shape.xy;
        float s;
        float c;
        sincos(params.y, s, c);
        // ロールの分だけ逆に回して、楕円の軸に合わせる
        d = float2(c * d.x + s * d.y, -s * d.x + c * d.y);
        const float dist = (length(d / radii) - 1) * min(radii.x, radii.y);
        const half core = saturate(0.5 - dist / pixel);
        const half glow = params.z > 0 ? pow(1 - saturate(dist / params.z), 2) : 0;
        return lerp(glowColor * glow, 1, core) * params.x;
    }

    // 角の丸い四角の符号付き距離 (内側が負)
    float RoundedBox(float2 p, float2 halfSize, float radius)
    {
        const float2 q = abs(p) - halfSize + radius;
        return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
    }
```

`ApplyGloss` のきらめきとハイライトの部分（`// きらめき:` から `return albedo;` の手前まで）を次に置き換える:

```hlsl
        // ddx / ddy は分岐の前で求める (ハイライトの個数で分岐するため)
        const float pixel = max(length(fwidth(q)), 1e-5);

        // 視差: 目がビューの右 (上) を向くと、カメラ側の光の映り込みは瞳の中で左 (下) へ寄る
        const float2 gazeVS = mul((float3x3)UNITY_MATRIX_V, _EyeGaze.xyz).xy;
        const float2 p = s + gazeVS * _Parallax;

        const float2 reflectionHalf = max(_ReflectionShape.zw * 0.5, 1e-4);
        const float reflectionShort = min(reflectionHalf.x, reflectionHalf.y);
        const float reflectionDist = RoundedBox(p - _ReflectionShape.xy, reflectionHalf, _ReflectionParams.x * reflectionShort);
        const float reflectionBlur = max(_ReflectionParams.y * reflectionShort, pixel);
        const half reflection = 1 - smoothstep(-reflectionBlur, reflectionBlur, reflectionDist);
        albedo = lerp(albedo, _ReflectionColor.rgb, reflection * _ReflectionStrength * shine);

        highlight = 0;
        [unroll]
        for (int i = 0; i < MAX_HIGHLIGHTS; i++)
        {
            if (i < _HighlightCount && _HighlightParams[i].x > 0)
            {
                highlight += Highlight(p, _HighlightShape[i], _HighlightParams[i], _HighlightGlow[i].rgb, pixel);
            }
        }
        highlight *= shine;
```

（`ToViewAligned` の呼び出し `const float2 s = ToViewAligned(q, viewPos);` は今の位置のまま。`pixel` は `s` の直後に置く）

`ApplyGloss` のシグネチャを `half3 ApplyGloss(half3 albedo, float2 uv, float3 viewPos, half shine, out half3 highlight)` にし、ハイライトを albedo へ足さずに `highlight` で返す（白目へはみ出させるため、アルファの合成を呼び出し側で行う）。末尾の `return albedo;` はそのまま。

`fragBase` を次に置き換える:

```hlsl
            half4 fragBase(v2f i) : SV_Target
            {
                const half4 tex = tex2D(_MainTex, i.uv) * _Color;
                half3 highlight;
                const half3 albedo = ApplyGloss(tex.rgb, i.uv, i.viewPos, 1, highlight);
                // 顔の MToon の光側と同じく、主光源の色に上下で均した環境光を足す (光と影の境界は入れない)
                const half3 ambient = (ShadeSH9(half4(0, 1, 0, 1)) + ShadeSH9(half4(0, -1, 0, 1))) * 0.5;
                const half3 light = _LightColor0.rgb + ambient;
                // ハイライトを瞳のアルファで切り抜くと白目へはみ出せない。
                // ブレンドの結果が「瞳 (アルファで重ねる) + ハイライト (足す)」になるよう、アルファと色を合わせて出す
                const half coverage = saturate(max(highlight.r, max(highlight.g, highlight.b)));
                const half alpha = saturate(tex.a + coverage * (1 - tex.a));
                const half3 color = (albedo * tex.a + highlight) * light / max(alpha, 1e-4);
                return half4(color, alpha);
            }
```

`fragAdd` は `half3 highlight;` を宣言して `ApplyGloss(tex.rgb, i.uv, i.viewPos, 0, highlight)` で呼ぶ（shine 0 なので highlight は 0。出力は今のまま `tex.a` のアルファ）。

使わなくなる `Hash22` を削除する。ファイル先頭のコメントの「下側の明るさときらめき」を「下側の明るさ・映り込み」に、「画面基準のハイライトを重ねる」を「画面基準のハイライト (白目へはみ出してよい) を重ねる」に直す。

- [ ] **Step 3: EyeConverter.Apply を直す**

フィールドに配列のバッファを足す（`SetVectorArray` は最初に渡した長さでシェーダー側の配列長が決まるため、常に 8 要素で渡す）:

```csharp
        // 個数が少ないときも MaxHighlightCount 個で渡す (SetVectorArray は最初に渡した長さで配列長が決まり、後から長くできない)
        private static readonly Vector4[] HighlightShape = new Vector4[EyeSettings.MaxHighlightCount];
        private static readonly Vector4[] HighlightParams = new Vector4[EyeSettings.MaxHighlightCount];
        private static readonly Vector4[] HighlightGlow = new Vector4[EyeSettings.MaxHighlightCount];
```

`Apply` のハイライトの部分（`var strength = Vector4.zero;` から `EyeHighlightGlowColor` の行まで）と `EyeSparkle` の行を、次に置き換える:

```csharp
            var r = s.reflection;
            m.SetFloat(MToonProps.EyeReflectionStrength, r.strength);
            m.SetColor(MToonProps.EyeReflectionColor, ToColor(r.color));
            m.SetVector(MToonProps.EyeReflectionShape, new Vector4(r.x, r.y, r.width, r.height));
            m.SetVector(MToonProps.EyeReflectionParams, new Vector4(r.roundness, r.softness, 0f, 0f));

            var count = Math.Min(s.highlights.Count, EyeSettings.MaxHighlightCount);
            for (var i = 0; i < EyeSettings.MaxHighlightCount; i++)
            {
                if (i < count)
                {
                    var h = s.highlights[i];
                    HighlightShape[i] = new Vector4(h.x, h.y, h.width, h.height);
                    HighlightParams[i] = new Vector4(h.strength, h.roll * Mathf.Deg2Rad, h.glowWidth, 0f);
                    HighlightGlow[i] = new Vector4(h.glowColor.r, h.glowColor.g, h.glowColor.b, 0f);
                }
                else
                {
                    HighlightShape[i] = Vector4.zero;
                    HighlightParams[i] = Vector4.zero;
                    HighlightGlow[i] = Vector4.zero;
                }
            }
            m.SetFloat(MToonProps.EyeHighlightCount, count);
            m.SetVectorArray(MToonProps.EyeHighlightShape, HighlightShape);
            m.SetVectorArray(MToonProps.EyeHighlightParams, HighlightParams);
            m.SetVectorArray(MToonProps.EyeHighlightGlow, HighlightGlow);
```

- [ ] **Step 4: バンドルとビルド**

Unity エディタが閉じていることを確かめ、Run: `build-bundle.bat`（フルパスで `cmd //c` から）
Expected: `コピーしました` と出て、`UnityProject2022/Logs/build-bundle.log` に `Shader error` が無い

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 0 エラー / 全件 PASS

- [ ] **Step 5: コミット**

commit スキルで `feat: 瞳のハイライトをくっきりした芯と色付きのにじみにし、映り込みを描く` としてコミットする。

---

### Task 3: 目タブを作り直す

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`

**Interfaces:**
- Consumes: Task 1 の設定、`GUIView.DrawFloatField(FloatFieldOption)`（`label` + `labelWidth` + `dragSensitivity` でドラッグラベルつき）、`GUIView.DrawButton(text, width, height, enabled, color)`、`GUIView.option.accentColor`、`GUIView.Vector3DragLabelWidth`（14）、`GUIView.Vector3FieldMinWidth`（40）

- [ ] **Step 1: 状態と定数を足す**

```csharp
        // 選んでいるハイライトの番号。個数が減ったら ClampHighlightIndex で範囲へ寄せる
        private int _highlightIndex;
        // 8 個と [追加][削除] が最小幅 380 に収まる幅
        private const float DataTabWidth = 24f;
        private const float DataButtonWidth = 44f;
        private const float ResetButtonSmallWidth = 20f;
        // 位置と大きさの 2 値の行のドラッグ 1px あたりの増減
        private const float PairDragSensitivity = 0.002f;
```

- [ ] **Step 2: DrawEyeSettings を作り直す**

「下側の明るさ」の「きらめき」の行を削除し、ハイライトの `for` ループから「ハイライトの動き」の前までを次に置き換える（「視差」のスライダーは範囲を `-EyeSettings.MaxParallax`〜`EyeSettings.MaxParallax` に直す）:

```csharp
            view.DrawLabel("映り込み", -1, RowHeight);
            var r = eye.reflection;
            var rd = def.reflection;
            EyeSlider(view, "強さ", r.strength, 0f, 1f, rd.strength, v => r.strength = v, editable);
            EyeColorRow(view, "目/映り込みの色", "色", r.color, rd.color, editable);
            PairRow(view, "位置", "X", "Y", r.x, r.y, -EyeSettings.MaxHighlightOffset, EyeSettings.MaxHighlightOffset, rd.x, rd.y,
                (a, b) => { r.x = a; r.y = b; }, editable);
            PairRow(view, "大きさ", "W", "H", r.width, r.height, 0f, 1f, rd.width, rd.height,
                (a, b) => { r.width = a; r.height = b; }, editable);
            EyeSlider(view, "角の丸み", r.roundness, 0f, 1f, rd.roundness, v => r.roundness = v, editable);
            EyeSlider(view, "ぼかし", r.softness, 0f, 1f, rd.softness, v => r.softness = v, editable);

            view.DrawLabel("ハイライト", -1, RowHeight);
            // 番号の行より前で寄せる (範囲外のまま [削除] を押すと RemoveAt が例外になる)
            ClampHighlightIndex(eye);
            DrawHighlightTabs(view, eye, editable);
            // 0 個のときも行は描く (入力欄キャッシュの番号がずれるため)。値は新規の既定を無効表示で見せる
            var hasHighlight = eye.highlights.Count > 0;
            var h = hasHighlight ? eye.highlights[_highlightIndex] : _newHighlight;
            var hd = _newHighlight;
            var hEditable = editable && hasHighlight;
            EyeSlider(view, "強さ", h.strength, 0f, 1f, hd.strength, v => h.strength = v, hEditable);
            PairRow(view, "位置", "X", "Y", h.x, h.y, -EyeSettings.MaxHighlightOffset, EyeSettings.MaxHighlightOffset, hd.x, hd.y,
                (a, b) => { h.x = a; h.y = b; }, hEditable);
            PairRow(view, "大きさ", "W", "H", h.width, h.height, 0f, EyeSettings.MaxHighlightSize, hd.width, hd.height,
                (a, b) => { h.width = a; h.height = b; }, hEditable);
            EyeSlider(view, "ロール", h.roll, -EyeSettings.MaxHighlightRoll, EyeSettings.MaxHighlightRoll, hd.roll, v => h.roll = v, hEditable);
            // 番号ごとに別のキーにする (同じキーだと、ピッカーを開いたまま番号を変えたとき前の色を指したままになりうる)
            EyeColorRow(view, "目/ハイライト" + (_highlightIndex + 1) + "のにじみの色", "にじみの色", h.glowColor, hd.glowColor, hEditable);
            EyeSlider(view, "にじみの幅", h.glowWidth, 0f, EyeSettings.MaxGlowWidth, hd.glowWidth, v => h.glowWidth = v, hEditable);
```

（「ハイライト共通」の見出しと「にじみの色」の行は Task 1 で削除済み）フィールドに `private readonly EyeHighlightSettings _newHighlight = EyeSettings.CreateNewHighlight();` を足す（リセット先と 0 個のときの表示に使う）。

- [ ] **Step 3: データ番号の行・2 値の行・番号の丸めを足す**

```csharp
        /// <summary>PostEffects のパラフィンのデータ番号の行と同じ作り ([1][2]… [追加][削除])</summary>
        private void DrawHighlightTabs(GUIView view, EyeSettings eye, bool editable)
        {
            var list = eye.highlights;
            view.SetEnabled(_baseEnabled && editable);
            view.BeginHorizontal();
            for (var i = 0; i < list.Count; i++)
            {
                if (view.DrawButton((i + 1).ToString(), DataTabWidth, RowHeight, true,
                    i == _highlightIndex ? GUIView.option.accentColor : (Color?)null))
                {
                    _highlightIndex = i;
                }
            }
            if (view.DrawButton("追加", DataButtonWidth, RowHeight, list.Count < EyeSettings.MaxHighlightCount))
            {
                list.Add(EyeSettings.CreateNewHighlight());
                _highlightIndex = list.Count - 1;
                _eyeChanged = true;
            }
            if (view.DrawButton("削除", DataButtonWidth, RowHeight, list.Count > 0))
            {
                list.RemoveAt(_highlightIndex);
                // 同じ描画の後続の行が list[_highlightIndex] を読むため、ここで寄せる
                _highlightIndex = Math.Max(0, Math.Min(_highlightIndex - 1, list.Count - 1));
                _eyeChanged = true;
            }
            view.EndLayout();
            view.SetEnabled(_baseEnabled);
        }

        /// <summary>削除・読み直し・メイドの切り替えで個数が減っても範囲外を指さないよう寄せる</summary>
        private void ClampHighlightIndex(EyeSettings eye)
        {
            _highlightIndex = Mathf.Clamp(_highlightIndex, 0, Mathf.Max(0, eye.highlights.Count - 1));
        }

        /// <summary>
        /// ラベル + 軸名のドラッグラベルと入力欄 ×2 + リセットの 1 行。
        /// MTEUtils の DrawVector2Row は軸名が X / Y 固定で、大きさの W / H に使えない
        /// </summary>
        private void PairRow(GUIView view, string label, string nameA, string nameB, float a, float b, float min, float max,
            float defaultA, float defaultB, Action<float, float> set, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            var available = view.viewRect.width - view.padding.x * 2 - LabelWidth - ResetButtonSmallWidth - view.margin * 4;
            var fieldWidth = Mathf.Max(available / 2f, GUIView.Vector3DragLabelWidth + GUIView.Vector3FieldMinWidth);
            view.BeginHorizontal();
            view.DrawLabel(label, LabelWidth, RowHeight);
            PairField(view, nameA, a, min, max, fieldWidth, v => set(v, b));
            PairField(view, nameB, b, min, max, fieldWidth, v => set(a, v));
            if (view.DrawButton("R", ResetButtonSmallWidth, RowHeight))
            {
                set(defaultA, defaultB);
                _eyeChanged = true;
            }
            view.EndLayout();
            view.SetEnabled(_baseEnabled);
        }

        private void PairField(GUIView view, string name, float value, float min, float max, float width, Action<float> set)
        {
            view.DrawFloatField(new GUIView.FloatFieldOption
            {
                label = name,
                labelWidth = GUIView.Vector3DragLabelWidth,
                dragSensitivity = PairDragSensitivity,
                fieldType = FloatFieldType.F3,
                value = value,
                minValue = min,
                maxValue = max,
                width = width,
                height = RowHeight,
                onChanged = v =>
                {
                    set(Mathf.Clamp(v, min, max));
                    _eyeChanged = true;
                },
            });
        }
```

`DrawEyeResetButton` は今のまま（`EyeSettings.CreateDefault()` が既定の 3 個と映り込みを作る）。リセット後は `_highlightIndex = 0` にする行を足す。

行のリセット（各スライダーの既定と PairRow の R）は、どの番号でも `_newHighlight`（新規の既定）へ戻す（spec どおり。既定の 3 個の初期値へ戻すのはタブ全体のリセット）。

`DrawFloatField` の `fieldCache` を渡さないと `GetFieldCache(option.label, …)` が描画順で引かれ、`label` が軸名（X / Y / W / H）だけになる。これは描画順の番号で引くので問題ないが、念のため Step 4 で 2 つの行の値が混ざらないことを実機で確かめる。

- [ ] **Step 4: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 0 エラー / 全件 PASS

- [ ] **Step 5: コミット**

commit スキルで `feat: 目タブにハイライトの追加・削除と映り込みの行を足す` としてコミットする。

---

### Task 4: 実機で確かめ、既定値と文書を合わせる

`com3d25-devbridge:restart-verify` の手順で反映する。**ゲームを終了する前に、ユーザーへシーンの保存の有無を確認し、回答を ledger に書く。** メイドの設定ファイルを控えてから変え、最後に戻す。

- [ ] **Step 1: 反映して起動・ロード**（前回と同じ: `debug.bat com3d25` → 起動 → スロット 42 をロード → `reset_evaluator`）
- [ ] **Step 2: 目タブを開いて確かめる**: ウィンドウの目タブを `screenshot`（`window` 指定）で撮り、`[1][2][3] [追加][削除]`、`位置 X[ ] Y[ ]`、`大きさ W[ ] H[ ]` が横並びで、行がスクロールバーと重ならないこと。[追加] で 4 個目が選ばれ、[削除] で 1 つ前が選ばれること。8 個まで追加して番号の行が崩れず [追加] が無効になること。0 個まで削除して行が無効表示で残り、[削除] が無効になること。0 個のままゲームを再起動して設定を読み直し、0 個のままであること（Mono の XmlSerializer の確認。再起動は Step 5 の後片付けの前に 1 回だけ。ユーザーへ保存の確認をしてから）
- [ ] **Step 3: 見た目を確かめる**: カメラを瞳へ寄せ、表情を「通常」・まばたきを止めて撮る（前回と同じ手順）。白い芯の縁がくっきりして、外側に色付きのにじみが出ること。映り込みが瞳の下寄りに淡く出ること。きらめきの粒が無いこと。ハイライトの位置を瞳の外（X 0.45 など）へ動かし、白目の上に出てまぶたの所で隠れること、瞳の内側の見た目が変わらないこと。横長の楕円（既定の 3 個目）の端でにじみが極端に短く見えないこと。瞳のメッシュが白目をどこまで覆うかを、ハイライトを大きくして確かめる
- [ ] **Step 4: 既定値を詰める**: 参考画像（`C:\Users\kidon\Pictures\IMG_0864.png`）と見比べ、ユーザーと既定値を決める。決めた値を `EyeSettings`（`CreateDefaultHighlight`・`EyeReflectionSettings` の初期値）・シェーダーの Properties の既定・テストの期待値・spec の表へ反映する
- [ ] **Step 5: 後片付け**: カメラ・まばたきを戻し、メイドの設定ファイルを控えに戻す（ゲーム停止中にファイルを戻すか、目タブを OFF にする）
- [ ] **Step 6: 文書**: README の使い方 6 の文を「ハイライトは [追加][削除] で 8 個まで増減でき、位置・大きさ（楕円）・ロール・にじみの色と幅を個別に決められます。白い芯の外側が、にじみの色でぼけます。ハイライトは白目へはみ出せます（瞳のメッシュの範囲まで）。瞳の下寄りには半透明の映り込みが重なります」に合わせて直す
- [ ] **Step 7: コミット**: 既定値は `feat: …`、文書は `docs: …` で分けてコミットする


## レビュー却下メモ

- （なし。plan-review の指摘はすべて取り込んだ。楕円の近似距離の限界は spec で承知済みのため、Task 4 の実機確認の項目として扱う）
