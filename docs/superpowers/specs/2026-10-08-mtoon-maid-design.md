# COM3D25.MToonMaid.Plugin 設計

日付: 2026-10-08
対象: COM3D2.5 専用（Unity 2022.3.62、BepInEx）

## 目的

指定したメイドのトゥーンシェーダー（`CM3D2/Toony_Lighted` 系）を MToon10 ベースのシェーダーに置き換え、VRM 系の見た目で表示する。設定はメイド単位で永続化し、どのシーンに出てきても自動で適用する。SceneEditor（SE）が無くても単体で動く。

## 要件

| 項目 | 決定 |
|---|---|
| 適用範囲 | UI で ON にしたメイドだけ |
| 設定の保存 | メイドの GUID ごとに永続化。起動し直しても、どのシーンでも適用 |
| 調整の粒度 | メイド × 部位グループ（肌・顔・髪・衣装）ごとに 1 セット |
| 対象マテリアル | 不透明・Cutout の Toony 系だけ。`_Trans` / `_NoZ` 系（目・まつ毛・ハイライト等）はバニラのまま |
| UI | 独自の IMGUI ウィンドウ（ギアメニュー＋ホットキー） |
| 対象ゲーム | COM3D2.5 のみ。COM3D2 (2.0) では何もしない |

範囲外（v1）: MatCap・発光・UV アニメの調整、マテリアル個別の上書き、SE のタイムライン／シーンプリセットへの保存、半透明マテリアルの置き換え。

## 事前調査で確定していること

- MToon10（UniVRM `Packages/VRM10/MToon10`）の built-in RP 版は Unity 2022.3.62 でエラーなくバンドル化でき、実機（D3D11）で `isSupported=True`。メイドの不透明マテリアルを差し替えて、セル調陰影・ワールド幅アウトライン・SE 追加ライト（ForwardAdd）の陰影まで描画されることを確認した
- Unity 5.6 では MToon10 はコンパイルできない（`UnpackNormalWithScale` 不在、URP include、`UNITY_LIGHTING_COORDS`）。2.0 非対応の理由
- 無限色はシェーダーの機能ではない。`TBody.ChangeTex`（`TBody.cs:3378-3412`）がグレースケールの R8 RenderTexture を作り、`InfinityColorTextureCache.UpdateTexture` が色テーブル（`_MultiColTex`）付きのシステムマテリアルで Blit した ARGB32 の RenderTexture を `_MainTex` / `_ShadowTex` に `SetTexture` する。色変更時は同じ RenderTexture へ Blit し直す
- Toony 系のプロパティ（実機で取得）: `_Color` `_ShadowColor` `_MainTex` `_ToonRamp` `_ShadowTex` `_ShadowRateToon` `_RimColor` `_RimPower` `_RimShift` `_Shininess` `_OutlineColor` `_OutlineTex` `_OutlineToonRamp` `_OutlineWidth` `_OutlineWidthTex` `_MultiColTex` `_UseMulticolTex` `_CutoffTex`（髪は `_HiTex` `_HiRate` `_HiPow` が加わる）
- 前髪などは renderQueue に 980 のような特殊な値を持つ

## シェーダー（MToon10 のフォーク）

- UniVRM の `vrmc_materials_mtoon.shader` と関連 `.hlsl` を取り込み、シェーダー名を `COM3D25/MToon10` に変える（本家 `VRM10/MToon10` を読む他 MOD と衝突させない）
- URP 版（`vrmc_materials_mtoon_urp.shader`）は取り込まない
- プロパティ名は**意味が同じものだけ**ゲーム側に揃える

| ゲーム側 | MToon10 本家 | フォークでの扱い |
|---|---|---|
| `_ShadowTex` | `_ShadeTex` | `_ShadowTex` に改名する。ゲームが書き込んだテクスチャ（無限色を含む）をそのまま読むため、同期処理が要らない |
| `_MainTex` / `_Color` / `_RimColor` / `_OutlineColor` / `_Cutoff` / `_OutlineWidthTex` | 同名 | そのまま |
| `_ShadowColor` | `_ShadeColor` | 揃えない。ゲーム側は肌で茶色系 (0.39, 0.22, 0.10) が入っていて意味が違う |
| `_RimPower` / `_RimShift` | `_RimFresnelPower` / `_RimLift` | 名前は揃えない。値は変換時にマテリアルごとに引き継ぎ、グループの倍率・加算をかける（改訂 2026-10-10） |
| `_OutlineWidth` | 同名 | 名前はもともと同じ。単位が一致するかは実機で確認する（未確定事項） |

改名の対象は、シェーダーの `Properties`、`vrmc_materials_mtoon_input.hlsl` の宣言、`vrmc_materials_mtoon_lighting_mtoon.hlsl` の参照、C# 側のプロパティ名の対応表の 4 か所。

SE との関係: SE のマテリアル編集は名前で値を読み書きするため、同名のもの（`_Color` / `_RimColor` / `_OutlineColor`）は SE からも編集・キーフレーム化できる。意味の違うものを揃えないのは、SE の「リムパワー」等が別の意味で効くのを避けるためでもある。

ライセンス: UniVRM は MIT。リポジトリと配布物に UniVRM のライセンス表記を同梱する。

## リポジトリ構成

`W:\COM3D2_5\work\COM3D25.MToonMaid.Plugin`

- `COM3D25.CostumeFix.Plugin` と同じ構成（BepInEx、net471、C# 7.3、`debug.bat` / `release.bat` / `bump-version.bat`）
- `MTEUtils` を submodule で取り込み、`GearMenu` 等を再利用する
- `UnityProject2022/`: Unity 2022.3.62 のプロジェクト。フォークしたシェーダーを置き、`build-bundle.bat` で `mtoon_bundle` を作る。成果物は `source/` 側へコピーし、DLL に埋め込みリソースとして含める
- `source/COM3D25.MToonMaid.Plugin.Tests/`: ユニットテスト

## コンポーネント

| 名前 | 役割 | 依存先 |
|---|---|---|
| `MToonShaderProvider` | 埋め込みバンドルから `COM3D25/MToon10` を読む。読めない・`isSupported=false` ならプラグイン全体を無効化しログを出す | Unity のみ |
| `MToonValidator` | UniVRM `MToonValidator` の移植。`_AlphaMode` 等からキーワード・`_M_*` ブレンド設定・renderQueue を決める。判定部分は純粋な関数に切り出す | Unity のみ |
| `ToonMaterialClassifier` | シェーダー名で置き換え対象か判定し、スロット名から部位グループを決める | なし（純粋なロジック） |
| `MaterialConverter` | Toony → MToon の変換（値の引き継ぎ＋グループ設定の適用）と元に戻す処理。元の状態をマテリアルごとに控える | Validator、設定 |
| `MaidMToonController` | 1 メイド分の監視と再変換 | Converter、Classifier |
| `MaidSettingsStore` | GUID ごとの設定の読み書き | ファイル I/O |
| `MToonMaidWindow` | IMGUI ウィンドウ | Store、Controller |
| `MToonMaidPlugin` | エントリポイント。表示中メイドの Controller の出し入れ、ウィンドウの開閉 | すべて |

SE へのコンパイル時参照は持たない。

## 置き換え対象の判定

- 対象: シェーダー名が `CM3D2/Toony_Lighted` で始まり、`Trans` を含まないもの（`_Outline` / `_Outline_Tex` / `_Hair_Outline` / `_Hair_Outline_Tex` / `_Cutout_AtC` 等を含む）
- 除外: `_Trans` / `_Trans_NoZ` 系、`CM3D2/Lighted_Trans` など Toony 以外、NPRShader（`com3d2mod/`）や SE が変えたシェーダー
- 部位グループ（スロット名で判定）

| グループ | スロット |
|---|---|
| 肌 | `body` |
| 顔 | `head` |
| 髪 | `hairF` / `hairR` / `hairS` / `hairT` / `hairAho` など `hair` で始まるスロット |
| 衣装 | 上記以外 |

## 監視と変換の流れ（案 A：監視のみ）

フックは使わず、定期的な走査で状態をそろえる。作り直し直後に数フレームだけバニラの見た目が出るのは許容する。気になる場合の後付け候補として、`AllProcPropSeq` 完了時に再走査を促すトリガーがある（v1 では入れない）。

### 対象メイド

- 毎フレーム `CharacterMgr` の表示中メイドを見て、設定が ON の GUID のメイドにだけ Controller を付ける
- メイドが消えたら Controller を外す。マテリアルはゲームが破棄するので戻し処理はしない

### 走査

- タイミング: busy から非 busy へ変わった瞬間と、非 busy の間の一定間隔（既定 10 フレーム。Config で変更可）
- `goSlot` の全 `TBodySkin`（2.5 は 2 次元）のレンダラーを見る。**`sharedMaterials` だけを使う**（`materials` は複製を作る）
- マテリアルのインスタンス ID をキーに、元のシェーダー・renderQueue・キーワードを控えるキャッシュを持つ

| マテリアルの状態 | 処理 |
|---|---|
| 置き換え対象の Toony で、キャッシュに無い | 控えてから変換する |
| `COM3D25/MToon10` で、キャッシュにある | 何もしない |
| Toony だが、キャッシュにある（menu の `shader` コマンドで戻された） | 控えは残したまま再変換する。シェーダー・renderQueue・キーワードはゲームの新しい値を控え直す |
| `COM3D25/MToon10` だが、キャッシュに無い（`renderer.materials` による複製など） | 触らない。OFF で戻せないため警告ログを 1 回出す |
| それ以外 | 触らない |

Controller は作成時の GUID を持つ。毎 Tick で現在の GUID・`enabled` と照合し、ストック Maid の使い回しで別キャラが入ったか、OFF にされたら元へ戻して外す。

破棄されたマテリアル（null）は走査のついでにキャッシュから外す。

### 変換時の値

- `_MainTex` / `_Color` / `_ShadowTex` / `_OutlineColor` / `_Cutoff` は同名なのでそのまま使う
- `_ShadowTex` を持たないマテリアルは `_ShadowTex` に `_MainTex` を入れる
- シェーダー名に `Outline` を含めばアウトライン ON（ワールド幅、`_MTOON_OUTLINE_WORLD`）
- `Cutout` を含めば `_AlphaMode=1`
- 残り（影色・境界・GI・リム・アウトライン幅等）は部位グループの設定から入れる
- Validator 実行後、**renderQueue は元の値へ戻す**（前髪の 980 等の描画順を保つ）

### 設定変更時

該当メイドの変換済みマテリアルのうち、該当グループのものへ値を入れ直す（走査を待たない）。

### OFF 時

キャッシュにある全マテリアルを元のシェーダー・renderQueue・キーワードへ戻し、キャッシュを空にする。同名で上書きした `_OutlineWidth` / `_RimColor` / `_OutlineColor` も変換前の値へ戻す。変換後に他者（SE の NPR 化、ゲームの Toony への巻き戻し）がシェーダーを変えていたマテリアルは、その選択を優先して戻さない。

## 設定の保存

- 保存先: `BepInEx/config/COM3D25.MToonMaid/<GUID>.xml`（1 メイド 1 ファイル、`XmlSerializer`）
- 書き込み: 値の変更から約 1 秒後にまとめて書く
- 読み込み: メイドが初めて表示されたときに 1 回。ファイルが無いメイドは OFF
- XML にバージョン属性を持たせ、欠けた項目は既定値で読む

1 メイド分の中身:

- `enabled`
- グループ（肌・顔・髪・衣装）ごとに:
  - 陰影: `_ShadeColor`、`_ShadeStrength`（改訂 2026-10-10 (6)）、`_ShadingShiftFactor`、`_ShadingToonyFactor`、`_GiEqualization`
  - リム: 色の上書き有無と `_RimColor`、鋭さの倍率（`_RimPower` へ掛ける）、持ち上げの加算（`_RimShift` へ足す）、`_RimLightingMix`
  - アウトライン: ON/OFF、`_OutlineWidth`、`_OutlineLightingMix`、色の上書き有無と色（上書きしなければゲームの線の色を引き継ぐ。改訂 2026-10-10 (3)）

既定値はグループごとにコードで持つ。肌は `_ShadeColor` をやや赤みのある色、他は白。

## UI

- ギアメニューのアイコン（MTEUtils `GearMenu`）と、BepInEx Config で変えられるホットキーで開閉
- 上から: 対象メイドの選択（表示中のメイド）／「MToon にする」チェック／グループのタブ（肌・顔・髪・衣装）／選んだグループのスライダーと色の欄／「このグループを既定に戻す」
- 値の変更は即座に反映する

## エラー処理

| 状況 | 振る舞い |
|---|---|
| シェーダーが読めない（バンドル破損、`isSupported=false`） | 起動時にエラーログを 1 回出し、Controller を作らない。ウィンドウに「シェーダーを読み込めませんでした」と表示。描画には手を出さない |
| 変換中の例外 | 半端に変わったマテリアルを元へ戻してから、そのマテリアルだけ飛ばす。マテリアル名付きで警告ログを出し、失敗した ID を覚えて以後は飛ばす |
| URP が有効 | MToon10 の built-in 版は描画されないため、ログを出して何もしない。ウィンドウに理由を表示 |
| 設定ファイルが壊れている | 警告ログを出してそのメイドは OFF。壊れたファイルは上書きせず `.bak` へ退避 |
| COM3D2 (2.0) で起動された | 起動時に判定し、ログを出して何もしない。ただし 2.0 には `UnityEngine.CoreModule` が無く、判定より前に BepInEx のロードエラーになりうる（許容） |

## テスト

### ユニットテスト（Unity のネイティブ呼び出し不要の部分）

- `ToonMaterialClassifier`: シェーダー名から対象判定（`_Trans` / `_NoZ` / NPR の除外）、スロット名から部位グループ
- `MaidSettingsStore`: XML の往復、欠けた項目を既定値で補う、壊れたファイルの扱い
- `MToonValidator` の判定部分: AlphaMode 等からキーワードの組と renderQueue

### 実機検証（devbridge）

- ON で不透明・Cutout だけが変換され、`_Trans` は残る
- 無限色で髪の色を変えても MToon のまま色が追従する（`_ShadowTex` 改名の効果）
- 着替え・menu の再処理の後に再変換される
- OFF で元のシェーダー・renderQueue・キーワードに戻る
- `_OutlineWidth` の単位を確認し、既定値を決める
- SE で NPR へ変えたマテリアルには触らない

## 未確定事項

- `_OutlineWidth` の単位がゲームと MToon（ワールド幅、m）で一致するか。2026-10-08 の実機確認（デイリー画面、メイド 1 人）では、ゲーム側の値は全マテリアル 0.0008。MToon の 0.0008 と 0.0015 はどちらも同じ構図で 1〜2px の細い線になり、見た目の差はわずかだった。バニラの線は線の色の影響でさらに目立たない。厳密な単位の一致は確かめられていないため、既定値 0.0015・UI の上限 0.01 を暫定で維持する
- ゲームの `_OutlineWidthTex` のチャンネル構成（MToon は G を読む）。確認したメイドではどのマテリアルにも `_OutlineWidthTex` が設定されておらず、判断できなかった。グレースケールなら問題なし
- プラグイン名 `COM3D25.MToonMaid.Plugin` は仮称

## 実機検証で分かったこと

- `AssetBundle.LoadAllAssets` を BepInEx の `Awake`（Chainloader 実行中）で呼ぶと、ネイティブでクラッシュする。シェーダーの読み込みは最初の `Update` で行う
- ゲームがシェーダーだけを Toony へ戻しても、MToon が付けたキーワードはマテリアルに残る。再変換で控え直すときは、MToon が付けたキーワードを除く
- OFF にした後、元のシェーダーに無い `_OutlineColor` 等は、マテリアルの保存プロパティとして残る（Unity には消す API が無い）。Toony 側は参照しないため、描画には影響しない

## 改訂 2026-10-10: ゲームのリムの鋭さ・持ち上げを引き継ぐ

### 背景

ゲームの Toony はマテリアルごとに `_RimPower`（顔 20〜30、体 25 など）と `_RimShift` を持つ。これまでは「範囲も式も違う」として揃えず、部位グループ共通の鋭さ 5・持ち上げ 0 で上書きしていたため、部位ごとのリムの細さが失われていた。

### 実機で確認した式

正射影カメラを遠方（距離 500）に置いて球を描き、リム色の白と黒の差を取って比べた。ゲームの Toony のリムは MToon と同じ `pow(1 - N·V + _RimShift, _RimPower) * _RimColor` だった（`_RimPower` 1 / 2 / 5 / 25、`_RimShift` ±0.2〜0.5 で、Toony は MToon の約 0.96 倍）。違いは次の 2 点だけで、引き継ぎには影響しない。

- Toony は底を saturate しない。`_RimShift` が負のとき `1 - N·V + _RimShift` が負になる正面側に不定な値が出る。MToon は saturate するため出ない
- 近いカメラでは視線の向きの取り方の差で形が変わって見えるが、ゲームのカメラ（透視投影）では一致する

よって `_RimPower` → `_RimFresnelPower`、`_RimShift` → `_RimLift` はそのまま写せる。上の表の「揃えない。範囲も式も違う」はこの改訂で置き換える（プロパティ名は揃えない。SE の「リムパワー」は MToon に無いプロパティを書くため効かないのは従来どおり）。

### 仕様

- 変換時（`Convert`）と再変換時（`Reconvert`）に、マテリアルの `_RimPower` / `_RimShift` を `MaterialBackup` に控える。プロパティが無ければ 5 / 0（従来の既定）
- 描画に入れる値: `_RimFresnelPower = max(0, 控えの _RimPower × 鋭さの倍率)`、`_RimLift = 控えの _RimShift + 持ち上げの加算`
- グループ設定の `rimFresnelPower` / `rimLift`（絶対値）を廃止し、`rimFresnelPowerScale`（既定 1）/ `rimLiftOffset`（既定 0）に置き換える。共通タブからの継承キーも `rimFresnelPowerScale` / `rimLiftOffset` に変える
- `_RimPower` / `_RimShift` は MToon が書かないため、元へ戻すときに書き戻さない
- MToon にしている間にゲームが `_RimPower` を変えても反映しない（MToon にそのプロパティが無い）。Toony へ戻されて再変換したときに控え直す
- 設定ファイルの版は上げない。旧ファイルの `<rimFresnelPower>` / `<rimLift>` 要素は読み飛ばし、`overrides` の旧キーは `FillMissing` が取り除く。旧ファイルで鋭さ・持ち上げを手で変えていた値は失われ、倍率 1・加算 0（ゲームの値どおり）になる
- UI: リムの「鋭さ」「持ち上げ」を「鋭さの倍率」（0〜4）「持ち上げの加算」（-1〜1）に変える

## 改訂 2026-10-10 (2): リムの明るさを Toony に合わせる

### 背景

MToon にした髪で、影側のリムがゲームより明るかった。本家 MToon の主光源のパスは影の係数が常に 1 で、リムの明るさは `リム色 × (ライト色 + 環境光)` になり、影側にも光側と同じ明るさのリムが出る。

### 実機で確認した Toony のリムの明るさ

白テクスチャの球を専用の平行光（白、強さ 0.5〜2、色付き）と一様な環境光で照らし、リム色の白と黒の差を取った。

- 主光源: `リム色 × リムの式 × (環境光 + ライト色 × 陰影 × 0.5)`。陰影はテクスチャを白にしたときの下地（影側 0.396、光側 0.576）で、リム ÷ (リムの式 × 陰影) は影側・光側とも 0.507。ライトの強さと色に比例し、`_RimColor` のアルファには依らない
- 環境光の分は MToon と同じ（環境光だけで 0.137 と 0.136）
- 追加ライト（ポイントライト）: `リム色 × リムの式 × ライト色 × 減衰 × 陰影`（係数 1.0）

直す前の MToon は、白いライト 1 本で影側 5 倍・光側 3.4 倍明るかった。

### 仕様

- 主光源のパスのリムの明るさを `lerp(1, ライト色 × lerp(_ShadeColor, _ToonRampLitColor, shade) × 0.5 + 環境光, _RimLightingMix)` にする。`shade` は前髪の影を含む主光源の陰影
- 追加ライトのパスは変えない（本家のままで、陰影 `lerp(_AdditionalLightShade, 1, shade)` が掛かる近い形）
- `_ShadeColor` は設定の影色 × ランプの暗い色なので、影色を白以外にするとリムの影側もその色に染まる（下地と揃う。Toony には相当する設定が無い）
- キャストシャドウ（他のメッシュの影）の中のリムは、Toony と比べていない。MToon では `shade` に影マップの減衰が入るため、影の中のリムは影側の陰影まで暗くなる
- 「ライト色の影響」（`rimLightingMix`）の既定を 0.5 から 1 にする。上の式は 1 のときに Toony と一致し、0.5 では半分がライトに依らないリムになる（実機の髪で、ゲームの約 1.9 倍明るかった）。設定ファイルの版は上げない。保存済みの設定ファイルに書かれた値はそのまま使う
- 直した後の実測で、影側・光側・環境光あり・色付きのライトのどれもゲームと 1〜2% 以内で一致した

## 改訂 2026-10-10 (3): ゲームの線の色を引き継ぐ

### 背景

MToon にすると、ゲームの輪郭線の色が反映されなかった。プラグインはゲームの `_OutlineColor` を引き継いでいたが、肌・顔・髪・バニラ衣装の多くが使う `*_Outline_Tex` 系のシェーダーは `_OutlineColor` を使っていない。そのため衣装の線は黒に、髪の線は使われていない灰色になり、無限色で変えた輪郭色も届かなかった。

### 実機で確認したゲームの線の色

テスト用の球にゲームのシェーダーを付け、`_OutlineWidth` 0.02 で輪郭部分のピクセルを読んだ。

| シェーダー | 変えたもの | 線の色 |
|---|---|---|
| `CM3D2/Toony_Lighted_Outline_Tex` | `_OutlineColor` を赤・白にする | 変わらない |
| 同上 | `_OutlineTex` を白にする | 白（247〜250） |
| 同上 | `_OutlineTex` を外す（null） | ほぼ白（236〜242） |
| 同上 | `_OutlineToonRamp` を白にする | ほぼ変わらない |
| `CM3D2/Toony_Lighted_Outline` | `_OutlineColor` を赤にする | 赤（247, 0, 0） |

- `*_Outline_Tex` 系では、`_OutlineTex` を UV で読んだ色が線の色になる。衣装は `*_line` テクスチャ、肌・顔・髪はゲームが無限色（`SKIN_OUTLINE` / `HAIR_OUTLINE`）を書き込む RenderTexture（`Menu.ProcScript`）
- Tex なしの `*_Outline` 系では、`_OutlineColor` が線の色になる
- どちらもライトの影響を受けない（白を入れると白がそのまま出る）

### 仕様

- シェーダーに `_OutlineTex`（既定 white）と `_UseOutlineTex`（0 / 1）を足す。輪郭パスの色を `lerp(_OutlineColor.rgb, _OutlineTex.rgb(uv), _UseOutlineTex) × lerp(1, 陰影後の色, _OutlineLightingMix)` にする。`_OutlineTex` はゲームと同名なので、シェーダーを差し替えても値が残り、無限色の RenderTexture にもそのまま追従する
- シェーダー名に `Outline_Tex` を含むマテリアルは、線の色をテクスチャから取る（`_UseOutlineTex` 1）。含まないものは従来どおりゲームの `_OutlineColor`（`_UseOutlineTex` 0）。どちらも `_OutlineColor` にはゲームの値（控え）を入れる。白にしてテクスチャへ掛ける形にすると、SE の NPR 化など他者がシェーダーを差し替えたとき、`_OutlineColor` で線を描くシェーダーに白い線が残る
- 「線の色を上書きする」が ON なら、どちらのシェーダーでも指定色だけを使う（`_UseOutlineTex` 0）
- 再変換（`Reconvert`）でシェーダー名から判定し直す。`_OutlineColor` の控えは従来どおり読み直さない（MToon が書いた白や上書き色を控えてしまうため）。ゲームが Tex 系 ⇔ Tex なしへシェーダーを変えたとき控えが古い値のままになるのは許容する
- Tex 系では、ゲームと同じく `_OutlineColor` は線の色に効かない。SE などが `_OutlineColor` を書き換えても Tex 系の線は変わらない
- 元へ戻すときは従来どおり `_OutlineColor` を控えの値へ戻す。`_OutlineTex` はプラグインが書かないため戻さない。`_UseOutlineTex` は Toony に無いため残っても描画に影響しない
- 「ライト色の影響」（`outlineLightingMix`）の既定を 1 から 0.5 にする。ゲームの線はライトの影響を受けないが、MToon らしく少し陰影を残す。設定ファイルの版は上げない。保存済みの設定ファイルに書かれた値はそのまま使う

## 改訂 2026-10-10 (4): ゲームの影の境界（`_ShadowRateToon`）を使う

### 背景

MToon の主光源の陰影は、グループ共通の「境界の位置」「境界の硬さ」で決まり、既定（-0.05 / 0.95）では境界が N·L ≈ +0.05 にある。ゲームの Toony は部位ごとのテクスチャ `_ShadowRateToon` で境界を決めていて、顔は N·L ≈ -0.58 まで明るい。そのため MToon にすると、特に顔の影がゲームより広くなる。

### 実機で確認した Toony の陰影

テスト用の球にゲームのシェーダーを付け、主光源だけ（Gamma 空間）で N·L ごとの明るさを読んだ。

- `u = N·L × 0.5 + 0.5`、`出力 = lerp(_ShadowTex, _MainTex × _Color, _ShadowRateToon.r) × _ToonRamp × ライト色`。`_ShadowRateToon` は R だけを使う。`_ShadowColor` は自己陰影に効かない
- 通常の Toony は `_ShadowRateToon` とランプを `(u, u)`（対角線）で読む。Muscle（`CM3D2/Toony_Lighted_Muscle_Outline(_Tex)`）は `(u, 0.75)`
- 追加ライト（ForwardAdd）は `_ShadowRateToon` もランプも使わず平らに照らす

| 部位 | テクスチャ | 境界（割合 0.5）の N·L | 移り変わる範囲 |
|---|---|---|---|
| 顔 | `toonFace_shadow` | 約 -0.58 | -0.75〜-0.45 |
| 髪・衣装 | `toonDress_shadow` | 約 -0.33 | -0.5〜-0.2 |
| 肌 | `ToonSkin_Shadow` | 約 -0.14 | -0.3〜0 |

`Menu.cs` の `toonSkin_shadow_*`（肌の種類。muscle の body だけ）は下半分（v<0.5）だけが違い、Muscle は v=0.75 しか読まないため、主光源の陰影ではどれも同じ形になる。

### 仕様

- シェーダーに `_ShadowRateToon`（既定 white。ゲームと同名で、シェーダーを差し替えても値と無限色の RenderTexture が引き継がれる）、`_UseShadowRateToon`（0 / 1）、`_ShadowRateToonV`（負なら u と同じ v で読む）、`_ShadowRateShift` を足す
- `_UseShadowRateToon` が 1 のとき、ForwardBase の陰影を `_ShadowRateToon((clamp(N·L, -1, 1) + 凹凸のマスクのずれ + _ShadowRateShift) × 0.5 + 0.5, v).r` にする。凹凸のマスクのずれは `_ShadingShiftTex.r × _ShadingShiftTexScale`（パラメーターマップが ON のときだけ）。`_ShadingShiftFactor` と `_ShadingToonyFactor` は使わない。影マップの減衰と前髪の影はこれまでどおり掛ける。座標はテクセルの中心の範囲へ収め、Repeat のテクスチャでも端で反対側を拾わない
- 追加ライト（ForwardAdd）の陰影はこれまでどおり「境界の位置」「境界の硬さ」で決める（Toony は追加ライトに `_ShadowRateToon` を使わない）
- グループ設定に「ゲームの影の境界」（`useGameShadowRate`、既定 ON）と「境界のずらし」（`shadowRateShift`、既定 0、-1〜1。N·L へ足す）を足す。共通からの継承の単位はそれぞれ 1 つ。ON でもマテリアルの `_ShadowRateToon` が null なら従来の式で描く。ずらしは OFF のとき無効表示にする。ON の間は「境界の位置・硬さは追加ライトにだけ効く」旨の注意書きを 1 行出す（表示名に足すとラベル幅に収まらない）。MToon 中にゲームが `_ShadowRateToon` を null ⇔ 非 null へ差し替えたら、次の走査でフラグを決め直す
- Muscle（シェーダー名に `Muscle` を含む）は v=0.75、それ以外は対角線で読む。変換・再変換でシェーダー名から決めて控える
- プラグインは `_ShadowRateToon` を書かないため、元へ戻す処理は無い。`_UseShadowRateToon` などは Toony に無く、残っても描画に影響しない
- 設定ファイルの版は上げない。保存済みの設定は要素が無いため ON・ずらし 0 で読まれ、見た目が変わる（ゲームに近づく）
- `_ToonRamp` はこれまでどおり光側と影側の色だけを使う（境界の帯の色までは再現しない）
- 髪の Toony（`CM3D2/Toony_Lighted_Hair_Outline_Tex`）は `_ShadowRateToon` とは別に、N·L < -0.3 の裏側をさらに暗くする（`_ShadowRateToon` を白にしても N·L -0.6 で 0.5 倍、-0.7 で 0）。この項は再現しない

## 改訂 2026-10-10 (5): ゲームのランプ（`_ToonRamp`）を読む

### 背景

MToon はランプから光側（右端）と影側（最も暗い画素）の 2 色だけを取り、`_ToonRampLitColor` と `_ShadeColor` に掛けていた。肌のランプが境界付近に持つ赤い帯など、途中の色は出ない。また光側の色を環境光の分にも掛けていて、ランプの光側が暗い髪（黒髪で 0.58）では環境光の分がゲームの 6 割ほどになっていた。

### 実機で確認した Toony のランプ

試験用の球（主光源・環境光 Flat 0.5・ポイントライトを個別に）で測った。

- 主光源: `lerp(_ShadowTex, _MainTex × _Color, _ShadowRateToon.r) × _ToonRamp × ライト色`。ランプは `_ShadowRateToon` と同じ座標（通常 `(u, u)`、Muscle `(u, 0.75)`、`u = N·L × 0.5 + 0.5`）で読む
- 環境光: `_MainTex × 環境光` を足す。ランプ・`_ShadowRateToon`・`_ShadowTex`・`_ShadowColor` は効かない
- 追加ライト: `_MainTex × ライト色 × 減衰`（向きによらず平ら）。ランプ・`_ShadowRateToon`・`_ShadowTex` は効かない

### 仕様

- シェーダーで `_ToonRamp` を宣言して読む（専用のサンプラーは作らず `_ShadowRateToon` のサンプラーを使う。座標は自前で半テクセルの範囲へ収めるため、テクスチャ側の Repeat / Clamp に依らない）。`_UseToonRamp`（0 / 1）と `_ToonRampDarkColor` を足す
- `_UseToonRamp` が 1 のとき、ForwardBase の主光源の分（陰影後の色・ハイライト込み）とリムの主光源の分にランプの色を掛ける。座標は `_ShadowRateToon` と同じ（凹凸のマスクのずれと「境界のずらし」を含む）。落ち影（影マップの減衰。アウトラインのパスでは見ない）と前髪の影の濃さの分だけ、`_ShadowRateToon` で光側の所に限ってランプの色を `_ToonRampDarkColor`（ランプの最も暗い画素）へ寄せる。追加ライトのパスには掛けない
- 環境光の分は、ランプの有無・トグルによらず `_MainTex × _Color × 環境光` にする（光側の色を掛けない）。OFF でも直る
- C# は「ゲームの影の境界」（`useGameShadowRate`）が ON でマテリアルにランプと `_ShadowRateToon` の両方があるとき `_UseToonRamp` を 1 にし（`_ShadowRateToon` の無いマテリアルでランプだけを読むと、従来の境界の陰影にランプの暗さが重なるため）、`_ToonRampLitColor` を白、`_ShadeColor` を設定の影色だけにする（2 色の近似を外す。掛けると二重になる）。OFF、またはランプが無いときは従来どおり 2 色を掛ける。`_ToonRampDarkColor` には常にランプの暗い色を入れる
- UI の表示名を「ゲームの影の境界」から「ゲームの陰影」に変える。設定のキー（`gameShadowRate` / `useGameShadowRate`）は変えない
- 追加ライトの段差（`_AdditionalLightShade`）の仕組みは変えない。ON の間は影側も光側もランプの色が掛からない（ゲームと同じくランプの影響を受けない）
- アウトラインのパスも ForwardBase なので、線に混ぜる陰影（`_OutlineLightingMix`）にもランプが掛かり、環境光の変更も及ぶ
- 落ち影（影マップ）の中のランプの扱いはゲームと比べていない。N·L の座標のままだと光の当たる向きの面の落ち影でランプの光側の色が掛かり、OFF（影側の色）より明るく色も抜けるため、前髪の影と同じく影側の色へ寄せる。影の強さ 1 で完全に落ち影の中なら、色は OFF と同じ `_ShadowTex × 影色 × ランプの暗い色` になる。影の強さが 1 未満（ゲームの主光源の既定は 0.098）では、その分だけランプの色が残る。`_ShadowRateToon` の影側は寄せない（自分の影が落ちる裏側でもランプの境界の帯を残す）。減衰が中間の所（影の縁）は OFF よりわずかに暗い

## 改訂 2026-10-10 (6): 陰影の濃さ

### 背景

影色は既定が白で、暗さはランプから入るため、影色ではランプより薄くできなかった。陰影全体をまとめて薄くする項目が無かった。

### 仕様

- グループ設定に `shadeStrength`（0〜2、既定 1、継承の単位キー `shadeStrength`）を足し、`_ShadeStrength` に書く。手編集の範囲外は 0〜2 へ丸め、非有限は 1 にする
- ForwardBase（主光源とアウトラインのパス）で、陰影の割合（`_ShadowRateToon` または境界の位置・硬さ × 落ち影 × (1 - 前髪の影)）を `lerp(1, 割合, 濃さ)`、ランプの色を `lerp(ランプの光側の端, ランプの色, 濃さ)` にする。光側の端は u = 1（N·L = 1、Muscle は v = 0.75 のまま）で読む。落ち影（影マップの減衰）は周囲の物の影も含めて薄くなる。リムも同じ値を使う
- 1 より上では陰影の割合は変えず（外挿すると影が濃くならずに境界が光側へ広がる）、影側の色（`_ShadowTex × _ShadeColor`、リムの `_ShadeColor`）を `lerp(光側の色, 影側の色, 濃さ)` で、ランプの色を同じ式で外挿する。負にならないよう 0 で止める。髪のハイライト（天使の輪）は外挿せずに影側へ足す（外挿すると影色が光側の半分より暗いとき影の中で輪が暗く反転する）
- ForwardAdd（追加ライト）・環境光・エミッションには効かない
- 濃さ 1 で従来と同じ見た目

## 改訂 2026-10-11: アウトラインの幅を画面基準にできるようにする

### 背景

アウトラインはワールド幅（m）だけで、カメラから離れるほど線が細く、寄るほど太くなった。MToon10 には画面基準の幅（`_MTOON_OUTLINE_SCREEN`）があり、フォークのシェーダーも `multi_compile` でこの変種をバンドルに含めているが、プラグインがワールド幅に固定していた。

### MToon10 の画面基準の幅（ストリクトモードなし）

- 幅は画面の高さに対する割合。0.002 で高さの 0.2%（1080p で約 2px）
- 視錐台の高さが 2m になる距離（縦 FOV 35° で約 3.2m）までは距離によらず同じ太さ。それより遠くはワールド幅と同じく距離に反比例して細くなる
- カメラを向く法線の頂点ほど細くする（`1 - normalVS.z²`）
- メイドのスケールは効かない

### 仕様

- グループ設定に幅の基準 `outlineWidthMode`（`OutlineWidthMode.World` / `Screen`、名前で保存、既定 World、継承の単位キー `outlineWidthMode`）と画面基準の幅 `outlineScreenWidth`（既定 0.002）を足す
- 単位が違うため幅は基準ごとに別の値を持つ。`outlineWidth`（m）と `outlineScreenWidth` はまとめて継承の単位キー `outlineWidth` にする。基準を切り替えても他方の値は残る
- 線を描くとき、基準が World なら `_OutlineWidthMode` 1・`_MTOON_OUTLINE_WORLD`・`_OutlineWidth` に `outlineWidth`、Screen なら `_OutlineWidthMode` 2・`_MTOON_OUTLINE_SCREEN`・`_OutlineWidth` に `outlineScreenWidth` を書く。線を描かないときは従来どおり 0 で、どちらのキーワードも外す
- 要素の無い旧ファイルは World・0.002 で読み、個別にしない。設定ファイルの版は上げない
- UI はアウトライン節の「幅」の前に「幅の基準」（[ワールド][画面]）を置き、「幅」のスライダーは今の基準の値を出す（範囲 0〜0.01）
- シェーダーは変えない（バンドルの作り直しは不要）

## 改訂 2026-10-11 (2): リムの明るさの係数 0.5 をやめる

### 背景

「ライト色の影響」を 1 にすると服のリムがゲームより薄く、0 にすると髪のリムが明るすぎた。実シーンの照明でメイドのマテリアルを比べると、1 では髪・服ともゲームの約 0.53 倍だった。

### 実機で確認した Toony のリムの明るさ

単色テクスチャ（`_ShadowRateToon`・`_ShadowTex`・`_ToonRamp` を 0〜1 で振る）の球を、ゲームのシェーダーと同じ透視カメラで撮った。

- 主光源: `リム色 × リムの式 × (ライト色 × SRT × ランプ + 環境光)`。SRT が 1 のとき係数は 1.0。SRT が 0 ならリムは 0 で、`_ShadowTex`・`_ShadowColor`・`_MainTex`・`_Color` は効かない。環境光の項に SRT とランプは掛からない
- 改訂 2026-10-10 (2) の 0.5 は、`_ShadowTex` を白にした下地を陰影とみなしたため、SRT の効きを係数と取り違えていた
- `Toony_Lighted_Hair_Outline_Tex` も同じ式。N·L が約 -0.3 より裏側では、環境光・リムを含めて画素全体が 0 になる（MToon では未再現）

### 仕様

- 主光源のパスのリムの明るさを `lerp(1, ライト色 × shade × _ToonRampLitColor × ランプ + 環境光, _RimLightingMix)` にする。影側の `_ShadeColor` と係数 0.5 は使わない。陰影の経路（SRT・MToon の陰影・PBR 補正）によらず、影側のリムは 0 になる
- 直した後の実測で、メイドの髪 2 種・服を前・横・後ろから撮り、ゲームと 1〜2% 以内（ピークは一致）だった
- 既定の `rimLightingMix` は 1 のまま。設定ファイルの版は上げない
