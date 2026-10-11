# COM3D25.MToonMaid.Plugin 球の陰影を外す Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 「顔の陰影を球に」「顔を平らに」「上下の丸みを消す」と、それを支える頭の中心の受け渡し・シェーダーの球の法線を外す。「凹凸の影を消す」は残す。

**Architecture:** C# 側は設定キー・フィールド・スライダー・`MaterialConverter` の 3 項目・`MaidMToonController` の頭の中心の控えと受け渡し・`MToonMaidPlugin` の `onBeforeRender` を消す。設定ファイルは読み込み時に今の継承の単位に無いキーを `overrides` から取り除く。シェーダーは陰影の N・L を MToon 本来の `normalWS` へ戻し、球のプロパティと `shadingNormalWS` / `MToon_SafeNormalize` を消してバンドルを作り直す。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（シェーダーのバンドル）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-remove-sphere-design.md`

## Global Constraints

- v2.6 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-face-cylinder.md`）の Global Constraints をすべて引き継ぐ。特に: COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は実行しない、worktree 不使用、main で作業、MTEUtils は編集しない、`debug.bat` は実機検証のタスクでだけ使う、`build-bundle.bat` は Unity エディタを閉じて実行する
- 設定ファイルの版は 3 のまま。外した項目の要素は警告なしで読み飛ばし、`overrides` の知らないキーは読み込み時に取り除く
- 継承の単位は 15 項目（v2.6 の 18 項目から `sphereNormal` / `faceFlatten` / `faceCylinder` を除く）
- 「凹凸の影を消す」の振る舞いは変えない。顔のグループの判定は `ToonMaterialClassifier.UsesFaceBumpMask` に名前を変えて残す
- 過去の spec・計画は書き換えない
- **ゲームを終了する前に、ユーザーへシーンの保存の有無を明示的に確認し、回答を ledger に書く**（状況からの推測で承認とみなさない）

## Review Focus

- **古い設定ファイル**: `<sphereNormal>` などの要素と、`overrides` にある `sphereNormal` などのキーを持つ版 3 のファイルが、警告なしで読め、残りの項目（境界の位置など）の値と個別の指定が保たれる → Task 1 Step 1 のテスト
- **凹凸のマスクの対象**: 名前を変えた判定でも顔のグループだけにマスクを作り、肌・髪・衣装のタブではスライダーが無効表示 → Task 1 Step 1（Classifier のテスト）、Task 3 Step 3・4
- **陰影の法線**: シェーダーの N・L が `normalWS` に戻り、球のプロパティがマテリアルから消える（`HasProperty` が false） → Task 2 Step 2、Task 3 Step 2
- **ウィンドウの入力欄キャッシュ**: 行を 3 つ減らしても全タブで同じ順に描くため番号がそろう → Task 3 Step 4
- **onBeforeRender の購読漏れ**: 購読と解除を両方消す（片方だけ残すとコンパイルエラーか、破棄後の呼び出し） → Task 1 Step 3

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/GroupSettingKeys.cs    (Modify: 3 キーを消す)
├─ Settings/GroupSettings.cs       (Modify: 3 フィールド・case を消す、FillMissing で知らないキーを除く)
├─ ToonMaterialClassifier.cs       (Modify: UsesSphereNormal → UsesFaceBumpMask)
├─ Rendering/MToonProps.cs         (Modify: 6 プロパティ名を消す)
├─ Rendering/MaterialConverter.cs  (Modify: ApplyGroup の 3 行)
├─ MaidMToonController.cs          (Modify: 頭の中心の控え・受け渡しを消す)
├─ MToonMaidPlugin.cs              (Modify: onBeforeRender を消す)
├─ UI/MToonMaidWindow.cs           (Modify: スライダー 3 行)
└─ Resources/mtoon_bundle          (Rebuild)
source/COM3D25.MToonMaid.Plugin.Tests/
├─ CommonSettingsTests.cs          (Modify)
└─ ToonMaterialClassifierTests.cs  (Modify)
UnityProject2022/Assets/Shaders/MToon10/
├─ vrmc_materials_mtoon.shader                (Modify)
├─ vrmc_materials_mtoon_input.hlsl            (Modify)
├─ vrmc_materials_mtoon_forward_fragment.hlsl (Modify)
├─ vrmc_materials_mtoon_lighting_mtoon.hlsl   (Modify)
└─ vrmc_materials_mtoon_utility.hlsl          (Modify)
README.md / spec                   (Modify)
```

---

### Task 1: C# から球の項目と頭の中心の受け渡しを外す

**Files:**
- Modify: 上の `source/COM3D25.MToonMaid.Plugin/` の `Resources` 以外すべて
- Test: `CommonSettingsTests.cs`, `ToonMaterialClassifierTests.cs`
- Commit に spec と plan を含める

**Interfaces:**
- Produces: `ToonMaterialClassifier.UsesFaceBumpMask(PartGroup) : bool`（顔だけ true）。`GroupSettingKeys.All` は 15 項目

- [ ] **Step 1: テストを書き換える**

`CommonSettingsTests.cs`:
- `ChangeAll` の `g.sphereNormal = 0.5f;` / `g.faceFlatten = 0.2f;` / `g.faceCylinder = 0.4f;` の 3 行を消す
- テスト `陰影を球に寄せるは既定0で要素の無いファイルも個別にしない` / `顔を平らには既定0_5で要素の無いファイルも個別にしない` / `上下の丸みを消すは既定0で要素の無いファイルも個別にしない` を消す
- 消した位置（`凹凸の影を消すは…` の前）に追加:

```csharp
        [Fact]
        public void 外した球の項目は読み飛ばし個別の一覧からも取り除く()
        {
            Assert.DoesNotContain("sphereNormal", GroupSettingKeys.All);
            Assert.DoesNotContain("faceFlatten", GroupSettingKeys.All);
            Assert.DoesNotContain("faceCylinder", GroupSettingKeys.All);
            Assert.Equal(15, GroupSettingKeys.All.Length);

            var path = Path.Combine(_dir, "v3s.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\">"
                + "<common><sphereNormal>1</sphereNormal><faceFlatten>0.3</faceFlatten><shadingShift>0.1</shadingShift></common>"
                + "<face><shadingShift>0.2</shadingShift><faceCylinder>1</faceCylinder>"
                + "<overrides><string>sphereNormal</string><string>shadingShift</string><string>faceFlatten</string></overrides></face>"
                + "<skin><overrides><string>faceCylinder</string></overrides></skin>"
                + "</MToonMaid>");
            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s.face.overrides);
            Assert.Empty(s.skin.overrides);
            Assert.Equal(0.1f, s.common.shadingShift);
            Assert.Equal(0.2f, s.Resolve(PartGroup.Face).shadingShift);
        }
```

`ToonMaterialClassifierTests.cs` の `陰影を球に寄せるのは顔だけ` を次へ置き換える（`InlineData` 4 行はそのまま）:

```csharp
        public void 凹凸のマスクを作るのは顔だけ(PartGroup group, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.UsesFaceBumpMask(group));
        }
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`UsesFaceBumpMask` が無い）

- [ ] **Step 3: C# を外す**

`Settings/GroupSettingKeys.cs`: `SphereNormal` / `FaceFlatten` / `FaceCylinder` の定数を消し、`All` の 1 行目を次へ置き換える:

```csharp
            ShadeColor, ShadingShift, ShadingToony, GiEqualization, AdditionalLightShade, FaceBumpLift, HighlightScale,
```

`Settings/GroupSettings.cs`:
- フィールド `sphereNormal` / `faceFlatten` / `faceCylinder` とそれぞれの直前のコメントを消す
- `CopyField` と `FieldEquals` の `SphereNormal` / `FaceFlatten` / `FaceCylinder` の case を消す
- `FillMissing` の `if (overrides == null) { ... }` の直後に追加:

```csharp
            // 外した項目 (球の陰影など) のキーを残すと保存のたびに書き戻すため取り除く
            overrides.RemoveAll(k => Array.IndexOf(GroupSettingKeys.All, k) < 0);
```

（`using System;` が無ければ足す）

`ToonMaterialClassifier.cs`: `UsesSphereNormal` を `UsesFaceBumpMask` に改名し、XML コメントを `凹凸のマスク (_ShadingShiftTex) を作るグループ。顔のメッシュから作るため顔だけ` にする

`Rendering/MToonProps.cs`: `SphereNormal` / `HeadCenter` / `FaceFlatten` / `HeadBack` / `FaceCylinder` / `HeadUp` の 6 定数を消す

`Rendering/MaterialConverter.cs` の `ApplyGroup` の次の 5 行:

```csharp
            // 頭の中心は顔のマテリアルにしか入れないため、他のグループでは効かせない
            var usesSphere = ToonMaterialClassifier.UsesSphereNormal(backup.group);
            m.SetFloat(MToonProps.SphereNormal, usesSphere ? Mathf.Clamp01(settings.sphereNormal) : 0f);
            m.SetFloat(MToonProps.FaceFlatten, usesSphere ? Mathf.Clamp01(settings.faceFlatten) : 0f);
            m.SetFloat(MToonProps.FaceCylinder, usesSphere ? Mathf.Clamp01(settings.faceCylinder) : 0f);
```

と、その下の `var bumpLift = usesSphere ? ...` を次へ置き換える（直後の `m.SetTexture` / `m.SetFloat` の 2 行はそのまま）:

```csharp
            // 強さ 0 ではマスクを外し、パラメーターマップのキーワード (サンプリング) を切る
            var bumpLift = ToonMaterialClassifier.UsesFaceBumpMask(backup.group) ? Mathf.Clamp01(settings.faceBumpLift) : 0f;
```

（元の `// 強さ 0 ではマスクを外し…` のコメント行は重複させない）

`MaidMToonController.cs`:
- フィールド `_headLocalCenter` / `_headObject` / `_headBone` とその直前の 2 行のコメント、`HeadCenterId` / `HeadBackId` / `HeadUpId` / `HeadBackLocal` / `HeadUpLocal` / `MaxHeadCenterDistance` とそれぞれのコメントを消す
- `Scan` の `var headFound = false;`、コメント `// 子スロットや非表示の別の頭で後勝ちしないよう…` の行とその下の `if (j == 0 && group == PartGroup.Face && skin.obj.activeInHierarchy) { ... }` のブロック、ループ後の `if (!headFound) { ... }` のブロック（中のコメントごと）を消す。`skin.obj.GetComponentsInChildren(true, _rendererBuffer);` は後のマテリアルの走査で使うので残す
- メソッド `CaptureHeadCenter` と `UpdateHeadCenter`（XML コメントごと）を消す
- `EnsureBumpMask` の `ToonMaterialClassifier.UsesSphereNormal` を `ToonMaterialClassifier.UsesFaceBumpMask` に変える

`MToonMaidPlugin.cs`:
- `Awake` の `Application.onBeforeRender += UpdateHeadCenters;` と `OnDestroy` の `Application.onBeforeRender -= UpdateHeadCenters;` を消す
- メソッド `UpdateHeadCenters`（XML コメントごと）と、そこだけが使うフィールド `_headCenterErrorReported`（直前のコメント行ごと）を消す

`UI/MToonMaidWindow.cs`:
- `Slider(view, GroupSettingKeys.SphereNormal, ...)` / `FaceFlatten` / `FaceCylinder` の 3 行を消す
- `var sphereEditable = ... UsesSphereNormal(...)` を `var bumpMaskEditable = !_editGroup.HasValue || ToonMaterialClassifier.UsesFaceBumpMask(_editGroup.Value);` に変え、`FaceBumpLift` の行の `editable: sphereEditable` を `editable: bumpMaskEditable` にする（直前のコメントはそのまま）

- [ ] **Step 4: テストとビルドを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` と `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: テスト PASS（96 − 3 + 1 = 94 件）、ビルド成功（警告 0）。`grep -rni "spherenormal\|faceflatten\|facecylinder\|headcenter\|headback\|headup\|headLocalCenter\|headBone\|headObject" source --include=*.cs` がテストの文字列（外した項目の確認・凹凸のテストの XML）以外で 0 件

- [ ] **Step 5: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin source/COM3D25.MToonMaid.Plugin.Tests docs/superpowers/specs/2026-10-09-mtoon-maid-remove-sphere-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-remove-sphere.md
git commit -m "refactor: 顔の陰影を球に寄せる設定を外す"
```

---

### Task 2: シェーダーから球の法線を外す

**Files:**
- Modify: シェーダー 5 ファイル
- Rebuild: `Resources/mtoon_bundle`

シェーダーは単体テストが無い。コンパイルは `build-bundle.bat`、動作は Task 3 で確かめる。

- [ ] **Step 1: シェーダーを戻す**

`3358ea9`（球の機能を足す直前）より後で `UnityProject2022/Assets/Shaders/MToon10` に触れたコミット（`7ab0b92` / `f9fc67f` / `206a394` / `e457973` / `5d6301a`）は、どれも球の機能だけを変えている（計画作成時に `git show --stat` で確認）。そのため、フォルダを `3358ea9` の内容へ戻せばよい:

```bash
git checkout 3358ea9 -- UnityProject2022/Assets/Shaders/MToon10
```

戻した結果は、次の手作業の削除と同じになる（Step 2 で確かめる）:

`vrmc_materials_mtoon.shader`:
- 先頭の改変一覧の 3 行（`// - 陰影 (N・L) の法線を頭の球面の法線へ寄せる …`、`// - 球の中心を頭の後ろへずらして …`、`// - 球の法線から頭の上下の成分を抜いて …`）を消す
- Properties の `// COM3D25: 陰影の法線を頭の球面へ寄せる強さと…` から `_HeadUp ("Head Up", Vector) = (0, 0, 0, 0)` までの 9 行を消す

`vrmc_materials_mtoon_input.hlsl`: `float4 _HeadCenter;` / `float4 _HeadBack;` / `float4 _HeadUp;` / `half _SphereNormal;` / `half _FaceFlatten;` / `half _FaceCylinder;` を消す

`vrmc_materials_mtoon_forward_fragment.hlsl`: `mtoonInput.normalWS = normalWS;` の直後の `// COM3D25: 陰影の法線を頭の球面へ寄せる …` から `mtoonInput.shadingNormalWS = …;` までの 9 行を消す

`vrmc_materials_mtoon_lighting_mtoon.hlsl`:
- `MToonInput` の `// COM3D25: トゥーンの陰影 (N・L) にだけ使う法線。…` と `half3 shadingNormalWS;` の 2 行を消す
- `GetMToonLighting_DotNL` の `return dot(input.shadingNormalWS, lighting.directLightDirection);` を `return dot(input.normalWS, lighting.directLightDirection);` に戻す

`vrmc_materials_mtoon_utility.hlsl`: `// COM3D25: 0 ベクトルでも NaN を出さない正規化 …` と `MToon_SafeNormalize` の関数（4 行）と直後の空行を消す

- [ ] **Step 2: 消し残しを確かめる**

Run: `grep -rn "_SphereNormal\|_HeadCenter\|_FaceFlatten\|_HeadBack\|_FaceCylinder\|_HeadUp\|shadingNormalWS\|MToon_SafeNormalize" UnityProject2022/Assets/Shaders/MToon10`、`git diff 3358ea9 -- UnityProject2022/Assets/Shaders/MToon10`
Expected: grep が 0 件。フォルダが `3358ea9` と差分なし（`checkout` は今のフォルダにだけあるファイルを消さないため、この差分なしの確認は省かない）。`git status --short UnityProject2022/Assets/Shaders/MToon10` に未追跡のファイル（`??`）が無い。`_ShadingShiftTex` / `_ShadingShiftTexScale` は MToon10 本来のもので残っている（`grep -c "_ShadingShiftTexScale" UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader` が 1）

- [ ] **Step 3: バンドルを作り直す**

Run: `md5sum source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`、続けて `cmd //c ".\\build-bundle.bat"`
Expected: `コピーしました: ...`。`grep -c "Shader error" UnityProject2022/Logs/build-bundle.log` が 0。`md5sum` が変わっている

- [ ] **Step 4: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（94 件）

- [ ] **Step 5: Commit**

```bash
git add UnityProject2022/Assets/Shaders/MToon10 source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle
git commit -m "refactor: シェーダーから顔の球の法線を外す"
```

---

### Task 3: 実機検証と仕上げ

**終了の前にユーザーへシーンの保存の有無を明示的に確認し、回答を ledger に書く。** 承認のあと restart-verify スキルの手順で、ゲーム終了 → `debug.bat` → 起動 → 最新セーブのロード。eval は完全修飾名、ローカル関数は使わず `System.Func` / `System.Action`。検証で変えた状態（設定・ウィンドウ）は最後に戻す。

**Files:**
- Modify: `README.md`、`docs/superpowers/specs/2026-10-09-mtoon-maid-remove-sphere-design.md`（検証の結果）

- [ ] **Step 0: 保存の有無をユーザーに確認する**

ゲームが開いているシーン（`SceneManager.GetActiveScene().name`）を読み、ユーザーに「ゲームを終了してよいか（保存は済んでいるか）」を質問する。回答を ledger に `Task 3: 保存確認: <回答>` として書く。**回答が得られるまで終了しない**

- [ ] **Step 1: ユーザーの設定ファイルを控え、DLL を配置して起動する**

ゲームを終了したあとで、メイド 0 の設定ファイル（`W:\COM3D2_5\BepInEx\config\COM3D25.MToonMaid\` 配下）に `sphereNormal` などの要素や `overrides` のキーがあるかを `grep` で控える。restart-verify スキルに従って配置・起動・ロードする。DLL のハッシュが一致し、`LogOutput.log` に `MToonMaid 有効化` があり、設定の読み込みの警告が無いこと

- [ ] **Step 2: 球のプロパティが消え、凹凸のマスクが効くことを確かめる**

```csharp
var maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
var plugin = UnityEngine.Object.FindObjectOfType<COM3D25.MToonMaid.Plugin.MToonMaidPlugin>();
var s = plugin.GetSettings(maid0); var userBump = s.common.faceBumpLift;
System.Action NotifyAll = () => { foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g); };
var hs = maid0.body0.goSlot[(int)TBody.hashSlotName["head"]]; var smr = hs.obj.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
s.common.faceBumpLift = 1f; NotifyAll();
var sb = new System.Text.StringBuilder(); foreach (var m in smr.sharedMaterials) if (m != null && m.shader.name.Contains("MToon")) sb.Append(m.name + " sphere=" + m.HasProperty("_SphereNormal") + " headCenter=" + m.HasProperty("_HeadCenter") + " tex=" + (m.GetTexture("_ShadingShiftTex") == null ? "null" : m.GetTexture("_ShadingShiftTex").name) + "; ");
"userBump=" + userBump + " overrides(face)=" + string.Join(",", s.face.overrides.ToArray()) + " | " + sb
```

Expected: 顔の MToon（肌・口）が `sphere=False headCenter=False tex=MToonMaid_BumpMask`。`overrides` に外したキーが無い。確かめたら `s.common.faceBumpLift = userBump; NotifyAll();`

- [ ] **Step 3: 見え方を確かめる**

`screenshot`（顔が UI に隠れるなら `capture` の `target: "camera:CameraMain"`）で顔を撮り、黒や白の破綻・NaN の点が無く、陰影がふつうの MToon（v2.2 相当）に戻っていること

- [ ] **Step 4: ウィンドウを確かめる**

```csharp
var wm = (COM3D25.MToonMaid.Plugin.WindowManager)typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).GetField("_windowManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
var tabField = typeof(COM3D25.MToonMaid.Plugin.MToonMaidWindow).GetField("_tab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var wasShown = wm.MainWindow.isShowWnd; var wasTab = tabField.GetValue(wm.MainWindow); wm.MainWindow.isShowWnd = true; "shown"
```

`screenshot`（`window: "MToonMaid"`）で、共通タブの「追加ライトの影側」の下に「凹凸の影を消す」があり、球の 3 行が無いこと。値の欄がラベルとずれていないこと（入力欄キャッシュ）。`tabField.SetValue(wm.MainWindow, 1)`（肌）と `3`（髪）で「凹凸の影を消す」が個別のトグルを含めて無効表示、`2`（顔）で有効。確かめたら `tabField.SetValue(wm.MainWindow, wasTab)`、`isShowWnd = wasShown`。`LogOutput.log` に MToonMaid 由来の例外が無いこと

- [ ] **Step 5: README と spec を更新する**

`README.md` の「制限」から次の 2 行を消す:
- `- 顔の口元などに MToon の影が入るときは、「顔の陰影を球に」を上げてください。…`
- `- 「顔の陰影を球に」を上げても上からの光で口元などに横向きの影の境目が出るときは、…`

「顔の立体的な陰影を残したまま鼻や口元の影だけを消したいときは、…」の行の書き出しを `- 顔の鼻や口元に MToon の影が入るときは、` に変える（以降の文はそのまま）

`README.md` のほかの箇所に「顔の陰影を球に」「顔を平らに」「上下の丸みを消す」が残っていないことを `grep` で確かめる

spec の末尾に「## 実機検証の結果」を足し、Step 1〜4 の値と見え方を書く

- [ ] **Step 6: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-09-mtoon-maid-remove-sphere-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-remove-sphere.md
git commit -m "docs: 顔の陰影を球に寄せる設定を外したことを反映"
```

## レビュー却下メモ

- 「README の変更履歴・バージョン表記の要否」 — 変更履歴はリリース準備（release-prep）で書く。README に球の項目が残っていないかは Task 3 Step 5 の grep で確かめる

## コードレビュー却下メモ

- 「overrides の未知キーの除去を外したキーの一覧に絞る」 — 未知キーをすべて除く方が単純で、ダウングレードでの扱いは spec に明記した
- 「`IsOverridden` 付近に、overrides を直接回す処理は FillMissing の後に限ると書く」 — 今そうした処理は無く、FillMissing の summary に除去を書いた
