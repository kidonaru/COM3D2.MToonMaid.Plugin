namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 共通から継承する単位のキー。GroupSettings.overrides に入り、設定ファイルにも書くため値を変えない。
    /// リム色と線の色は「上書きするか」と色を 1 つの単位にする (片方だけ継承すると意味が崩れる)。
    /// アウトラインの幅は基準ごとの 2 つの値を 1 つの単位にする
    /// </summary>
    public static class GroupSettingKeys
    {
        public const string ShadeColor = "shadeColor";
        public const string ShadeStrength = "shadeStrength";
        public const string GameShadowRate = "gameShadowRate";
        public const string ShadingShift = "shadingShift";
        public const string ShadingToony = "shadingToony";
        public const string GiEqualization = "giEqualization";
        public const string AdditionalLightShade = "additionalLightShade";
        public const string FlatShadowRamp = "flatShadowRamp";
        public const string HairShadow = "hairShadow";
        public const string HairShadowMode = "hairShadowMode";
        public const string HairShadowOffsetX = "hairShadowOffsetX";
        public const string HairShadowOffsetY = "hairShadowOffsetY";
        public const string HairShadowResolution = "hairShadowResolution";
        public const string HairShadowSoftness = "hairShadowSoftness";
        public const string HighlightScale = "highlightScale";
        public const string RimColor = "rimColor";
        public const string RimFresnelPowerScale = "rimFresnelPowerScale";
        public const string RimLiftOffset = "rimLiftOffset";
        public const string RimLightingMix = "rimLightingMix";
        public const string OutlineEnabled = "outlineEnabled";
        public const string OutlineWidth = "outlineWidth";
        public const string OutlineNearDistance = "outlineNearDistance";
        public const string OutlineLightingMix = "outlineLightingMix";
        public const string OutlineColor = "outlineColor";
        public const string ReceiveShadows = "receiveShadows";
        public const string CastShadows = "castShadows";
        public const string FaceHalfShadow = "faceHalfShadow";
        public const string FaceHalfAngle = "faceHalfAngle";
        public const string FaceFullAngle = "faceFullAngle";
        public const string FaceHalfFadeWidth = "faceHalfFadeWidth";
        public const string FaceHalfSoftness = "faceHalfSoftness";
        public const string FaceFrontAngle = "faceFrontAngle";
        public const string FaceFrontShadowStrength = "faceFrontShadowStrength";

        public static readonly string[] All =
        {
            ShadeColor, ShadeStrength, GameShadowRate, ShadingShift, ShadingToony, GiEqualization, AdditionalLightShade, FlatShadowRamp,
            HairShadow, HairShadowMode, HairShadowOffsetX, HairShadowOffsetY, HairShadowResolution, HairShadowSoftness,
            HighlightScale,
            RimColor, RimFresnelPowerScale, RimLiftOffset, RimLightingMix,
            OutlineEnabled, OutlineWidth, OutlineNearDistance, OutlineLightingMix, OutlineColor,
            ReceiveShadows, CastShadows,
            FaceHalfShadow, FaceHalfAngle, FaceFullAngle, FaceHalfFadeWidth, FaceHalfSoftness, FaceFrontAngle, FaceFrontShadowStrength,
        };

        /// <summary>
        /// 一部のグループにしか効かない項目。共通の値を持たず、個別にしていないグループは既定の値を使う
        /// </summary>
        public static bool IsGroupOnly(string key)
        {
            switch (key)
            {
                case ReceiveShadows:
                case CastShadows:
                case HairShadow:
                case HairShadowMode:
                case HairShadowOffsetX:
                case HairShadowOffsetY:
                case HairShadowResolution:
                case HairShadowSoftness:
                case FaceHalfShadow:
                case FaceHalfAngle:
                case FaceFullAngle:
                case FaceHalfFadeWidth:
                case FaceHalfSoftness:
                case FaceFrontAngle:
                case FaceFrontShadowStrength:
                case HighlightScale:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>key の項目を group のタブに出すか。部位専用でない項目はどのグループにも出す</summary>
        public static bool AppliesTo(string key, PartGroup group)
        {
            switch (key)
            {
                case ReceiveShadows:
                case CastShadows:
                    return ToonMaterialClassifier.UsesRendererShadow(group);
                case HairShadow:
                case HairShadowMode:
                case HairShadowOffsetX:
                case HairShadowOffsetY:
                case HairShadowResolution:
                case HairShadowSoftness:
                    return ToonMaterialClassifier.UsesHairShadow(group);
                case FaceHalfShadow:
                case FaceHalfAngle:
                case FaceFullAngle:
                case FaceHalfFadeWidth:
                case FaceHalfSoftness:
                case FaceFrontAngle:
                case FaceFrontShadowStrength:
                    return ToonMaterialClassifier.UsesFaceHalfShadow(group);
                case HighlightScale:
                    return ToonMaterialClassifier.UsesHighlightScale(group);
                default:
                    return true;
            }
        }
    }
}
