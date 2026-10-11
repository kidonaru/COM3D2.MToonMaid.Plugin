using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>ゲームの Toony のリム (_RimPower / _RimShift) から MToon の _RimFresnelPower / _RimLift を作る</summary>
    public static class RimParams
    {
        public const float DefaultPower = 5f;
        public const float DefaultShift = 0f;

        public static float FresnelPower(float gamePower, float scale)
        {
            return Math.Max(0f, gamePower * scale);
        }

        public static float Lift(float gameShift, float offset)
        {
            return gameShift + offset;
        }
    }
}
