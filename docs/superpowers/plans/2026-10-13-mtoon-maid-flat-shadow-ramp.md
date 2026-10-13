# 影側を一色にする（flatShadowRamp）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 顔が主光源の影側に入ったとき、ランプ（`_ToonRamp`）のグラデーションで立体的（リアル調）に見えるのを、グループごとのトグル「影側を一色に」で平らな影色にできるようにする。

**Architecture:** シェーダーに `_FlatShadowRamp`（0/1）を足し、ON のときは `_ShadowRateToon` の影側（SRT=0）でランプを `_ToonRampDarkColor`（ランプの最暗画素）一色にする。設定は `GroupSettings.flatShadowRamp`（共通から継承する普通の項目）。新規メイドとデフォルトプリセットでは顔だけ個別 ON。

**Tech Stack:** C# (.NET 3.5 / 4.7.1)、xUnit、Unity ShaderLab/HLSL（Unity 2022.3 と 5.6 でバンドル化）

**Spec:** なし（会話で決定）。原因調査の結論:
- 実機（GameView）で顔の中心は N·L ≈ -0.74（u ≈ 0.13）で、SRT の影側に完全に入っている
- 今の `GetMToonLighting_ToonRamp` は SRT=0 で `unshadowed = 1` となり、ランプを N·L の位置（u）で読み続ける。顔のランプ `toonface_SKB` は u=0〜0.35 で (0.98,0.80,0.83)→白 の滑らかなグラデーションなので、影の中に N·L 依存の陰影が乗り、陰影の濃さ 1.5 でさらに 1.5 倍に強調される
- 実機で `_UseToonRamp=0`、またはランプを `_ToonRampDarkColor` 一色のテクスチャへ差し替えると、顔が平らなトゥーンになることを確認済み

## Global Constraints

- コードのコメント・ログは日本語。コメントは「なぜ他の自然な選択肢を採らなかったか」だけ
- 設定キー文字列は設定ファイルに書くので `"flatShadowRamp"` 固定
- `MaidSettings.CurrentVersion` は上げない（要素の無いファイルはフィールド初期値 false で読む）
- ビルド確認は dotnet build（ゲームフォルダへ反映しない）。`debug.bat` / `deploy.*` は実行しない
- テスト: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:\COM3D2_5`
- シェーダーの変更後は `build-bundle.bat all` で `Resources/mtoon_bundle` と `mtoon_bundle_com3d2` を作り直す（Unity エディタで UnityProject2022 を開いているとロックで失敗する）

## Review Focus

- 「ゲームの陰影」OFF（ランプを読まない）マテリアル: トグルは効かず、今と同じ見た目（`_ShadeColor × ramp.Dark` の平らな影）であること → シェーダーは `_UseToonRamp` の分岐の中だけを変える
- 光側（SRT=1）と境界付近: トグル ON でも光側のランプ（u 0.21〜0.35 の帯）と落ち影・前髪の影の扱いは変えないこと → `unshadowed` に SRT を掛けるだけで、SRT=1 では従来式と一致
- 既存の設定ファイル（要素なし）: 読み込みで警告が出ず、全グループ OFF（今の見た目のまま）になること → Task 1 のテスト
- 共通タブで ON にしたとき、個別にしていないグループ全部に効くこと → Task 1 の Resolve テスト
- アウトラインのパス: ランプは線の陰影にも掛かるので、ON で線の影側も平らになる（意図どおり）。落ち影は今も線では見ない（`castShadow = 1`）

---

### Task 1: 設定項目 flatShadowRamp

**Files:**
- Modify: `source/COM3D2.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Settings/GroupSettings.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Settings/MaidSettings.cs`（`CreateGroupDefault`）
- Modify: `UnityInjector/Config/MToonMaid/Presets/デフォルト.xml`
- Test: `source/COM3D2.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`

**Interfaces:**
- Produces: `GroupSettingKeys.FlatShadowRamp = "flatShadowRamp"`、`GroupSettings.flatShadowRamp`（bool、既定 false）

- [ ] **Step 1: 失敗するテストを書く**（`CommonSettingsTests` に追加し、既存の `新しいメイドは肌だけ陰影の境界の鋭さを個別に0_8にする` の `Assert.Empty(s.face.overrides)` を顔の個別 ON の検証に差し替える）

```csharp
        [Fact]
        public void 影側を一色には既定でOFFで共通から継承する()
        {
            Assert.Contains(GroupSettingKeys.FlatShadowRamp, GroupSettingKeys.All);
            Assert.False(GroupSettingKeys.IsGroupOnly(GroupSettingKeys.FlatShadowRamp));
            Assert.False(GroupSettings.CreateDefault().flatShadowRamp);

            var s = Filled();
            s.common.flatShadowRamp = true;

            Assert.True(s.Resolve(PartGroup.Hair).flatShadowRamp);
        }

        [Fact]
        public void 新しいメイドは顔だけ影側を一色にする()
        {
            var s = Filled();

            Assert.Equal(new[] { GroupSettingKeys.FlatShadowRamp }, s.face.overrides);
            Assert.True(s.Resolve(PartGroup.Face).flatShadowRamp);
            Assert.False(s.Resolve(PartGroup.Skin).flatShadowRamp);
            Assert.False(s.Resolve(PartGroup.Hair).flatShadowRamp);
            Assert.False(s.Resolve(PartGroup.Costume).flatShadowRamp);
        }

        [Fact]
        public void 影側を一色にの要素が無いファイルはOFFで読む()
        {
            var path = Path.Combine(_dir, "old.xml");
            MaidSettingsStore.Save(path, Filled());
            var xml = File.ReadAllText(path);
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"\s*<flatShadowRamp>\w+</flatShadowRamp>", "")
                .Replace("<string>flatShadowRamp</string>", "");
            File.WriteAllText(path, xml);

            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            Assert.False(loaded.Resolve(PartGroup.Face).flatShadowRamp);
        }
```

既存テストの差し替え:

```csharp
            Assert.Equal(new[] { GroupSettingKeys.FlatShadowRamp }, s.face.overrides);
```

`ChangeAll`（全項目を既定と違う値にするヘルパー）に足す。足さないと `全項目を個別にすると共通をどう変えてもグループの値のまま` が落ちる:

```csharp
            g.flatShadowRamp = true;
```

- [ ] **Step 2: 失敗を確認** — `dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:\COM3D2_5 --filter FullyQualifiedName~CommonSettingsTests` → コンパイルエラー（`FlatShadowRamp` 未定義）

- [ ] **Step 3: 実装**

`GroupSettingKeys.cs`: 定数 `public const string FlatShadowRamp = "flatShadowRamp";` を `AdditionalLightShade` の後に足し、`All` の陰影の行へ `AdditionalLightShade` の後に入れる（`IsGroupOnly` / `AppliesTo` は変えない＝全グループに出て共通から継承する）。

`GroupSettings.cs`: `additionalLightShade` の後に

```csharp
        // ゲームのランプを読むとき、主光源の影側 (_ShadowRateToon が 0 の側) をランプの最も暗い色一色で塗る
        public bool flatShadowRamp;
```

`CopyField` に `case GroupSettingKeys.FlatShadowRamp: flatShadowRamp = from.flatShadowRamp; break;`

`MaidSettings.CreateGroupDefault`:

```csharp
            if (group == PartGroup.Face)
            {
                g.flatShadowRamp = true;
                g.overrides.Add(GroupSettingKeys.FlatShadowRamp);
            }
```

（コメントは既存の「肌の鋭さを GroupSettings のフィールド初期値にすると…」を両方に効く文へ直す: `// 肌の鋭さ・顔の影側の一色を GroupSettings のフィールド初期値にすると、共通の既定が変わって他のグループにも効く`）

`デフォルト.xml`: 各グループと common の `<additionalLightShade>` の後に `<flatShadowRamp>false</flatShadowRamp>`（face だけ `true`）を入れ、face の `<overrides>` に `<string>flatShadowRamp</string>` を足す。

- [ ] **Step 4: 通過を確認** — 同じコマンドで PASS。続けて全テストも PASS
- [ ] **Step 5: コミット**（Task 2〜3 とまとめて 1 コミットでもよい）

### Task 2: シェーダー `_FlatShadowRamp` とマテリアルへの書き込み

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`（Properties と冒頭の改変一覧）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl`
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl:160-168`
- Modify: `source/COM3D2.MToonMaid.Plugin/Rendering/MToonProps.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Rendering/MaterialConverter.cs:ApplyGroup`
- Modify: `source/COM3D2.MToonMaid.Plugin/Resources/mtoon_bundle`, `mtoon_bundle_com3d2`（再ビルド）

**Interfaces:**
- Consumes: `GroupSettings.flatShadowRamp`
- Produces: シェーダープロパティ `_FlatShadowRamp`（Float、既定 0）、`MToonProps.FlatShadowRamp`

- [ ] **Step 1: シェーダー**

`.shader` の `_ToonRampDarkColor` の後:

```
        // COM3D2: 1 なら主光源の影側 (_ShadowRateToon が 0 の側) でランプを N·L で読まず _ToonRampDarkColor 一色にする (プラグインが設定する)
        _FlatShadowRamp ("Flat Shadow Ramp", Float) = 0
```

冒頭の改変一覧の `_UseToonRamp` の行の後:

```
// - _FlatShadowRamp が 1 のとき、主光源の影側ではランプを最も暗い色 (_ToonRampDarkColor) 一色にする (影の中に N·L のグラデーションを出さない)
```

`input.hlsl` の `half _UseToonRamp;` の後に `half _FlatShadowRamp;`。

`lighting_mtoon.hlsl` の `GetMToonLighting_ToonRamp`:

```hlsl
inline half3 GetMToonLighting_ToonRamp(const UnityLighting lighting, const MToonInput input, const half dotNL, const half hairShadow, const half shadeStrength)
{
    const float2 uv = MToon_ClampToTexels(GetMToonLighting_ToonUv(dotNL), _ToonRamp_TexelSize);
    const half3 ramp = _ToonRamp.SampleLevel(sampler_ShadowRateToon, uv, 0).rgb;
    const half castShadow = MToon_IsOutlinePass() ? 1 : lighting.directLightAttenuation.x;
    const half shadowRate = GetMToonLighting_ShadowRate(dotNL);
    // 影側を常に一色にすると、ゲームの Toony と同じく影の中もランプで陰影を付けたい衣装・髪までグラデーションが消える
    const half unshadowed = lerp(1, castShadow * (1 - hairShadow), shadowRate) * lerp(1, shadowRate, _FlatShadowRamp);
    return max(0, lerp(GetMToonLighting_ToonRampLit(), lerp(_ToonRampDarkColor.rgb, ramp, unshadowed), shadeStrength));
}
```

- [ ] **Step 2: C# 側**

`MToonProps.cs` の COM3D2 フォークのプロパティ群に `public const string FlatShadowRamp = "_FlatShadowRamp";`。

`MaterialConverter.ApplyGroup` の `m.SetColor(MToonProps.ToonRampDarkColor, ...)` の後に:

```csharp
            m.SetFloat(MToonProps.FlatShadowRamp, settings.flatShadowRamp ? 1f : 0f);
```

- [ ] **Step 3: バンドル再ビルド** — `build-bundle.bat all`（UnityProject56 の gitignore のコピーもこのスクリプトが同期する）。ログに `Shader error` が無いこと。バイナリのバンドルはレビューできないため、コミットメッセージに再ビルドしたことを書く
- [ ] **Step 4: ビルド** — `dotnet build source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj -c Debug -f net471 -p:COM3D25_DIR=W:\COM3D2_5` と `-f net35 -p:COM3D2_DIR=W:\COM3D2` が通る

### Task 3: UI と README

**Files:**
- Modify: `source/COM3D2.MToonMaid.Plugin/UI/MToonMaidWindow.cs:DrawSettings`
- Modify: `README.md`（陰影の表）

- [ ] **Step 1: UI** — 「境界の硬さ」スライダーの直後（「環境光の均一化」の前）に:

```csharp
            Toggle(view, GroupSettingKeys.FlatShadowRamp, "影側を一色に",
                "主光源の影側をゲームのランプの最も暗い色一色で塗る\nOFF では影の中もランプのグラデーションで明るさが変わる\n「ゲームの陰影」が ON のマテリアルにだけ効く",
                shown.flatShadowRamp, (s, v) => s.flatShadowRamp = v, gameShadowRate);
```

（「ゲームの陰影」OFF ではランプを読まず効かないため無効表示にする。行は消さない＝入力欄キャッシュの番号がずれない）

- [ ] **Step 2: README** — 陰影の表の「境界の硬さ」の行の後に:

```
| 影側を一色に | ON / OFF | 顔だけ ON | 主光源の影側をゲームのランプの最も暗い色一色で塗る。OFF ではゲームと同じく影の中もランプのグラデーションで明るさが変わり、顔が影側に入ると立体的に見える。「ゲームの陰影」が ON のマテリアルにだけ効く |
```

表の下の注記に追加:

```
- 「影側を一色に」は新しいメイドとプリセット「デフォルト」では顔だけ ON です。このバージョンより前に設定したメイドは OFF のままなので、顔タブで ON にしてください
```

- [ ] **Step 3: 実機確認**（ゲーム起動中なら devbridge）— DLL とバンドルは再起動しないと反映されないため、`restart-verify` スキルの手順に従うかユーザーに確認する。確認内容:
  - GameView のキャプチャで、顔タブの「影側を一色に」ON で顔の影が平らな影色、OFF で従来のグラデーション
  - 顔が光側・影側の境界にかかる向きで、ON にしても境界付近の帯が不自然に痩せたり暗くなったりしない（SRT の遷移帯 0<SRT<1 では式が変わるため）
  - 髪・衣装は既定 OFF で見た目が変わらない。プリセット「デフォルト」を適用すると顔が ON になる
- [ ] **Step 4: コミット**（`feat(shading): 影側をランプの最も暗い色一色にする設定を足す`）

## レビュー却下メモ

- 顔だけ ON だと、同じ肌ランプの首・耳（肌グループ）と段差が出うる — 要望が顔に限られ、肌も共通タブ／肌タブで ON にできるため割り切る
- 既存メイドを読み込み時に ON へ移行する — 「ユーザーが OFF にした」と「未設定」を区別できないため不採用（README で周知する）
