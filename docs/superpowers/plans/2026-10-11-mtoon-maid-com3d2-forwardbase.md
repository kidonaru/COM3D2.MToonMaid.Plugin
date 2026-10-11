# COM3D2 (2.0) の MToon の陰影・リムを直す Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans (インライン実行。このプロジェクトでは subagent-driven-development は使わない) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** COM3D2 (2.0) で MToon の主光源のパスを正しく判定させ、髪のリムの強さ・髪の白っぽさを COM3D2.5 と同じくゲームの Toony に合わせる。

**Architecture:** Unity 5.6 は `Tags { "LightMode" = "ForwardBase" }` から `UNITY_PASS_FORWARDBASE` を自動で定義しない（2022 は定義する）。このため 2.0 では `MToon_IsForwardBasePass()` が false になり、主光源のパスが追加ライトの計算で描かれている。MToon10 の ForwardBase の 2 パス（本体・輪郭）で、未定義なら自分で定義する。2022 では既に定義されているので、2.5 のコンパイル結果は変わらない。

**Tech Stack:** Unity 5.6.4f1 / 2022.3.62f2 のシェーダー（HLSL）、COM3D2 (2.0) の BepInExMcp（実機の C# 評価）、bun（比較スクリプト）

**Spec:** 別の spec は無い。根拠はユーザーの依頼（「2.0 の髪のリムと陰影を合わせる」）と、下の「調査結果」（2026-10-11 に 2.0 の実機で測定）。前の計画は `docs/superpowers/plans/2026-10-11-mtoon-maid-com3d2-support.md`。

## 調査結果（計画の前提）

2.0 の実機（BepInExMcp）で、1 人目のメイドの hairR のマテリアルを写した球を 2 つ作り、ゲームの Toony（A）と MToon（B）を同じライトで撮って赤道の画素を比べた。`_MainTex` / `_ShadowTex` は白、ランプと `_ShadowRateToon` は髪の実物のまま。

- MToon は N·L によらず平らに「ライト色そのまま」になる（ライト強さ 0.2 で 51、1 で 255）。ランプも `_ShadowRateToon` も効かない
- 環境光 0.3 だけにすると、Toony は 76、MToon は 0。環境光の項が無い
- これは `MToon_IsForwardBasePass()` が false のときの式（陰影の係数は追加ライト用、ランプ 1、環境光 0、リムは `_RimLightingMix` × ライト色）とちょうど一致する。リムの係数がランプも SRT も通らずライト色そのままになるので、髪の縁が強く光り、髪全体が白っぽく見えていた
- `UNITY_PASS_FORWARDBASE` は 5.6 / 2022 のどちらの CGIncludes にも `#define` が無い（2022 はコンパイラが LightMode から定義し、5.6 はしない）
- 2.0 の Toony の髪の式は 2.5 で測った式と同じだった（リム ＝ フレネル ×（ライト × SRT × ランプ ＋ 環境光）、環境光はランプを通らない）。式の作り直しは要らない
  - 2.5 の髪にある「N·L < −0.3 の裏側で全体が暗くなる」処理は 2.0 には無い（MToon はもともと再現していない）

## Global Constraints

- 2.5 の描画を変えない（2022 で `UNITY_PASS_FORWARDBASE` が定義済みなら、追加する定義は何もしない形にする）
- シェーダーの原本は `UnityProject2022/Assets/Shaders` だけ。`UnityProject56` は `build-bundle.ps1` が毎回作る
- 5.6 のバンドルは d3d11 だけ向け（前の計画の裁定）
- コードコメントは日本語で Why not だけ（`// <却下した案>にすると、<起きる問題>`）
- `deploy.bat` / `deploy.ps1` は実行しない
- ゲームの終了・起動はユーザーの了承を得てから（編集中のシーンを失わないため）

## Review Focus

1. **2.5 の見た目が変わらないこと** → Step 3 で、2022 では `UNITY_PASS_FORWARDBASE` が定義済みであることを `#error` でコンパイル時に確かめる（2022 のバンドルが通り、5.6 だけが止まる）
2. **前髪の影が 2.0 で初めて効くこと** → 今まで 2.0 では ForwardBase 扱いでないため前髪の影の計算（`GetMToonLighting_HairShadow`）が飛ばされていた。直すと初めて顔に前髪の影が落ちる。5.6 の d3d11 で影の位置・向きが合っているかを Step 8 で顔のアップを撮って確かめる
3. **輪郭（アウトライン）の色が変わること** → 輪郭のパスも ForwardBase なので、線に混ぜる陰影にランプ・環境光が入る（2.5 と同じになる）。Step 8 の全身の画像で、線が消えたり白く飛んだりしていないことを見る
4. **追加ライト（ポイント・スポット）の描画は変わらないこと** → ForwardAdd のパスは今も `MToon_IsForwardBasePass()` が false で正しい（`#else` の「unexpected」に入るが、返す値は ForwardAdd と同じ）。今回は触らない
5. **シェーダーのキーワード数・サンプラー数の上限** → キーワードは増えない。5.6 の ForwardBase で使われるサンプラーは増えるが、2022 と同じ集合なので d3d11 の上限 (16) 以内。5.6 のビルドログに `Shader error` が無いことを Step 5 で見る

---

### Task 1: Unity 5.6 でも ForwardBase のパスと判定させる

**Files:**
- Modify: `UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader`（先頭の改変一覧 1〜12 行目、`FORWARD_BASE` パスの 131〜149 行目、`FORWARD_BASE_OUTLINE` パスの 167〜189 行目）
- Modify: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle_com3d2`（5.6 で作り直す）
- Modify: `source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle`（2022 で作り直す。中身の意味は変わらない）
- Modify: `README.md:406-408`（制限の 2.0 の 3 行を消す）
- Test: 実機の比較スクリプト（scratchpad の `sph20/`。リポジトリには入れない）

**Interfaces:**
- Consumes: なし（前の計画のビルドスクリプト `build-bundle.bat <com3d25|com3d2|all>`、`debug.bat <com3d25|com3d2|all>` を使う）
- Produces: なし

- [ ] **Step 1: 比較スクリプトを scratchpad に用意する**

scratchpad（`C:\Users\kidon\AppData\Local\Temp\claude\W--COM3D2-5-work-COM3D25-MToonMaid-Plugin\<セッション ID>\scratchpad`）に次の 4 ファイルを作る。既にあれば中身を下と同じにする。

`ev20.mjs`:

```js
// 使い方: bun ev20.mjs <C# のファイル>。COM3D2 (2.0) の BepInExMcp /eval へ送り、結果だけを出す
import { readFileSync } from 'fs';
const code = readFileSync(process.argv[2], 'utf8');
const r = await fetch('http://127.0.0.1:18650/eval', { method: 'POST', headers: { 'Content-Type': 'text/plain; charset=utf-8' }, body: code });
const j = await r.json();
console.log(j.ok ? j.result : ('ERROR ' + JSON.stringify(j)));
```

`sph20/setup_a.cs`:

```csharp
// 手順 1/2。試験用のカメラ・ライトと、Toony の髪を写した球 A を作る。MToonMaid は OFF にして ON へ戻す (MToon への変換は次のフレーム)
var plugT = System.AppDomain.CurrentDomain.GetAssemblies().Select(asm => asm.GetType("COM3D25.MToonMaid.Plugin.MToonMaidPlugin")).FirstOrDefault(x => x != null);
var plug = UnityEngine.Object.FindObjectOfType(plugT);
var maid = GameMain.Instance.CharacterMgr.GetMaid(0);
var hairSlot = new[] { __SLOTS__ }.First(sl => maid.body0.IsSlotNo(sl) && maid.body0.GetSlot(sl).obj != null);
var old = UnityEngine.GameObject.Find("MTM_TestRoot"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
var root = new UnityEngine.GameObject("MTM_TestRoot"); root.transform.position = new UnityEngine.Vector3(0, 500, 0);
var camGo = new UnityEngine.GameObject("cam"); camGo.transform.parent = root.transform; camGo.transform.localPosition = new UnityEngine.Vector3(0, 0, -3);
var cam = camGo.AddComponent<UnityEngine.Camera>(); cam.enabled = false; cam.fieldOfView = 25; cam.cullingMask = 1 << 30; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = UnityEngine.Color.black; cam.nearClipPlane = 0.1f; cam.farClipPlane = 10f;
cam.targetTexture = new UnityEngine.RenderTexture(512, 512, 24, UnityEngine.RenderTextureFormat.ARGB32);
var lgo = new UnityEngine.GameObject("light"); lgo.transform.parent = root.transform; var l = lgo.AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.cullingMask = 1 << 30; l.color = UnityEngine.Color.white; l.shadows = UnityEngine.LightShadows.None;
var white = new UnityEngine.Texture2D(4, 4, UnityEngine.TextureFormat.RGBA32, false); white.name = "MTM_white"; var px = new UnityEngine.Color[16]; for (int i = 0; i < 16; i++) px[i] = UnityEngine.Color.white; white.SetPixels(px); white.Apply();
plugT.GetMethod("SetEnabled").Invoke(plug, new object[] { maid, false });
var matA = new UnityEngine.Material(maid.body0.GetSlot(hairSlot).obj.GetComponentsInChildren<UnityEngine.Renderer>(true)[0].sharedMaterial);
plugT.GetMethod("SetEnabled").Invoke(plug, new object[] { maid, true });
matA.SetTexture("_MainTex", white); matA.SetTexture("_ShadowTex", white); matA.SetFloat("_HiRate", 0); matA.SetFloat("_OutlineWidth", 0); matA.SetColor("_RimColor", UnityEngine.Color.white);
matA.SetFloat("_RimPower", 2); matA.SetFloat("_RimShift", 0);
var s = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere); s.name = "A"; UnityEngine.Object.DestroyImmediate(s.GetComponent<UnityEngine.Collider>()); s.transform.parent = root.transform; s.transform.localPosition = UnityEngine.Vector3.zero; s.layer = 30; s.GetComponent<UnityEngine.Renderer>().sharedMaterial = matA;
"A=" + matA.shader.name + " slot=" + hairSlot
```

`sph20/setup_b.cs`:

```csharp
// 手順 2/2。MToon に戻った髪を写して球 B を作る。陰影の濃さ 1・リムのライト色の影響 1・均一化 0 にし、球 A と同じリムにそろえる
var root = UnityEngine.GameObject.Find("MTM_TestRoot");
var white = root.transform.Find("A").GetComponent<UnityEngine.Renderer>().sharedMaterial.GetTexture("_MainTex");
var maid = GameMain.Instance.CharacterMgr.GetMaid(0);
var hairSlot = new[] { __SLOTS__ }.First(sl => maid.body0.IsSlotNo(sl) && maid.body0.GetSlot(sl).obj != null);
var matB = new UnityEngine.Material(maid.body0.GetSlot(hairSlot).obj.GetComponentsInChildren<UnityEngine.Renderer>(true)[0].sharedMaterial);
matB.SetTexture("_MainTex", white); matB.SetTexture("_ShadowTex", white); matB.SetFloat("_HiRate", 0); matB.SetFloat("_HiRateScale", 0); matB.SetFloat("_OutlineWidth", 0); matB.SetColor("_RimColor", UnityEngine.Color.white);
matB.SetFloat("_RimFresnelPower", 2); matB.SetFloat("_RimLift", 0); matB.SetFloat("_ShadeStrength", 1); matB.SetFloat("_RimLightingMix", 1); matB.SetFloat("_GiEqualization", 0);
var old = root.transform.Find("B"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var s = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere); s.name = "B"; UnityEngine.Object.DestroyImmediate(s.GetComponent<UnityEngine.Collider>()); s.transform.parent = root.transform; s.transform.localPosition = UnityEngine.Vector3.zero; s.layer = 30; s.GetComponent<UnityEngine.Renderer>().sharedMaterial = matB;
"B=" + matB.shader.name
```

`sph20/cmp.mjs`:

```js
// 使い方: bun sph20/cmp.mjs。setup_a/b.cs で作った球 A (Toony) と B (MToon) を同じ条件で撮り、赤道の画素を比べる
const EV = 'http://127.0.0.1:18650/eval';
const ev = async (code) => { const j = await (await fetch(EV, { method: 'POST', body: code })).json(); if (!j.ok) throw new Error(JSON.stringify(j)); return j.result; };
const render = (sph, intensity, amb) => `
var root = UnityEngine.GameObject.Find("MTM_TestRoot"); var cam = root.transform.Find("cam").GetComponent<UnityEngine.Camera>();
var lt = root.transform.Find("light"); lt.rotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(-1, 0, 0)); lt.GetComponent<UnityEngine.Light>().intensity = ${intensity}f;
root.transform.Find("A").gameObject.SetActive("${sph}" == "A"); root.transform.Find("B").gameObject.SetActive("${sph}" == "B");
var amb = new UnityEngine.Color(${amb}f, ${amb}f, ${amb}f, 1);
var am = UnityEngine.RenderSettings.ambientMode; var al = UnityEngine.RenderSettings.ambientLight; var ap = UnityEngine.RenderSettings.ambientProbe;
var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(amb);
UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; UnityEngine.RenderSettings.ambientLight = amb; UnityEngine.RenderSettings.ambientProbe = sh;
cam.Render();
UnityEngine.RenderSettings.ambientMode = am; UnityEngine.RenderSettings.ambientLight = al; UnityEngine.RenderSettings.ambientProbe = ap;
var prev = UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active = cam.targetTexture;
var tex = new UnityEngine.Texture2D(512, 512, UnityEngine.TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, 512, 512), 0, 0); tex.Apply(); UnityEngine.RenderTexture.active = prev;
var sb = new System.Text.StringBuilder();
for (int x = 0; x < 512; x += 4) { var c = tex.GetPixel(x, 256); sb.Append(x + "," + (int)(c.r * 255 + 0.5f) + "," + (int)(c.g * 255 + 0.5f) + "," + (int)(c.b * 255 + 0.5f) + ";"); }
UnityEngine.Object.Destroy(tex); sb.ToString()`;
const parse = (s) => s.split(';').filter(Boolean).map((t) => t.split(',').map(Number));
const cases = [
  { name: '横からの光・環境光なし', intensity: 1, amb: 0 },
  { name: '横からの弱い光 0.4・環境光なし', intensity: 0.4, amb: 0 },
  { name: '横からの光・環境光 0.2', intensity: 1, amb: 0.2 },
  { name: '環境光 0.3 だけ', intensity: 0, amb: 0.3 },
];
let fail = false, comparedCases = 0;
for (const c of cases) {
  const a = parse(await ev(render('A', c.intensity, c.amb)));
  const b = parse(await ev(render('B', c.intensity, c.amb)));
  let maxDiff = 0, worst = '', compared = 0;
  for (let i = 0; i < a.length; i++) {
    const [x, ...ca] = a[i]; const cb = b[i].slice(1);
    if (ca.every((v) => v === 0) && cb.every((v) => v === 0)) continue; // 球の外
    if (ca.some((v) => v >= 250)) continue; // ゲーム側が頭打ち
    compared++;
    const d = Math.max(...ca.map((v, k) => Math.abs(v - cb[k])));
    if (d > maxDiff) { maxDiff = d; worst = `x=${x} Toony=${ca} MToon=${cb}`; }
  }
  // 肌は光 1 でゲーム側が白く飛び、比べる画素が残らない
  if (compared === 0) { console.log(`SKIP ${c.name}: ゲーム側が頭打ち`); continue; }
  comparedCases++;
  const ok = maxDiff <= 4;
  if (!ok) fail = true;
  console.log(`${ok ? 'PASS' : 'FAIL'} ${c.name}: 比較 ${compared} 画素・最大差 ${maxDiff} ${worst}`);
}
await ev('UnityEngine.GameObject.Find("MTM_TestRoot").transform.Find("light").GetComponent<UnityEngine.Light>().intensity = 1f; "ok"');
console.log(fail || comparedCases === 0 ? 'RESULT FAIL' : 'RESULT PASS');
```

`setup_a.cs` / `setup_b.cs` の `__SLOTS__` は、写すスロットの候補（先頭から最初に見つかったもの）。実行のたびに次の形で置き換えたコピーを作って送る:

```bash
# 髪: SLOTS='"hairR", "hairF", "hairS"'、肌: SLOTS='"body"'
for f in setup_a setup_b; do sed "s/__SLOTS__/$SLOTS/" sph20/$f.cs > sph20/_$f.cs; done
```

片付け用の `sph20/cleanup.cs`:

```csharp
var r = UnityEngine.GameObject.Find("MTM_TestRoot"); if (r != null) UnityEngine.Object.Destroy(r); "cleaned"
```

許容差 4（/255）の根拠: 球 A・B は `_MainTex` / `_ShadowTex` が白、陰影の濃さ 1、リムのライト色の影響 1 なので、2.5 で合わせ込んだ式どおりなら両者は同じ値になる。差は half 精度と 8bit の丸めだけ。

- [ ] **Step 2: 今の DLL で比較が失敗するのを見る（RED・実機）**

前提: 2.0 が起動中で、1 人目のメイドが髪付きで出ている（エディット画面など）。起動していなければ AskUserQuestion で起動を頼む。

Run（scratchpad で）:
```bash
for SLOTS in '"hairR", "hairF", "hairS"' '"body"'; do
  for f in setup_a setup_b; do sed "s/__SLOTS__/$SLOTS/" sph20/$f.cs > sph20/_$f.cs; done
  timeout 30 bun ev20.mjs sph20/_setup_a.cs && timeout 30 bun ev20.mjs sph20/_setup_b.cs && timeout 60 bun sph20/cmp.mjs; timeout 20 bun ev20.mjs sph20/cleanup.cs
done
```
Expected: 髪（1 周目）は `A=CM3D2/Toony_Lighted_Hair_Outline_Tex`、肌（2 周目）は `A=CM3D2/Toony_Lighted_Outline` など髪以外の Toony、どちらも `B=COM3D25/MToon10` で、比べられたケースはすべて FAIL（肌は光 1 のケースが `SKIP`）。横からの光では MToon が `255,255,255`（平ら）、「環境光 0.3 だけ」では MToon が `0,0,0`。最後に `RESULT FAIL`。
（2026-10-11 の計画作成時の髪の実測: 最大差 158 / 105 / 135。肌は計画作成時には測っていない）

`B=` が `CM3D2/...` のままなら、MToon への変換が間に合っていない。`_setup_b.cs` だけをもう一度実行してから `cmp.mjs` を実行する。肌のスロットが MToon にならない設定（グループが OFF など）なら、肌の周は飛ばし、`Ruling:` に記録する。

- [ ] **Step 3: 5.6 だけが未定義であることをコンパイルで確かめる（RED・コンパイル）**

Unity エディタで `UnityProject2022` を開いていたら閉じる（batchmode がロックで失敗する）。

`vrmc_materials_mtoon.shader` の `FORWARD_BASE` パスの `#pragma fragment MToonFragment` の直後（`#include` の前）に、一時的に次を入れる:

```hlsl
            #ifndef UNITY_PASS_FORWARDBASE
            #error UNITY_PASS_FORWARDBASE is not defined
            #endif
```

Run: `cmd //c build-bundle.bat com3d25`
Expected: 成功（`コピーしました: ...\Resources\mtoon_bundle`）。`Shader error` が出ない ＝ 2022 は定義済み。

Run: `cmd //c build-bundle.bat com3d2`
Expected: 失敗。`Shader error in 'COM3D25/MToon10': UNITY_PASS_FORWARDBASE is not defined` を含む行が出て `シェーダーのコンパイルエラーがあります`。`mtoon_bundle_com3d2` は更新されない（`git status` で変更なし）。

- [ ] **Step 4: ForwardBase の 2 パスで定義する**

Step 3 の `#error` の 3 行を消し、`FORWARD_BASE` と `FORWARD_BASE_OUTLINE` の両方のパスで、`#pragma fragment MToonFragment` の直後（`FORWARD_BASE_OUTLINE` は `#define MTOON_PASS_OUTLINE` の直前）に次を入れる:

```hlsl
            // LightMode から定義させるだけにすると、Unity 5.6 (COM3D2) では定義されず、主光源が追加ライトの式で描かれる
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif
```

ファイル先頭の改変一覧の最後（`// - ワールド基準の輪郭の幅に ...` の次の行）に 1 行足す:

```hlsl
// - ForwardBase のパスで UNITY_PASS_FORWARDBASE が未定義なら定義する (Unity 5.6 は LightMode から定義しない)
```

- [ ] **Step 5: 両方のバンドルを作り直す**

Run: `cmd //c build-bundle.bat all`
Expected: 2 回の `コピーしました:`（`mtoon_bundle` と `mtoon_bundle_com3d2`）、`Shader error` なし、終了コード 0。

Run: `rg -c "Shader error" UnityProject56/Logs/build-bundle.log UnityProject2022/Logs/build-bundle.log`
Expected: 一致なし（rg の終了コード 1）。

- [ ] **Step 6: DLL をビルドしてテストを通し、2.0 へ入れる**

Run: `dotnet test source/COM3D25.MToonMaid.Plugin.Tests -c Debug 2>&1 | tail -3`
Expected: `失敗: 0`。

AskUserQuestion で、2.0 のゲームを終了してよいか（編集中のシーンは保存済みか）を確認する。了承を得たら:

Run（scratchpad で）: `echo 'UnityEngine.Application.Quit(); "quit"' > quit.cs && timeout 20 bun ev20.mjs quit.cs`
Expected: `quit`。続けて `tasklist | grep -i COM3D2x64` が `W:\COM3D2` のプロセスを出さなくなるまで待つ（2.5 も起動中なら、`powershell -NoProfile -Command "(Get-Process COM3D2x64 -ErrorAction SilentlyContinue).Path"` に `W:\COM3D2\COM3D2x64.exe` が無いことで判定する）。

Run: `cmd //c debug.bat all`
Expected: `COM3D2 へデプロイしました`。2.5 の行は、2.5 が起動中なら `警告: COM3D2.5 へのデプロイに失敗しました`、停止中なら `COM3D2.5 へデプロイしました`（どちらでもよい）。最後に `ビルドに成功しました`。

Run: `sha256sum "BepInEx (COM3D2)/plugins/COM3D25.MToonMaid.dll" "/w/COM3D2/BepInEx/plugins/COM3D25.MToonMaid.dll"`
Expected: 2 つのハッシュが一致。

- [ ] **Step 7: 比較が通るのを見る（GREEN・実機）**

AskUserQuestion で 2.0 の起動を頼む（前回と同じく、ユーザーが起動して髪付きのメイドを出す）。起動後、`BepInEx\LogOutput.log` に MToonMaid のエラーが無いことを確かめる:

Run: `rg -n "MToonMaid" /w/COM3D2/BepInEx/LogOutput.log | rg -i "error|exception" ; echo rg_exit=$?`
Expected: `rg_exit=1`（一致なし）。

Run（scratchpad で）:
```bash
for SLOTS in '"hairR", "hairF", "hairS"' '"body"'; do
  for f in setup_a setup_b; do sed "s/__SLOTS__/$SLOTS/" sph20/$f.cs > sph20/_$f.cs; done
  timeout 30 bun ev20.mjs sph20/_setup_a.cs && timeout 30 bun ev20.mjs sph20/_setup_b.cs && timeout 60 bun sph20/cmp.mjs; timeout 20 bun ev20.mjs sph20/cleanup.cs
done
```
Expected: 髪・肌の両方で `SKIP` 以外がすべて `PASS`（最大差 4 以下）、`RESULT PASS`。

FAIL が残る場合は、まず差の出方で切り分ける。差が N·L によらずほぼ一様に 5〜8 程度なら 5.6 と 2022 の丸め・SH の実装差なので、赤道の値を ledger に残して許容差を 8 に緩めてよい（`Ruling:` に記録）。特定の N·L の範囲（影側だけ・縁だけなど）に差が集まるなら式の差なので、superpowers:systematic-debugging で原因を探す。差がランプの暗い側だけなら `_ToonRampDarkColor` / `_ShadeStrength` の扱い、環境光だけなら `MToon_SampleSH`（5.6 の `ShadeSH9`）を疑う。

- [ ] **Step 8: 実機の見た目を確かめる**

ユーザーのカメラは動かさない。今の構図のまま、MToonMaid の ON / OFF で画面を撮る。

`off.cs` / `on.cs`（scratchpad）:
```csharp
var t = System.AppDomain.CurrentDomain.GetAssemblies().Select(asm => asm.GetType("COM3D25.MToonMaid.Plugin.MToonMaidPlugin")).FirstOrDefault(x => x != null);
var p = UnityEngine.Object.FindObjectOfType(t);
t.GetMethod("SetEnabled").Invoke(p, new object[] { GameMain.Instance.CharacterMgr.GetMaid(0), false });
"off"
```
（`on.cs` は `false` を `true`、`"off"` を `"on"` にしたもの）

撮影は `http://127.0.0.1:18650/capture?target=screen` を bun の fetch で取り、PNG を scratchpad に保存する（`off.cs` の後に `cap_off.png`、`on.cs` の後に `cap_on.png`。切り替えの反映は次のフレームなので、評価と撮影の間に 1 秒おく）。

Read で 2 枚を見比べ、次を確かめる:
- 髪のリムが、前回（`cap20_mtoon.png`）のような水色の強い縁ではなく、ゲーム（`cap_off.png`）に近い細く弱い縁になっている
- 髪の光側・影側の明るさがゲームに近い（髪全体が白っぽくない）
- 輪郭の線が消えたり白く飛んだりしていない（Review Focus 3）
- 肌・服の陰影と明るさが、ゲーム（`cap_off.png`）と大きくずれていない（比較スクリプトは髪と肌だけなので、服は目視で見る）
- 顔に前髪の影が落ち、位置が前髪の形に沿っている（Review Focus 2）。顔が小さくて判断できなければ、ユーザーにエディット画面で顔へ寄ってもらい、もう一度撮る

2 枚を SendUserFile でユーザーに送り、見た目が合っているかを確認してもらう。前髪の影の位置がずれている・上下が反転しているなど 5.6 固有の崩れが出たら、`Ruling:` に記録し、README の制限に「COM3D2 (2.0) では前髪の影が崩れる」と書いて別の計画にする（この計画では直さない）。

- [ ] **Step 9: README の制限を直す**

`README.md` の制限から次の 3 行を消す:

```markdown
- COM3D2 (2.0) では、ゲームの陰影・リムの引き継ぎを COM3D2.5 のゲームのシェーダーに合わせているため、見た目がゲームとずれます
  - 髪のリムが明るく（ランプの色が付いて）出ます。ゲームの 2.0 の髪のリムはごく弱く、色も付きません
  - 髪全体がゲームより白っぽくなります
```

同じ節の髪の裏側の行は、2.0 の髪のシェーダーにはその陰影が無いので、COM3D2.5 のことだと分かるように直す:

```markdown
- COM3D2.5 の髪のゲームのシェーダー（`Toony_Lighted_Hair_Outline_Tex`）にある、光の真裏側をさらに暗くする陰影は再現しません。MToon の髪の裏側はゲームより明るくなります
```

Run: `rg -n "白っぽく|ランプの色が付いて" README.md; echo rg_exit=$?`
Expected: `rg_exit=1`。

- [ ] **Step 10: コミット**

```bash
git add UnityProject2022/Assets/Shaders/MToon10/vrmc_materials_mtoon.shader source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle source/COM3D25.MToonMaid.Plugin/Resources/mtoon_bundle_com3d2 README.md docs/superpowers/plans/2026-10-11-mtoon-maid-com3d2-forwardbase.md
git commit -m "fix(shader): COM3D2 (2.0) で主光源のパスが追加ライトの式で描かれるのを直す"
```

コミット本文（Why）には次を書く: Unity 5.6 は LightMode から `UNITY_PASS_FORWARDBASE` を定義しないため、2.0 では陰影・ランプ・環境光が無く、リムがライト色そのままになって髪が白っぽく縁が強く光っていた。2022 は定義済みなので 2.5 は変わらない。`.env.sample` の変更（ユーザーの作業）はコミットに含めない。


## レビュー却下メモ

- 2.5 の実機で前後の画像を比べる — Step 3 の `#error` で 2022 では `UNITY_PASS_FORWARDBASE` が定義済みと分かれば、`#ifndef` の中は前処理で消え、コンパイルされるコードは同じになる。2.5 の再起動（編集中のシーンを閉じる）に見合う追加の情報が無い
- 完了時に memory（com3d2-20-support-feasibility）を更新する手順を足す — 計画作成時に、原因（5.6 で UNITY_PASS_FORWARDBASE が無い）へ書き換え済み
- README の旧記述「2.0 の髪のリムはごく弱く、色も付かない」との食い違い — 旧記述は誤った見立てで、Step 9 で消す。2.0 の髪の Toony が 2.5 と同じ式であることは計画作成時に実測済み（調査結果の節）
- 5.6 の組み込み include が UNITY_PASS_FORWARDBASE で挙動を変える可能性 — 5.6 の CGIncludes に UNITY_PASS_FORWARDBASE の参照は無い（計画作成時に rg で確認）。影響は MToon の define.hlsl だけ
