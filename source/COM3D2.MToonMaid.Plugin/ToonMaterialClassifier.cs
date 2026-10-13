using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 置き換え対象の判定と部位グループの決定。
    /// 半透明 (_Trans / _Trans_NoZ) は描画順が崩れやすいため対象外にする
    /// </summary>
    public static class ToonMaterialClassifier
    {
        private const string ToonyPrefix = "CM3D2/Toony_Lighted";

        public const string HeadSlot = "head";
        // eye の menu は顔によらず head の番号 0 を左目 (EYE_L)、1 を右目 (EYE_R) として書く
        public const int LeftEyeMaterialIndex = 0;
        public const int RightEyeMaterialIndex = 1;
        // eye_hi は番号 2。ゲームは eye_hi_r の「テクスチャ変更」を番号 7 へ向け直す (Menu.ProcScript)
        private const int EyeHighlightMaterialIndex = 2;
        private const int RightEyeHighlightMaterialIndex = 7;
        // 前方一致にすると _NoZ (深度を比べない) の瞳まで深度を比べるシェーダーへ差し替え、描画順が変わる
        private const string EyeShaderName = "CM3D2/Toony_Lighted_Trans";
        // 番号だけで判定すると、顔 MOD の番号 2 や古い顔の番号 7 (bukkake) まで消す
        private const string EyeHighlightShaderName = "CM3D2/Lighted_Trans";

        public const float DiagonalShadowRateV = -1f;
        // 実測で Muscle は v=0.75 の行を読む。肌の種類の toonSkin_shadow_* は下半分だけが違うため、どれも同じ形になる
        private const float MuscleShadowRateV = 0.75f;

        public static bool IsTarget(string shaderName)
        {
            return !string.IsNullOrEmpty(shaderName)
                && shaderName.StartsWith(ToonyPrefix, StringComparison.Ordinal)
                && shaderName.IndexOf("Trans", StringComparison.Ordinal) < 0;
        }

        public static bool HasOutline(string shaderName)
        {
            return shaderName != null && shaderName.IndexOf("Outline", StringComparison.Ordinal) >= 0;
        }

        /// <summary>ゲームが _OutlineColor ではなく _OutlineTex の色で線を描くシェーダーか</summary>
        public static bool UsesOutlineTex(string shaderName)
        {
            return shaderName != null && shaderName.IndexOf("Outline_Tex", StringComparison.Ordinal) >= 0;
        }

        public static bool IsCutout(string shaderName)
        {
            return shaderName != null && shaderName.IndexOf("Cutout", StringComparison.Ordinal) >= 0;
        }

        /// <summary>_ShadowRateToon を読む v。DiagonalShadowRateV (負) なら u と同じ値で読む</summary>
        public static float ShadowRateToonV(string shaderName)
        {
            return shaderName != null && shaderName.IndexOf("Muscle", StringComparison.Ordinal) >= 0
                ? MuscleShadowRateV
                : DiagonalShadowRateV;
        }

        public static PartGroup GetGroup(string slotCategory)
        {
            if (slotCategory == "body")
            {
                return PartGroup.Skin;
            }
            if (slotCategory == "head")
            {
                return PartGroup.Face;
            }
            if (slotCategory != null && slotCategory.StartsWith("hair", StringComparison.OrdinalIgnoreCase))
            {
                return PartGroup.Hair;
            }
            return PartGroup.Costume;
        }

        /// <summary>前髪の影を受けるグループ。前髪が落とす先は顔だけ</summary>
        public static bool UsesHairShadow(PartGroup group)
        {
            return group == PartGroup.Face;
        }

        /// <summary>head スロットの Renderer の影を受ける・落とすを書くグループ</summary>
        public static bool UsesRendererShadow(PartGroup group)
        {
            return group == PartGroup.Face;
        }

        /// <summary>半面切り替えの影を出すグループ。頭の中心と正面で影を決めるため顔だけ</summary>
        public static bool UsesFaceHalfShadow(PartGroup group)
        {
            return group == PartGroup.Face;
        }

        /// <summary>
        /// 天使の輪の強さを調整に出すグループ。髪のシェーダーは髪スロットのほか、帽子や髪付きのアクセ (衣装) にも使われる
        /// </summary>
        public static bool UsesHighlightScale(PartGroup group)
        {
            return group == PartGroup.Hair || group == PartGroup.Costume;
        }

        public static bool IsEyeShader(string shaderName)
        {
            return shaderName == EyeShaderName;
        }

        /// <summary>瞳の光沢のシェーダーへ差し替える瞳のマテリアル</summary>
        public static bool IsEyeMaterial(string slotCategory, int materialIndex, string shaderName)
        {
            return slotCategory == HeadSlot
                && (materialIndex == LeftEyeMaterialIndex || materialIndex == RightEyeMaterialIndex)
                && IsEyeShader(shaderName);
        }

        /// <summary>
        /// eye_hi の板 (SkinHi)。hasRightHighlight は MPN.eye_hi_r にファイルがあるか。
        /// shaderName は差し替える前のシェーダー名 (消した後は控えの名前を渡す)
        /// </summary>
        public static bool IsEyeHighlightMaterial(string slotCategory, int materialIndex, bool hasRightHighlight, string shaderName)
        {
            return slotCategory == HeadSlot
                && shaderName == EyeHighlightShaderName
                && (materialIndex == EyeHighlightMaterialIndex
                    || (hasRightHighlight && materialIndex == RightEyeHighlightMaterialIndex));
        }

        /// <summary>
        /// 白目を含みうるマテリアル名。白目を含まない口 (Face007_GP_Mouth) も候補になるが、瞳の外接箱に三角形が無く範囲が求まらないため後段で除かれる
        /// </summary>
        public static bool IsScleraMaterialName(string materialName)
        {
            return materialName != null
                && (materialName.IndexOf("Mouth", StringComparison.OrdinalIgnoreCase) >= 0
                    || materialName.IndexOf("EyeWhite", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
