# COM3D25.MToonMaid.Plugin 顔の陰影を球に寄せる Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 顔の陰影に使う法線を頭の球面の法線へ寄せる「顔の陰影を球に寄せる」（0〜1、既定 0）を足し、口元などの凹凸で入る影を消せるようにする。

**Architecture:** シェーダーは陰影用の法線 `shadingNormalWS = normalize(lerp(N, normalize(positionWS - _HeadCenter.xyz), _SphereNormal * _HeadCenter.w))` を作り、`N·L` だけに使う。設定は `GroupSettings.sphereNormal` を継承の単位の 15 項目目として足し、`MaterialConverter.ApplyGroup` が顔のグループにだけ `_SphereNormal` を入れる（他は 0）。頭の中心は、頭のスロットの GameObject が変わったときに描画のバウンズの中心を頭のボーンの相対位置として控え、`LateUpdate` で世界座標へ戻して顔の変換済みマテリアルへ `_HeadCenter`（w = 1）として入れる。控えられていない間は (0,0,0,0) で、w = 0 のため効かない。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ HLSL（Built-in RP、Unity 2022.3.62）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-sphere-normal-design.md`（前提として `2026-10-09-mtoon-maid-additional-light-design.md`）

## Global Constraints

- v2.2 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-additional-light.md`）の Global Constraints をすべて引き継ぐ。特に: COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は実行しない、worktree 不使用、main で作業、MTEUtils は編集しない、`debug.bat` は実機検証のタスクでだけ使う、`build-bundle.bat` は Unity エディタを閉じて実行する
- 設定ファイルの版は 3 のまま。要素の無いファイルは既定 0 で読む
- 継承の単位は 15 項目（v2.2 の 14 項目 + `sphereNormal`）
- 混ぜた法線は `N·L` だけに使う。環境光（SH）、リム、天使の輪、アウトラインの幅はメッシュの法線のまま
- `_SphereNormal` を入れるのは顔のグループだけ。肌・髪・衣装には常に 0（髪は毛先が頭から遠く、放射方向の陰影が不自然になるため）
- GUIView の入力欄キャッシュは描画順の番号で引かれる。スライダーは全タブで同じ位置に描き、効かないタブでは無効表示にする（行を消さない）
- ゲームの再起動の前に、ゲームが SceneEditor のシーンを開いていたらユーザーに保存の確認をとる（未保存の編集が失われるため）

## Review Focus

- **既定 0 の見た目の不変**: 強さ 0 のとき陰影用の法線がメッシュの法線と一致すること。`_HeadCenter` が未設定（0,0,0）でも NaN を出さないこと（`normalize` の 0 ベクトル、`lerp` に NaN が混ざると 0 を掛けても NaN） → Task 2 Step 1 の安全な正規化、Task 3 Step 3
- **顔以外に効かない**: 共通の値を上げても肌・髪・衣装のマテリアルは 0 → Task 1 の `UsesSphereNormal` のテスト、Task 3 Step 4
- **中心が決まる前**: 変換直後や頭のスロットが無いとき、`_HeadCenter.w` が 0 で効かない（原点からの球で陰影されない） → Task 2 Step 1 の `_SphereNormal * _HeadCenter.w`、Task 2 Step 4 の (0,0,0,0)
- **頭が動いても中心が追従する**: モーション中も `_HeadCenter` が頭と一緒に動く（走査の間隔を待たない） → Task 3 Step 4（2 回読んで頭の位置との差が一定）
- **中心の安定**: 走査のたびにバウンズを取り直すと表情や頭の傾きで中心が揺れる。頭のスロットの GameObject が変わったときだけ控え、大きさ 0・頭のボーンから 0.3 以上離れた値は採用しない → Task 2 Step 4 の `CaptureHeadCenter`
- **写し漏れ**: `CopyField` / `FieldEquals` への追加漏れ → Task 1 の `全項目を個別にすると…`（`ChangeAll` に足す）

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/GroupSettingKeys.cs   (Modify: SphereNormal)
├─ Settings/GroupSettings.cs      (Modify: sphereNormal、CopyField / FieldEquals)
├─ ToonMaterialClassifier.cs      (Modify: UsesSphereNormal)
├─ Rendering/MToonProps.cs        (Modify: SphereNormal / HeadCenter)
├─ Rendering/MaterialConverter.cs (Modify: ApplyGroup で SetFloat)
├─ MaidMToonController.cs         (Modify: 頭の中心を控える・毎フレーム入れる)
├─ MToonMaidPlugin.cs             (Modify: LateUpdate で UpdateHeadCenter)
├─ UI/MToonMaidWindow.cs          (Modify: スライダー 1 行、Slider に editable)
└─ Resources/mtoon_bundle         (Rebuild)
source/COM3D25.MToonMaid.Plugin.Tests/
├─ CommonSettingsTests.cs         (Modify)
└─ ToonMaterialClassifierTests.cs (Modify)
UnityProject2022/Assets/Shaders/MToon10/
├─ vrmc_materials_mtoon.shader             (Modify: プロパティ、改変一覧)
├─ vrmc_materials_mtoon_input.hlsl         (Modify: 宣言)
├─ vrmc_materials_mtoon_forward_fragment.hlsl (Modify: 陰影用の法線)
└─ vrmc_materials_mtoon_lighting_mtoon.hlsl   (Modify: MToonInput と DotNL)
README.md / spec                   (Modify)
```

---

### Task 1: 設定に「顔の陰影を球に寄せる」を足す

**Files:**
- Modify: `Settings/GroupSettingKeys.cs`, `Settings/GroupSettings.cs`, `ToonMaterialClassifier.cs`, `Rendering/MToonProps.cs`, `Rendering/MaterialConverter.cs`, `UI/MToonMaidWindow.cs`
- Test: `CommonSettingsTests.cs`, `ToonMaterialClassifierTests.cs`

**Interfaces:**
- Produces:
  - `GroupSettingKeys.SphereNormal = "sphereNormal"`（`All` の `AdditionalLightShade` の直後）
  - `GroupSettings.sphereNormal : float = 0f`
  - `ToonMaterialClassifier.UsesSphereNormal(PartGroup) : bool`（Face だけ true）
  - `MToonProps.SphereNormal = "_SphereNormal"`、`MToonProps.HeadCenter = "_HeadCenter"`（Task 2 のシェーダーと一致させる）

- [ ] **Step 1: 失敗するテストを書く**

`CommonSettingsTests.cs` の `ChangeAll` の `g.additionalLightShade = 0.9f;` の直後に追加:

```csharp
            g.sphereNormal = 0.5f;
```

`個別を外してから再び個別にすると共通の値から始まる` の前に追加:

```csharp
        [Fact]
        public void 陰影を球に寄せるは既定0で要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.SphereNormal, GroupSettingKeys.All);
            Assert.Equal(0f, GroupSettings.CreateDefault().sphereNormal);

            var v2 = Path.Combine(_dir, "v2f.xml");
            File.WriteAllText(v2,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><face><shadingShift>0.2</shadingShift></face></MToonMaid>");
            var s2 = MaidSettingsStore.Load(v2, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s2.face.overrides);
            Assert.Equal(0f, s2.Resolve(PartGroup.Face).sphereNormal);
        }
```

`ToonMaterialClassifierTests.cs` のクラス末尾に追加:

```csharp
        [Theory]
        [InlineData(PartGroup.Face, true)]
        [InlineData(PartGroup.Hair, false)]
        [InlineData(PartGroup.Skin, false)]
        [InlineData(PartGroup.Costume, false)]
        public void 陰影を球に寄せるのは顔だけ(PartGroup group, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.UsesSphereNormal(group));
        }
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`sphereNormal` / `GroupSettingKeys.SphereNormal` / `UsesSphereNormal` が無い）

- [ ] **Step 3: キー・フィールド・判定を足す**

`Settings/GroupSettingKeys.cs` の `public const string AdditionalLightShade = "additionalLightShade";` の直後に追加:

```csharp
        public const string SphereNormal = "sphereNormal";
```

`All` の 1 行目を次へ置き換える:

```csharp
            ShadeColor, ShadingShift, ShadingToony, GiEqualization, AdditionalLightShade, SphereNormal, HighlightScale,
```

`Settings/GroupSettings.cs` の `public float additionalLightShade = 1f;` の直後に追加:

```csharp
        // 陰影 (N・L) の法線を頭の球面の法線へ寄せる強さ。口元などの凹凸で入る影を消す。顔のグループにだけ効く
        public float sphereNormal;
```

`CopyField` の `case GroupSettingKeys.AdditionalLightShade: ...` の直後に追加:

```csharp
                case GroupSettingKeys.SphereNormal: sphereNormal = from.sphereNormal; break;
```

`FieldEquals` の `case GroupSettingKeys.AdditionalLightShade: ...` の直後に追加:

```csharp
                case GroupSettingKeys.SphereNormal: return FloatEquals(sphereNormal, other.sphereNormal);
```

`ToonMaterialClassifier.cs` の `GetGroup` の後ろに追加:

```csharp
        /// <summary>
        /// 陰影を頭の球面へ寄せるグループ。髪は毛先が頭から遠く、頭の中心からの放射方向では不自然になるため含めない
        /// </summary>
        public static bool UsesSphereNormal(PartGroup group)
        {
            return group == PartGroup.Face;
        }
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（79 + 1 + 4 = 84 件）

- [ ] **Step 5: マテリアルとウィンドウへつなぐ**

`Rendering/MToonProps.cs` の `public const string AdditionalLightShade = "_AdditionalLightShade";` の直後に追加:

```csharp
        public const string SphereNormal = "_SphereNormal";
        public const string HeadCenter = "_HeadCenter";
```

`Rendering/MaterialConverter.cs` の `ApplyGroup` の `_AdditionalLightShade` を入れる行の直後に追加:

```csharp
            // 頭の中心は顔のマテリアルにしか入れないため、他のグループでは効かせない
            m.SetFloat(MToonProps.SphereNormal, ToonMaterialClassifier.UsesSphereNormal(backup.group) ? Mathf.Clamp01(settings.sphereNormal) : 0f);
```

`UI/MToonMaidWindow.cs` の `Slider` を次へ置き換える（`editable` を足し、行全体を無効表示にできるようにする）:

```csharp
        private void Slider(GUIView view, string key, string label, float value, float min, float max, float defaultValue,
            Action<GroupSettings, float> set, FloatFieldType fieldType = FloatFieldType.Float, bool editable = true)
        {
            view.SetEnabled(_baseEnabled && editable);
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
            view.SetEnabled(_baseEnabled);
        }
```

`DrawSettings` の「追加ライトの影側」の `Slider(...)` 行の直後に追加:

```csharp
            // 顔以外には効かないため個別のトグルを含めて無効表示にする (行を消すと入力欄キャッシュの番号がずれる)
            var sphereEditable = !_editGroup.HasValue || ToonMaterialClassifier.UsesSphereNormal(_editGroup.Value);
            Slider(view, GroupSettingKeys.SphereNormal, "顔の陰影を球に", shown.sphereNormal, 0f, 1f, def.sphereNormal, (s, v) => s.sphereNormal = v, editable: sphereEditable);
```

- [ ] **Step 6: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（84 件）

- [ ] **Step 7: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin source/COM3D25.MToonMaid.Plugin.Tests
git commit -m "feat: 顔の陰影を頭の球面へ寄せる設定を追加"
```

---

### Task 2: シェーダーの陰影用の法線と、頭の中心の受け渡し

**Files:**
- Modify: シェーダー 4 ファイル、`MaidMToonController.cs`、`MToonMaidPlugin.cs`
- Rebuild: `Resources/mtoon_bundle`

**Interfaces:**
- Consumes: `MToonProps.SphereNormal` / `HeadCenter`、`ToonMaterialClassifier.UsesSphereNormal`（Task 1）
- Produces: シェーダーのプロパティ `_SphereNormal`（Float、既定 0）/ `_HeadCenter`（Vector）、`MaidMToonController.UpdateHeadCenter()`

シェーダーと Unity の Transform は単体テストが無い。コンパイルは `build-bundle.bat`、動作は Task 3 で確かめる。

- [ ] **Step 1: シェーダーを変える**

`vrmc_materials_mtoon_input.hlsl` の `half4 _ToonRampLitColor;` の直後に追加:

```hlsl
float4 _HeadCenter;
```

同ファイルの `half _AdditionalLightShade;` の直後に追加:

```hlsl
half _SphereNormal;
```

`vrmc_materials_mtoon.shader` の `_AdditionalLightShade (...)` の行の直後に追加:

```
        // COM3D25: 陰影の法線を頭の球面へ寄せる強さと、頭の中心 (xyz、w は中心が有効なら 1)。どちらもプラグインが設定する
        _SphereNormal ("Sphere Normal", Range(0, 1)) = 0
        _HeadCenter ("Head Center", Vector) = (0, 0, 0, 0)
```

同ファイル先頭の改変一覧の `// - 追加ライト (ForwardAdd) ...` の行の直後に追加:

```
// - 陰影 (N・L) の法線を頭の球面の法線へ寄せる _SphereNormal / _HeadCenter を追加
```

`vrmc_materials_mtoon_lighting_mtoon.hlsl` の `struct MToonInput` の `half3 normalWS;` の直後に追加:

```hlsl
    // COM3D25: トゥーンの陰影 (N・L) にだけ使う法線。頭の球面へ寄せたもの
    half3 shadingNormalWS;
```

同ファイルの `GetMToonLighting_DotNL` の本体を次へ置き換える:

```hlsl
    return dot(input.shadingNormalWS, lighting.directLightDirection);
```

`vrmc_materials_mtoon_forward_fragment.hlsl` の `mtoonInput.normalWS = normalWS;` の直後に追加:

```hlsl
    // COM3D25: 陰影の法線を頭の球面へ寄せる (口元などの凹凸で影が入らないように)。
    // 0 ベクトルの正規化で NaN を出さないよう長さの下限を設ける (lerp に NaN が混ざると強さ 0 でも NaN になる)
    const float3 toSurface = input.positionWS - _HeadCenter.xyz;
    const float3 sphereNormalWS = toSurface * rsqrt(max(dot(toSurface, toSurface), 1e-8));
    // 中心が決まるまで (w = 0) は効かせない (原点からの球で陰影しない)
    const float3 mixedNormalWS = lerp(normalWS, sphereNormalWS, _SphereNormal * _HeadCenter.w);
    mtoonInput.shadingNormalWS = mixedNormalWS * rsqrt(max(dot(mixedNormalWS, mixedNormalWS), 1e-8));
```

- [ ] **Step 2: 宣言漏れを確かめる**

Run: `grep -rn "_SphereNormal\|_HeadCenter\|shadingNormalWS" UnityProject2022/Assets/Shaders/MToon10`
Expected: `.shader` にプロパティ 2 行と改変一覧 1 行、`input.hlsl` に 2 行、`lighting_mtoon.hlsl` に `shadingNormalWS` の宣言と DotNL の 2 行、`forward_fragment.hlsl` に 4 行前後。`mtoonInput` を組み立てる箇所が `forward_fragment.hlsl` だけであること（`grep -rn "MToonInput mtoonInput" UnityProject2022/Assets/Shaders/MToon10` が 1 件。他にあれば同じ代入を足す）

- [ ] **Step 3: バンドルを作り直す**

Run: `md5sum source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`、続けて `cmd //c ".\\build-bundle.bat"`
Expected: `コピーしました: ...`。`grep -c "Shader error" UnityProject2022/Logs/build-bundle.log` が 0。`md5sum` が変わっている

- [ ] **Step 4: 頭の中心を控えて毎フレーム入れる**

`MaidMToonController.cs` のフィールドの `private bool _rampRefreshWarned;` の直後に追加:

```csharp
        // 頭のスロットの描画の中心を頭のボーンから見た位置と、控えたときの頭のスロットの GameObject。
        // 走査のたびに取り直すと表情や頭の傾きでバウンズが揺れて陰影の境界がちらつくため、GameObject が変わったときだけ控える
        private Vector3? _headLocalCenter;
        private GameObject _headObject;
        private static readonly int HeadCenterId = Shader.PropertyToID(MToonProps.HeadCenter);
        // 頭のボーンからこれより離れた中心は描画のバウンズが壊れている (更新前など) とみなして採用しない
        private const float MaxHeadCenterDistance = 0.3f;
```

`Scan` の `var slots = Maid.body0.goSlot;` の直後に追加:

```csharp
            var headFound = false;
```

`Scan` の `skin.obj.GetComponentsInChildren(true, _rendererBuffer);` の直後に追加（子スロットや非表示の別の頭で後勝ちしないよう、親スロットで表示中のものだけを使う）:

```csharp
                    if (j == 0 && skin.Category == "head" && skin.obj.activeInHierarchy)
                    {
                        headFound = true;
                        if (skin.obj != _headObject)
                        {
                            CaptureHeadCenter(skin.obj);
                        }
                    }
```

`Scan` の外側の `for` ループの閉じ括弧の直後（メソッドの末尾）に追加:

```csharp
            if (!headFound)
            {
                // 頭が無くなったら古い中心で陰影しない
                _headObject = null;
                _headLocalCenter = null;
            }
```

`ScanMaterial` の前にメソッドを足す:

```csharp
        private void CaptureHeadCenter(GameObject headObject)
        {
            var head = Maid.body0.trsHead;
            if (head == null || _rendererBuffer.Count == 0)
            {
                return;
            }
            var bounds = _rendererBuffer[0].bounds;
            for (var r = 1; r < _rendererBuffer.Count; r++)
            {
                bounds.Encapsulate(_rendererBuffer[r].bounds);
            }
            // 採用しなければ _headObject を更新せず、次の走査で取り直す
            if (bounds.size == Vector3.zero || Vector3.Distance(bounds.center, head.position) > MaxHeadCenterDistance)
            {
                return;
            }
            _headLocalCenter = head.InverseTransformPoint(bounds.center);
            _headObject = headObject;
        }

        /// <summary>
        /// 顔の変換済みマテリアルへ頭の中心を入れる。頭はモーションで毎フレーム動くため、走査の間隔を待たずに LateUpdate で呼ぶ。
        /// 中心が無いときは w = 0 を入れ、シェーダーで効かせない
        /// </summary>
        public void UpdateHeadCenter()
        {
            var center = Vector4.zero;
            var head = Maid.body0 != null ? Maid.body0.trsHead : null;
            if (_headLocalCenter.HasValue && head != null)
            {
                var p = head.TransformPoint(_headLocalCenter.Value);
                center = new Vector4(p.x, p.y, p.z, 1f);
            }
            foreach (var e in _entries.Values)
            {
                if (e.material != null && ToonMaterialClassifier.UsesSphereNormal(e.backup.group) && e.material.shader == _converter.MToonShader)
                {
                    e.material.SetVector(HeadCenterId, center);
                }
            }
        }
```

`MToonMaidPlugin.cs` の `LateUpdate` を次へ置き換える:

```csharp
        private void LateUpdate()
        {
            _windowManager?.LateUpdate();
            foreach (var pair in _controllers)
            {
                // 破棄済みの Maid は次の Update の TickControllers が取り除く
                if (pair.Key != null)
                {
                    pair.Value.UpdateHeadCenter();
                }
            }
        }
```

- [ ] **Step 5: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（84 件）

- [ ] **Step 6: Commit**

```bash
git add UnityProject2022/Assets/Shaders/MToon10 source/COM3D25.MToonMaid.Plugin
git commit -m "feat: 顔の陰影の法線を頭の球面へ寄せる"
```

---

### Task 3: 実機検証と仕上げ

**restart-verify スキル**の手順で、ゲーム終了 → `debug.bat` → 起動 → 最新セーブのロード。**終了の前に、ゲームが SceneEditor のシーンを開いているならユーザーに保存の確認をとる**。eval は完全修飾名、ローカル関数は使わず `System.Func` / `System.Action`。検証で変えた状態は最後に戻す。

**Files:**
- Modify: `README.md`、`docs/superpowers/specs/2026-10-09-mtoon-maid-sphere-normal-design.md`（検証の結果）

- [ ] **Step 1: DLL を配置して起動し、セーブをロードする**

restart-verify スキルに従う。DLL のハッシュが一致し、`LogOutput.log` に `MToonMaid 有効化` があること

- [ ] **Step 2: プロパティと頭の中心を確かめる**

```csharp
var maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
var plugin = UnityEngine.Object.FindObjectOfType<COM3D25.MToonMaid.Plugin.MToonMaidPlugin>();
var s = plugin.GetSettings(maid0); var wasEnabled = s.enabled; plugin.SetEnabled(maid0, true);
System.Func<string, UnityEngine.Material> MToonOf = prefix => { var sl = maid0.body0.goSlot;
  for (int i = 0; i < sl.Count; i++) for (int j = 0; j < sl.CountChildren(i); j++) { var sk = sl[i, j]; if (sk == null || sk.obj == null || !sk.Category.StartsWith(prefix)) continue;
    foreach (var r in sk.obj.GetComponentsInChildren<UnityEngine.Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null && m.shader.name.Contains("MToon")) return m; }
  return null; };
System.Action NotifyAll = () => { foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g); };
var hc = MToonOf("head").GetVector("_HeadCenter");
"has=" + MToonOf("head").HasProperty("_SphereNormal") + " head=" + MToonOf("head").GetFloat("_SphereNormal") + " w=" + hc.w + " centerToHeadBone=" + UnityEngine.Vector3.Distance((UnityEngine.Vector3)hc, maid0.body0.trsHead.position).ToString("F3") + " bodyCenter=" + MToonOf("body").GetVector("_HeadCenter") + " hairCenter=" + MToonOf("hairF").GetVector("_HeadCenter")
```

Expected: `has=True head=0 w=1`、`centerToHeadBone` が 0.03〜0.15（頭のボーンの近く。v2.3 の調査では約 0.066）、`bodyCenter` と `hairCenter` が `(0.0, 0.0, 0.0, 0.0)`（顔以外には入れない）

- [ ] **Step 3: 既定 0 の見た目が変わらないことを確かめる**

`screenshot` で顔を拡大して撮り、v2.2 の検証と同じく口元から頬に影が入る見た目（強さ 0 では今までどおり）であること。顔に黒や白の点（NaN）が出ていないこと

- [ ] **Step 4: 顔を球に寄せて確かめる**

```csharp
var key = COM3D25.MToonMaid.Plugin.GroupSettingKeys.SphereNormal;
s.SetOverride(COM3D25.MToonMaid.Plugin.PartGroup.Face, key, true); s.face.sphereNormal = 1f;
s.common.sphereNormal = 1f; NotifyAll();
"head=" + MToonOf("head").GetFloat("_SphereNormal") + " hair=" + MToonOf("hairF").GetFloat("_SphereNormal") + " body=" + MToonOf("body").GetFloat("_SphereNormal")
```

Expected: `head=1 hair=0 body=0`（共通を 1 にしても顔以外は 0）。`screenshot` で口元から頬の影が消え、顔全体が光の向きに合わせてなめらかに明暗していること

頭の中心が頭に追従することを確かめる（2 回の読みで、頭のボーンとの距離が変わらない）:

```csharp
"d=" + UnityEngine.Vector3.Distance(MToonOf("head").GetVector("_HeadCenter"), maid0.body0.trsHead.position).ToString("F4") + " pos=" + maid0.body0.trsHead.position.ToString("F3")
```

Expected: 少し間を空けて 2 回実行し、`pos` が変わっていても `d` が同じ（小数 3 桁まで）

戻す:

```csharp
s.face.sphereNormal = 0f; s.SetOverride(COM3D25.MToonMaid.Plugin.PartGroup.Face, key, false); s.common.sphereNormal = 0f; NotifyAll();
plugin.SetEnabled(maid0, wasEnabled);
"face overridden=" + s.face.IsOverridden(key) + " common=" + s.common.sphereNormal
```

- [ ] **Step 5: ウィンドウを確かめる**

```csharp
var wm = (COM3D25.MToonMaid.Plugin.WindowManager)typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).GetField("_windowManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
var tabField = typeof(COM3D25.MToonMaid.Plugin.MToonMaidWindow).GetField("_tab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
wm.MainWindow.isShowWnd = true; "shown"
```

`screenshot`（`window: "MToonMaid"`）で、共通タブの「追加ライトの影側」の下に「顔の陰影を球に」（0.00）があること。`tabField.SetValue(wm.MainWindow, 1)`（肌タブ）と `3`（髪タブ）でその行が個別のトグルを含めて無効表示、`2`（顔タブ）で有効であること。`tail_log` に MToonMaid 由来の例外が無いこと。確かめたら `tabField.SetValue(wm.MainWindow, 0)`、`isShowWnd = false`

- [ ] **Step 6: README と spec を更新する**

`README.md` の「使い方」の 4 の後ろに 1 文を足す代わりに、「制限」の追加ライトの行の直後に追加:

```markdown
- 顔の口元などに MToon の影が入るときは、「顔の陰影を球に」を上げてください。顔の陰影を頭を球とみなした向きで付けるため、凹凸で影が入らなくなります（顔にだけ効きます）
```

spec の末尾に「## 実機検証の結果」を足し、Step 2〜5 の値と見え方を書く

- [ ] **Step 7: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-09-mtoon-maid-sphere-normal-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-sphere-normal.md
git commit -m "docs: 顔の陰影を球に寄せる設定の使い方と実機検証の結果を追加"
```

## レビュー却下メモ

- 「LateUpdate より後に IK が頭を動かすと 1 フレーム遅れる。onBeforeRender で入れる」 — 球の中心の 1 フレームのずれは陰影の向きにほぼ影響しない。spec に許容と明記した
- 「髪カードの裏面で球面の法線が facing で反転しない」 — 髪を対象から外したため該当しない。顔のメッシュは裏面が見えない
- 「肌・髪・衣装タブの無効表示で共通の値がそのまま見える」 — 他の行と同じく解決後の値を出す規則どおり。無効表示で効かないことは分かる
