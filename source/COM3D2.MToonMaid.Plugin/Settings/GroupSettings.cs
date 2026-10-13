using System;
using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 部位グループ 1 つ分、または共通の調整値。フィールド初期値が XML に欠けた項目の既定値になる。
    /// 参照型 (色) は null のまま読み、MaidSettings.FillMissing で補う
    /// </summary>
    public class GroupSettings
    {
        // 陰影
        public ColorData shadeColor;
        // 主光源の陰影の濃さ (0〜MaxShadeStrength)。0 で光側一色、1 より上は影側を濃くする。追加ライトには効かない
        public float shadeStrength = 1.5f;
        // 主光源の陰影の境界をゲームの _ShadowRateToon で決める。ON の間、shadingToony は追加ライトにだけ効く
        public bool useGameShadowRate = true;
        // 境界をずらす量 (N·L に足す、-1〜1)。プラスで光側が広がる。ゲームの境界と MToon の境界の両方に足す
        public float shadingShift;
        public float shadingToony = 0.95f;
        public float giEqualization = 0.9f;
        // 追加ライト (ForwardAdd) の影側の明るさ (光側に対する倍率)。0 なら影側に当てず、1 なら光側と同じ明るさ (色だけ影色)
        public float additionalLightShade = 1f;
        // ゲームのランプを読むとき、主光源の影側 (_ShadowRateToon が 0 の側) をランプの最も暗い色一色で塗る
        public bool flatShadowRamp;
        // 前髪 (hairF) が顔へ落とす影の濃さ (0〜1)。0 なら影マップを作らない。顔にだけ効く
        public float hairShadow = 1f;
        public HairShadowMode hairShadowMode = HairShadowMode.Fixed;
        // 影をずらす量 (m、±MaxHairShadowOffset)。X はメイドから見て右、Y は頭の上がプラス
        public float hairShadowOffsetX = 0.004f;
        public float hairShadowOffsetY;
        // 影マップの一辺の画素数。HairShadowProjection.Resolutions のどれか (使うときに丸める)
        public int hairShadowResolution = 2048;
        // 影の縁のぼかしの幅 (影マップの画素数、0〜MaxHairShadowSoftness)
        public float hairShadowSoftness = 1f;
        // 顔 (head スロット) の Renderer の影を受ける・落とす
        public bool receiveShadows;
        public bool castShadows = true;
        // 顔の自分の影を N·L ではなく主光源の水平の角度で「影なし → 光と反対側の半面 → 全面」と切り替える。顔にだけ効く
        public bool faceHalfShadow = true;
        // 頭の正面から光までの水平の角度 (度) が、これを超えると半面・全面が影になる
        public float faceHalfAngle = 80f;
        public float faceFullAngle = 110f;
        // 切り替えをなめらかにする角度の幅 (度)。0 で一瞬で切り替わる
        public float faceHalfFadeWidth = 50f;
        // 左右の半面の境目のぼかしの幅 (m)
        public float faceHalfSoftness = 0.005f;
        // 顔の正面から法線までの角度 (度)。これより外側は N·L の影にする
        public float faceFrontAngle = 60f;
        // 正面の範囲の半面・全面の影の濃さ。0 で消え、N·L の影・落ち影は変わらない
        public float faceFrontShadowStrength = 1f;

        public const float MaxHairShadowOffset = 0.02f;
        public const float MaxHairShadowSoftness = 3f;
        public const float MaxShadeStrength = 2f;
        public const float MaxFaceHalfAngle = 180f;
        public const float MaxFaceHalfFadeWidth = 90f;
        public const float MaxFaceHalfSoftness = 0.02f;
        public const float MaxFaceFrontAngle = 180f;
        public const float MaxFaceFrontShadowStrength = 1f;

        // 天使の輪 (Toony の髪のハイライト) の強さ。_HiTex を持たないマテリアルには効かない
        public float highlightScale = 1f;

        // リム。上書きしなければゲームの _RimColor を使う。鋭さ・持ち上げはゲームの _RimPower / _RimShift への倍率・加算
        public bool rimColorOverride;
        public ColorData rimColor;
        public float rimFresnelPowerScale = 1f;
        public float rimLiftOffset;
        public float rimLightingMix = 1f;

        // アウトライン。元のシェーダーにアウトラインがあるマテリアルにだけ効く
        public bool outlineEnabled = true;
        public float outlineWidth = 0.0008f;
        public float outlineNearDistance = 0.7f;
        public float outlineLightingMix = 0.5f;
        public bool outlineColorOverride;
        public ColorData outlineColor;

        // 個別にした項目 (GroupSettingKeys)。無い項目は MaidSettings.common の値を使う。common 自身では使わない
        public List<string> overrides = new List<string>();

        public static GroupSettings CreateDefault()
        {
            var s = new GroupSettings();
            s.FillMissing();
            return s;
        }

        /// <summary>null の参照型を既定で埋める</summary>
        public void FillMissing()
        {
            if (shadeColor == null)
            {
                // 影の色味は Toony のランプから自動で入るため、ここは掛ける倍率として白を既定にする
                shadeColor = new ColorData(1f, 1f, 1f, 1f);
            }
            if (rimColor == null)
            {
                rimColor = new ColorData(0f, 0f, 0f, 1f);
            }
            if (outlineColor == null)
            {
                outlineColor = new ColorData(0f, 0f, 0f, 1f);
            }
            if (overrides == null)
            {
                overrides = new List<string>();
            }
        }

        public bool IsOverridden(string key)
        {
            return overrides.Contains(key);
        }

        /// <summary>key の項目を from から写す。色は参照を共有しないよう成分を写す (ウィンドウが成分を書き換えるため)</summary>
        public void CopyField(string key, GroupSettings from)
        {
            switch (key)
            {
                case GroupSettingKeys.ShadeColor: CopyColor(from.shadeColor, shadeColor); break;
                case GroupSettingKeys.ShadeStrength: shadeStrength = from.shadeStrength; break;
                case GroupSettingKeys.GameShadowRate: useGameShadowRate = from.useGameShadowRate; break;
                case GroupSettingKeys.ShadingShift: shadingShift = from.shadingShift; break;
                case GroupSettingKeys.ShadingToony: shadingToony = from.shadingToony; break;
                case GroupSettingKeys.GiEqualization: giEqualization = from.giEqualization; break;
                case GroupSettingKeys.AdditionalLightShade: additionalLightShade = from.additionalLightShade; break;
                case GroupSettingKeys.FlatShadowRamp: flatShadowRamp = from.flatShadowRamp; break;
                case GroupSettingKeys.HairShadow: hairShadow = from.hairShadow; break;
                case GroupSettingKeys.HairShadowMode: hairShadowMode = from.hairShadowMode; break;
                case GroupSettingKeys.HairShadowOffsetX: hairShadowOffsetX = from.hairShadowOffsetX; break;
                case GroupSettingKeys.HairShadowOffsetY: hairShadowOffsetY = from.hairShadowOffsetY; break;
                case GroupSettingKeys.HairShadowResolution: hairShadowResolution = from.hairShadowResolution; break;
                case GroupSettingKeys.HairShadowSoftness: hairShadowSoftness = from.hairShadowSoftness; break;
                case GroupSettingKeys.HighlightScale: highlightScale = from.highlightScale; break;
                case GroupSettingKeys.RimColor:
                    rimColorOverride = from.rimColorOverride;
                    CopyColor(from.rimColor, rimColor);
                    break;
                case GroupSettingKeys.RimFresnelPowerScale: rimFresnelPowerScale = from.rimFresnelPowerScale; break;
                case GroupSettingKeys.RimLiftOffset: rimLiftOffset = from.rimLiftOffset; break;
                case GroupSettingKeys.RimLightingMix: rimLightingMix = from.rimLightingMix; break;
                case GroupSettingKeys.OutlineEnabled: outlineEnabled = from.outlineEnabled; break;
                case GroupSettingKeys.OutlineWidth: outlineWidth = from.outlineWidth; break;
                case GroupSettingKeys.OutlineNearDistance: outlineNearDistance = from.outlineNearDistance; break;
                case GroupSettingKeys.OutlineLightingMix: outlineLightingMix = from.outlineLightingMix; break;
                case GroupSettingKeys.OutlineColor:
                    outlineColorOverride = from.outlineColorOverride;
                    CopyColor(from.outlineColor, outlineColor);
                    break;
                case GroupSettingKeys.ReceiveShadows: receiveShadows = from.receiveShadows; break;
                case GroupSettingKeys.CastShadows: castShadows = from.castShadows; break;
                case GroupSettingKeys.FaceHalfShadow: faceHalfShadow = from.faceHalfShadow; break;
                case GroupSettingKeys.FaceHalfAngle: faceHalfAngle = from.faceHalfAngle; break;
                case GroupSettingKeys.FaceFullAngle: faceFullAngle = from.faceFullAngle; break;
                case GroupSettingKeys.FaceHalfFadeWidth: faceHalfFadeWidth = from.faceHalfFadeWidth; break;
                case GroupSettingKeys.FaceHalfSoftness: faceHalfSoftness = from.faceHalfSoftness; break;
                case GroupSettingKeys.FaceFrontAngle: faceFrontAngle = from.faceFrontAngle; break;
                case GroupSettingKeys.FaceFrontShadowStrength: faceFrontShadowStrength = from.faceFrontShadowStrength; break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }
        }

        private static void CopyColor(ColorData from, ColorData to)
        {
            to.r = from.r;
            to.g = from.g;
            to.b = from.b;
            to.a = from.a;
        }
    }
}
