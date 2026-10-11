# 顔の影を SceneEditor から MToonMaid へ移す 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task（CLAUDE.md の指定により subagent-driven-development は使わない）. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 顔（head スロット）の「影を受ける」「影を落とす」を、SceneEditor の上書き・キー・プリセットから外し、MToonMaid のメイドごとの設定（顔グループ）として持つ。

**Architecture:**
- MToonMaid は `GroupSettings` に bool を 2 つ足し、継承の単位（`GroupSettingKeys`）に入れる。
- Renderer への書き込みと復元は、新しいクラス `FaceShadowApplier`（メイド 1 人に 1 つ）が持つ。`MaidMToonController` は Scan・ApplyGroup・RestoreAll・DestroyOwnedTextures からそれを呼ぶ。
- SE は顔の影のコミット群（`b040a0d`〜`d84eafe`）の直前 `f75d317` から、顔の影だけを入れたファイルを戻す。顔の影専用のファイルは消す。レイヤー移動と共有の `MaidFaceRendererManager` / `FaceRendererRules` は、手で影の部分だけを削る。

**Tech Stack:** C# 7.3（MToonMaid）/ C# 9（SE テスト）、Unity 2022（COM3D2.5）、xUnit、IMGUI（MTEUtils の GUIView）

**Spec:** `docs/superpowers/specs/2026-10-10-face-shadow-move-design.md`

## Global Constraints

- 既定値: `receiveShadows = true`（影を受ける）、`castShadows = false`（影を落とす）。`castShadows` が true なら `ShadowCastingMode.On`、false なら `Off`
- 効くのは顔のグループ（head スロット）だけ。適用条件は「メイドで MToonMaid が有効」かつ `MaidSettings.IsSlotActive("head")`
- MToonMaid の設定ファイルの版（`MaidSettings.CurrentVersion = 3`）は上げない
- SE の `TransformType.FaceShadow` は enum から完全に削除する。シーンプリセットの `CurrentVersion = 42` は据え置く
- コードのコメント・ログは日本語。コメントは「なぜ他の自然な選択肢を採らなかったか」だけを書く
- ビルドの確認では、ゲームフォルダへ DLL をコピーしない（`debug.bat` / `build.bat` は使わない）
  - MToonMaid: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug "/p:COM3D25_DIR=W:\COM3D2_5"`
  - SE: `"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" source/COM3D2.SceneEditor.Plugin/COM3D2.SceneEditor.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D25 "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"`
- `deploy.bat` / `release.bat` は実行しない
- 検索は `rg` を使う（`grep` は使わない）
- コミットは commit スキルで行い、リポジトリごとに分ける。MToonMaid のコミットは `W:\COM3D2_5\work\COM3D25.MToonMaid.Plugin`、SE のコミットは `W:\COM3D2_5\work\COM3D2.SceneEditor.Plugin` で行う

## Review Focus

1. **共通タブで切り替えたとき**: 顔グループで個別にしていなければ顔へ効き、個別にしていれば顔の値のまま。→ Task 1 のテスト「影を受ける落とすは共通から継承し個別なら顔の値のまま」
2. **head スロットの除外・顔グループの無効化・MToonMaid の無効化**: そのとき head の Renderer が元の値へ戻ること。→ Unity なしでは試せないため、Task 6 の実機確認の項目に入れる。復元の値の決め方は Task 2 のルールのテストで押さえる
3. **他プラグイン・ゲームが値を変えたとき**: 戻すときに、前回書いた値から変えられていればその値を残すこと。→ Task 2 のテスト「戻すときに他者が書き換えていたらその値を残す」
4. **要素の無い旧設定ファイル**: 受ける ON／落とす OFF で読まれ、個別にもならないこと。→ Task 1 のテスト「影を受ける落とすは既定で受けて落とさず要素の無いファイルも個別にしない」
5. **SE の削除後、顔のみ／体のみライトで顔が Face レイヤーへ移ること**: レイヤー移動が壊れていないこと。→ Task 4 で `FaceRendererRulesTests` のレイヤーのテストを残す。実機の確認は Task 6

---

## File Structure

**MToonMaid（`W:\COM3D2_5\work\COM3D25.MToonMaid.Plugin`）**

| ファイル | 変更 | 役割 |
|---|---|---|
| `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs` | Modify | `receiveShadows` / `castShadows` のフィールド、`CopyField` / `FieldEquals` |
| `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs` | Modify | 2 キー |
| `source/COM3D25.MToonMaid.Plugin/ToonMaterialClassifier.cs` | Modify | `UsesRendererShadow(PartGroup)` |
| `source/COM3D25.MToonMaid.Plugin/Rendering/RendererShadowRules.cs` | Create | 書くか・戻す値の純粋関数 |
| `source/COM3D25.MToonMaid.Plugin/Rendering/FaceShadowApplier.cs` | Create | head の Renderer の控え・書き込み・復元 |
| `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs` | Modify | Scan で head の Renderer を集めて Applier を呼ぶ |
| `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs` | Modify | トグル 2 つ |
| `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs` | Modify | 設定のテスト |
| `source/COM3D25.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs` | Modify | `UsesRendererShadow` のテスト |
| `source/COM3D25.MToonMaid.Plugin.Tests/RendererShadowRulesTests.cs` | Create | ルールのテスト |
| `README.md` | Modify | 使い方 |

MToonMaid の csproj は SDK 形式で、`source/COM3D25.MToonMaid.Plugin/` 配下の .cs を自動で取り込む。追加するのは MTEUtils のリンクだけで、今回は不要。テストプロジェクトも自動で取り込む。

**SE（`W:\COM3D2_5\work\COM3D2.SceneEditor.Plugin`、以下 `source/COM3D2.SceneEditor.Plugin/` を `P/`、`source/COM3D2.SceneEditor.Plugin.Tests/` を `T/` と書く）**

| ファイル | 変更 |
|---|---|
| `P/MaidFaceWindow.cs`, `P/Timeline/ItemInspector/MorphItemInspector.cs`, `P/Timeline/TimelineIntegration.cs`, `P/Timeline/TimelineLayer/MorphTimelineLayer.cs`, `P/Timeline/TransformData/ITransformData.cs`, `P/Manager/History/HistoryScope.cs`, `P/Manager/History/SnapshotFactory.cs`, `P/Manager/ScenePresetManager.cs`, `P/ScenePresetData.cs`, `P/Manager/CharacterShadowProxyManager.cs`, `T/LaneColorInfoTests.cs`, `T/TimelineLayerResetOnRemoveTests.cs` | `f75d317` の版へ戻す（この区間の変更は顔の影だけ） |
| `P/MaidManipulation/FaceShadowItems.cs`, `P/FaceShadowRowDrawer.cs`, `P/Timeline/TransformData/TransformDataFaceShadow.cs`, `P/Manager/History/FaceShadowSnapshot.cs`, `T/FaceShadowLayerTests.cs`, `T/ScenePresetFaceShadowTests.cs` | 削除 |
| `P/COM3D2.SceneEditor.Plugin.csproj` | 削除したファイルのエントリを外す |
| `P/Manager/MaidFaceRendererManager.cs` | 影の上書きを外し、レイヤー移動とライトの補正だけ残す |
| `P/MaidManipulation/FaceRendererRules.cs` | `ResolveReceive` / `ResolveCast` / `IsDefault` を外し、`ShouldAdoptExternal` の `overridden` を外す |
| `T/FaceRendererRulesTests.cs` | 影のテストを外す |
| `P/COM3D2.SceneEditor.Plugin.cs` | 登録のコメントから顔の影の記述を外す |
| `P/MaidManipulation/MaidManipulateManager.cs` | `Release` 呼び出しのコメントから顔の影の記述を外す |
| `docs-site/guide/maid-editing.md`, `docs-site/guide/scene-preset.md`, `docs-site/timeline/compatibility.md`, `docs-site/timeline/layers-maid.md` | 顔の影の記述を削除 |

SE の本体 csproj は旧形式で、`Compile Include` を手で並べる。テストプロジェクトは SDK 形式で、ソースを自動で取り込む。

**ワークスペース**: `W:\COM3D2_5\work\CLAUDE.md`（git 管理外）の 99 行目「表情レイヤーの顔の影キー」の項を削除する。

---

### Task 1: MToonMaid の設定に影を受ける／落とすを足す

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/ToonMaterialClassifier.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs`

**Interfaces:**
- Produces:
  - `GroupSettings.receiveShadows`（bool、既定 true）、`GroupSettings.castShadows`（bool、既定 false）
  - `GroupSettingKeys.ReceiveShadows = "receiveShadows"`、`GroupSettingKeys.CastShadows = "castShadows"`
  - `ToonMaterialClassifier.UsesRendererShadow(PartGroup group) : bool`（顔だけ true）

- [ ] **Step 1: 失敗するテストを書く**

`CommonSettingsTests.cs` の `ChangeAll` の末尾（`g.outlineColor.g = 0.7f;` の後）に 2 行足す:

```csharp
            g.receiveShadows = false;
            g.castShadows = true;
```

`前髪の影は既定で…` のテストの `Assert.Equal(23, GroupSettingKeys.All.Length);` を `Assert.Equal(25, GroupSettingKeys.All.Length);` に変える。

`凹凸の影を消すは既定0で要素の無いファイルも個別にしない` テストの後に、次の 2 テストを足す:

```csharp
        [Fact]
        public void 影を受ける落とすは既定で受けて落とさず要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.ReceiveShadows, GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.CastShadows, GroupSettingKeys.All);
            var def = GroupSettings.CreateDefault();
            Assert.True(def.receiveShadows);
            Assert.False(def.castShadows);

            var path = Path.Combine(_dir, "v3shadow.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\"><face><overrides><string>shadingShift</string></overrides><shadingShift>0.2</shadingShift></face></MToonMaid>");
            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s.face.overrides);
            Assert.True(s.Resolve(PartGroup.Face).receiveShadows);
            Assert.False(s.Resolve(PartGroup.Face).castShadows);
        }

        [Fact]
        public void 影を受ける落とすは共通から継承し個別なら顔の値のまま()
        {
            var s = Filled();
            s.common.receiveShadows = false;
            s.common.castShadows = true;

            Assert.False(s.Resolve(PartGroup.Face).receiveShadows);
            Assert.True(s.Resolve(PartGroup.Face).castShadows);

            s.SetOverride(PartGroup.Face, GroupSettingKeys.ReceiveShadows, true);
            s.SetOverride(PartGroup.Face, GroupSettingKeys.CastShadows, true);
            s.face.receiveShadows = true;
            s.face.castShadows = false;

            Assert.True(s.Resolve(PartGroup.Face).receiveShadows);
            Assert.False(s.Resolve(PartGroup.Face).castShadows);
        }
```

`MaidSettings.SetOverride(PartGroup group, string key, bool overridden)` は、個別にした瞬間に共通の値を写す（`CopyField`）。上のテストは写した後に `s.face.*` を代入している。

`ToonMaterialClassifierTests.cs` の `前髪の影を受けるのは顔だけ` の後に足す:

```csharp
        [Theory]
        [InlineData(PartGroup.Face, true)]
        [InlineData(PartGroup.Skin, false)]
        [InlineData(PartGroup.Hair, false)]
        [InlineData(PartGroup.Costume, false)]
        public void 影を受ける落とすが効くのは顔だけ(PartGroup group, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.UsesRendererShadow(group));
        }
```

- [ ] **Step 2: テストが失敗する（コンパイルエラーになる）ことを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: FAIL。`receiveShadows` / `ReceiveShadows` / `UsesRendererShadow` が無いというコンパイルエラー

- [ ] **Step 3: 実装する**

`GroupSettingKeys.cs`: `OutlineColor` の定数の後に足し、`All` の末尾にも足す:

```csharp
        public const string ReceiveShadows = "receiveShadows";
        public const string CastShadows = "castShadows";
```

```csharp
            OutlineEnabled, OutlineWidth, OutlineLightingMix, OutlineColor,
            ReceiveShadows, CastShadows,
```

`GroupSettings.cs`: `hairShadowSoftness` のフィールドの後（`MaxHairShadowOffset` の定数より前）に足す:

```csharp
        // 顔 (head スロット) の Renderer の影を受ける・落とす。既定はゲームの顔の値
        public bool receiveShadows = true;
        public bool castShadows;
```

`CopyField` の `default:` の前に足す:

```csharp
                case GroupSettingKeys.ReceiveShadows: receiveShadows = from.receiveShadows; break;
                case GroupSettingKeys.CastShadows: castShadows = from.castShadows; break;
```

`FieldEquals` の `default:` の前に足す:

```csharp
                case GroupSettingKeys.ReceiveShadows: return receiveShadows == other.receiveShadows;
                case GroupSettingKeys.CastShadows: return castShadows == other.castShadows;
```

`ToonMaterialClassifier.cs`: `UsesHairShadow` の後に足す:

```csharp
        /// <summary>head スロットの Renderer の影を受ける・落とすを書くグループ</summary>
        public static bool UsesRendererShadow(PartGroup group)
        {
            return group == PartGroup.Face;
        }
```

- [ ] **Step 4: テストが通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: PASS（全テスト）

- [ ] **Step 5: コミット**

commit スキルで、上の 5 ファイルをコミットする。例: `feat(settings): 顔の影を受ける・落とすの設定を足す`

---

### Task 2: head の Renderer へ影を受ける／落とすを書く

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/RendererShadowRules.cs`
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/FaceShadowApplier.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/MaidMToonController.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/RendererShadowRulesTests.cs`

**Interfaces:**
- Consumes: `GroupSettings.receiveShadows` / `castShadows`、`ToonMaterialClassifier.UsesRendererShadow`、`ToonMaterialClassifier.HeadSlot`（既存の `"head"`）
- Produces:
  - `RendererShadowRules.ShouldWrite<T>(T desired, T current) : bool`
  - `RendererShadowRules.ResolveRestore<T>(T current, T lastWritten, T original) : T`
  - `FaceShadowApplier.Apply(List<Renderer> renderers, GroupSettings face)`（face が null なら全て戻す）
  - `FaceShadowApplier.Write(GroupSettings face)`、`RestoreAll()`、`Forget()`

テストは Unity のネイティブ呼び出しを含まない型だけを扱う（テスト csproj のコメント）。そのため、ルールのテストは bool / int で書き、`ShadowCastingMode` は使わない。

- [ ] **Step 1: 失敗するテストを書く**

`source/COM3D25.MToonMaid.Plugin.Tests/RendererShadowRulesTests.cs`:

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class RendererShadowRulesTests
    {
        [Fact]
        public void 今の値と同じなら書かない()
        {
            Assert.False(RendererShadowRules.ShouldWrite(true, true));
            Assert.True(RendererShadowRules.ShouldWrite(false, true));
        }

        [Fact]
        public void 戻すときは自分が書いた値のままなら元の値へ戻す()
        {
            Assert.True(RendererShadowRules.ResolveRestore(false, false, true));
            Assert.Equal(0, RendererShadowRules.ResolveRestore(1, 1, 0));
        }

        [Fact]
        public void 戻すときに他者が書き換えていたらその値を残す()
        {
            // MToonMaid が受けない (false) を書いた後、他プラグインが true にした
            Assert.True(RendererShadowRules.ResolveRestore(true, false, false));
            Assert.Equal(2, RendererShadowRules.ResolveRestore(2, 1, 0));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: FAIL。`RendererShadowRules` が無いというコンパイルエラー

- [ ] **Step 3: ルールを実装する**

`source/COM3D25.MToonMaid.Plugin/Rendering/RendererShadowRules.cs`:

```csharp
using System.Collections.Generic;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>Renderer の影設定を書くか・戻す値の決め方 (FaceShadowApplier の判定部分)</summary>
    public static class RendererShadowRules
    {
        public static bool ShouldWrite<T>(T desired, T current)
        {
            return !EqualityComparer<T>.Default.Equals(desired, current);
        }

        /// <summary>今の値が前回書いた値のままなら元の値、他者が書き換えていればその値</summary>
        public static T ResolveRestore<T>(T current, T lastWritten, T original)
        {
            return EqualityComparer<T>.Default.Equals(current, lastWritten) ? original : current;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確かめる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: PASS

- [ ] **Step 5: Applier を実装する**

`source/COM3D25.MToonMaid.Plugin/Rendering/FaceShadowApplier.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の顔 (head スロット) の Renderer に、影を受ける・落とすを書き、元の値へ戻す。
    /// 元の値は Renderer ごとに最初に書く前の値
    /// </summary>
    public sealed class FaceShadowApplier
    {
        private sealed class State
        {
            public Renderer renderer;
            public bool originalReceive;
            public ShadowCastingMode originalCast;
            public bool writtenReceive;
            public ShadowCastingMode writtenCast;
        }

        private readonly Dictionary<int, State> _states = new Dictionary<int, State>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _removeBuffer = new List<int>();

        /// <summary>
        /// renderers に face の値を書く。face が null (MToon にしない・head を除外) なら全て戻す。
        /// renderers に無くなった Renderer (着替え・破棄) は戻して控えを捨てる
        /// </summary>
        public void Apply(List<Renderer> renderers, GroupSettings face)
        {
            if (face == null)
            {
                RestoreAll();
                return;
            }

            _seen.Clear();
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                var id = renderer.GetInstanceID();
                _seen.Add(id);
                State state;
                if (!_states.TryGetValue(id, out state))
                {
                    state = new State
                    {
                        renderer = renderer,
                        originalReceive = renderer.receiveShadows,
                        originalCast = renderer.shadowCastingMode,
                    };
                    _states.Add(id, state);
                }
                Write(state, face);
            }

            _removeBuffer.Clear();
            foreach (var pair in _states)
            {
                if (!_seen.Contains(pair.Key))
                {
                    _removeBuffer.Add(pair.Key);
                }
            }
            foreach (var id in _removeBuffer)
            {
                Restore(_states[id]);
                _states.Remove(id);
            }
        }

        /// <summary>設定を変えたとき。控えのある Renderer へ走査を待たずに書く</summary>
        public void Write(GroupSettings face)
        {
            foreach (var state in _states.Values)
            {
                if (state.renderer != null)
                {
                    Write(state, face);
                }
            }
        }

        public void RestoreAll()
        {
            foreach (var state in _states.Values)
            {
                Restore(state);
            }
            _states.Clear();
        }

        /// <summary>Maid ごと破棄されたとき。Renderer も破棄済みで戻す先が無い</summary>
        public void Forget()
        {
            _states.Clear();
        }

        private static void Write(State state, GroupSettings face)
        {
            var renderer = state.renderer;
            var receive = face.receiveShadows;
            if (RendererShadowRules.ShouldWrite(receive, renderer.receiveShadows))
            {
                renderer.receiveShadows = receive;
            }
            state.writtenReceive = receive;

            var cast = face.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (RendererShadowRules.ShouldWrite(cast, renderer.shadowCastingMode))
            {
                renderer.shadowCastingMode = cast;
            }
            state.writtenCast = cast;
        }

        private static void Restore(State state)
        {
            var renderer = state.renderer;
            if (renderer == null)
            {
                return;
            }
            var receive = RendererShadowRules.ResolveRestore(renderer.receiveShadows, state.writtenReceive, state.originalReceive);
            if (RendererShadowRules.ShouldWrite(receive, renderer.receiveShadows))
            {
                renderer.receiveShadows = receive;
            }
            var cast = RendererShadowRules.ResolveRestore(renderer.shadowCastingMode, state.writtenCast, state.originalCast);
            if (RendererShadowRules.ShouldWrite(cast, renderer.shadowCastingMode))
            {
                renderer.shadowCastingMode = cast;
            }
        }
    }
}
```

- [ ] **Step 6: Controller から呼ぶ**

`MaidMToonController.cs`:

1. フィールドに足す（`_materialBuffer` の宣言の後）:

```csharp
        private readonly FaceShadowApplier _faceShadow = new FaceShadowApplier();
        private readonly List<Renderer> _headRenderers = new List<Renderer>();
```

2. `ApplyGroup` の先頭（`UsesHairShadow` の if の前）に足す:

```csharp
            if (ToonMaterialClassifier.UsesRendererShadow(group))
            {
                _faceShadow.Write(settings);
            }
```

3. `RestoreAll` の `ReleaseHairShadow();` の後に `_faceShadow.RestoreAll();` を足す。

4. `DestroyOwnedTextures` の `ReleaseHairShadow();` の後に `_faceShadow.Forget();` を足す。

5. `Scan` を次のように変える。`var slots = Maid.body0.goSlot;` の前で集め先を空にする。`skin.obj.GetComponentsInChildren(true, _rendererBuffer);` の直後で head の Renderer を集める。ループの後（`_eyeGloss?.Scan(settings.eye);` の前）で Applier を呼ぶ:

```csharp
            _headRenderers.Clear();
            GroupSettings headSettings = null;
            var slots = Maid.body0.goSlot;
```

```csharp
                    skin.obj.GetComponentsInChildren(true, _rendererBuffer);
                    if (active && skin.Category == ToonMaterialClassifier.HeadSlot && ToonMaterialClassifier.UsesRendererShadow(group))
                    {
                        headSettings = groupSettings;
                        _headRenderers.AddRange(_rendererBuffer);
                    }
```

```csharp
            _faceShadow.Apply(_headRenderers, headSettings);
            _eyeGloss?.Scan(settings.eye);
```

`ToonMaterialClassifier.HeadSlot` が `public const string HeadSlot = "head";` であることを確かめる（`ToonMaterialClassifier.cs:13`）。

- [ ] **Step 7: ビルドとテスト**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: Build succeeded（警告が増えていないこと）

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: PASS

- [ ] **Step 8: コミット**

commit スキルで、上の 4 ファイルをコミットする。例: `feat: 顔の Renderer へ影を受ける・落とすを書き、無効にしたら戻す`

---

### Task 3: ウィンドウにトグルを足し、README に書く

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs`（`DrawSettings` の 陰影 セクション、現在の 365 行目 `前髪の影のぼかし` の後）
- Modify: `README.md`（制限の「前髪の影は前髪スロットだけが落とします…」の行の後）

**Interfaces:**
- Consumes: `GroupSettingKeys.ReceiveShadows` / `CastShadows`、`ToonMaterialClassifier.UsesRendererShadow`、既存の `Toggle(GUIView view, string key, string label, bool value, Action<GroupSettings, bool> set, bool editable = true)`

- [ ] **Step 1: トグルを足す**

`MToonMaidWindow.cs` の `Slider(view, GroupSettingKeys.HairShadowSoftness, …);` の行の直後（`view.DrawLabel("天使の輪", …)` の前の空行より前）に足す:

```csharp
            // 顔以外には効かないため個別のトグルを含めて無効表示にする (行を消すと入力欄キャッシュの番号がずれる)
            var rendererShadowEditable = !_editGroup.HasValue || ToonMaterialClassifier.UsesRendererShadow(_editGroup.Value);
            Toggle(view, GroupSettingKeys.ReceiveShadows, "顔が影を受ける", shown.receiveShadows, (s, v) => s.receiveShadows = v, rendererShadowEditable);
            Toggle(view, GroupSettingKeys.CastShadows, "顔が影を落とす", shown.castShadows, (s, v) => s.castShadows = v, rendererShadowEditable);
```

ラベルは、全タブで同じ行に出ても顔にだけ効くことが分かるよう「顔が」を付ける。ラベル幅（110）に収まることを、実機の確認（Task 6）で見る。

- [ ] **Step 2: ビルド**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug "/p:COM3D25_DIR=W:\COM3D2_5"`
Expected: Build succeeded

- [ ] **Step 3: README を書く**

`README.md` の制限の「- 前髪の影は前髪スロットだけが落とします…」の行の後に足す:

```markdown
- 顔が影を受けるか・落とすかは、顔タブ（または共通タブ）の「顔が影を受ける」「顔が影を落とす」で切り替えます。MToon にしたメイドの顔は、この設定の値で上書きされます。既定はゲームの顔と同じ（受ける ON・落とす OFF）です。「顔が影を受ける」を外すと髪や体の影が顔に落ちなくなり、「顔が影を落とす」を入れると顔の影が首や体に落ちます。顔（head スロット）にだけ効き、MToon にしない間はゲームの値へ戻ります
```

- [ ] **Step 4: コミット**

commit スキルで 2 ファイルをコミットする。例: `feat(ui): 顔が影を受ける・落とすのトグルを足す`

---

### Task 4: SE から顔の影を外す（コード）

作業ディレクトリ: `W:\COM3D2_5\work\COM3D2.SceneEditor.Plugin`

**Files:** File Structure の SE の表のうち docs-site 以外の全て

**Interfaces:**
- Produces（SE 内）:
  - `FaceRendererRules.ShouldAdoptExternal<T>(T current, T lastWritten) : bool`（引数 `overridden` を削除）
  - `FaceRendererRules.IsDefault` は削除し、呼び出し側は `!moveToFace` を使う
  - `MaidFaceRendererManager` の公開 API は `Release(Maid)`、`Destroy()`、`isFaceLayerActive` だけになる

- [ ] **Step 1: 作業前の状態を確かめる**

Run: `git status --short` → 空であること。
Run: Global Constraints の SE の MSBuild コマンドに `/v:minimal` を付けて実行し、変更前の警告数を控える（Step 9 で比べる）。
Run: `git log --oneline -1` → `270b8a1 Merge branch 'feat/face-body-shadow'` であること。違えば、`git diff f75d317 HEAD --stat -- source` を見て、下の Step 2 で戻すファイルに顔の影以外の変更が入っていないことを確かめてから進める。

- [ ] **Step 2: 顔の影だけを入れたファイルを f75d317 の版へ戻す**

```bash
git checkout f75d317 -- \
  source/COM3D2.SceneEditor.Plugin/MaidFaceWindow.cs \
  source/COM3D2.SceneEditor.Plugin/Timeline/ItemInspector/MorphItemInspector.cs \
  source/COM3D2.SceneEditor.Plugin/Timeline/TimelineIntegration.cs \
  source/COM3D2.SceneEditor.Plugin/Timeline/TimelineLayer/MorphTimelineLayer.cs \
  source/COM3D2.SceneEditor.Plugin/Timeline/TransformData/ITransformData.cs \
  source/COM3D2.SceneEditor.Plugin/Manager/History/HistoryScope.cs \
  source/COM3D2.SceneEditor.Plugin/Manager/History/SnapshotFactory.cs \
  source/COM3D2.SceneEditor.Plugin/Manager/ScenePresetManager.cs \
  source/COM3D2.SceneEditor.Plugin/ScenePresetData.cs \
  source/COM3D2.SceneEditor.Plugin/Manager/CharacterShadowProxyManager.cs \
  source/COM3D2.SceneEditor.Plugin.Tests/LaneColorInfoTests.cs \
  source/COM3D2.SceneEditor.Plugin.Tests/TimelineLayerResetOnRemoveTests.cs
```

`ScenePresetData.cs` の v42 の版コメントは、`f75d317` では照射対象の 1 行だけのはず。戻した後に次で確かめる:
Run: `rg -n "v42" source/COM3D2.SceneEditor.Plugin/ScenePresetData.cs`
Expected: `// v42: 追加ライトの照射対象 (target) に 3=顔のみ / 4=体のみ を追加。旧 SE は「全て」として読む。` の 1 行だけで、faceShadow の行が無いこと。faceShadow の行が残っていれば、その 2 行（`maid に faceShadow …` と `上書きが無くても空要素を書く…`）を消す。`CurrentVersion = 42` であること。

- [ ] **Step 3: 顔の影専用のファイルを消す**

```bash
git rm -q \
  source/COM3D2.SceneEditor.Plugin/MaidManipulation/FaceShadowItems.cs \
  source/COM3D2.SceneEditor.Plugin/FaceShadowRowDrawer.cs \
  source/COM3D2.SceneEditor.Plugin/Timeline/TransformData/TransformDataFaceShadow.cs \
  source/COM3D2.SceneEditor.Plugin/Manager/History/FaceShadowSnapshot.cs \
  source/COM3D2.SceneEditor.Plugin.Tests/FaceShadowLayerTests.cs \
  source/COM3D2.SceneEditor.Plugin.Tests/ScenePresetFaceShadowTests.cs
```

`P/COM3D2.SceneEditor.Plugin.csproj` から次の 4 行を消す（`FaceRendererRules.cs` と `MaidFaceRendererManager.cs` の行は残す）:

```xml
    <Compile Include="MaidManipulation\FaceShadowItems.cs" />
    <Compile Include="Manager\History\FaceShadowSnapshot.cs" />
    <Compile Include="FaceShadowRowDrawer.cs" />
    <Compile Include="Timeline\TransformData\TransformDataFaceShadow.cs" />
```

- [ ] **Step 4: FaceRendererRules の影の部分を外す**

`P/MaidManipulation/FaceRendererRules.cs`:
- `ResolveReceive` と `ResolveCast` のメソッドを丸ごと消す
- `IsDefault` のメソッドを丸ごと消す
- `ShouldAdoptExternal` を次に置き換える:

```csharp
        /// <summary>
        /// 他者 (VR の LayerCheck・他プラグイン) が前回書いた値から変えた値を、新しい元の値として取り込むか。
        /// 取り込まないと、元の値から決めた値で他者の値を取り消し続ける
        /// </summary>
        public static bool ShouldAdoptExternal<T>(T current, T lastWritten)
        {
            return !EqualityComparer<T>.Default.Equals(current, lastWritten);
        }
```

- `using UnityEngine.Rendering;` が使われなくなるので消す（`using System.Collections.Generic;` は `EqualityComparer` で使うので残す）
- クラスの `<summary>` は `顔 (head スロット) の Renderer に書くレイヤーの決め方 (MaidFaceRendererManager の判定部分)` にする

- [ ] **Step 5: MaidFaceRendererManager の影の部分を外す**

`P/Manager/MaidFaceRendererManager.cs` を次のように変える（行番号は変更前のもの。編集で後ろの番号がずれるため、メソッド名・フィールド名で場所を探して直す）:

1. クラスの `<summary>`（9-13 行目）を次にする:

```csharp
    /// <summary>
    /// メイドの顔 (head スロット) の Renderer を Face レイヤーへ移す。
    /// ゲームは通常顔も Charactor に置くため、「顔のみ／体のみ」の追加ライトがある間だけ顔を Face へ移す。
    /// 元のレイヤーは Renderer ごとに初めて書くときに退避し、移さなくなったら戻す
    /// </summary>
```

2. `using UnityEngine.Rendering;` を消す。

3. `RendererValues` 構造体（24-40 行目）を消す。`RendererState` を次に置き換える:

```csharp
        private class RendererState
        {
            /// <summary>ゲームのレイヤー。他者が値を変えたらその値に置き直す</summary>
            public int originalLayer;
            /// <summary>前回書いたレイヤー。他者が変えたかを見分けるために持つ</summary>
            public int writtenLayer;
        }
```

4. `Entry` から `overrides` フィールド（52 行目）を消す。

5. `_castChangedMaids`（78-82 行目）を消す。

6. 公開 API の `TryGetOverride`、`SetOverride`、`ClearOverride`、`ClearAll`、`GetOverrides`、`SetOverrides`、`IsEnabled`、`ConsumeCastChanged`（113-198 行目）を消す。

7. `GetOrCreateEntry` は `LateUpdate` から使うので残す。

8. `Apply` の Renderer のループ（332-387 行目）を次に置き換える:

```csharp
            foreach (var renderer in entry.renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                RendererState state;
                if (!entry.states.TryGetValue(renderer, out state))
                {
                    var current = renderer.gameObject.layer;
                    state = new RendererState { originalLayer = current, writtenLayer = current };
                    entry.states.Add(renderer, state);
                }

                // 他者が前回書いた値から変えていたら、その値を元の値として取り込み、取り消さない
                // (VR の一人称モードが頭を隠すために Face へ移す)
                var layer = renderer.gameObject.layer;
                if (FaceRendererRules.ShouldAdoptExternal(layer, state.writtenLayer))
                {
                    state.originalLayer = layer;
                }
                var desiredLayer = FaceRendererRules.ResolveLayer(
                    state.originalLayer, moveToFace, _charactorLayer, _faceLayer);
                if (FaceRendererRules.ShouldWrite(desiredLayer, layer))
                {
                    renderer.gameObject.layer = desiredLayer;
                }
                state.writtenLayer = desiredLayer;
            }
```

9. `RestoreAll(Maid maid, Entry entry)` を次に置き換え、呼び出し側（`Release`、`Destroy`、`RemoveIdleEntries`）の引数から `maid` を外して `RestoreAll(entry)` にする:

```csharp
        /// <summary>元のレイヤーへ戻す。前回書いた後に他者が書き換えた値は残す</summary>
        private static void RestoreAll(Entry entry)
        {
            foreach (var pair in entry.states)
            {
                var renderer = pair.Key;
                if (renderer == null)
                {
                    continue;
                }
                renderer.gameObject.layer = FaceRendererRules.ResolveRestore(
                    renderer.gameObject.layer, pair.Value.writtenLayer, pair.Value.originalLayer);
            }
            entry.states.Clear();
            entry.renderers.Clear();
            entry.headObj = null;
        }
```

10. `RemoveIdleEntries` を次に置き換える（上書きが無くなったので、移さないエントリといなくなったメイドのエントリは常に戻して捨てる）:

```csharp
        /// <summary>
        /// 移さないエントリと、いなくなったメイドのエントリは、ゲームの値へ戻して捨てる
        /// (いなくなったメイドの Renderer は破棄済みか、Release 済み)
        /// </summary>
        private void RemoveIdleEntries(bool moveToFace)
        {
            _removeScratch.Clear();
            foreach (var pair in _entries)
            {
                if (!moveToFace || !_maids.Contains(pair.Key))
                {
                    RestoreAll(pair.Value);
                    _removeScratch.Add(pair.Key);
                }
            }
            foreach (var maid in _removeScratch)
            {
                _entries.Remove(maid);
            }
        }
```

11. `Release` の `<summary>` を `メイドがストックへ戻るとき。顔をゲームのレイヤーへ戻す` にする。

12. `CollectRenderers` の `<summary>` の括弧内「SE が書いた値を元の値と取り違える」はレイヤーにも当てはまるので、そのまま残す。

最後に、ファイルに `FaceShadowItems` / `overrides` / `receiveShadows` / `shadowCastingMode` / `_castChangedMaids` が残っていないことを確かめる:
Run: `rg -n "FaceShadowItems|overrides|receiveShadows|shadowCastingMode|castChanged|ShadowCastingMode" source/COM3D2.SceneEditor.Plugin/Manager/MaidFaceRendererManager.cs`
Expected: 出力なし

- [ ] **Step 6: 登録と Release のコメントを直す**

`P/COM3D2.SceneEditor.Plugin.cs` の `RegisterManager(MaidFaceRendererManager.instance)` の上のコメント 2 行を、次の 1 行にする:

```csharp
                // 顔のレイヤーはライトの照射対象で決まり、影の複製 (CharacterShadowProxyManager) は移した後のレイヤーを見て作るため、その間に登録する
```

（登録の並びは `StudioLightManager`（449 行目）→ `MaidFaceRendererManager`（475 行目）→ `CharacterShadowProxyManager`（478 行目）で、コメントと合っている）

`P/MaidManipulation/MaidManipulateManager.cs` の `MaidFaceRendererManager.instance.Release(maid);` の上のコメントを次にする:

```csharp
            // Face レイヤーへの移動も持ち越さない（ストックの Maid は使い回される）
```

- [ ] **Step 7: テストを直す**

`T/FaceRendererRulesTests.cs`:
- `using System.Collections.Generic;` と `using UnityEngine.Rendering;` を消す
- `Overrides` ヘルパー（12-20 行目）を消す
- テスト `上書きが無ければ元の影設定のまま`、`上書きで影受けと影落としを切り替える`、`影落としOFFの上書きはOffにする`、`上書きが無く移さなければ書くものは無い`、`上書きのある項目は他者の値を取り込まず上書きを書き直す`、`項目はゲームの既定値を持つ` を消す
- `戻すときに他者が書き換えていたらその値を残す` の `Assert.True(FaceRendererRules.ResolveRestore(true, false, false));` を消す（レイヤーの int だけにする）
- `他者が前回書いた値から変えていれば新しい元の値として取り込む` の 2 行を、`overridden` を外した形にする:

```csharp
            Assert.True(FaceRendererRules.ShouldAdoptExternal(Face, Charactor));
            Assert.False(FaceRendererRules.ShouldAdoptExternal(Charactor, Charactor));
```

`T/CharacterShadowProxyRulesTests.cs` に顔の影の記述が無いことを確かめる:
Run: `rg -n -i "faceshadow|顔の影|ConsumeCastChanged" source/COM3D2.SceneEditor.Plugin.Tests`
Expected: 出力なし

- [ ] **Step 8: 残りの参照が無いことを確かめる**

Run: `rg -n "FaceShadow|faceShadow|faceReceiveShadows|faceCastShadows|ConsumeCastChanged|顔の影" source`
Expected: 出力なし。残っていれば、その行が顔の影だけのためのものかを読んで消す。`CharacterShadowProxyManager` やライトのコメントの「顔のみ」はレイヤーの話なので残す

- [ ] **Step 9: ビルドとテスト**

Run（SE のリポジトリ直下で）: `"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" source/COM3D2.SceneEditor.Plugin/COM3D2.SceneEditor.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D25 "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5" /v:minimal`
Expected: エラー 0。警告が増えていないこと（変更前に同じコマンドで警告数を控えておく）

Run: `dotnet test source/COM3D2.SceneEditor.Plugin.Tests`
Expected: PASS。テストは上のビルドの出力（`bin/Debug/COM3D25`）を参照するため、必ずビルドの後に実行する

- [ ] **Step 10: コミット**

commit スキルで SE のリポジトリにコミットする。例: `refactor(face-shadow): 顔の影の上書きを外し MToonMaid へ移す`
本文に「顔の影はメイドの見た目の設定として MToonMaid で持つため。顔のみ／体のみライト用の Face レイヤーへの移動は残す」と書く。

---

### Task 5: SE のドキュメントとワークスペースの CLAUDE.md を直す

**Files:**
- Modify: `docs-site/guide/maid-editing.md`、`docs-site/guide/scene-preset.md`、`docs-site/timeline/compatibility.md`、`docs-site/timeline/layers-maid.md`（SE リポジトリ）
- Modify: `W:\COM3D2_5\work\CLAUDE.md`（git 管理外）

- [ ] **Step 1: docs-site の顔の影の記述を消す**

- `docs-site/guide/maid-editing.md`
  - 表の「表情」の行のタブの並びから `` / `影` `` を消す（`` `シェイプキー` / `プリセット` `` にする）
  - `## 顔の影` の節（見出しから、次の `## 視線` の直前の空行まで）を丸ごと消す
- `docs-site/guide/scene-preset.md`: 「メイド」の含まれる状態の列挙から `顔の影・` を消す（`ノード表示・体型・` にする）
- `docs-site/timeline/compatibility.md`: `` - `メイド表情` の `顔の影` キーは SceneEditor 独自です。… `` の 1 行を消す（直後の「ライトキーの `対象` の `顔のみ` / `体のみ`…」の行は残す）
- `docs-site/timeline/layers-maid.md`
  - 「表情モーフの値と、まばたき抑止の `強制上書き`、顔の影を扱います。」を「表情モーフの値と、まばたき抑止の `強制上書き` を扱います。」に戻す
  - `` - `表情設定` → `顔の影: 影を受ける` … `` の項目と、その下の 2 つの子項目を消す
  - warning の文を「`強制上書き` のキーは SceneEditor 独自の形式です。これを含むタイムラインは MTE では読み込めません。」に戻す

`docs-site/guide/staging.md` の顔のみ／体のみ・Face レイヤー・キャラの影の記述は残す。

Run: `rg -n "顔の影|影を受ける|影を落とす" docs-site`
Expected: 出力なし（`staging.md` の「照らさない髪や体の影が顔に落ちます」は語が違うので出ない）

- [ ] **Step 2: コミット**

commit スキルで SE にコミットする。例: `docs(docs-site): 顔の影の記述を消す`

- [ ] **Step 3: ワークスペースの CLAUDE.md を直す**

`W:\COM3D2_5\work\CLAUDE.md` の「タイムライン XML の互換方向」の箇条のうち、`- 表情レイヤーの顔の影キー（` で始まる 1 項目を丸ごと消す。直後の「ライトキーの照射対象（index 18）の値 3（顔のみ）・4（体のみ）…」の項目は残す（その中の「シーンプリセットは v42 で追加ライトの `target` に同じ値を許す」は照射対象の話なので正しい）。
git 管理外なので、コミットはしない。

---

### Task 6: 実機で確かめる

ゲームが起動していない場合は、この Task は飛ばして、確認していないことをユーザーへの報告に書く。DLL の反映（ゲームの再起動）が要るため、ゲームが起動中でも、反映するかはユーザーに確かめてから restart-verify スキルに従う。

- [ ] **Step 1: 反映と準備**

MToonMaid と SE の新しい DLL をゲームへ反映し、メイドを呼ぶ。**MToonMaid を有効にする前に**、下の式で head の Renderer の値を読んで控える（MOD の顔を使うメイドがいれば、そのメイドでも読む）。既定（受ける ON・落とす OFF）と違う顔があれば、有効にすると見た目が変わるので、結果の報告に書く。控えた後に MToonMaid を有効にする。

- [ ] **Step 2: devbridge の eval_csharp で確かめる**

head の Renderer の値は次で読む（`maid` は対象のメイド）:

```csharp
var head = maid.body0.goSlot[(int)TBody.hashSlotName["head"]].obj;
string.Join(",", head.GetComponentsInChildren<UnityEngine.Renderer>(true)
    .Select(r => r.name + ":" + r.receiveShadows + "/" + r.shadowCastingMode).ToArray())
```

（`hashSlotName` や `goSlot` の添字の形は版で違う。推測せず、`maid.body0.goSlot` の型とメンバーを eval で確かめてから書く）

確かめること:
1. 既定（受ける ON・落とす OFF）で値がゲームと同じこと（`True/Off`）
2. 顔タブで「顔が影を落とす」を入れると `On` になり、「顔が影を受ける」を外すと `False` になること
3. 共通タブで切り替えると、顔を個別にしていなければ顔へ効くこと
4. 顔タブで head スロットを MToon の対象から外す、または「MToon にする」を外すと、元の値（`True/Off`）へ戻ること
5. 「顔が影を落とす」を入れた状態で着替え（または顔の差し替え）をした後、新しい head の Renderer に設定の値（`On`）が付き直すこと
5b. 続けて「MToon にする」を外すと、新しい head の Renderer が Step 1 で控えた元の値へ戻ること（着替えの後に元の値を控え直していること）
6. SE で追加ライトの対象を「顔のみ」にすると、head の Renderer のレイヤーが `Face` になり、ライトを消すと `Charactor` に戻ること（`LayerMask.LayerToName(r.gameObject.layer)`）
7. 「顔が影を落とす」を入れた状態で、SE の「背景のみ」＋影ありのライトでキャラの影を ON にすると、数フレーム以内に顔の影も複製へ入ること（`CharacterShadowProxyManager` の定期チェック）
8. MToonMaid のウィンドウで、トグルのラベルが欠けずに表示されること（screenshot）

- [ ] **Step 3: 結果を報告する**

確かめた項目と結果を、ユーザーへの報告に書く。食い違いがあれば、直す前に superpowers:systematic-debugging に従って原因を調べる。

## レビュー却下メモ

- ApplyGroup がグループ無効・head 除外を見ずに書く — グループの有効切替と除外は `_targetsChanged` → `RequestScan` で次の Tick に間隔を待たず Scan し、`Apply(…, null)` で戻るため、書いてしまう期間は 1 フレーム程度で実害が無い
- プラグイン単体の破棄 (OnDestroy) で顔の値が戻らない — マテリアルも戻さない既存の挙動と整合しており、README の「MToon にしない間は戻る」はメイド単位の話として読める
- FaceShadowApplier の状態遷移の自動テスト — Unity の型を使うためテストできない。実機確認 5 / 5b で着替え前後の控え直しと書き直しを確かめる（状態管理を int キーのクラスへ切り出す案は過剰として見送り）
