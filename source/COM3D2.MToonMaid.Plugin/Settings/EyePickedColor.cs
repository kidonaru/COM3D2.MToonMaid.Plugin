namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 色の行に見せる色。選んだ色から求めた値のままの間は、選んだ色をそのまま見せる
    /// </summary>
    public sealed class EyePickedColor
    {
        private ColorData _picked;
        private int _hue;
        private int _chroma;
        private int _brightness;

        public void Remember(ColorData picked, int hue, int chroma, int brightness)
        {
            _picked = picked;
            _hue = hue;
            _chroma = chroma;
            _brightness = brightness;
        }

        public void Forget()
        {
            _picked = null;
        }

        public ColorData Shown(int hue, int chroma, int brightness)
        {
            // 値から求め直した色を見せると、丸めでずれた色からピッカーが色相を作り直し (灰色を通ると赤になる)、入力途中の HEX も書き換わる
            if (_picked != null && hue == _hue && chroma == _chroma && brightness == _brightness)
            {
                return _picked;
            }
            _picked = null;
            return EyeColorHsl.ToColor(hue, chroma, brightness);
        }
    }
}
