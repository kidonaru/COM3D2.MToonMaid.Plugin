using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>ゲームの瞳のパーツカラー (MaidParts.PartsColor) の値。範囲はエディットのスライダーと同じ整数</summary>
    public struct EyeColorValues : IEquatable<EyeColorValues>
    {
        public const int MaxHue = 255;
        public const int MaxChroma = 255;
        public const int MaxBrightness = 510;
        public const int MaxContrast = 200;
        public const int MaxShadowRate = 255;

        public int mainHue;
        public int mainChroma;
        public int mainBrightness;
        public int mainContrast;
        public int shadowRate;
        public int shadowHue;
        public int shadowChroma;
        public int shadowBrightness;
        public int shadowContrast;

        public EyeColorValues Clamped()
        {
            return new EyeColorValues
            {
                mainHue = Clamp(mainHue, MaxHue),
                mainChroma = Clamp(mainChroma, MaxChroma),
                mainBrightness = Clamp(mainBrightness, MaxBrightness),
                mainContrast = Clamp(mainContrast, MaxContrast),
                shadowRate = Clamp(shadowRate, MaxShadowRate),
                shadowHue = Clamp(shadowHue, MaxHue),
                shadowChroma = Clamp(shadowChroma, MaxChroma),
                shadowBrightness = Clamp(shadowBrightness, MaxBrightness),
                shadowContrast = Clamp(shadowContrast, MaxContrast),
            };
        }

        public bool Equals(EyeColorValues o)
        {
            return mainHue == o.mainHue && mainChroma == o.mainChroma && mainBrightness == o.mainBrightness && mainContrast == o.mainContrast
                && shadowRate == o.shadowRate && shadowHue == o.shadowHue && shadowChroma == o.shadowChroma
                && shadowBrightness == o.shadowBrightness && shadowContrast == o.shadowContrast;
        }

        public override bool Equals(object obj)
        {
            return obj is EyeColorValues && Equals((EyeColorValues)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var h = mainHue;
                h = h * 31 + mainChroma;
                h = h * 31 + mainBrightness;
                h = h * 31 + mainContrast;
                h = h * 31 + shadowRate;
                h = h * 31 + shadowHue;
                h = h * 31 + shadowChroma;
                h = h * 31 + shadowBrightness;
                h = h * 31 + shadowContrast;
                return h;
            }
        }

        private static int Clamp(int v, int max)
        {
            return Math.Max(0, Math.Min(max, v));
        }
    }

    /// <summary>瞳の色を変える目。ゲームの EyePartsTab (LR / L / R) と対応する</summary>
    public enum EyeColorSide
    {
        Both,
        Left,
        Right,
    }

    public static class EyeColorSides
    {
        /// <summary>
        /// 左右が選ばれていて左右の値が違うときは右にする (エディットのパレットを開いたときと同じ。
        /// 左右のまま書くと右の色を黙って上書きする)
        /// </summary>
        public static EyeColorSide ResolveSide(EyeColorSide stored, EyeColorValues left, EyeColorValues right)
        {
            return stored == EyeColorSide.Both && !left.Equals(right) ? EyeColorSide.Right : stored;
        }
    }
}
