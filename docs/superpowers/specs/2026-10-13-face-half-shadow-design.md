# 顔の半面切り替えの影 設計

日付: 2026-10-13
対象: COM3D25.MToonMaid.Plugin

## 背景

顔の自分の影は、ゲームの Toony と同じく N·L（`_ShadowRateToon` とランプ）で決まる。光の向きが変わると境界線が顔の上を動くため、立体的なリアル調に見える。境界を段階にするだけでは、顔のメッシュの法線のでこぼこがまだらな影になる（2026-10-13 の実機の試し撮りで確認）。

アニメ塗りに近づけるため、顔の影を光の向きで「影なし → 半面 → 全面 → 半面 → 影なし」と切り替える。状態の間は、境界線を動かさずに半面や全面を一様にフェードさせる。

この機能は「影側を一色に」（`flatShadowRamp`、ブランチ `feat/flat-shadow-ramp`）の式の上に作る。

## 動き方

顔グループで「半面切り替えの影」を ON にしたときだけ、次のように描く。

- 顔の自分の影は N·L で決めない。ゲームの `_ShadowRateToon` とランプ（`_ToonRamp`）の N·L による読み込みをやめ、半面・全面の濃さ（下記）だけで決める
- 影の色は「影側を一色に」が ON のときと同じ（`_ToonRampDarkColor`、ランプを読まないマテリアルは `_ShadowTex × _ShadeColor`）。光側の色はランプの光側の色（u = 1）。陰影の濃さ（`_ShadeStrength`）は今の式のまま効く
- 落ち影・前髪の影は今の式のまま重ねる。追加ライト（ForwardAdd）は今の式のまま（N·L と `_AdditionalLightShade`）
- アウトラインのパスも同じ濃さを使う（線の陰影が顔の影と合う）

### 角度と濃さ

頭の向き（後述の正面 F・頭の上 U・右 R = U × F）で、主光源へ向かう単位ベクトル L を分解する。

- 水平角 θ = atan2(|L·R|, L·F)。0 = 正面、π = 真後ろ。左右は対称に扱う
- 仰角 e = atan2(|L·U|, √((L·F)² + (L·R)²))。0〜π/2
- 仰角による減衰 k = 1 − smoothstep(60°, 90°, e)
- 光と反対側の半面の濃さ far = smoothstep(半面角 − 幅/2, 半面角 + 幅/2, θ) × k
- 光側の半面の濃さ near = smoothstep(全面角 − 幅/2, 全面角 + 幅/2, θ) × k
- 幅が 0 のときは smoothstep を step にする（0 除算を避ける）

画素がどちら側の半面かは、頭の中心 C を通り法線が R の面からの符号付き距離 s = (P − C)·R で決める。

- 光側への寄り t = smoothstep(−ぼかし, ぼかし, s × sign(L·R))。ぼかし 0 なら step
- 顔の影の濃さ amount = lerp(far, near, t)
- θ = 0 で far = near = 0、θ = π で far = near = 1 なので、L·R = 0 で左右が入れ替わっても値は飛ばない

### シェーダーでの差し替え

`amount` から作る `faceLit = 1 − amount` を、ForwardBase で `_ShadowRateToon` の値の代わりに使う。

- `GetMToonLighting_Shade`: `toonShade = faceLit`（`_UseShadowRateToon` によらない）。落ち影・前髪の影を掛けるのは今のまま
- `GetMToonLighting_ToonRamp`: ランプの N·L による読み込みを光側の色（`GetMToonLighting_ToonRampLit()`）に置き換え、`shadowRate = faceLit`、`_FlatShadowRamp` は 1 として扱う（`unshadowed = lerp(1, 落ち影 × (1 − 前髪の影), faceLit) × faceLit`）
- 上のどちらも、頭の向きが届いていない（`dot(_FaceHalfForward, _FaceHalfForward) < 1e-6`）間は今の式で描く

主光源の向きはシェーダーの `directLightDirection` から取る。プラグインで主光源を探して角度を渡す形にすると、前髪の影で起きたように、プラグインが選ぶライトとシェーダーを照らすライトがずれる。

## 設定

`GroupSettings` に顔専用の項目を足す。`GroupSettingKeys.IsGroupOnly` を true にし（共通から継承しない、前髪の影と同じ）、`AppliesTo` は前髪の影と同じく顔だけ。`All` と `CopyField` に入れる。

| フィールド | キー | 型・範囲 | 既定 | UI の表示名 |
|---|---|---|---|---|
| `faceHalfShadow` | `faceHalfShadow` | bool | false | 半面切り替えの影 |
| `faceHalfAngle` | `faceHalfAngle` | 0〜180（度） | 45 | 半面になる角度 |
| `faceFullAngle` | `faceFullAngle` | 0〜180（度） | 135 | 全面になる角度 |
| `faceHalfFadeWidth` | `faceHalfFadeWidth` | 0〜90（度） | 30 | フェード幅 |
| `faceHalfSoftness` | `faceHalfSoftness` | 0〜0.02（m） | 0.005 | 境目のぼかし（UI は cm で 0〜2） |

- 既定 OFF なので今の見た目は変わらない。設定ファイルの版（`MaidSettings.CurrentVersion`）は上げない。要素の無いファイルはフィールド初期値で読む
- 仰角で薄める 60°〜90° は固定値（シェーダーの定数）

## プラグイン側の値の書き込み

### 設定を変えたとき（`MaterialConverter.ApplyGroup`）

顔グループのマテリアルへ次を書く。値の変換は純粋関数のクラス `FaceHalfShadowParams`（net48 のテストで使えるよう Unity 型に依存しない）に置く。

- `_FaceHalfShadow`: ON で 1、OFF で 0。顔以外のグループは常に 0
- `_FaceHalfAngle` / `_FaceFullAngle` / `_FaceHalfFadeWidth`: ラジアン。範囲外は丸め、NaN・無限大は既定値にする。全面角は半面角以上へそろえる
- `_FaceHalfSoftness`: m。0〜0.02 へ丸める

### 毎フレーム（カメラの描画の直前）

`MToonMaidPlugin.OnCameraPreCull` で、前髪の影・瞳の視線と同じ並びに `MaidMToonController.UpdateFaceHalfShadow(face)` を呼ぶ（例外は同じく try で受けて 1 回だけ警告する）。解決した顔の設定が ON のときだけ、顔のマテリアル（`_faceMaterials`）へ書く。

- `_FaceHalfCenter`: head スロットの Renderer のローカルバウンズの中心をワールドへ写した点（前髪の影の投影の中心と同じ計算）
- `_FaceHalfForward`: `head.up`（前髪の影の `FaceForward`）
- `_FaceHalfUp`: `-head.right`（前髪の影の `HeadTop`）

頭や head の Renderer が見つからないときは `_FaceHalfForward` を 0 にする（シェーダーが今の式へ戻る）。OFF に切り替えたときも 0 にする。頭の向きの計算は前髪の影と共有するため、`HairShadowRenderer` の `FaceForward` / `HeadTop` / バウンズの中心の計算を、両方から使える静的クラス（例: `HeadFrame`）へ移す。

## UI

「陰影」セクションの顔専用の行（顔が影を受ける・落とすの後）に、トグル「半面切り替えの影」と 4 つのスライダーを足す。顔タブにだけ出す（前髪の影の行と同じ `Shows` の扱い）。トグルが OFF の間、スライダーは無効表示にする（行は消さない）。

## README

陰影の表（顔専用の項目）に 5 項目を足す。「ON の間、顔の自分の影は光の角度ではなく、光の水平方向の角度で半面・全面を切り替える。落ち影・前髪の影・追加ライトは今どおり」と書く。

## テスト

- 設定: 既定値、`IsGroupOnly` で共通から継承しないこと、`CopyField` で 5 キーを写すこと、項目数、要素の無い XML を既定で読むこと、XML の往復
- `FaceHalfShadowParams`: 度→ラジアン、cm→m の境界、範囲外の丸め、NaN・無限大、全面角 < 半面角のそろえ
- 既存テストの項目数（`GroupSettingKeys.All.Length`）と `ChangeAll` を更新する

## 実機での確認（devbridge）

一時カメラで顔の正面から撮り、主光源を頭の水平方向に 0° → 180°（15° 刻み）で回した連続画像を作る。後片付けで光・カメラを元に戻す。

- 0°〜30° 影なし、30°〜60° で反対側の半面がフェードイン、60°〜120° 半面、120°〜150° で全面へフェード、150°〜180° 全面
- 左右を入れ替えても対称
- 仰角 90°（真上）で影なし
- OFF に戻すと今の見た目に戻る
- 2.0 向けバンドルは Unity 5.6 でコンパイルが通ること（`build-bundle.bat all` のログに `Shader error` が無い）

## 範囲外

- 境界を鼻や頬の形に沿わせる顔用の影マスク画像（SDF）
- 顔の法線を球状にならす処理
- 顔以外（肌・髪・衣装）への適用
- 首など肌グループとの境目の陰影の整合（横からの光で顎の下の形がずれうるのは許容する）
