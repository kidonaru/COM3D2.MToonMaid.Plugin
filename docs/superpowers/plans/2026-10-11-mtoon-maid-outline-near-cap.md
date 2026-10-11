# アウトラインの近距離の頭打ち Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MToon のワールド基準の線に、ゲームと同じ「近距離で画面上の太さを頭打ちにする」補正を入れ、プラグインの幅の基準の切り替え（ワールド / 画面）を「頭打ちの距離」の設定に置き換える。幅の設定値はゲームの `_OutlineWidth` と同じ尺度にし、0.0008 でゲームの線と同じ太さにする。

**Architecture:** フォークした MToon10 シェーダーのワールド基準の分岐で、頂点のビュー深度（クリップ座標の w）が `_OutlineNearDistance` 未満なら幅を `深度 / _OutlineNearDistance` 倍する（0 以下なら補正なし）。プラグインは常にワールド基準（`_OutlineWidthMode = 1`）で描き、`GroupSettings.outlineNearDistance` をマテリアルへ書く。`OutlineWidthMode` 列挙と `outlineScreenWidth` は削除する。幅は `OutlineParams.Width` で設定値 × 幅の倍率（`GroupSettings.outlineWidthScale`、既定 0.70 = ゲームの線の厚み / `_OutlineWidth` の実測値）を MToon の `_OutlineWidth`（m）にして書く。

**Tech Stack:** C# (.NET 3.5 相当 / Unity 2022.3)、HLSL (MToon10 フォーク)、xUnit、Unity batchmode によるアセットバンドルのビルド（`build-bundle.bat`）。

**Spec:** ユーザー指示「シェーダーを改造して。幅の基準切り替えをなくして、頭打ちの調整にして。」と、実測の結論（memory `game-outline-width-formula`）:
- ゲームの線はビュー深度が約 0.71m 以上でワールド一定、未満では距離に比例して細くなる（画面上で一定）。境目は FOV に依存しない
- 厚みは 0.70 × `_OutlineWidth` / オブジェクトのスケール（スケール依存の再現は今回の範囲外）
- 追加指示「設定値 0.0008 で今（ゲーム）の見た目になるように計算式を調整したい」→ MToon へ書く幅を設定値 × 0.70 にする
- 追加指示「0.70 倍は調整できるようにしたい」→ 倍率を設定項目「幅の倍率」（既定 0.70、0〜2）にする

## Global Constraints

- コードのコメントとログは日本語。コメントは原則「却下した案と、それで起きる問題」だけを書く（`<summary>` は名前で分からない契約に限り可）
- シェーダーの改変箇所には既存の流儀どおり `COM3D25:` の注記を付け、`vrmc_materials_mtoon.shader` 冒頭の改変一覧に 1 行足す
- 設定ファイルに書くキー文字列（`GroupSettingKeys`）は値を変えない。新キーは `"outlineNearDistance"` と `"outlineWidthScale"`
- 既定値: 頭打ちの距離 `0.71`（m）、幅 `0.0008`（ゲームの既定の `_OutlineWidth` と同じ）、幅の倍率 `0.70`（MToon へは 0.0008 × 0.70 = 0.00056m で書く）
- 新キーは `"outlineWidthScale"` も追加
- `deploy.bat` / `deploy.ps1` は実行しない。ビルド確認は `debug.bat`（ゲーム停止中は実機へコピーされる点に注意）
- `build-bundle.bat` は Unity エディタで `UnityProject2022` を開いていると失敗する

## Review Focus

1. 旧設定ファイル（`<outlineWidthMode>` / `<outlineScreenWidth>` 要素と、overrides に `"outlineWidthMode"` を含む）を読み込んでも例外にならず、頭打ちの距離は既定 0.71 になる → Task 2 のテスト「旧形式の幅の基準を含む設定を読める」
2. 頭打ちの距離 0 は「頭打ちしない」（純粋なワールド基準）として描く。負値の手編集でもゼロ除算しない → Task 1 のシェーダーで `> 0` のときだけ割る。実機確認は Task 3
3. 線を描かない設定（`outlineEnabled = false` や元に線が無い）では従来どおり `_OutlineWidthMode = 0` → Task 2 のテスト「線の基準」
4. 部位タブで頭打ちの距離を個別にでき、共通から継承する → Task 2 のテスト「頭打ちの距離を継承する」
5. UI の行が「幅の基準」1 行 → 「幅の倍率」「頭打ちの距離」2 行に増えても、他の行の入力欄がずれない（行は常に出し、条件で隠さない）→ Task 3 の実機確認

---

### Task 1: シェーダーに近距離の頭打ちを足してバンドルを作り直す

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`（冒頭の改変一覧、Outline のプロパティ）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl`（CBUFFER に `half _OutlineNearDistance;`）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_geometry_vertex.hlsl`（ワールド基準の分岐）
- Regenerate: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`

**Interfaces:**
- Produces: マテリアルプロパティ `_OutlineNearDistance`（float、m、既定 0 = 補正なし）

- [ ] **Step 1: 改変一覧に追記**（`vrmc_materials_mtoon.shader` の 11 行目の後）

```hlsl
// - ワールド基準の輪郭で、ビュー深度が _OutlineNearDistance より近いと幅を深度に比例させ、画面上の太さを頭打ちにする (ゲームの Toony の線と同じ)
```

- [ ] **Step 2: プロパティを追加**（`_OutlineLightingMix` の行の後）

```hlsl
        // COM3D25: ワールド基準の輪郭で、ビュー深度がこれより近いと画面上の太さを頭打ちにする (m)。0 以下なら頭打ちしない (プラグインが設定する)
        _OutlineNearDistance ("Outline Near Distance", Float) = 0
```

- [ ] **Step 3: CBUFFER に宣言**（`vrmc_materials_mtoon_input.hlsl` の `half _OutlineLightingMix;` の後）

```hlsl
half _OutlineNearDistance;
```

- [ ] **Step 4: ワールド基準の分岐で幅に倍率を掛ける**（`vrmc_materials_mtoon_geometry_vertex.hlsl`）

`MToon_GetOutlineVertex_ScreenCoordinatesWidthMultiplier` の後に関数を足す:

```hlsl
// COM3D25: ゲームの Toony の線は、ビュー深度が一定より近いと画面上の太さが頭打ちになる。
//          ワールドの幅を深度に比例させると画面上の太さが一定になる
inline float MToon_GetOutlineVertex_NearWidthMultiplier(const float viewDepth)
{
    return _OutlineNearDistance > 0 ? saturate(viewDepth / _OutlineNearDistance) : 1.0f;
}
```

ワールド基準の分岐を次のように変える:

```hlsl
    if (MToon_IsOutlineModeWorldCoordinates())
    {
        const float3 positionWS = mul(unity_ObjectToWorld, float4(positionOS, 1)).xyz;
        const half3 normalWS =  MToon_TransformObjectToWorldNormal(normalOS);
        const float viewDepth = MToon_TransformWorldToHClip(positionWS).w;
        const float outlineWidth = MToon_GetOutlineVertex_OutlineWidth(uv) * MToon_GetOutlineVertex_NearWidthMultiplier(viewDepth);
```

（以降の `output.positionWS = float4(positionWS + normalWS * outlineWidth, 1);` はそのまま）

- [ ] **Step 5: バンドルを作り直す**

Run: `powershell -NoProfile -Command "& .\build-bundle.bat"`（リポジトリ直下）
Expected: `コピーしました: ...\Resources\mtoon_bundle`。失敗時は `UnityProject2022\Logs\build-bundle.log` の `Shader error` を見る

- [ ] **Step 6: Commit**

```bash
git add UnityProject2022/Assets/Shaders/MToon10 source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle
git commit -m "feat(shader): ワールド基準の線に近距離の頭打ちを足す"
```

---

### Task 2: 幅の基準を廃止して頭打ちの距離の設定にする

**Files:**
- Delete: `source/COM3D25.MToonMaid.Plugin/Settings/OutlineWidthMode.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs:52-57, 126-130`
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs:29, 42`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/OutlineParams.cs:22-35`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`（フォークのプロパティ欄に追加）
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs:125-128`
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs:32-36, 424-446, 1136-1137`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/OutlineParamsTests.cs`, `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`

**Interfaces:**
- Consumes: Task 1 の `_OutlineNearDistance`
- Produces: `GroupSettings.outlineNearDistance`（float）、`GroupSettingKeys.OutlineNearDistance = "outlineNearDistance"`、`MToonProps.OutlineNearDistance = "_OutlineNearDistance"`、`OutlineParams.Mode(bool outline)`、`OutlineParams.Width(float width, float scale)`（MToon の `_OutlineWidth` に書く m）、`GroupSettings.outlineWidthScale`（float）、`GroupSettingKeys.OutlineWidthScale = "outlineWidthScale"`

- [ ] **Step 1: テストを書き換える**

`OutlineParamsTests.cs` の「線の基準」と「幅は基準に合う方を使う」を次の 1 本に置き換える:

```csharp
        [Theory]
        [InlineData(false, MToonProps.OutlineModeNone)]
        [InlineData(true, MToonProps.OutlineModeWorld)]
        public void 線の基準(bool outline, int expected)
        {
            Assert.Equal(expected, OutlineParams.Mode(outline));
        }

        [Fact]
        public void 幅はゲームの線と同じ太さになるよう換算する()
        {
            Assert.Equal(0.00056f, OutlineParams.Width(0.0008f, 0.7f), 6);
            Assert.Equal(0.0008f, OutlineParams.Width(0.0008f, 1f), 6);
            Assert.Equal(0f, OutlineParams.Width(0f, 0.7f));
        }
```

`CommonSettingsTests.cs` の `ChangeAll(GroupSettings g)`（全項目を既定と違う値にするヘルパー。78〜79 行）で幅の基準と画面の幅の 2 行を次に置き換える（既定 0.71 と違う値にしないと「全項目を個別にすると…」のテストが落ちる）:

```csharp
            g.outlineNearDistance = 0.3f;
            g.outlineWidthScale = 1.2f;
```

「幅の基準は既定がワールド」「幅は基準ごとの値をまとめて継承する」「幅の基準と画面基準の幅を保存して読み直せる」の 3 本を次に置き換える:

```csharp
        [Fact]
        public void 頭打ちの距離の既定はゲームの線と同じ()
        {
            Assert.Contains(GroupSettingKeys.OutlineNearDistance, GroupSettingKeys.All);
            var def = GroupSettings.CreateDefault();
            Assert.Equal(0.71f, def.outlineNearDistance);
            Assert.Contains(GroupSettingKeys.OutlineWidthScale, GroupSettingKeys.All);
            Assert.Equal(0.7f, def.outlineWidthScale);
            Assert.Equal(0.0008f, def.outlineWidth);
        }

        [Fact]
        public void 頭打ちの距離を継承する()
        {
            var s = Filled();
            s.common.outlineNearDistance = 0.5f;
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineNearDistance, true);
            s.hair.outlineNearDistance = 0f;

            Assert.Equal(0f, s.Resolve(PartGroup.Hair).outlineNearDistance);
            Assert.Equal(0.5f, s.Resolve(PartGroup.Skin).outlineNearDistance);
        }

        [Fact]
        public void 旧形式の幅の基準を含む設定を読める()
        {
            var path = Path.Combine(_dir, "outline-legacy.xml");
            var s = Filled();
            // 空の overrides は <overrides /> と書かれ、旧キーを差し込めない
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineWidth, true);
            MaidSettingsStore.Save(path, s);
            var xml = File.ReadAllText(path)
                .Replace("<outlineNearDistance>0.71</outlineNearDistance>",
                    "<outlineWidthMode>Screen</outlineWidthMode><outlineScreenWidth>0.004</outlineScreenWidth>")
                .Replace("<string>outlineWidth</string>", "<string>outlineWidth</string><string>outlineWidthMode</string>");
            Assert.DoesNotContain("outlineNearDistance", xml);
            Assert.Contains("<string>outlineWidthMode</string>", xml);
            File.WriteAllText(path, xml);

            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            Assert.Equal(0.71f, loaded.common.outlineNearDistance);
            Assert.Equal(0.71f, loaded.Resolve(PartGroup.Costume).outlineNearDistance);
        }
```

（`Assert.DoesNotContain` / `Assert.Contains` は置換の空振りを検出するためのもの。落ちたら保存 XML の実際の書式（数値の表記・インデント）に置換文字列だけ合わせる）

旧形式のプリセット（`MaidPreset`）も同じ `GroupSettings` を XmlSerializer で読むため、同じ仕組みで無視される。テストは設定ファイル側の 1 本で代表させる。

範囲外として残すもの: `MToonProps.OutlineModeScreen` / `KeywordOutlineScreen` と `MToonRenderState` の Screen 分岐（シェーダーにある本家 MToon の機能に対応する定数で、`MToonRenderStateTests` も使っている）。

旧設定の扱い: 画面基準にしていた設定は、画面基準の幅を捨ててワールド幅（`outlineWidth`）で描く。リリース前のため変換はしない。

- [ ] **Step 2: テストが落ちることを確認**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`outlineNearDistance` / `OutlineNearDistance` / `Mode(bool)` が無い）

- [ ] **Step 3: 実装**

`OutlineWidthMode.cs` を削除する。

`GroupSettings.cs` のアウトライン欄（54〜57 行）:

```csharp
        public bool outlineEnabled = true;
        public float outlineWidth = 0.0008f;
        // 1 にすると、ゲームと同じ幅の値でも線が約 1.4 倍太くなる (ゲームの線の厚みは _OutlineWidth の 0.70 倍。実測)
        public float outlineWidthScale = 0.7f;
        public float outlineNearDistance = 0.71f;
        public float outlineLightingMix = 0.5f;
```

（「1 つの値を基準で読み替えると…」のコメントは対象が無くなるので消す）

`CopyField` の 2 ケースを次の 3 ケースに置き換える:

```csharp
                case GroupSettingKeys.OutlineWidth: outlineWidth = from.outlineWidth; break;
                case GroupSettingKeys.OutlineWidthScale: outlineWidthScale = from.outlineWidthScale; break;
                case GroupSettingKeys.OutlineNearDistance: outlineNearDistance = from.outlineNearDistance; break;
```

`GroupSettingKeys.cs`: `OutlineWidthMode` 定数を `public const string OutlineWidthScale = "outlineWidthScale";` と `public const string OutlineNearDistance = "outlineNearDistance";` の 2 行に置き換え、`All` の `OutlineWidthMode, OutlineWidth,` を `OutlineWidth, OutlineWidthScale, OutlineNearDistance,` にする。

`OutlineParams.cs` の `Mode` と `Width` を置き換える:

```csharp
        /// <summary>_OutlineWidthMode に書く値。outline は元のシェーダーに線があり、設定で描くとき true</summary>
        public static int Mode(bool outline)
        {
            return outline ? MToonProps.OutlineModeWorld : MToonProps.OutlineModeNone;
        }

        /// <summary>MToon の _OutlineWidth (m) に書く値。width はゲームの _OutlineWidth と同じ尺度、scale は GroupSettings.outlineWidthScale</summary>
        public static float Width(float width, float scale)
        {
            return width * scale;
        }
```


`MToonProps.cs` のフォーク欄（`UseToonRamp` の後）:

```csharp
        public const string OutlineNearDistance = "_OutlineNearDistance";
```

`MaterialConverter.cs` 126〜128 行:

```csharp
            var outlineMode = OutlineParams.Mode(outline);
            m.SetInt(MToonProps.OutlineWidthMode, outlineMode);
            m.SetFloat(MToonProps.OutlineWidth, OutlineParams.Width(settings.outlineWidth, settings.outlineWidthScale));
            m.SetFloat(MToonProps.OutlineNearDistance, settings.outlineNearDistance);
```

`MToonMaidWindow.cs`:
- 32〜36 行の `OutlineWidthModeLabels` と `OutlineWidthModeChoiceWidth`（とそのコメント）を削除する
- 424〜446 行（幅の基準の `ChoiceRow` から幅の `Slider` まで）を次に置き換える:

```csharp
            // 既定の書式 (小数 2 桁) では 0.0008 が 0.00 に見えるため 4 桁で出す
            Slider(view, GroupSettingKeys.OutlineWidth, "幅", shown.outlineWidth, 0f, 0.01f, def.outlineWidth,
                (s, v) => s.outlineWidth = v, FloatFieldType.F4);
            Slider(view, GroupSettingKeys.OutlineWidthScale, "幅の倍率", shown.outlineWidthScale, 0f, 2f, def.outlineWidthScale,
                (s, v) => s.outlineWidthScale = v);
            Slider(view, GroupSettingKeys.OutlineNearDistance, "頭打ちの距離", shown.outlineNearDistance, 0f, 2f, def.outlineNearDistance,
                (s, v) => s.outlineNearDistance = v);
```

- `ChoiceRow` の `float choiceWidth = ChoiceWidth` 引数は呼び出し元が既定値だけになるので削除し、本体の `choiceWidth` を `ChoiceWidth` に置き換える

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: プラグインのビルド確認**

Run: `MSBuild source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj /p:Configuration=Debug`（実機へ反映したくないとき。反映してよければ `debug.bat`）
Expected: 0 エラー

- [ ] **Step 6: Commit**

```bash
git add -A source/COM3D25.MToonMaid.Plugin source/COM3D25.MToonMaid.Plugin.Tests
git commit -m "feat(outline): 幅の基準の切り替えをやめて近距離の頭打ちの距離を設定できるようにする"
```

---

### Task 3: README の更新と実機確認

**Files:**
- Modify: `README.md:166-176`（アウトラインの表と注記）

- [ ] **Step 1: README のアウトラインの表を書き換える**

「幅の基準」の行を削除し、「幅」と「頭打ちの距離」を次にする:

```markdown
| 幅 | 0〜0.01 | 0.0008 | 線の太さ。ゲームのマテリアルの線の幅と同じ尺度。カメラから遠いほど細く見える |
| 幅の倍率 | 0〜2 | 0.7 | 幅に掛けて線の太さ（m）にする倍率。既定の 0.7 でゲームの線と同じ太さ（幅 0.0008 で約 0.56mm）になる |
| 頭打ちの距離 | 0〜2 | 0.71 | カメラからこの距離（m）より近いと、寄っても画面上の線が太くならない。ゲームの線と同じ 0.71 が既定。0 で頭打ちしない |
```

表の下の「幅は基準ごとに別の値を覚えます…」の注記を削除する。

- [ ] **Step 2: 実機確認**（restart-verify スキル）

ゲームを終了 → `debug.bat` → 起動 → セーブをロードし、eval で確かめる:
- 変換後の body マテリアルの `_OutlineNearDistance` が 0.71、`_OutlineWidthMode` が 1、`_OutlineWidth` が 0.00056
- 球の測定で、同じ幅 0.0008 のゲームのシェーダーと MToon の線の厚みが 0.71m 以上でほぼ一致する
- 球の測定（memory `game-outline-width-formula` の手順）で、MToon の線の「厚み / 幅」が 0.71m 以上で一定、未満で深度に比例する。`_OutlineNearDistance = 0` で近距離でも一定
- ウィンドウのアウトライン欄に「頭打ちの距離」が出て、共通タブと部位タブ（個別 ON）の両方で、スライダーと数値入力の操作がマテリアルの値に反映される（他の行の入力欄がずれない）
- `tail_log` に `COM3D25.MToonMaid` 由来の例外が無い

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: アウトラインの頭打ちの距離を README に書く"
```

## レビュー却下メモ

- ビュー深度の変換が 2 回になる — 頂点 1 つにつき行列 1 回の差で、幅が深度に依存するため最終位置は計算し直しが要る。読みやすさを優先して残す
- `_OutlineNearDistance` を float にする — 上限 2m では half で足り、隣の `_OutlineLightingMix` と揃える
- UI のラベルに「（0で無効）」を足す — 他の行もラベルは名前だけで、意味は README に書く流儀
