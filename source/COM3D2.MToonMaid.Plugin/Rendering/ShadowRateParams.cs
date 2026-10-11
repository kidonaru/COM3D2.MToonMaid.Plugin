namespace COM3D2.MToonMaid.Plugin
{
    public static class ShadowRateParams
    {
        /// <summary>主光源の陰影を _ShadowRateToon で決めるか。hasTexture は MToon にした後のマテリアルの _ShadowRateToon が null でないか</summary>
        public static bool Use(bool gameShadowRate, bool hasTexture)
        {
            // テクスチャの無い MOD などで使うと、シェーダーの既定 (white) で影が消える
            return gameShadowRate && hasTexture;
        }

        /// <summary>_ToonRamp をシェーダーで読むか。hasRamp / hasShadowRate は MToon にした後のマテリアルのテクスチャが null でないか</summary>
        public static bool UseRamp(bool gameShadowRate, bool hasRamp, bool hasShadowRate)
        {
            // _ShadowRateToon の無いマテリアルでランプだけを読むと、従来の境界の陰影にランプの暗さが重なる
            return Use(gameShadowRate, hasShadowRate) && hasRamp;
        }

        /// <summary>_ToonRampLitColor に入れる色。ランプを読むときは白 (シェーダーがランプの色を掛ける)</summary>
        public static ColorData RampLitColor(bool useRamp, ToonRampColors ramp)
        {
            // ランプを読みながら 2 色も掛けると、ランプの色が二重に掛かる
            return useRamp ? ToonRampColors.White.Lit : ramp.Lit;
        }

        /// <summary>設定の影色に掛けて _ShadeColor にする色。ランプを読むときは白</summary>
        public static ColorData RampShadeMultiplier(bool useRamp, ToonRampColors ramp)
        {
            return useRamp ? ToonRampColors.White.Dark : ramp.Dark;
        }

        /// <summary>_ShadeStrength に書く値。手編集の範囲外を 0〜GroupSettings.MaxShadeStrength へ丸める</summary>
        public static float ClampShadeStrength(float value)
        {
            // Mathf.Clamp01 は NaN をそのまま返し、シェーダーの lerp が NaN になって真っ黒になる
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 1f;
            }
            return System.Math.Max(0f, System.Math.Min(GroupSettings.MaxShadeStrength, value));
        }
    }
}
