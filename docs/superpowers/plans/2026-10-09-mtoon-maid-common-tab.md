# COM3D25.MToonMaid.Plugin 共通タブ・ウィンドウの余白 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 「共通」タブを足し、各部位グループは項目ごとに共通を継承するか個別に上書きできるようにする。あわせてウィンドウの余白を SceneEditor にそろえ、スクロールエリアの上に水平線と空きを入れる。

**Architecture:** `MaidSettings` に共通の調整値 `common` を、`GroupSettings` に個別にした項目のキー一覧 `overrides` を足す（版 3）。マテリアルへ入れる値は `MaidSettings.Resolve(group)` が項目ごとに共通かグループかを選んで作る。Controller とプラグイン本体は `Get` の代わりに `Resolve` を使う。ウィンドウはタブを 5 つにし、グループのタブでは各行の先頭に「個別」のトグルを置き、「共通を使う」行を編集したらその項目を個別にしてから書く。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-common-tab-design.md`（前提として `docs/superpowers/specs/2026-10-08-mtoon-maid-v2-design.md`）

## Global Constraints

- v2 計画（`docs/superpowers/plans/2026-10-08-mtoon-maid-v2.md`）の Global Constraints をすべて引き継ぐ。特に: 対象は COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は作らない・実行しない、git worktree は使わない、作業は `W:\COM3D2_5\work\COM3D25.MToonMaid.Plugin` の main で行う、MTEUtils のファイルは編集しない
- ビルド確認は `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`、テストは `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`。`debug.bat` はゲームへ DLL をコピーするため Task 4 でだけ使う
- 設定ファイルの版は 3。版 1・版 2 のファイルを読めて、読み込み後の見た目が変わらないこと
- 継承の単位は spec の 13 項目。「このグループを MToon にする」（`GroupSettings.enabled`）とスロットの除外は継承しない
- GUIView の入力欄キャッシュは描画順の番号で引かれる。スライダーの行の数と順序は全タブで同じにする（共通タブでも同じ行を同じ順で描く）
- 計画からの逸脱や実装中の判断は ledger（`.superpowers/sdd/<計画名>/progress.md`）に `Ruling:` 行で記録する

## Review Focus

- **版 2 の設定ファイル**（グループごとに違う値が入っている）: 読み込み後に各グループの解決後の値が元の値と同じ。既定と同じ項目は個別にしない → Task 1 のテスト `版2で既定と違う項目だけ個別として引き継ぐ`
- **「共通を使う」行の編集**: その項目が個別になり、編集前の見た目（共通の値）から編集が当たる。共通の値は変わらない → Task 1 の `個別にした項目は…`、Task 3 の `BeginEdit`、Task 4 Step 5
- **色の参照の共有**: 個別にした色を編集しても共通や他のグループの色が変わらない（`ColorData` は参照型で、ウィンドウは成分を書き換える） → Task 1 の `個別にした色は共通と別の色になる`
- **共通タブの変更**: 次の走査を待たずに全グループのマテリアルへ反映される → Task 3 の `Commit`、Task 4 Step 4
- **グループのタブの「R」**: 既定値ではなく共通の値へ戻る（個別のまま共通と同じ値になる）。共通タブの「R」は既定値へ戻る → Task 3 の `DrawSettings` の `def`、Task 4 Step 6
- **全 13 項目の写し漏れ**: `CopyField` / `FieldEquals` の switch に項目を足し忘れると、その項目だけ継承が効かない → Task 1 の `全項目を個別にすると…`

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/GroupSettingKeys.cs   (Create: 継承の単位のキー)
├─ Settings/GroupSettings.cs      (Modify: overrides / CopyField / FieldEquals、CreateDefault の引数を外す)
├─ Settings/MaidSettings.cs       (Modify: common / Resolve / SetOverride / ClearOverrides / 版 3 への更新)
├─ MaidMToonController.cs         (Modify: Get → Resolve)
├─ MToonMaidPlugin.cs             (Modify: Get → Resolve)
└─ UI/MToonMaidWindow.cs          (Rewrite: 余白・水平線・共通タブ・個別トグル)
source/COM3D25.MToonMaid.Plugin.Tests/
├─ CommonSettingsTests.cs         (Create)
└─ MaidSettingsStoreTests.cs      (Modify: CreateDefault の呼び出し)
README.md                          (Modify)
docs/superpowers/specs/2026-10-09-mtoon-maid-common-tab-design.md (Modify: 検証の結果)
```

---

### Task 1: 設定の版 3（共通・個別の上書き・解決）

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`, `Settings/MaidSettings.cs`
- Create: `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin.Tests/MaidSettingsStoreTests.cs`（2 か所の `GroupSettings.CreateDefault(PartGroup.Skin)` → `GroupSettings.CreateDefault()`）
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（コンパイルを通すため `CreateDefault(group)` / `CreateDefault(_group)` の 2 か所だけ。Task 3 で書き直す）

**Interfaces:**
- Consumes: `MaidSettingsStore.Save(string, MaidSettings)` / `MaidSettingsStore.Load(string, Action<string>)`（既存）
- Produces:
  - `static class GroupSettingKeys`（13 個の `const string` と `static readonly string[] All`）
  - `GroupSettings.overrides : List<string>`、`bool IsOverridden(string key)`、`void CopyField(string key, GroupSettings from)`、`bool FieldEquals(string key, GroupSettings other)`、`static GroupSettings CreateDefault()`、`void FillMissing()`（引数なし）
  - `MaidSettings.CurrentVersion = 3`、`MaidSettings.common : GroupSettings`、`GroupSettings Resolve(PartGroup group)`、`void SetOverride(PartGroup group, string key, bool overridden)`、`void ClearOverrides(PartGroup group)`

- [ ] **Step 1: 失敗するテストを書く**

`source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class CommonSettingsTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public CommonSettingsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidCommonTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        private static MaidSettings Filled()
        {
            var s = new MaidSettings();
            s.FillMissing();
            return s;
        }

        /// <summary>全項目を既定と違う値にする。FieldEquals で全項目が既定と違うことを確かめてから使う</summary>
        private static void ChangeAll(GroupSettings g)
        {
            g.shadeColor.r = 0.5f;
            g.shadingShift = 0.3f;
            g.shadingToony = 0.5f;
            g.giEqualization = 0.1f;
            g.highlightScale = 2f;
            g.rimColorOverride = true;
            g.rimFresnelPower = 10f;
            g.rimLift = 0.5f;
            g.rimLightingMix = 0.2f;
            g.outlineEnabled = false;
            g.outlineWidth = 0.005f;
            g.outlineLightingMix = 0.3f;
            g.outlineColorOverride = true;
        }

        [Fact]
        public void 既定では各グループは共通の値を使う()
        {
            var s = Filled();
            s.common.shadingShift = 0.3f;
            s.common.shadeColor.g = 0.5f;

            Assert.Equal(0.3f, s.Resolve(PartGroup.Hair).shadingShift);
            Assert.Equal(0.5f, s.Resolve(PartGroup.Skin).shadeColor.g);
            Assert.Empty(s.hair.overrides);
        }

        [Fact]
        public void 個別にした項目はグループの値を使い共通の変更が効かない()
        {
            var s = Filled();
            s.common.shadingShift = 0.3f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);

            // 個別にした時点の共通の値を写す (見た目を変えずに個別へ切り替える)
            Assert.Equal(0.3f, s.face.shadingShift);

            s.face.shadingShift = 0.2f;
            s.common.shadingShift = 0.4f;

            Assert.Equal(0.2f, s.Resolve(PartGroup.Face).shadingShift);
            Assert.Equal(0.4f, s.Resolve(PartGroup.Skin).shadingShift);
            Assert.True(s.face.IsOverridden(GroupSettingKeys.ShadingShift));
        }

        [Fact]
        public void 個別を外すと共通の値に戻る()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);
            s.face.shadingShift = 0.2f;

            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, false);

            Assert.Equal(s.common.shadingShift, s.Resolve(PartGroup.Face).shadingShift);
            Assert.Empty(s.face.overrides);
        }

        [Fact]
        public void 個別にするのを繰り返してもキーは1つ()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);
            s.face.shadingShift = 0.2f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);

            Assert.Single(s.face.overrides);
            // 既に個別なら共通の値で上書きしない
            Assert.Equal(0.2f, s.face.shadingShift);
        }

        [Fact]
        public void 個別にした色は共通と別の色になる()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadeColor, true);
            s.hair.shadeColor.r = 0.1f;

            Assert.NotSame(s.common.shadeColor, s.hair.shadeColor);
            Assert.Equal(1f, s.common.shadeColor.r);
            Assert.Equal(1f, s.Resolve(PartGroup.Skin).shadeColor.r);
            Assert.Equal(0.1f, s.Resolve(PartGroup.Hair).shadeColor.r);
        }

        [Fact]
        public void リム色は上書きの有無と色をまとめて継承する()
        {
            var s = Filled();
            s.common.rimColorOverride = true;
            s.common.rimColor.r = 1f;

            Assert.True(s.Resolve(PartGroup.Hair).rimColorOverride);
            Assert.Equal(1f, s.Resolve(PartGroup.Hair).rimColor.r);

            s.SetOverride(PartGroup.Hair, GroupSettingKeys.RimColor, true);
            s.hair.rimColorOverride = false;

            Assert.False(s.Resolve(PartGroup.Hair).rimColorOverride);
            Assert.True(s.Resolve(PartGroup.Skin).rimColorOverride);
        }

        [Fact]
        public void 全項目を個別にすると共通をどう変えてもグループの値のまま()
        {
            var s = Filled();
            var def = GroupSettings.CreateDefault();
            foreach (var key in GroupSettingKeys.All)
            {
                s.SetOverride(PartGroup.Costume, key, true);
            }

            ChangeAll(s.common);

            foreach (var key in GroupSettingKeys.All)
            {
                // ChangeAll が全項目を変えていること (変え漏れがあると下の確認が空振りする)
                Assert.False(s.common.FieldEquals(key, def), key);
                Assert.True(s.Resolve(PartGroup.Costume).FieldEquals(key, def), key);
                Assert.True(s.Resolve(PartGroup.Skin).FieldEquals(key, s.common), key);
            }
        }

        [Fact]
        public void 共通に戻すと全項目の個別が外れる()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadingShift, true);
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineColor, true);

            s.ClearOverrides(PartGroup.Hair);

            Assert.Empty(s.hair.overrides);
        }

        [Fact]
        public void グループのONOFFは継承しない()
        {
            var s = Filled();
            s.common.enabled = false;

            Assert.True(s.Resolve(PartGroup.Hair).enabled);

            s.hair.enabled = false;

            Assert.False(s.Resolve(PartGroup.Hair).enabled);
        }

        [Fact]
        public void 版2で既定と違う項目だけ個別として引き継ぐ()
        {
            var path = Path.Combine(_dir, "v2.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><enabled>true</enabled>" +
                "<skin><shadingShift>-0.05</shadingShift></skin>" +
                "<face><shadingShift>0.2</shadingShift><rimColorOverride>true</rimColorOverride></face>" +
                "</MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(MaidSettings.CurrentVersion, s.version);
            Assert.Equal(new[] { GroupSettingKeys.ShadingShift, GroupSettingKeys.RimColor }, s.face.overrides);
            Assert.Empty(s.skin.overrides);
            Assert.Equal(0.2f, s.Resolve(PartGroup.Face).shadingShift);
            Assert.True(s.Resolve(PartGroup.Face).rimColorOverride);
            Assert.Equal(-0.05f, s.common.shadingShift);
            Assert.Equal(-0.05f, s.Resolve(PartGroup.Skin).shadingShift);
        }

        [Fact]
        public void 版1の肌の旧既定の影色は個別にしない()
        {
            var path = Path.Combine(_dir, "v1.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"1\">" +
                "<skin><shadeColor r=\"1\" g=\"0.93\" b=\"0.93\" a=\"1\" /></skin></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(s.skin.overrides);
            Assert.Equal(1f, s.Resolve(PartGroup.Skin).shadeColor.g);
        }

        [Fact]
        public void 共通と個別を保存して読み直せる()
        {
            var path = Path.Combine(_dir, "v3.xml");
            var s = Filled();
            s.common.shadingShift = 0.3f;
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineWidth, true);
            s.hair.outlineWidth = 0.004f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(0.3f, loaded.common.shadingShift);
            Assert.Equal(new[] { GroupSettingKeys.OutlineWidth }, loaded.hair.overrides);
            Assert.Equal(0.004f, loaded.Resolve(PartGroup.Hair).outlineWidth);
            Assert.Equal(0.3f, loaded.Resolve(PartGroup.Hair).shadingShift);
            Assert.Empty(loaded.common.overrides);
        }

        [Fact]
        public void 版2で上書きしないリム色と線の色は色が違っても個別にしない()
        {
            var path = Path.Combine(_dir, "v2c.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\">" +
                "<hair><rimColorOverride>false</rimColorOverride><rimColor r=\"1\" g=\"0\" b=\"0\" a=\"1\" />" +
                "<outlineColorOverride>false</outlineColorOverride><outlineColor r=\"0\" g=\"1\" b=\"0\" a=\"1\" /></hair>" +
                "</MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(s.hair.overrides);
        }

        [Fact]
        public void 個別を外してから再び個別にすると共通の値から始まる()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);
            s.face.shadingShift = 0.2f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, false);
            s.common.shadingShift = 0.6f;

            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);

            Assert.Equal(0.6f, s.face.shadingShift);
        }

        [Fact]
        public void 重複したキーも個別を外せる()
        {
            var s = Filled();
            s.face.overrides.Add(GroupSettingKeys.ShadingShift);
            s.face.overrides.Add(GroupSettingKeys.ShadingShift);

            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, false);

            Assert.Empty(s.face.overrides);
        }

        [Fact]
        public void 解決の結果はグループごとに使い回し共通の変更を次の解決で拾う()
        {
            var s = Filled();
            var first = s.Resolve(PartGroup.Hair);
            s.common.shadingShift = 0.5f;
            var second = s.Resolve(PartGroup.Hair);

            Assert.Same(first, second);
            Assert.NotSame(first, s.Resolve(PartGroup.Skin));
            Assert.Equal(0.5f, second.shadingShift);
        }

        [Fact]
        public void 版3では個別でないグループの値は使わない()
        {
            var path = Path.Combine(_dir, "v3b.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\">" +
                "<common><shadingShift>0.1</shadingShift></common>" +
                "<face><shadingShift>0.7</shadingShift></face></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(s.face.overrides);
            Assert.Equal(0.1f, s.Resolve(PartGroup.Face).shadingShift);
        }
    }
}
```

`MaidSettingsStoreTests.cs` の `GroupSettings.CreateDefault(PartGroup.Skin)` 2 か所（`Assert.Equal(GroupSettings.CreateDefault(PartGroup.Skin).shadeColor.g, ...)` と `Assert.Equal(1f, GroupSettings.CreateDefault(PartGroup.Skin).shadeColor.g)`）を `GroupSettings.CreateDefault()` に置き換える。

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`GroupSettingKeys` / `common` / `Resolve` / `SetOverride` / `ClearOverrides` / `overrides` / `CreateDefault()` が無い）

- [ ] **Step 3: キーを足す**

`source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// 共通から継承する単位のキー。GroupSettings.overrides に入り、設定ファイルにも書くため値を変えない。
    /// リム色と線の色は「上書きするか」と色を 1 つの単位にする (片方だけ継承すると意味が崩れる)
    /// </summary>
    public static class GroupSettingKeys
    {
        public const string ShadeColor = "shadeColor";
        public const string ShadingShift = "shadingShift";
        public const string ShadingToony = "shadingToony";
        public const string GiEqualization = "giEqualization";
        public const string HighlightScale = "highlightScale";
        public const string RimColor = "rimColor";
        public const string RimFresnelPower = "rimFresnelPower";
        public const string RimLift = "rimLift";
        public const string RimLightingMix = "rimLightingMix";
        public const string OutlineEnabled = "outlineEnabled";
        public const string OutlineWidth = "outlineWidth";
        public const string OutlineLightingMix = "outlineLightingMix";
        public const string OutlineColor = "outlineColor";

        public static readonly string[] All =
        {
            ShadeColor, ShadingShift, ShadingToony, GiEqualization, HighlightScale,
            RimColor, RimFresnelPower, RimLift, RimLightingMix,
            OutlineEnabled, OutlineWidth, OutlineLightingMix, OutlineColor,
        };
    }
}
```

- [ ] **Step 4: GroupSettings を変える**

`Settings/GroupSettings.cs` の先頭に `using System;` と `using System.Collections.Generic;` を足す。クラスの summary を次へ置き換える:

```csharp
    /// <summary>
    /// 部位グループ 1 つ分、または共通の調整値。フィールド初期値が XML に欠けた項目の既定値になる。
    /// 参照型 (色) は null のまま読み、MaidSettings.FillMissing で補う
    /// </summary>
```

`public ColorData outlineColor;` の直後に追加:

```csharp

        // 個別にした項目 (GroupSettingKeys)。無い項目は MaidSettings.common の値を使う。common 自身では使わない
        public List<string> overrides = new List<string>();
```

`CreateDefault(PartGroup group)` と `FillMissing(PartGroup group)` を次へ置き換え（全グループで既定が同じになったため引数を外す）、その後ろにメソッドを足す:

```csharp
        public static GroupSettings CreateDefault()
        {
            var s = new GroupSettings();
            s.FillMissing();
            return s;
        }

        /// <summary>null の参照型を既定で埋める</summary>
        public void FillMissing()
        {
            if (shadeColor == null)
            {
                // 影の色味は Toony のランプから自動で入るため、ここは掛ける倍率として白を既定にする
                shadeColor = new ColorData(1f, 1f, 1f, 1f);
            }
            if (rimColor == null)
            {
                rimColor = new ColorData(0f, 0f, 0f, 1f);
            }
            if (outlineColor == null)
            {
                outlineColor = new ColorData(0f, 0f, 0f, 1f);
            }
            if (overrides == null)
            {
                overrides = new List<string>();
            }
        }

        public bool IsOverridden(string key)
        {
            return overrides.Contains(key);
        }

        /// <summary>key の項目を from から写す。色は参照を共有しないよう成分を写す (ウィンドウが成分を書き換えるため)</summary>
        public void CopyField(string key, GroupSettings from)
        {
            switch (key)
            {
                case GroupSettingKeys.ShadeColor: CopyColor(from.shadeColor, shadeColor); break;
                case GroupSettingKeys.ShadingShift: shadingShift = from.shadingShift; break;
                case GroupSettingKeys.ShadingToony: shadingToony = from.shadingToony; break;
                case GroupSettingKeys.GiEqualization: giEqualization = from.giEqualization; break;
                case GroupSettingKeys.HighlightScale: highlightScale = from.highlightScale; break;
                case GroupSettingKeys.RimColor:
                    rimColorOverride = from.rimColorOverride;
                    CopyColor(from.rimColor, rimColor);
                    break;
                case GroupSettingKeys.RimFresnelPower: rimFresnelPower = from.rimFresnelPower; break;
                case GroupSettingKeys.RimLift: rimLift = from.rimLift; break;
                case GroupSettingKeys.RimLightingMix: rimLightingMix = from.rimLightingMix; break;
                case GroupSettingKeys.OutlineEnabled: outlineEnabled = from.outlineEnabled; break;
                case GroupSettingKeys.OutlineWidth: outlineWidth = from.outlineWidth; break;
                case GroupSettingKeys.OutlineLightingMix: outlineLightingMix = from.outlineLightingMix; break;
                case GroupSettingKeys.OutlineColor:
                    outlineColorOverride = from.outlineColorOverride;
                    CopyColor(from.outlineColor, outlineColor);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }
        }

        public bool FieldEquals(string key, GroupSettings other)
        {
            switch (key)
            {
                case GroupSettingKeys.ShadeColor: return ColorEquals(shadeColor, other.shadeColor);
                case GroupSettingKeys.ShadingShift: return FloatEquals(shadingShift, other.shadingShift);
                case GroupSettingKeys.ShadingToony: return FloatEquals(shadingToony, other.shadingToony);
                case GroupSettingKeys.GiEqualization: return FloatEquals(giEqualization, other.giEqualization);
                case GroupSettingKeys.HighlightScale: return FloatEquals(highlightScale, other.highlightScale);
                // 上書きしないなら色は描画に使われないため比べない (版 2 からの移行で不要な個別を作らない)
                case GroupSettingKeys.RimColor:
                    return rimColorOverride == other.rimColorOverride
                        && (!rimColorOverride || ColorEquals(rimColor, other.rimColor));
                case GroupSettingKeys.RimFresnelPower: return FloatEquals(rimFresnelPower, other.rimFresnelPower);
                case GroupSettingKeys.RimLift: return FloatEquals(rimLift, other.rimLift);
                case GroupSettingKeys.RimLightingMix: return FloatEquals(rimLightingMix, other.rimLightingMix);
                case GroupSettingKeys.OutlineEnabled: return outlineEnabled == other.outlineEnabled;
                case GroupSettingKeys.OutlineWidth: return FloatEquals(outlineWidth, other.outlineWidth);
                case GroupSettingKeys.OutlineLightingMix: return FloatEquals(outlineLightingMix, other.outlineLightingMix);
                case GroupSettingKeys.OutlineColor:
                    return outlineColorOverride == other.outlineColorOverride
                        && (!outlineColorOverride || ColorEquals(outlineColor, other.outlineColor));
                default: throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }
        }

        private static void CopyColor(ColorData from, ColorData to)
        {
            to.r = from.r;
            to.g = from.g;
            to.b = from.b;
            to.a = from.a;
        }

        private static bool ColorEquals(ColorData a, ColorData b)
        {
            return FloatEquals(a.r, b.r) && FloatEquals(a.g, b.g) && FloatEquals(a.b, b.b) && FloatEquals(a.a, b.a);
        }

        private static bool FloatEquals(float a, float b)
        {
            // アウトライン幅 (0.0015 前後) を区別できる細かさにする
            const float epsilon = 1e-6f;
            return Math.Abs(a - b) < epsilon;
        }
```

- [ ] **Step 5: MaidSettings を変える**

`Settings/MaidSettings.cs`:

`public const int CurrentVersion = 2;` を `3` にし、`private const int SkinShadeWhiteVersion = 2;` の直後に追加:

```csharp
        // 共通タブ (common と overrides) を足した版。それより前の版はグループの値を個別として引き継ぐ
        private const int CommonVersion = 3;
```

`public GroupSettings costume;` の直後に追加:

```csharp
        // 共通の調整値。グループで個別にしていない項目はこの値で描く
        public GroupSettings common;
```

`public List<string> excludedSlots = new List<string>();` の直後に追加:

```csharp
        // Resolve の結果。マテリアルの走査ごとに呼ばれるため、グループごとに使い回す
        private readonly GroupSettings[] _resolved = new GroupSettings[Enum.GetValues(typeof(PartGroup)).Length];
```

`FillMissing()` を次へ置き換える:

```csharp
        /// <summary>欠けたグループ・共通と、それぞれの null の参照型を既定で埋める</summary>
        public void FillMissing()
        {
            foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
            {
                var g = Get(group);
                if (g == null)
                {
                    Set(group, GroupSettings.CreateDefault());
                }
                else
                {
                    g.FillMissing();
                }
            }
            if (common == null)
            {
                common = GroupSettings.CreateDefault();
            }
            else
            {
                common.FillMissing();
            }
            if (excludedSlots == null)
            {
                excludedSlots = new List<string>();
            }
        }

        /// <summary>
        /// マテリアルへ入れる値。項目ごとに、グループで個別ならグループの値、そうでなければ共通の値を使う。
        /// 返す実体は使い回すため、呼び出し側は保持も書き換えもしない
        /// </summary>
        public GroupSettings Resolve(PartGroup group)
        {
            var g = Get(group);
            var resolved = _resolved[(int)group];
            if (resolved == null)
            {
                resolved = GroupSettings.CreateDefault();
                _resolved[(int)group] = resolved;
            }
            resolved.enabled = g.enabled;
            foreach (var key in GroupSettingKeys.All)
            {
                resolved.CopyField(key, g.IsOverridden(key) ? g : common);
            }
            return resolved;
        }

        /// <summary>個別にするときは今の共通の値を写してから切り替え、見た目を変えない</summary>
        public void SetOverride(PartGroup group, string key, bool overridden)
        {
            var g = Get(group);
            if (overridden)
            {
                if (!g.IsOverridden(key))
                {
                    g.CopyField(key, common);
                    g.overrides.Add(key);
                }
            }
            else
            {
                // 手編集で重複したキーも残さない
                g.overrides.RemoveAll(k => k == key);
            }
        }

        public void ClearOverrides(PartGroup group)
        {
            Get(group).overrides.Clear();
        }
```

`Upgrade()` を次へ置き換える:

```csharp
        /// <summary>読み込んだ古い版の値を今の版へ合わせる。FillMissing より前に呼ぶ</summary>
        internal void Upgrade()
        {
            if (version < SkinShadeWhiteVersion && skin != null && IsLegacySkinShade(skin.shadeColor))
            {
                // null にして FillMissing の既定 (白) で埋める
                skin.shadeColor = null;
            }
            if (version < CommonVersion)
            {
                // 共通が無かった版。見た目を変えないよう、共通を既定にして既定と違う項目を個別にする
                FillMissing();
                common = GroupSettings.CreateDefault();
                foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
                {
                    var g = Get(group);
                    g.overrides.Clear();
                    foreach (var key in GroupSettingKeys.All)
                    {
                        if (!g.FieldEquals(key, common))
                        {
                            g.overrides.Add(key);
                        }
                    }
                }
            }
            version = CurrentVersion;
        }
```

- [ ] **Step 6: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（既存 59 件 + 新規 17 件 = 76 件）。その前に、プラグイン本体の `UI/MToonMaidWindow.cs` にある `GroupSettings.CreateDefault(group)`（コンストラクタの `_defaults` の初期化）と `GroupSettings.CreateDefault(_group)`（`ResetGroup`）の 2 か所を `GroupSettings.CreateDefault()` に直す。直さないとテストプロジェクトが参照する本体がコンパイルできない（ウィンドウは Task 3 で書き直す）

- [ ] **Step 7: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/Settings source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs source/COM3D25.MToonMaid.Plugin.Tests/MaidSettingsStoreTests.cs
git commit -m "feat: 共通の調整値と項目ごとの個別の上書きを設定に追加"
```

---

### Task 2: マテリアルへ入れる値を解決後の値にする

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs`, `source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs`

**Interfaces:**
- Consumes: `MaidSettings.Resolve(PartGroup) : GroupSettings`（Task 1）
- Produces: なし（挙動の変更のみ。`IsSlotActive` は `Get(...).enabled` のまま）

Material を扱うため単体テストは無い。Task 4 の実機手順で確かめる。

- [ ] **Step 1: Controller を変える**

`MaidMToonController.cs` の `Scan` の

```csharp
                    var active = settings.IsSlotActive(skin.Category);
```

の直後に追加:

```csharp
                    // 解決はスロットごとに 1 回でよい (マテリアルごとに呼ぶと 13 項目の写しを繰り返す)
                    var groupSettings = active ? settings.Resolve(group) : null;
```

同じく `Scan` の中の `ScanMaterial(_materialBuffer[k], skin, k, group, settings.Get(group));` を `ScanMaterial(_materialBuffer[k], skin, k, group, groupSettings);` に置き換える。

`RefreshRampsIfPartsColorChanged` の中の `RefreshRamp(entry, settings.Get(entry.backup.group));` を `RefreshRamp(entry, settings.Resolve(entry.backup.group));` に置き換える。

- [ ] **Step 2: プラグイン本体を変える**

`MToonMaidPlugin.cs` の `NotifyGroupChanged` の `controller.ApplyGroup(group, GetSettings(maid).Get(group));` を `controller.ApplyGroup(group, GetSettings(maid).Resolve(group));` に置き換える。

- [ ] **Step 3: 残りの呼び出しを確かめる**

Run: `grep -rn "\.Get(" source/COM3D25.MToonMaid.Plugin --include=*.cs | grep -v "/obj/" | grep -v "_store.Get"`
Expected: `Settings/MaidSettings.cs`（`FillMissing` / `IsSlotActive` / `Resolve` / `SetOverride` / `ClearOverrides` / `Upgrade` の中）と `UI/MToonMaidWindow.cs` だけ。Controller とプラグイン本体には残らない

- [ ] **Step 4: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（76 件）

- [ ] **Step 5: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs source/COM3D25.MToonMaid.Plugin/MToonMaidPlugin.cs
git commit -m "feat: マテリアルへ共通と個別を解決した値を入れる"
```

---

### Task 3: ウィンドウの余白・水平線・共通タブ・個別トグル

**Files:**
- Rewrite: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`

**Interfaces:**
- Consumes: `MaidSettings.common` / `Resolve` / `SetOverride` / `ClearOverrides`、`GroupSettings.IsOverridden` / `CreateDefault()`、`GroupSettingKeys.*`（Task 1）、`IMToonMaidHost`（既存。変更しない）、`DockableWindowBase.FRAME` / `HEADER_HEIGHT`、`GUIView.BeginHorizontal` / `EndLayout` / `DrawToggle(bool, float, float, Action<bool>)` / `DrawEmpty(float, float)` / `DrawHorizontalLine(Color)` / `AddSpace(float)`
- Produces: なし

UI のため単体テストは無い。ビルドと Task 4 の実機手順で確かめる。

レイアウトの要点:
- 内容のビューは `(FRAME, HEADER_HEIGHT, 幅 - FRAME * 2, 高さ - HEADER_HEIGHT - FRAME)` で初期化する。`DockableWindowBase.contentRect` と同じ式だが、`OnSizeChanged` で渡された新しいサイズを使うため自前で計算する
- 個別のトグルは `BeginHorizontal` → `DrawToggle(20 幅)` → 行の本体（幅 -1 = 残り幅）→ `EndLayout`。`DrawColor` は内部で `BeginLayout` / `EndLayout` を呼ぶが、`BeginLayout` は x を戻さないのでトグルの右から描かれ、`EndLayout` の重複は無害
- リム色・線の色の色の行は、直前の「上書きする」の行と同じキーなので、トグルの代わりに同じ幅の空き（`DrawEmpty`）を置いて揃える
- 共通タブではトグルを描かない。スライダーの行は全タブで同じ順に描く（入力欄キャッシュの番号を揃える）

- [ ] **Step 1: ウィンドウを書き直す**

`UI/MToonMaidWindow.cs` を次の内容で置き換える:

```csharp
using System;
using System.Collections.Generic;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>メイドの選択・ON/OFF・共通と部位グループごとの調整値・対象スロットのウィンドウ</summary>
    public sealed class MToonMaidWindow : DockableWindowBase
    {
        public const int DefaultWidth = 380;
        public const int DefaultHeight = 640;

        private const int WindowId = 0x4D546F6E; // "MTon"
        private const float RowHeight = 20f;
        private const float LabelWidth = 110f;
        private const float TabWidth = 60f;
        private const float ResetButtonWidth = 200f;
        private const float OverrideToggleWidth = 20f;
        // SceneEditor が内容の先頭の水平線の下に空けている量
        private const float ScrollTopSpace = 5f;
        // 0 番が共通、1 番以降が PartGroup の順
        private const int CommonTab = 0;

        private static readonly string[] TabLabels = { "共通", "肌", "顔", "髪", "衣装" };

        private readonly IMToonMaidHost _host;
        private readonly WindowPlacementConfig _placement;
        private readonly GUIView _rootView = new GUIView();
        private readonly GUIView _contentView = new GUIView();
        private readonly GUIComboBox<Maid> _maidComboBox = new GUIComboBox<Maid>();
        // スライダーと色のリセット先
        private readonly GroupSettings _default = GroupSettings.CreateDefault();
        private int _width = DefaultWidth;
        private int _height = DefaultHeight;
        private int _tab = CommonTab;
        // 描いている最中の対象。null のグループは共通タブ
        private MaidSettings _settings;
        private PartGroup? _editGroup;
        // 描画中の変更は描画の後でまとめて本体へ伝える
        private bool _groupChanged;
        private bool _targetsChanged;
        private bool? _enabledRequest;
        // コンボボックスのポップアップ中は全体を無効にする。行単位の無効表示の後はこの値へ戻す
        private bool _baseEnabled = true;

        protected override int windowId => WindowId;
        protected override string windowTitle => "MToonMaid";
        // 色の行 (個別のトグル・ラベル・見本・HEX・R・編集) がスクロールバーと重ならない幅
        protected override int minWidth => 380;
        protected override int minHeight => 320;

        public MToonMaidWindow(IMToonMaidHost host, WindowPlacementConfig placement)
        {
            _host = host;
            _placement = placement;
            windowIndex = 0;
            _maidComboBox.getName = (maid, index) => maid != null && maid.status != null ? maid.status.fullNameJpStyle : "";
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
            // SceneEditor の窓と同じく、左右と下に枠の幅を空けた内容領域 (contentRect と同じ式) に GUIView 既定のパディングで描く
            _contentView.Init(FRAME, HEADER_HEIGHT, _width - FRAME * 2, _height - HEADER_HEIGHT - FRAME);
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

            _settings = _host.GetSettings(maid);
            view.DrawToggle("MToon にする", _settings.enabled, -1, RowHeight, v => _enabledRequest = v);
            view.DrawLabel("変換済みマテリアル: " + _host.GetConvertedCount(maid), -1, RowHeight);
            view.DrawHorizontalLine();

            // string[] のままだと汎用版 DrawTabs<T>(T currentTab, ...) が選ばれるため IList<string> で渡す
            _tab = view.DrawTabs((IList<string>)TabLabels, _tab, TabWidth, RowHeight);
            _editGroup = _tab == CommonTab ? (PartGroup?)null : (PartGroup)(_tab - 1);
            if (_editGroup.HasValue)
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
            DrawSettings(view);
            DrawResetButton(view);
            if (_editGroup.HasValue)
            {
                // スロットの数はメイドや着替えで変わるため、入力欄キャッシュの番号がずれないようスライダーより後に描く
                DrawSlots(view, maid, _editGroup.Value);
            }
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

        /// <summary>共通タブは共通の値を、グループのタブは解決後の値を出す (どの行もタブ間で同じ順に描く)</summary>
        private void DrawSettings(GUIView view)
        {
            var shown = _editGroup.HasValue ? _settings.Resolve(_editGroup.Value) : _settings.common;
            // スライダーと色の「R」の戻し先。グループのタブでは共通の値 (個別のまま共通と同じ値になる)
            var def = _editGroup.HasValue ? _settings.common : _default;
            var prefix = TabLabels[_tab] + "/";

            view.DrawLabel("陰影", -1, RowHeight);
            ColorRow(view, GroupSettingKeys.ShadeColor, true, prefix + "影色", "影色", shown.shadeColor, def.shadeColor, true, s => s.shadeColor);
            Slider(view, GroupSettingKeys.ShadingShift, "境界の位置", shown.shadingShift, -1f, 1f, def.shadingShift, (s, v) => s.shadingShift = v);
            Slider(view, GroupSettingKeys.ShadingToony, "境界の硬さ", shown.shadingToony, 0f, 1f, def.shadingToony, (s, v) => s.shadingToony = v);
            Slider(view, GroupSettingKeys.GiEqualization, "環境光の均一化", shown.giEqualization, 0f, 1f, def.giEqualization, (s, v) => s.giEqualization = v);

            view.DrawLabel("天使の輪", -1, RowHeight);
            Slider(view, GroupSettingKeys.HighlightScale, "強さ", shown.highlightScale, 0f, 2f, def.highlightScale, (s, v) => s.highlightScale = v);

            view.DrawLabel("リム", -1, RowHeight);
            Toggle(view, GroupSettingKeys.RimColor, "色を上書きする", shown.rimColorOverride, (s, v) => s.rimColorOverride = v);
            // 上書きしないときも行は隠さず無効表示にする (行の増減で入力欄キャッシュの番号がずれる)。
            // 個別のトグルは直前の行と同じキーなので出さない
            ColorRow(view, GroupSettingKeys.RimColor, false, prefix + "リム色", "リム色", shown.rimColor, def.rimColor, shown.rimColorOverride, s => s.rimColor);
            Slider(view, GroupSettingKeys.RimFresnelPower, "鋭さ", shown.rimFresnelPower, 0f, 100f, def.rimFresnelPower, (s, v) => s.rimFresnelPower = v);
            Slider(view, GroupSettingKeys.RimLift, "持ち上げ", shown.rimLift, 0f, 1f, def.rimLift, (s, v) => s.rimLift = v);
            Slider(view, GroupSettingKeys.RimLightingMix, "ライト色の影響", shown.rimLightingMix, 0f, 1f, def.rimLightingMix, (s, v) => s.rimLightingMix = v);

            view.DrawLabel("アウトライン", -1, RowHeight);
            Toggle(view, GroupSettingKeys.OutlineEnabled, "アウトラインを描く", shown.outlineEnabled, (s, v) => s.outlineEnabled = v);
            // 既定の書式 (小数 2 桁) では 0.0015 が 0.00 に見えるため 4 桁で出す
            Slider(view, GroupSettingKeys.OutlineWidth, "幅", shown.outlineWidth, 0f, 0.01f, def.outlineWidth, (s, v) => s.outlineWidth = v, FloatFieldType.F4);
            Slider(view, GroupSettingKeys.OutlineLightingMix, "ライト色の影響", shown.outlineLightingMix, 0f, 1f, def.outlineLightingMix, (s, v) => s.outlineLightingMix = v);
            Toggle(view, GroupSettingKeys.OutlineColor, "線の色を上書きする", shown.outlineColorOverride, (s, v) => s.outlineColorOverride = v);
            ColorRow(view, GroupSettingKeys.OutlineColor, false, prefix + "線の色", "線の色", shown.outlineColor, def.outlineColor, shown.outlineColorOverride, s => s.outlineColor);
        }

        private void DrawResetButton(GUIView view)
        {
            if (!_editGroup.HasValue)
            {
                if (view.DrawButton("共通の調整値を既定に戻す", ResetButtonWidth, RowHeight))
                {
                    _settings.common = GroupSettings.CreateDefault();
                    _groupChanged = true;
                }
                return;
            }
            if (view.DrawButton("このグループを全て共通に戻す", ResetButtonWidth, RowHeight))
            {
                _settings.ClearOverrides(_editGroup.Value);
                _groupChanged = true;
            }
        }

        private void DrawSlots(GUIView view, Maid maid, PartGroup group)
        {
            var settings = _settings;
            var groupEnabled = settings.Get(group).enabled;
            view.DrawHorizontalLine();
            view.DrawLabel("MToon にするスロット", -1, RowHeight);
            foreach (var slot in _host.GetSlotCategories(maid, group))
            {
                // グループが OFF の間は選べないよう無効表示にする
                view.DrawToggle(slot, !settings.excludedSlots.Contains(slot), -1, RowHeight, groupEnabled && _baseEnabled, v =>
                {
                    settings.SetSlotExcluded(slot, !v);
                    _targetsChanged = true;
                });
            }
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
                if (_editGroup.HasValue)
                {
                    _host.NotifyGroupChanged(maid, _editGroup.Value);
                }
                else
                {
                    // 共通は個別にしていない全グループに効く
                    foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
                    {
                        _host.NotifyGroupChanged(maid, group);
                    }
                }
            }
        }

        /// <summary>
        /// 値を書く先。共通タブは共通、グループのタブはその項目を個別にしたうえでグループ
        /// (個別にするとき今の共通の値を写すので、編集は見えていた値から当たる)
        /// </summary>
        private GroupSettings BeginEdit(string key)
        {
            if (!_editGroup.HasValue)
            {
                return _settings.common;
            }
            var group = _editGroup.Value;
            _settings.SetOverride(group, key, true);
            return _settings.Get(group);
        }

        /// <summary>グループのタブでは行の先頭に個別のトグル (showToggle が false なら同じ幅の空き) を置く</summary>
        private void BeginRow(GUIView view, string key, bool showToggle)
        {
            if (!_editGroup.HasValue)
            {
                return;
            }
            view.BeginHorizontal();
            if (!showToggle)
            {
                view.DrawEmpty(OverrideToggleWidth, RowHeight);
                return;
            }
            var group = _editGroup.Value;
            view.DrawToggle(_settings.Get(group).IsOverridden(key), OverrideToggleWidth, RowHeight, v =>
            {
                _settings.SetOverride(group, key, v);
                _groupChanged = true;
            });
        }

        private void EndRow(GUIView view)
        {
            if (_editGroup.HasValue)
            {
                view.EndLayout();
            }
        }

        private void Slider(GUIView view, string key, string label, float value, float min, float max, float defaultValue,
            Action<GroupSettings, float> set, FloatFieldType fieldType = FloatFieldType.Float)
        {
            BeginRow(view, key, true);
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
                    set(BeginEdit(key), v);
                    _groupChanged = true;
                },
            });
            EndRow(view);
        }

        private void Toggle(GUIView view, string key, string label, bool value, Action<GroupSettings, bool> set)
        {
            BeginRow(view, key, true);
            view.DrawToggle(label, value, -1, RowHeight, v =>
            {
                set(BeginEdit(key), v);
                _groupChanged = true;
            });
            EndRow(view);
        }

        /// <summary>
        /// colorKey はカラーピッカーの同定にも使うため、タブ間で重複させない。
        /// 行の全要素 (編集ボタンを含む) を無効にするため BeginEnabled ではなく SetEnabled を使う
        /// (BeginEnabled は直後の 1 要素にしか効かない)。
        /// 書くときは BeginEdit の先の色を取り直す (共通を使う行で見えている色は共通の色のため)
        /// </summary>
        private void ColorRow(GUIView view, string key, bool showToggle, string colorKey, string label, ColorData color,
            ColorData defaultColor, bool editable, Func<GroupSettings, ColorData> target)
        {
            BeginRow(view, key, showToggle);
            view.SetEnabled(_baseEnabled && editable);
            view.DrawColor(view.GetColorFieldCache(colorKey, false), ToColor(color), ToColor(defaultColor), c =>
            {
                var dest = target(BeginEdit(key));
                dest.r = c.r;
                dest.g = c.g;
                dest.b = c.b;
                _groupChanged = true;
            }, label);
            view.SetEnabled(_baseEnabled);
            EndRow(view);
        }

        private static Color ToColor(ColorData c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
    }
}
```

- [ ] **Step 2: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（76 件）。`grep -n "GroupLabels\|_defaults\|ResetGroup" source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs` が 0 件

- [ ] **Step 3: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs
git commit -m "feat: 共通タブと項目ごとの個別トグルを追加し、余白を SceneEditor にそろえる"
```

---

### Task 4: 実機検証と仕上げ

ゲームが DLL を掴んでいるため、**restart-verify スキル**（`com3d25-devbridge:restart-verify`）の手順で、ゲーム終了 → `debug.bat` で DLL を配置 → 起動 → 最新セーブのロード → デイリー画面で検証する。eval では `UnityEngine` の型を完全修飾名で書き、ローカル関数は使わず `System.Func` で書く。検証で変えた状態（メイドの「MToon にする」、共通・個別の値）は最後に検証前へ戻す。

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-10-09-mtoon-maid-common-tab-design.md`（検証の結果）

- [ ] **Step 1: DLL を配置して起動し、セーブをロードする**

restart-verify スキルに従う。起動後の `LogOutput.log` に `MToonMaid 有効化` があり、`ウィンドウを作れない` が無いこと。

- [ ] **Step 2: 準備の eval を流す**

```csharp
var maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
var plugin = UnityEngine.Object.FindObjectOfType<COM3D25.MToonMaid.Plugin.MToonMaidPlugin>();
System.Func<string, UnityEngine.Material> MatOf = prefix => { var sl = maid0.body0.goSlot;
  for (int i = 0; i < sl.Count; i++) for (int j = 0; j < sl.CountChildren(i); j++) { var sk = sl[i, j]; if (sk == null || sk.obj == null || !sk.Category.StartsWith(prefix)) continue;
    foreach (var r in sk.obj.GetComponentsInChildren<UnityEngine.Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null) return m; }
  return null; };
var s = plugin.GetSettings(maid0); var wasEnabled = s.enabled; plugin.SetEnabled(maid0, true);
// 検証で書き換える値を控える。利用者の個別 (版 2 から移行したものを含む) を最後に元へ戻すため
var savedCommonShift = s.common.shadingShift; var savedFaceShift = s.face.shadingShift;
var savedFaceOverrides = new System.Collections.Generic.List<string>(s.face.overrides);
var shiftKey = COM3D25.MToonMaid.Plugin.GroupSettingKeys.ShadingShift;
"version=" + s.version + " wasEnabled=" + wasEnabled + " overrides skin/face/hair/costume=" + string.Join(",", s.skin.overrides) + "/" + string.Join(",", s.face.overrides) + "/" + string.Join(",", s.hair.overrides) + "/" + string.Join(",", s.costume.overrides)
```

Expected: `version=3`。版 2 の設定ファイルから上がったメイドなら、既定のまま使っていたグループの個別は空。`shadingShift` を個別にしているグループがあれば、Step 4 ではそのグループを期待値の対象から外す（ledger に記録する）

- [ ] **Step 3: 版 3 で保存されることを確かめる**

`BepInEx/config/COM3D25.MToonMaid/<GUID>.xml`（GUID は `maid0.status.guid`）を開き、`version="3"`・`<common>`・各グループの `<overrides />` があることを確かめる（Step 2 の `SetEnabled` で 1 秒後に保存されている）

- [ ] **Step 4: 共通の変更が全グループへ即時に効くことを確かめる**

```csharp
s.common.shadingShift = 0.4f;
foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g);
"body=" + MatOf("body").GetFloat("_ShadingShiftFactor") + " hair=" + MatOf("hairF").GetFloat("_ShadingShiftFactor") + " onepiece=" + MatOf("onepiece").GetFloat("_ShadingShiftFactor")
```

Expected: `shadingShift` を個別にしていないグループは `0.4`（`onepiece` を着ていないメイドなら、着ている衣装のスロット名に替える）

- [ ] **Step 5: 個別にしたグループだけ共通の変更が効かないことを確かめる**

顔（`head` スロット）で確かめる。Step 2 で顔が既に `shadingShift` を個別にしていた場合は、個別にしていない別のグループ（スロット名も合わせて替える）を使い、ledger に記録する。

```csharp
s.face.overrides.Remove(shiftKey);
s.SetOverride(COM3D25.MToonMaid.Plugin.PartGroup.Face, shiftKey, true);
var faceCopied = s.face.shadingShift; s.face.shadingShift = -0.3f; s.common.shadingShift = 0.2f;
foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g);
"copied=" + faceCopied + " body=" + MatOf("body").GetFloat("_ShadingShiftFactor") + " head=" + MatOf("head").GetFloat("_ShadingShiftFactor") + " headShader=" + MatOf("head").shader.name
```

Expected: `copied=0.4`（個別にした時点の共通の値）、`body=0.2`（肌が個別でなければ）、`head=-0.3`。`MatOf("head")` が Toony のまま（顔の先頭のマテリアルが変換対象外）なら、`head` スロットの MToon のマテリアルを探して読む

Step 2 で控えた値へ戻す（利用者の個別を消さない）:

```csharp
s.face.overrides.Clear(); s.face.overrides.AddRange(savedFaceOverrides); s.face.shadingShift = savedFaceShift; s.common.shadingShift = savedCommonShift;
foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g);
"face overrides=" + string.Join(",", s.face.overrides) + " common shift=" + s.common.shadingShift
```

Expected: Step 2 の顔の個別と共通の `shadingShift` に戻っている

- [ ] **Step 6: ウィンドウを確かめる**

```csharp
var wm = (COM3D25.MToonMaid.Plugin.WindowManager)typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).GetField("_windowManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
var tabField = typeof(COM3D25.MToonMaid.Plugin.MToonMaidWindow).GetField("_tab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
wm.MainWindow.isShowWnd = true; "shown"
```

`screenshot`（`window: "MToonMaid"`）で次を確かめる:
- 内容が左右に余白を持ち（SceneEditor のウィンドウと同じ見え方）、タブの下（グループのタブでは「このグループを MToon にする」の下）に灰色の水平線と空きがあってからスクロールエリアが始まる
- 共通タブ（`_tab` = 0）: タブが「共通・肌・顔・髪・衣装」の 5 つ。個別のトグルが無く、「このグループを MToon にする」とスロット一覧が無い。ボタンは「共通の調整値を既定に戻す」
- 髪タブ（`tabField.SetValue(wm.MainWindow, 3)`）: 各行の先頭に個別のトグルがあり、リム色・線の色の色の行はトグルの位置が空きで揃っている。色の行がスクロールバーと重ならない。ボタンは「このグループを全て共通に戻す」、その下にスロット一覧

`tail_log` に `ArgumentException`（GUILayout のコントロール数の不一致）や `COM3D25.MToonMaid` を含む例外が無いこと。ウィンドウを最小幅（380）にした見え方は、`wm.MainWindow` の位置とサイズを変えずに確かめられないため、利用者に確認してもらう。マウス操作（行の編集で個別のトグルが入ること）も利用者に確認してもらう。確かめたら `tabField.SetValue(wm.MainWindow, 0)`、`isShowWnd = false`、`plugin.SetEnabled(maid0, wasEnabled)` で戻す

- [ ] **Step 7: README と spec を更新する**

`README.md` の「使い方」の 3 と 4 を次へ置き換える:

```markdown
3. 「共通」タブで全部位の影色・境界・天使の輪・リム・アウトラインをまとめて調整します
4. 肌・顔・髪・衣装のタブでは、行の先頭のチェックを入れた項目だけがその部位の個別の値になります（チェックの無い項目は共通の値）。行を編集するとチェックが入ります。「このグループを MToon にする」で部位ごとに置き換えを切り替えられ、タブの下のスロット一覧で個別のスロット（帽子など）だけを外すこともできます
```

`docs/superpowers/specs/2026-10-09-mtoon-maid-common-tab-design.md` の末尾に「## 実機検証の結果」を足し、Step 2〜6 で得た値と、確認できなかった項目（最小幅の見え方、マウス操作）を書く。

- [ ] **Step 8: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-09-mtoon-maid-common-tab-design.md
git commit -m "docs: 共通タブの使い方と実機検証の結果を追加"
```

## レビュー却下メモ

- 「Scan の Resolve をグループごと 1 回に寄せる」 — スロットごとに 13 項目の写しで、走査間隔（既定 10 フレーム）があり負荷は無視できる
