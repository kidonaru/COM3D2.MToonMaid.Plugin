# COM3D25.MToonMaid.Plugin 目タブのサブタブと瞳の色 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 目タブを [影・明るさ] [ハイライト] [瞳の色] のサブタブに分け、瞳の色のサブタブでゲームのパーツカラー（EYE_L / EYE_R、エディットと同じ全項目、左右 / 左 / 右）を変えられるようにする。

**Architecture:** パーツカラーの値・範囲・左右の選び方は Unity に依存しない型（`EyeColorValues` / `EyeColorSide` / `EyeColorSides`）にして単体テストする。ゲームの型との読み書き（`MaidParts.PartsColor`・`maid.status.eyePartsTab`）は薄いアダプタ `MaidEyeColor` に閉じ込め、実機で確かめる。ウィンドウは `DrawEyeSettings` をサブタブごとのメソッドに分ける。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-eye-gloss-design.md` の「改訂 2026-10-10 (2): 目タブのサブタブと瞳の色」節

## Global Constraints

- main で作業する。コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- ゲームを終了する前に、シーンの保存の有無をユーザーに確認する（`/com3d25-devbridge:restart-verify` をユーザーが起動した場合はそれを承認とみなす）
- 範囲（エディットの `ColorPaletteUIManager` と同じ整数）: 色相 0〜255、彩度 0〜255、明度 0〜510、コントラスト 0〜200、影の割合 0〜255
- 書くときは読んだ `PartsColor` の値だけを差し替え、`m_bUse` は残す
- 左右の選択は `maid.status.eyePartsTab`（`LR` / `L` / `R`）に入れる。左右では左の値を見せ、両目へ書く。左右へ切り替えたときは左を右へ写す。左右なのに左右の値が違うときは右へ切り替える
- 瞳の色のサブタブは、瞳の光沢の ON/OFF・瞳の光沢のシェーダー（`IsEyeGlossAvailable`）の有無に関係なく使える（MToon のシェーダー自体が読めないときはウィンドウ全体が使えないので対象外）。行のリセット（R）は出さない
- 瞳の色のサブタブを開いただけで、左右の値が違えば `eyePartsTab` が `R` になる（エディットのパレットを開いたときと同じ。ゲームのセーブに入る）。README に書く
- 範囲外の値（MOD など）は、どれか 1 項目を動かしたときに 9 項目まとめて範囲へ丸まる（spec どおり）。README に書く
- パーツカラーの値はプラグインの設定ファイルに持たない（ゲームのセーブに入る）
- パーツカラーの書き込み（`SetPartsColor` → `TBody.MulTexProc` の Blit）はウィンドウの描画中（OnGUI）に呼ばず、描画中はためておいて `MToonMaidWindow.Update()`（プラグインの `Update` から呼ばれる）で書く

## Review Focus

- **左右の値が違う状態で左右が選ばれている**: 右の色を黙って上書きせず、右へ切り替わる → Task 1 のテスト（`ResolveSide`）
- **範囲外の値**（MOD や手編集のセーブ）: 表示はそのまま、書くときに範囲へ丸める → Task 1 のテスト（`Clamped`）
- **メイドの切り替え・エディットでの変更**: ためた書き込みが無ければ毎回の描画で読み直すので古い値を書き戻さない → Task 3 の `DrawEyeColor`、Task 4 Step 3
- **色の変更に対応していない瞳**: 書いても例外にならない → Task 4 Step 3
- **サブタブの切り替えで入力欄の値が混ざらない**（入力欄キャッシュは描いた順番で引かれる。`FloatFieldCache` は毎回 `UpdateValue` で値が違えば文字を更新し、`fieldType` が変わればリセットするので、値が違う欄は混ざらない。値が同じ欄に入力途中の文字が残る程度は許容）→ Task 4 Step 2（同じ値の欄どうしで切り替えて確かめる）
- **ためた書き込みが窓の描画→Update の経路で反映される**: eval で直接 `Write` を呼ぶだけでは確かめられないため、窓の private フィールドをリフレクションで書いて `Update()` を呼ぶ → Task 4 Step 3
- **ためている間のメイドの切り替え**: 前のメイドの書き込みを新しいメイドへ書かない → Task 3 Step 4（ためている間は別のメイドの値を描かない）
- **書き込みの例外**（メイドのロード中・着替え中など）: プラグインの Update を止めず、日本語の警告を 1 回出す → Task 3 Step 1（`FlushEyeColor` の try/catch）
- **ためた書き込みが適用される前の再描画**: 同じフレームの次の OnGUI で古い値を表示してスライダーが戻って見えないよう、ためている間はためた値を表示する → Task 3 Step 4

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/EyeColorValues.cs   (Create: 値・範囲・左右の選び方。Unity に依存しない)
├─ MaidEyeColor.cs              (Create: ゲームのパーツカラーとの読み書き)
└─ UI/MToonMaidWindow.cs        (Modify: サブタブ・瞳の色のサブタブ)
source/COM3D25.MToonMaid.Plugin.Tests/EyeColorValuesTests.cs (Create)
README.md                                                    (Modify)
```

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`

---

### Task 1: 値・範囲・左右の選び方

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Settings/EyeColorValues.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/EyeColorValuesTests.cs`

**Interfaces:**
- Produces: `struct EyeColorValues { int mainHue, mainChroma, mainBrightness, mainContrast, shadowRate, shadowHue, shadowChroma, shadowBrightness, shadowContrast; EyeColorValues Clamped(); bool Equals(EyeColorValues) }`、定数 `EyeColorValues.MaxHue = 255` / `MaxChroma = 255` / `MaxBrightness = 510` / `MaxContrast = 200` / `MaxShadowRate = 255`、`enum EyeColorSide { Both, Left, Right }`、`static class EyeColorSides { EyeColorSide ResolveSide(EyeColorSide stored, EyeColorValues left, EyeColorValues right) }`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class EyeColorValuesTests
    {
        private static EyeColorValues Color(int hue)
        {
            return new EyeColorValues
            {
                mainHue = hue, mainChroma = 100, mainBrightness = 255, mainContrast = 100,
                shadowRate = 128, shadowHue = 10, shadowChroma = 20, shadowBrightness = 30, shadowContrast = 40,
            };
        }

        [Fact]
        public void 範囲外の値はエディットのスライダーの範囲へ丸める()
        {
            var v = new EyeColorValues
            {
                mainHue = 300, mainChroma = -5, mainBrightness = 600, mainContrast = 250,
                shadowRate = 999, shadowHue = -1, shadowChroma = 256, shadowBrightness = 511, shadowContrast = 201,
            }.Clamped();

            Assert.Equal(new[] { 255, 0, 510, 200, 255, 0, 255, 510, 200 },
                new[] { v.mainHue, v.mainChroma, v.mainBrightness, v.mainContrast, v.shadowRate, v.shadowHue, v.shadowChroma, v.shadowBrightness, v.shadowContrast });
        }

        [Fact]
        public void 範囲内の値は丸めても変わらない()
        {
            Assert.True(Color(10).Equals(Color(10).Clamped()));
        }

        [Fact]
        public void 左右が選ばれていて左右の値が違うときは右へ切り替える()
        {
            Assert.Equal(EyeColorSide.Right, EyeColorSides.ResolveSide(EyeColorSide.Both, Color(10), Color(20)));
        }

        [Fact]
        public void 左右が選ばれていて左右の値が同じなら左右のまま()
        {
            Assert.Equal(EyeColorSide.Both, EyeColorSides.ResolveSide(EyeColorSide.Both, Color(10), Color(10)));
        }

        [Theory]
        [InlineData(EyeColorSide.Left)]
        [InlineData(EyeColorSide.Right)]
        public void 左か右が選ばれていれば値が違ってもそのまま(EyeColorSide side)
        {
            Assert.Equal(side, EyeColorSides.ResolveSide(side, Color(10), Color(20)));
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルドエラー（`EyeColorValues` / `EyeColorSide` / `EyeColorSides` が無い）

- [ ] **Step 3: 実装する**

`Settings/EyeColorValues.cs`:

```csharp
using System;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>ゲームの瞳のパーツカラー (MaidParts.PartsColor) の値。範囲はエディットのスライダーと同じ整数</summary>
    public struct EyeColorValues : IEquatable<EyeColorValues>
    {
        public const int MaxHue = 255;
        public const int MaxChroma = 255;
        public const int MaxBrightness = 510;
        public const int MaxContrast = 200;
        public const int MaxShadowRate = 255;

        public int mainHue;
        public int mainChroma;
        public int mainBrightness;
        public int mainContrast;
        public int shadowRate;
        public int shadowHue;
        public int shadowChroma;
        public int shadowBrightness;
        public int shadowContrast;

        public EyeColorValues Clamped()
        {
            return new EyeColorValues
            {
                mainHue = Clamp(mainHue, MaxHue),
                mainChroma = Clamp(mainChroma, MaxChroma),
                mainBrightness = Clamp(mainBrightness, MaxBrightness),
                mainContrast = Clamp(mainContrast, MaxContrast),
                shadowRate = Clamp(shadowRate, MaxShadowRate),
                shadowHue = Clamp(shadowHue, MaxHue),
                shadowChroma = Clamp(shadowChroma, MaxChroma),
                shadowBrightness = Clamp(shadowBrightness, MaxBrightness),
                shadowContrast = Clamp(shadowContrast, MaxContrast),
            };
        }

        public bool Equals(EyeColorValues o)
        {
            return mainHue == o.mainHue && mainChroma == o.mainChroma && mainBrightness == o.mainBrightness && mainContrast == o.mainContrast
                && shadowRate == o.shadowRate && shadowHue == o.shadowHue && shadowChroma == o.shadowChroma
                && shadowBrightness == o.shadowBrightness && shadowContrast == o.shadowContrast;
        }

        public override bool Equals(object obj)
        {
            return obj is EyeColorValues && Equals((EyeColorValues)obj);
        }

        public override int GetHashCode()
        {
            return mainHue ^ (mainChroma << 8) ^ (mainBrightness << 16) ^ (shadowHue << 4) ^ (shadowBrightness << 12);
        }

        private static int Clamp(int v, int max)
        {
            return Math.Max(0, Math.Min(max, v));
        }
    }

    /// <summary>瞳の色を変える目。ゲームの EyePartsTab (LR / L / R) と対応する</summary>
    public enum EyeColorSide
    {
        Both,
        Left,
        Right,
    }

    public static class EyeColorSides
    {
        /// <summary>
        /// 左右が選ばれていて左右の値が違うときは右にする (エディットのパレットを開いたときと同じ。
        /// 左右のまま書くと右の色を黙って上書きする)
        /// </summary>
        public static EyeColorSide ResolveSide(EyeColorSide stored, EyeColorValues left, EyeColorValues right)
        {
            return stored == EyeColorSide.Both && !left.Equals(right) ? EyeColorSide.Right : stored;
        }
    }
}
```

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

`feat: 瞳の色の値と左右の選び方を足す`

---

### Task 2: ゲームのパーツカラーとの読み書き

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/MaidEyeColor.cs`

**Interfaces:**
- Consumes: Task 1 の `EyeColorValues` / `EyeColorSide` / `EyeColorSides`
- Produces: `static class MaidEyeColor { EyeColorSide GetSide(Maid); void SetSide(Maid, EyeColorSide); EyeColorValues Read(Maid, EyeColorSide); void Write(Maid, EyeColorSide, EyeColorValues); EyeColorSide ResolveAndStoreSide(Maid) }`

ゲームの型（`MaidParts` / `MaidStatus.EyePartsTab`）に触るため単体テストはしない（テストのプロジェクトは Unity のネイティブ呼び出しを含まない型だけを扱う）。実機の確認は Task 4。

- [ ] **Step 1: 実装する**

```csharp
using MaidStatus;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// ゲームの瞳のパーツカラー (EYE_L / EYE_R) の読み書き。値はゲームのセーブに入る。
    /// 左右の選択はエディットと共有する maid.status.eyePartsTab
    /// </summary>
    public static class MaidEyeColor
    {
        public static EyeColorSide GetSide(Maid maid)
        {
            switch (maid.status.eyePartsTab)
            {
                case EyePartsTab.L: return EyeColorSide.Left;
                case EyePartsTab.R: return EyeColorSide.Right;
                default: return EyeColorSide.Both;
            }
        }

        /// <summary>左右へ切り替えたときは左を右へ写す (エディットと同じ。右の色は失われる)</summary>
        public static void SetSide(Maid maid, EyeColorSide side)
        {
            maid.status.eyePartsTab = side == EyeColorSide.Left ? EyePartsTab.L : side == EyeColorSide.Right ? EyePartsTab.R : EyePartsTab.LR;
            if (side == EyeColorSide.Both)
            {
                Write(maid, EyeColorSide.Right, Read(maid, EyeColorSide.Left));
            }
        }

        /// <summary>左右の値が違うのに左右が選ばれていれば右へ切り替えて返す</summary>
        public static EyeColorSide ResolveAndStoreSide(Maid maid)
        {
            var stored = GetSide(maid);
            var side = EyeColorSides.ResolveSide(stored, Read(maid, EyeColorSide.Left), Read(maid, EyeColorSide.Right));
            if (side != stored)
            {
                maid.status.eyePartsTab = EyePartsTab.R;
            }
            return side;
        }

        /// <summary>左右 (Both) では左の値</summary>
        public static EyeColorValues Read(Maid maid, EyeColorSide side)
        {
            var c = maid.Parts.GetPartsColor(side == EyeColorSide.Right ? MaidParts.PARTS_COLOR.EYE_R : MaidParts.PARTS_COLOR.EYE_L);
            return new EyeColorValues
            {
                mainHue = c.m_nMainHue,
                mainChroma = c.m_nMainChroma,
                mainBrightness = c.m_nMainBrightness,
                mainContrast = c.m_nMainContrast,
                shadowRate = c.m_nShadowRate,
                shadowHue = c.m_nShadowHue,
                shadowChroma = c.m_nShadowChroma,
                shadowBrightness = c.m_nShadowBrightness,
                shadowContrast = c.m_nShadowContrast,
            };
        }

        /// <summary>範囲へ丸めて書く。左右 (Both) では両目へ書く</summary>
        public static void Write(Maid maid, EyeColorSide side, EyeColorValues values)
        {
            var v = values.Clamped();
            if (side != EyeColorSide.Right)
            {
                Set(maid, MaidParts.PARTS_COLOR.EYE_L, v);
            }
            if (side != EyeColorSide.Left)
            {
                Set(maid, MaidParts.PARTS_COLOR.EYE_R, v);
            }
        }

        private static void Set(Maid maid, MaidParts.PARTS_COLOR type, EyeColorValues v)
        {
            // 新しい PartsColor を作ると m_bUse が落ちる。エディットは落とすが、セーブに残る値なので元のままにする
            var c = maid.Parts.GetPartsColor(type);
            c.m_nMainHue = v.mainHue;
            c.m_nMainChroma = v.mainChroma;
            c.m_nMainBrightness = v.mainBrightness;
            c.m_nMainContrast = v.mainContrast;
            c.m_nShadowRate = v.shadowRate;
            c.m_nShadowHue = v.shadowHue;
            c.m_nShadowChroma = v.shadowChroma;
            c.m_nShadowBrightness = v.shadowBrightness;
            c.m_nShadowContrast = v.shadowContrast;
            maid.Parts.SetPartsColor(type, c);
        }
    }
}
```

（`EyePartsTab` の名前空間が `MaidStatus` でなければ、ビルドエラーに合わせて `using` を直す。逆コンパイル済みソースでは `Assembly-CSharp/MaidStatus/EyePartsTab.cs`）

- [ ] **Step 2: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 0 エラー / 全件 PASS

- [ ] **Step 3: コミット**

`feat: ゲームの瞳のパーツカラーを読み書きする`

---

### Task 3: 目タブをサブタブに分け、瞳の色のサブタブを描く

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`

**Interfaces:**
- Consumes: Task 2 の `MaidEyeColor`、Task 1 の `EyeColorValues` の定数と `EyeColorSide`、既存の `GUIView.DrawTabs(IList<string>, int, float, float)`、`GUIView.SliderOption`（`fieldType` / `snapStep` / `hiddenResetButton`）

- [ ] **Step 1: 状態と定数を足す**

定数の並び（`PairDragSensitivity` の後）に:

```csharp
        private const int EyeShadeSubTab = 0;
        private const int EyeHighlightSubTab = 1;
        private const int EyeColorSubTab = 2;
        private static readonly string[] EyeSubTabLabels = { "影・明るさ", "ハイライト", "瞳の色" };
        private static readonly string[] EyeColorSideLabels = { "左右", "左", "右" };
        // 「影・明るさ」が TabWidth (52) に収まらない
        private const float EyeSubTabWidth = 90f;
```

フィールド（`_highlightIndex` の後）に:

```csharp
        // 選んでいるサブタブ。メイドを切り替えても変えない
        private int _eyeSubTab = EyeShadeSubTab;
        // 描画中に SetPartsColor を呼ぶと、ゲームのテクスチャの作り直し (Blit) が GUI の描画中に走るため、Update まで遅らせる
        private Maid _eyeColorMaid;
        private EyeColorSide _eyeColorSide;
        private EyeColorValues _eyeColorValues;
        private bool _eyeColorWritePending;
        private bool _eyeColorSidePending;
        private bool _eyeColorWarned;
```

（`MToonMaidPlugin.Log` は既存の `ManualLogSource`。名前が違えばプラグインの既存のログの出し方に合わせる）

`Update()` を次にする:

```csharp
        public override void Update()
        {
            base.Update();
            _placement.FlushIfReleased();
            FlushEyeColor();
        }

        private void FlushEyeColor()
        {
            var maid = _eyeColorMaid;
            var side = _eyeColorSidePending;
            var write = _eyeColorWritePending;
            // 書く前に降ろす (例外が出ても毎フレーム同じ書き込みを繰り返さない)
            _eyeColorSidePending = false;
            _eyeColorWritePending = false;
            if (maid == null || maid.body0 == null || !(side || write))
            {
                return;
            }
            try
            {
                if (side)
                {
                    MaidEyeColor.SetSide(maid, _eyeColorSide);
                }
                if (write)
                {
                    MaidEyeColor.Write(maid, _eyeColorSide, _eyeColorValues);
                }
            }
            catch (Exception e)
            {
                // 窓の Update はプラグインの Update の try の外で呼ばれ、例外を投げるとそのフレームの走査が止まる
                if (!_eyeColorWarned)
                {
                    _eyeColorWarned = true;
                    MToonMaidPlugin.Log.LogWarning("瞳の色を変えられませんでした: " + e.Message);
                }
            }
        }
```

- [ ] **Step 2: サブタブを描く**

`DrawContent` の目タブの分岐（`DrawEyeSettings(view); DrawEyeResetButton(view);`）を次に置き換える。サブタブの行はスクロールの外（`瞳を光沢のシェーダーにする` の直後）に置くため、`view.DrawHorizontalLine(Color.gray);` の前で描く:

`if (eyeTab) { var eye = _settings.eye; view.DrawToggle("瞳を光沢のシェーダーにする", ...); }` の `DrawToggle` の後に:

```csharp
                _eyeSubTab = view.DrawTabs((IList<string>)EyeSubTabLabels, _eyeSubTab, EyeSubTabWidth, RowHeight);
```

スクロールの中の目タブの分岐を:

```csharp
            if (eyeTab)
            {
                if (_eyeSubTab == EyeColorSubTab)
                {
                    DrawEyeColor(view, maid);
                }
                else
                {
                    DrawEyeSettings(view);
                    DrawEyeResetButton(view);
                }
            }
```

- [ ] **Step 3: DrawEyeSettings をサブタブで分ける**

`DrawEyeSettings` / `DrawEyeResetButton` の直前までを次の 3 メソッドに置き換える（`DrawEyeShade` と `DrawEyeHighlights` の行は今の `DrawEyeSettings` の L321〜L357 をそのまま振り分けたもの。`eye_hi を消す` はハイライトの先頭へ移る）:

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
            if (_eyeSubTab == EyeShadeSubTab)
            {
                DrawEyeShade(view, eye, def, editable);
            }
            else
            {
                DrawEyeHighlights(view, eye, def, editable);
            }
        }

        private void DrawEyeShade(GUIView view, EyeSettings eye, EyeSettings def, bool editable)
        {
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
        }

        private void DrawEyeHighlights(GUIView view, EyeSettings eye, EyeSettings def, bool editable)
        {
            EyeToggle(view, "eye_hi を消す", eye.hideEyeHighlight, v => eye.hideEyeHighlight = v, editable);

            view.DrawLabel("ハイライト", -1, RowHeight);
            // 番号の行より前で寄せる (範囲外のまま [削除] を押すと RemoveAt が例外になる)
            _highlightIndex = HighlightSelection.Clamp(_highlightIndex, eye.highlights.Count);
            DrawHighlightTabs(view, eye, editable);
            // 0 個のときも行は描く (入力欄キャッシュの番号がずれるため)。値は新規の既定を無効表示で見せる
            var hasHighlight = eye.highlights.Count > 0;
            var h = hasHighlight ? eye.highlights[_highlightIndex] : _emptyHighlight;
            var hd = _newHighlight;
            var hEditable = editable && hasHighlight;
            EyeSlider(view, "強さ", h.strength, 0f, 1f, hd.strength, v => h.strength = v, hEditable);
            // 番号ごとに別のキーにする (同じキーだと、ピッカーを開いたまま番号を変えたとき前の色を指したままになりうる)
            EyeColorRow(view, "目/ハイライト" + (_highlightIndex + 1) + "の色", "色", h.color, hd.color, hEditable);
            PairRow(view, "位置", "X", "Y", h.x, h.y, -EyeSettings.MaxHighlightOffset, EyeSettings.MaxHighlightOffset, hd.x, hd.y,
                (a, b) => { h.x = a; h.y = b; }, hEditable);
            PairRow(view, "大きさ", "W", "H", h.width, h.height, 0f, EyeSettings.MaxHighlightSize, hd.width, hd.height,
                (a, b) => { h.width = a; h.height = b; }, hEditable);
            EyeSlider(view, "ロール", h.roll, -EyeSettings.MaxHighlightRoll, EyeSettings.MaxHighlightRoll, hd.roll, v => h.roll = v, hEditable);
            EyeColorRow(view, "目/ハイライト" + (_highlightIndex + 1) + "のにじみの色", "にじみの色", h.glowColor, hd.glowColor, hEditable, true);
            EyeSlider(view, "にじみの強さ", h.glowStrength, 0f, EyeSettings.MaxGlowStrength, hd.glowStrength, v => h.glowStrength = v, hEditable);
            PairRow(view, "にじみの幅", "W", "H", h.glowWidth, h.glowHeight, 0f, EyeSettings.MaxGlowWidth, hd.glowWidth, hd.glowHeight,
                (a, b) => { h.glowWidth = a; h.glowHeight = b; }, hEditable);

            view.DrawLabel("ハイライトの動き", -1, RowHeight);
            EyeSlider(view, "視差", eye.parallax, -EyeSettings.MaxParallax, EyeSettings.MaxParallax, def.parallax, v => eye.parallax = v, editable);
            EyeSlider(view, "まぶたで中央へ", eye.lidFollow, 0f, 1f, def.lidFollow, v => eye.lidFollow = v, editable);
        }
```

着手時に今の `DrawEyeSettings` の行と上のコードを見比べ、行の中身に差があれば今のコードの方を使い、差を ledger に裁定として残す。

- [ ] **Step 4: 瞳の色のサブタブを描く**

```csharp
        /// <summary>
        /// ゲームの瞳のパーツカラー。ためた書き込みが無ければ毎回メイドの今の値を読み直す
        /// (メイドの切り替えやエディットでの変更に追従するため)。書き込みは Update で行う
        /// </summary>
        private void DrawEyeColor(GUIView view, Maid maid)
        {
            var pending = _eyeColorWritePending || _eyeColorSidePending;
            if (pending && _eyeColorMaid != maid)
            {
                // 前のメイドの書き込みが Update で済むまで描かない (ためた値を新しいメイドへ書かないため。1 フレームで終わる)
                return;
            }
            if (!pending)
            {
                _eyeColorMaid = maid;
                _eyeColorSide = MaidEyeColor.ResolveAndStoreSide(maid);
                _eyeColorValues = MaidEyeColor.Read(maid, _eyeColorSide);
            }
            view.SetEnabled(_baseEnabled);
            view.BeginHorizontal();
            view.DrawLabel("変える目", LabelWidth, RowHeight);
            var selected = view.DrawTabs((IList<string>)EyeColorSideLabels, (int)_eyeColorSide, ChoiceWidth, RowHeight);
            view.EndLayout();
            if (selected != (int)_eyeColorSide)
            {
                _eyeColorSide = (EyeColorSide)selected;
                _eyeColorSidePending = true;
                // 左右へ切り替えると右は左の値になるため左を見せる。右を選んだら右の今の値を見せる
                _eyeColorValues = MaidEyeColor.Read(maid, _eyeColorSide == EyeColorSide.Right ? EyeColorSide.Right : EyeColorSide.Left);
            }

            view.DrawLabel("メイン", -1, RowHeight);
            EyeColorSlider(view, "色相", _eyeColorValues.mainHue, EyeColorValues.MaxHue, v => _eyeColorValues.mainHue = v);
            EyeColorSlider(view, "彩度", _eyeColorValues.mainChroma, EyeColorValues.MaxChroma, v => _eyeColorValues.mainChroma = v);
            EyeColorSlider(view, "明度", _eyeColorValues.mainBrightness, EyeColorValues.MaxBrightness, v => _eyeColorValues.mainBrightness = v);
            EyeColorSlider(view, "コントラスト", _eyeColorValues.mainContrast, EyeColorValues.MaxContrast, v => _eyeColorValues.mainContrast = v);
            view.DrawLabel("影", -1, RowHeight);
            EyeColorSlider(view, "割合", _eyeColorValues.shadowRate, EyeColorValues.MaxShadowRate, v => _eyeColorValues.shadowRate = v);
            EyeColorSlider(view, "色相", _eyeColorValues.shadowHue, EyeColorValues.MaxHue, v => _eyeColorValues.shadowHue = v);
            EyeColorSlider(view, "彩度", _eyeColorValues.shadowChroma, EyeColorValues.MaxChroma, v => _eyeColorValues.shadowChroma = v);
            EyeColorSlider(view, "明度", _eyeColorValues.shadowBrightness, EyeColorValues.MaxBrightness, v => _eyeColorValues.shadowBrightness = v);
            EyeColorSlider(view, "コントラスト", _eyeColorValues.shadowContrast, EyeColorValues.MaxContrast, v => _eyeColorValues.shadowContrast = v);
        }

        /// <summary>
        /// 整数のスライダー。リセット (R) は出さない (パーツカラーの既定値をゲームが持っていないため)。
        /// 値はゲームのセーブに入るだけなので、_eyeChanged (プラグインの設定の保存) は立てない
        /// </summary>
        private void EyeColorSlider(GUIView view, string label, int value, int max, Action<int> set)
        {
            view.SetEnabled(_baseEnabled);
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = LabelWidth,
                width = -1,
                fieldType = FloatFieldType.Int,
                snapStep = 1f,
                min = 0f,
                max = max,
                value = value,
                hiddenResetButton = true,
                onChanged = v =>
                {
                    set(Mathf.RoundToInt(v));
                    _eyeColorWritePending = true;
                },
            });
        }
```

（`set` は `_eyeColorValues` の 1 項目を書き換えるラムダ。`_eyeColorValues` はフィールドなのでラムダから直接書ける。`ChoiceWidth` は既存の定数 50。`(int)side` は `EyeColorSide` の並び Both=0 / Left=1 / Right=2 が `EyeColorSideLabels` の並びと一致することに依存する）

- [ ] **Step 5: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 0 エラー / 全件 PASS

- [ ] **Step 6: コミット**

`feat: 目タブをサブタブに分け、瞳の色をゲームのパーツカラーで変えられるようにする`

---

### Task 4: 実機で確かめ、文書を合わせる

`/com3d25-devbridge:restart-verify` の手順で反映する（ゲームの終了はユーザーの確認後）。メイドの設定ファイルとパーツカラーの元の値を控えてから変え、最後に戻す。

- [ ] **Step 1: 反映して起動・ロード**（`debug.bat com3d25` → 起動 → スロット 42 をロード → `reset_evaluator`）
- [ ] **Step 2: サブタブ**: 目タブを `screenshot`（`window` 指定）で撮り、[影・明るさ][ハイライト][瞳の色] が並び、各サブタブに spec どおりの行が出ること。影・明るさとハイライトを行き来して、スライダーの値が混ざらないこと（`_tab` と `_eyeSubTab` は private なので eval でリフレクションで書き換えて撮る。同じ値（例: 両方 0.50）の欄が同じ番号に来る組み合わせも撮る）
- [ ] **Step 3: 瞳の色（窓の経路）**: 窓の `_eyeSubTab` を瞳の色にして撮る。eval で窓の private フィールド `_eyeColorValues`（色相を変えた値）と `_eyeColorWritePending = true` をリフレクションで書き、次のフレームの `GetPartsColor(EYE_L/EYE_R)` が変わり `_eyeColorWritePending` が false に戻ること（窓の `Update` で書かれた）を確かめる。同じく `_eyeColorSide = Both` と `_eyeColorSidePending = true` で、右が左の値になり `eyePartsTab` が `LR` になること
- [ ] **Step 3b: 瞳の色（読み書き）**: eval で `MaidEyeColor.Read` の元の値を控える。`MaidEyeColor.Write(maid, EyeColorSide.Both, …)` で色相を変え、瞳のアップを撮って色が変わること、`GetPartsColor(EYE_L)` と `EYE_R` が同じ値になること、`m_bUse` が変わらないことを確かめる。左だけ別の値にして `ResolveAndStoreSide` が右を返し `maid.status.eyePartsTab` が `R` になること。左右へ切り替えて右が左の値になること。範囲外（色相 300）を書いて 255 に丸まること。ログに MToonMaid 由来の例外が無いこと。最後に控えた値を左右それぞれへ書き戻し、`eyePartsTab` も元へ戻す
- [ ] **Step 4: 文書**: README の使い方 6 の後ろに「目タブは [影・明るさ] [ハイライト] [瞳の色] のサブタブに分かれています。[瞳の色] ではエディットの「瞳の色」と同じパーツカラー（左右 / 左 / 右、メインと影の色相・彩度・明度・コントラスト、影の割合）を変えられます。瞳の光沢を OFF にしていても使えます。値はゲームのセーブに保存されます（プラグインの設定ファイルには入りません）。エディットと同じく、左右が選ばれていて左右の色が違うときは [瞳の色] を開いた時点で「右」に切り替わり、「左右」へ切り替えると右の色は左と同じになります。範囲外の値（MOD など）は、どれかを動かしたときにエディットの範囲へ丸まります」を足す
- [ ] **Step 5: コミット**: `docs: 目タブのサブタブと瞳の色を README に書く`

## レビュー却下メモ

- 同じフレームでスライダーの変更と左右の切り替えが重なると前者が失われる — IMGUI では 1 フレームに複数のポインタ操作は実質起きないため許容
