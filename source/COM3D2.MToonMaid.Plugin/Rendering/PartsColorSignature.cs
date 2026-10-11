namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// メイドの無限色の設定値 (MaidParts.PartsColor の全フィールド) の署名。
    /// 無限色のランプは同じ RenderTexture へ描き直され、変化を知らせる値も増えないため、設定値の変化で読み直しを判断する。
    /// 構造体の既定の GetHashCode は実装次第で一部のフィールドしか見ないため、明示的に混ぜる
    /// </summary>
    public static class PartsColorSignature
    {
        public const int Seed = 17;

        public static int Mix(int hash, bool use, int mainHue, int mainChroma, int mainBrightness, int mainContrast,
            int shadowRate, int shadowHue, int shadowChroma, int shadowBrightness, int shadowContrast)
        {
            unchecked
            {
                hash = hash * 31 + (use ? 1 : 0);
                hash = hash * 31 + mainHue;
                hash = hash * 31 + mainChroma;
                hash = hash * 31 + mainBrightness;
                hash = hash * 31 + mainContrast;
                hash = hash * 31 + shadowRate;
                hash = hash * 31 + shadowHue;
                hash = hash * 31 + shadowChroma;
                hash = hash * 31 + shadowBrightness;
                hash = hash * 31 + shadowContrast;
                return hash;
            }
        }
    }
}
