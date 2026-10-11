# COM3D25.MToonMaid.Plugin 瞳の色をカラーピッカーで選ぶ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 瞳の色のサブタブの色相・彩度・明度のスライダー（メイン・影）を、プラグインの色の行（見本・HEX・R・[編集] でカラーピッカー）に置き換える。

**Architecture:** パーツカラーの (色相, 彩度, 明度) と「テクスチャの明るさ 128 の位置に出る色」の相互変換を、Unity に依存しない静的クラス `EyeColorHsl` にして、実機で測った値で単体テストする。ウィンドウは色の行 1 本で 3 値を書き換え、書き込みは既存の「ためて Update で書く」経路（`_eyeColorWritePending`）に乗せる。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-eye-gloss-design.md` の「改訂 2026-10-10 (3): 瞳の色をカラーピッカーで選ぶ」節

## Global Constraints

- main で作業する。コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- ゲームを終了する前に、シーンの保存の有無をユーザーに確認する
- 色 = `HSL(色相/255, 彩度/255, clamp(明度 − 127, 0, 255)/255)`
- 色 → 値: 色相 = round(H × 255)、彩度 = round(S × 255)、明度 = round(L × 255) + 127（127〜382）
- 彩度が 0 のときは色相を残す。L が 0 か 1 のときは色相と彩度を残す
- コントラスト・影の割合は今の整数スライダー（`EyeColorSlider`）のまま
- 色の行の R とピッカーのリセットは今の色へ戻す（何も変わらない）
- ピッカーの同定キーはメイドと左右ごとに分ける
- 書き込みは描画中に行わず `_eyeColorWritePending` を立てて Update で書く（改訂 (2) の経路）

## Review Focus

- **ゲームの色テーブルとの一致**: 色の行の見本が実機の表の x = 128 の色と ±2/255 で一致する → Task 1 のテスト（実機で測った 6 点）
- **選び直しの往復**: 色 → 値 → 色で値が変わらない（範囲内の整数）→ Task 1 のテスト（格子で往復）
- **灰色・黒・白を選ぶ**: 色相（と彩度）が失われない → Task 1 のテスト
- **範囲外の明度**（127 未満・382 超え）: 表示は黒か白で、触るまで値は変わらない → Task 1 のテスト（表示）、Task 3 Step 3（触らなければ書かない）
- **量子化でピッカーと窓が書き込みを繰り返さない**: ピッカーは自分の色と目標の色が違うときだけ呼び返し、窓の `DrawColor` も同じ。値を丸めた色が次の目標になるので 1 回で止まる → Task 3 Step 3（`MaidEyeColor.Write` の呼び出し回数を数える）

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/EyeColorHsl.cs   (Create: パーツカラーと色の変換。Unity に依存しない)
└─ UI/MToonMaidWindow.cs     (Modify: 色相・彩度・明度のスライダーを色の行へ)
source/COM3D25.MToonMaid.Plugin.Tests/EyeColorHslTests.cs (Create)
README.md                                                 (Modify)
```

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`

---

### Task 1: パーツカラーと色の変換

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Settings/EyeColorHsl.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/EyeColorHslTests.cs`

**Interfaces:**
- Consumes: 既存の `ColorData`（`Settings/ColorData.cs`、`r g b a` の float、`new ColorData(r, g, b, a)`）
- Produces: `static class EyeColorHsl { const int MinBrightness = 127; const int MaxBrightness = 382; ColorData ToColor(int hue, int chroma, int brightness); void FromColor(ColorData c, ref int hue, ref int chroma, ref int brightness) }`

- [ ] **Step 1: 失敗するテストを書く**

期待値の 6 点は、実機で `UTY.UpdateColorTableTexture` の表を RenderTexture 経由で読み戻した x = 128 の値（コントラスト 100）。

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class EyeColorHslTests
    {
        private const float Tolerance = 2f / 255f;

        private static void AssertColor(int r, int g, int b, ColorData c)
        {
            Assert.InRange(c.r, r / 255f - Tolerance, r / 255f + Tolerance);
            Assert.InRange(c.g, g / 255f - Tolerance, g / 255f + Tolerance);
            Assert.InRange(c.b, b / 255f - Tolerance, b / 255f + Tolerance);
        }

        [Theory]
        [InlineData(0, 255, 255, 254, 1, 1)]
        [InlineData(85, 255, 255, 1, 254, 1)]
        [InlineData(0, 128, 255, 191, 64, 64)]
        [InlineData(8, 67, 266, 169, 119, 108)]
        [InlineData(0, 255, 191, 128, 0, 0)]
        [InlineData(0, 255, 383, 255, 255, 255)]
        public void ゲームの色テーブルの明るさ128の位置と同じ色になる(int hue, int chroma, int brightness, int r, int g, int b)
        {
            AssertColor(r, g, b, EyeColorHsl.ToColor(hue, chroma, brightness));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(126)]
        public void 明度が下限より下は黒で表示する(int brightness)
        {
            AssertColor(0, 0, 0, EyeColorHsl.ToColor(40, 200, brightness));
        }

        [Theory]
        [InlineData(383)]
        [InlineData(510)]
        public void 明度が上限より上は白で表示する(int brightness)
        {
            AssertColor(255, 255, 255, EyeColorHsl.ToColor(40, 200, brightness));
        }

        [Fact]
        public void 範囲内の値は色を経由しても変わらない()
        {
            foreach (var hue in new[] { 0, 8, 43, 85, 128, 170, 254 })
            foreach (var chroma in new[] { 1, 67, 128, 255 })
            foreach (var brightness in new[] { 127, 128, 200, 266, 381, 382 })
            {
                int h = -1, s = -1, b = -1;
                if (brightness == EyeColorHsl.MinBrightness || brightness == EyeColorHsl.MaxBrightness)
                {
                    // 黒と白は色相と彩度を残すため、元の値を入れておく
                    h = hue;
                    s = chroma;
                }
                EyeColorHsl.FromColor(EyeColorHsl.ToColor(hue, chroma, brightness), ref h, ref s, ref b);
                Assert.Equal(new[] { hue, chroma, brightness }, new[] { h, s, b });
            }
        }

        [Fact]
        public void 色相255は0と同じ赤として表示し選び直すと0になる()
        {
            int h = -1, s = -1, b = -1;
            EyeColorHsl.FromColor(EyeColorHsl.ToColor(255, 255, 255), ref h, ref s, ref b);
            Assert.Equal(new[] { 0, 255, 255 }, new[] { h, s, b });
        }

        [Fact]
        public void 純色の赤は色相0彩度255明度255になる()
        {
            int h = 99, s = 99, b = 99;
            EyeColorHsl.FromColor(new ColorData(1f, 0f, 0f, 1f), ref h, ref s, ref b);
            Assert.Equal(new[] { 0, 255, 255 }, new[] { h, s, b });
        }

        [Fact]
        public void 灰色を選ぶと色相を残して彩度を0にする()
        {
            int h = 40, s = 100, b = 300;
            EyeColorHsl.FromColor(new ColorData(0.5f, 0.5f, 0.5f, 1f), ref h, ref s, ref b);
            Assert.Equal(new[] { 40, 0, 255 }, new[] { h, s, b });
        }

        [Theory]
        [InlineData(0f, 127)]
        [InlineData(1f, 382)]
        public void 黒か白を選ぶと色相と彩度を残す(float v, int brightness)
        {
            int h = 40, s = 100, b = 300;
            EyeColorHsl.FromColor(new ColorData(v, v, v, 1f), ref h, ref s, ref b);
            Assert.Equal(new[] { 40, 100, brightness }, new[] { h, s, b });
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルドエラー（`EyeColorHsl` が無い）

- [ ] **Step 3: 実装する**

`Settings/EyeColorHsl.cs`:

```csharp
using System;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// パーツカラーの (色相, 彩度, 明度) と、瞳のテクスチャの明るさ 128 の位置に出る色 (RGB 0〜1) の変換。
    /// ゲームの色テーブルは HSL の着色で、明るさ 128 の位置の L は (明度 − 127) / 255 になる (実機で確認)
    /// </summary>
    public static class EyeColorHsl
    {
        public const int MinBrightness = 127;
        public const int MaxBrightness = 382;

        public static ColorData ToColor(int hue, int chroma, int brightness)
        {
            var h = hue / 255.0;
            var s = Clamp01(chroma / 255.0);
            var l = Clamp01((brightness - MinBrightness) / 255.0);
            var q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            var p = 2.0 * l - q;
            return new ColorData((float)HueToRgb(p, q, h + 1.0 / 3.0), (float)HueToRgb(p, q, h), (float)HueToRgb(p, q, h - 1.0 / 3.0), 1f);
        }

        /// <summary>
        /// 色から値を求めて書き換える。RGB から決まらない値 (灰色の色相、黒・白の色相と彩度) は渡された値を残す
        /// </summary>
        public static void FromColor(ColorData c, ref int hue, ref int chroma, ref int brightness)
        {
            var r = Clamp01(c.r);
            var g = Clamp01(c.g);
            var b = Clamp01(c.b);
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var l = (max + min) / 2.0;
            var lum = (int)Math.Round(l * 255.0, MidpointRounding.AwayFromZero);
            brightness = lum + MinBrightness;
            if (lum == 0 || lum == 255)
            {
                return;
            }

            var d = max - min;
            var newChroma = (int)Math.Round(Clamp01(d / (1.0 - Math.Abs(2.0 * l - 1.0))) * 255.0, MidpointRounding.AwayFromZero);
            chroma = newChroma;
            if (newChroma == 0)
            {
                return;
            }

            double h;
            if (max == r)
            {
                h = (g - b) / d + (g < b ? 6.0 : 0.0);
            }
            else if (max == g)
            {
                h = (b - r) / d + 2.0;
            }
            else
            {
                h = (r - g) / d + 4.0;
            }
            hue = (int)Math.Round(h / 6.0 * 255.0, MidpointRounding.AwayFromZero);
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0.0)
            {
                t += 1.0;
            }
            if (t > 1.0)
            {
                t -= 1.0;
            }
            if (t < 1.0 / 6.0)
            {
                return p + (q - p) * 6.0 * t;
            }
            if (t < 0.5)
            {
                return q;
            }
            if (t < 2.0 / 3.0)
            {
                return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            }
            return p;
        }

        private static double Clamp01(double v)
        {
            return Math.Max(0.0, Math.Min(1.0, v));
        }
    }
}
```

（`c.r` は float。`Clamp01(double)` に渡して double で計算する。float のままだと彩度 1・明度 381 付近で `d` が 3e-5 程度になり、色相の往復が丸めの境目に近づく）

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

`feat: 瞳のパーツカラーと色を相互に変換する`

---

### Task 2: 色相・彩度・明度のスライダーを色の行に置き換える

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（`DrawEyeColor` の L464〜L474、`EyeColorSlider` の直後へメソッド追加）
- Modify: `README.md`（使い方 7）

**Interfaces:**
- Consumes: Task 1 の `EyeColorHsl.ToColor` / `FromColor`、既存の `ToColor(ColorData)`（窓の private static、`ColorData` → `UnityEngine.Color`）、`GUIView.DrawColor(ColorFieldCache, Color, Color resetColor, Action<Color>, string displayLabel)`、`GUIView.GetColorFieldCache(string label, bool hasAlpha)`、`maid.status.guid`

- [ ] **Step 1: 色の行のメソッドを足す**

`EyeColorSlider` の後に:

```csharp
        /// <summary>パーツカラーの色相・彩度・明度を、瞳のテクスチャの明るさ 128 の位置に出る色 1 つで編集する</summary>
        private void EyePartsColorRow(GUIView view, string colorKey, int hue, int chroma, int brightness, Action<int, int, int> set)
        {
            var shown = ToColor(EyeColorHsl.ToColor(hue, chroma, brightness));
            view.SetEnabled(_baseEnabled);
            // R の戻し先を別に持つと、ゲームが持たないパーツカラーの既定値をでっち上げることになる
            view.DrawColor(view.GetColorFieldCache(colorKey, false), shown, shown, c =>
            {
                int h = hue, s = chroma, b = brightness;
                EyeColorHsl.FromColor(new ColorData(c.r, c.g, c.b, 1f), ref h, ref s, ref b);
                set(h, s, b);
                _eyeColorWritePending = true;
            }, "色");
        }
```

- [ ] **Step 2: DrawEyeColor の行を置き換える**

`view.DrawLabel("メイン", -1, RowHeight);` から影のコントラストの行までを:

```csharp
            // 同じキーだと、ピッカーを開いたままメイドや左右を変えたとき前の対象の色を指したままになりうる
            var keyPrefix = "目/瞳の色/" + maid.status.guid + "/" + _eyeColorSide + "/";
            view.DrawLabel("メイン", -1, RowHeight);
            EyePartsColorRow(view, keyPrefix + "メイン", _eyeColorValues.mainHue, _eyeColorValues.mainChroma, _eyeColorValues.mainBrightness,
                (h, s, b) => { _eyeColorValues.mainHue = h; _eyeColorValues.mainChroma = s; _eyeColorValues.mainBrightness = b; });
            EyeColorSlider(view, "コントラスト", _eyeColorValues.mainContrast, EyeColorValues.MaxContrast, v => _eyeColorValues.mainContrast = v);
            view.DrawLabel("影", -1, RowHeight);
            EyeColorSlider(view, "割合", _eyeColorValues.shadowRate, EyeColorValues.MaxShadowRate, v => _eyeColorValues.shadowRate = v);
            EyePartsColorRow(view, keyPrefix + "影", _eyeColorValues.shadowHue, _eyeColorValues.shadowChroma, _eyeColorValues.shadowBrightness,
                (h, s, b) => { _eyeColorValues.shadowHue = h; _eyeColorValues.shadowChroma = s; _eyeColorValues.shadowBrightness = b; });
            EyeColorSlider(view, "コントラスト", _eyeColorValues.shadowContrast, EyeColorValues.MaxContrast, v => _eyeColorValues.shadowContrast = v);
```

`EyeColorValues.MaxHue` / `MaxChroma` / `MaxBrightness` は窓から使われなくなるが、`Clamped()` が使うので残す。

- [ ] **Step 3: README を直す**

使い方 7 の `[瞳の色] ではエディットの「瞳の色」と同じパーツカラー（左右 / 左 / 右、メインと影の色相・彩度・明度・コントラスト、影の割合）を変えられます。` を次にする:

`[瞳の色] ではエディットの「瞳の色」と同じパーツカラー（左右 / 左 / 右、メインと影の色とコントラスト、影の割合）を変えられます。色は [編集] のカラーピッカーで選び、瞳のテクスチャの中間の明るさの位置に出る色を表します（エディットの色相・彩度・明度にあたります）。`

同じ項目の末尾の `範囲外の値（MOD など）は、どれかを動かしたときにエディットの範囲へ丸まります` の後に `。エディットの明度のうち、色で表せない暗すぎる・明るすぎる値は、色を選んだときに表せる範囲へ寄ります。値は整数のため、選んだ色や HEX はわずかに寄ります（例: FF0000 は FF0101 になります）。黒・白・灰色を選ぶと、カラーピッカーの色相の表示は赤へ戻りますが、瞳の色相は残ります` を足す。

- [ ] **Step 4: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 0 エラー / 全件 PASS

- [ ] **Step 5: コミット**

`feat: 瞳の色をカラーピッカーで選べるようにする`

---

### Task 3: 実機で確かめる

`/com3d25-devbridge:restart-verify` の手順で反映する（ゲームの終了はユーザーの確認後）。パーツカラーと `eyePartsTab` の元の値を控えてから変え、最後に戻す。

- [ ] **Step 1: 反映して起動・ロード**（ゲーム終了 → `debug.bat com3d25` → DLL のハッシュ一致 → 起動 → スロット 42 をロード → `reset_evaluator`）
- [ ] **Step 2: 表示**: 窓の `_tab = 5`・`_eyeSubTab = 2`（リフレクション）で窓を `screenshot`（`window` 指定）。メイン（色・コントラスト）と影（割合・色・コントラスト）の行が出ること。メインの色の見本の HEX が `EyeColorHsl.ToColor(今の値)` と一致すること
- [ ] **Step 3: ピッカーの経路**:
  - 型は MToonMaid の DLL から引く（MTE・SceneEditor も同名の `COM3D2.MotionTimelineEditor.ColorPickerWindow` を自前でコンパイルしているため）: `typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).Assembly.GetType("COM3D2.MotionTimelineEditor.ColorPickerWindow")` の static `instance`
  - [編集] を押す代わりに、eval で窓の色の行と同じキー（`"目/瞳の色/" + guid + "/" + side + "/メイン"`）を使って `Open(key, color, color, false, null, rect)` でピッカーを開く。数フレーム待ち、窓の `Sync` でピッカーの private `_onColorChanged` が窓のコールバックに置き換わったことを確かめる
  - 書き込み回数を数えるため、`profile_add` で `COM3D25.MToonMaid.Plugin.MaidEyeColor.Write` を計測対象にする（使えなければ Harmony の prefix でカウンタを足す。ledger に裁定を残す）
  - ピッカー自身の L370〜371 と同じ動きを再現する: private `_targetColor` を青（0, 0, 1）にし、`_onColorChanged` を取り出して青で呼ぶ（`_fieldCache` を書き換えても次の `OnGUI` が `_targetColor` で上書きするため効かない）
  - 次のフレームで `GetPartsColor(EYE_L)` の色相・彩度・明度が 170・255・255 になること。その後 10 フレーム以上たっても `Write` の呼び出し回数が 1 のままであること（量子化で書き込みを繰り返さない）。瞳のアップを撮って青いこと。ピッカーを閉じ、計測を外す
- [ ] **Step 4: 範囲外の明度**: `MaidEyeColor.Write` を通さず `SetPartsColor` で左右の明度を 500 にする（`m_nMainBrightness = 500`）。数フレーム後も 500 のまま（窓を開いて瞳の色を表示していても書かれない）こと。見本が白であること
- [ ] **Step 5: 後片付けとログ**: 控えた値を左右それぞれへ書き戻し、`eyePartsTab` を戻す。`tail_log` に MToonMaid 由来の例外が無いこと
- [ ] **Step 6: コミット**: 実機確認で直した点があれば `fix:` でコミットする。無ければコミットしない

## レビュー却下メモ

- R を隠す — MTEUtils の `DrawColor` は R を常に描き、MTEUtils は編集しない。戻し先を今の色にして何も起きないようにする
- 明度の範囲外を残すためにスライダーも残す — ユーザーが「色の行＋コントラスト（＋影の割合）だけ」を選んだ
- ピッカーの HSV が量子化した色から作り直され、灰色・黒付近で色相バーが 0 へ飛ぶのを防ぐ — `Sync` へ渡す色を変えるには MTEUtils の編集が要る。spec と README に挙動として書くにとどめる
