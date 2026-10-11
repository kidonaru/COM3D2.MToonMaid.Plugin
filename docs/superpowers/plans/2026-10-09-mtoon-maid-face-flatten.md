# COM3D25.MToonMaid.Plugin 顔を平らに Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 「顔の陰影を球に」の球の中心を頭の後ろへずらす「顔を平らに」（0〜1、既定 0.5）を足し、横からの光でも顔が半分影にならないアニメ調の陰影にできるようにする。

**Architecture:** シェーダーは陰影の球の中心を `_HeadCenter.xyz + _HeadBack.xyz * _FaceFlatten` にする。`_FaceFlatten` はグループの設定値 `GroupSettings.faceFlatten`（継承 16 項目目）を `MaterialConverter.ApplyGroup` が顔のグループにだけ入れる（他は 0）。`_HeadBack` は頭のボーンのローカル (0, -0.5, 0) を世界座標のベクトルにしたもので、`MaidMToonController.UpdateHeadCenter` が `_HeadCenter` と一緒に毎フレーム顔の変換済みマテリアルへ入れる。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ HLSL（Built-in RP、Unity 2022.3.62）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-face-flatten-design.md`（前提として `2026-10-09-mtoon-maid-sphere-normal-design.md`）

## Global Constraints

- v2.3 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-sphere-normal.md`）の Global Constraints をすべて引き継ぐ。特に: COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は実行しない、worktree 不使用、main で作業、MTEUtils は編集しない、`debug.bat` は実機検証のタスクでだけ使う、`build-bundle.bat` は Unity エディタを閉じて実行する
- 設定ファイルの版は 3 のまま。要素の無いファイルは既定 0.5 で読む
- 継承の単位は 16 項目（v2.3 の 15 項目 + `faceFlatten`）
- `_FaceFlatten` を入れるのは顔のグループだけ。肌・髪・衣装には常に 0
- 顔の正面は頭のボーンのローカル +Y（2026-10-09 に実機で確認）。後ろ向きのベクトルはローカル (0, -0.5, 0)
- GUIView の入力欄キャッシュは描画順の番号で引かれる。スライダーは全タブで同じ位置に描き、効かないタブでは無効表示にする（行を消さない）
- ゲームの再起動の前に、ゲームが SceneEditor のシーンを開いていたらユーザーに保存の確認をとる

## Review Focus

- **強さ 0 の見た目の不変**: 「顔の陰影を球に」が 0 なら「顔を平らに」の値によらずメッシュの法線のまま（`lerp` の重みが `_SphereNormal * _HeadCenter.w`） → Task 2 Step 1、Task 3 Step 2
- **既定 0.5 の読込**: 要素の無い v2 / v3 のファイルが 0.5 で読まれ、個別にもならない → Task 1 のテスト
- **顔以外に効かない**: 共通の値を上げても肌・髪・衣装の `_FaceFlatten` / `_HeadBack` は 0 → Task 3 Step 2
- **頭の向きへの追従**: 頭を回しても後ろ向きのベクトルが頭と一緒に回る（`TransformVector` を毎フレーム） → Task 3 Step 3
- **写し漏れ**: `CopyField` / `FieldEquals` への追加漏れ → Task 1 の `全項目を個別にすると…`（`ChangeAll` に足す）

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/GroupSettingKeys.cs   (Modify: FaceFlatten)
├─ Settings/GroupSettings.cs      (Modify: faceFlatten = 0.5f、CopyField / FieldEquals)
├─ Rendering/MToonProps.cs        (Modify: FaceFlatten / HeadBack)
├─ Rendering/MaterialConverter.cs (Modify: ApplyGroup で SetFloat)
├─ MaidMToonController.cs         (Modify: UpdateHeadCenter で _HeadBack)
├─ UI/MToonMaidWindow.cs          (Modify: スライダー 1 行)
└─ Resources/mtoon_bundle         (Rebuild)
source/COM3D25.MToonMaid.Plugin.Tests/
└─ CommonSettingsTests.cs         (Modify)
UnityProject2022/Assets/Shaders/MToon10/
├─ vrmc_materials_mtoon.shader             (Modify: プロパティ、改変一覧)
├─ vrmc_materials_mtoon_input.hlsl         (Modify: 宣言)
└─ vrmc_materials_mtoon_forward_fragment.hlsl (Modify: 球の中心)
README.md / spec                   (Modify)
```

---

### Task 1: 設定に「顔を平らに」を足す

**Files:**
- Modify: `Settings/GroupSettingKeys.cs`, `Settings/GroupSettings.cs`, `Rendering/MToonProps.cs`, `Rendering/MaterialConverter.cs`, `UI/MToonMaidWindow.cs`
- Test: `CommonSettingsTests.cs`

**Interfaces:**
- Consumes: `ToonMaterialClassifier.UsesSphereNormal(PartGroup)`（v2.3）
- Produces:
  - `GroupSettingKeys.FaceFlatten = "faceFlatten"`（`All` の `SphereNormal` の直後）
  - `GroupSettings.faceFlatten : float = 0.5f`
  - `MToonProps.FaceFlatten = "_FaceFlatten"`、`MToonProps.HeadBack = "_HeadBack"`（Task 2 のシェーダーと一致させる）

- [ ] **Step 1: 失敗するテストを書く**

`CommonSettingsTests.cs` の `ChangeAll` の `g.sphereNormal = 0.5f;` の直後に追加:

```csharp
            g.faceFlatten = 0.2f;
```

`個別を外してから再び個別にすると共通の値から始まる` の前に追加:

```csharp
        [Fact]
        public void 顔を平らには既定0_5で要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.FaceFlatten, GroupSettingKeys.All);
            Assert.Equal(0.5f, GroupSettings.CreateDefault().faceFlatten);

            var v2 = Path.Combine(_dir, "v2g.xml");
            File.WriteAllText(v2,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><face><shadingShift>0.2</shadingShift></face></MToonMaid>");
            var s2 = MaidSettingsStore.Load(v2, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s2.face.overrides);
            Assert.Equal(0.5f, s2.Resolve(PartGroup.Face).faceFlatten);

            var v3 = Path.Combine(_dir, "v3g.xml");
            File.WriteAllText(v3,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\"><common><sphereNormal>1</sphereNormal></common></MToonMaid>");
            var s3 = MaidSettingsStore.Load(v3, _warnings.Add);

            Assert.Equal(0.5f, s3.common.faceFlatten);
            Assert.Equal(0.5f, s3.Resolve(PartGroup.Face).faceFlatten);
        }
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`faceFlatten` / `GroupSettingKeys.FaceFlatten` が無い）

- [ ] **Step 3: キー・フィールドを足す**

`Settings/GroupSettingKeys.cs` の `public const string SphereNormal = "sphereNormal";` の直後に追加:

```csharp
        public const string FaceFlatten = "faceFlatten";
```

`All` の 1 行目を次へ置き換える:

```csharp
            ShadeColor, ShadingShift, ShadingToony, GiEqualization, AdditionalLightShade, SphereNormal, FaceFlatten, HighlightScale,
```

`Settings/GroupSettings.cs` の `public float sphereNormal;` の直後に追加:

```csharp
        // 球の中心を頭の後ろへずらす量 (0〜1)。大きいほど顔の陰影が正面向きにそろい、横からの光でも顔が半分影にならない
        public float faceFlatten = 0.5f;
```

`CopyField` の `case GroupSettingKeys.SphereNormal: ...` の直後に追加:

```csharp
                case GroupSettingKeys.FaceFlatten: faceFlatten = from.faceFlatten; break;
```

`FieldEquals` の `case GroupSettingKeys.SphereNormal: ...` の直後に追加:

```csharp
                case GroupSettingKeys.FaceFlatten: return FloatEquals(faceFlatten, other.faceFlatten);
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（84 + 1 = 85 件）

- [ ] **Step 5: マテリアルとウィンドウへつなぐ**

`Rendering/MToonProps.cs` の `public const string HeadCenter = "_HeadCenter";` の直後に追加:

```csharp
        public const string FaceFlatten = "_FaceFlatten";
        public const string HeadBack = "_HeadBack";
```

`Rendering/MaterialConverter.cs` の `ApplyGroup` の `MToonProps.SphereNormal` を入れる行の直後に追加:

```csharp
            m.SetFloat(MToonProps.FaceFlatten, ToonMaterialClassifier.UsesSphereNormal(backup.group) ? Mathf.Clamp01(settings.faceFlatten) : 0f);
```

`UI/MToonMaidWindow.cs` の `Slider(view, GroupSettingKeys.SphereNormal, ...)` 行の直後に追加:

```csharp
            Slider(view, GroupSettingKeys.FaceFlatten, "顔を平らに", shown.faceFlatten, 0f, 1f, def.faceFlatten, (s, v) => s.faceFlatten = v, editable: sphereEditable);
```

- [ ] **Step 6: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（85 件）

- [ ] **Step 7: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin source/COM3D25.MToonMaid.Plugin.Tests docs/superpowers/specs/2026-10-09-mtoon-maid-face-flatten-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-face-flatten.md
git commit -m "feat: 顔を平らにする設定を追加"
```

---

### Task 2: シェーダーの球の中心をずらし、頭の後ろ向きを渡す

**Files:**
- Modify: シェーダー 3 ファイル、`MaidMToonController.cs`
- Rebuild: `Resources/mtoon_bundle`

**Interfaces:**
- Consumes: `MToonProps.FaceFlatten` / `HeadBack`（Task 1）
- Produces: シェーダーのプロパティ `_FaceFlatten`（Float、既定 0）/ `_HeadBack`（Vector、既定 0）

シェーダーと Unity の Transform は単体テストが無い。コンパイルは `build-bundle.bat`、動作は Task 3 で確かめる。

- [ ] **Step 1: シェーダーを変える**

`vrmc_materials_mtoon_input.hlsl` の `float4 _HeadCenter;` の直後に追加:

```hlsl
float4 _HeadBack;
```

同ファイルの `half _SphereNormal;` の直後に追加:

```hlsl
half _FaceFlatten;
```

`vrmc_materials_mtoon.shader` の `_HeadCenter ("Head Center", Vector) = (0, 0, 0, 0)` の直後に追加:

```
        // COM3D25: 球の中心を頭の後ろへずらす量と、頭の後ろ向きのベクトル (_FaceFlatten = 1 のときのずれ)。どちらもプラグインが設定する
        _FaceFlatten ("Face Flatten", Range(0, 1)) = 0
        _HeadBack ("Head Back", Vector) = (0, 0, 0, 0)
```

同ファイル先頭の改変一覧の `// - 陰影 (N・L) の法線を頭の球面の法線へ寄せる ...` の行の直後に追加:

```
// - 球の中心を頭の後ろへずらして顔の陰影を正面向きにそろえる _FaceFlatten / _HeadBack を追加
```

`vrmc_materials_mtoon_forward_fragment.hlsl` の次の行:

```hlsl
    const float3 sphereNormalWS = MToon_SafeNormalize(input.positionWS - _HeadCenter.xyz);
```

を次へ置き換える:

```hlsl
    // 中心を頭の後ろへずらすほど顔の各点の法線が正面へそろい、顔全体が同じ明るさに近づく
    const float3 sphereCenterWS = _HeadCenter.xyz + _HeadBack.xyz * _FaceFlatten;
    const float3 sphereNormalWS = MToon_SafeNormalize(input.positionWS - sphereCenterWS);
```

- [ ] **Step 2: 宣言漏れを確かめる**

Run: `grep -rn "_FaceFlatten\|_HeadBack" UnityProject2022/Assets/Shaders/MToon10`
Expected: `.shader` にプロパティ 2 行と改変一覧・コメントの計 4 行前後、`input.hlsl` に 2 行、`forward_fragment.hlsl` に 1 行

- [ ] **Step 3: バンドルを作り直す**

Run: `md5sum source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`、続けて `cmd //c ".\\build-bundle.bat"`
Expected: `コピーしました: ...`。`grep -c "Shader error" UnityProject2022/Logs/build-bundle.log` が 0。`md5sum` が変わっている

- [ ] **Step 4: 頭の後ろ向きを毎フレーム入れる**

`MaidMToonController.cs` の `private static readonly int HeadCenterId = ...;` の直後に追加:

```csharp
        private static readonly int HeadBackId = Shader.PropertyToID(MToonProps.HeadBack);
        // 「顔を平らに」が 1 のときの球の中心のずれ (頭のボーンのローカル)。+Y が顔の正面 (実機で確認)
        private static readonly Vector3 HeadBackLocal = new Vector3(0f, -0.5f, 0f);
```

`UpdateHeadCenter` を次へ置き換える（XML コメントも更新する）:

```csharp
        /// <summary>
        /// 顔の変換済みマテリアルへ頭の中心と後ろ向きのベクトルを入れる。頭はモーションで毎フレーム動くため、走査の間隔を待たずに LateUpdate で呼ぶ。
        /// 中心が無いときは w = 0 を入れ、シェーダーで効かせない
        /// </summary>
        public void UpdateHeadCenter()
        {
            var center = Vector4.zero;
            var back = Vector4.zero;
            var head = Maid.body0 != null ? Maid.body0.trsHead : null;
            if (_headLocalCenter.HasValue && head != null)
            {
                var p = head.TransformPoint(_headLocalCenter.Value);
                center = new Vector4(p.x, p.y, p.z, 1f);
                // 拡大縮小も含めて変換し、メイドの大きさに追従させる
                var b = head.TransformVector(HeadBackLocal);
                back = new Vector4(b.x, b.y, b.z, 0f);
            }
            foreach (var e in _entries.Values)
            {
                // Unity の null 判定はネイティブ側を見るため、安いグループの判定を先にする
                if (!ToonMaterialClassifier.UsesSphereNormal(e.backup.group) || e.material == null || e.material.shader != _converter.MToonShader)
                {
                    continue;
                }
                e.material.SetVector(HeadCenterId, center);
                e.material.SetVector(HeadBackId, back);
            }
        }
```

- [ ] **Step 5: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（85 件）

- [ ] **Step 6: Commit**

```bash
git add UnityProject2022/Assets/Shaders/MToon10 source/COM3D25.MToonMaid.Plugin
git commit -m "feat: 球の中心を頭の後ろへずらして顔の陰影を平らにする"
```

---

### Task 3: 実機検証と仕上げ

**restart-verify スキル**の手順で、ゲーム終了 → `debug.bat` → 起動 → 最新セーブのロード。**終了の前に、ゲームが SceneEditor のシーンを開いているならユーザーに保存の確認をとる**。eval は完全修飾名、ローカル関数は使わず `System.Func` / `System.Action`。検証で変えた状態（設定・メインライトの向き）は最後に戻す。

**Files:**
- Modify: `README.md`、`docs/superpowers/specs/2026-10-09-mtoon-maid-face-flatten-design.md`（検証の結果）

- [ ] **Step 1: DLL を配置して起動し、セーブをロードする**

restart-verify スキルに従う。DLL のハッシュが一致し、`LogOutput.log` に `MToonMaid 有効化` があること

- [ ] **Step 2: プロパティを確かめる**

```csharp
var maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
var plugin = UnityEngine.Object.FindObjectOfType<COM3D25.MToonMaid.Plugin.MToonMaidPlugin>();
var s = plugin.GetSettings(maid0); var wasEnabled = s.enabled; plugin.SetEnabled(maid0, true);
System.Func<string, UnityEngine.Material> MToonOf = prefix => { var sl = maid0.body0.goSlot;
  for (int i = 0; i < sl.Count; i++) for (int j = 0; j < sl.CountChildren(i); j++) { var sk = sl[i, j]; if (sk == null || sk.obj == null || !sk.Category.StartsWith(prefix)) continue;
    foreach (var r in sk.obj.GetComponentsInChildren<UnityEngine.Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null && m.shader.name.Contains("MToon")) return m; }
  return null; };
System.Action NotifyAll = () => { foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g); };
var head = maid0.body0.trsHead;
var hb = (UnityEngine.Vector3)MToonOf("head").GetVector("_HeadBack");
"flatten=" + MToonOf("head").GetFloat("_FaceFlatten") + " backLen=" + hb.magnitude.ToString("F3") + " backDotFwd=" + UnityEngine.Vector3.Dot(hb.normalized, head.TransformDirection(UnityEngine.Vector3.up)).ToString("F2") + " bodyFlatten=" + MToonOf("body").GetFloat("_FaceFlatten") + " hairBack=" + MToonOf("hairF").GetVector("_HeadBack")
```

Expected: `flatten=0.5`、`backLen` が 0.5 × 頭のボーンの拡大率（v2.3 の調査で lossyScale ≈ 1.1 なので 0.5〜0.6）、`backDotFwd=-1.00`（受け渡しの確認。ローカル +Y が本当に顔の正面かは Step 3 の見え方で確かめる）、`bodyFlatten=0`、`hairBack=(0.0, 0.0, 0.0, 0.0)`

- [ ] **Step 3: 見え方と追従を確かめる**

横からの光にして、強さ 1 で「顔を平らに」0 / 0.5 / 1 を比べる:

```csharp
var lt = GameMain.Instance.MainLight.GetComponent<UnityEngine.Light>(); var origRot = lt.transform.rotation;
var key = COM3D25.MToonMaid.Plugin.GroupSettingKeys.FaceFlatten;
s.common.sphereNormal = 1f; s.common.faceFlatten = 0f; NotifyAll(); lt.transform.rotation = UnityEngine.Quaternion.Euler(20, 120, 0);
"head=" + MToonOf("head").GetFloat("_SphereNormal") + " flatten=" + MToonOf("head").GetFloat("_FaceFlatten")
```

`screenshot`（顔を拡大、rect は画面解像度に合わせる）で、0 では顔の半分が影（v2.3 と同じ）であること。続けて `s.common.faceFlatten = 0.5f; NotifyAll();` で頬に柔らかいグラデーションまたは顔全体が明るいこと、`1f` で顔全体が明るいこと（0 → 1 で影の範囲が減ることが、ローカル +Y が顔の正面である実質の確認になる。逆に影が増えたら軸が逆）。顔に黒や白の点（NaN）が出ていないこと。試作の 0.1 / 0.3 は頭のボーンのローカルの長さで、既定 0.5 は 0.25 にあたる。既定 0.5 の見え方が狙い（頬に少しグラデーションが残る〜顔全体が明るい）から外れたら、既定値を直してテストも合わせ、ledger に Ruling を残す

強さ 0 で平らにが効かないことを確かめる: `s.common.sphereNormal = 0f; s.common.faceFlatten = 0f; NotifyAll();` で `screenshot`、続けて `s.common.faceFlatten = 1f; NotifyAll();` で `screenshot` し、2 枚が同じ（影の境界が鼻・口元の凹凸に沿う）こと。あわせて `MToonOf("head").GetFloat("_SphereNormal")` が 0、`GetFloat("_FaceFlatten")` が 1 であること

後ろ向きのベクトルが頭に追従することを確かめる（2 回の読みで、顔の正面との内積が -1 のまま）:

```csharp
"dot=" + UnityEngine.Vector3.Dot(((UnityEngine.Vector3)MToonOf("head").GetVector("_HeadBack")).normalized, head.TransformDirection(UnityEngine.Vector3.up)).ToString("F3") + " fwd=" + head.TransformDirection(UnityEngine.Vector3.up).ToString("F3")
```

Expected: 少し間を空けて 2 回実行し、`fwd` が変わっていても `dot=-1.000`

戻す:

```csharp
s.common.sphereNormal = 0f; s.common.faceFlatten = 0.5f; NotifyAll(); lt.transform.rotation = origRot; plugin.SetEnabled(maid0, wasEnabled);
"common sphere=" + s.common.sphereNormal + " flatten=" + s.common.faceFlatten + " light=" + lt.transform.rotation.eulerAngles
```

- [ ] **Step 4: ウィンドウを確かめる**

```csharp
var wm = (COM3D25.MToonMaid.Plugin.WindowManager)typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).GetField("_windowManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
var tabField = typeof(COM3D25.MToonMaid.Plugin.MToonMaidWindow).GetField("_tab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
wm.MainWindow.isShowWnd = true; "shown"
```

`screenshot`（`window: "MToonMaid"`）で、共通タブの「顔の陰影を球に」の下に「顔を平らに」（0.50）があること。`tabField.SetValue(wm.MainWindow, 1)`（肌タブ）と `3`（髪タブ）でその行が個別のトグルを含めて無効表示、`2`（顔タブ）で有効であること。`tail_log` に MToonMaid 由来の例外が無いこと。確かめたら `tabField.SetValue(wm.MainWindow, 0)`、`isShowWnd = false`

- [ ] **Step 5: README と spec を更新する**

`README.md` の「制限」の「顔の口元などに MToon の影が入るときは…」の行を次へ置き換える:

```markdown
- 顔の口元などに MToon の影が入るときは、「顔の陰影を球に」を上げてください。顔の陰影を頭を球とみなした向きで付けるため、凹凸で影が入らなくなります。「顔を平らに」を上げるほど顔の陰影が正面向きにそろい、横からの光でも顔が半分影になりにくくなります（どちらも顔にだけ効きます）
```

spec の末尾に「## 実機検証の結果」を足し、Step 2〜4 の値と見え方を書く

- [ ] **Step 6: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-09-mtoon-maid-face-flatten-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-face-flatten.md
git commit -m "docs: 顔を平らにする設定の使い方と実機検証の結果を追加"
```

## レビュー却下メモ

- 「中心が無いとき `_HeadBack` = 0 でも w = 0 が唯一の無効化手段とコメントで明記」 — シェーダーに v2.3 で「中心が決まるまで (w = 0) は効かせない」と既に書いてある
- 「既定 0.5 を保存したファイルは将来既定を変えても 0.5 のまま」 — 他の項目（`giEqualization` など）と同じ規則で、今回の範囲で対処することはない
- 「テスト件数 85 を固定しない」 — 現在 84 件を実行で確認済み（v2.3 の実装時）。件数の一致で追加漏れも分かる
