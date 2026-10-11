# COM3D25.MToonMaid.Plugin ゲームのリムの鋭さ・持ち上げを引き継ぐ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MToon にしたとき、マテリアルごとのゲームの `_RimPower` / `_RimShift` をリムの鋭さ・持ち上げとして引き継ぎ、部位タブでは倍率・加算で調整できるようにする。

**Architecture:** 換算（倍率・加算）を Unity に依存しない静的クラス `RimParams` にして単体テストする。`MaterialBackup` にゲームの値を控え、`MaterialConverter.ApplyGroup` で換算して入れる。グループ設定の `rimFresnelPower` / `rimLift` を `rimFresnelPowerScale` / `rimLiftOffset` に置き換え、UI のスライダーも差し替える。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-08-mtoon-maid-design.md` の「改訂 2026-10-10: ゲームのリムの鋭さ・持ち上げを引き継ぐ」節

テスト: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / ビルド: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`

## Global Constraints

- main で作業する。コメント・ログは日本語。`deploy.bat` は実行しない。MTEUtils は編集しない
- 設定ファイルの版（`MaidSettings.CurrentVersion` = 3）は上げない
- 継承キーの値: `rimFresnelPowerScale` / `rimLiftOffset`（設定ファイルに書くため以後変えない）
- 既定: 鋭さの倍率 1、持ち上げの加算 0。プロパティが無いマテリアルの控えは `_RimPower` 5 / `_RimShift` 0
- `_RimFresnelPower = max(0, rimPower × scale)`、`_RimLift = rimShift + offset`
- UI の範囲: 鋭さの倍率 0〜4（ゲームの値が 5 前後の衣装でも旧 UI の 20 まで届くように）、持ち上げの加算 -1〜1

## Review Focus

- **旧ファイルの `overrides` に `rimFresnelPower` / `rimLift` がある**: 読込でキーが消え、他の個別キーは残る → Task 2 のテスト
- **旧ファイルの `<rimFresnelPower>` 要素**: 例外にならず読み飛ばし、倍率 1 で読む → Task 2 のテスト
- **倍率 0 や負の倍率（手編集）**: 鋭さは 0 未満にならない → Task 1 のテスト
- **ゲームが Toony へ戻した後に menu で `_RimPower` が変わる**: 再変換で控え直す → Task 3 Step 2（`Reconvert`）、Task 3 Step 5 実機
- **SE の NPR 化などで他者がシェーダーを変えた後に OFF**: `_RimPower` / `_RimShift` は書いていないので戻す処理は不要 → Task 3 でコードを足さないことを確認

---

### Task 1: 換算の `RimParams`

**Files:**
- Create: `source/COM3D25.MToonMaid.Plugin/Rendering/RimParams.cs`
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/RimParamsTests.cs`

**Interfaces:**
- Produces: `RimParams.DefaultPower`（5f）、`RimParams.DefaultShift`（0f）、`float RimParams.FresnelPower(float gamePower, float scale)`、`float RimParams.Lift(float gameShift, float offset)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using COM3D25.MToonMaid.Plugin;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class RimParamsTests
    {
        [Fact]
        public void 鋭さはゲームの値に倍率を掛ける()
        {
            Assert.Equal(25f, RimParams.FresnelPower(25f, 1f));
            Assert.Equal(12.5f, RimParams.FresnelPower(25f, 0.5f));
        }

        [Fact]
        public void 鋭さは負にならない()
        {
            Assert.Equal(0f, RimParams.FresnelPower(25f, -1f));
            Assert.Equal(0f, RimParams.FresnelPower(-3f, 1f));
        }

        [Fact]
        public void 持ち上げはゲームの値に加算する()
        {
            Assert.Equal(0.2f, RimParams.Lift(0f, 0.2f), 5);
            Assert.Equal(-0.1f, RimParams.Lift(0.1f, -0.2f), 5);
        }
    }
}
```

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: `RimParams` が無くビルドエラー

- [ ] **Step 3: 実装**

```csharp
using System;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>ゲームの Toony のリム (_RimPower / _RimShift) から MToon の _RimFresnelPower / _RimLift を作る</summary>
    public static class RimParams
    {
        public const float DefaultPower = 5f;
        public const float DefaultShift = 0f;

        public static float FresnelPower(float gamePower, float scale)
        {
            return Math.Max(0f, gamePower * scale);
        }

        public static float Lift(float gameShift, float offset)
        {
            return gameShift + offset;
        }
    }
}
```

- [ ] **Step 4: 通過を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: PASS

- [ ] **Step 5: Commit** — `feat: ゲームのリムの鋭さ・持ち上げを倍率・加算で換算する`

### Task 2: グループ設定を倍率・加算へ置き換える

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettings.cs`（フィールド・`CopyField`・`FieldEquals`）
- Modify: `source/COM3D25.MToonMaid.Plugin/Settings/GroupSettingKeys.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/UI/MToonMaidWindow.cs:348-349`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs:103-104`（コンパイルを通すための一時的な置き換え。Task 3 で換算に変える）
- Test: `source/COM3D25.MToonMaid.Plugin.Tests/CommonSettingsTests.cs`

**Interfaces:**
- Produces: `GroupSettings.rimFresnelPowerScale`（float, 1f）、`GroupSettings.rimLiftOffset`（float, 0f）、`GroupSettingKeys.RimFresnelPowerScale = "rimFresnelPowerScale"`、`GroupSettingKeys.RimLiftOffset = "rimLiftOffset"`

- [ ] **Step 1: テストを書き換え・追加する**

`ChangeAll` の 2 行を差し替える:

```csharp
            g.rimFresnelPowerScale = 0.5f;
            g.rimLiftOffset = 0.2f;
```

旧ファイルのテストを足す（既存の XML 読込テストと同じく `_dir` に書いて `MaidSettingsStore.Load(path, _warnings.Add)` で読む。ファイルの書き方は同ファイル内の既存テストのヘルパーに合わせる）:

```csharp
        [Fact]
        public void 旧ファイルのリムの鋭さと持ち上げは読み飛ばしゲームの値どおりにする()
        {
            var path = Path.Combine(_dir, "old-rim.xml");
            File.WriteAllText(path,
                "<MToonMaid version=\"3\"><enabled>true</enabled>" +
                "<hair><rimFresnelPower>10</rimFresnelPower><rimLift>0.3</rimLift>" +
                "<overrides><string>rimFresnelPower</string><string>rimLift</string><string>shadingShift</string></overrides></hair>" +
                "<common><rimFresnelPower>8</rimFresnelPower></common>" +
                "</MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s.hair.overrides);
            Assert.Equal(1f, s.Resolve(PartGroup.Hair).rimFresnelPowerScale);
            Assert.Equal(0f, s.Resolve(PartGroup.Hair).rimLiftOffset);
            Assert.Empty(_warnings);
        }
```

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: `rimFresnelPowerScale` が無くビルドエラー

- [ ] **Step 3: 実装**

`GroupSettings.cs` のリムのフィールド:

```csharp
        // リム。上書きしなければゲームの _RimColor を使う。鋭さ・持ち上げはゲームの _RimPower / _RimShift への倍率・加算
        public bool rimColorOverride;
        public ColorData rimColor;
        public float rimFresnelPowerScale = 1f;
        public float rimLiftOffset;
        public float rimLightingMix = 1f;
```

`CopyField`:

```csharp
                case GroupSettingKeys.RimFresnelPowerScale: rimFresnelPowerScale = from.rimFresnelPowerScale; break;
                case GroupSettingKeys.RimLiftOffset: rimLiftOffset = from.rimLiftOffset; break;
```

`FieldEquals`:

```csharp
                case GroupSettingKeys.RimFresnelPowerScale: return FloatEquals(rimFresnelPowerScale, other.rimFresnelPowerScale);
                case GroupSettingKeys.RimLiftOffset: return FloatEquals(rimLiftOffset, other.rimLiftOffset);
```

`GroupSettingKeys.cs`: `RimFresnelPower` / `RimLift` の定数を消し、

```csharp
        public const string RimFresnelPowerScale = "rimFresnelPowerScale";
        public const string RimLiftOffset = "rimLiftOffset";
```

を足して `All` の該当行を `RimColor, RimFresnelPowerScale, RimLiftOffset, RimLightingMix,` にする。

`MToonMaidWindow.cs` の 2 行:

```csharp
            Slider(view, GroupSettingKeys.RimFresnelPowerScale, "鋭さの倍率", shown.rimFresnelPowerScale, 0f, 4f, def.rimFresnelPowerScale, (s, v) => s.rimFresnelPowerScale = v);
            Slider(view, GroupSettingKeys.RimLiftOffset, "持ち上げの加算", shown.rimLiftOffset, -1f, 1f, def.rimLiftOffset, (s, v) => s.rimLiftOffset = v);
```

`MaterialConverter.ApplyGroup` の 2 行（Task 3 で置き換える。ここではゲームの値なしの既定で換算）:

```csharp
            m.SetFloat(MToonProps.RimFresnelPower, RimParams.FresnelPower(RimParams.DefaultPower, settings.rimFresnelPowerScale));
            m.SetFloat(MToonProps.RimLift, RimParams.Lift(RimParams.DefaultShift, settings.rimLiftOffset));
```

- [ ] **Step 4: 通過を確認** — Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` と build / Expected: PASS・ビルド成功。`rg -n "rimFresnelPower\b|rimLift\b|RimFresnelPower\b|RimLift\b" source` で `MToonProps` 以外に旧名が残っていないこと

- [ ] **Step 5: Commit** — `feat: リムの鋭さ・持ち上げの設定をゲームの値への倍率・加算にする`

### Task 3: ゲームの値を控えて換算する

**Files:**
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialBackup.cs`
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MaterialConverter.cs`（`Convert` / `Reconvert` / `ApplyGroup`）
- Modify: `source/COM3D25.MToonMaid.Plugin/Rendering/MToonProps.cs`
- Modify: `README.md`（リムの項目の説明があれば。無ければ使い方に 1 行）

**Interfaces:**
- Consumes: `RimParams`（Task 1）、`GroupSettings.rimFresnelPowerScale` / `rimLiftOffset`（Task 2）
- Produces: `MaterialBackup.rimPower` / `rimShift`、`MToonProps.GameRimPower = "_RimPower"` / `GameRimShift = "_RimShift"`

- [ ] **Step 1: プロパティ名と控え**

`MToonProps.cs` の `_ToonRamp` の下に:

```csharp
        // ゲームの Toony のリムの鋭さ・持ち上げ。MToon は書かないため控えるだけで戻さない
        public const string GameRimPower = "_RimPower";
        public const string GameRimShift = "_RimShift";
```

`MaterialBackup.cs` の `rimColor` の下に:

```csharp
        public float rimPower;
        public float rimShift;
```

- [ ] **Step 2: 変換・再変換で控える**

`MaterialConverter` に:

```csharp
        private static void ReadRim(Material m, MaterialBackup backup)
        {
            backup.rimPower = m.HasProperty(MToonProps.GameRimPower) ? m.GetFloat(MToonProps.GameRimPower) : RimParams.DefaultPower;
            backup.rimShift = m.HasProperty(MToonProps.GameRimShift) ? m.GetFloat(MToonProps.GameRimShift) : RimParams.DefaultShift;
        }
```

`Convert` の `ReadRamp(m, backup);` の直後と、`Reconvert` の `ReadRamp(m, backup);` の直後に `ReadRim(m, backup);` を足す（どちらも m はまだ Toony なので `HasProperty` が効く。MToon にした後は `HasProperty` が false になる）。

- [ ] **Step 3: 換算して入れる**

`ApplyGroup` の 2 行を:

```csharp
            m.SetFloat(MToonProps.RimFresnelPower, RimParams.FresnelPower(backup.rimPower, settings.rimFresnelPowerScale));
            m.SetFloat(MToonProps.RimLift, RimParams.Lift(backup.rimShift, settings.rimLiftOffset));
```

`RestoreUnchecked` には何も足さない（`_RimPower` / `_RimShift` は書いていない）。

- [ ] **Step 4: ビルドとテスト** — Run: build と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` / Expected: 成功

- [ ] **Step 5: 実機確認**（ゲーム起動中なら DLL はコピーされないので、ユーザーに再起動を依頼するか、停止中に `debug.bat com3d25` で反映する。ゲームを終了させる前にシーンの保存の有無を確認する）
  - MToon にしたメイドで、顔・体・乳首などのマテリアルの `_RimFresnelPower` がゲームの `_RimPower`（30 / 20.4 / 25 / 28.4 など）になっていること（`eval_csharp` で `GetFloat("_RimFresnelPower")` を列挙）
  - 鋭さの倍率 0.5 で半分、持ち上げの加算 0.1 で `_RimLift` が 0.1 増えること
  - OFF で Toony に戻り、`_RimPower` が変わっていないこと
  - 再変換の控え直し: `eval_csharp` でマテリアルを Toony（控えの shader）へ戻し `_RimPower` を 10 にしてから監視に再変換させ、`_RimFresnelPower` が 10 × 倍率になること。MToon にしている間の `_RimPower` の変更は反映しない（仕様）

- [ ] **Step 6: README** — `rg -n "リム" README.md` で使い方の説明を確認し、「リムの鋭さ・持ち上げはゲームのマテリアルの値を引き継ぎ、倍率・加算で調整する」旨と、「以前の版で手で変えた鋭さ・持ち上げは引き継がれず、ゲームの値に戻る」旨を共通タブの説明（17 行目付近）の近くに足す

- [ ] **Step 7: Commit** — `feat: MToon にしたときゲームのリムの鋭さ・持ち上げをマテリアルごとに引き継ぐ`

## レビュー却下メモ

- `Math.Max(0f, NaN)` が NaN を返す — 手編集で NaN を入れる経路は他の float 項目にもあり、この項目だけ丸める理由が薄い（YAGNI）
