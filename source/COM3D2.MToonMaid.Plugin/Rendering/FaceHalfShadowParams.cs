using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>半面切り替えの影の設定値を、シェーダーへ書く値 (角度・幅はラジアン、ぼかしは m) にする</summary>
    public static class FaceHalfShadowParams
    {
        private const float DegreesToRadians = (float)(Math.PI / 180.0);
        // 既定値を定数で二重に持つと、GroupSettings の既定値を変えたときにずれる
        private static readonly GroupSettings Defaults = new GroupSettings();

        /// <summary>_FaceHalfShadow を 1 にするか</summary>
        public static bool Enabled(bool faceHalfShadow, PartGroup group)
        {
            // 手編集で顔以外を個別に ON にしても、頭の中心で左右を分ける影は顔以外の形に合わない
            return faceHalfShadow && ToonMaterialClassifier.UsesFaceHalfShadow(group);
        }

        public static float HalfAngle(float degrees)
        {
            return HalfDegrees(degrees) * DegreesToRadians;
        }

        /// <summary>全面になる角度。半面になる角度より小さいときは半面になる角度にそろえる</summary>
        public static float FullAngle(float halfDegrees, float fullDegrees)
        {
            return FullDegrees(halfDegrees, fullDegrees) * DegreesToRadians;
        }

        /// <summary>半面へ切り替えるフェードの幅。正面・真後ろで左右が入れ替わっても影が飛ばないよう、0°〜180° の内側でフェードさせる</summary>
        public static float HalfFadeWidth(float halfDegrees, float widthDegrees)
        {
            return FadeWidthWithin(HalfDegrees(halfDegrees), widthDegrees) * DegreesToRadians;
        }

        /// <summary>全面へ切り替えるフェードの幅。HalfFadeWidth と同じく 0°〜180° の内側でフェードさせる</summary>
        public static float FullFadeWidth(float halfDegrees, float fullDegrees, float widthDegrees)
        {
            // 半面と同じ幅を使い回すと、片方の角度が端に寄っただけでもう片方の切り替えまで硬くなる
            return FadeWidthWithin(FullDegrees(halfDegrees, fullDegrees), widthDegrees) * DegreesToRadians;
        }

        public static float FrontAngle(float degrees)
        {
            return ClampFinite(degrees, GroupSettings.MaxFaceFrontAngle, Defaults.faceFrontAngle) * DegreesToRadians;
        }

        public static float FrontShadowStrength(float strength)
        {
            return ClampFinite(strength, GroupSettings.MaxFaceFrontShadowStrength, Defaults.faceFrontShadowStrength);
        }

        public static float Softness(float meters)
        {
            return ClampFinite(meters, GroupSettings.MaxFaceHalfSoftness, Defaults.faceHalfSoftness);
        }

        private static float HalfDegrees(float degrees)
        {
            return ClampFinite(degrees, GroupSettings.MaxFaceHalfAngle, Defaults.faceHalfAngle);
        }

        private static float FullDegrees(float halfDegrees, float fullDegrees)
        {
            // 全面が半面より先に始まると、光側の半面だけが影になり光と反対側が明るい逆の見た目になる
            return Math.Max(HalfDegrees(halfDegrees), ClampFinite(fullDegrees, GroupSettings.MaxFaceHalfAngle, Defaults.faceFullAngle));
        }

        private static float FadeWidthWithin(float centerDegrees, float widthDegrees)
        {
            var width = ClampFinite(widthDegrees, GroupSettings.MaxFaceHalfFadeWidth, Defaults.faceHalfFadeWidth);
            return Math.Min(width, 2f * Math.Min(centerDegrees, GroupSettings.MaxFaceHalfAngle - centerDegrees));
        }

        private static float ClampFinite(float value, float max, float fallback)
        {
            // Math.Min / Max だけだと NaN を通し、シェーダーの smoothstep が NaN になって顔が真っ黒になる
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }
            return Math.Max(0f, Math.Min(max, value));
        }
    }
}
