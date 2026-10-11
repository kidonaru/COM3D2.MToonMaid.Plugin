# COM3D25.MToonMaid.Plugin 追加ライトの陰影 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

> **注記:** 実行後に既定値を 0.5 から 1 に変えた（spec 参照）。以下のコードブロックの 0.5 は実行時点の記録。

**Goal:** 追加ライト（ForwardAdd）でもトゥーンの段差が出るようにし、影側の明るさを「追加ライトの影側」（0〜1、既定 0.5）で調整できるようにする。

**Architecture:** シェーダーの ForwardAdd の減衰を `減衰 × lerp(_AdditionalLightShade, 1, shade)` に変える。設定は `GroupSettings.additionalLightShade` を継承の単位の 14 項目目として足し、`MaterialConverter.ApplyGroup` が `_AdditionalLightShade` へ入れる。ウィンドウは「陰影」の末尾にスライダーを 1 行足す。シェーダーは `build-bundle.bat` でバンドルを作り直し、埋め込みリソースを更新する。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ HLSL（Built-in RP、Unity 2022.3.62）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-additional-light-design.md`（前提として `2026-10-09-mtoon-maid-common-tab-design.md`）

## Global Constraints

- v2.1 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-common-tab.md`）の Global Constraints をすべて引き継ぐ。特に: 対象は COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は実行しない、git worktree は使わない、作業は main で行う、MTEUtils のファイルは編集しない
- ビルド確認は `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`、テストは `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`。`debug.bat` はゲームへ DLL をコピーするため Task 3 でだけ使う
- `build-bundle.bat` は Unity エディタで `UnityProject2022` を開いているとロックで失敗する。失敗したらユーザーにエディタを閉じてもらう
- 設定ファイルの版は 3 のまま上げない。要素の無いファイルは既定 0.5 で読む
- 継承の単位は 14 項目（spec の 13 項目 + `additionalLightShade`）
- メインライト（ForwardBase）の描画式は変えない。URP 用の関数（`#ifdef MTOON_URP`）は触らない
- GUIView の入力欄キャッシュは描画順の番号で引かれる。スライダーは全タブで同じ位置に足す

## Review Focus

- **ForwardBase の不変**: `GetMToonLighting_Shadow` の ForwardBase 分岐は `return 1;` のまま。`MToon_IsForwardBasePass()` の分岐を崩すとメインライトの見た目が変わる → Task 2 Step 1 の差分、Task 3 Step 3（メインライトだけの見た目が変わらない）
- **影側の明るさ 0 と 1**: 0 で背面が追加ライトで光らず、1 で影側が影色で平らになる → Task 3 Step 4
- **版 2・版 3 の既存ファイル**: `additionalLightShade` の要素が無くても 0.5 で読み、個別にならない → Task 1 のテスト
- **写し漏れ**: `CopyField` / `FieldEquals` の switch に足し忘れると、その項目だけ継承が効かないか例外になる → Task 1 の `全項目を個別にすると…`（`ChangeAll` に足す）
- **多灯の累積とリム・天使の輪**: 追加ライトが 2 灯あると影側が灯数ぶん明るくなる。リムと天使の輪も追加ライトでは倍になる（spec の互換） → Task 3 Step 4（2 灯目、髪とリムが入る構図）
- **バンドルの更新漏れ**: シェーダーを直してもバンドルを作り直さないと実機に反映されない。プロパティ名の綴り違いは `SetFloat` が黙って無視する → Task 2 Step 4（バンドル内のシェーダーにプロパティがあることを確かめる）、Task 3 Step 2

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/GroupSettingKeys.cs   (Modify: AdditionalLightShade)
├─ Settings/GroupSettings.cs      (Modify: additionalLightShade、CopyField / FieldEquals)
├─ Rendering/MToonProps.cs        (Modify: AdditionalLightShade)
├─ Rendering/MaterialConverter.cs (Modify: ApplyGroup で SetFloat)
├─ UI/MToonMaidWindow.cs          (Modify: スライダー 1 行)
└─ Resources/mtoon_bundle         (Rebuild: build-bundle.bat)
source/COM3D25.MToonMaid.Plugin.Tests/
└─ CommonSettingsTests.cs         (Modify)
UnityProject2022/Assets/Shaders/MToon10/
├─ vrmc_materials_mtoon.shader            (Modify: プロパティ、ヘッダーの改変一覧)
├─ vrmc_materials_mtoon_input.hlsl        (Modify: 変数の宣言)
└─ vrmc_materials_mtoon_lighting_mtoon.hlsl (Modify: ForwardAdd の減衰)
README.md                          (Modify)
docs/superpowers/specs/2026-10-09-mtoon-maid-additional-light-design.md (Modify: 検証の結果)
```

---

### Task 1: 設定に「追加ライトの影側」を足す

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`, `Settings/GroupSettings.cs`, `Rendering/MToonProps.cs`, `Rendering/MaterialConverter.cs`, `UI/MToonMaidWindow.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`

**Interfaces:**
- Consumes: `GroupSettingKeys.All`、`GroupSettings.CopyField` / `FieldEquals`、`MaidSettingsStore.Load`（既存）
- Produces:
  - `GroupSettingKeys.AdditionalLightShade = "additionalLightShade"`（`All` の `GiEqualization` の直後）
  - `GroupSettings.additionalLightShade : float = 0.5f`
  - `MToonProps.AdditionalLightShade = "_AdditionalLightShade"`（Task 2 のシェーダーのプロパティ名と一致させる）

- [ ] **Step 1: 失敗するテストを書く**

`CommonSettingsTests.cs` の `ChangeAll` の `g.giEqualization = 0.1f;` の直後に追加:

```csharp
            g.additionalLightShade = 0.9f;
```

`個別を外してから再び個別にすると共通の値から始まる` の前にテストを足す:

```csharp
        [Fact]
        public void 追加ライトの影側は既定0_5で要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.AdditionalLightShade, GroupSettingKeys.All);
            Assert.Equal(0.5f, GroupSettings.CreateDefault().additionalLightShade);

            var v2 = Path.Combine(_dir, "v2e.xml");
            File.WriteAllText(v2,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><hair><shadingShift>0.2</shadingShift></hair></MToonMaid>");
            var s2 = MaidSettingsStore.Load(v2, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s2.hair.overrides);
            Assert.Equal(0.5f, s2.Resolve(PartGroup.Hair).additionalLightShade);

            var v3 = Path.Combine(_dir, "v3e.xml");
            File.WriteAllText(v3,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\"><common><shadingShift>0.1</shadingShift></common></MToonMaid>");
            var s3 = MaidSettingsStore.Load(v3, _warnings.Add);

            Assert.Equal(0.5f, s3.common.additionalLightShade);
            Assert.Equal(0.5f, s3.Resolve(PartGroup.Skin).additionalLightShade);
        }
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`additionalLightShade` / `GroupSettingKeys.AdditionalLightShade` が無い）

- [ ] **Step 3: キーとフィールドを足す**

`Settings/GroupSettingKeys.cs` の `public const string GiEqualization = "giEqualization";` の直後に追加:

```csharp
        public const string AdditionalLightShade = "additionalLightShade";
```

`All` の 1 行目を次へ置き換える:

```csharp
            ShadeColor, ShadingShift, ShadingToony, GiEqualization, AdditionalLightShade, HighlightScale,
```

`Settings/GroupSettings.cs` の `public float giEqualization = 0.9f;` の直後に追加:

```csharp
        // 追加ライト (ForwardAdd) の影側の明るさ。0 なら影側に当てず、1 なら影色で平らに照らす
        public float additionalLightShade = 0.5f;
```

`CopyField` の `case GroupSettingKeys.GiEqualization: ...` の直後に追加:

```csharp
                case GroupSettingKeys.AdditionalLightShade: additionalLightShade = from.additionalLightShade; break;
```

`FieldEquals` の `case GroupSettingKeys.GiEqualization: ...` の直後に追加:

```csharp
                case GroupSettingKeys.AdditionalLightShade: return FloatEquals(additionalLightShade, other.additionalLightShade);
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（78 + 1 = 79 件）

- [ ] **Step 5: マテリアルとウィンドウへつなぐ**

`Rendering/MToonProps.cs` の `public const string HiRateScale = "_HiRateScale";` の直後に追加:

```csharp
        public const string AdditionalLightShade = "_AdditionalLightShade";
```

`Rendering/MaterialConverter.cs` の `ApplyGroup` の `m.SetFloat(MToonProps.GiEqualization, settings.giEqualization);` の直後に追加:

```csharp
            m.SetFloat(MToonProps.AdditionalLightShade, settings.additionalLightShade);
```

`UI/MToonMaidWindow.cs` の `DrawSettings` の「環境光の均一化」の `Slider(...)` 行の直後に追加:

```csharp
            Slider(view, GroupSettingKeys.AdditionalLightShade, "追加ライトの影側", shown.additionalLightShade, 0f, 1f, def.additionalLightShade, (s, v) => s.additionalLightShade = v);
```

- [ ] **Step 6: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（79 件）

- [ ] **Step 7: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs
git commit -m "feat: 追加ライトの影側の明るさを設定に追加"
```

---

### Task 2: シェーダーの ForwardAdd の減衰を変えてバンドルを作り直す

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`, `vrmc_materials_mtoon_input.hlsl`, `vrmc_materials_mtoon_lighting_mtoon.hlsl`
- Rebuild: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

**Interfaces:**
- Consumes: `MToonProps.AdditionalLightShade = "_AdditionalLightShade"`（Task 1）
- Produces: シェーダーのプロパティ `_AdditionalLightShade`（Float、既定 0.5）

シェーダーは単体テストが無い。コンパイルは `build-bundle.bat`（ログに `Shader error` が無いこと）で、見た目は Task 3 の実機で確かめる。

- [ ] **Step 1: 減衰の式を変える**

`vrmc_materials_mtoon_lighting_mtoon.hlsl` の `GetMToonLighting_Shadow` を次へ置き換える:

```hlsl
// COM3D25: 追加ライトは本家の「半分の明るさ + 影側を N・L で暗くする」をやめ、
// 光側は減衰だけ、影側は _AdditionalLightShade 倍で平らに照らす (追加ライトだけの演出でもトゥーンの段差を出す)
inline half GetMToonLighting_Shadow(const UnityLighting lighting, const half dotNL, const half shade)
{
    if (MToon_IsPbrCorrectOn())
    {
        return lighting.directLightAttenuation.x * step(0, dotNL);
    }

    if (MToon_IsForwardBasePass())
    {
        return 1;
    }
    else
    {
        return lighting.directLightAttenuation.x * lerp(_AdditionalLightShade, 1, shade);
    }
}
```

`GetMToonLighting` の `const half shadow = GetMToonLighting_Shadow(unityLight, dotNL);` を次へ置き換える:

```hlsl
    const half shadow = GetMToonLighting_Shadow(unityLight, dotNL, shade);
```

- [ ] **Step 2: プロパティを宣言する**

`vrmc_materials_mtoon_input.hlsl` の `half _GiEqualization;` の直後に追加:

```hlsl
half _AdditionalLightShade;
```

`vrmc_materials_mtoon.shader` の `_GiEqualization ("mtoon.giEqualizationFactor", Range(0, 1)) = 0.9` の直後に追加:

```
        // COM3D25: 追加ライトの影側の明るさ (プラグインが設定する)
        _AdditionalLightShade ("Additional Light Shade", Range(0, 1)) = 0.5
```

同ファイル先頭の改変一覧の `// - Toony の髪のハイライト ...` の行の直後に追加:

```
// - 追加ライト (ForwardAdd) の減衰を、光側は減衰だけ・影側は _AdditionalLightShade 倍の平らな明るさに変更
```

- [ ] **Step 3: 宣言漏れを確かめる**

Run: `grep -rn "_AdditionalLightShade" UnityProject2022/Assets/Shaders/MToon10`
Expected: `.shader` のプロパティ 1 行と改変一覧 1 行、`input.hlsl` 1 行、`lighting_mtoon.hlsl` 2 行（コメントと式）。`grep -n "GetMToonLighting_Shadow(" UnityProject2022/Assets/Shaders/MToon10/*.hlsl` が定義 1 行と呼び出し 1 行で、呼び出しが 3 引数

- [ ] **Step 4: バンドルを作り直す**

先に今のバンドルのハッシュを控える:

Run: `md5sum source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

Run: `cmd //c ".\\build-bundle.bat"`
Expected: `コピーしました: ...\Resources\mtoon_bundle`。`grep -c "Shader error" UnityProject2022/Logs/build-bundle.log` が 0。`md5sum` が控えた値から変わっている（変わらなければ Unity がシェーダーを拾っていないので、ログを読んで原因を探す）。ロックで失敗したら Unity エディタを閉じてもらってから再実行する

バンドルは LZ4 圧縮でプロパティ名を grep できないことが多い。プロパティが入ったことは Task 3 Step 2 の `HasProperty` で確かめる

- [ ] **Step 5: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（79 件）

- [ ] **Step 6: Commit**

```bash
git add UnityProject2022/Assets/Shaders/MToon10 source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle
git commit -m "feat: 追加ライトでもトゥーンの段差を出し、影側の明るさを調整できるようにする"
```

---

### Task 3: 実機検証と仕上げ

**restart-verify スキル**（`com3d25-devbridge:restart-verify`）の手順で、ゲーム終了 → `debug.bat` で DLL を配置 → 起動 → 最新セーブのロードを行う。eval では `UnityEngine` の型を完全修飾名で書き、ローカル関数は使わず `System.Func` で書く。検証で変えた状態（ライトの有効・無効、メイドの「MToon にする」、設定値）は最後に検証前へ戻す。

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-10-09-mtoon-maid-additional-light-design.md`（検証の結果）

- [ ] **Step 1: DLL を配置して起動し、セーブをロードする**

restart-verify スキルに従う。終了前に、ゲームが SceneEditor のシーンを開いているかを控える（再起動後に同じシーンへ戻せないため、検証はデイリー画面で行う）。起動後の `LogOutput.log` に `MToonMaid 有効化` があること。DLL のハッシュがビルド成果物と一致すること

- [ ] **Step 2: プロパティが届いていることを確かめる**

```csharp
var maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
var plugin = UnityEngine.Object.FindObjectOfType<COM3D25.MToonMaid.Plugin.MToonMaidPlugin>();
var s = plugin.GetSettings(maid0); var wasEnabled = s.enabled; plugin.SetEnabled(maid0, true);
var savedShade = s.common.additionalLightShade;
System.Func<string, UnityEngine.Material> MToonOf = prefix => { var sl = maid0.body0.goSlot;
  for (int i = 0; i < sl.Count; i++) for (int j = 0; j < sl.CountChildren(i); j++) { var sk = sl[i, j]; if (sk == null || sk.obj == null || !sk.Category.StartsWith(prefix)) continue;
    foreach (var r in sk.obj.GetComponentsInChildren<UnityEngine.Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null && m.shader.name.Contains("MToon")) return m; }
  return null; };
"has=" + MToonOf("body").HasProperty("_AdditionalLightShade") + " value=" + MToonOf("body").GetFloat("_AdditionalLightShade") + " wasEnabled=" + wasEnabled
```

Expected: `has=True value=0.5`

- [ ] **Step 3: メインライトだけの見た目が変わらないことを確かめる**

`screenshot`（GameView 全体、メイドの上半身が入る `rect`）で、追加ライトを置く前の見た目を撮る。v2.1 の検証（spec `2026-10-09-mtoon-maid-common-tab-design.md` の実機検証の結果）と同じく、メインライトの陰影でトゥーンの段差が出ていること

- [ ] **Step 4: 追加ライトの陰影を確かめる**

他のライトを止め、メイドの横から検証用のスポットを 1 灯だけ当てる:

```csharp
var savedLights = new System.Collections.Generic.List<UnityEngine.Light>();
foreach (var l in UnityEngine.Object.FindObjectsOfType<UnityEngine.Light>()) if (l.enabled) { savedLights.Add(l); l.enabled = false; }
var head = maid0.body0.trsHead.position;
var go = new UnityEngine.GameObject("MToonTestSpot");
var tl = go.AddComponent<UnityEngine.Light>();
tl.type = UnityEngine.LightType.Spot; tl.range = 5f; tl.spotAngle = 80f; tl.intensity = 3f; tl.color = UnityEngine.Color.white;
go.transform.position = head + UnityEngine.Camera.main.transform.right * 1.2f + UnityEngine.Vector3.up * 0.2f;
go.transform.LookAt(head);
"disabled=" + savedLights.Count
```

`additionalLightShade` を 0.5 → 0 → 1 と変えて、それぞれ `screenshot` を撮る:

```csharp
s.common.additionalLightShade = 0f;
foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g);
"body=" + MToonOf("body").GetFloat("_AdditionalLightShade")
```

Expected（影側の色は `影色 × ライト色 × 減衰 × S`、光側は `明るい側の色 × ライト色 × 減衰`）:
- 0.5: 光側が平らに明るく、影側は影色の半分の明るさで平ら。境界に段差がある（腕の影側に N·L のグラデーションが無い）
- 0: 光の当たらない側が暗いまま（`body=0`）
- 1: 影側も光側と同じ強さで照らされ、色だけ影色になる

撮る範囲には髪（天使の輪）と体の縁（リム）を入れ、光側でリムと天使の輪が出ていることも見る。

続けて S を 0.5 に戻し、反対側から同じ強さの 2 灯目を足して撮る。どちらのライトからも影側になる面（頭頂など）が 2 灯ぶん（S × 2）明るくなることを確かめる:

```csharp
var go2 = new UnityEngine.GameObject("MToonTestSpot2");
var tl2 = go2.AddComponent<UnityEngine.Light>();
tl2.type = UnityEngine.LightType.Spot; tl2.range = 5f; tl2.spotAngle = 80f; tl2.intensity = 3f; tl2.color = UnityEngine.Color.white;
go2.transform.position = head - UnityEngine.Camera.main.transform.right * 1.2f + UnityEngine.Vector3.up * 0.2f;
go2.transform.LookAt(head);
"added"
```

撮り終えたら戻す:

```csharp
UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(go2); foreach (var l in savedLights) l.enabled = true;
s.common.additionalLightShade = savedShade;
foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g);
plugin.SetEnabled(maid0, wasEnabled);
"restored lights=" + savedLights.Count + " shade=" + s.common.additionalLightShade
```

- [ ] **Step 5: ウィンドウを確かめる**

```csharp
var wm = (COM3D25.MToonMaid.Plugin.WindowManager)typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).GetField("_windowManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
wm.MainWindow.isShowWnd = true; "shown"
```

`screenshot`（`window: "MToonMaid"`）で、共通タブの「陰影」に「環境光の均一化」の下の「追加ライトの影側」（0.50）が出ていること。`tail_log` に `COM3D25.MToonMaid` を含む例外が無いこと。確かめたら `wm.MainWindow.isShowWnd = false` で戻す

- [ ] **Step 6: README と spec を更新する**

`README.md` の「制限」の `- トゥーンランプ (...)` の行の直後に追加:

```markdown
- ポイント・スポットなどの追加ライトは、光側を平らに、影側を「追加ライトの影側」の明るさで平らに照らします（0 で影側に当てない、1 で光側と同じ強さの影色で照らす）。以前の版より追加ライトの光側が倍の明るさになり、リムと天使の輪も明るくなります。キャラの基本の明るさと陰影は平行光で作ると、MToon の調整項目（環境光の均一化など）がすべて効きます
```

`docs/superpowers/specs/2026-10-09-mtoon-maid-additional-light-design.md` の末尾に「## 実機検証の結果」を足し、Step 2〜5 で得た値と見え方を書く。確認できなかった項目があれば書く

- [ ] **Step 7: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-09-mtoon-maid-additional-light-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-additional-light.md
git commit -m "docs: 追加ライトの陰影の使い方と実機検証の結果を追加"
```

## レビュー却下メモ

- 「ForwardAdd の shade が既定のマイナスの境界の位置で背面寄りにずれる」 — メインライトと同じ既存の式で、境界の位置で調整できる。変更不要
