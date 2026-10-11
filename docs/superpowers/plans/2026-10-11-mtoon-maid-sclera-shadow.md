# 白目の影 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task（このリポジトリでは subagent-driven-development は使わない）。Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 口のアトラスに描き込まれた白目の影の代わりに、スライダーで形と色を決められる白目の影を MToon10 シェーダーで描く。

**Architecture:** MToon10 のフラグメントで、`_ScleraRect`（白目の UV の外接矩形）の内側だけ色を「白目の色 + 影」へ置き換える。光側の色と影側の `_ShadowTex` の両方を置き換える。C# の `MaidScleraShadow` が、head スロットの名前に `Mouth` を含む MToon のマテリアルを探し、瞳のサブメッシュの外接箱に入る三角形から矩形を求めて設定値と一緒に入れる。まぶたの閉じ具合は瞳と共通の `LidClosureReader` で毎フレーム読む。

**Tech Stack:** C# 7.3（net471 = COM3D2.5 / net35 = COM3D2 2.0）、Unity ShaderLab/HLSL（Unity 2022.3 と 5.6 の両方でコンパイル）、xUnit（net48）

**Spec:** `docs/superpowers/specs/2026-10-11-mtoon-maid-sclera-shadow-design.md`

## Global Constraints

- main で作業する（git worktree を使わない）。コードのコメント・ログは日本語。コメントは「なぜ他の自然な選択肢を採らなかったか」だけを書く
- `deploy.bat` / `deploy.ps1` は実行しない。MTEUtils（submodule）は編集しない
- `build-bundle.bat` は Unity エディタを閉じて実行する。`debug.bat` はゲーム停止中だと DLL を実機へ反映するので、実機確認のタスク以外では `dotnet build` を使う
- ゲームを終了する前に、シーンの保存の有無をユーザーに確認する
- テスト: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests`
- ビルド（実機へコピーしない）: `dotnet build source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj -c Debug -f net471` と `dotnet build source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj -c Debug -f net35 -p:COM3D2_DIR=W:/COM3D2`
- テストの net48 では UnityEngine を読めないため、テストする型は Unity の型を使わない（`IrisBounds` と同じ作法）
- `_ScleraRect` の zw が 0 のマテリアルでは見た目を一切変えない（既存の全マテリアルが対象）
- 既定値: 影の強さ 1、影の色 `#E0D3C9`、高さ 0.5、ぼかし 0.1、反り 0.2、まぶたへの追従 0.2、白目の色 白、白目の影を描く OFF、白目の色を指定する OFF

## Review Focus

- **白目の無い顔・瞳のサブメッシュが 2 個未満のメッシュ**: 例外を出さず範囲なしになり、UI に「白目が見つかりません」が出る → Task 1（該当なし・空配列）、Task 5（`subMeshCount` の確認）
- **白目の影 OFF・メイドの MToon OFF・顔スロットの除外**: 前に入れた `_ScleraRect` が残らず、描き込みの見た目に戻る → Task 5 の `Clear` と Task 7 の実機確認
- **口のマテリアルの差し替え（顔の付け替え）**: 前のマテリアルの範囲を消し、新しいメッシュで求め直す → Task 5（メッシュ ID とマテリアル番号のキャッシュ）、Task 7
- **NaN・範囲外の手編集値**: 0〜1 へ丸まり、シェーダーへ NaN が届かない → Task 3 のテスト
- **口内・歯・舌**: 白目の矩形の外は今と同じ色になる → Task 4（矩形の外では分岐しない）、Task 7 の実機確認

---

### Task 1: 白目の UV の範囲を求める `ScleraBounds`

**Files:**
- Create: `source/COM3D2.MToonMaid.Plugin/Rendering/ScleraBounds.cs`
- Test: `source/COM3D2.MToonMaid.Plugin.Tests/ScleraBoundsTests.cs`

**Interfaces:**
- Produces: `public static bool ScleraBounds.TryCompute(float[] positions, float[] uvs, IList<int[]> eyeTriangles, int[] scleraTriangles, out IrisRect rect)`
  - `positions` は頂点ごとに x, y, z を並べた配列（長さ 3n）、`uvs` は u, v を並べた配列（長さ 2n）
  - `eyeTriangles` は目ごとの三角形の頂点番号（左目・右目のサブメッシュ）。目ごとに外接箱を作る
  - 3 頂点がすべていずれか 1 つの箱の内側（境界を含む）にある白目の三角形の UV の外接矩形を返す。無ければ false で rect は `default(IrisRect)`（全部 0）
  - 矩形の型は既存の `IrisRect`（x, y, width, height）を使う

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class ScleraBoundsTests
    {
        // 頂点 0〜2 = 目の三角形 (x 0〜1, y 0〜1, z 0)。3〜5 = 箱の内側の白目、6〜8 = 1 頂点だけ箱の外の白目
        private static readonly float[] Positions =
        {
            0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f,
            0.1f, 0.1f, 0f, 0.5f, 0.1f, 0f, 0.1f, 0.5f, 0f,
            0.2f, 0.2f, 0f, 2f, 0.2f, 0f, 0.2f, 0.4f, 0f,
        };

        private static readonly float[] Uvs =
        {
            0f, 0f, 0f, 0f, 0f, 0f,
            0.1f, 0.2f, 0.3f, 0.2f, 0.1f, 0.4f,
            0.8f, 0.8f, 0.9f, 0.9f, 0.8f, 0.9f,
        };

        private static readonly int[] Eye = { 0, 1, 2 };

        [Fact]
        public void 目の箱の内側の三角形のUVの外接矩形を返す()
        {
            IrisRect r;
            Assert.True(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { Eye }, new[] { 3, 4, 5 }, out r));

            Assert.Equal(0.1f, r.x, 5);
            Assert.Equal(0.2f, r.y, 5);
            Assert.Equal(0.2f, r.width, 5);
            Assert.Equal(0.2f, r.height, 5);
        }

        [Fact]
        public void 一頂点でも箱の外の三角形は含めない()
        {
            IrisRect r;
            Assert.True(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { Eye }, new[] { 3, 4, 5, 6, 7, 8 }, out r));

            Assert.Equal(0.1f, r.x, 5);
            Assert.Equal(0.2f, r.width, 5);
        }

        [Fact]
        public void 箱の境界上の頂点は内側とみなす()
        {
            var positions = (float[])Positions.Clone();
            // 頂点 4 を箱の角 (1, 0, 0) へ
            positions[12] = 1f;
            positions[13] = 0f;

            IrisRect r;
            Assert.True(ScleraBounds.TryCompute(positions, Uvs, new List<int[]> { Eye }, new[] { 3, 4, 5 }, out r));
        }

        [Fact]
        public void 目ごとの箱の間にある三角形は含めない()
        {
            // 左目 x 0〜1、右目 x 3〜4。白目の三角形は x 1.5〜2.5 (両目を 1 つの箱にすると内側になる)
            var positions = new float[]
            {
                0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f,
                3f, 0f, 0f, 4f, 0f, 0f, 3f, 1f, 0f,
                1.5f, 0.1f, 0f, 2.5f, 0.1f, 0f, 2f, 0.5f, 0f,
            };
            var uvs = new float[18];

            IrisRect r;
            Assert.False(ScleraBounds.TryCompute(positions, uvs,
                new List<int[]> { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } }, new[] { 6, 7, 8 }, out r));
        }

        [Fact]
        public void 左右の目の箱それぞれの三角形をまとめた外接矩形を返す()
        {
            // 左目 x 0〜1、右目 x 3〜4。白目の三角形は左右に 1 つずつ (実機と同じく UV は近い範囲)
            var positions = new float[]
            {
                0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f,
                3f, 0f, 0f, 4f, 0f, 0f, 3f, 1f, 0f,
                0.1f, 0.1f, 0f, 0.5f, 0.1f, 0f, 0.1f, 0.5f, 0f,
                3.1f, 0.1f, 0f, 3.5f, 0.1f, 0f, 3.1f, 0.5f, 0f,
            };
            var uvs = new float[]
            {
                0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f,
                0.1f, 0.1f, 0.3f, 0.1f, 0.1f, 0.3f,
                0.2f, 0.2f, 0.4f, 0.2f, 0.2f, 0.5f,
            };

            IrisRect r;
            Assert.True(ScleraBounds.TryCompute(positions, uvs,
                new List<int[]> { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } }, new[] { 6, 7, 8, 9, 10, 11 }, out r));

            Assert.Equal(0.1f, r.x, 5);
            Assert.Equal(0.1f, r.y, 5);
            Assert.Equal(0.3f, r.width, 5);
            Assert.Equal(0.4f, r.height, 5);
        }

        [Fact]
        public void 該当する三角形が無ければfalseで全部0()
        {
            IrisRect r;
            Assert.False(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { Eye }, new[] { 6, 7, 8 }, out r));

            Assert.Equal(0f, r.width);
            Assert.Equal(0f, r.height);
        }

        [Fact]
        public void 目や白目の三角形が空ならfalse()
        {
            IrisRect r;
            Assert.False(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]>(), new[] { 3, 4, 5 }, out r));
            Assert.False(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { new int[0] }, new[] { 3, 4, 5 }, out r));
            Assert.False(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { Eye }, new int[0], out r));
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests --filter ScleraBoundsTests`
Expected: ビルドエラー（`ScleraBounds` が無い）

- [ ] **Step 3: 実装する**

```csharp
using System;
using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 口のアトラスのうち白目の UV の範囲を、瞳のメッシュの外接箱に入る三角形から求める。
    /// Unity の型を使わない (テストの net48 では UnityEngine.CoreModule を読めないため)
    /// </summary>
    public static class ScleraBounds
    {
        /// <summary>
        /// positions は頂点ごとの x, y, z、uvs は u, v。eyeTriangles は目ごとのサブメッシュの頂点番号で、目ごとに外接箱を作る。
        /// 3 頂点がどれか 1 つの箱の内側 (境界を含む) にある白目の三角形の UV の外接矩形を返す。無ければ false で rect は全部 0
        /// </summary>
        public static bool TryCompute(float[] positions, float[] uvs, IList<int[]> eyeTriangles, int[] scleraTriangles, out IrisRect rect)
        {
            rect = default(IrisRect);
            var boxes = new List<float[]>();
            foreach (var eye in eyeTriangles)
            {
                if (eye != null && eye.Length > 0)
                {
                    boxes.Add(Box(positions, eye));
                }
            }
            if (boxes.Count == 0 || scleraTriangles == null)
            {
                return false;
            }

            var minU = float.MaxValue;
            var minV = float.MaxValue;
            var maxU = float.MinValue;
            var maxV = float.MinValue;
            var found = false;
            for (var t = 0; t + 2 < scleraTriangles.Length; t += 3)
            {
                if (!InsideAnyBox(positions, boxes, scleraTriangles, t))
                {
                    continue;
                }
                found = true;
                for (var j = 0; j < 3; j++)
                {
                    var i = scleraTriangles[t + j];
                    minU = Math.Min(minU, uvs[i * 2]);
                    maxU = Math.Max(maxU, uvs[i * 2]);
                    minV = Math.Min(minV, uvs[i * 2 + 1]);
                    maxV = Math.Max(maxV, uvs[i * 2 + 1]);
                }
            }
            if (!found)
            {
                return false;
            }
            rect = new IrisRect(minU, minV, maxU - minU, maxV - minV);
            return true;
        }

        // 戻り値は min x, y, z, max x, y, z
        private static float[] Box(float[] positions, int[] indices)
        {
            var box = new[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue };
            foreach (var i in indices)
            {
                for (var a = 0; a < 3; a++)
                {
                    var p = positions[i * 3 + a];
                    box[a] = Math.Min(box[a], p);
                    box[a + 3] = Math.Max(box[a + 3], p);
                }
            }
            return box;
        }

        private static bool InsideAnyBox(float[] positions, List<float[]> boxes, int[] triangles, int t)
        {
            foreach (var box in boxes)
            {
                if (Inside(positions, box, triangles[t]) && Inside(positions, box, triangles[t + 1]) && Inside(positions, box, triangles[t + 2]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Inside(float[] positions, float[] box, int i)
        {
            for (var a = 0; a < 3; a++)
            {
                var p = positions[i * 3 + a];
                if (p < box[a] || p > box[a + 3])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
```

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests --filter ScleraBoundsTests`
Expected: 7 件 PASS

- [ ] **Step 5: コミット**（commit スキル。例: `feat(sclera): 瞳の外接箱から白目の UV の範囲を求める`）

---

### Task 2: 白目のマテリアルの判定

**Files:**
- Modify: `source/COM3D2.MToonMaid.Plugin/ToonMaterialClassifier.cs`
- Test: `source/COM3D2.MToonMaid.Plugin.Tests/ToonMaterialClassifierTests.cs`

**Interfaces:**
- Produces: `public static bool ToonMaterialClassifier.IsScleraMaterialName(string materialName)` — 名前に `Mouth` を含む（大文字小文字を区別しない）

- [ ] **Step 1: 失敗するテストを書く**（`ToonMaterialClassifierTests` クラスの末尾へ追加）

```csharp
        [Theory]
        [InlineData("Face006_Mouth", true)]
        [InlineData("face003_mouth (Instance)", true)]
        [InlineData("face_tokinagare_Skin (Instance)", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void 白目のマテリアルは名前にMouthを含むもの(string name, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsScleraMaterialName(name));
        }
```

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests --filter ToonMaterialClassifierTests`
Expected: ビルドエラー（`IsScleraMaterialName` が無い）

- [ ] **Step 3: 実装する**（`IsEyeHighlightMaterial` の後へ追加）

```csharp
        /// <summary>白目を含む口のアトラスのマテリアル (バニラの顔は FaceXXX_Mouth)</summary>
        public static bool IsScleraMaterialName(string materialName)
        {
            return materialName != null && materialName.IndexOf("Mouth", StringComparison.OrdinalIgnoreCase) >= 0;
        }
```

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests --filter ToonMaterialClassifierTests`
Expected: PASS

- [ ] **Step 5: コミット**（例: `feat(sclera): 白目を含む口のマテリアルを名前で判定する`）

---

### Task 3: 白目の影の設定（`EyeSettings`）

**Files:**
- Modify: `source/COM3D2.MToonMaid.Plugin/Settings/EyeSettings.cs`
- Test: `source/COM3D2.MToonMaid.Plugin.Tests/EyeSettingsTests.cs`

**Interfaces:**
- Produces（`EyeSettings` の public フィールド。後のタスクがこの名前で読む）:
  - `bool scleraShadowEnabled`（既定 false）、`bool scleraColorOverride`（既定 false）
  - `ColorData scleraColor`（`FillMissing` で白 (1,1,1,1)）、`ColorData scleraShadowColor`（`FillMissing` で `#E0D3C9` = (0xE0/255, 0xD3/255, 0xC9/255, 1)）
  - `float scleraShadow = 1f`、`scleraShadowHeight = 0.5f`、`scleraShadowSoftness = 0.15f`、`scleraShadowBend = 0.2f`、`scleraLidFollow = 0.5f`（どれも 0〜1、NaN は 0）

- [ ] **Step 1: 失敗するテストを書く**（`EyeSettingsTests` へ追加）

```csharp
        [Fact]
        public void 白目の影の要素の無いファイルは既定()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye /></MToonMaid>");

            Assert.False(s.eye.scleraShadowEnabled);
            Assert.False(s.eye.scleraColorOverride);
            Assert.Equal(1f, s.eye.scleraColor.r);
            Assert.Equal(1f, s.eye.scleraColor.b);
            Assert.Equal(1f, s.eye.scleraShadow);
            Assert.Equal(0xE0 / 255f, s.eye.scleraShadowColor.r, 5);
            Assert.Equal(0xD3 / 255f, s.eye.scleraShadowColor.g, 5);
            Assert.Equal(0xC9 / 255f, s.eye.scleraShadowColor.b, 5);
            Assert.Equal(0.5f, s.eye.scleraShadowHeight);
            Assert.Equal(0.15f, s.eye.scleraShadowSoftness);
            Assert.Equal(0.2f, s.eye.scleraShadowBend);
            Assert.Equal(0.5f, s.eye.scleraLidFollow);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 白目の影の範囲外の値とNaNは丸める()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye>"
                + "<scleraShadow>3</scleraShadow><scleraShadowHeight>-1</scleraShadowHeight>"
                + "<scleraShadowSoftness>NaN</scleraShadowSoftness><scleraShadowBend>2</scleraShadowBend>"
                + "<scleraLidFollow>-0.5</scleraLidFollow>"
                + "</eye></MToonMaid>");

            Assert.Equal(1f, s.eye.scleraShadow);
            Assert.Equal(0f, s.eye.scleraShadowHeight);
            Assert.Equal(0f, s.eye.scleraShadowSoftness);
            Assert.Equal(1f, s.eye.scleraShadowBend);
            Assert.Equal(0f, s.eye.scleraLidFollow);
        }

        [Fact]
        public void 白目の影を保存して読み直すと同じ値になる()
        {
            var path = Path.Combine(_dir, "sclera.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.eye.scleraShadowEnabled = true;
            s.eye.scleraColorOverride = true;
            s.eye.scleraColor = new ColorData(0.9f, 0.95f, 1f, 1f);
            s.eye.scleraShadow = 0.7f;
            s.eye.scleraShadowColor = new ColorData(0.5f, 0.6f, 0.7f, 1f);
            s.eye.scleraShadowHeight = 0.3f;
            s.eye.scleraShadowSoftness = 0.4f;
            s.eye.scleraShadowBend = 0.6f;
            s.eye.scleraLidFollow = 0.8f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.True(loaded.eye.scleraShadowEnabled);
            Assert.True(loaded.eye.scleraColorOverride);
            Assert.Equal(0.95f, loaded.eye.scleraColor.g);
            Assert.Equal(0.7f, loaded.eye.scleraShadow);
            Assert.Equal(0.6f, loaded.eye.scleraShadowColor.g);
            Assert.Equal(0.3f, loaded.eye.scleraShadowHeight);
            Assert.Equal(0.4f, loaded.eye.scleraShadowSoftness);
            Assert.Equal(0.6f, loaded.eye.scleraShadowBend);
            Assert.Equal(0.8f, loaded.eye.scleraLidFollow);
            Assert.Empty(_warnings);
        }
```

`EyePresetStoreTests` の `保存して読み直すと同じ値になる` にも、`eye.scleraShadowEnabled = true` と `eye.scleraShadowBend = 0.6f` を保存前に入れ、読み直した値を `Assert.True` / `Assert.Equal(0.6f, …)` で確かめる 2 行ずつを足す（プリセットに白目の影が入ることの確認）。

- [ ] **Step 2: 失敗を確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests --filter "EyeSettingsTests|EyePresetStoreTests"`
Expected: ビルドエラー（フィールドが無い）

- [ ] **Step 3: 実装する**

`highlightLightInfluence` の宣言の後へ追加:

```csharp
        /// <summary>白目の影を描く。OFF なら白目は口のテクスチャの描き込みのまま</summary>
        public bool scleraShadowEnabled;
        /// <summary>白目の色を scleraColor にする。OFF ならテクスチャの白目の白い部分の色</summary>
        public bool scleraColorOverride;
        public ColorData scleraColor;
        public float scleraShadow = 1f;
        public ColorData scleraShadowColor;
        public float scleraShadowHeight = 0.5f;
        public float scleraShadowSoftness = 0.15f;
        /// <summary>白目の左右の端ほど影の縁を下げる量 (白目の高さに対する割合)</summary>
        public float scleraShadowBend = 0.2f;
        /// <summary>まぶたを閉じた割合 (0〜1) にこの値を掛けた分 (白目の高さに対する割合) だけ影を下へずらす</summary>
        public float scleraLidFollow = 0.5f;
```

`FillMissing` の `lowerGlowColor` の補完の後へ追加:

```csharp
            if (scleraColor == null)
            {
                scleraColor = new ColorData(1f, 1f, 1f, 1f);
            }
            if (scleraShadowColor == null)
            {
                // 実機の口のアトラスの描き込み (白 (246,243,237)・影 (216,201,187)) の比。強さ 1 で元の濃さになる
                scleraShadowColor = new ColorData(0xE0 / 255f, 0xD3 / 255f, 0xC9 / 255f, 1f);
            }
```

`ClampValues` の `lowerGlowSoftness` の行の後へ追加:

```csharp
            scleraShadow = Clamp01(scleraShadow);
            scleraShadowHeight = Clamp01(scleraShadowHeight);
            scleraShadowSoftness = Clamp01(scleraShadowSoftness);
            scleraShadowBend = Clamp01(scleraShadowBend);
            scleraLidFollow = Clamp01(scleraLidFollow);
```

（コメント規約: 上の `scleraShadowColor` のコメントは値の根拠＝「なぜこの数値か」なので残す。他は追加しない）

- [ ] **Step 4: 通ることを確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミット**（例: `feat(sclera): 白目の影の設定を足す`）

---

### Task 4: MToon10 シェーダーで白目を塗り替える

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`（改変履歴のコメント、Properties）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_input.hlsl`（CBUFFER）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_lighting_mtoon.hlsl`（`MToonInput`、白目の関数、影側の色）
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon_forward_fragment.hlsl`（光側の色）
- Modify: `source/COM3D2.MToonMaid.Plugin/Rendering/MToonProps.cs`（プロパティ名の定数）
- 生成物: `source/COM3D2.MToonMaid.Plugin/Resources/mtoon_bundle` と `mtoon_bundle_com3d2`（`build-bundle.bat` が更新する）

**Interfaces:**
- Produces（シェーダーのプロパティ。Task 5 が `MToonProps` の定数で書く）:
  - `MToonProps.ScleraRect = "_ScleraRect"`（Vector、xy = 最小、zw = 大きさ。zw が 0 なら無効）
  - `MToonProps.ScleraColor = "_ScleraColor"`（Color、アルファ 0 ならテクスチャから取る）
  - `MToonProps.ScleraShadow = "_ScleraShadow"`、`ScleraShadowColor = "_ScleraShadowColor"`、`ScleraShadowHeight = "_ScleraShadowHeight"`、`ScleraShadowSoftness = "_ScleraShadowSoftness"`、`ScleraShadowBend = "_ScleraShadowBend"`、`ScleraLidFollow = "_ScleraLidFollow"`
  - まぶたの閉じた割合は既存の `MToonProps.EyeLidClose`（`"_LidClose"`）を MToon10 にも持たせて使う

- [ ] **Step 1: Properties と改変履歴**

`vrmc_materials_mtoon.shader` の改変履歴の最後の行（`// - ForwardBase / ForwardAdd のパスで …`）の後へ:

```
// - _ScleraRect の内側 (口のアトラスの白目) は、テクスチャの代わりに白目の色へ _ScleraShadow* の影を重ねた色で描く (光側・影側とも)
```

`_ShadeStrength ("Shade Strength", Range(0, 2)) = 1` の行の後へ:

```
        // COM3D2: 白目の範囲 (UV の外接矩形、zw が 0 なら無効) と、白目に重ねる影。白目の色のアルファが 0 ならテクスチャの白目の下側の色を使う (プラグインが設定する)
        _ScleraRect ("Sclera Rect", Vector) = (0, 0, 0, 0)
        _ScleraColor ("Sclera Color", Color) = (1, 1, 1, 0)
        _ScleraShadow ("Sclera Shadow", Range(0, 1)) = 0
        _ScleraShadowColor ("Sclera Shadow Color", Color) = (0.878, 0.827, 0.788, 1)
        _ScleraShadowHeight ("Sclera Shadow Height", Range(0, 1)) = 0.5
        _ScleraShadowSoftness ("Sclera Shadow Softness", Range(0, 1)) = 0.15
        _ScleraShadowBend ("Sclera Shadow Bend", Range(0, 1)) = 0.2
        _ScleraLidFollow ("Sclera Lid Follow", Range(0, 1)) = 0.5
        _LidClose ("Lid Close", Range(0, 1)) = 0
```

- [ ] **Step 2: CBUFFER**

`vrmc_materials_mtoon_input.hlsl` の CBUFFER で、`float4x4 _HairShadowMatrix;` の後へ `float4 _ScleraRect;`、`half4 _ToonRampDarkColor;` の後へ `half4 _ScleraColor;` と `half4 _ScleraShadowColor;`、`half _HairShadowSoftness;` の後へ次を足す:

```hlsl
half _ScleraShadow;
half _ScleraShadowHeight;
half _ScleraShadowSoftness;
half _ScleraShadowBend;
half _ScleraLidFollow;
half _LidClose;
```

- [ ] **Step 3: 白目の色の関数と `MToonInput`**

`vrmc_materials_mtoon_lighting_mtoon.hlsl` の `struct MToonInput` に、`half alpha;` の後へ:

```hlsl
    // COM3D2: 白目の範囲の内側か。内側なら影側も _ShadowTex の代わりに scleraColor を使う (_Color は掛けない)
    bool isSclera;
    half3 scleraColor;
```

`struct MToonInput { … };` の直後へ:

```hlsl
// COM3D2: 白目 (口のアトラスの _ScleraRect の内側) の色。内側なら true で、白目の色に影を重ねた色 (_Color は掛けない) を返す。
// 白目の色は分岐の中で読むため、勾配の要らない LOD 0 で読む
inline bool MToon_GetScleraColor(const float2 uv, out half3 color)
{
    color = 0;
    if (_ScleraRect.z <= 0 || _ScleraRect.w <= 0)
    {
        return false;
    }
    const float2 n = (uv - _ScleraRect.xy) / _ScleraRect.zw;
    if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
    {
        return false;
    }
    // 白目の中央・下から 1 割の所は、アトラスで白く塗られている (上側は描き込みの影)
    const half3 base = _ScleraColor.a > 0
        ? _ScleraColor.rgb
        : _MainTex.SampleLevel(sampler_MainTex, _ScleraRect.xy + _ScleraRect.zw * float2(0.5, 0.1), 0).rgb;
    const float x = 2 * n.x - 1;
    const half edge = 1 - _ScleraShadowHeight - _ScleraShadowBend * x * x - _LidClose * _ScleraLidFollow;
    const half mask = smoothstep(edge, edge + max(_ScleraShadowSoftness * _ScleraShadowHeight, 1e-4), n.y) * _ScleraShadow;
    color = lerp(base, base * _ScleraShadowColor.rgb, mask);
    return true;
}
```

`GetMToonLighting_DirectLighting` の `shadeBase` の行を次へ置き換える:

```hlsl
    const half3 shadowTex = input.isSclera ? input.scleraColor : MTOON_SAMPLE_TEXTURE2D(_ShadowTex, input.uv).rgb;
    const half3 shadeBase = max(0, lerp(input.litColor, shadowTex * _ShadeColor.rgb, shadeDepth));
```

- [ ] **Step 4: 光側の色**

`vrmc_materials_mtoon_forward_fragment.hlsl` の

```hlsl
    // Get LitColor with Alpha
    const half4 litColor = MTOON_SAMPLE_TEXTURE2D(_MainTex, uv) * _Color;
```

を次へ置き換える:

```hlsl
    // Get LitColor with Alpha
    half4 litColor = MTOON_SAMPLE_TEXTURE2D(_MainTex, uv) * _Color;
    half3 scleraColor;
    const bool isSclera = MToon_GetScleraColor(uv, scleraColor);
    if (isSclera)
    {
        litColor.rgb = scleraColor * _Color.rgb;
    }
```

`mtoonInput.alpha = alpha;` の後へ:

```hlsl
    mtoonInput.isSclera = isSclera;
    mtoonInput.scleraColor = scleraColor;
```

- [ ] **Step 5: `MToonProps` の定数**

`MToonProps.cs` の `EyeHeadUp` の行の後へ:

```csharp

        // 白目の影 (MToon10 の口のマテリアルへ入れる)。まぶたの閉じた割合は瞳と同じ EyeLidClose を使う
        public const string ScleraRect = "_ScleraRect";
        public const string ScleraColor = "_ScleraColor";
        public const string ScleraShadow = "_ScleraShadow";
        public const string ScleraShadowColor = "_ScleraShadowColor";
        public const string ScleraShadowHeight = "_ScleraShadowHeight";
        public const string ScleraShadowSoftness = "_ScleraShadowSoftness";
        public const string ScleraShadowBend = "_ScleraShadowBend";
        public const string ScleraLidFollow = "_ScleraLidFollow";
```

- [ ] **Step 6: バンドルを作る**

Unity エディタが閉じていることを確かめ、Run: `cmd //c "W:\COM3D2_5\work\COM3D25.MToonMaid.Plugin\build-bundle.bat"`（引数なし = com3d25 と com3d2 の両方）
Expected: 失敗のメッセージ（`シェーダーバンドルのビルドに失敗しました`）が出ず、`UnityProject2022/Logs/` の build-bundle のログに `Shader error` が無い。`git status` で `Resources/mtoon_bundle` と `Resources/mtoon_bundle_com3d2` が更新されている

- [ ] **Step 7: ビルドとテスト**

Run: 両フレームワークの `dotnet build`（Global Constraints のコマンド）と `dotnet test source/COM3D2.MToonMaid.Plugin.Tests`
Expected: 0 エラー、全件 PASS

- [ ] **Step 8: コミット**（シェーダー・`MToonProps.cs`・2 つのバンドルをまとめて。例: `feat(shader): MToon10 で白目の範囲を設定の影で塗り替える`）

---

### Task 5: `MaidScleraShadow` と、まぶたの閉じ具合の共通化

**Files:**
- Create: `source/COM3D2.MToonMaid.Plugin/Rendering/LidClosureReader.cs`
- Create: `source/COM3D2.MToonMaid.Plugin/MaidScleraShadow.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/MaidEyeGloss.cs`（`ComputeLidClosure` と `_lidMorph` / `_lidBlendValue` を消し、`UpdateGaze(float closure)` にする）
- Modify: `source/COM3D2.MToonMaid.Plugin/MaidMToonController.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/IMToonMaidHost.cs`、`source/COM3D2.MToonMaid.Plugin/MToonMaidPlugin.cs`

**Interfaces:**
- Consumes: `ScleraBounds.TryCompute`（Task 1）、`ToonMaterialClassifier.IsScleraMaterialName`（Task 2）、`EyeSettings.sclera*`（Task 3）、`MToonProps.Sclera*` / `EyeLidClose`（Task 4）
- Produces:
  - `public sealed class LidClosureReader { public float Read(TBody body); }` — 顔のブレンドが読めなければ 0
  - `public sealed class MaidScleraShadow { MaidScleraShadow(Maid maid, Shader mtoonShader, Action<string> warn); bool IsFound { get; } void Scan(EyeSettings s); void Apply(EyeSettings s); void UpdateLid(float closure); void Clear(); void Forget(); }`
  - `MaidEyeGloss.UpdateGaze(float closure)`（引数が増える）
  - `MaidMToonController.IsScleraFound`（bool プロパティ）
  - `IMToonMaidHost.IsScleraFound(Maid maid)`（bool）

- [ ] **Step 1: `LidClosureReader`**

```csharp
using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>顔のブレンドからまぶたを閉じた割合 (0〜1) を読む。メイド 1 人につき 1 個持つ</summary>
    public sealed class LidClosureReader
    {
        // 毎フレーム morph.GetBlendValues を渡すと、デリゲートの生成で GC が出る
        private TMorph _morph;
        private Func<int, float> _blendValue;

        /// <summary>顔のブレンドが読めなければ 0 (シェーダーではまぶたに合わせて動かさない)</summary>
        public float Read(TBody body)
        {
            var morph = body != null && body.Face != null ? body.Face.morph : null;
            if (morph == null || morph.BlendDataIdx_EyeClose == null)
            {
                return 0f;
            }
            if (morph != _morph)
            {
                _morph = morph;
                _blendValue = morph.GetBlendValues;
            }
            return EyelidClosure.Sum(morph.BlendDataIdx_EyeClose, _blendValue);
        }
    }
}
```

- [ ] **Step 2: `MaidEyeGloss` を `LidClosureReader` の値を受け取る形にする**

`MaidEyeGloss.cs` から `_lidMorph`・`_lidBlendValue` のフィールド（とその上のコメント）と `ComputeLidClosure` メソッドを消す。`UpdateGaze` を次へ置き換える（本体は今のまま、`closure` を引数で受ける）:

```csharp
        /// <summary>カメラの描画の前に呼ぶ (フレームに 1 回)。視線をワールドで瞳へ入れる。closure はまぶたを閉じた割合</summary>
        public void UpdateGaze(float closure)
        {
            var body = _maid.body0;
            if (_entries.Count == 0 || body == null)
            {
                return;
            }
            var head = body.trsHead;
            // head.up を上にすると、頭のボーンの up は顔の正面を向いているため、ハイライトが顔の前後へ動く
            var headRight = head != null ? head.forward : Vector3.zero;
            var headUp = head != null ? -head.right : Vector3.zero;
            foreach (var e in _entries.Values)
            {
                if (e.isEye && e.material != null && e.material.shader == _converter.EyeShader)
                {
                    EyeConverter.SetGaze(e.material, ComputeGaze(body, e.materialIndex));
                    EyeConverter.SetHeadAxes(e.material, headRight, headUp);
                    EyeConverter.SetLidClosure(e.material, closure);
                }
            }
        }
```

- [ ] **Step 3: `MaidScleraShadow`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の白目の影。MToon にした口のマテリアル (白目を含むアトラス) へ、白目の範囲と設定値を入れる。
    /// シェーダーの差し替え・復元は MaidMToonController が行い、ここでは _Sclera* と _LidClose だけを書く
    /// </summary>
    public sealed class MaidScleraShadow
    {
        private readonly Maid _maid;
        private readonly Shader _mtoonShader;
        private readonly Action<string> _warn;
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        private readonly List<Material> _materialBuffer = new List<Material>();
        private readonly List<int[]> _eyeTriangles = new List<int[]>();
        // メッシュとマテリアル番号の組ごとの範囲 (見つからなければ null)。直近 1 件だけにすると、Mouth の候補が複数あるとき走査のたびに頂点を読み直す
        private readonly Dictionary<long, IrisRect?> _rects = new Dictionary<long, IrisRect?>();
        // 値を入れているマテリアル。外れたら範囲を消す
        private Material _material;
        private IrisRect _rect;
        // 失敗は毎走査で繰り返しうるため 1 回だけ知らせる
        private bool _warned;

        /// <summary>直前の走査で、白目のマテリアルと範囲が見つかったか (UI の表示用)</summary>
        public bool IsFound { get; private set; }

        public MaidScleraShadow(Maid maid, Shader mtoonShader, Action<string> warn)
        {
            _maid = maid;
            _mtoonShader = mtoonShader;
            _warn = warn;
        }

        public void Scan(EyeSettings settings)
        {
            Material found = null;
            try
            {
                found = Find();
            }
            catch (Exception e)
            {
                if (!_warned)
                {
                    _warned = true;
                    _warn("白目の範囲を求められないため白目の影を描きません: " + e.Message);
                }
            }
            if (found != _material)
            {
                Clear();
                _material = found;
            }
            IsFound = found != null;
            Apply(settings);
        }

        private Material Find()
        {
            var skin = _maid.body0.GetSlot(ToonMaterialClassifier.HeadSlot);
            if (skin == null || skin.obj == null)
            {
                return null;
            }
            skin.obj.GetComponentsInChildren(true, _rendererBuffer);
            foreach (var renderer in _rendererBuffer)
            {
                var smr = renderer as SkinnedMeshRenderer;
                if (smr == null || smr.sharedMesh == null)
                {
                    continue;
                }
                GameCompat.GetSharedMaterials(renderer, _materialBuffer);
                for (var k = 0; k < _materialBuffer.Count; k++)
                {
                    var m = _materialBuffer[k];
                    if (m != null && m.shader == _mtoonShader && ToonMaterialClassifier.IsScleraMaterialName(m.name)
                        && TryGetRect(smr.sharedMesh, k))
                    {
                        return m;
                    }
                }
            }
            return null;
        }

        private bool TryGetRect(Mesh mesh, int index)
        {
            var key = ((long)mesh.GetInstanceID() << 32) | (uint)index;
            IrisRect? cached;
            if (!_rects.TryGetValue(key, out cached))
            {
                IrisRect computed;
                // 求める前に鍵を記録すると、頂点の読み出しが例外になったとき、前の顔の範囲を新しい顔に使い続ける
                cached = ComputeRect(mesh, index, out computed) ? computed : (IrisRect?)null;
                _rects[key] = cached;
            }
            if (cached.HasValue)
            {
                _rect = cached.Value;
            }
            return cached.HasValue;
        }

        private bool ComputeRect(Mesh mesh, int index, out IrisRect rect)
        {
            rect = default(IrisRect);
            var rightEye = ToonMaterialClassifier.LeftEyeMaterialIndex + 1;
            if (index >= mesh.subMeshCount || rightEye >= mesh.subMeshCount || index <= rightEye)
            {
                return false;
            }
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            if (uv.Length != vertices.Length)
            {
                return false;
            }
            var positions = new float[vertices.Length * 3];
            var uvs = new float[uv.Length * 2];
            for (var i = 0; i < vertices.Length; i++)
            {
                positions[i * 3] = vertices[i].x;
                positions[i * 3 + 1] = vertices[i].y;
                positions[i * 3 + 2] = vertices[i].z;
                uvs[i * 2] = uv[i].x;
                uvs[i * 2 + 1] = uv[i].y;
            }
            _eyeTriangles.Clear();
            _eyeTriangles.Add(mesh.GetTriangles(ToonMaterialClassifier.LeftEyeMaterialIndex));
            _eyeTriangles.Add(mesh.GetTriangles(rightEye));
            return ScleraBounds.TryCompute(positions, uvs, _eyeTriangles, mesh.GetTriangles(index), out rect);
        }

        /// <summary>設定値を入れる。白目の影が OFF なら範囲を消す</summary>
        public void Apply(EyeSettings s)
        {
            if (_material == null)
            {
                return;
            }
            if (!s.scleraShadowEnabled)
            {
                _material.SetVector(MToonProps.ScleraRect, Vector4.zero);
                return;
            }
            _material.SetVector(MToonProps.ScleraRect, new Vector4(_rect.x, _rect.y, _rect.width, _rect.height));
            _material.SetColor(MToonProps.ScleraColor, s.scleraColorOverride
                ? new Color(s.scleraColor.r, s.scleraColor.g, s.scleraColor.b, 1f)
                : new Color(1f, 1f, 1f, 0f));
            _material.SetFloat(MToonProps.ScleraShadow, s.scleraShadow);
            _material.SetColor(MToonProps.ScleraShadowColor, new Color(s.scleraShadowColor.r, s.scleraShadowColor.g, s.scleraShadowColor.b, 1f));
            _material.SetFloat(MToonProps.ScleraShadowHeight, s.scleraShadowHeight);
            _material.SetFloat(MToonProps.ScleraShadowSoftness, s.scleraShadowSoftness);
            _material.SetFloat(MToonProps.ScleraShadowBend, s.scleraShadowBend);
            _material.SetFloat(MToonProps.ScleraLidFollow, s.scleraLidFollow);
        }

        /// <summary>カメラの描画の前に呼ぶ (フレームに 1 回)。closure はまぶたを閉じた割合</summary>
        public void UpdateLid(float closure)
        {
            if (_material != null)
            {
                _material.SetFloat(MToonProps.EyeLidClose, closure);
            }
        }

        /// <summary>入れた範囲を消し、描き込みの見た目へ戻す</summary>
        public void Clear()
        {
            // Unity の == は破棄済みオブジェクトを null と判定する
            if (_material != null)
            {
                _material.SetVector(MToonProps.ScleraRect, Vector4.zero);
            }
            _material = null;
            IsFound = false;
        }

        /// <summary>メイドごと破棄された。マテリアルも破棄済みなので触らずに捨てる</summary>
        public void Forget()
        {
            _material = null;
            _rects.Clear();
            IsFound = false;
        }
    }
}
```

- [ ] **Step 4: `MaidMToonController` へつなぐ**

フィールドへ:

```csharp
        private readonly MaidScleraShadow _sclera;
        private readonly LidClosureReader _lidClosure = new LidClosureReader();
```

コンストラクタの `_eyeGloss = …` の後へ `_sclera = new MaidScleraShadow(maid, converter.MToonShader, warn);`

`ConvertedCount` の後へ:

```csharp
        public bool IsScleraFound => _sclera.IsFound;
```

`ApplyEye` の `_eyeGloss?.Apply(settings);` の後へ `_sclera.Apply(settings);`

`UpdateEyeGaze` の本体を次へ置き換える:

```csharp
            var closure = _lidClosure.Read(Maid.body0);
            _eyeGloss?.UpdateGaze(closure);
            _sclera.UpdateLid(closure);
```

`RestoreAll` の `_eyeGloss?.RestoreAll();` の後へ `_sclera.Clear();`、`Scan` の最後の `_eyeGloss?.Scan(settings.eye);` の後へ `_sclera.Scan(settings.eye);`、`DestroyOwnedTextures` の `_eyeGloss?.Forget();` の後へ `_sclera.Forget();`

（`RestoreAll` の `_sclera.Clear()` は、MToon の entries を戻す `foreach` より前に入る。`SetVector` はシェーダーによらず書けるので順は問わない。範囲を消すのは、次に ON にしたとき古い範囲で描かないため）

`MaidScleraShadow` の頂点の読み出しは例外になりうる（`Scan` が 1 回だけ警告して範囲なしにする）。例外の後は `_rects` に何も入らないので、次の走査でもう一度求める。`ComputeRect` が false の組は null として覚え、読み直さない

- [ ] **Step 5: ホストのメソッド**

`IMToonMaidHost.cs` の `GetConvertedCount` の後へ:

```csharp
        /// <summary>直前の走査で白目のマテリアルと範囲が見つかったか。コントローラーが無ければ false</summary>
        bool IsScleraFound(Maid maid);
```

`MToonMaidPlugin.cs` の `GetConvertedCount` の後へ:

```csharp
        public bool IsScleraFound(Maid maid)
        {
            MaidMToonController controller;
            return _controllers.TryGetValue(maid, out controller) && controller.IsScleraFound;
        }
```

- [ ] **Step 6: ビルドとテスト**

Run: 両フレームワークの `dotnet build` と `dotnet test source/COM3D2.MToonMaid.Plugin.Tests`
Expected: 0 エラー、全件 PASS。net35 で `GetComponentsInChildren(bool, List<T>)` などが無いと言われたら、`GameCompat` に同等の分岐があるか確かめてそれを使う

- [ ] **Step 7: コミット**（例: `feat(sclera): 口のマテリアルへ白目の範囲と影の設定を入れる`）

---

### Task 6: 目タブの「白目の影」セクション

**Files:**
- Modify: `source/COM3D2.MToonMaid.Plugin/UI/MToonMaidWindow.cs`

**Interfaces:**
- Consumes: `IMToonMaidHost.IsScleraFound(Maid)`（Task 5）、`EyeSettings.sclera*`（Task 3）

- [ ] **Step 0: 瞳のシェーダーが無いときの文言を瞳に限定する**

`DrawEyeSettings` の `"瞳のシェーダーを読めないため使えません (BepInEx のログを確認してください)"` を `"瞳のシェーダーを読めないため、瞳の項目は使えません (BepInEx のログを確認してください)"` にする（同じ画面の白目の影は使えるため）

- [ ] **Step 1: 呼び出しへメイドを渡す**

`DrawEyeSettings(view);` の呼び出しを `DrawEyeSettings(view, maid);` にし、`private void DrawEyeSettings(GUIView view)` を `private void DrawEyeSettings(GUIView view, Maid maid)` にする。その中の `DrawEyeShade(view, eye, def, editable);` の後（`if (_eyeSubTab == EyeShadeSubTab)` の分岐の中）へ `DrawScleraShadow(view, maid, eye, def);` を足す

- [ ] **Step 2: セクションを描く**（`DrawEyeShade` の後へ追加）

```csharp
        /// <summary>白目の影は瞳のシェーダーとは別の仕組み (MToon10) なので、瞳の光沢の ON/OFF やシェーダーの有無に関係なく編集できる</summary>
        private void DrawScleraShadow(GUIView view, Maid maid, EyeSettings eye, EyeSettings def)
        {
            DrawSection(view, "白目の影");
            if (eye.scleraShadowEnabled && !_host.IsScleraFound(maid))
            {
                // DrawLabel は入力欄キャッシュを使わないため、条件つきで描いても番号はずれない
                view.DrawLabel("白目が見つかりません (顔を MToon にしていないか、対応していない顔です)", -1, RowHeight);
            }
            EyeToggle(view, "白目の影を描く", "口のテクスチャに描かれた白目の影の代わりに、下の設定の影を描く\nOFF ではテクスチャの描き込みのまま",
                eye.scleraShadowEnabled, v => eye.scleraShadowEnabled = v, true);
            var editable = eye.scleraShadowEnabled;
            EyeSlider(view, "強さ", "白目の上側を影の色で暗くする濃さ",
                eye.scleraShadow, 0f, 1f, def.scleraShadow, v => eye.scleraShadow = v, editable);
            EyeColorRow(view, "瞳/白目の影の色", "色", "白目の上側に掛ける影の色 (白目の色に乗算する)",
                eye.scleraShadowColor, def.scleraShadowColor, editable);
            EyeSlider(view, "高さ", "影が白目の上端から届く範囲 (白目の高さに対する割合)",
                eye.scleraShadowHeight, 0f, 1f, def.scleraShadowHeight, v => eye.scleraShadowHeight = v, editable);
            EyeSlider(view, "ぼかし", "影の下端をぼかす幅 (影の高さに対する割合)",
                eye.scleraShadowSoftness, 0f, 1f, def.scleraShadowSoftness, v => eye.scleraShadowSoftness = v, editable);
            EyeSlider(view, "反り", "白目の左右の端ほど影の下端を下げる量 (白目の高さに対する割合)",
                eye.scleraShadowBend, 0f, 1f, def.scleraShadowBend, v => eye.scleraShadowBend = v, editable);
            EyeSlider(view, "まぶたへの追従", "まぶたを閉じるほど影を下へずらす割合",
                eye.scleraLidFollow, 0f, 1f, def.scleraLidFollow, v => eye.scleraLidFollow = v, editable);
            EyeToggle(view, "白目の色を指定する", "白目の色を下の色にする\nOFF ではテクスチャの白目の白い部分の色",
                eye.scleraColorOverride, v => eye.scleraColorOverride = v, editable);
            EyeColorRow(view, "瞳/白目の色", "白目の色", "白目の地の色",
                eye.scleraColor, def.scleraColor, editable && eye.scleraColorOverride);
        }
```

- [ ] **Step 3: 既定に戻すボタンで ON/OFF を残す**

`DrawEyeResetButton` を次の形にする（白目の影の ON/OFF も調整値ではないため残す）:

```csharp
                // ON/OFF は調整値ではないため残す
                var enabled = _settings.eye.enabled;
                var scleraEnabled = _settings.eye.scleraShadowEnabled;
                _settings.eye = EyeSettings.CreateDefault();
                _settings.eye.enabled = enabled;
                _settings.eye.scleraShadowEnabled = scleraEnabled;
```

README の「瞳の調整値を既定に戻す」の説明（ON/OFF 以外を戻す）と一致することを Task 7 で確かめる。プリセットの適用（`ApplyEyePreset`）は `scleraShadowEnabled` もプリセットの値を使う（仕様どおり。コードの変更なし）

- [ ] **Step 4: ビルドとテスト**

Run: 両フレームワークの `dotnet build` と `dotnet test source/COM3D2.MToonMaid.Plugin.Tests`
Expected: 0 エラー、全件 PASS

- [ ] **Step 5: コミット**（例: `feat(ui): 目タブに白目の影のセクションを足す`）

---

### Task 7: 実機の確認と README

**Files:**
- Modify: `README.md`（瞳タブの節）
- 必要なら Modify: `source/COM3D2.MToonMaid.Plugin/Settings/EyeSettings.cs`・`vrmc_materials_mtoon.shader`・`EyeSettingsTests.cs`（まぶたへの追従の既定値の見直し）

- [ ] **Step 1: 反映して起動・ロード**

restart-verify スキルの手順に従う（ゲームを終了する前にシーンの保存の有無をユーザーに確認 → `debug.bat com3d25` → 起動 → セーブをロード → `reset_evaluator`）

- [ ] **Step 2: 見た目を確かめる**（目のアップは、臨時のカメラを作って RenderTexture へ描き、PNG に保存して見る。終わったらカメラを破棄する。2026-10-11 の調査と同じ方法）

1. 白目の影 OFF: 今と同じ見た目（灰色と白のアーチの描き込み）
2. ON（既定値）: 描き込みが消え、設定の影が出る。中央が高さ約 0.5、左右の端が少し下がり、元の描き込みに近い
3. 強さ・色・高さ・ぼかし・反りのスライダーがそのとおりに効く。白目の色の指定で地の色が変わる
4. まばたき（`eyeclose` を 0.5 / 1 にする）で影が下へずれ、まぶたの下に影が残る。ずれ方が大きすぎる・小さすぎるなら `scleraLidFollow` の既定を直す（直したら `EyeSettings` の既定・シェーダーの Properties の既定・`白目の影の要素の無いファイルは既定` のテスト・仕様書・計画の Global Constraints をそろえる）
5. 口を開けた表情（`moutha` 100）で、口内・歯・舌の色が OFF のときと同じ
6. ON のままメイドの「MToon にする」を OFF → ON、顔スロットの除外 → 解除で、描き込みの見た目に戻ってから影が戻る
7. 主光源の向き・明るさを変えて、白目の明るさが顔の肌と同じようにライトへ追従する（影の形は動かない）
8. 顔を別の顔（バニラの face003 など）に替えて、白目が見つかり影が出る

- [ ] **Step 3: README**

「瞳タブ」の節の「影・明るさ」の表の後へ、次の節を足す:

```markdown
#### 白目の影

[影・明るさ] の下にあります。瞳の光沢とは別の仕組み（顔の MToon）なので、「瞳を光沢のシェーダーにする」の ON/OFF に関係なく使えます。顔を MToon にしているときに効きます。

| 項目 | 範囲 | 既定 | 効果 |
|---|---|---|---|
| 白目の影を描く | ON/OFF | OFF | 口のテクスチャに描かれた白目の影の代わりに、下の設定の影を描く |
| 強さ | 0〜1 | 1 | 白目の上側に重ねる影の濃さ |
| 色 | 色 | `#E0D3C9` | 影の色。白目の色に乗算で重なる |
| 高さ | 0〜1 | 0.5 | 影がかかる範囲。白目の上端から測った、白目の高さに対する割合 |
| ぼかし | 0〜1 | 0.1 | 影の下の縁をぼかす幅（影の高さに対する割合） |
| 反り | 0〜1 | 0.2 | 白目の左右の端ほど影の縁を下げる量 |
| まぶたへの追従 | 0〜1 | 0.5 | まぶたを閉じるほど影を下へずらす割合 |
| 白目の色を指定する | ON/OFF | OFF | OFF ではテクスチャの白目の白い部分の色を使う |
| 白目の色 | 色 | 白 | 白目の地の色 |

- 既定値は、バニラの白目の描き込みに近くなるようにしています
- 白目は口のテクスチャ（名前に `Mouth` を含むマテリアル）の一部です。名前の違う MOD の顔では「白目が見つかりません」と出て、効きません
- 瞳のプリセットには白目の影の ON/OFF も入ります。白目の影を足す前に保存したプリセットを適用すると、白目の影は OFF になります
```

`Step 2-4` で既定値を直したら、表の値もそろえる。「瞳の調整値を既定に戻す」の説明に「ON/OFF（瞳の光沢・白目の影）以外」と書き足す

- [ ] **Step 4: テストとコミット**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests`
Expected: 全件 PASS
コミット（例: `docs: README に白目の影を書く`。既定値を直した場合は別コミット `fix(sclera): まぶたへの追従の既定を実機に合わせる`）

## レビュー却下メモ

- 白目の色の r/g/b を `ClampValues` で丸める — 既存の色（上まぶたの影の色・ハイライトの色など）も丸めておらず、白目だけ変えると作法がずれるため見送り
- `_LidClose` を全メイドで毎フレーム読む負荷 — レビュー側も問題なしと結論。変更なし
