# COM3D25.MToonMaid.Plugin アウトラインの幅を画面基準にする Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** アウトラインの幅の基準をワールド（m）と画面（画面の高さに対する割合）から選べるようにする。

**Architecture:** シェーダーは既に `_MTOON_OUTLINE_SCREEN` の変種を持つため変えない。グループ設定に基準（`OutlineWidthMode`）と画面基準の幅を足し、どの `_OutlineWidthMode` と幅を書くかは Unity に依存しない `OutlineParams.Mode` / `OutlineParams.Width` で決めて単体テストする。`MToonRenderState` が基準に合うキーワードを付け、`MaterialConverter.ApplyGroup` がそれを使って書く。UI はアウトライン節に「幅の基準」を足す。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md` の「改訂 2026-10-11: アウトラインの幅を画面基準にできるようにする」節

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`（`debug.bat` はゲーム停止中だと実機へコピーするため、反映したいときだけ使う）

## Global Constraints

- main で作業する。コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- 設定ファイルの版（`MaidSettings.CurrentVersion` = 4）は上げない
- `OutlineWidthMode` は設定ファイルに名前で書くため、`World` / `Screen` の名前を変えない
- 既定: 基準 `World`、`outlineWidth` 0.0015（従来どおり）、`outlineScreenWidth` 0.002
- `_OutlineWidthMode`: 0 = なし、1 = World、2 = Screen（UniVRM の `MToon10OutlineMode` と同じ数値）
- シェーダー・バンドルは変えない

## Review Focus

- **要素の無い既存ファイル**: World・0.002 で読み、個別にならない（見た目が変わらない）→ Task 1 Step 1 のテスト
- **基準だけ個別にしたグループ**: 共通の幅のうち、そのグループの基準に合う方を使う → Task 1 Step 1 のテスト
- **線を描かない（ゲームのシェーダーに線が無い・OFF）**: 基準が Screen でもどちらのキーワードも外れる → Task 2 Step 1 のテスト
- **基準を切り替えたときのスライダー**: 今の基準の値を出し、[R] は今の基準の既定（グループのタブでは共通の値）へ戻す。もう一方の値は書き換えない → Task 3 実機
- **ゲームが Toony へ戻して再変換・OFF で戻す**: `_MTOON_OUTLINE_SCREEN` も `StripOwnKeywords` の対象なので残らない（既存テストの範囲）→ Task 3 実機で OFF/ON を往復する

---

### Task 1: 設定に幅の基準と画面基準の幅を足す

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Settings/OutlineWidthMode.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`（アウトラインのフィールド、`CopyField`、`FieldEquals`）
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`（csproj は SDK 形式で `*.cs` を自動で含むため編集しない）

**Interfaces:**
- Produces: `enum OutlineWidthMode { World, Screen }`、`GroupSettings.outlineWidthMode`（既定 `World`）、`GroupSettings.outlineScreenWidth`（float、既定 0.002f）、`GroupSettingKeys.OutlineWidthMode = "outlineWidthMode"`。`GroupSettingKeys.OutlineWidth` は `outlineWidth` と `outlineScreenWidth` の両方を写す・比べる

- [ ] **Step 1: 失敗するテストを書く** — `CommonSettingsTests.ChangeAll` の `g.outlineWidth = 0.005f;` の後に:

```csharp
            g.outlineWidthMode = OutlineWidthMode.Screen;
            g.outlineScreenWidth = 0.006f;
```

`影を受ける落とすは既定で…` の前に次を足す:

```csharp
        [Fact]
        public void 幅の基準は既定でワールドで要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.OutlineWidthMode, GroupSettingKeys.All);
            var def = GroupSettings.CreateDefault();
            Assert.Equal(OutlineWidthMode.World, def.outlineWidthMode);
            Assert.Equal(0.0015f, def.outlineWidth);
            Assert.Equal(0.002f, def.outlineScreenWidth);

            var path = Path.Combine(_dir, "v4outline.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"4\"><hair><overrides><string>shadingShift</string></overrides><shadingShift>0.2</shadingShift></hair></MToonMaid>");
            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s.hair.overrides);
            Assert.Equal(OutlineWidthMode.World, s.Resolve(PartGroup.Hair).outlineWidthMode);
            Assert.Equal(0.002f, s.Resolve(PartGroup.Hair).outlineScreenWidth);
        }

        [Fact]
        public void 幅は基準ごとの値をまとめて継承する()
        {
            var s = Filled();
            s.common.outlineWidth = 0.003f;
            s.common.outlineScreenWidth = 0.004f;
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineWidthMode, true);
            s.hair.outlineWidthMode = OutlineWidthMode.Screen;

            var hair = s.Resolve(PartGroup.Hair);
            Assert.Equal(OutlineWidthMode.Screen, hair.outlineWidthMode);
            Assert.Equal(0.004f, hair.outlineScreenWidth);
            Assert.Equal(OutlineWidthMode.World, s.Resolve(PartGroup.Skin).outlineWidthMode);

            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineWidth, true);
            Assert.Equal(0.003f, s.hair.outlineWidth);
            Assert.Equal(0.004f, s.hair.outlineScreenWidth);

            var other = GroupSettings.CreateDefault();
            other.outlineScreenWidth = 0.009f;
            Assert.False(GroupSettings.CreateDefault().FieldEquals(GroupSettingKeys.OutlineWidth, other));
        }

        [Fact]
        public void 幅の基準と画面基準の幅を保存して読み直せる()
        {
            var path = Path.Combine(_dir, "outline-screen.xml");
            var s = Filled();
            s.common.outlineWidthMode = OutlineWidthMode.Screen;
            s.common.outlineScreenWidth = 0.005f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(OutlineWidthMode.Screen, loaded.common.outlineWidthMode);
            Assert.Equal(0.005f, loaded.common.outlineScreenWidth);
            Assert.Equal(OutlineWidthMode.Screen, loaded.Resolve(PartGroup.Costume).outlineWidthMode);
        }
```

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: `OutlineWidthMode` / `outlineScreenWidth` が無くビルドエラー

- [ ] **Step 3: enum** — `Settings/OutlineWidthMode.cs` を作る（`HairShadowMode.cs` と同じ形）:

```csharp
namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>アウトラインの幅の基準。World は m、Screen は画面の高さに対する割合。設定ファイルに名前で書くため名前を変えない</summary>
    public enum OutlineWidthMode
    {
        World,
        Screen,
    }
}
```

- [ ] **Step 4: 設定のフィールド** — `GroupSettings.cs` のアウトライン節を次にする:

```csharp
        // アウトライン。元のシェーダーにアウトラインがあるマテリアルにだけ効く
        public bool outlineEnabled = true;
        public OutlineWidthMode outlineWidthMode = OutlineWidthMode.World;
        // 幅は基準ごとに持つ。outlineWidth は m、outlineScreenWidth は画面の高さに対する割合
        public float outlineWidth = 0.0015f;
        public float outlineScreenWidth = 0.002f;
        public float outlineLightingMix = 0.5f;
```

`CopyField` の `OutlineEnabled` の次と `OutlineWidth` を:

```csharp
                case GroupSettingKeys.OutlineWidthMode: outlineWidthMode = from.outlineWidthMode; break;
                case GroupSettingKeys.OutlineWidth:
                    outlineWidth = from.outlineWidth;
                    outlineScreenWidth = from.outlineScreenWidth;
                    break;
```

`FieldEquals` の `OutlineEnabled` の次と `OutlineWidth` を:

```csharp
                case GroupSettingKeys.OutlineWidthMode: return outlineWidthMode == other.outlineWidthMode;
                case GroupSettingKeys.OutlineWidth:
                    return FloatEquals(outlineWidth, other.outlineWidth) && FloatEquals(outlineScreenWidth, other.outlineScreenWidth);
```

- [ ] **Step 5: キー** — `GroupSettingKeys.cs` の `OutlineEnabled` の次に `public const string OutlineWidthMode = "outlineWidthMode";`。`All` の `OutlineEnabled, OutlineWidth,` を `OutlineEnabled, OutlineWidthMode, OutlineWidth,` にする。クラスの `<summary>` の「リム色と線の色は…」の後に「アウトラインの幅は基準ごとの 2 つの値を 1 つの単位にする」を足す。`CommonSettingsTests.cs` のキー数の確認 `Assert.Equal(25, GroupSettingKeys.All.Length);` を 26 にする

- [ ] **Step 6: テスト** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: 全件 PASS（`ChangeAll` を使う既存テストが新項目も既定と違うことを確かめる）

- [ ] **Step 7: コミット** — `feat(outline): アウトラインの幅の基準と画面基準の幅を設定に足す`

---

### Task 2: 基準に合わせて `_OutlineWidthMode`・キーワード・幅を書く

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`（`OutlineModeScreen`）
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/OutlineParams.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonRenderState.cs`（`Resolve` のキーワード）
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs:125-128`（`ApplyGroup`）
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/OutlineParamsTests.cs`、`source/COM3D25.MToonMaid.Plugin.Tests/MToonRenderStateTests.cs`

**Interfaces:**
- Consumes: `OutlineWidthMode`、`GroupSettings.outlineWidthMode` / `outlineWidth` / `outlineScreenWidth`（Task 1）
- Produces: `MToonProps.OutlineModeScreen = 2`、`OutlineParams.Mode(bool outline, OutlineWidthMode widthMode) : int`、`OutlineParams.Width(OutlineWidthMode widthMode, float worldWidth, float screenWidth) : float`

- [ ] **Step 1: 失敗するテストを書く** — `OutlineParamsTests.cs` に:

```csharp
        [Theory]
        [InlineData(false, OutlineWidthMode.World, MToonProps.OutlineModeNone)]
        [InlineData(false, OutlineWidthMode.Screen, MToonProps.OutlineModeNone)]
        [InlineData(true, OutlineWidthMode.World, MToonProps.OutlineModeWorld)]
        [InlineData(true, OutlineWidthMode.Screen, MToonProps.OutlineModeScreen)]
        public void 線の基準(bool outline, OutlineWidthMode widthMode, int expected)
        {
            Assert.Equal(expected, OutlineParams.Mode(outline, widthMode));
        }

        [Theory]
        [InlineData(OutlineWidthMode.World, 0.0015f)]
        [InlineData(OutlineWidthMode.Screen, 0.002f)]
        public void 幅は基準に合う方を使う(OutlineWidthMode widthMode, float expected)
        {
            Assert.Equal(expected, OutlineParams.Width(widthMode, 0.0015f, 0.002f));
        }
```

`MToonRenderStateTests.cs` に:

```csharp
        [Fact]
        public void 画面基準のアウトラインのキーワード()
        {
            var s = MToonRenderState.Resolve(MToonProps.AlphaModeOpaque, false, MToonProps.OutlineModeScreen, false);

            Assert.Contains(MToonProps.KeywordOutlineScreen, s.enabledKeywords);
            Assert.Contains(MToonProps.KeywordOutlineWorld, s.disabledKeywords);
        }
```

既存の `不透明()` に `Assert.Contains(MToonProps.KeywordOutlineScreen, s.disabledKeywords);` を足す（線なしでは画面基準も外れる）。

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: `OutlineModeScreen` / `OutlineParams.Mode` が無くビルドエラー

- [ ] **Step 3: 定数** — `MToonProps.cs` の `OutlineModeWorld = 1;` の次に `public const int OutlineModeScreen = 2;`

- [ ] **Step 4: 基準と幅の決定** — `OutlineParams` クラスに足す:

```csharp
        /// <summary>_OutlineWidthMode に書く値。outline は元のシェーダーに線があり、設定で描くとき true</summary>
        public static int Mode(bool outline, OutlineWidthMode widthMode)
        {
            if (!outline)
            {
                return MToonProps.OutlineModeNone;
            }
            return widthMode == OutlineWidthMode.Screen ? MToonProps.OutlineModeScreen : MToonProps.OutlineModeWorld;
        }

        public static float Width(OutlineWidthMode widthMode, float worldWidth, float screenWidth)
        {
            return widthMode == OutlineWidthMode.Screen ? screenWidth : worldWidth;
        }
```

- [ ] **Step 5: キーワード** — `MToonRenderState.Resolve` の `s.Toggle(MToonProps.KeywordOutlineScreen, false);` を `s.Toggle(MToonProps.KeywordOutlineScreen, outlineMode == MToonProps.OutlineModeScreen);` にする

- [ ] **Step 6: 書き込み** — `MaterialConverter.ApplyGroup` の:

```csharp
            var outlineMode = outline ? MToonProps.OutlineModeWorld : MToonProps.OutlineModeNone;
            m.SetInt(MToonProps.OutlineWidthMode, outlineMode);
            m.SetFloat(MToonProps.OutlineWidth, settings.outlineWidth);
```

を次にする:

```csharp
            var outlineMode = OutlineParams.Mode(outline, settings.outlineWidthMode);
            m.SetInt(MToonProps.OutlineWidthMode, outlineMode);
            m.SetFloat(MToonProps.OutlineWidth, OutlineParams.Width(settings.outlineWidthMode, settings.outlineWidth, settings.outlineScreenWidth));
```

World 固定の箇所はここと Step 5 の `MToonRenderState` だけ（`MaterialBackup` の `outlineWidth` は Toony の値の退避・復元で基準と無関係なので変えない）。

- [ ] **Step 7: テストとビルド** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: 全件 PASS。Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / Expected: `0 エラー`

- [ ] **Step 8: コミット** — `feat(outline): 画面基準のアウトラインを描けるようにする`

---

### Task 3: UI に「幅の基準」を足し、実機で確かめてドキュメントに書く

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（`DrawSettings` のアウトライン節、ラベルの静的配列）
- Modify: `README.md`（使い方の 3.）

**Interfaces:**
- Consumes: Task 1 の設定、`ChoiceRow(GUIView, string key, string label, IList<string> labels, int index, Action<GroupSettings, int> set, bool editable)`

- [ ] **Step 1: ラベル** — `MToonMaidWindow` の `ResolutionLabels` の定義の近くに:

```csharp
        // OutlineWidthMode の並びと同じ
        private static readonly string[] OutlineWidthModeLabels = { "ワールド", "画面" };
```

- [ ] **Step 2: アウトライン節** — `DrawSettings` の「幅」の `Slider` を次にする:

```csharp
            ChoiceRow(view, GroupSettingKeys.OutlineWidthMode, "幅の基準", OutlineWidthModeLabels, (int)shown.outlineWidthMode,
                (s, i) => s.outlineWidthMode = (OutlineWidthMode)i, true);
            var screenWidth = shown.outlineWidthMode == OutlineWidthMode.Screen;
            // 既定の書式 (小数 2 桁) では 0.0015 が 0.00 に見えるため 4 桁で出す
            Slider(view, GroupSettingKeys.OutlineWidth, "幅",
                screenWidth ? shown.outlineScreenWidth : shown.outlineWidth, 0f, 0.01f,
                screenWidth ? def.outlineScreenWidth : def.outlineWidth,
                (s, v) =>
                {
                    if (screenWidth)
                    {
                        s.outlineScreenWidth = v;
                    }
                    else
                    {
                        s.outlineWidth = v;
                    }
                },
                FloatFieldType.F4);
```

（`screenWidth` は描画中の解決後の基準。`BeginEdit` の先が共通でもグループでも、見えている基準の値を書く）

- [ ] **Step 3: ビルド** — Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` / Expected: `0 エラー`

- [ ] **Step 4: 実機確認** — `com3d25-devbridge:restart-verify` スキルで DLL を反映し、メイドを MToon にして確かめる:
  - 共通タブで「幅の基準」を [画面] にすると線の太さが変わり、カメラを寄せ引きしても（約 3m まで）線の太さがほぼ変わらない。`eval_csharp` で線のあるマテリアルの `_OutlineWidthMode` が 2、`IsKeywordEnabled("_MTOON_OUTLINE_SCREEN")` が true、`_MTOON_OUTLINE_WORLD` が false
  - 「幅」のスライダーが 0.0020 を出し、動かすと線が変わる。[ワールド] に戻すとスライダーが元の値（ワールドの幅）を出し、画面基準の値は保存ファイルに残る
  - 髪タブで「幅の基準」だけ個別にして [画面] にすると、髪だけ画面基準になる。その状態で「幅」を動かすと幅が個別になり、ワールド側の値（共通から写した値）は変わらない
  - 「幅」の入力欄にフォーカスしたまま基準を切り替えても、入力欄が新しい基準の値を出す
  - キーワードだけでなく見た目（寄せ引きで太さが保たれる）が変わることで、バンドルに画面基準の変種が入っていることを確かめる
  - メイドの MToon を OFF → ON で、OFF の間 `_MTOON_OUTLINE_SCREEN` が残らない
  - 既存の設定ファイルのメイドで、見た目が変わらない（World のまま）

- [ ] **Step 5: README** — 使い方 3. の「アウトラインの色も…引き継ぎます。」の後に次を足す:

```
アウトラインの「幅の基準」を [画面] にすると、幅は画面の高さに対する割合（0.002 で高さの 0.2%）になり、カメラの距離によらず同じ太さで描きます（約 3m より遠くでは細くなります。メイドの大きさは効きません）。幅は基準ごとに別の値を覚えます。
```

- [ ] **Step 6: コミット** — `feat(ui): アウトラインの幅の基準を選べるようにする`（README を同じコミットに含める）

## レビュー却下メモ

- 画面基準のカメラ依存（影用カメラ・別画角）を README に書く — 線は通常カメラの描画にだけ出るため要件外。実機で問題が見えたら足す
- スライダー範囲 0〜0.01 の妥当性 — 指摘自体が変更不要と結論している
