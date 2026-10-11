using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 前髪の影マップの正射影。ワールド位置を (u, v, 深度) の 0〜1 へ写す。光に近いほど深度が小さい。
    /// テストの net48 では Unity 2022 の UnityEngine.CoreModule を読めないため、
    /// ベクトルは float[3]、行列は行優先の float[16] で扱う
    /// </summary>
    public static class HairShadowProjection
    {
        // 光の向きと上向きの候補がこれ以上平行なら、基底が潰れないよう予備の上向きを使う
        private const float ParallelThreshold = 0.99f;

        /// <summary>選べる影マップの一辺の画素数 (小さい順)</summary>
        public static readonly int[] Resolutions = { 512, 1024, 2048, 4096 };

        /// <summary>顔が影マップを読むための行列。影が worldOffset (ワールドの m) だけ動いて見えるようにする。前髪を描く行列は m のまま使う</summary>
        public static float[] Offset(float[] m, float[] worldOffset)
        {
            var r = (float[])m.Clone();
            // 深度までずらすと、光の向きに沿ったずらしで前髪より奥の顔が手前扱いになり影が抜ける
            for (var row = 0; row < 2; row++)
            {
                var o = row * 4;
                r[o + 3] -= m[o] * worldOffset[0] + m[o + 1] * worldOffset[1] + m[o + 2] * worldOffset[2];
            }
            return r;
        }

        /// <summary>手で編集した設定ファイルの値も使えるよう、Resolutions の最も近い値へ丸める</summary>
        public static int ClampResolution(int value)
        {
            var best = Resolutions[0];
            foreach (var r in Resolutions)
            {
                if (Math.Abs(r - value) < Math.Abs(best - value))
                {
                    best = r;
                }
            }
            return best;
        }

        /// <summary>
        /// center を中心に半径 radius の立方体を影マップへ写す行列。toLight は顔から光へ向かう向き。
        /// v は upHint の向きに増える (toLight と平行なら fallbackUp を使う)
        /// </summary>
        public static float[] Create(float[] center, float[] toLight, float[] upHint, float[] fallbackUp, float radius)
        {
            // 深度は光から遠ざかる向きに増やす
            var forward = Normalize(Scale(toLight, -1f));
            var up = Math.Abs(Dot(Normalize(upHint), forward)) < ParallelThreshold ? upHint : fallbackUp;
            var right = Normalize(Cross(up, forward));
            var trueUp = Cross(forward, right);
            var s = 0.5f / radius;
            return new[]
            {
                right[0] * s, right[1] * s, right[2] * s, 0.5f - Dot(right, center) * s,
                trueUp[0] * s, trueUp[1] * s, trueUp[2] * s, 0.5f - Dot(trueUp, center) * s,
                forward[0] * s, forward[1] * s, forward[2] * s, 0.5f - Dot(forward, center) * s,
                0f, 0f, 0f, 1f,
            };
        }

        /// <summary>
        /// 0〜1 の (u, v, 深度) を -1〜1 のクリップ空間 (OpenGL 流) へ写す行列。
        /// CommandBuffer.SetViewProjectionMatrices へそのまま渡す (GL.GetGPUProjectionMatrix は通さない)
        /// </summary>
        public static float[] ToClip(float[] m)
        {
            var clip = new float[16];
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    // 2 × 行 - 最後の行 (= 2x - 1)
                    clip[row * 4 + col] = 2f * m[row * 4 + col] - m[12 + col];
                }
            }
            Array.Copy(m, 12, clip, 12, 4);
            return clip;
        }

        /// <summary>前髪の影を描くか。描かないときは影マップを作らず、顔の強さを 0 にする</summary>
        public static bool ShouldDraw(float strength, bool casterAvailable, bool waitingRetry)
        {
            return strength > 0f && casterAvailable && !waitingRetry;
        }

        /// <summary>顔へ入れる前髪の影の濃さ。主光源の落ち影 (Light.shadowStrength) と同じ濃さを 1 とした倍率で掛ける</summary>
        public static float LinkedStrength(float hairShadow, bool lightCastsShadows, float lightShadowStrength)
        {
            return lightCastsShadows ? Clamp01(hairShadow) * Clamp01(lightShadowStrength) : 0f;
        }

        private static float Clamp01(float value)
        {
            // Math.Min/Max だけだと、手で編集した設定ファイルの NaN がそのままシェーダーへ渡る
            return float.IsNaN(value) ? 0f : Math.Min(Math.Max(value, 0f), 1f);
        }

        public static float[] Apply(float[] m, float[] p)
        {
            var r = new float[3];
            for (var row = 0; row < 3; row++)
            {
                r[row] = m[row * 4] * p[0] + m[row * 4 + 1] * p[1] + m[row * 4 + 2] * p[2] + m[row * 4 + 3];
            }
            return r;
        }

        private static float Dot(float[] a, float[] b)
        {
            return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        private static float[] Cross(float[] a, float[] b)
        {
            return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        }

        private static float[] Scale(float[] a, float s)
        {
            return new[] { a[0] * s, a[1] * s, a[2] * s };
        }

        private static float[] Normalize(float[] a)
        {
            var len = (float)Math.Sqrt(Dot(a, a));
            return Scale(a, 1f / len);
        }
    }
}
