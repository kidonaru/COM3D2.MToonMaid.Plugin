using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>Renderer の影設定を書くか・戻す値の決め方 (FaceShadowApplier の判定部分)</summary>
    public static class RendererShadowRules
    {
        public static bool ShouldWrite<T>(T desired, T current)
        {
            return !EqualityComparer<T>.Default.Equals(desired, current);
        }

        /// <summary>
        /// 書く影のモード。設定の ON/OFF が元の値の ON/OFF (originalCasts) と同じなら元のモード、違えば on / off
        /// </summary>
        public static T ResolveCastMode<T>(bool cast, T original, bool originalCasts, T on, T off)
        {
            // 常に on / off を書くと、既定のままでもモデルが指定した TwoSided / ShadowsOnly を潰す
            if (cast == originalCasts)
            {
                return original;
            }
            return cast ? on : off;
        }

        /// <summary>今の値が前回書いた値のままなら元の値、他者が書き換えていればその値</summary>
        public static T ResolveRestore<T>(T current, T lastWritten, T original)
        {
            return EqualityComparer<T>.Default.Equals(current, lastWritten) ? original : current;
        }
    }
}
