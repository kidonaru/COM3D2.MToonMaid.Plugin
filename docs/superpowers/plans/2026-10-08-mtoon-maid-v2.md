# COM3D25.MToonMaid.Plugin v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** UI を MTEUtils の GUIView へ置き換え、MToon への置き換えを部位グループ・スロット単位で制御できるようにし、Toony のランプと天使の輪を MToon で再現して髪の見た目のずれを無くす。

**Architecture:** 設定に「グループの ON/OFF」「除外スロット」「天使の輪の強さ」を足し（版 2）、Controller の走査で対象外のスロットを元へ戻す。`_ToonRamp` は変換時に GPU で 1 行だけ読み出してマテリアルの控えに持ち、無限色の変化（`PartsColor` の値）で読み直す（ゲームは RenderTexture をプールで使い回すため、テクスチャ単位ではキャッシュしない）。フォークしたシェーダーに `_ToonRampLitColor` と天使の輪（`_HiTex` × `_HiRate` × `pow(N·H, _HiPow)`）を足す。UI は `DockableWindowBase` のウィンドウと `WindowManagerBase` の管理に置き換え、入力の抑止は PostEffects と同じ方式にする。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / Unity 2022.3.62（シェーダービルド用）/ xunit（net48）/ MTEUtils（submodule）

**Spec:** `docs/superpowers/specs/2026-10-08-mtoon-maid-v2-design.md`（前提として `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md`）

## Global Constraints

- v1 の Global Constraints（`docs/superpowers/plans/2026-10-08-mtoon-maid.md`）をすべて引き継ぐ。特に: 対象は COM3D2.5 のみ、`sharedMaterials` だけを読む、変換後の renderQueue は元の値、コメント・ログは日本語、`deploy.bat` は作らない・実行しない、git worktree は使わない、作業は `W:\COM3D2_5\work\COM3D25.MToonMaid.Plugin` の main で行う
- `debug.bat` はゲームフォルダへ DLL をコピーする。各タスクのビルド確認は `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` を直接使う
- MTEUtils は既存の submodule（リポジトリ直下 `MTEUtils/`）を使う。ファイルをコピーしない。csproj から `Link` 付きで個別に Include する。MTEUtils 側のファイルは編集しない
- シェーダーの改名・追加は「ゲームと同名で意味が同じもの（`_HiTex` / `_HiRate` / `_HiPow`）はそのまま、MToon 側だけのもの（`_ToonRampLitColor` / `_HiRateScale`）は新しい名前」。ゲームと同名のプロパティを C# から書く場合は、v1 と同じく控えて OFF で戻す（今回足すものはゲームと同名のものを書かない）
- 設定ファイルの版は 2。版 1 のファイルを読めること
- `build-bundle.bat` は Unity エディタで `UnityProject2022` を開いていると失敗する
- 計画からの逸脱や実装中の判断は、executing-plans の ledger（`.superpowers/sdd/<計画名>/progress.md`）に `Ruling:` 行で記録する
- UI の倍率（`GUIScale`）は 1 のまま。SceneEditor の UI 倍率には追従しない（`UIScaleClient` は取り込まない）

## Review Focus

- **無限色で髪の色を変えたとき**（ランプの RenderTexture はインスタンスが同じまま中身だけ変わる。さらにゲームは RenderTexture をプールで使い回すので、同じインスタンスが別の髪・別のスロットのランプになりうる）: 次の走査で `_ToonRampLitColor` と `_ShadeColor` が新しいランプの値になる。ランプの値はテクスチャ ID ではなくマテリアルの控えごとに持つ → Task 2 の署名テスト、Task 4 の実装、Task 6 Step 5 の実機手順
- **ON のまま、グループを OFF にしたり、スロットを除外したりしたとき**: 次の走査を待たずにそのスロットのマテリアルが元のシェーダーへ戻る。ゲームの shader コマンドの戻し先も補正される → Task 4 の実装と Task 6 Step 4 の実機手順
- **版 1 の設定ファイル**（肌の影色が旧既定 (1, 0.93, 0.93)）: 白へ置き換える。利用者が変えた値は残す → Task 1 のテスト
- **ランプが無い・読めないマテリアル**（`_ToonRamp` 未設定、幅 0、GPU 読み出しの例外）: 白（補正なし）として扱い、例外を外へ出さない。読み出しの失敗は 1 回だけ警告する。ランプの読み直しで例外が出ても、他のメイドの処理と設定の保存を止めない → Task 2 のテストと実装、Task 4 の try/catch
- **GUIView の入力欄キャッシュは描画順の番号で管理される**: スライダーの行の数と順序をフレームごとに変えない（条件で行を増減させず無効表示にする）。数が変わるスロット一覧はスライダーより後に描く → Task 5 の実装と Task 6 Step 6 の実機手順

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ COM3D25.MToonMaid.Plugin.csproj   (Modify: MTEUtils の Link 追加、AnimationModule 参照)
├─ IMToonMaidHost.cs                  (Modify: スロット一覧・対象変更の通知)
├─ MToonMaidPlugin.cs                 (Modify: WindowManager へ置き換え、ランプ読み出しの生成)
├─ MaidMToonController.cs             (Modify: 対象外スロットの復元、無限色の検知、即時走査)
├─ Settings/GroupSettings.cs          (Modify: enabled / highlightScale / 影色の既定を白)
├─ Settings/MaidSettings.cs           (Modify: excludedSlots / IsSlotActive / 版 2 への更新)
├─ Settings/MaidSettingsStore.cs      (Modify: 読み込み時に版を上げる)
├─ Rendering/MToonProps.cs            (Modify: 追加プロパティ名)
├─ Rendering/ToonRampColors.cs        (Create: ランプ 1 行から光側・影側の色、純粋)
├─ Rendering/PartsColorSignature.cs   (Create: 無限色の設定値の署名、純粋)
├─ Rendering/ToonRampReader.cs        (Create: GPU 読み出し。キャッシュしない)
├─ Rendering/MaterialBackup.cs        (Modify: ランプの色を控えに持つ)
├─ Rendering/MaterialConverter.cs     (Modify: ランプと天使の輪の倍率を入れる)
├─ UI/WindowPlacementConfig.cs        (Create: ウィンドウ位置の保存)
├─ UI/WindowManager.cs                (Create: WindowManagerBase、入力の抑止)
└─ UI/MToonMaidWindow.cs              (Rewrite: DockableWindowBase + GUIView)
source/COM3D25.MToonMaid.Plugin.Tests/
├─ MaidSettingsStoreTests.cs          (Modify)
├─ ToonRampColorsTests.cs             (Create)
└─ PartsColorSignatureTests.cs        (Create)
UnityProject2022/Assets/Shaders/MToon10/
├─ vrmc_materials_mtoon.shader        (Modify: Properties)
├─ vrmc_materials_mtoon_input.hlsl    (Modify: 宣言)
├─ vrmc_materials_mtoon_forward_fragment.hlsl (Modify: _ToonRampLitColor)
└─ vrmc_materials_mtoon_lighting_mtoon.hlsl   (Modify: 天使の輪)
```

---

### Task 1: 設定の版 2（グループの ON/OFF・除外スロット・天使の輪の強さ）

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`, `Settings/MaidSettings.cs`, `Settings/MaidSettingsStore.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/MaidSettingsStoreTests.cs`

**Interfaces:**
- Consumes: `ToonMaterialClassifier.GetGroup(string) : PartGroup`、`PartGroup`
- Produces:
  - `GroupSettings.enabled : bool`（既定 true）、`GroupSettings.highlightScale : float`（既定 1）。影色の既定は全グループ白
  - `MaidSettings.CurrentVersion = 2`、`MaidSettings.excludedSlots : List<string>`
  - `MaidSettings.IsSlotActive(string slotCategory) : bool`、`MaidSettings.SetSlotExcluded(string slotCategory, bool excluded)`、`MaidSettings.Upgrade()`（`internal`）

- [ ] **Step 1: 失敗するテストを足す**

`MaidSettingsStoreTests.cs` の末尾（最後の `[Fact]` の後、クラスの閉じ括弧の前）に追加:

```csharp
        [Fact]
        public void グループとスロットの既定は対象()
        {
            var s = new MaidSettings();
            s.FillMissing();

            Assert.True(s.hair.enabled);
            Assert.Equal(1f, s.hair.highlightScale);
            Assert.True(s.IsSlotActive("hairF"));
            Assert.Equal(1f, GroupSettings.CreateDefault(PartGroup.Skin).shadeColor.g);
        }

        [Fact]
        public void グループがOFFならそのスロットは対象外()
        {
            var s = new MaidSettings();
            s.FillMissing();
            s.hair.enabled = false;

            Assert.False(s.IsSlotActive("hairF"));
            Assert.True(s.IsSlotActive("body"));
        }

        [Fact]
        public void 除外したスロットだけ対象外()
        {
            var s = new MaidSettings();
            s.FillMissing();
            s.SetSlotExcluded("accHat", true);
            s.SetSlotExcluded("accHat", true);

            Assert.False(s.IsSlotActive("accHat"));
            Assert.True(s.IsSlotActive("wear"));
            Assert.Single(s.excludedSlots);

            s.SetSlotExcluded("accHat", false);
            Assert.True(s.IsSlotActive("accHat"));
        }

        [Fact]
        public void 除外スロットとグループのONOFFを保存して読み直せる()
        {
            var path = Path.Combine(_dir, "e.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.costume.enabled = false;
            s.hair.highlightScale = 0.5f;
            s.SetSlotExcluded("accHat", true);

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.False(loaded.costume.enabled);
            Assert.Equal(0.5f, loaded.hair.highlightScale);
            Assert.Equal(new[] { "accHat" }, loaded.excludedSlots);
            Assert.Equal(MaidSettings.CurrentVersion, loaded.version);
        }

        [Fact]
        public void 版1の肌の旧既定の影色は白へ置き換える()
        {
            var path = Path.Combine(_dir, "v1a.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"1\"><enabled>true</enabled>" +
                "<skin><shadeColor r=\"1\" g=\"0.93\" b=\"0.93\" a=\"1\" /></skin></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(1f, s.skin.shadeColor.g);
            Assert.Equal(1f, s.skin.shadeColor.b);
            Assert.True(s.skin.enabled);
            Assert.Empty(s.excludedSlots);
            Assert.Equal(MaidSettings.CurrentVersion, s.version);
        }

        [Fact]
        public void 版1でも利用者が変えた肌の影色は残す()
        {
            var path = Path.Combine(_dir, "v1b.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"1\">" +
                "<skin><shadeColor r=\"0.8\" g=\"0.5\" b=\"0.5\" a=\"1\" /></skin></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(0.5f, s.skin.shadeColor.g);
        }
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`enabled` / `highlightScale` / `IsSlotActive` / `SetSlotExcluded` / `excludedSlots` が無い）

- [ ] **Step 3: GroupSettings を変える**

`Settings/GroupSettings.cs` のクラス先頭（`// 陰影` の直前）に追加:

```csharp
        // false ならこのグループのスロットは MToon にしない (メイドの enabled が親スイッチ)
        public bool enabled = true;

```

`public float giEqualization = 0.9f;` の直後に追加:

```csharp

        // 天使の輪 (Toony の髪のハイライト) の強さ。_HiTex を持たないマテリアルには効かない
        public float highlightScale = 1f;
```

`FillMissing` の `shadeColor` の分岐を次へ置き換える（ランプ由来の色味が影に入るため、全グループ白にする）:

```csharp
            if (shadeColor == null)
            {
                // 影の色味は Toony のランプから自動で入るため、ここは掛ける倍率として白を既定にする
                shadeColor = new ColorData(1f, 1f, 1f, 1f);
            }
```

`FillMissing(PartGroup group)` の引数 `group` は使われなくなるが、呼び出し側を変えないため残す。

- [ ] **Step 4: MaidSettings を変える**

`Settings/MaidSettings.cs` の `using System;` の下に `using System.Collections.Generic;` を足し、クラスを次のように変える。

`public const int CurrentVersion = 1;` を `2` にし、その下に版 1 の肌の既定値を足す:

```csharp
        public const int CurrentVersion = 2;

        // 版 1 の肌の影色の既定。版 2 ではランプから色味が入るため白へ置き換える
        private const float LegacySkinShadeR = 1f;
        private const float LegacySkinShadeGB = 0.93f;
```

`public GroupSettings costume;` の直後に追加:

```csharp
        // MToon にしないスロット (TBodySkin.Category)。グループが ON でもここにあれば元のまま
        public List<string> excludedSlots = new List<string>();
```

`FillMissing()` の末尾（foreach の後）に追加:

```csharp
            if (excludedSlots == null)
            {
                excludedSlots = new List<string>();
            }
```

クラスの末尾に追加:

```csharp
        public bool IsSlotActive(string slotCategory)
        {
            return Get(ToonMaterialClassifier.GetGroup(slotCategory)).enabled
                && (slotCategory == null || !excludedSlots.Contains(slotCategory));
        }

        public void SetSlotExcluded(string slotCategory, bool excluded)
        {
            if (excluded)
            {
                if (!excludedSlots.Contains(slotCategory))
                {
                    excludedSlots.Add(slotCategory);
                }
            }
            else
            {
                excludedSlots.Remove(slotCategory);
            }
        }

        /// <summary>読み込んだ古い版の値を今の版へ合わせる。FillMissing より前に呼ぶ</summary>
        internal void Upgrade()
        {
            if (version < 2 && skin != null && IsLegacySkinShade(skin.shadeColor))
            {
                // null にして FillMissing の既定 (白) で埋める
                skin.shadeColor = null;
            }
            version = CurrentVersion;
        }

        private static bool IsLegacySkinShade(ColorData c)
        {
            const float epsilon = 0.0001f;
            return c != null
                && Math.Abs(c.r - LegacySkinShadeR) < epsilon
                && Math.Abs(c.g - LegacySkinShadeGB) < epsilon
                && Math.Abs(c.b - LegacySkinShadeGB) < epsilon;
        }
```

- [ ] **Step 5: 読み込み時に版を上げる**

`Settings/MaidSettingsStore.cs` の `Load` の末尾で、

```csharp
            if (settings == null)
            {
                settings = new MaidSettings();
            }
            settings.FillMissing();
            return settings;
```

を次へ置き換える:

```csharp
            if (settings == null)
            {
                settings = new MaidSettings();
            }
            settings.Upgrade();
            settings.FillMissing();
            return settings;
```

- [ ] **Step 6: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（既存 42 件 + 新規 6 件 = 48 件）

- [ ] **Step 7: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/Settings source/COM3D25.MToonMaid.Plugin.Tests/MaidSettingsStoreTests.cs
git commit -m "feat: グループの ON/OFF・除外スロット・天使の輪の強さを設定に追加"
```

---

### Task 2: ランプの色の算出・読み出しと、無限色の署名

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/ToonRampColors.cs`, `Rendering/PartsColorSignature.cs`, `Rendering/ToonRampReader.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/ToonRampColorsTests.cs`, `source/COM3D25.MToonMaid.Plugin.Tests/PartsColorSignatureTests.cs`

**Interfaces:**
- Consumes: `ColorData`
- Produces:
  - `sealed class ToonRampColors { ColorData Lit { get; } ColorData Dark { get; } static ToonRampColors White { get; } static ToonRampColors FromRow(float[] rgba, int width) }`
  - `static class PartsColorSignature { const int Seed = 17; static int Mix(int hash, bool use, int mainHue, int mainChroma, int mainBrightness, int mainContrast, int shadowRate, int shadowHue, int shadowChroma, int shadowBrightness, int shadowContrast) }`
  - `sealed class ToonRampReader(Action<string> warn)`、`ToonRampColors Read(Texture ramp)`（キャッシュしない。読めなければ `ToonRampColors.White`）

ランプの値をテクスチャ ID でキャッシュしないのは、ゲームの `RenderTextureCache`（`InfinityColorTextureCache` が使う static のプール）が解放済みの RenderTexture を同サイズの別の要求へ使い回し、同じインスタンスが別の髪・別のスロットのランプになるため。値は Task 4 でマテリアルの控え（`MaterialBackup`）に持つ。

- [ ] **Step 1: 失敗するテストを書く**

`ToonRampColorsTests.cs`:

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class ToonRampColorsTests
    {
        [Fact]
        public void 左端が影側で右端が光側()
        {
            var row = new[]
            {
                0.40f, 0.41f, 0.42f, 1f,
                0.50f, 0.50f, 0.50f, 1f,
                0.58f, 0.59f, 0.60f, 1f,
            };

            var c = ToonRampColors.FromRow(row, 3);

            Assert.Equal(0.40f, c.Dark.r);
            Assert.Equal(0.42f, c.Dark.b);
            Assert.Equal(0.58f, c.Lit.r);
            Assert.Equal(0.60f, c.Lit.b);
            Assert.Equal(1f, c.Lit.a);
        }

        [Fact]
        public void 幅が足りなければ白()
        {
            var c = ToonRampColors.FromRow(new[] { 0.5f, 0.5f, 0.5f }, 1);

            Assert.Same(ToonRampColors.White, c);
        }

        [Fact]
        public void 空なら白()
        {
            Assert.Same(ToonRampColors.White, ToonRampColors.FromRow(new float[0], 0));
            Assert.Same(ToonRampColors.White, ToonRampColors.FromRow(null, 4));
        }

        [Fact]
        public void 白は光側も影側も1()
        {
            Assert.Equal(1f, ToonRampColors.White.Lit.g);
            Assert.Equal(1f, ToonRampColors.White.Dark.g);
        }
    }
}
```

`PartsColorSignatureTests.cs`:

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class PartsColorSignatureTests
    {
        private static int Of(bool use, int mainHue, int shadowContrast)
        {
            return PartsColorSignature.Mix(PartsColorSignature.Seed, use, mainHue, 100, 200, 50, 3, 10, 20, 30, shadowContrast);
        }

        [Fact]
        public void 同じ値なら同じ署名()
        {
            Assert.Equal(Of(true, 10, 40), Of(true, 10, 40));
        }

        [Fact]
        public void 色相が変われば署名が変わる()
        {
            Assert.NotEqual(Of(true, 10, 40), Of(true, 11, 40));
        }

        [Fact]
        public void 使用フラグと最後のフィールドも署名に入る()
        {
            Assert.NotEqual(Of(true, 10, 40), Of(false, 10, 40));
            Assert.NotEqual(Of(true, 10, 40), Of(true, 10, 41));
        }

        [Fact]
        public void 順に混ぜると前の値も効く()
        {
            var a = PartsColorSignature.Mix(Of(true, 1, 0), false, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var b = PartsColorSignature.Mix(Of(true, 2, 0), false, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.NotEqual(a, b);
        }
    }
}
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`ToonRampColors` / `PartsColorSignature` が無い）

- [ ] **Step 3: 純粋な算出を実装する**

`Rendering/ToonRampColors.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// Toony の _ToonRamp から取った光側 (右端) と影側 (左端) の色。
    /// Toony は陰影をこのランプの色で掛けるため、MToon では光側をアルベドに、影側を影色に掛けて明るさを揃える
    /// </summary>
    public sealed class ToonRampColors
    {
        public static ToonRampColors White { get; } = new ToonRampColors(new ColorData(1f, 1f, 1f, 1f), new ColorData(1f, 1f, 1f, 1f));

        public ColorData Lit { get; }
        public ColorData Dark { get; }

        private ToonRampColors(ColorData lit, ColorData dark)
        {
            Lit = lit;
            Dark = dark;
        }

        /// <summary>RGBA を並べたランプ 1 行分から作る。読めない形なら White</summary>
        public static ToonRampColors FromRow(float[] rgba, int width)
        {
            if (rgba == null || width < 1 || rgba.Length < width * 4)
            {
                return White;
            }
            var last = (width - 1) * 4;
            return new ToonRampColors(
                new ColorData(rgba[last], rgba[last + 1], rgba[last + 2], 1f),
                new ColorData(rgba[0], rgba[1], rgba[2], 1f));
        }
    }
}
```

`Rendering/PartsColorSignature.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// メイドの無限色の設定値 (MaidParts.PartsColor の全フィールド) の署名。
    /// 無限色のランプは同じ RenderTexture へ描き直され、変化を知らせる値も増えないため、設定値の変化で読み直しを判断する。
    /// 構造体の既定の GetHashCode は実装次第で一部のフィールドしか見ないため、明示的に混ぜる
    /// </summary>
    public static class PartsColorSignature
    {
        public const int Seed = 17;

        public static int Mix(int hash, bool use, int mainHue, int mainChroma, int mainBrightness, int mainContrast,
            int shadowRate, int shadowHue, int shadowChroma, int shadowBrightness, int shadowContrast)
        {
            unchecked
            {
                hash = hash * 31 + (use ? 1 : 0);
                hash = hash * 31 + mainHue;
                hash = hash * 31 + mainChroma;
                hash = hash * 31 + mainBrightness;
                hash = hash * 31 + mainContrast;
                hash = hash * 31 + shadowRate;
                hash = hash * 31 + shadowHue;
                hash = hash * 31 + shadowChroma;
                hash = hash * 31 + shadowBrightness;
                hash = hash * 31 + shadowContrast;
                return hash;
            }
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（48 + 4 + 4 = 56 件）

- [ ] **Step 5: GPU からの読み出しを実装する**

`Rendering/ToonRampReader.cs`:

```csharp
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// _ToonRamp の中央の行を GPU から読み出す。ランプは読み取り不可のことが多いため Blit してから読む。
    /// ゲームは RenderTexture をプールで使い回すため、テクスチャ単位ではキャッシュしない (呼び出し側がマテリアルごとに持つ)
    /// </summary>
    public sealed class ToonRampReader
    {
        private readonly Action<string> _warn;
        private bool _warned;

        public ToonRampReader(Action<string> warn)
        {
            _warn = warn;
        }

        public ToonRampColors Read(Texture ramp)
        {
            if (ramp == null || ramp.width < 1 || ramp.height < 1)
            {
                return ToonRampColors.White;
            }
            var width = ramp.width;
            var row = ramp.height / 2;
            // ゲームは Gamma 色空間。シェーダーが見る値そのものを読むため Linear 指定で変換させない
            var rt = RenderTexture.GetTemporary(width, ramp.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            Texture2D buffer = null;
            try
            {
                Graphics.Blit(ramp, rt);
                RenderTexture.active = rt;
                buffer = new Texture2D(width, 1, TextureFormat.RGBA32, false, true);
                buffer.ReadPixels(new Rect(0, row, width, 1), 0, 0);
                buffer.Apply(false);
                var pixels = buffer.GetPixels();
                var rgba = new float[pixels.Length * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    rgba[i * 4] = pixels[i].r;
                    rgba[i * 4 + 1] = pixels[i].g;
                    rgba[i * 4 + 2] = pixels[i].b;
                    rgba[i * 4 + 3] = pixels[i].a;
                }
                return ToonRampColors.FromRow(rgba, width);
            }
            catch (Exception e)
            {
                // 読めないランプは補正なし (白) で描く。毎回の警告は避ける
                if (!_warned)
                {
                    _warned = true;
                    _warn("ランプを読み出せないため補正せずに描きます: " + ramp.name + " " + e.Message);
                }
                return ToonRampColors.White;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (buffer != null)
                {
                    Object.Destroy(buffer);
                }
            }
        }
    }
}
```

- [ ] **Step 6: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（56 件）

- [ ] **Step 7: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/Rendering/ToonRampColors.cs source/COM3D25.MToonMaid.Plugin/Rendering/PartsColorSignature.cs source/COM3D25.MToonMaid.Plugin/Rendering/ToonRampReader.cs source/COM3D25.MToonMaid.Plugin.Tests/ToonRampColorsTests.cs source/COM3D25.MToonMaid.Plugin.Tests/PartsColorSignatureTests.cs
git commit -m "feat: Toony のランプから光側と影側の色を読み出す"
```

---

### Task 3: シェーダーにランプの光側の色と天使の輪を足す

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`, `vrmc_materials_mtoon_input.hlsl`, `vrmc_materials_mtoon_forward_fragment.hlsl`, `vrmc_materials_mtoon_lighting_mtoon.hlsl`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`（`build-bundle.bat` の生成物）

**Interfaces:**
- Produces: シェーダープロパティ `_ToonRamp`（2D、既定 white、HLSL では使わない）、`_ToonRampLitColor`（Color、既定白）、`_HiTex`（2D、既定 black）、`_HiRate`（Float、既定 0）、`_HiPow`（Float、既定 0.001）、`_HiRateScale`（Float、既定 1）。`MToonProps.ToonRamp = "_ToonRamp"`、`MToonProps.ToonRampLitColor = "_ToonRampLitColor"`、`MToonProps.HiRateScale = "_HiRateScale"`

- [ ] **Step 1: Properties を足す**

`vrmc_materials_mtoon.shader` の先頭の改変注記に 2 行足す:

```
// - Toony の _ToonRamp の光側の色を掛ける _ToonRampLitColor を追加
// - Toony の髪のハイライト (天使の輪: _HiTex / _HiRate / _HiPow、倍率 _HiRateScale) を追加
```

`_ShadowTex ("mtoon.shadeMultiplyTexture", 2D) = "white" {}` の行の直後に追加:

```
        // COM3D25: ゲームの Toony のランプ。シェーダーでは使わないが、宣言して MToon 化後も確実に読めるようにする
        _ToonRamp ("Toon Ramp (unused)", 2D) = "white" {}
        // COM3D25: Toony の _ToonRamp の光側の色 (プラグインが設定する)
        _ToonRampLitColor ("ToonRamp Lit Color", Color) = (1, 1, 1, 1)
        // COM3D25: Toony の髪のハイライト。_HiTex / _HiRate / _HiPow はゲームと同名で、シェーダーを差し替えても値が引き継がれる
        _HiTex ("Highlight Texture", 2D) = "black" {}
        _HiRate ("Highlight Rate", Float) = 0
        _HiPow ("Highlight Power", Float) = 0.001
        _HiRateScale ("Highlight Rate Scale", Float) = 1
```

- [ ] **Step 2: 宣言を足す**

`vrmc_materials_mtoon_input.hlsl` の `MTOON_DECLARE_TEX2D(_ShadowTex);` の直後に `MTOON_DECLARE_TEX2D(_HiTex);` を足す。`half4 _OutlineColor;` の直後に `half4 _ToonRampLitColor;` を、`half _OutlineLightingMix;` の直後に次を足す:

```
half _HiRate;
half _HiPow;
half _HiRateScale;
```

- [ ] **Step 3: アルベドにランプの光側の色を掛ける**

`vrmc_materials_mtoon_forward_fragment.hlsl` の `mtoonInput.litColor = litColor.rgb;` を次へ置き換える:

```
    // COM3D25: Toony のランプの光側の明るさに揃える (アルファには掛けない)
    mtoonInput.litColor = litColor.rgb * _ToonRampLitColor.rgb;
```

- [ ] **Step 4: 天使の輪を直接光の項へ足す**

`vrmc_materials_mtoon_lighting_mtoon.hlsl` の `inline half3 GetMToonLighting_DirectLighting(` の直前に追加:

```
// COM3D25: Toony の髪のハイライト (天使の輪)。実測した Toony の式:
//   _HiTex(uv) * _HiRate * pow(saturate(N.H), _HiPow) をアルベドへ加算してから陰影を掛ける (影側にも掛かる)
// Toony のアウトラインは単色なので、アウトラインのパスでは足さない
inline half3 GetMToonLighting_Highlight(const UnityLighting unityLight, const MToonInput input)
{
    const half rate = _HiRate * _HiRateScale;
    if (rate <= 0 || MToon_IsOutlinePass())
    {
        return 0;
    }
    // 逆光ではライトと視線の和がほぼ 0 になり、half の normalize が NaN になるため長さの下限を設ける
    const float3 halfSum = (float3)unityLight.directLightDirection + (float3)input.viewDirWS;
    const float3 halfDir = halfSum * rsqrt(max(dot(halfSum, halfSum), 1e-4));
    const half dotNH = saturate(dot(input.normalWS, halfDir));
    return MTOON_SAMPLE_TEXTURE2D(_HiTex, input.uv).rgb * rate * pow(dotNH, max(_HiPow, 0.001));
}

```

`GetMToonLighting_DirectLighting` の本体の 2 行

```
    const half3 shadeColor = MTOON_SAMPLE_TEXTURE2D(_ShadowTex, input.uv).rgb * _ShadeColor.rgb;
    const half3 albedo = lerp(shadeColor, input.litColor, shade);
```

を次へ置き換える:

```
    const half3 highlight = GetMToonLighting_Highlight(unityLight, input);
    const half3 shadeColor = (MTOON_SAMPLE_TEXTURE2D(_ShadowTex, input.uv).rgb + highlight) * _ShadeColor.rgb;
    const half3 litColor = input.litColor + highlight * _ToonRampLitColor.rgb;
    const half3 albedo = lerp(shadeColor, litColor, shade);
```

- [ ] **Step 5: プロパティ名の定数を足す**

`Rendering/MToonProps.cs` の `// MToon 固有` の直前に追加:

```csharp
        // ゲームの Toony のランプ。MToon にした後も GetTexture で読める (保存プロパティとして残る)
        public const string ToonRamp = "_ToonRamp";

```

`public const string UvAnimMaskTex = "_UvAnimMaskTex";` の直後に追加:

```csharp
        // COM3D25 のフォークで足したもの (ゲームと同名ではないので控えない)
        public const string ToonRampLitColor = "_ToonRampLitColor";
        public const string HiRateScale = "_HiRateScale";
```

- [ ] **Step 6: バンドルを作り直す**

Run: `cmd //c "W:\\COM3D2_5\\work\\COM3D25.MToonMaid.Plugin\\build-bundle.bat"`
Expected: `コピーしました: ...\Resources\mtoon_bundle`。`UnityProject2022/Logs/build-bundle.log` に `Shader error` が無い（`grep -c "Shader error" UnityProject2022/Logs/build-bundle.log` → `0`）

- [ ] **Step 7: ビルドを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: ビルド成功

- [ ] **Step 8: Commit**

```bash
git add UnityProject2022/Assets/Shaders/MToon10 source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle
git commit -m "feat: シェーダーにランプの光側の色と天使の輪を追加"
```

---

### Task 4: 変換にランプと天使の輪を反映し、対象をグループ・スロット単位で制御する

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialBackup.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/IMToonMaidHost.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`

**Interfaces:**
- Consumes: `ToonRampReader` / `ToonRampColors` / `PartsColorSignature`（Task 2）、`MToonProps.ToonRamp` / `ToonRampLitColor` / `HiRateScale`（Task 3）、`MaidSettings.IsSlotActive`、`MaidSettings.excludedSlots`、`GroupSettings.highlightScale`（Task 1）
- Produces:
  - `MaterialBackup.ramp : ToonRampColors`（既定 `ToonRampColors.White`）
  - `MaterialConverter(Shader mtoonShader, ToonRampReader rampReader)`、`void RefreshRamp(Material m, MaterialBackup backup)`
  - `MaidMToonController.RequestScan()`
  - `IMToonMaidHost.GetSlotCategories(Maid maid, PartGroup group) : List<string>`（着けているスロットと、除外中で今は着けていないスロットの和。名前順）、`IMToonMaidHost.NotifyTargetsChanged(Maid maid)`

Material と Maid を扱うため単体テストはできない。Task 6 の実機手順で確かめる。

- [ ] **Step 1: 控えにランプの色を持たせる**

`Rendering/MaterialBackup.cs` の `public Texture copiedShadowTex;` の直後に追加:

```csharp
        // 変換・再変換・無限色の変更のときに読んだ _ToonRamp の色。
        // ゲームは RenderTexture をプールで使い回すため、テクスチャ単位ではなくマテリアルごとに持つ
        public ToonRampColors ramp = ToonRampColors.White;
```

- [ ] **Step 2: Converter にランプと天使の輪の倍率を入れる**

`Rendering/MaterialConverter.cs`:

フィールドとコンストラクタを次へ置き換える:

```csharp
        private readonly ToonRampReader _rampReader;

        public Shader MToonShader { get; private set; }

        public MaterialConverter(Shader mtoonShader, ToonRampReader rampReader)
        {
            MToonShader = mtoonShader;
            _rampReader = rampReader;
        }

        /// <summary>無限色の変更などでランプの中身が変わったとき、読み直して控えへ入れる (ApplyGroup で反映される)</summary>
        public void RefreshRamp(Material m, MaterialBackup backup)
        {
            backup.ramp = _rampReader.Read(m.GetTexture(MToonProps.ToonRamp));
        }
```

`Convert` の `var hasShadowTex = ...` の行の直前に追加（シェーダーを差し替える前の Toony の状態で読む）:

```csharp
            backup.ramp = _rampReader.Read(m.GetTexture(MToonProps.ToonRamp));
```

`Reconvert` の `SwitchShader(m, backup, settings);` の直前に追加:

```csharp
            backup.ramp = _rampReader.Read(m.GetTexture(MToonProps.ToonRamp));
```

`ApplyGroup` の先頭の 1 行 `m.SetColor(MToonProps.ShadeColor, ToColor(settings.shadeColor));` を次へ置き換える（ApplyGroup は OnGUI からも呼ばれるため、ここでは GPU から読まず控えの値を使う）:

```csharp
            // Toony はランプの色で陰影を掛けるため、光側をアルベドに、影側を影色に掛けて明るさを揃える
            m.SetColor(MToonProps.ToonRampLitColor, ToColor(backup.ramp.Lit));
            m.SetColor(MToonProps.ShadeColor, ToColor(settings.shadeColor) * ToColor(backup.ramp.Dark));
            m.SetFloat(MToonProps.HiRateScale, settings.highlightScale);
```

- [ ] **Step 3: Controller で対象外のスロットを戻し、無限色の変化でランプを読み直す**

`MaidMToonController.cs`:

フィールド `private int _framesSinceScan;` の直後に追加:

```csharp
        private bool _scanRequested;
        // 無限色の設定値の署名。変わったらランプを読み直す
        private int? _partsColorSignature;
        // ランプの読み直しの失敗は毎走査で繰り返しうるため 1 回だけ知らせる
        private bool _rampRefreshWarned;
```

`Tick` を次へ置き換える:

```csharp
        public void Tick(MaidSettings settings)
        {
            if (Maid.IsBusy || !Maid.Visible || Maid.body0 == null)
            {
                _wasBusy = true;
                return;
            }
            if (!_wasBusy && !_scanRequested && ++_framesSinceScan < ScanInterval)
            {
                return;
            }
            _wasBusy = false;
            _scanRequested = false;
            _framesSinceScan = 0;
            Scan(settings);
        }

        /// <summary>対象のグループ・スロットが変わったとき、次の Tick で間隔を待たずに走査させる</summary>
        public void RequestScan()
        {
            _scanRequested = true;
        }
```

`RestoreAll` を次へ置き換える（1 件分の戻しを切り出す）:

```csharp
        public void RestoreAll()
        {
            foreach (var e in _entries.Values)
            {
                RestoreEntry(e);
            }
            _entries.Clear();
            _failed.Clear();
            _unknownReported.Clear();
        }

        private void RestoreEntry(Entry e)
        {
            if (e.material != null)
            {
                _converter.Restore(e.material, e.backup);
            }
            GameShaderBackup.Replace(e.skin, e.materialIndex, _converter.MToonShader, e.backup.shader);
        }
```

`Scan` の先頭 `RemoveDestroyed();` の直後に追加:

```csharp
            RefreshRampsIfPartsColorChanged(settings);
```

`Scan` の中の `var group = ToonMaterialClassifier.GetGroup(skin.Category);` の直後に追加:

```csharp
                    var active = settings.IsSlotActive(skin.Category);
```

同じく `Scan` の中の

```csharp
                            if (_materialBuffer[k] != null)
                            {
                                ScanMaterial(_materialBuffer[k], skin, k, group, settings.Get(group));
                            }
```

を次へ置き換える:

```csharp
                            if (_materialBuffer[k] == null)
                            {
                                continue;
                            }
                            if (active)
                            {
                                ScanMaterial(_materialBuffer[k], skin, k, group, settings.Get(group));
                            }
                            else
                            {
                                RestoreIfConverted(_materialBuffer[k]);
                            }
```

クラスの末尾（`RemoveDestroyed` の後）に追加:

```csharp
        /// <summary>グループの OFF やスロットの除外で対象外になったマテリアルを元へ戻す</summary>
        private void RestoreIfConverted(Material m)
        {
            var id = m.GetInstanceID();
            Entry entry;
            if (_entries.TryGetValue(id, out entry))
            {
                RestoreEntry(entry);
                _entries.Remove(id);
            }
        }

        /// <summary>
        /// 無限色のランプは同じ RenderTexture へ描き直され、変化を知らせる値も増えない。
        /// メイドの無限色の設定値が変わったらランプを読み直し、変換済みのマテリアルへ入れ直す
        /// </summary>
        private void RefreshRampsIfPartsColorChanged(MaidSettings settings)
        {
            try
            {
                var signature = ComputePartsColorSignature();
                var changed = _partsColorSignature.HasValue && _partsColorSignature.Value != signature;
                _partsColorSignature = signature;
                if (!changed)
                {
                    return;
                }
                foreach (var e in _entries.Values)
                {
                    if (e.material != null && e.material.shader == _converter.MToonShader)
                    {
                        _converter.RefreshRamp(e.material, e.backup);
                        _converter.ApplyGroup(e.material, e.backup, settings.Get(e.backup.group));
                    }
                }
            }
            catch (Exception e)
            {
                // 走査と設定の保存を止めないよう、ここで止めて知らせる
                if (!_rampRefreshWarned)
                {
                    _rampRefreshWarned = true;
                    _warn("無限色の変更に合わせたランプの読み直しに失敗しました: " + e.Message);
                }
            }
        }

        private int ComputePartsColorSignature()
        {
            var parts = Maid.Parts;
            var hash = PartsColorSignature.Seed;
            for (var i = 0; i < (int)MaidParts.PARTS_COLOR.MAX; i++)
            {
                var c = parts.GetPartsColor((MaidParts.PARTS_COLOR)i);
                hash = PartsColorSignature.Mix(hash, c.m_bUse, c.m_nMainHue, c.m_nMainChroma, c.m_nMainBrightness, c.m_nMainContrast,
                    c.m_nShadowRate, c.m_nShadowHue, c.m_nShadowChroma, c.m_nShadowBrightness, c.m_nShadowContrast);
            }
            return hash;
        }
```

- [ ] **Step 4: ホストのインターフェースを足す**

`IMToonMaidHost.cs` のインターフェースの末尾に追加:

```csharp
        /// <summary>
        /// 指定グループのスロット (名前順)。今着けているものに加え、除外中で今は着けていないものも含める
        /// (外した後も除外を解除できるように)
        /// </summary>
        List<string> GetSlotCategories(Maid maid, PartGroup group);

        /// <summary>グループの ON/OFF やスロットの除外を変えた。保存し、間隔を待たずに走査させる</summary>
        void NotifyTargetsChanged(Maid maid);
```

- [ ] **Step 5: プラグイン本体を合わせる**

`MToonMaidPlugin.cs`:

フィールド `private readonly List<Maid> _activeMaids = new List<Maid>();` の直後に追加:

```csharp
        private readonly List<string> _slotCategories = new List<string>();
```

`LoadShader` の `_converter = new MaterialConverter(shader);` を次へ置き換える:

```csharp
                _converter = new MaterialConverter(shader, new ToonRampReader(Log.LogWarning));
```

`GetConvertedCount` の後に追加:

```csharp
        public List<string> GetSlotCategories(Maid maid, PartGroup group)
        {
            _slotCategories.Clear();
            var slots = maid.body0.goSlot;
            for (var i = 0; i < slots.Count; i++)
            {
                for (var j = 0; j < slots.CountChildren(i); j++)
                {
                    var skin = slots[i, j];
                    if (skin != null && skin.obj != null)
                    {
                        AddSlotCategory(skin.Category, group);
                    }
                }
            }
            foreach (var excluded in GetSettings(maid).excludedSlots)
            {
                AddSlotCategory(excluded, group);
            }
            _slotCategories.Sort(System.StringComparer.Ordinal);
            return _slotCategories;
        }

        private void AddSlotCategory(string category, PartGroup group)
        {
            if (!string.IsNullOrEmpty(category)
                && ToonMaterialClassifier.GetGroup(category) == group
                && !_slotCategories.Contains(category))
            {
                _slotCategories.Add(category);
            }
        }

        public void NotifyTargetsChanged(Maid maid)
        {
            _store.MarkDirty(maid.status.guid, Time.realtimeSinceStartup);
            MaidMToonController controller;
            if (_controllers.TryGetValue(maid, out controller))
            {
                controller.RequestScan();
            }
        }
```

- [ ] **Step 6: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）。テスト PASS（56 件）

- [ ] **Step 7: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/Rendering/MaterialBackup.cs source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs source/COM3D25.MToonMaid.Plugin/IMToonMaidHost.cs source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs
git commit -m "feat: ランプと天使の輪を変換に反映し、グループ・スロット単位で対象を制御する"
```

---

### Task 5: UI を MTEUtils の GUIView へ置き換える

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj`
- Create: `source/COM3D25.MToonMaid.Plugin/UI/WindowPlacementConfig.cs`, `UI/WindowManager.cs`
- Rewrite: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`

**Interfaces:**
- Consumes: `IMToonMaidHost`（Task 4 の追加分を含む）、`GroupSettings` / `MaidSettings`（Task 1）、MTEUtils の `DockableWindowBase` / `WindowManagerBase` / `GUIView` / `GUIComboBox<T>` / `ComboBoxPopupWindow` / `ColorPickerWindow` / `DialogPopupWindow` / `MTEUtils.IsMouseOverWindowRect`（名前空間 `COM3D2.MotionTimelineEditor`）
- Produces: `WindowPlacementConfig(ConfigFile config, int defaultWidth, int defaultHeight)`、`WindowManager(IMToonMaidHost host, WindowPlacementConfig placement)`、`WindowManager.MainWindow : MToonMaidWindow`、`MToonMaidWindow.DefaultWidth` / `DefaultHeight`

UI のため単体テストは無い。ビルドと Task 6 の実機手順で確かめる。

- [ ] **Step 1: csproj に MTEUtils と AnimationModule を足す**

`COM3D25.MToonMaid.Plugin.csproj` の `<Compile Include="..\..\MTEUtils\GearMenu.cs" Link="MTEUtils\GearMenu.cs" />` の行を、次の ItemGroup の中身へ置き換える（コメントも含める）:

```xml
    <!-- MTEUtils (submodule) から、ギアメニューと GUIView のウィンドウ一式だけをリンクする。
         MTEUtils は利用側が明示列挙する前提のため glob にしない。依存が増えたら CS0246 の型のファイルを足す -->
    <Compile Include="..\..\MTEUtils\GearMenu.cs" Link="MTEUtils\GearMenu.cs" />
    <Compile Include="..\..\MTEUtils\ColorExtensions.cs" Link="MTEUtils\ColorExtensions.cs" />
    <Compile Include="..\..\MTEUtils\ColorPickerWindow.cs" Link="MTEUtils\ColorPickerWindow.cs" />
    <Compile Include="..\..\MTEUtils\ComboBoxPopupWindow.cs" Link="MTEUtils\ComboBoxPopupWindow.cs" />
    <Compile Include="..\..\MTEUtils\CurveData.cs" Link="MTEUtils\CurveData.cs" />
    <Compile Include="..\..\MTEUtils\CurveEditorWindow.cs" Link="MTEUtils\CurveEditorWindow.cs" />
    <Compile Include="..\..\MTEUtils\DialogPopupWindow.cs" Link="MTEUtils\DialogPopupWindow.cs" />
    <Compile Include="..\..\MTEUtils\DockableWindowBase.cs" Link="MTEUtils\DockableWindowBase.cs" />
    <Compile Include="..\..\MTEUtils\DockingClient.cs" Link="MTEUtils\DockingClient.cs" />
    <Compile Include="..\..\MTEUtils\Extensions.cs" Link="MTEUtils\Extensions.cs" />
    <Compile Include="..\..\MTEUtils\GUICache.cs" Link="MTEUtils\GUICache.cs" />
    <Compile Include="..\..\MTEUtils\GUIComboBox.cs" Link="MTEUtils\GUIComboBox.cs" />
    <Compile Include="..\..\MTEUtils\GUIScale.cs" Link="MTEUtils\GUIScale.cs" />
    <Compile Include="..\..\MTEUtils\GUIView.cs" Link="MTEUtils\GUIView.cs" />
    <Compile Include="..\..\MTEUtils\IGUIWindow.cs" Link="MTEUtils\IGUIWindow.cs" />
    <Compile Include="..\..\MTEUtils\IScreenScalableWindow.cs" Link="MTEUtils\IScreenScalableWindow.cs" />
    <Compile Include="..\..\MTEUtils\ITabVisibleWindow.cs" Link="MTEUtils\ITabVisibleWindow.cs" />
    <Compile Include="..\..\MTEUtils\InputRemapperClient.cs" Link="MTEUtils\InputRemapperClient.cs" />
    <Compile Include="..\..\MTEUtils\MTEUtils.cs" Link="MTEUtils\MTEUtils.cs" />
    <Compile Include="..\..\MTEUtils\MainThreadDispatcher.cs" Link="MTEUtils\MainThreadDispatcher.cs" />
    <Compile Include="..\..\MTEUtils\ManagerRegistry.cs" Link="MTEUtils\ManagerRegistry.cs" />
    <Compile Include="..\..\MTEUtils\ResizeCursor.cs" Link="MTEUtils\ResizeCursor.cs" />
    <Compile Include="..\..\MTEUtils\TabBarDrawer.cs" Link="MTEUtils\TabBarDrawer.cs" />
    <Compile Include="..\..\MTEUtils\TabBarLayout.cs" Link="MTEUtils\TabBarLayout.cs" />
    <Compile Include="..\..\MTEUtils\TooltipDrawer.cs" Link="MTEUtils\TooltipDrawer.cs" />
    <Compile Include="..\..\MTEUtils\TransformCache.cs" Link="MTEUtils\TransformCache.cs" />
    <Compile Include="..\..\MTEUtils\WindowManagerBase.cs" Link="MTEUtils\WindowManagerBase.cs" />
    <Compile Include="..\..\MTEUtils\WindowResizeController.cs" Link="MTEUtils\WindowResizeController.cs" />
```

参照の ItemGroup の `UnityEngine.TextRenderingModule` の行の直後に追加:

```xml
    <Reference Include="UnityEngine.AnimationModule"><HintPath>$(Managed)\UnityEngine.AnimationModule.dll</HintPath><Private>false</Private></Reference>
```

- [ ] **Step 2: ビルドして足りない依存を洗い出す**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`（この時点では旧ウィンドウのままで構わない）
Expected: ビルド成功。`CS0246`（型が見つからない）が MTEUtils の型を指して出た場合は、その型を定義する `MTEUtils/*.cs` を同じ形で足して再ビルドする（`grep -l "class <型名>\|interface <型名>\|enum <型名>" MTEUtils/*.cs` で探す）。`CS0012` が出たら、エラーが示す `UnityEngine.*Module.dll` を参照に足す。足したものは ledger に Ruling として記録する

- [ ] **Step 3: ウィンドウ位置の保存を書く**

`UI/WindowPlacementConfig.cs`:

```csharp
using BepInEx.Configuration;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// ウィンドウの位置とサイズを BepInEx の設定へ保存する。
    /// DockableWindowBase はドラッグ中に毎フレーム StorePlacement を呼ぶため、
    /// マウスを離してからまとめて書く (設定は値の代入ごとにファイルへ書かれる)
    /// </summary>
    public sealed class WindowPlacementConfig
    {
        private readonly ConfigFile _config;
        private readonly ConfigEntry<int> _x;
        private readonly ConfigEntry<int> _y;
        private readonly ConfigEntry<int> _width;
        private readonly ConfigEntry<int> _height;
        private int _pendingX;
        private int _pendingY;
        private int _pendingWidth;
        private int _pendingHeight;
        private bool _dirty;

        public WindowPlacementConfig(ConfigFile config, int defaultWidth, int defaultHeight)
        {
            _config = config;
            // 負の位置は画面中央 (DockableWindowBase.Init の扱い)
            _x = config.Bind("Window", "X", -1, "ウィンドウの X 座標 (負なら画面中央)");
            _y = config.Bind("Window", "Y", -1, "ウィンドウの Y 座標 (負なら画面中央)");
            _width = config.Bind("Window", "Width", defaultWidth, "ウィンドウの幅");
            _height = config.Bind("Window", "Height", defaultHeight, "ウィンドウの高さ");
        }

        public void Load(out int x, out int y, out int width, out int height)
        {
            x = _x.Value;
            y = _y.Value;
            width = _width.Value;
            height = _height.Value;
        }

        public void Store(int x, int y, int width, int height)
        {
            _pendingX = x;
            _pendingY = y;
            _pendingWidth = width;
            _pendingHeight = height;
            _dirty = true;
        }

        public void FlushIfReleased()
        {
            if (!_dirty || Input.GetMouseButton(0))
            {
                return;
            }
            _dirty = false;
            var saveOnSet = _config.SaveOnConfigSet;
            _config.SaveOnConfigSet = false;
            _x.Value = _pendingX;
            _y.Value = _pendingY;
            _width.Value = _pendingWidth;
            _height.Value = _pendingHeight;
            _config.SaveOnConfigSet = saveOnSet;
            _config.Save();
        }
    }
}
```

- [ ] **Step 4: WindowManager を書く**

`UI/WindowManager.cs`（入力の抑止は `W:\COM3D2_5\work\COM3D2.PostEffects.Plugin\source\COM3D25.PostEffects.Plugin\Manager\WindowManager.cs` と同じ方式）:

```csharp
using COM3D2.MotionTimelineEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// MToonMaid のウィンドウ管理。ウィンドウ上にカーソルがある間は、カメラ・ゲーム UI・ギズモへの入力を止める。
    /// どれもゲームや他プラグインと共有するフラグなので、自分が止めたときだけ戻す
    /// </summary>
    public sealed class WindowManager : WindowManagerBase
    {
        // カラーピッカーは他プラグインに同梱された MTEUtils と既定の ID が同じため、ずらしておく
        private const int ColorPickerWindowId = 0x4D546F6F;

        private readonly IMToonMaidHost _host;
        private readonly WindowPlacementConfig _placement;
        private bool _isCameraControlDisabled;
        private bool _isUIInputDisabled;
        private bool _isGizmoLocked;
        private bool _isCameraPressInProgress;
        private bool _isCameraDragFromOutside;

        public MToonMaidWindow MainWindow { get; private set; }

        public WindowManager(IMToonMaidHost host, WindowPlacementConfig placement)
        {
            _host = host;
            _placement = placement;
        }

        public override void Init()
        {
            base.Init();

            MainWindow = new MToonMaidWindow(_host, _placement);
            AddWindow(MainWindow);

            ColorPickerWindow.windowId = ColorPickerWindowId;
            AddWindow(ColorPickerWindow.instance);
            // コンボボックスのポップアップはホストの描画中に開閉が決まるため、ホストより後に登録する
            AddWindow(ComboBoxPopupWindow.instance);
            // モーダルダイアログは最前面へ出すため最後に登録する
            AddWindow(DialogPopupWindow.instance);
        }

        protected override void OnAfterUpdate()
        {
            UpdateInputBlock();
        }

        private void UpdateInputBlock()
        {
            var isMouseOverWindow = false;
            foreach (var window in windows)
            {
                if (window.isShowWnd && MTEUtils.IsMouseOverWindowRect(window.windowRect))
                {
                    isMouseOverWindow = true;
                    break;
                }
            }

            UpdateCameraDragFromOutside(isMouseOverWindow);
            // ウィンドウ外から始まったドラッグはカメラ操作を続けさせる。UI とギズモは誤操作防止を優先して塞ぐ
            UpdateCameraControl(isMouseOverWindow && !_isCameraDragFromOutside);
            UpdateUIInput(isMouseOverWindow);
            UpdateGizmoLock(GUIUtility.hotControl != 0 || (isMouseOverWindow && Input.GetMouseButton(0)));
        }

        /// <summary>押下フレームのカーソル位置で、ドラッグがウィンドウ外から始まったかを決め、ボタンを離すまで保つ</summary>
        private void UpdateCameraDragFromOutside(bool isMouseOverWindow)
        {
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
            {
                _isCameraPressInProgress = false;
                _isCameraDragFromOutside = false;
                return;
            }
            if (_isCameraPressInProgress)
            {
                return;
            }
            _isCameraPressInProgress = true;
            _isCameraDragFromOutside = !isMouseOverWindow;
        }

        private void UpdateCameraControl(bool shouldBlock)
        {
            var mainCamera = GameMain.Instance != null ? GameMain.Instance.MainCamera : null;
            if (mainCamera == null)
            {
                return;
            }
            if (shouldBlock)
            {
                // 自分が止める前から無効なら他者の管理下なので触らない
                if (_isCameraControlDisabled || mainCamera.GetControl())
                {
                    mainCamera.SetControl(false);
                    _isCameraControlDisabled = true;
                }
            }
            else if (_isCameraControlDisabled)
            {
                mainCamera.SetControl(true);
                _isCameraControlDisabled = false;
            }
        }

        private void UpdateUIInput(bool shouldBlock)
        {
            if (shouldBlock)
            {
                if (_isUIInputDisabled || UICamera.InputEnable)
                {
                    UICamera.InputEnable = false;
                    _isUIInputDisabled = true;
                }
            }
            else if (_isUIInputDisabled)
            {
                UICamera.InputEnable = true;
                _isUIInputDisabled = false;
            }
        }

        private void UpdateGizmoLock(bool shouldLock)
        {
            if (shouldLock)
            {
                if (_isGizmoLocked || !GizmoRender.global_control_lock)
                {
                    GizmoRender.global_control_lock = true;
                    _isGizmoLocked = true;
                }
            }
            else if (_isGizmoLocked)
            {
                GizmoRender.global_control_lock = false;
                _isGizmoLocked = false;
            }
        }

        private void RestoreInputBlock()
        {
            UpdateCameraControl(false);
            UpdateUIInput(false);
            UpdateGizmoLock(false);
            _isCameraPressInProgress = false;
            _isCameraDragFromOutside = false;
        }

        protected override void OnBeforeCloseWindows()
        {
            RestoreInputBlock();
        }

        public override void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            RestoreInputBlock();
            base.OnChangedSceneLevel(scene, sceneMode);
        }
    }
}
```

- [ ] **Step 5: ウィンドウを書き直す**

`UI/MToonMaidWindow.cs` を次の内容で置き換える:

```csharp
using System;
using System.Collections.Generic;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>メイドの選択・ON/OFF・対象のグループとスロット・グループごとの調整値のウィンドウ</summary>
    public sealed class MToonMaidWindow : DockableWindowBase
    {
        public const int DefaultWidth = 380;
        public const int DefaultHeight = 640;

        private const int WindowId = 0x4D546F6E; // "MTon"
        private const float RowHeight = 20f;
        private const float LabelWidth = 110f;
        private const float TabWidth = 60f;
        private const float ResetButtonWidth = 200f;

        private static readonly string[] GroupLabels = { "肌", "顔", "髪", "衣装" };

        private readonly IMToonMaidHost _host;
        private readonly WindowPlacementConfig _placement;
        private readonly GUIView _rootView = new GUIView();
        private readonly GUIView _contentView = new GUIView();
        private readonly GUIComboBox<Maid> _maidComboBox = new GUIComboBox<Maid>();
        // リセットボタンと色の既定値に使う。描画ごとに作らない
        private readonly GroupSettings[] _defaults;
        private int _width = DefaultWidth;
        private int _height = DefaultHeight;
        private PartGroup _group = PartGroup.Skin;
        // 描画中の変更は描画の後でまとめて本体へ伝える
        private bool _groupChanged;
        private bool _targetsChanged;
        private bool? _enabledRequest;
        // コンボボックスのポップアップ中は全体を無効にする。行単位の無効表示の後はこの値へ戻す
        private bool _baseEnabled = true;

        protected override int windowId => WindowId;
        protected override string windowTitle => "MToonMaid";
        // 色の行 (ラベル・見本・HEX・R・編集) がスクロールバーと重ならない幅
        protected override int minWidth => 340;
        protected override int minHeight => 320;

        public MToonMaidWindow(IMToonMaidHost host, WindowPlacementConfig placement)
        {
            _host = host;
            _placement = placement;
            windowIndex = 0;
            _maidComboBox.getName = (maid, index) => maid != null && maid.status != null ? maid.status.fullNameJpStyle : "";

            var groups = (PartGroup[])Enum.GetValues(typeof(PartGroup));
            _defaults = new GroupSettings[groups.Length];
            foreach (var group in groups)
            {
                _defaults[(int)group] = GroupSettings.CreateDefault(group);
            }
        }

        protected override void LoadPlacement(out int x, out int y, out int width, out int height)
        {
            _placement.Load(out x, out y, out width, out height);
        }

        protected override void StorePlacement(int x, int y, int width, int height)
        {
            _placement.Store(x, y, width, height);
        }

        public override void Init()
        {
            base.Init();
            _width = (int)localWindowRect.width;
            _height = (int)localWindowRect.height;
            InitView();
        }

        public override void Update()
        {
            base.Update();
            _placement.FlushIfReleased();
        }

        protected override void OnSizeChanged(int width, int height)
        {
            _width = width;
            _height = height;
            InitView();
        }

        private void InitView()
        {
            _rootView.Init(0, 0, _width, _height);
            _contentView.Init(0, HEADER_HEIGHT, _width, _height - HEADER_HEIGHT);
            _contentView.parent = _rootView;
        }

        protected override void DrawContent()
        {
            _rootView.ResetLayout();
            DrawMain(_contentView);
            ComboBoxPopupWindow.instance.ProcessFocus(_rootView, this);
        }

        private void DrawMain(GUIView view)
        {
            view.ResetLayout();
            _baseEnabled = !ComboBoxPopupWindow.instance.IsOpenFor(this);
            view.SetEnabled(_baseEnabled);

            if (!_host.IsShaderAvailable)
            {
                view.DrawLabel("シェーダーを読み込めないか、URP が有効なため動作しません。BepInEx のログを確認してください", -1, RowHeight * 3);
                return;
            }

            var maids = _host.GetActiveMaids();
            if (maids.Count == 0)
            {
                view.DrawLabel("表示中のメイドがいません", -1, RowHeight);
                return;
            }

            SyncMaidItems(maids);
            _maidComboBox.DrawButton("メイド", view);
            var maid = _maidComboBox.currentItem;
            if (maid == null)
            {
                return;
            }

            var settings = _host.GetSettings(maid);
            view.DrawToggle("MToon にする", settings.enabled, -1, RowHeight, v => _enabledRequest = v);
            view.DrawLabel("変換済みマテリアル: " + _host.GetConvertedCount(maid), -1, RowHeight);
            view.DrawHorizontalLine();

            _group = (PartGroup)view.DrawTabs(GroupLabels, (int)_group, TabWidth, RowHeight);
            var g = settings.Get(_group);
            view.DrawToggle("このグループを MToon にする", g.enabled, -1, RowHeight, v =>
            {
                g.enabled = v;
                _targetsChanged = true;
            });

            view.BeginScrollView();
            DrawGroupSettings(view, g, _defaults[(int)_group]);
            if (view.DrawButton("このグループの調整値を既定に戻す", ResetButtonWidth, RowHeight))
            {
                ResetGroup(settings);
            }
            // スロットの数はメイドや着替えで変わるため、入力欄キャッシュの番号がずれないようスライダーより後に描く
            DrawSlots(view, maid, settings, g.enabled);
            view.EndScrollView();

            Commit(maid);
        }

        private void SyncMaidItems(List<Maid> maids)
        {
            var current = _maidComboBox.currentItem;
            _maidComboBox.items.Clear();
            _maidComboBox.items.AddRange(maids);
            var index = current != null ? maids.IndexOf(current) : -1;
            _maidComboBox.currentIndex = index >= 0 ? index : 0;
        }

        private void DrawGroupSettings(GUIView view, GroupSettings g, GroupSettings def)
        {
            var prefix = GroupLabels[(int)_group] + "/";

            view.DrawLabel("陰影", -1, RowHeight);
            ColorRow(view, prefix + "影色", "影色", g.shadeColor, def.shadeColor, true);
            Slider(view, "境界の位置", g.shadingShift, -1f, 1f, def.shadingShift, v => g.shadingShift = v);
            Slider(view, "境界の硬さ", g.shadingToony, 0f, 1f, def.shadingToony, v => g.shadingToony = v);
            Slider(view, "環境光の均一化", g.giEqualization, 0f, 1f, def.giEqualization, v => g.giEqualization = v);

            view.DrawLabel("天使の輪", -1, RowHeight);
            Slider(view, "強さ", g.highlightScale, 0f, 2f, def.highlightScale, v => g.highlightScale = v);

            view.DrawLabel("リム", -1, RowHeight);
            Toggle(view, "色を上書きする", g.rimColorOverride, v => g.rimColorOverride = v);
            // 上書きしないときも行は隠さず無効表示にする (行の増減で入力欄キャッシュの番号がずれる)
            ColorRow(view, prefix + "リム色", "リム色", g.rimColor, def.rimColor, g.rimColorOverride);
            Slider(view, "鋭さ", g.rimFresnelPower, 0f, 100f, def.rimFresnelPower, v => g.rimFresnelPower = v);
            Slider(view, "持ち上げ", g.rimLift, 0f, 1f, def.rimLift, v => g.rimLift = v);
            Slider(view, "ライト色の影響", g.rimLightingMix, 0f, 1f, def.rimLightingMix, v => g.rimLightingMix = v);

            view.DrawLabel("アウトライン", -1, RowHeight);
            Toggle(view, "アウトラインを描く", g.outlineEnabled, v => g.outlineEnabled = v);
            // 既定の書式 (小数 2 桁) では 0.0015 が 0.00 に見えるため 4 桁で出す
            Slider(view, "幅", g.outlineWidth, 0f, 0.01f, def.outlineWidth, v => g.outlineWidth = v, FloatFieldType.F4);
            Slider(view, "ライト色の影響", g.outlineLightingMix, 0f, 1f, def.outlineLightingMix, v => g.outlineLightingMix = v);
            Toggle(view, "線の色を上書きする", g.outlineColorOverride, v => g.outlineColorOverride = v);
            ColorRow(view, prefix + "線の色", "線の色", g.outlineColor, def.outlineColor, g.outlineColorOverride);
        }

        private void DrawSlots(GUIView view, Maid maid, MaidSettings settings, bool groupEnabled)
        {
            view.DrawHorizontalLine();
            view.DrawLabel("MToon にするスロット", -1, RowHeight);
            foreach (var slot in _host.GetSlotCategories(maid, _group))
            {
                var name = slot;
                // グループが OFF の間は選べないよう無効表示にする
                view.DrawToggle(name, !settings.excludedSlots.Contains(name), -1, RowHeight, groupEnabled && _baseEnabled, v =>
                {
                    settings.SetSlotExcluded(name, !v);
                    _targetsChanged = true;
                });
            }
        }

        private void ResetGroup(MaidSettings settings)
        {
            // 対象の ON/OFF は調整値ではないので残す
            var enabled = settings.Get(_group).enabled;
            var reset = GroupSettings.CreateDefault(_group);
            reset.enabled = enabled;
            settings.Set(_group, reset);
            _groupChanged = true;
        }

        private void Commit(Maid maid)
        {
            if (_enabledRequest.HasValue)
            {
                _host.SetEnabled(maid, _enabledRequest.Value);
                _enabledRequest = null;
            }
            if (_targetsChanged)
            {
                _targetsChanged = false;
                _host.NotifyTargetsChanged(maid);
            }
            if (_groupChanged)
            {
                _groupChanged = false;
                _host.NotifyGroupChanged(maid, _group);
            }
        }

        private void Slider(GUIView view, string label, float value, float min, float max, float defaultValue, Action<float> set,
            FloatFieldType fieldType = FloatFieldType.Float)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = LabelWidth,
                width = -1,
                fieldType = fieldType,
                min = min,
                max = max,
                defaultValue = defaultValue,
                value = value,
                onChanged = v =>
                {
                    set(v);
                    _groupChanged = true;
                },
            });
        }

        private void Toggle(GUIView view, string label, bool value, Action<bool> set)
        {
            view.DrawToggle(label, value, -1, RowHeight, v =>
            {
                set(v);
                _groupChanged = true;
            });
        }

        /// <summary>
        /// key はカラーピッカーの同定にも使うため、グループ間で重複させない。
        /// 行の全要素 (編集ボタンを含む) を無効にするため BeginEnabled ではなく SetEnabled を使う
        /// (BeginEnabled は直後の 1 要素にしか効かない)
        /// </summary>
        private void ColorRow(GUIView view, string key, string label, ColorData color, ColorData defaultColor, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            view.DrawColor(view.GetColorFieldCache(key, false), ToColor(color), ToColor(defaultColor), c =>
            {
                color.r = c.r;
                color.g = c.g;
                color.b = c.b;
                _groupChanged = true;
            }, label);
            view.SetEnabled(_baseEnabled);
        }

        private static Color ToColor(ColorData c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
    }
}
```

入力欄のキャッシュは描画順の番号で引かれるため、リムとアウトラインで同じラベル（「ライト色の影響」）を使っても干渉しない。

- [ ] **Step 6: プラグイン本体を WindowManager へつなぎ替える**

`MToonMaidPlugin.cs`:

`using UnityEngine;` の直後に `using UnityEngine.SceneManagement;` を足す。

フィールド `private MToonMaidWindow _window;` を `private WindowManager _windowManager;` に置き換える。

`Awake` の `_window = new MToonMaidWindow(this);` を次へ置き換える:

```csharp
            _windowManager = new WindowManager(this,
                new WindowPlacementConfig(Config, MToonMaidWindow.DefaultWidth, MToonMaidWindow.DefaultHeight));
            _windowManager.Init();
            SceneManager.sceneLoaded += OnSceneLoaded;
```

`Update` の先頭から `_window.UpdateCameraLock();` までを次へ置き換える:

```csharp
            if (_toggleKey.Value.IsDown())
            {
                ToggleWindow();
            }
            EnsureGearIcon();
            _windowManager.Update();
```

`OnApplicationQuit` の直後の `OnGUI` を次へ置き換え、メソッドを足す:

```csharp
        private void LateUpdate()
        {
            _windowManager?.LateUpdate();
        }

        private void OnGUI()
        {
            _windowManager?.OnGUI();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _windowManager?.OnPluginDisable();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _windowManager.OnChangedSceneLevel(scene, mode);
        }

        private void ToggleWindow()
        {
            var window = _windowManager.MainWindow;
            window.isShowWnd = !window.isShowWnd;
        }
```

`EnsureGearIcon` の中の `go => _window.Visible = !_window.Visible);` を `go => ToggleWindow());` に、`_window.Visible` の 3 か所を `_windowManager.MainWindow.isShowWnd` に置き換える。

- [ ] **Step 7: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0。MTEUtils 由来の警告が出た場合は件数を ledger に記録するだけでよい）、テスト PASS（56 件）。`grep -n "UpdateCameraLock\|_window\b" source/COM3D25.MToonMaid.Plugin/*.cs` が 0 件

- [ ] **Step 8: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj source/COM3D25.MToonMaid.Plugin/UI source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs
git commit -m "feat: UI を MTEUtils の GUIView へ置き換え"
```

---

### Task 6: 実機検証と仕上げ

ゲームが DLL を掴んでいるため、反映には再起動が要る。**restart-verify スキル**（`com3d25-devbridge:restart-verify`）の手順で、ゲーム終了 → `debug.bat` で DLL を配置 → 起動 → セーブのロード → デイリー画面で検証する。

eval では `UnityEngine` の型を完全修飾名で書く。ローカル関数は使えないので `System.Func` で書く。

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-10-08-mtoon-maid-v2-design.md`（検証の結果）

- [ ] **Step 1: DLL を配置して起動する**

restart-verify スキルに従う。起動後に `tail_log` で `MToonMaid 有効化` が出ていて、`COM3D25.MToonMaid` を含む例外が無いことを確認する。

- [ ] **Step 2: 準備の eval を流す**

```csharp
var maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
var plugin = UnityEngine.Object.FindObjectOfType<COM3D25.MToonMaid.Plugin.MToonMaidPlugin>();
System.Func<string, System.Collections.Generic.List<UnityEngine.Material>> MatsOf = prefix => {
  var list = new System.Collections.Generic.List<UnityEngine.Material>(); var sl = maid0.body0.goSlot;
  for (int i = 0; i < sl.Count; i++) for (int j = 0; j < sl.CountChildren(i); j++) { var sk = sl[i, j]; if (sk == null || sk.obj == null || !sk.Category.StartsWith(prefix)) continue;
    foreach (var r in sk.obj.GetComponentsInChildren<UnityEngine.Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null) list.Add(m); }
  return list; };
var cam = GameMain.Instance.MainCamera.camera;
System.Func<UnityEngine.Color[]> Shot = () => { int w = 640, h = 360; var rt = UnityEngine.RenderTexture.GetTemporary(w, h, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.Linear); var pt = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = pt; var pa = UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active = rt; var t = new UnityEngine.Texture2D(w, h, UnityEngine.TextureFormat.RGBA32, false, true); t.ReadPixels(new UnityEngine.Rect(0, 0, w, h), 0, 0); t.Apply(); UnityEngine.RenderTexture.active = pa; UnityEngine.RenderTexture.ReleaseTemporary(rt); var px = t.GetPixels(); UnityEngine.Object.Destroy(t); return px; };
plugin.IsShaderAvailable
```

Expected: `true`

- [ ] **Step 3: 髪の明るさがバニラに近いことを数値で確かめる**

OFF で撮り、髪のマテリアルだけ一時的に黒くして髪の画素を特定し、ON で撮り直して髪の画素の平均を比べる:

```csharp
plugin.SetEnabled(maid0, false);
var hair = MatsOf("hair"); var vanilla = Shot();
var savedColors = new System.Collections.Generic.List<UnityEngine.Color>(); foreach (var m in hair) { savedColors.Add(m.GetColor("_Color")); m.SetColor("_Color", UnityEngine.Color.black); }
var blackHair = Shot(); for (int i = 0; i < hair.Count; i++) hair[i].SetColor("_Color", savedColors[i]);
var mask = new System.Collections.Generic.List<int>(); for (int i = 0; i < vanilla.Length; i++) if (System.Math.Abs(vanilla[i].g - blackHair[i].g) > 0.02f) mask.Add(i);
plugin.SetEnabled(maid0, true); "mask=" + mask.Count
```

次の eval（数フレーム後）で:

```csharp
var mtoon = Shot();
double sv = 0, sm = 0; foreach (var i in mask) { sv += vanilla[i].r + vanilla[i].g + vanilla[i].b; sm += mtoon[i].r + mtoon[i].g + mtoon[i].b; }
"vanilla=" + (sv / mask.Count / 3).ToString("F3") + " mtoon=" + (sm / mask.Count / 3).ToString("F3") + " hair0=" + hair[0].shader.name + " lit=" + hair[0].GetColor("_ToonRampLitColor") + " shade=" + hair[0].GetColor("_ShadeColor")
```

Expected: `hair0=COM3D25/MToon10`。`mtoon` が `vanilla` の ±20% 以内（v1 では約 1.7 倍だった）。`lit` は黒髪で 0.58 前後のグレー。`screenshot` で天使の輪（前髪の艶の帯）が出ていること、髪のアウトラインに天使の輪の明るさが乗っていないことを目で確かめる

アンビエント（GI）の効き方も合っているかを、メインライトを弱めて比べる（`_ToonRampLitColor` は GI にも掛かるため）。次の eval で、OFF と ON をそれぞれライトの強さ 0.3 倍で撮る:

```csharp
var light = GameMain.Instance.MainLight.GetComponent<UnityEngine.Light>(); var intensity = light.intensity;
light.intensity = intensity * 0.3f; var dimMtoon = Shot(); plugin.SetEnabled(maid0, false); light.intensity = intensity; "dim mtoon taken"
```

次の eval で:

```csharp
light.intensity = intensity * 0.3f; var dimVanilla = Shot(); light.intensity = intensity; plugin.SetEnabled(maid0, true);
double dv = 0, dm = 0; foreach (var i in mask) { dv += dimVanilla[i].r + dimVanilla[i].g + dimVanilla[i].b; dm += dimMtoon[i].r + dimMtoon[i].g + dimMtoon[i].b; }
"dimVanilla=" + (dv / mask.Count / 3).ToString("F3") + " dimMtoon=" + (dm / mask.Count / 3).ToString("F3")
```

Expected: `dimMtoon` が `dimVanilla` の ±30% 以内。外れたら、GI に `_ToonRampLitColor` を掛けない構成（`GetMToonLighting_GlobalIllumination` で `input.litColor` の代わりに `_ToonRampLitColor` を掛ける前の色を使う）を検討し、結果を spec の検証結果に書く

- [ ] **Step 4: グループの OFF とスロットの除外で元へ戻ることを確かめる**

```csharp
var s = plugin.GetSettings(maid0); s.hair.enabled = false; plugin.NotifyTargetsChanged(maid0); "hair off"
```

次の eval で:

```csharp
var r1 = MatsOf("hair")[0].shader.name + " / body=" + MatsOf("body")[0].shader.name;
s.hair.enabled = true; s.SetSlotExcluded("accHat", true); plugin.NotifyTargetsChanged(maid0); r1
```

Expected: `CM3D2/Toony_Lighted_Hair_Outline_Tex / body=COM3D25/MToon10`（髪だけ元に戻り、肌は MToon のまま）

次の eval で:

```csharp
var r2 = MatsOf("hair")[0].shader.name + " / accHat=" + (MatsOf("accHat").Count > 0 ? MatsOf("accHat")[0].shader.name : "none");
s.SetSlotExcluded("accHat", false); plugin.NotifyTargetsChanged(maid0); r2
```

Expected: 髪は `COM3D25/MToon10` に戻り、`accHat` は元の Toony（帽子を着けていないメイドなら `none`。その場合は衣装の別スロットで同じ確認をする）。最後の eval の後、`accHat` が `COM3D25/MToon10` に戻ることも確かめる

- [ ] **Step 5: 無限色の変更でランプが読み直されることを確かめる**

```csharp
var pc0 = maid0.Parts.GetPartsColor(MaidParts.PARTS_COLOR.HAIR); var pc1 = pc0; pc1.m_bUse = true; pc1.m_nMainHue = 0; pc1.m_nMainChroma = 200; pc1.m_nMainBrightness = 300;
var before = MatsOf("hair")[0].GetColor("_ToonRampLitColor"); maid0.Parts.SetPartsColor(MaidParts.PARTS_COLOR.HAIR, pc1); "before=" + before
```

次の eval（10 フレーム以上後）で:

```csharp
var after = MatsOf("hair")[0].GetColor("_ToonRampLitColor"); maid0.Parts.SetPartsColor(MaidParts.PARTS_COLOR.HAIR, pc0); "after=" + after
```

Expected: `after` が `before` と違う値（調査では 0.58 → 約 1.0）。さらに次の eval で元の値へ戻ること

- [ ] **Step 6: ウィンドウを確かめる**

```csharp
var wm = (COM3D25.MToonMaid.Plugin.WindowManager)typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).GetField("_windowManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
wm.MainWindow.isShowWnd = true; "shown"
```

`screenshot`（`window: "MToonMaid"`）で、メイドのコンボボックス・「MToon にする」・グループのタブ・「このグループを MToon にする」・陰影／天使の輪／リム／アウトラインの行・スロット一覧が描かれていること、上書きしない色の行が無効表示であることを確かめる。タブを切り替えた状態でも描画が崩れないことを、`_group` をリフレクションで変えて確かめる。`tail_log` に `ArgumentException`（GUILayout のコントロール数の不一致）や `COM3D25.MToonMaid` を含む例外が無いこと。ウィンドウ上でのマウス操作でカメラが動かないことは利用者に確認してもらう（devbridge ではマウスを動かせない）。SceneEditor が導入されていれば、SceneEditor のウィンドウを開いた状態で同じく描画とポップアップ（メイドのコンボボックス）が崩れないことを確かめる（MTEUtils の `ComboBoxPopupWindow` 等のウィンドウ ID は他プラグインの同梱コピーと同じ固定値のため）。確かめたら `isShowWnd = false` に戻す

- [ ] **Step 7: 版 1 の設定ファイルが読めることを確かめる**

`BepInEx/config/COM3D25.MToonMaid/<GUID>.xml` を開き、`version="2"` で保存されていること、`<excludedSlots />` があることを確かめる（Step 4 で値を変えているので 1 秒後に保存されている）

- [ ] **Step 8: README と spec を更新する**

`README.md` の「使い方」の 3 を次へ置き換え、4・5 を足す:

```markdown
3. 肌・顔・髪・衣装のタブで、影色・境界・天使の輪・リム・アウトラインを調整します
4. 「このグループを MToon にする」で部位ごとに置き換えを切り替えられます。タブの下のスロット一覧で、個別のスロット（帽子など）だけを外すこともできます
5. SceneEditor を導入している場合、ウィンドウは SceneEditor のドッキングに参加します
```

「制限」の `- 髪の天使の輪 (`_HiTex`) とトゥーンランプ (`_ToonRamp`) は MToon に対応するものが無いため反映されません` を次へ置き換える:

```markdown
- トゥーンランプ (`_ToonRamp`) は光側と影側の色だけを使います。光と影の境界の位置はグループの「境界の位置」で調整してください
```

`docs/superpowers/specs/2026-10-08-mtoon-maid-v2-design.md` の末尾に「## 実機検証の結果」を足し、Step 3〜7 で得た値（髪の明るさのバニラ比、ランプの値の変化、確認できなかった項目）を書く。

- [ ] **Step 9: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-08-mtoon-maid-v2-design.md
git commit -m "docs: v2 の実機検証の結果と使い方を追加"
```

## レビュー却下メモ

- 「ledger が未定義」 — executing-plans スキルが `.superpowers/sdd/<計画名>/progress.md` に作る台帳を指す。Global Constraints に場所を明記した
- 「UI 倍率（GUIScale）が SceneEditor に追従しない」 — 意図どおり（UIScaleClient を取り込まない）。Global Constraints に明記した
