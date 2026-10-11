using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>まぶたの閉じ具合。ゲームがまばたきを足すとき (TMorph.FixBlendValues_Face) と同じ数え方にする</summary>
    public static class EyelidClosure
    {
        /// <summary>
        /// rows は TMorph.BlendDataIdx_EyeClose (顔タイプごとの eyeclose 系のブレンドの番号)。番号 0 は「そのブレンドが無い」の印。結果は 0〜1
        /// </summary>
        public static float Sum(int[][] rows, Func<int, float> blendValue)
        {
            if (rows == null)
            {
                return 0f;
            }
            // 今の顔タイプの行だけを選ぶと、その値が private で、CRC の顔 (TMorphSkin) では派生クラスの別のフィールドになる。
            // 今の顔タイプ以外の行はゲームが毎フレーム 0 にするため、全ての行を足しても同じ値になる
            var sum = 0f;
            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }
                foreach (var index in row)
                {
                    if (index == 0)
                    {
                        continue;
                    }
                    var v = blendValue(index);
                    // NaN を足すと合計が NaN になり、シェーダーでハイライトが消える。負の値も数えない
                    if (v > 0f)
                    {
                        sum += v;
                    }
                }
            }
            return Math.Min(sum, 1f);
        }
    }
}
