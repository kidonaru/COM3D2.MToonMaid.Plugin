# COM3D25.MToonMaid.Plugin 凹凸の影を消す Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 顔のメッシュから `_ShadingShiftTex`（凹凸のマスク）を自動で作り、「凹凸の影を消す」（0〜1、既定 0）で鼻・口元の細かい影だけを消せるようにする。

**Architecture:** 計算は Unity に依存しない `BumpMaskBaker`（頂点ごとの凹凸の強さ・UV 空間への描き込み・広げ・ぼかし）に置いて単体テストする。`FaceBumpMask` がメッシュと TMorph から入力を集めて `Texture2D` を作る。`MaidMToonController` は強さが 0 より大きくなった顔のマテリアルにだけマスクを作って `MaterialBackup.bumpMask` に持ち、`MaterialConverter.ApplyGroup` が `_ShadingShiftTex` / `_ShadingShiftTexScale` に入れる。戻すときと破棄されたときにマスクを破棄する。

**Tech Stack:** C# 7.3 / net471 / BepInEx 5 / xunit（net48）/ Unity 2022.3.62 / MTEUtils（submodule、編集しない）

**Spec:** `docs/superpowers/specs/2026-10-09-mtoon-maid-bump-mask-design.md`（前提として v2.4 `2026-10-09-mtoon-maid-face-flatten-design.md`）

## Global Constraints

- v2.4 計画（`docs/superpowers/plans/2026-10-09-mtoon-maid-face-flatten.md`）の Global Constraints をすべて引き継ぐ。特に: COM3D2.5 のみ、コメント・ログは日本語、`deploy.bat` は実行しない、worktree 不使用、main で作業、MTEUtils は編集しない、`debug.bat` は実機検証のタスクでだけ使う
- シェーダーは変えない（`_ShadingShiftTex` / `_ShadingShiftTexScale` / `_MTOON_PARAMETERMAP` は MToon10 にあり、キーワードは `multi_compile`）。バンドルは作り直さない
- 設定ファイルの版は 3 のまま。要素の無いファイルは既定 0 で読む
- 継承の単位は 17 項目（v2.4 の 16 項目 + `faceBumpLift`）
- 対象は顔のグループだけ（`ToonMaterialClassifier.UsesSphereNormal`）
- マスクの定数（spec より）: 半径 0.025、強さ 1 になる差 0.5、256×256、広げ 4 回、ぼかし半径 2 × 2 回
- GUIView の入力欄キャッシュは描画順の番号で引かれる。スライダーは全タブで同じ位置に描き、効かないタブでは無効表示にする（行を消さない）
- ゲームの再起動の前に、ゲームが SceneEditor のシーンを開いていたらユーザーに保存の確認をとる
- 実機検証では、ユーザーが今の設定に入れている値（共通の「顔の陰影を球に」0.47・「顔を平らに」0.25 など）を控えてから変え、最後に戻す

## Review Focus

- **強さ 0 の不変**: 「凹凸の影を消す」が 0 なら `_ShadingShiftTex` は null でキーワードも切れ、マスクも作らない → Task 3 Step 1、Task 4 Step 2
- **マスクの破棄漏れ**: 戻す（OFF・グループ OFF・スロット除外）とき、マテリアルが破棄されたときに `Texture2D` を破棄する（毎回の着替えで溜まらない） → Task 3 Step 3、Task 4 Step 4
- **作れない顔**: 読めないメッシュ・UV や法線の数が合わない・サブメッシュが無いときに例外にせず、警告 1 回で以後は作らない → Task 3 Step 2（`bumpMaskFailed`）
- **UV の継ぎ目**: 継ぎ目の外側を広げないと、バイリニアで 0 を拾って継ぎ目に線が出る → Task 2 のテスト（広げ）
- **大きな頂点数**: 格子で近傍を探し、総当たりにしない → Task 2 Step 3 の実装、Task 4 Step 2 の作成時間

---

## ファイル構成

```
source/COM3D25.MToonMaid.Plugin/
├─ Settings/GroupSettingKeys.cs    (Modify: FaceBumpLift)
├─ Settings/GroupSettings.cs       (Modify: faceBumpLift、CopyField / FieldEquals)
├─ Rendering/BumpMaskBaker.cs      (Create: 凹凸の強さ・描き込み)
├─ Rendering/FaceBumpMask.cs       (Create: メッシュからテクスチャを作る)
├─ Rendering/MToonProps.cs         (Modify: ShadingShiftTexScale)
├─ Rendering/MaterialBackup.cs     (Modify: bumpMask)
├─ Rendering/MaterialConverter.cs  (Modify: ApplyGroup / RestoreUnchecked)
├─ MaidMToonController.cs          (Modify: マスクを作る・破棄する)
└─ UI/MToonMaidWindow.cs           (Modify: スライダー 1 行)
source/COM3D25.MToonMaid.Plugin.Tests/
├─ CommonSettingsTests.cs          (Modify)
└─ BumpMaskBakerTests.cs           (Create)
README.md / spec                   (Modify)
```

---

### Task 1: 設定に「凹凸の影を消す」を足す

**Files:**
- Modify: `Settings/GroupSettingKeys.cs`, `Settings/GroupSettings.cs`, `UI/MToonMaidWindow.cs`
- Test: `CommonSettingsTests.cs`
- Commit に spec と plan を含める

**Interfaces:**
- Produces:
  - `GroupSettingKeys.FaceBumpLift = "faceBumpLift"`（`All` の `FaceFlatten` の直後）
  - `GroupSettings.faceBumpLift : float = 0f`

- [ ] **Step 1: 失敗するテストを書く**

`CommonSettingsTests.cs` の `ChangeAll` の `g.faceFlatten = 0.2f;` の直後に追加:

```csharp
            g.faceBumpLift = 0.7f;
```

`個別を外してから再び個別にすると共通の値から始まる` の前に追加:

```csharp
        [Fact]
        public void 凹凸の影を消すは既定0で要素の無いファイルも個別にしない()
        {
            Assert.Contains(GroupSettingKeys.FaceBumpLift, GroupSettingKeys.All);
            Assert.Equal(0f, GroupSettings.CreateDefault().faceBumpLift);

            var v2 = Path.Combine(_dir, "v2h.xml");
            File.WriteAllText(v2,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><face><shadingShift>0.2</shadingShift></face></MToonMaid>");
            var s2 = MaidSettingsStore.Load(v2, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadingShift }, s2.face.overrides);
            Assert.Equal(0f, s2.Resolve(PartGroup.Face).faceBumpLift);

            var v3 = Path.Combine(_dir, "v3h.xml");
            File.WriteAllText(v3,
                "<?xml version=\"1.0\"?><MToonMaid version=\"3\"><common><sphereNormal>1</sphereNormal></common></MToonMaid>");
            var s3 = MaidSettingsStore.Load(v3, _warnings.Add);

            Assert.Equal(0f, s3.common.faceBumpLift);
        }
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`faceBumpLift` / `GroupSettingKeys.FaceBumpLift` が無い）

- [ ] **Step 3: キー・フィールドを足す**

`Settings/GroupSettingKeys.cs` の `public const string FaceFlatten = "faceFlatten";` の直後に追加:

```csharp
        public const string FaceBumpLift = "faceBumpLift";
```

`All` の 1 行目を次へ置き換える:

```csharp
            ShadeColor, ShadingShift, ShadingToony, GiEqualization, AdditionalLightShade, SphereNormal, FaceFlatten, FaceBumpLift, HighlightScale,
```

`Settings/GroupSettings.cs` の `public float faceFlatten = 0.5f;` の直後に追加:

```csharp
        // 顔の凹凸 (鼻・口元) の影を、メッシュから作ったマスクで境界をずらして消す強さ (0〜1)。顔の立体的な陰影は残る
        public float faceBumpLift;
```

`CopyField` の `case GroupSettingKeys.FaceFlatten: ...` の直後に追加:

```csharp
                case GroupSettingKeys.FaceBumpLift: faceBumpLift = from.faceBumpLift; break;
```

`FieldEquals` の `case GroupSettingKeys.FaceFlatten: ...` の直後に追加:

```csharp
                case GroupSettingKeys.FaceBumpLift: return FloatEquals(faceBumpLift, other.faceBumpLift);
```

`UI/MToonMaidWindow.cs` の `Slider(view, GroupSettingKeys.FaceFlatten, ...)` 行の直後に追加:

```csharp
            Slider(view, GroupSettingKeys.FaceBumpLift, "凹凸の影を消す", shown.faceBumpLift, 0f, 1f, def.faceBumpLift, (s, v) => s.faceBumpLift = v, editable: sphereEditable);
```

- [ ] **Step 4: テストとビルドを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests` と `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug`
Expected: テスト PASS（85 + 1 = 86 件）、ビルド成功（警告 0）

- [ ] **Step 5: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin source/COM3D25.MToonMaid.Plugin.Tests docs/superpowers/specs/2026-10-09-mtoon-maid-bump-mask-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-bump-mask.md
git commit -m "feat: 凹凸の影を消す設定を追加"
```

---

### Task 2: 凹凸のマスクの計算（BumpMaskBaker）

**Files:**
- Create: `Rendering/BumpMaskBaker.cs`
- Test: `BumpMaskBakerTests.cs`（Create）

**Interfaces:**
- Produces:
  - `BumpMaskBaker.ComputeBumpiness(Vector3[] vertices, Vector3[] normals, int[] tris, float radius, float fullScale) : float[]`（頂点数の長さ。tris に無い頂点は 0）
  - `BumpMaskBaker.Rasterize(Vector2[] uv, int[] tris, float[] values, int size, int dilate, int blurRadius, int blurPasses) : float[]`（`size * size`。行 0 が v = 0。Texture2D.SetPixels32 の並びと同じ）

- [ ] **Step 1: 失敗するテストを書く**

`source/COM3D25.MToonMaid.Plugin.Tests/BumpMaskBakerTests.cs` を作る:

```csharp
using System.Collections.Generic;
using COM3D25.MToonMaid.Plugin;
using UnityEngine;
using Xunit;

namespace COM3D25.MToonMaid.Plugin.Tests
{
    public class BumpMaskBakerTests
    {
        private const float Radius = 0.025f;
        private const float FullScale = 0.5f;

        // XZ 平面の n×n の格子 (間隔 0.01)。法線は上向き
        private static void Grid(int n, out Vector3[] vertices, out Vector3[] normals, out int[] tris)
        {
            vertices = new Vector3[n * n];
            normals = new Vector3[n * n];
            for (var z = 0; z < n; z++)
            {
                for (var x = 0; x < n; x++)
                {
                    vertices[z * n + x] = new Vector3(x * 0.01f, 0f, z * 0.01f);
                    normals[z * n + x] = Vector3.up;
                }
            }
            var list = new List<int>();
            for (var z = 0; z < n - 1; z++)
            {
                for (var x = 0; x < n - 1; x++)
                {
                    var i = z * n + x;
                    list.AddRange(new[] { i, i + n, i + 1, i + 1, i + n, i + n + 1 });
                }
            }
            tris = list.ToArray();
        }

        [Fact]
        public void 平らな面は凹凸0()
        {
            Grid(5, out var v, out var n, out var t);

            var b = BumpMaskBaker.ComputeBumpiness(v, n, t, Radius, FullScale);

            foreach (var x in b)
            {
                Assert.InRange(x, 0f, 1e-5f);
            }
        }

        [Fact]
        public void 周りと向きが違う頂点は凹凸があり半径の外は影響しない()
        {
            Grid(5, out var v, out var n, out var t);
            // 中央 (0.02, 0, 0.02) を 60° 傾ける。角 (0,0,0) は距離 0.028 で半径の外
            n[12] = new Vector3(Mathf.Sin(Mathf.PI / 3f), Mathf.Cos(Mathf.PI / 3f), 0f);

            var b = BumpMaskBaker.ComputeBumpiness(v, n, t, Radius, FullScale);

            Assert.True(b[12] > 0.3f, "中央の凹凸: " + b[12]);
            Assert.InRange(b[0], 0f, 1e-5f);
        }

        [Fact]
        public void 強さは1に抑える()
        {
            Grid(5, out var v, out var n, out var t);
            n[12] = Vector3.down;

            var b = BumpMaskBaker.ComputeBumpiness(v, n, t, Radius, FullScale);

            Assert.Equal(1f, b[12]);
        }

        [Fact]
        public void 三角形に含まれない頂点は0()
        {
            Grid(5, out var v, out var n, out var t);
            var v2 = new List<Vector3>(v) { new Vector3(0.02f, 0f, 0.02f) }.ToArray();
            var n2 = new List<Vector3>(n) { Vector3.down }.ToArray();

            var b = BumpMaskBaker.ComputeBumpiness(v2, n2, t, Radius, FullScale);

            Assert.Equal(0f, b[25]);
            // 含まれない頂点は周りの平均にも入れない
            Assert.InRange(b[12], 0f, 1e-5f);
        }

        [Fact]
        public void 原点をまたぐ座標でも近傍を探せる()
        {
            Grid(5, out var v, out var n, out var t);
            // 格子を (-0.02, -0.01, -0.02) ずらし、中央を原点に置く (負のセルの番号を通す)
            for (var i = 0; i < v.Length; i++)
            {
                v[i] += new Vector3(-0.02f, -0.01f, -0.02f);
            }
            n[12] = new Vector3(Mathf.Sin(Mathf.PI / 3f), Mathf.Cos(Mathf.PI / 3f), 0f);

            var b = BumpMaskBaker.ComputeBumpiness(v, n, t, Radius, FullScale);

            Assert.True(b[12] > 0.3f, "中央の凹凸: " + b[12]);
            // 中央の隣 (距離 0.01) も中央の傾きを拾う
            Assert.True(b[13] > 0f, "隣の凹凸: " + b[13]);
            Assert.InRange(b[0], 0f, 1e-5f);
        }

        [Fact]
        public void 三角形が無ければすべて0()
        {
            Grid(3, out var v, out var n, out var t);
            var empty = new int[0];

            var b = BumpMaskBaker.ComputeBumpiness(v, n, empty, Radius, FullScale);
            var px = BumpMaskBaker.Rasterize(new Vector2[v.Length], empty, b, 4, 2, 1, 1);

            Assert.All(b, x => Assert.Equal(0f, x));
            Assert.All(px, x => Assert.Equal(0f, x));
        }

        // UV の正方形全体を 2 つの三角形で覆う
        private static readonly Vector2[] QuadUv = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        private static readonly int[] QuadTris = { 0, 2, 1, 1, 2, 3 };

        [Fact]
        public void 全体を覆う一定の値はぼかしても同じ値()
        {
            var values = new[] { 0.6f, 0.6f, 0.6f, 0.6f };

            var px = BumpMaskBaker.Rasterize(QuadUv, QuadTris, values, 8, 2, 1, 1);

            Assert.Equal(64, px.Length);
            foreach (var p in px)
            {
                Assert.InRange(p, 0.6f - 1e-4f, 0.6f + 1e-4f);
            }
        }

        [Fact]
        public void 頂点の値を補間する()
        {
            // 左 (u = 0) が 0、右 (u = 1) が 1
            var values = new[] { 0f, 1f, 0f, 1f };

            var px = BumpMaskBaker.Rasterize(QuadUv, QuadTris, values, 8, 0, 0, 0);

            // 行 0 の左端の画素の中心は u = 0.5 / 8、右端は u = 7.5 / 8
            Assert.InRange(px[0], 0.0625f - 1e-4f, 0.0625f + 1e-4f);
            Assert.InRange(px[7], 0.9375f - 1e-4f, 0.9375f + 1e-4f);
        }

        [Fact]
        public void 覆っていない画素は継ぎ目の近くだけ広げる()
        {
            // 左下の三角形 (画素で x + y < 8 の範囲) を 1 で塗る
            var uv = new[] { new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0, 0.5f) };
            var tris = new[] { 0, 2, 1 };
            var values = new[] { 1f, 1f, 1f };

            var px = BumpMaskBaker.Rasterize(uv, tris, values, 16, 2, 0, 0);

            // 画素 (4,4) の中心は x + y = 9 で外側だが、2 回の広げの範囲内
            Assert.InRange(px[4 * 16 + 4], 1f - 1e-4f, 1f + 1e-4f);
            // 右上の角は広げの範囲外
            Assert.Equal(0f, px[15 * 16 + 15]);
        }
    }
}
```

- [ ] **Step 2: 失敗を確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: コンパイルエラー（`BumpMaskBaker` が無い）

- [ ] **Step 3: 実装する**

`source/COM3D25.MToonMaid.Plugin/Rendering/BumpMaskBaker.cs` を作る:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>
    /// 顔の凹凸の影を消すための _ShadingShiftTex を計算する。
    /// 頂点ごとに「法線と、周りの法線の平均との差」を凹凸の強さとし、UV 空間へ描き込む
    /// </summary>
    public static class BumpMaskBaker
    {
        /// <summary>
        /// 頂点ごとの凹凸の強さ (0〜1)。1 - dot(法線, 半径内の法線の重み付き平均) を fullScale で割って抑える。
        /// tris に含まれない頂点は 0 で、周りの平均にも入れない
        /// </summary>
        public static float[] ComputeBumpiness(Vector3[] vertices, Vector3[] normals, int[] tris, float radius, float fullScale)
        {
            var result = new float[vertices.Length];
            var used = new HashSet<int>(tris);
            // 近傍は半径の格子で探す (MOD の顔は頂点が数千あり、総当たりでは重い)
            var grid = new Dictionary<long, List<int>>();
            foreach (var i in used)
            {
                var key = CellKey(Cell(vertices[i].x, radius), Cell(vertices[i].y, radius), Cell(vertices[i].z, radius));
                List<int> list;
                if (!grid.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    grid[key] = list;
                }
                list.Add(i);
            }

            var r2 = radius * radius;
            foreach (var i in used)
            {
                var p = vertices[i];
                int cx = Cell(p.x, radius), cy = Cell(p.y, radius), cz = Cell(p.z, radius);
                var sum = Vector3.zero;
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            List<int> list;
                            if (!grid.TryGetValue(CellKey(cx + dx, cy + dy, cz + dz), out list))
                            {
                                continue;
                            }
                            foreach (var j in list)
                            {
                                var d2 = (vertices[j] - p).sqrMagnitude;
                                if (d2 < r2)
                                {
                                    sum += normals[j] * (1f - d2 / r2);
                                }
                            }
                        }
                    }
                }
                var diff = 1f - Vector3.Dot(normals[i].normalized, sum.normalized);
                result[i] = Mathf.Clamp01(diff / fullScale);
            }
            return result;
        }

        private static int Cell(float v, float size)
        {
            return (int)Math.Floor(v / size);
        }

        private static long CellKey(int x, int y, int z)
        {
            // 各軸 21 ビット (±100 万セル) に詰める。顔のメッシュでは十分
            return ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);
        }

        /// <summary>
        /// 頂点の値を UV 空間の size×size へ三角形ごとに描き込む (行 0 が v = 0)。
        /// UV の継ぎ目でバイリニアが外側の 0 を拾わないよう、覆っていない画素を dilate 回、周りの覆った画素の平均で広げる。
        /// 三角形の境目の段差が出ないよう、半径 blurRadius の平均で blurPasses 回ぼかす
        /// </summary>
        public static float[] Rasterize(Vector2[] uv, int[] tris, float[] values, int size, int dilate, int blurRadius, int blurPasses)
        {
            var buf = new float[size * size];
            var covered = new bool[size * size];
            for (var t = 0; t + 2 < tris.Length; t += 3)
            {
                FillTriangle(uv, values, tris[t], tris[t + 1], tris[t + 2], size, buf, covered);
            }
            for (var i = 0; i < dilate; i++)
            {
                Dilate(ref buf, ref covered, size);
            }
            for (var i = 0; i < blurPasses; i++)
            {
                buf = Blur(buf, size, blurRadius);
            }
            return buf;
        }

        private static void FillTriangle(Vector2[] uv, float[] values, int i0, int i1, int i2, int size, float[] buf, bool[] covered)
        {
            var a = uv[i0] * size;
            var b = uv[i1] * size;
            var c = uv[i2] * size;
            var den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Math.Abs(den) < 1e-12f)
            {
                return;
            }
            var x0 = Math.Max(0, (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))));
            var x1 = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))));
            var y0 = Math.Max(0, (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))));
            var y1 = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))));
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    // 画素の中心で重心座標を求める
                    float px = x + 0.5f, py = y + 0.5f;
                    var w0 = ((b.y - c.y) * (px - c.x) + (c.x - b.x) * (py - c.y)) / den;
                    var w1 = ((c.y - a.y) * (px - c.x) + (a.x - c.x) * (py - c.y)) / den;
                    var w2 = 1f - w0 - w1;
                    if (w0 < 0f || w1 < 0f || w2 < 0f)
                    {
                        continue;
                    }
                    buf[y * size + x] = w0 * values[i0] + w1 * values[i1] + w2 * values[i2];
                    covered[y * size + x] = true;
                }
            }
        }

        private static void Dilate(ref float[] buf, ref bool[] covered, int size)
        {
            var next = (float[])buf.Clone();
            var nextCovered = (bool[])covered.Clone();
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (covered[y * size + x])
                    {
                        continue;
                    }
                    var sum = 0f;
                    var count = 0;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= size || yy >= size || !covered[yy * size + xx])
                            {
                                continue;
                            }
                            sum += buf[yy * size + xx];
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        next[y * size + x] = sum / count;
                        nextCovered[y * size + x] = true;
                    }
                }
            }
            buf = next;
            covered = nextCovered;
        }

        private static float[] Blur(float[] buf, int size, int radius)
        {
            var next = new float[buf.Length];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var sum = 0f;
                    var count = 0;
                    for (var dy = -radius; dy <= radius; dy++)
                    {
                        for (var dx = -radius; dx <= radius; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= size || yy >= size)
                            {
                                continue;
                            }
                            sum += buf[yy * size + xx];
                            count++;
                        }
                    }
                    next[y * size + x] = sum / count;
                }
            }
            return next;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: PASS（86 + 9 = 95 件）。テスト実行環境で `UnityEngine.Vector3` の演算が動かない（ネイティブ呼び出しで例外）場合は、`BumpMaskBaker` 内の `normalized` / `Dot` / `sqrMagnitude` を自前の計算へ置き換え、ledger に Ruling を残す

- [ ] **Step 5: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin source/COM3D25.MToonMaid.Plugin.Tests
git commit -m "feat: 顔のメッシュから凹凸のマスクを計算する"
```

---

### Task 3: マスクを作ってマテリアルへつなぐ

**Files:**
- Create: `Rendering/FaceBumpMask.cs`
- Modify: `Rendering/MToonProps.cs`, `Rendering/MaterialBackup.cs`, `Rendering/MaterialConverter.cs`, `MaidMToonController.cs`

**Interfaces:**
- Consumes: `BumpMaskBaker`（Task 2）、`GroupSettings.faceBumpLift`（Task 1）、`TBodySkin.morph`（ゲーム。`TMorph.m_vOriVert` / `m_vOriNorm`）
- Produces: `FaceBumpMask.Create(TBodySkin skin, Renderer renderer, int submesh) : Texture2D`（作れなければ null）、`MaterialBackup.bumpMask : Texture2D`、`MToonProps.ShadingShiftTexScale = "_ShadingShiftTexScale"`

Unity のテクスチャとゲームの型は単体テストが無い。動作は Task 4 で確かめる。

- [ ] **Step 1: マテリアルへ入れる・外す**

`Rendering/MToonProps.cs` の `public const string ShadingShiftTex = "_ShadingShiftTex";` の直後に追加:

```csharp
        public const string ShadingShiftTexScale = "_ShadingShiftTexScale";
```

`Rendering/MaterialBackup.cs` の `public bool rampIsRenderTexture;` の直後に追加:

```csharp
        // 顔の凹凸の影を消すためにプラグインが作ったマスク (_ShadingShiftTex)。戻すときや破棄されたときに破棄する
        public Texture2D bumpMask;
```

`Rendering/MaterialConverter.cs` の `ApplyGroup` の `m.SetFloat(MToonProps.FaceFlatten, ...)` の行の直後に追加:

```csharp
            // 強さ 0 ではマスクを外し、パラメーターマップのキーワード (サンプリング) を切る
            var bumpLift = usesSphere ? Mathf.Clamp01(settings.faceBumpLift) : 0f;
            m.SetTexture(MToonProps.ShadingShiftTex, bumpLift > 0f ? backup.bumpMask : null);
            m.SetFloat(MToonProps.ShadingShiftTexScale, bumpLift);
```

（`ApplyGroup` の末尾の `ApplyRenderState` が `_ShadingShiftTex` の有無で `_MTOON_PARAMETERMAP` を決めるので、入れる順はこのままでよい）

`RestoreUnchecked` の先頭（`m.shader = backup.shader;` の前）に追加:

```csharp
            // 破棄するマスクを Toony に残さない。Toony には _ShadingShiftTex が無いため、シェーダーを戻す前に外す
            m.SetTexture(MToonProps.ShadingShiftTex, null);
```

- [ ] **Step 2: メッシュからテクスチャを作る**

`source/COM3D25.MToonMaid.Plugin/Rendering/FaceBumpMask.cs` を作る:

```csharp
using UnityEngine;

namespace COM3D25.MToonMaid.Plugin
{
    /// <summary>顔のメッシュのサブメッシュから、凹凸のマスク (_ShadingShiftTex) を作る</summary>
    public static class FaceBumpMask
    {
        // 周りの法線を平均する半径 (メッシュのローカル)。鼻・口元の凹凸より大きく、頬の丸みより小さい
        private const float SmoothRadius = 0.025f;
        // 法線の差 (1 - cos) がこの値で強さ 1 になる
        private const float FullScale = 0.5f;
        // 凹凸の分布はなめらかなので 256 で足りる
        private const int Size = 256;
        private const int DilateCount = 4;
        private const int BlurRadius = 2;
        private const int BlurPasses = 2;

        /// <summary>作れない (メッシュが読めない・UV や法線の数が合わない・サブメッシュが無い) ときは null</summary>
        public static Texture2D Create(TBodySkin skin, Renderer renderer, int submesh)
        {
            var mesh = GetMesh(renderer);
            if (mesh == null || !mesh.isReadable || submesh < 0 || submesh >= mesh.subMeshCount)
            {
                return null;
            }
            var count = mesh.vertexCount;
            var uv = mesh.uv;
            // 表情は頂点と法線を毎回メッシュへ書き込むため、変形前の形があればそれを使う
            var morph = skin != null ? skin.morph : null;
            var vertices = morph != null && morph.m_vOriVert != null && morph.m_vOriVert.Length == count ? morph.m_vOriVert : mesh.vertices;
            var normals = morph != null && morph.m_vOriNorm != null && morph.m_vOriNorm.Length == count ? morph.m_vOriNorm : mesh.normals;
            if (uv.Length != count || normals.Length != count)
            {
                return null;
            }
            var tris = mesh.GetTriangles(submesh);
            var bump = BumpMaskBaker.ComputeBumpiness(vertices, normals, tris, SmoothRadius, FullScale);
            var pixels = BumpMaskBaker.Rasterize(uv, tris, bump, Size, DilateCount, BlurRadius, BlurPasses);

            var colors = new Color32[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var b = (byte)Mathf.RoundToInt(Mathf.Clamp01(pixels[i]) * 255f);
                colors[i] = new Color32(b, b, b, 255);
            }
            // R をそのまま境界のずれに使うため、sRGB の変換をかけないリニアにする
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "MToonMaid_BumpMask",
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(colors);
            tex.Apply(false, true);
            return tex;
        }

        private static Mesh GetMesh(Renderer renderer)
        {
            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                return skinned.sharedMesh;
            }
            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            return filter != null ? filter.sharedMesh : null;
        }
    }
}
```

- [ ] **Step 3: コントローラーで作る・破棄する**

`MaidMToonController.cs` の `Entry` に追加（`public int materialIndex;` の直後）:

```csharp
            // マスクを作るメッシュを持つレンダラー。materialIndex はこのレンダラーのマテリアルの番号 (= サブメッシュの番号)
            public Renderer renderer;
            // マスクを作れなかった。毎回の走査で作り直して警告を繰り返さない
            public bool bumpMaskFailed;
```

`Scan` の `ScanMaterial(_materialBuffer[k], skin, k, group, groupSettings);` を次へ置き換える:

```csharp
                                ScanMaterial(_materialBuffer[k], skin, renderer, k, group, groupSettings);
```

`ScanMaterial` のシグネチャを次へ置き換える:

```csharp
        private void ScanMaterial(Material m, TBodySkin skin, Renderer renderer, int materialIndex, PartGroup group, GroupSettings groupSettings)
```

`ScanMaterial` の新規変換の 2 行:

```csharp
                    var backup = _converter.Convert(m, group, groupSettings);
                    _entries[id] = new Entry { material = m, backup = backup, skin = skin, materialIndex = materialIndex };
```

を次へ置き換える:

```csharp
                    var backup = _converter.Convert(m, group, groupSettings);
                    var entry2 = new Entry { material = m, backup = backup, skin = skin, renderer = renderer, materialIndex = materialIndex };
                    _entries[id] = entry2;
                    if (EnsureBumpMask(entry2, groupSettings))
                    {
                        _converter.ApplyGroup(m, backup, groupSettings);
                    }
```

`ApplyGroup(PartGroup group, GroupSettings settings)` の `_converter.ApplyGroup(e.material, e.backup, settings);` の直前に追加:

```csharp
                    EnsureBumpMask(e, settings);
```

`RestoreEntry` の末尾（`GameShaderBackup.Replace(...)` の後）に追加:

```csharp
            DestroyBumpMask(e);
```

`RemoveDestroyed` の 2 つ目のループ（`_entries.Remove(id);`）を次へ置き換える:

```csharp
            foreach (var id in _removeBuffer)
            {
                DestroyBumpMask(_entries[id]);
                _entries.Remove(id);
            }
```

`RestoreIfConverted` の前にメソッドを足す:

```csharp
        /// <summary>
        /// 強さが 0 より大きい顔のマテリアルにマスクを作る (数十 ms かかるため、使わない間は作らない)。作ったら true
        /// </summary>
        private bool EnsureBumpMask(Entry e, GroupSettings settings)
        {
            if (e.backup.bumpMask != null || e.bumpMaskFailed || settings.faceBumpLift <= 0f
                || !ToonMaterialClassifier.UsesSphereNormal(e.backup.group))
            {
                return false;
            }
            // 例外を外へ出すと、走査では変換済みのマテリアルが以後飛ばされ、スライダーの操作では UI まで飛ぶ
            string reason;
            try
            {
                e.backup.bumpMask = FaceBumpMask.Create(e.skin, e.renderer, e.materialIndex);
                reason = e.backup.bumpMask == null ? "メッシュが読めないか、UV・法線・サブメッシュが合いません" : null;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
            }
            if (reason != null)
            {
                e.bumpMaskFailed = true;
                _warn("凹凸のマスクを作れないため、このマテリアルでは凹凸の影を消せません: " + e.material.name + " " + reason);
                return false;
            }
            return true;
        }

        private static void DestroyBumpMask(Entry e)
        {
            if (e.backup.bumpMask != null)
            {
                UnityEngine.Object.Destroy(e.backup.bumpMask);
                e.backup.bumpMask = null;
            }
        }
```

- [ ] **Step 4: ビルドとテストを確認する**

Run: `dotnet build source/COM3D25.MToonMaid.Plugin/COM3D25.MToonMaid.Plugin.csproj -c Debug` と `dotnet test source/COM3D25.MToonMaid.Plugin.Tests`
Expected: ビルド成功（警告 0）、テスト PASS（95 件）

- [ ] **Step 5: Commit**

```bash
git add source/COM3D25.MToonMaid.Plugin
git commit -m "feat: 顔の凹凸のマスクを作って境界をずらす"
```

---

### Task 4: 実機検証と仕上げ

**restart-verify スキル**の手順で、ゲーム終了 → `debug.bat` → 起動 → 最新セーブのロード。**終了の前に、ゲームが SceneEditor のシーンを開いているならユーザーに保存の確認をとる**。eval は完全修飾名、ローカル関数は使わず `System.Func` / `System.Action`。比較の撮影では `UnityEngine.Time.timeScale = 0` で動きを止め、光は頭に対して決める（カメラ・モーションが動くため）。検証で変えた状態（設定・ライト・timeScale）は最後に戻す。

**Files:**
- Modify: `README.md`、`docs/superpowers/specs/2026-10-09-mtoon-maid-bump-mask-design.md`（検証の結果）

- [ ] **Step 1: DLL を配置して起動し、セーブをロードする**

restart-verify スキルに従う。DLL のハッシュが一致し、`LogOutput.log` に `MToonMaid 有効化` があること

- [ ] **Step 2: マスクが作られ、強さ 0 では外れることを確かめる**

```csharp
var maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
var plugin = UnityEngine.Object.FindObjectOfType<COM3D25.MToonMaid.Plugin.MToonMaidPlugin>();
var s = plugin.GetSettings(maid0); var wasEnabled = s.enabled; plugin.SetEnabled(maid0, true);
var userSphere = s.common.sphereNormal; var userFlatten = s.common.faceFlatten; var userBump = s.common.faceBumpLift;
System.Action NotifyAll = () => { foreach (COM3D25.MToonMaid.Plugin.PartGroup g in System.Enum.GetValues(typeof(COM3D25.MToonMaid.Plugin.PartGroup))) plugin.NotifyGroupChanged(maid0, g); };
var hs = maid0.body0.goSlot[(int)TBody.hashSlotName["head"]]; var smr = hs.obj.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
System.Func<string> Masks = () => { var sb = new System.Text.StringBuilder(); var ms = smr.sharedMaterials;
  for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].shader.name.Contains("MToon")) sb.Append(i + ":" + ms[i].name + " tex=" + (ms[i].GetTexture("_ShadingShiftTex") == null ? "null" : ms[i].GetTexture("_ShadingShiftTex").name) + " scale=" + ms[i].GetFloat("_ShadingShiftTexScale") + " kw=" + ms[i].IsKeywordEnabled("_MTOON_PARAMETERMAP") + "; ");
  return sb.ToString(); };
"before: " + Masks() + " masks=" + UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Texture2D>().Count(t => t.name == "MToonMaid_BumpMask")
```

`Count` が使えなければ `System.Linq.Enumerable.Count(...)` と書く。

Expected: 顔の MToon（肌・口）がすべて `tex=null`、`kw=False`、マスクは 0 枚（既定 0 では作らない）

続けて作る:

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew(); s.common.faceBumpLift = 1f; NotifyAll(); sw.Stop();
"ms=" + sw.ElapsedMilliseconds + " " + Masks() + " masks=" + System.Linq.Enumerable.Count(UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Texture2D>(), t => t.name == "MToonMaid_BumpMask")
```

Expected: 肌・口が `tex=MToonMaid_BumpMask scale=1 kw=True`、マスクは顔の MToon の数（肌・口で 2）。`ms` が数百 ms 以内。`tail_log` に「凹凸のマスクを作れない」の警告が無いこと

強さ 0 に戻すと外れることを確かめる: `s.common.faceBumpLift = 0f; NotifyAll(); Masks()` で `tex=null kw=False`（マスクは残る: 枚数は 2 のまま）

- [ ] **Step 3: 見え方を比べる**

ユーザーの値で比較が崩れないよう、「顔の陰影を球に」を 0 にして比べる。動きを止め、頭に対して横（前寄り）から光を当てる:

```csharp
var head = maid0.body0.trsHead; var lt = GameMain.Instance.MainLight.GetComponent<UnityEngine.Light>(); var origRot = lt.transform.rotation; var origTs = UnityEngine.Time.timeScale;
s.common.sphereNormal = 0f; s.common.faceBumpLift = 0f; NotifyAll(); UnityEngine.Time.timeScale = 0f;
var hf = head.TransformDirection(UnityEngine.Vector3.up); var hu = head.TransformDirection(UnityEngine.Vector3.left); var hr = UnityEngine.Vector3.Cross(hu, hf);
lt.transform.rotation = UnityEngine.Quaternion.LookRotation((hr * 0.75f - hf * 0.6f - hu * 0.3f).normalized);
var cam = UnityEngine.Camera.main; var sp = cam.WorldToScreenPoint(head.position); var sp2 = cam.WorldToScreenPoint(head.position + cam.transform.up * 0.12f); var h = System.Math.Abs(sp2.y - sp.y) * 1.3f;
((int)System.Math.Max(0, sp.x - h)) + "," + ((int)System.Math.Max(0, UnityEngine.Screen.height - sp.y - h)) + "," + (int)(2 * h) + "," + (int)(2 * h)
```

返った rect で `screenshot` し（強さ 0）、`s.common.faceBumpLift = 1f; NotifyAll();` で同じ rect を撮る（強さ 1）。強さ 1 で鼻の横・口元の影の塊が消え、頬の大きな明暗は残ること。顔に黒や白の点・UV の継ぎ目の線が出ていないこと。光の向きの `hr` の符号で影が見えない側に出たら `-hr` にして撮り直す

後ろからの光で浮き方を確かめる: `lt.transform.rotation = UnityEngine.Quaternion.LookRotation((hf * 0.85f + hr * 0.3f - hu * 0.3f).normalized);` で強さ 1 と `0.5f` を撮り、浮き方を spec に書く

ユーザーの実際の使い方（球との併用）で確かめる: 横からの光へ戻し、`s.common.sphereNormal = userSphere; s.common.faceFlatten = userFlatten;` にして、強さ 0 / 0.5 / 1 を同じ rect で撮る。マスクが球寄りの陰影と合わずに不自然な明るい斑が出ないか、効き目がどの程度残るかを spec に書く

- [ ] **Step 4: 戻すとマスクが破棄されることを確かめる**

```csharp
plugin.SetEnabled(maid0, false);
"off: masks=" + System.Linq.Enumerable.Count(UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Texture2D>(), t => t.name == "MToonMaid_BumpMask")
```

次の eval（1 フレーム以上あと。`Destroy` はフレームの終わりで消える）で枚数を読み直し 0 であること。続けて `plugin.SetEnabled(maid0, true);` で再変換し、強さ 1 のまま次の eval でマスクが 2 枚作り直されていること

- [ ] **Step 5: 戻す**

```csharp
s.common.sphereNormal = userSphere; s.common.faceFlatten = userFlatten; s.common.faceBumpLift = userBump; NotifyAll();
lt.transform.rotation = origRot; UnityEngine.Time.timeScale = origTs; plugin.SetEnabled(maid0, wasEnabled);
"sphere=" + s.common.sphereNormal + " flatten=" + s.common.faceFlatten + " bump=" + s.common.faceBumpLift + " ts=" + UnityEngine.Time.timeScale + " light=" + lt.transform.rotation.eulerAngles
```

Expected: ユーザーの元の値（Step 2 の前に読んだ値）、`ts=1`、ライトが元の向き

- [ ] **Step 6: ウィンドウを確かめる**

```csharp
var wm = (COM3D25.MToonMaid.Plugin.WindowManager)typeof(COM3D25.MToonMaid.Plugin.MToonMaidPlugin).GetField("_windowManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
var tabField = typeof(COM3D25.MToonMaid.Plugin.MToonMaidWindow).GetField("_tab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var wasShown = wm.MainWindow.isShowWnd; wm.MainWindow.isShowWnd = true; "shown"
```

`screenshot`（`window: "MToonMaid"`）で、共通タブの「顔を平らに」の下に「凹凸の影を消す」があること。`tabField.SetValue(wm.MainWindow, 1)`（肌）と `3`（髪）で行が個別のトグルを含めて無効表示、`2`（顔）で有効であること。確かめたら `tabField.SetValue(wm.MainWindow, 0)`、`isShowWnd = wasShown`。`tail_log` に MToonMaid 由来の例外が無いこと

- [ ] **Step 7: README と spec を更新する**

`README.md` の「制限」の「顔の口元などに MToon の影が入るときは…」の行の直後に追加:

```markdown
- 顔の立体的な陰影を残したまま鼻や口元の影だけを消したいときは、「凹凸の影を消す」を上げてください。顔のメッシュから凹凸の場所を自動で求め、その場所だけ影の境界をずらします。光の向きによらず効くため、後ろから光が当たると鼻の線や口角が少し明るく浮くことがあります。表情（口を開けたときなど）には追従しません（顔にだけ効きます）
```

spec の末尾に「## 実機検証の結果」を足し、Step 2〜6 の値と見え方を書く

- [ ] **Step 8: Commit**

```bash
git add README.md docs/superpowers/specs/2026-10-09-mtoon-maid-bump-mask-design.md docs/superpowers/plans/2026-10-09-mtoon-maid-bump-mask.md
git commit -m "docs: 凹凸の影を消す設定の使い方と実機検証の結果を追加"
```

## レビュー却下メモ

- 「半径をメッシュのバウンズで正規化する」 — 標準の顔で試作した値で、MOD の顔の大きさの分布が分からないまま正規化の基準を決められない。spec の範囲外に明記した
- 「UV アイランドをまたぐぼかし・UV 範囲外・縮退三角形のテスト」 — 縮退は `den` の判定で飛ばし、範囲外は画素の範囲で切るだけの単純な処理。アイランドの混ざりは実機の見え方（Task 4 Step 3 の線・点の確認）で見る
- 「Task 1 の時点ではスライダーが効かない」 — Task 3 までを 1 回で実装するため、途中のコミットで効かなくても困らない
