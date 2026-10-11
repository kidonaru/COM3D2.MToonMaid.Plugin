# COM3D25.MToonMaid.Plugin 上下の丸みを消す Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 「顔の陰影を球に」の球を、頭の上下の軸を中心にした円柱へ寄せる「上下の丸みを消す」（0〜1、既定 0）を足し、口元などの横向きの影の境目を消せるようにする。

**Architecture:** シェーダーの球の法線 `d = p - c` から、頭の上向き `_HeadUp` の成分を `_FaceCylinder` の割合で抜いてから正規化する。プラグインは `_FaceCylinder` を `MaterialConverter.ApplyGroup` で、`_HeadUp` を `MaidMToonController.UpdateHeadCenter` で毎フレーム入れる。設定は継承の単位 `faceCylinder` として足す。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62（シェーダーのバンドル）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-face-cylinder-design.md`（前提として v2.5 `2026-10-09-mtoon-maid-bump-mask-design.md`）

## Global Constraints

- v2.5 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-bump-mask.md`）の Global Constraints をすべて引き継ぐ。特に: COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は実行しない、worktree 不使用、main で作業、MTEUtils は編集しない、`debug.bat` は実機検証のタスクでだけ使う、`build-bundle.bat` は Unity エディタを閉じて実行する
- 設定ファイルの版は 3 のまま。要素の無いファイルは既定 0 で読む
- 継承の単位は 18 項目（v2.5 の 17 項目 + `faceCylinder`、`FaceFlatten` の直後）
- 対象は顔のグループだけ（`ToonMaterialClassifier.UsesSphereNormal`）。ほかのグループには `_FaceCylinder` 0 を入れる
- 頭の上向きは頭のボーンのローカルの −X（`Vector3.left`）。`TransformDirection` で単位ベクトルのまま渡す
- GUIView の入力欄キャッシュは描画順の番号で引かれる。スライダーは全タブで同じ位置に描き、効かないタブでは無効表示にする（行を消さない）
- ゲームの再起動の前に、ゲームが SceneEditor のシーンを開いていたらユーザーに保存の確認をとる（2026-10-09 時点でユーザーは SceneEditor で作業中）
- 実機検証では、ユーザーが今の設定に入れている値を控えてから変え、最後に戻す。ライト・timeScale も戻す

## Review Focus

- **強さ 0 の不変**: 「上下の丸みを消す」0 では今の球と同じ陰影（`d - u * dot(d,u) * 0 = d`） → Task 2 Step 1、Task 3 Step 3
- **中心が無いとき**: `_HeadCenter.w = 0` の間は `_HeadUp` も 0 で、球自体が効かない（NaN を出さない: 円柱で `d` が上向きと平行なら長さ 0 になるが `MToon_SafeNormalize` が受ける） → Task 2 Step 1
- **上向きの軸の向き**: 頭のボーンのローカルの −X が上（試作で、メッシュ空間の +Z と一致）。違う軸だと円柱の軸が前後や左右になり、境目が横に出る → Task 3 Step 3 の見え方
- **顔以外**: 肌・髪・衣装のグループでは `_FaceCylinder` が 0、スライダーは無効表示 → Task 1 Step 3、Task 3 Step 5
- **「顔の陰影を球に」0**: 球を混ぜないので効かない（README に書く） → Task 3 Step 6

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/GroupSettingKeys.cs    (Modify: FaceCylinder)
├─ Settings/GroupSettings.cs       (Modify: faceCylinder、CopyField / FieldEquals)
├─ Rendering/MToonProps.cs         (Modify: FaceCylinder / HeadUp)
├─ Rendering/MaterialConverter.cs  (Modify: ApplyGroup)
├─ MaidMToonController.cs          (Modify: UpdateHeadCenter で _HeadUp)
├─ UI/MToonMaidWindow.cs           (Modify: スライダー 1 行)
└─ Resources/mtoon_bundle          (Rebuild)
source/COM3D25.MToonMaid.Plugin.Tests/
└─ CommonSettingsTests.cs          (Modify)
UnityProject2022/Assets/Shaders/MToon10/
├─ vrmc_materials_mtoon.shader               (Modify)
├─ vrmc_materials_mtoon_input.hlsl           (Modify)
└─ vrmc_materials_mtoon_forward_fragment.hlsl (Modify)
README.md / spec                   (Modify)
```

---

### Task 1: 設定に「上下の丸みを消す」を足す

**Files:**
- Modify: `Settings/GroupSettingKeys.cs`, `Settings/GroupSettings.cs`, `Rendering/MToonProps.cs`, `Rendering/MaterialConverter.cs`, `UI/MToonMaidWindow.cs`
- Test: `CommonSettingsTests.cs`
- Commit に spec と plan を含める

**Interfaces:**
- Produces:
  - `GroupSettingKeys.FaceCylinder = "faceCylinder"`（`All` の `FaceFlatten` の直後）
  - `GroupSettings.faceCylinder : float = 0f`
  - `MToonProps.FaceCylinder = "_FaceCylinder"`、`MToonProps.HeadUp = "_HeadUp"`

- [ ] **Step 1: 失敗するテストを書く**

`CommonSettingsTests.cs` の `ChangeAll` の `g.faceFlatten = 0.2f;` の直後に追加:

```csharp
            g.faceCylinder = 0.4f;
```

`凹凸の影を消すは既定0で要素の無いファイルも個別にしない` の前に追加:

```csharp
        [Fact]
        public void 上下の丸みを消すは既定0で要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.FaceCylinder, GroupSettingKeys.All);
            Assert.Equal(0f, GroupSettings.CreateDefault().faceCylinder);

            var v2 = Path.Combine(_dir, "v2c.xml");
            File.WriteAllText(v2,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><face><shadingShift>0.2</shadingShift></face></MToonMaid>");
            var s2 = MaidSettingsStore.Load(v2, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s2.face.overrides);
            Assert.Equal(0f, s2.Resolve(PartGroup.Face).faceCylinder);

            var v3 = Path.Combine(_dir, "v3c.xml");
            File.WriteAllText(v3,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\"><common><sphereNormal>1</sphereNormal></common></MToonMaid>");
            var s3 = MaidSettingsStore.Load(v3, _warnings.Add);

            Assert.Equal(0f, s3.common.faceCylinder);
        }
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`faceCylinder` / `GroupSettingKeys.FaceCylinder` が無い）

- [ ] **Step 3: キー・フィールド・プロパティ名・スライダーを足す**

`Settings/GroupSettingKeys.cs` の `public const string FaceFlatten = "faceFlatten";` の直後に追加:

```csharp
        public const string FaceCylinder = "faceCylinder";
```

`All` の 1 行目を次へ置き換える:

```csharp
            ShadeColor, ShadingShift, ShadingToony, GiEqualization, AdditionalLightShade, SphereNormal, FaceFlatten, FaceCylinder, FaceBumpLift, HighlightScale,
```

`Settings/GroupSettings.cs` の `public float faceFlatten = 0.5f;` の直後に追加:

```csharp
        // 球の法線から頭の上下の成分を抜く割合 (0〜1)。1 で頭の上下の軸を中心にした円柱になり、口元などに横向きの影の境目が出ない
        public float faceCylinder;
```

`CopyField` の `case GroupSettingKeys.FaceFlatten: ...` の直後に追加:

```csharp
                case GroupSettingKeys.FaceCylinder: faceCylinder = from.faceCylinder; break;
```

`FieldEquals` の `case GroupSettingKeys.FaceFlatten: ...` の直後に追加:

```csharp
                case GroupSettingKeys.FaceCylinder: return FloatEquals(faceCylinder, other.faceCylinder);
```

`Rendering/MToonProps.cs` の `public const string HeadBack = "_HeadBack";` の直後に追加:

```csharp
        public const string FaceCylinder = "_FaceCylinder";
        public const string HeadUp = "_HeadUp";
```

`Rendering/MaterialConverter.cs` の `ApplyGroup` の `m.SetFloat(MToonProps.FaceFlatten, ...)` の行の直後に追加:

```csharp
            m.SetFloat(MToonProps.FaceCylinder, usesSphere ? Mathf.Clamp01(settings.faceCylinder) : 0f);
```

`UI/MToonMaidWindow.cs` の `Slider(view, GroupSettingKeys.FaceFlatten, ...)` 行の直後（`FaceBumpLift` の行の前）に追加:

```csharp
            Slider(view, GroupSettingKeys.FaceCylinder, "上下の丸みを消す", shown.faceCylinder, 0f, 1f, def.faceCylinder, (s, v) => s.faceCylinder = v, editable: sphereEditable);
```

- [ ] **Step 4: テストとビルドを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` と `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: テスト PASS（95 + 1 = 96 件）、ビルド成功（警告 0）

- [ ] **Step 5: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin source/COM3D25.MToonMaid.Plugin.Tests docs/superpowers/specs/2026-10-09-mtoon-maid-face-cylinder-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-face-cylinder.md
git commit -m "feat: 上下の丸みを消す設定を追加"
```

---

### Task 2: シェーダーで球を円柱へ寄せ、頭の上向きを渡す

**Files:**
- Modify: シェーダー 3 ファイル、`MaidMToonController.cs`
- Rebuild: `Resources/mtoon_bundle`

**Interfaces:**
- Consumes: `MToonProps.FaceCylinder` / `HeadUp`（Task 1）
- Produces: シェーダーのプロパティ `_FaceCylinder`（Float、既定 0）/ `_HeadUp`（Vector、既定 0）

シェーダーと Unity の Transform は単体テストが無い。コンパイルは `build-bundle.bat`、動作は Task 3 で確かめる。

- [ ] **Step 1: シェーダーを変える**

`vrmc_materials_mtoon_input.hlsl` の `float4 _HeadBack;` の直後に追加:

```hlsl
float4 _HeadUp;
```

同ファイルの `half _FaceFlatten;` の直後に追加:

```hlsl
half _FaceCylinder;
```

`vrmc_materials_mtoon.shader` の `_HeadBack ("Head Back", Vector) = (0, 0, 0, 0)` の直後に追加:

```
        // COM3D25: 球の法線から頭の上下の成分を抜く割合 (1 で円柱) と、頭の上向きの単位ベクトル。どちらもプラグインが設定する
        _FaceCylinder ("Face Cylinder", Range(0, 1)) = 0
        _HeadUp ("Head Up", Vector) = (0, 0, 0, 0)
```

同ファイル先頭の改変一覧の `// - 球の中心を頭の後ろへずらして ...` の行の直後に追加:

```
// - 球の法線から頭の上下の成分を抜いて円柱へ寄せる _FaceCylinder / _HeadUp を追加
```

`vrmc_materials_mtoon_forward_fragment.hlsl` の次の行:

```hlsl
    const float3 sphereNormalWS = MToon_SafeNormalize(input.positionWS - sphereCenterWS);
```

を次へ置き換える:

```hlsl
    // 上下の成分を抜くほど頭の上下の軸を中心にした円柱へ近づき、陰影の境目が縦の線だけになる (口元などに横向きの境目が出ない)
    const float3 sphereDirWS = input.positionWS - sphereCenterWS;
    const float3 sphereNormalWS = MToon_SafeNormalize(sphereDirWS - _HeadUp.xyz * (dot(sphereDirWS, _HeadUp.xyz) * _FaceCylinder));
```

- [ ] **Step 2: 宣言漏れを確かめる**

Run: `grep -rn "_FaceCylinder\|_HeadUp" UnityProject2022/Assets/Shaders/MToon10`
Expected: `.shader` にプロパティ 2 行と改変一覧・コメントの計 4 行前後、`input.hlsl` に 2 行、`forward_fragment.hlsl` に 1 行

- [ ] **Step 3: バンドルを作り直す**

Run: `md5sum source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`、続けて `cmd //c ".\\build-bundle.bat"`
Expected: `コピーしました: ...`。`grep -c "Shader error" UnityProject2022/Logs/build-bundle.log` が 0。`md5sum` が変わっている

- [ ] **Step 4: 頭の上向きを毎フレーム入れる**

`MaidMToonController.cs` の `private static readonly int HeadBackId = ...;` の直後に追加:

```csharp
        private static readonly int HeadUpId = Shader.PropertyToID(MToonProps.HeadUp);
```

`private static readonly Vector3 HeadBackLocal = ...;` の直後に追加:

```csharp
        // 頭の上向き (頭のボーンのローカル)。-X が頭頂 (実機で確認)
        private static readonly Vector3 HeadUpLocal = Vector3.left;
```

`UpdateHeadCenter` の XML コメントの 1 行目を `顔の変換済みマテリアルへ頭の中心・後ろ向き・上向きのベクトルを入れる。...` に変え、本体を次のように変える:

- `var back = Vector4.zero;` の直後に `var up = Vector4.zero;` を足す
- `back = new Vector4(b.x, b.y, b.z, 0f);` の直後に次を足す:

```csharp
                // シェーダーで内積から成分を抜くため、大きさに依らない単位ベクトルにする
                var u = head.TransformDirection(HeadUpLocal);
                up = new Vector4(u.x, u.y, u.z, 0f);
```

- `e.material.SetVector(HeadBackId, back);` の直後に `e.material.SetVector(HeadUpId, up);` を足す

- [ ] **Step 5: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（96 件）

- [ ] **Step 6: Commit**

```bash
git add UnityProject2022/Assets/Shaders/MToon10 source/COM3D25.MToonMaid.Plugin
git commit -m "feat: 球の法線から頭の上下の成分を抜いて円柱へ寄せる"
```

---

### Task 3: 実機検証と仕上げ

**restart-verify スキル**の手順で、ゲーム終了 → `debug.bat` → 起動 → 最新セーブのロード。**終了の前に、ゲームが SceneEditor のシーンを開いているならユーザーに保存の確認をとる**（承認があるまで終了しない）。eval は完全修飾名、ローカル関数は使わず `System.Func` / `System.Action`。比較の撮影では `UnityEngine.Time.timeScale = 0` で動きを止め、光は頭に対して決める。顔が UI の後ろに隠れる場合は `capture` の `target: "camera:CameraMain"` で撮る。検証で変えた状態（設定・ライト・timeScale）は最後に戻す。

**Files:**
- Modify: `README.md`、`docs/superpowers/specs/2026-10-09-mtoon-maid-face-cylinder-design.md`（検証の結果）

- [ ] **Step 1: DLL を配置して起動し、セーブをロードする**

restart-verify スキルに従う。DLL のハッシュが一致し、`LogOutput.log` に `MToonMaid 有効化` があること

- [ ] **Step 2: プロパティが入ることを確かめる**

```csharp
var maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
var plugin = UnityEngine.Object.FindObjectOfType<COM3D25.MToonMaid.Plugin.MToonMaidPlugin>();
var s = plugin.GetSettings(maid0); var wasEnabled = s.enabled; plugin.SetEnabled(maid0, true);
var userSphere = s.common.sphereNormal; var userFlatten = s.common.faceFlatten; var userCyl = s.common.faceCylinder; var userBump = s.common.faceBumpLift;
System.Action NotifyAll = () => { foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g); };
var head = maid0.body0.trsHead;
var hs = maid0.body0.goSlot[(int)TBody.hashSlotName["head"]]; var smr = hs.obj.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
System.Func<string> Props = () => { var sb = new System.Text.StringBuilder(); var ms = smr.sharedMaterials;
  for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].shader.name.Contains("MToon")) sb.Append(i + ":" + ms[i].name + " cyl=" + ms[i].GetFloat("_FaceCylinder") + " up=" + ms[i].GetVector("_HeadUp").ToString("F3") + "; ");
  return sb.ToString(); };
s.common.faceCylinder = 1f; NotifyAll();
"user: sphere=" + userSphere + " flatten=" + userFlatten + " cyl=" + userCyl + " bump=" + userBump + " | " + Props() + " headUp=" + head.TransformDirection(UnityEngine.Vector3.left).ToString("F3")
```

Expected: 顔の MToon（肌・口）が `cyl=1`、`up` が `headUp` と一致（長さ 1）

顔以外のスロットが 0 のままであることを確かめる:

```csharp
float maxOther = 0f; int otherCount = 0; var slots = maid0.body0.goSlot;
for (int i = 0; i < slots.Count; i++) { var sk = slots[i, 0]; if (sk == null || sk.obj == null || sk.Category == "head") continue;
  foreach (var r in sk.obj.GetComponentsInChildren<UnityEngine.Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null && m.shader.name.Contains("MToon")) { otherCount++; maxOther = System.Math.Max(maxOther, m.GetFloat("_FaceCylinder")); } }
"other MToon=" + otherCount + " maxCyl=" + maxOther
```

Expected: `other MToon` が 1 以上、`maxCyl=0`

- [ ] **Step 3: 見え方を比べる**

動きを止め、「顔の陰影を球に」1・「顔を平らに」0・「凹凸の影を消す」0 にして、頭に対して横（前寄り）からの光と、前寄りの上からの光で、「上下の丸みを消す」0 / 0.5 / 1 を同じ rect で撮る:

```csharp
var lt = GameMain.Instance.MainLight.GetComponent<UnityEngine.Light>(); var origRot = lt.transform.rotation; var origTs = UnityEngine.Time.timeScale;
s.common.sphereNormal = 1f; s.common.faceFlatten = 0f; s.common.faceBumpLift = 0f; s.common.faceCylinder = 0f; NotifyAll(); UnityEngine.Time.timeScale = 0f;
var hf = head.TransformDirection(UnityEngine.Vector3.up); var hu = head.TransformDirection(UnityEngine.Vector3.left); var hr = UnityEngine.Vector3.Cross(hu, hf);
lt.transform.rotation = UnityEngine.Quaternion.LookRotation((hr * 0.75f - hf * 0.6f - hu * 0.3f).normalized);
var cam = UnityEngine.Camera.main; var sp = cam.WorldToScreenPoint(head.position); var sp2 = cam.WorldToScreenPoint(head.position + cam.transform.up * 0.12f); var h = System.Math.Abs(sp2.y - sp.y) * 1.3f;
((int)System.Math.Max(0, sp.x - h)) + "," + ((int)System.Math.Max(0, UnityEngine.Screen.height - sp.y - h)) + "," + (int)(2 * h) + "," + (int)(2 * h)
```

上からの光は `lt.transform.rotation = UnityEngine.Quaternion.LookRotation((-hu * 0.8f - hf * 0.5f + hr * 0.2f).normalized);`。

Expected:
- 「上下の丸みを消す」0 は v2.5 の球と同じ見え方（強さ 0 の不変）
- 1 では横からの光で顔を縦に分ける境目、上からの光で口の上下に横向きの境目が出ない（試作と同じ）
- 境目が横向きに出たら上向きの軸が違う。`HeadUpLocal` を疑う

ユーザーの実際の設定（Step 2 で控えた値）でも 0 / 1 を撮り、効き方を spec に書く

- [ ] **Step 4: 戻す**

```csharp
s.common.sphereNormal = userSphere; s.common.faceFlatten = userFlatten; s.common.faceCylinder = userCyl; s.common.faceBumpLift = userBump; NotifyAll();
lt.transform.rotation = origRot; UnityEngine.Time.timeScale = origTs; plugin.SetEnabled(maid0, wasEnabled);
"sphere=" + s.common.sphereNormal + " flatten=" + s.common.faceFlatten + " cyl=" + s.common.faceCylinder + " bump=" + s.common.faceBumpLift + " ts=" + UnityEngine.Time.timeScale + " light=" + (lt.transform.rotation == origRot)
```

Expected: ユーザーの元の値、`ts` が元の値、`light=True`

- [ ] **Step 5: ウィンドウを確かめる**

```csharp
var wm = (COM3D25.MToonMaid.Plugin.WindowManager)typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).GetField("_windowManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
var tabField = typeof(COM3D25.MToonMaid.Plugin.MToonMaidWindow).GetField("_tab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var wasShown = wm.MainWindow.isShowWnd; var wasTab = tabField.GetValue(wm.MainWindow); wm.MainWindow.isShowWnd = true; "shown"
```

`screenshot`（`window: "MToonMaid"`）で、共通タブの「顔を平らに」の下・「凹凸の影を消す」の上に「上下の丸みを消す」があること。`tabField.SetValue(wm.MainWindow, 1)`（肌）と `3`（髪）で行が個別のトグルを含めて無効表示、`2`（顔）で有効であること。確かめたら `tabField.SetValue(wm.MainWindow, wasTab)`、`isShowWnd = wasShown`。`LogOutput.log` に MToonMaid 由来の例外が無いこと

- [ ] **Step 6: README と spec を更新する**

`README.md` の「制限」の「顔の立体的な陰影を残したまま鼻や口元の影だけを消したいときは…」の行の直前に追加:

```markdown
- 「顔の陰影を球に」を上げても上からの光で口元などに横向きの影の境目が出るときは、「上下の丸みを消す」を上げてください。陰影の球を頭の上下の軸を中心にした円柱へ寄せ、境目を顔を縦に分ける線だけにします。上げるほど上や下からの光で顔の上下の明暗が付かなくなります。「顔の陰影を球に」が 0 のときは効きません（顔にだけ効きます）。上下の軸は標準の頭のボーンの向きで決めているため、ボーンの向きが違うメイドでは効き方が変わることがあります
```

spec の末尾に「## 実機検証の結果」を足し、Step 2〜5 の値と見え方を書く

- [ ] **Step 7: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-09-mtoon-maid-face-cylinder-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-face-cylinder.md
git commit -m "docs: 上下の丸みを消す設定の使い方と実機検証の結果を追加"
```

## レビュー却下メモ

- 「中心が無いとき (_HeadUp = 0) の見え方を検証手順に足す」 — `_HeadCenter.w = 0` で球の混ぜ自体が 0 になり、`_HeadUp = 0` でも `d - 0 = d` で今の球と同じ。既存の `_HeadBack` と同じ扱いで、新しい壊れ方が無い
- 「変換直後の 1 フレームは `_HeadUp` が 0」 — 0 では今の球と同じ見え方で壊れない。`_HeadBack` と同じ経路
- 「検証中の例外で timeScale が戻らない」 — 検証は対話で 1 手ずつ進め、Step 4 で必ず戻す運用。例外時も次の eval で戻せる
- 「Task 1 の時点ではシェーダーに `_FaceCylinder` が無い」 — `SetFloat` は無いプロパティを無視する。Task 2 までを 1 回で実装する

## コードレビュー却下メモ

- 「円柱の軸の上で陰影の法線がほぼ 0 になる」 — 軸は頭の中心（「顔を平らに」でさらに後ろ）を通る上下の直線で、顔の肌・口のメッシュは顔の前面だけを覆い頭頂に届かない。NaN も出ない
- 「「顔の陰影を球に」0 でもスライダーが有効表示」 — 既存の「顔を平らに」と同じ扱い。README に「球が 0 のときは効かない」と書いた
- 「設定ごとに同形のテストが増える」 — 既存のテストの形にそろえた。項目が増えたら Theory 化を考える
