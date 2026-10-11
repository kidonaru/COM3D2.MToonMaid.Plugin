# COM3D25.MToonMaid.Plugin 設計 v2.7（球の陰影を外す）

日付: 2026-10-09
前提: `2026-10-09-mtoon-maid-face-cylinder-design.md`（v2.6）までの設計を引き継ぐ。ここに書いたことだけを変える。

## 背景

v2.3〜v2.6 で、顔の陰影の法線を頭の球（v2.4 で中心を後ろへずらす、v2.6 で円柱へ寄せる）へ寄せる機能を足した。ユーザーが実際に使ってみたところ、「境界の位置」の調整と「凹凸の影を消す」（v2.5）で十分だったため、球の陰影の機能を外してコードとシェーダーを簡単にする。

## 外すもの

- 設定: 「顔の陰影を球に」（`sphereNormal`）、「顔を平らに」（`faceFlatten`）、「上下の丸みを消す」（`faceCylinder`）。継承の単位は 15 項目になる（v2.6 の 18 項目から 3 つ減る）
- ウィンドウ: 上の 3 つのスライダー
- プラグイン: 頭の中心の控え（走査で頭のスロットのバウンズから取る処理）と、描画の直前に頭の中心・後ろ向き・上向きをマテリアルへ入れる処理（`Application.onBeforeRender`）
- シェーダー: `_SphereNormal` / `_HeadCenter` / `_FaceFlatten` / `_HeadBack` / `_FaceCylinder` / `_HeadUp`、陰影専用の法線（`MToonInput.shadingNormalWS`）、NaN を出さない正規化（`MToon_SafeNormalize`）。陰影の N・L は MToon 本来の `normalWS` に戻す
- README の 3 つの使い方の行

## 残すもの

- 「凹凸の影を消す」（`faceBumpLift`、`_ShadingShiftTex` のマスク）はそのまま。対象は顔のグループだけで、肌・髪・衣装のタブでは無効表示のまま
- 顔のグループの判定（今の `ToonMaterialClassifier.UsesSphereNormal`）は凹凸のマスクが使うため残し、名前を `UsesFaceBumpMask` に変える

## 既存の設定ファイル

- 版は 3 のまま
- 外した項目の要素（`<sphereNormal>` など）は読み飛ばす（XmlSerializer は知らない要素を無視する）。警告は出さない
- 継承の単位の一覧（`overrides`）にある、今の継承の単位に無いキーは、読み込み時に取り除く。次の保存でファイルからも消える
- 外した項目の値は戻らない。球の陰影をやめた見た目になる
- 取り除くのは「この DLL が知らないキー」すべて。版を上げずに項目を足したあとで古い DLL へ戻して保存すると、その項目の個別の指定も消える（値の要素も XmlSerializer が書き戻さないため、値と個別の指定がそろって消える）

## 範囲外

- 過去の spec・計画の書き換え（履歴として残す）
- 外した機能の代わりの機能

## 実機検証の結果

2026-10-09、セーブ 42、メイド 0（Nekomimi）で確かめた。

- 終了前のユーザーの設定ファイルには、外した項目の要素が 15 個（`sphereNormal` 0 ×5、`faceFlatten` 0 ×2・0.5 ×3、`faceCylinder` 0 ×5）あり、`overrides` に外したキーは無かった。新しい DLL で読み込みの警告は出ない
- 読み込み後の `overrides` は 肌 `[shadingToony]`・顔 `[shadingShift]`・髪と衣装は空で、外したキーは無い
- 顔の MToon（肌 `face_tokinagare_Skin`・口 `Face006_Mouth`）は `_SphereNormal` / `_HeadCenter` を持たない（`HasProperty` が false）。「凹凸の影を消す」1 でマスク `MToonMaid_BumpMask` が入る
- 顔はふつうの MToon の陰影で描かれ、黒や白の破綻は無い
- ウィンドウ（SceneEditor にドッキングした状態）: 共通タブは「追加ライトの影側」の下に「凹凸の影を消す」が出て、球の 3 行は無い。値の欄はラベルとずれない。肌タブでは「凹凸の影を消す」が個別のトグルを含めて無効表示、顔タブでは有効。MToonMaid 由来の例外は無い
