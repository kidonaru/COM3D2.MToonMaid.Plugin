# 顔の影を受ける／落とすを SceneEditor から MToonMaid へ移す 設計

日付: 2026-10-10
対象: COM3D25.MToonMaid.Plugin（追加）、COM3D2.SceneEditor.Plugin（削除）

## 背景

SceneEditor（以下 SE）には、メイドの顔（head スロット）の Renderer の「影を受ける」（`receiveShadows`）と「影を落とす」（`shadowCastingMode`）を上書きする機能がある。上書きは表情ウィンドウの「影」タブ、表情レイヤーのキー（`TransformType.FaceShadow`）、シーンプリセット v42 の `faceShadow` で扱う。

顔の影はシーンの演出というより、メイドの見た目の設定である。そのため、メイドごとに見た目を持つ MToonMaid へ移す。タイムラインのキーやシーンプリセットでの切り替えはやめる。機能はまだリリースしていない（SE の CHANGELOG に記載なし）。

## MToonMaid（追加）

### 設定

- `GroupSettings` に次の 2 項目を足す。既定値は SE の既定（＝ゲームの顔の値）と同じにする
  - `receiveShadows`（bool、既定 true）: 影を受ける
  - `castShadows`（bool、既定 false）: 影を落とす（true なら `ShadowCastingMode.On`、false なら `Off`）
- 継承の単位（`GroupSettingKeys`）に `receiveShadows` / `castShadows` を足し、`All` と `CopyField` にも入れる。効くのは顔のグループだけで、「凹凸の影を消す」（`faceBumpLift`）と同じ扱いにする。共通タブで変えると、個別にしていない顔に効く
- 設定ファイルの版（`MaidSettings.CurrentVersion = 3`）は上げない。要素の無いファイルはフィールドの初期値（受ける ON／落とす OFF）で読む

### 適用

- 適用の条件は「そのメイドで MToonMaid が有効」かつ「head スロットが有効」（`MaidSettings.IsSlotActive("head")`）。顔グループの無効化と除外スロットもこれに含まれる
- 条件を満たす間は、`MaidMToonController.Scan` のたびに head スロットの GameObject 配下の全 Renderer（非アクティブを含む）へ、解決した顔グループの値を書く。書くのは今の値と違うときだけにする
- Renderer ごとに「元の値」と「前回書いた値」を控える（SE の `MaidFaceRendererManager.RendererState` と同じ考え方）
  - 元の値は、その Renderer へ最初に書く前の値
  - 控えのある Renderer は、他者が値を変えても次の走査で設定値に書き直す。着替えでゲームが値を戻しても設定が保たれる。設定がゲームの値と同じでも同じ規則で書く
- 戻すとき（条件を満たさなくなったとき、`RestoreAll`、コントローラ破棄）は、今の値が前回書いた値のままなら元の値へ戻す。他者が書き換えていればその値を残す（SE の `FaceRendererRules.ResolveRestore`）。戻した後は控えを消す
- 破棄された Renderer の控えは走査で捨てる。着替えで head が作り直されると新しい Renderer になり、元の値から控え直す
- 顔グループの設定を変えたとき（`ApplyGroup(PartGroup.Face, …)`）は、走査を待たずにすぐ書く
- 値の決め方（書く値・書くか・戻す値）は純粋関数のクラス（例: `RendererShadowRules`）に切り出し、Renderer の控えと書き込みは別クラス（例: `FaceShadowApplier`、メイド 1 人に 1 つ）に置く。`MaidMToonController` は Scan・ApplyGroup・RestoreAll からそれを呼ぶだけにする

### UI

- 「陰影」セクションの顔専用の項目（凹凸の影を消す・前髪の影）の並びに、トグル「顔が影を受ける」「顔が影を落とす」を足す（全タブで同じ行に出ても顔にだけ効くと分かるよう「顔が」を付ける）
- 表示は顔専用の既存項目にそろえる。全タブで同じ位置に出し、肌・髪・衣装タブでは個別のトグルを含めて行ごと無効表示にする。共通タブと顔タブでは有効

### テスト

- `RendererShadowRules`: 書く値、書くかの判定、戻す値（前回書いた値のまま／他者が変えた）
- 設定: 既定値、`CopyField` による 2 キーの継承、要素の無い XML を既定値で読むこと、XML の往復

### README

README に「顔が影を受ける」「顔が影を落とす」（顔だけに効く）を追記する。

## SceneEditor（削除）

### 丸ごと削除

- `MaidManipulation\FaceShadowItems.cs`、`FaceShadowRowDrawer.cs`、`Timeline\TransformData\TransformDataFaceShadow.cs`、`Manager\History\FaceShadowSnapshot.cs`
- `TransformType.FaceShadow`（enum から完全に削除する）と `TimelineIntegration` での登録
- `HistoryScope.FaceShadow` と `SnapshotFactory` の分岐
- 表情ウィンドウ（`MaidFaceWindow`）の「影」タブと `DrawShadowContent`
- `MorphItemInspector` の顔の影の分岐
- `MorphTimelineLayer` の顔の影の処理（ボーン選択・メニュー項目・キーの取得と適用・モーション外での適用・レイヤー削除時の上書き解除）
- シーンプリセットの `ScenePresetFaceShadowItem` / `ScenePresetFaceShadow`、`ScenePresetMaid.faceShadow`、`ScenePresetManager` での取得と適用（`ApplyFaceShadow`）
- テスト: `FaceShadowLayerTests`、`ScenePresetFaceShadowTests`、`LaneColorInfoTests` の `TransformDataFaceShadow` のケース、ほかのテストにある顔の影への言及
- csproj の該当エントリ

### 一部だけ残す

- `MaidFaceRendererManager`: 顔の影の上書き（overrides・API・`receiveShadows` / `shadowCastingMode` の書き込みと復元・`_castChangedMaids`・`ConsumeCastChanged`）を外す。照射対象が「顔のみ／体のみ」のライトがある間に head を Face レイヤーへ移す処理と、ライトの `cullingMask` の補正は残す
- `FaceRendererRules`: `ResolveReceive` / `ResolveCast` を外し、`IsDefault` はレイヤー移動の有無だけで判定する。`ShouldAdoptExternal` の `overridden` 引数は、レイヤーで常に false になるなら外す
- `FaceRendererRulesTests`: レイヤーとライト補正のテストだけを残す
- `CharacterShadowProxyManager`: `ConsumeCastChanged` の呼び出しを外す。MToonMaid が書いた `shadowCastingMode` の変化は、既存の定期チェック（`SourceCheckInterval` ごとの `HasSourceChanged` → `CharacterShadowProxyRules.ShouldProxy`）で拾われ、複製が作り直される。数フレームの遅れは許容する

### 互換

- `<Type>FaceShadow</Type>` を含むタイムライン XML は読めなくなる（未リリースのため許容する）。手元の `PhotoModeData\_Timeline\仮装狂騒曲 篠澤広\仮装狂騒曲 篠澤広271.xml` には含まれていないことを確認済み
- シーンプリセットの `CurrentVersion = 42` は据え置く（v42 には照射対象 3/4 の追加も含まれるため）。旧プリセットの `faceShadow` 要素は XmlSerializer が読み飛ばす。版のコメントから顔の影の記述を外す
- SE で上書きしていた顔の影の値は MToonMaid へ移さない

### ドキュメント

- docs-site: `guide\maid-editing.md` の「顔の影」節、`timeline\layers-maid.md` の顔の影の記述、`timeline\compatibility.md` の該当行、`guide\scene-preset.md` の「顔の影」を削除する。`guide\staging.md` の顔のみ／体のみと Face レイヤーの記述は残す
- ワークスペースの `W:\COM3D2_5\work\CLAUDE.md`: 「表情レイヤーの顔の影キー」の項を削除する。「ライトキーの照射対象（index 18）の値 3・4」の項は残すが、「シーンプリセットは v42 で…」の文が顔の影に触れていないことを確かめる
- 過去の計画・仕様書（`docs\superpowers\`）は履歴なので書き換えない

## 進め方

- 計画は 1 本にまとめ、MToonMaid への追加 → SE からの削除の順で進める。コミットはリポジトリごとに分ける
- ビルドの確認では MSBuild を直接呼び、ゲームフォルダへは反映しない
- 実機の確認（ゲーム起動中なら devbridge）では次を確かめる
  - MToonMaid のトグルで head の Renderer の値が変わること
  - MToonMaid を無効にすると元の値へ戻ること
  - SE の顔のみ／体のみライトで、レイヤーの移動が今までどおり動くこと

## 範囲外

- 顔以外（肌・髪・衣装）の影を受ける／落とす
- 影の設定のタイムラインのキー化・シーンプリセットへの保存
- SE の旧データ（XML・プリセット）から MToonMaid の設定への移行
