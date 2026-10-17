namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// COM3D2/MToon10 のプロパティ名とキーワード。
    /// 本家 MToon10 の _ShadeTex はゲームに合わせて _ShadowTex へ改名している
    /// </summary>
    public static class MToonProps
    {
        public const string ShaderName = "COM3D2/MToon10";

        // ゲームの Toony と同名 (変換で上書きするものは MaterialBackup に控える)
        public const string MainTex = "_MainTex";
        public const string Color = "_Color";
        public const string ShadowTex = "_ShadowTex";
        public const string RimColor = "_RimColor";
        public const string OutlineColor = "_OutlineColor";
        public const string OutlineWidth = "_OutlineWidth";
        public const string OutlineWidthTex = "_OutlineWidthTex";

        // ゲームの Toony のランプ。MToon にした後も GetTexture で読める (保存プロパティとして残る)
        public const string ToonRamp = "_ToonRamp";
        // ゲームの Toony の影の割合。プラグインは書かず、MToon がそのまま読む
        public const string ShadowRateToon = "_ShadowRateToon";

        // ゲームの Toony のリムの鋭さ・持ち上げ。MToon は書かないため控えるだけで戻さない
        public const string GameRimPower = "_RimPower";
        public const string GameRimShift = "_RimShift";

        // MToon 固有
        public const string AlphaMode = "_AlphaMode";
        public const string DoubleSided = "_DoubleSided";
        public const string ShadeColor = "_ShadeColor";
        public const string ShadingShiftFactor = "_ShadingShiftFactor";
        public const string ShadingToonyFactor = "_ShadingToonyFactor";
        public const string GiEqualization = "_GiEqualization";
        public const string RimFresnelPower = "_RimFresnelPower";
        public const string RimLift = "_RimLift";
        public const string RimLightingMix = "_RimLightingMix";
        public const string OutlineWidthMode = "_OutlineWidthMode";
        public const string OutlineLightingMix = "_OutlineLightingMix";
        public const string UseOutlineTex = "_UseOutlineTex";
        public const string ShadingShiftTex = "_ShadingShiftTex";
        public const string UvAnimMaskTex = "_UvAnimMaskTex";

        // COM3D2 のフォークで足したプロパティ。ゲーム側に同名が無いため MaterialBackup では退避しない
        public const string ToonRampLitColor = "_ToonRampLitColor";
        public const string ToonRampDarkColor = "_ToonRampDarkColor";
        public const string FlatShadowRamp = "_FlatShadowRamp";
        public const string HiRateScale = "_HiRateScale";
        public const string AdditionalLightShade = "_AdditionalLightShade";
        public const string UseShadowRateToon = "_UseShadowRateToon";
        public const string ShadowRateToonV = "_ShadowRateToonV";
        public const string ShadowRateShift = "_ShadowRateShift";
        public const string ShadeStrength = "_ShadeStrength";
        public const string UseToonRamp = "_UseToonRamp";
        public const string OutlineNearDistance = "_OutlineNearDistance";

        // 前髪の影 (顔の MToon が読む)
        public const string HairShadowTex = "_HairShadowTex";
        public const string HairShadowStrength = "_HairShadowStrength";
        public const string HairShadowMatrix = "_HairShadowMatrix";
        public const string HairShadowSoftness = "_HairShadowSoftness";

        // 顔の半面切り替えの影 (顔の MToon が読む)。中心・正面・頭の上はプラグインが毎フレーム入れる
        public const string FaceHalfShadow = "_FaceHalfShadow";
        public const string FaceHalfAngle = "_FaceHalfAngle";
        public const string FaceFullAngle = "_FaceFullAngle";
        public const string FaceHalfFadeWidth = "_FaceHalfFadeWidth";
        public const string FaceFullFadeWidth = "_FaceFullFadeWidth";
        public const string FaceHalfSoftness = "_FaceHalfSoftness";
        public const string FaceFrontAngle = "_FaceFrontAngle";
        public const string FaceFrontShadowStrength = "_FaceFrontShadowStrength";
        public const string FaceHalfCenter = "_FaceHalfCenter";
        public const string FaceHalfForward = "_FaceHalfForward";
        public const string FaceHalfUp = "_FaceHalfUp";

        // 前髪の深度を書くシェーダーと、そのグローバル値 (ゲームのグローバルと衝突しない専用名)
        public const string HairShadowCasterShaderName = "Hidden/COM3D2/MToonHairShadowCaster";
        public const string CasterTex = "_MToonHairShadowCasterTex";
        public const string CasterTexSt = "_MToonHairShadowCasterTex_ST";
        public const string CasterCutoff = "_MToonHairShadowCasterCutoff";
        public const string CasterMatrix = "_MToonHairShadowCasterMatrix";

        // 瞳の光沢 (MToonMaidEye) と、eye_hi の板を消す・残すシェーダー
        public const string EyeShaderName = "COM3D2/MToonMaidEye";
        public const string HiddenShaderName = "Hidden/COM3D2/MToonMaidHidden";
        public const string EyeHiShaderName = "COM3D2/MToonMaidEyeHi";
        public const string EyeIrisRect = "_IrisRect";
        public const string EyeUpperShadow = "_UpperShadow";
        public const string EyeUpperShadowColor = "_UpperShadowColor";
        public const string EyeUpperShadowHeight = "_UpperShadowHeight";
        public const string EyeUpperShadowSoftness = "_UpperShadowSoftness";
        public const string EyeLowerGlow = "_LowerGlow";
        public const string EyeLowerGlowColor = "_LowerGlowColor";
        public const string EyeLowerGlowHeight = "_LowerGlowHeight";
        public const string EyeLowerGlowSoftness = "_LowerGlowSoftness";
        public const string EyeHighlightCount = "_HighlightCount";
        public const string EyeHighlightShape = "_HighlightShape";
        public const string EyeHighlightParams = "_HighlightParams";
        public const string EyeHighlightColor = "_HighlightColor";
        public const string EyeHighlightGlow = "_HighlightGlow";
        public const string EyeLidFollow = "_LidFollow";
        public const string EyeLidCenter = "_LidCenter";
        public const string EyeLidClose = "_LidClose";
        public const string EyeParallax = "_Parallax";
        public const string EyeGaze = "_EyeGaze";
        public const string EyeHighlightLightInfluence = "_HighlightLightInfluence";
        public const string EyeIrisLightInfluence = "_IrisLightInfluence";
        public const string EyeHeadRight = "_HeadRight";
        public const string EyeHeadUp = "_HeadUp";

        // 白目の影 (MToon10 の白目を含むマテリアルへ入れる)。まぶたの閉じた割合は瞳と同じ EyeLidClose を使う
        public const string ScleraRect = "_ScleraRect";
        public const string ScleraColor = "_ScleraColor";
        public const string ScleraShadow = "_ScleraShadow";
        public const string ScleraShadowColor = "_ScleraShadowColor";
        public const string ScleraShadowHeight = "_ScleraShadowHeight";
        public const string ScleraShadowSoftness = "_ScleraShadowSoftness";
        public const string ScleraShadowBend = "_ScleraShadowBend";
        public const string ScleraShadowRoll = "_ScleraShadowRoll";
        public const string ScleraLidFollow = "_ScleraLidFollow";

        // 描画ステート (シェーダーの Pass が参照する)
        public const string SrcBlend = "_M_SrcBlend";
        public const string DstBlend = "_M_DstBlend";
        public const string ZWrite = "_M_ZWrite";
        public const string AlphaToMask = "_M_AlphaToMask";
        public const string CullMode = "_M_CullMode";

        public const string RenderTypeTag = "RenderType";

        public const string KeywordAlphaTest = "_ALPHATEST_ON";
        public const string KeywordAlphaBlend = "_ALPHABLEND_ON";
        public const string KeywordOutlineWorld = "_MTOON_OUTLINE_WORLD";
        public const string KeywordOutlineScreen = "_MTOON_OUTLINE_SCREEN";
        public const string KeywordParameterMap = "_MTOON_PARAMETERMAP";

        public const int AlphaModeOpaque = 0;
        public const int AlphaModeCutout = 1;
        public const int OutlineModeNone = 0;
        public const int OutlineModeWorld = 1;
        public const int OutlineModeScreen = 2;
    }
}
