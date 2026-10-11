namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// Toony の _ToonRamp から取った光側 (右端) と影側 (最も暗い画素) の色。
    /// Toony は陰影をこのランプの色で掛けるため、MToon では光側をアルベドに、影側を影色に掛けて明るさを揃える
    /// </summary>
    public sealed class ToonRampColors
    {
        public static ToonRampColors White { get; } = new ToonRampColors(new ColorData(1f, 1f, 1f, 1f), new ColorData(1f, 1f, 1f, 1f));

        public ColorData Lit { get; }
        public ColorData Dark { get; }

        private ToonRampColors(ColorData lit, ColorData dark)
        {
            Lit = lit;
            Dark = dark;
        }

        /// <summary>RGBA を並べたランプ 1 行分から作る。読めない形なら White</summary>
        public static ToonRampColors FromRow(float[] rgba, int width)
        {
            if (rgba == null || width < 1 || rgba.Length < width * 4)
            {
                return White;
            }
            var last = (width - 1) * 4;
            var dark = FindDarkest(rgba, width);
            return new ToonRampColors(
                new ColorData(rgba[last], rgba[last + 1], rgba[last + 2], 1f),
                new ColorData(rgba[dark], rgba[dark + 1], rgba[dark + 2], 1f));
        }

        /// <summary>
        /// 輝度が最も低い画素の先頭インデックス。肌のランプは左端が白へ戻るため、左端ではなく最暗画素を影側にする
        /// (バニラの肌ではここが最も赤い)。透明な画素は MOD ランプの余白とみなして選ばず、全て透明なら左端
        /// </summary>
        private static int FindDarkest(float[] rgba, int width)
        {
            const float minAlpha = 0.5f;
            var darkest = 0;
            var minLuminance = float.MaxValue;
            for (var i = 0; i < width * 4; i += 4)
            {
                if (rgba[i + 3] < minAlpha)
                {
                    continue;
                }
                // 輝度 (Rec.601)
                var luminance = 0.299f * rgba[i] + 0.587f * rgba[i + 1] + 0.114f * rgba[i + 2];
                if (luminance < minLuminance)
                {
                    minLuminance = luminance;
                    darkest = i;
                }
            }
            return darkest;
        }
    }
}
