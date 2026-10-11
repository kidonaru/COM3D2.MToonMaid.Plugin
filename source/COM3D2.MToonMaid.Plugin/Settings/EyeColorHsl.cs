using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>パーツカラーの (色相, 彩度, 明度) と、瞳のテクスチャの明るさ 128 の位置の色 (RGB 0〜1) の変換。この位置の L は (明度 − 127) / 255</summary>
    public static class EyeColorHsl
    {
        public const int ColorMinBrightness = 127;
        public const int ColorMaxBrightness = ColorMinBrightness + 255;

        public static ColorData ToColor(int hue, int chroma, int brightness)
        {
            var h = hue / 255.0;
            // HueToRgb の ±1 の折り返しは 1 回分なので、MOD などの範囲外の色相は先に一周へ収める
            h -= Math.Floor(h);
            var s = Clamp01(chroma / 255.0);
            var l = Clamp01((brightness - ColorMinBrightness) / 255.0);
            var q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            var p = 2.0 * l - q;
            return new ColorData((float)HueToRgb(p, q, h + 1.0 / 3.0), (float)HueToRgb(p, q, h), (float)HueToRgb(p, q, h - 1.0 / 3.0), 1f);
        }

        /// <summary>
        /// 色から値を求めて書き換える。RGB から決まらない値 (灰色の色相、黒・白の色相と彩度) は渡された値を残す
        /// </summary>
        public static void FromColor(ColorData c, ref int hue, ref int chroma, ref int brightness)
        {
            // float のままだと、彩度 1・明度 381 付近で max − min が小さすぎて色相の往復が丸めの境目に寄る
            var r = Clamp01(c.r);
            var g = Clamp01(c.g);
            var b = Clamp01(c.b);
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var l = (max + min) / 2.0;
            var lum = (int)Math.Round(l * 255.0, MidpointRounding.AwayFromZero);
            brightness = lum + ColorMinBrightness;
            if (lum == 0 || lum == 255)
            {
                return;
            }

            var d = max - min;
            var newChroma = (int)Math.Round(Clamp01(d / (1.0 - Math.Abs(2.0 * l - 1.0))) * 255.0, MidpointRounding.AwayFromZero);
            chroma = newChroma;
            if (newChroma == 0)
            {
                return;
            }

            double h;
            if (max == r)
            {
                h = (g - b) / d + (g < b ? 6.0 : 0.0);
            }
            else if (max == g)
            {
                h = (b - r) / d + 2.0;
            }
            else
            {
                h = (r - g) / d + 4.0;
            }
            hue = (int)Math.Round(h / 6.0 * 255.0, MidpointRounding.AwayFromZero);
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0.0)
            {
                t += 1.0;
            }
            if (t > 1.0)
            {
                t -= 1.0;
            }
            if (t < 1.0 / 6.0)
            {
                return p + (q - p) * 6.0 * t;
            }
            if (t < 0.5)
            {
                return q;
            }
            if (t < 2.0 / 3.0)
            {
                return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            }
            return p;
        }

        private static double Clamp01(double v)
        {
            return Math.Max(0.0, Math.Min(1.0, v));
        }
    }
}
