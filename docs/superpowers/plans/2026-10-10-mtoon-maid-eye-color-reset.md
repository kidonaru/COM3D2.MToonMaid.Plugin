# COM3D25.MToonMaid.Plugin 瞳の色を編集前に戻す Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 瞳の色のサブタブに、行ごとの R（編集前の値へ）と「瞳の色を編集前に戻す」ボタン（左右の目と左右の選択をまとめて戻す）を足す。

**Architecture:** 編集前の控え（左右の 9 値と左右の選択）を Unity に依存しない型 `EyeColorSnapshot` / `EyeColorBaselines` にして単体テストする。ゲームとの読み書きは `MaidEyeColor.ReadSnapshot` / `Restore` に足す。ウィンドウは表示の最初に控えを取り、R とボタンを既存の「ためて Update で書く」経路に乗せる。シーンが変わったら控えを捨てる。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-eye-gloss-design.md` の「改訂 2026-10-10 (4): 瞳の色を編集前に戻す」節

## Global Constraints

- main で作業する。コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- ゲームを終了する前に、シーンの保存の有無をユーザーに確認する
- 控えはメイドの `maid.status.guid` ごと。左右の値が違うときに右へ切り替える処理（`ResolveAndStoreSide`）より前に取る
- シーンが変わったら控えを捨てる。控えは設定ファイルにもセーブにも入れない
- 行の R は今表示している目の控え（左右では左）へ戻す。色の行は 3 値を控えの整数のまま戻す
- 「瞳の色を編集前に戻す」は左右の 9 値と左右の選択を丸めずに戻し、ためている他の書き込みより優先して捨てる
- 書き込みは描画中に行わず Update で書く

## Review Focus

- **色で表せない明度（127 未満・382 超え）の行を R で戻す**: 色を経由せず控えの整数に戻る → Task 3 Step 3（実機で明度 67 の影の色を変えてから R）
- **左右の値が違う状態の控え**: 戻すと左右の値と `eyePartsTab` が控えどおりになり、次の描画で改訂 (2) どおり右へ切り替わる → Task 1 のテスト（`ValuesFor`）、Task 3 Step 4
- **セーブのロード後**: 前のセーブの色へ戻さない（控えを捨てて取り直す）→ Task 3 Step 5
- **ためている書き込みとボタンが同じフレームで重なる**: ボタンが勝ち、他は捨てる → Task 2 Step 3（`FlushEyeColor` の分岐）
- **控えを取る前に別メイドの書き込みがたまっている**: 今の早期 return（前のメイドの書き込みが済むまで描かない）が控えの取得より前にあるので、他のメイドの途中の値を控えにしない → Task 2 Step 2

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/EyeColorSnapshot.cs  (Create: 控えの値と、メイドごとの控え。Unity に依存しない)
├─ MaidEyeColor.cs               (Modify: ReadSnapshot / Restore)
└─ UI/MToonMaidWindow.cs         (Modify: 控えの取得・行の R・戻すボタン・シーン変更で捨てる)
source/COM3D25.MToonMaid.Plugin.Tests/EyeColorSnapshotTests.cs (Create)
README.md                                                     (Modify)
```

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`

---

### Task 1: 控えの型

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Settings/EyeColorSnapshot.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/EyeColorSnapshotTests.cs`

**Interfaces:**
- Consumes: 既存の `EyeColorValues`、`EyeColorSide`（`Settings/EyeColorValues.cs`）
- Produces: `struct EyeColorSnapshot { EyeColorValues left; EyeColorValues right; EyeColorSide side; EyeColorValues ValuesFor(EyeColorSide shown) }`、`sealed class EyeColorBaselines { EyeColorSnapshot GetOrCapture(string key, Func<EyeColorSnapshot> capture); void Clear() }`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class EyeColorSnapshotTests
    {
        private static EyeColorSnapshot Snapshot(int leftHue, int rightHue, EyeColorSide side)
        {
            return new EyeColorSnapshot
            {
                left = new EyeColorValues { mainHue = leftHue },
                right = new EyeColorValues { mainHue = rightHue },
                side = side,
            };
        }

        [Theory]
        [InlineData(EyeColorSide.Both, 10)]
        [InlineData(EyeColorSide.Left, 10)]
        [InlineData(EyeColorSide.Right, 20)]
        public void 表示している目の控えの値を返し左右では左を返す(EyeColorSide shown, int expectedHue)
        {
            Assert.Equal(expectedHue, Snapshot(10, 20, EyeColorSide.Both).ValuesFor(shown).mainHue);
        }

        [Fact]
        public void 同じメイドの控えは最初の1回だけ取る()
        {
            var baselines = new EyeColorBaselines();
            var calls = 0;
            baselines.GetOrCapture("a", () => { calls++; return Snapshot(10, 10, EyeColorSide.Both); });

            var second = baselines.GetOrCapture("a", () => { calls++; return Snapshot(99, 99, EyeColorSide.Right); });

            Assert.Equal(1, calls);
            Assert.Equal(10, second.left.mainHue);
            Assert.Equal(EyeColorSide.Both, second.side);
        }

        [Fact]
        public void メイドごとに別の控えを持つ()
        {
            var baselines = new EyeColorBaselines();
            baselines.GetOrCapture("a", () => Snapshot(10, 10, EyeColorSide.Both));

            var b = baselines.GetOrCapture("b", () => Snapshot(30, 40, EyeColorSide.Left));

            Assert.Equal(30, b.left.mainHue);
            Assert.Equal(10, baselines.GetOrCapture("a", () => Snapshot(99, 99, EyeColorSide.Both)).left.mainHue);
        }

        [Fact]
        public void 捨てたあとは控えを取り直す()
        {
            var baselines = new EyeColorBaselines();
            baselines.GetOrCapture("a", () => Snapshot(10, 10, EyeColorSide.Both));
            baselines.Clear();

            var again = baselines.GetOrCapture("a", () => Snapshot(50, 60, EyeColorSide.Right));

            Assert.Equal(50, again.left.mainHue);
            Assert.Equal(EyeColorSide.Right, again.side);
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルドエラー（`EyeColorSnapshot` / `EyeColorBaselines` が無い）

- [ ] **Step 3: 実装する**

`Settings/EyeColorSnapshot.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>瞳の色の編集前の控え。左右の目の値と左右の選択</summary>
    public struct EyeColorSnapshot
    {
        public EyeColorValues left;
        public EyeColorValues right;
        public EyeColorSide side;

        /// <summary>左右 (Both) では左の値</summary>
        public EyeColorValues ValuesFor(EyeColorSide shown)
        {
            return shown == EyeColorSide.Right ? right : left;
        }
    }

    /// <summary>メイドごとの編集前の控え。シーンが変わったら Clear する</summary>
    public sealed class EyeColorBaselines
    {
        private readonly Dictionary<string, EyeColorSnapshot> _snapshots = new Dictionary<string, EyeColorSnapshot>();

        public EyeColorSnapshot GetOrCapture(string key, Func<EyeColorSnapshot> capture)
        {
            EyeColorSnapshot snapshot;
            if (!_snapshots.TryGetValue(key, out snapshot))
            {
                snapshot = capture();
                _snapshots[key] = snapshot;
            }
            return snapshot;
        }

        public void Clear()
        {
            _snapshots.Clear();
        }
    }
}
```

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

`feat: 瞳の色の編集前の控えを持つ`

---

### Task 2: ゲームとの読み書きとウィンドウ

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidEyeColor.cs`（`ResolveAndStoreSide` の後に 2 メソッド）
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（フィールド、`FlushEyeColor` L127〜159、`DrawEyeColor` L441〜494、`EyeColorSlider` L496〜520、`EyePartsColorRow` L522〜537、`OnChangedSceneLevel` を追加）
- Modify: `README.md`（使い方 7）

**Interfaces:**
- Consumes: Task 1 の `EyeColorSnapshot` / `EyeColorBaselines`、既存の `MaidEyeColor.Read` / `GetSide` / private `Set`、`DockableWindowBase.OnChangedSceneLevel(Scene, LoadSceneMode)`（virtual、`WindowManagerBase` が各ウィンドウへ流す。プラグインの `SceneManager.sceneLoaded` から呼ばれる）、`GUIView.SliderOption.defaultValue` / `hiddenResetButton`、`GUIView.DrawButton(string, float, float)`、定数 `ResetButtonWidth`
- Produces: `MaidEyeColor.ReadSnapshot(Maid) : EyeColorSnapshot`、`MaidEyeColor.Restore(Maid, EyeColorSnapshot)`

ゲームの型に触るため単体テストはしない（Task 3 で実機確認）。

- [ ] **Step 1: MaidEyeColor に控えの読み書きを足す**

`ResolveAndStoreSide` の後に:

```csharp
        public static EyeColorSnapshot ReadSnapshot(Maid maid)
        {
            return new EyeColorSnapshot
            {
                left = Read(maid, EyeColorSide.Left),
                right = Read(maid, EyeColorSide.Right),
                side = GetSide(maid),
            };
        }

        /// <summary>左右の値と左右の選択を控えのまま戻す (範囲へ丸めない。左を右へ写さない)</summary>
        public static void Restore(Maid maid, EyeColorSnapshot snapshot)
        {
            maid.status.eyePartsTab = ToTab(snapshot.side);
            Set(maid, MaidParts.PARTS_COLOR.EYE_L, snapshot.left);
            Set(maid, MaidParts.PARTS_COLOR.EYE_R, snapshot.right);
        }
```

`SetSide` の switch を `maid.status.eyePartsTab = ToTab(side);` にし、次を足す（`Restore` と共有する）:

```csharp
        private static EyePartsTab ToTab(EyeColorSide side)
        {
            switch (side)
            {
                case EyeColorSide.Left: return EyePartsTab.L;
                case EyeColorSide.Right: return EyePartsTab.R;
                default: return EyePartsTab.LR;
            }
        }
```

- [ ] **Step 2: 控えを取り、行の R とボタンを描く**

フィールド（`_eyeColorShownSide` の後）:

```csharp
        private readonly EyeColorBaselines _eyeColorBaselines = new EyeColorBaselines();
        private EyeColorSnapshot _eyeColorRestore;
        private bool _eyeColorRestorePending;
```

`OnChangedSceneLevel` を足す（`Update` の後）:

```csharp
        public override void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            base.OnChangedSceneLevel(scene, sceneMode);
            // 追加読み込みで捨てると、編集の途中で編集前が編集後の値に変わる
            if (sceneMode == LoadSceneMode.Single)
            {
                // 控えを残すと、セーブをロードしたあとに前のセーブの色へ戻してしまう
                _eyeColorBaselines.Clear();
            }
        }
```

（`using UnityEngine.SceneManagement;` を足す）

`DrawEyeColor` を次のように変える:

1. `var pending = _eyeColorWritePending || _eyeColorSidePending;` → `var pending = _eyeColorWritePending || _eyeColorSidePending || _eyeColorRestorePending;`
2. 早期 return の直後、`if (!pending)` の前に:

```csharp
            // 右へ切り替える処理 (ResolveAndStoreSide) より前に取る。後だと控えの左右の選択が右になる
            var baseline = _eyeColorBaselines.GetOrCapture(maid.status.guid, () => MaidEyeColor.ReadSnapshot(maid));
```

3. 行の描画を次にする（`b` は今表示している目の控え）:

```csharp
            var b = baseline.ValuesFor(_eyeColorSide);
            view.DrawLabel("メイン", -1, RowHeight);
            EyePartsColorRow(view, EyeMainColorKey, _eyeMainPicked, _eyeColorValues.mainHue, _eyeColorValues.mainChroma, _eyeColorValues.mainBrightness,
                b.mainHue, b.mainChroma, b.mainBrightness,
                (h, s, v) => { _eyeColorValues.mainHue = h; _eyeColorValues.mainChroma = s; _eyeColorValues.mainBrightness = v; });
            EyeColorSlider(view, "コントラスト", _eyeColorValues.mainContrast, EyeColorValues.MaxContrast, b.mainContrast, v => _eyeColorValues.mainContrast = v);
            view.DrawLabel("影", -1, RowHeight);
            EyeColorSlider(view, "割合", _eyeColorValues.shadowRate, EyeColorValues.MaxShadowRate, b.shadowRate, v => _eyeColorValues.shadowRate = v);
            EyePartsColorRow(view, EyeShadowColorKey, _eyeShadowPicked, _eyeColorValues.shadowHue, _eyeColorValues.shadowChroma, _eyeColorValues.shadowBrightness,
                b.shadowHue, b.shadowChroma, b.shadowBrightness,
                (h, s, v) => { _eyeColorValues.shadowHue = h; _eyeColorValues.shadowChroma = s; _eyeColorValues.shadowBrightness = v; });
            EyeColorSlider(view, "コントラスト", _eyeColorValues.shadowContrast, EyeColorValues.MaxContrast, b.shadowContrast, v => _eyeColorValues.shadowContrast = v);

            if (view.DrawButton("瞳の色を編集前に戻す", ResetButtonWidth, RowHeight))
            {
                _eyeColorRestore = baseline;
                _eyeColorRestorePending = true;
                // 戻す値と食い違う書き込みを後から重ねない
                _eyeColorWritePending = false;
                _eyeColorSidePending = false;
                _eyeColorSide = baseline.side;
                _eyeColorValues = baseline.ValuesFor(baseline.side);
            }
```

`EyeColorSlider` に `int defaultValue` を足し、R を出す（`<summary>` の「リセット (R) は出さない」の文を「R は編集前の値へ戻す」に直す）:

```csharp
        /// <summary>
        /// 整数のスライダー。R は編集前の値へ戻す。
        /// 値はゲームのセーブに入るだけなので、_eyeChanged (プラグインの設定の保存) は立てない
        /// </summary>
        private void EyeColorSlider(GUIView view, string label, int value, int max, int defaultValue, Action<int> set)
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
                defaultValue = defaultValue,
                value = value,
                onChanged = v =>
                {
                    set(Mathf.RoundToInt(v));
                    _eyeColorWritePending = true;
                },
            });
        }
```

`EyePartsColorRow` に控えの 3 値を足し、R（とピッカーのリセット）で控えの整数へ戻す:

```csharp
        /// <summary>パーツカラーの色相・彩度・明度を、瞳のテクスチャの明るさ 128 の位置に出る色 1 つで編集する。R は編集前の値へ戻す</summary>
        private void EyePartsColorRow(GUIView view, string colorKey, EyePickedColor picked, int hue, int chroma, int brightness,
            int defaultHue, int defaultChroma, int defaultBrightness, Action<int, int, int> set)
        {
            var shown = ToColor(picked.Shown(hue, chroma, brightness));
            var reset = ToColor(EyeColorHsl.ToColor(defaultHue, defaultChroma, defaultBrightness));
            view.SetEnabled(_baseEnabled);
            view.DrawColor(view.GetColorFieldCache(colorKey, false), shown, reset, c =>
            {
                if (c == reset)
                {
                    // 色から求め直すと、色で表せない明度 (127 未満・382 超え) が控えに戻らない
                    picked.Forget();
                    set(defaultHue, defaultChroma, defaultBrightness);
                    _eyeColorWritePending = true;
                    return;
                }
                int h = hue, s = chroma, b = brightness;
                var raw = new ColorData(c.r, c.g, c.b, 1f);
                EyeColorHsl.FromColor(raw, ref h, ref s, ref b);
                picked.Remember(raw, h, s, b);
                set(h, s, b);
                _eyeColorWritePending = true;
            }, "色");
        }
```

（`c == reset` は Unity の `Color` の近似比較。ピッカーで控えと同じ色を選んだときも控えの整数になるが、その色を選んだ結果と同じなので問題ない）

- [ ] **Step 3: FlushEyeColor に戻す書き込みを足す**

`FlushEyeColor` を次にする:

```csharp
        private void FlushEyeColor()
        {
            var maid = _eyeColorMaid;
            var restore = _eyeColorRestorePending;
            var side = _eyeColorSidePending;
            var write = _eyeColorWritePending;
            // 書く前に降ろす (例外が出ても毎フレーム同じ書き込みを繰り返さない)
            _eyeColorRestorePending = false;
            _eyeColorSidePending = false;
            _eyeColorWritePending = false;
            if (maid == null || maid.body0 == null || !(restore || side || write))
            {
                return;
            }
            try
            {
                if (restore)
                {
                    MaidEyeColor.Restore(maid, _eyeColorRestore);
                    return;
                }
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
                    MToonMaidPlugin.Log.LogWarning("瞳の色を変えられませんでした: " + e);
                }
            }
        }
```

（ボタンを押したあと同じフレームで R やスライダーが書き込みを立てても、`restore` が先に勝つ。ボタンは行の後に描くので、同じ描画の行の変更はボタンが降ろす）

- [ ] **Step 4: README**

使い方 7 の `範囲外の値（MOD など）は、` の前に次を足す: `各行の [R] はその行を、[瞳の色を編集前に戻す] は左右の目と「左右 / 左 / 右」の選択をまとめて、編集前（そのメイドを [瞳の色] で最初に表示したときの色。セーブをロードすると取り直します）へ戻します。行の [R] では戻らない値（色で表せない明度や範囲外の値など）も、[瞳の色を編集前に戻す] なら元どおりになります。`

- [ ] **Step 5: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 0 エラー / 全件 PASS

- [ ] **Step 6: コミット**

`feat: 瞳の色を編集前に戻せるようにする`

---

### Task 3: 実機で確かめる

`/com3d25-devbridge:restart-verify` の手順で反映する（ゲームの終了はユーザーの確認後）。パーツカラーと `eyePartsTab` の元の値を控え、最後に戻す。窓の private フィールドはリフレクションで扱う（前回と同じ）。

- [ ] **Step 1: 反映して起動・ロード**（ゲーム終了 → `debug.bat com3d25` → DLL のハッシュ一致 → 起動 → スロット 42 をロード → `reset_evaluator`）
- [ ] **Step 2: 表示**: `_tab = 5`・`_eyeSubTab = 2` で窓を撮る。色の行・スライダーの R と「瞳の色を編集前に戻す」が出ること。`_eyeColorBaselines` に今のメイドの控えがあり、元の値と一致すること
- [ ] **Step 3: 行の R**: `MaidEyeColor.Write(Both)` で影の色相・彩度・明度を (170, 255, 255) にする。R の押下（`ColorFieldCache.ResetColor` が `_color` を戻し先の色にし、`DrawColor` がその色で呼び返す。GUIView.cs L3235〜3270）は `DrawColor` の描画の中でしか起きず、eval から `ResetColor()` を呼んでも次の描画の `UpdateColor` が上書きする。そこで、行のコールバックへ戻し先の色そのものを渡して、R と同じ呼び返しを再現する: `ColorPickerWindow`（MToonMaid の DLL の型）を影のキー `"目/瞳の色/影"` で開き、数フレーム後に `Sync` で入った `_onColorChanged`（窓の行のコールバック）を取り出し、`EyeColorHsl.ToColor(控えの影の 3 値)` を `UnityEngine.Color` にして呼ぶ。次のフレームで影の色相・彩度・明度が控えの整数（このメイドなら明度 67）に戻ること。スライダーの R は `defaultValue` を `onChanged` に渡すことを GUIView.cs L3124〜3143 で確かめてあるので、画面で R が出ていることの目視にとどめる
- [ ] **Step 4: 戻すボタン**: 左だけ色相を 50、右を 90、`eyePartsTab = R` にしておく（控えは LR・左右同値）。窓の `_eyeColorRestore` に控え、`_eyeColorRestorePending = true` をリフレクションで書く（ボタンの押下と同じ状態）。次のフレームで左右の 9 値が控えと一致し、`eyePartsTab` が `LR` になること
- [ ] **Step 5: ロード後**: 色相を 128 に変えてから `GameMain.Instance.Deserialize(42)` で同じセーブを読み直し、ログの静止を待つ。窓を表示したあと `_eyeColorBaselines` の控えがロード後の値（元の値）であること。あわせて、窓の `OnChangedSceneLevel` が `Single` で呼ばれたか（`SceneManager.GetActiveScene().name` の変化と、控えが捨てられたか）を記録する。控えが取り直されていなければ、`Deserialize` がシーンを切り替えない経路と判断し、spec の「既知の制約」に「シーンが切り替わらないロードでは編集前を取り直さない」を足して README にも書き、ledger に裁定を残す（コードでの対処は `GameMain.Deserialize` へのフックが要り侵襲が大きいため、この計画ではしない）
- [ ] **Step 6: 後片付けとログ**: 元の値を左右へ書き戻し、`eyePartsTab` を戻す。`tail_log` に MToonMaid 由来の例外が無いこと
- [ ] **Step 7: コミット**: 直した点があれば `fix:` でコミットする。無ければコミットしない

## レビュー却下メモ
- 色が同じで整数が違うと色の行の R が効かない — MTEUtils の `ResetColor` の早期 return で、MTEUtils は編集しない。spec の既知の制約に書き、[瞳の色を編集前に戻す] で戻せる
- スライダーの R が範囲外の控えを丸める — MTEUtils のスライダーの丸め。spec の既知の制約に書き、ボタンで戻せる
- 控えのキーに Maid インスタンスを含めて同じセーブの再ロードを見分ける — ロードで Maid インスタンスが作り直されるかは未確認で、Task 3 Step 5 で sceneLoaded の発火を確かめてから判断する
- Restore で eyePartsTab を先に書く順序 — `MaidEyeColor.Set` は `eyePartsTab` を読まない（`GetPartsColor` / `SetPartsColor` だけ）ため問題ない
