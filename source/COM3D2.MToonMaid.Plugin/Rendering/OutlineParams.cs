namespace COM3D2.MToonMaid.Plugin
{
    public enum OutlineColorSource
    {
        Override,
        Texture,
        Game,
    }

    public static class OutlineParams
    {
        public static OutlineColorSource Source(bool fromTex, bool colorOverride)
        {
            // テクスチャを掛けたまま指定色を乗算すると、上書きした色がそのまま出ない
            if (colorOverride)
            {
                return OutlineColorSource.Override;
            }
            return fromTex ? OutlineColorSource.Texture : OutlineColorSource.Game;
        }

        /// <summary>_OutlineWidthMode に書く値。outline は元のシェーダーに線があり、設定で描くとき true</summary>
        public static int Mode(bool outline)
        {
            return outline ? MToonProps.OutlineModeWorld : MToonProps.OutlineModeNone;
        }
    }
}
