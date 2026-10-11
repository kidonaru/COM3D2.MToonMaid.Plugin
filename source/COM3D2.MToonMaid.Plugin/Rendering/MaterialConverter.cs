using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>Toony → COM3D2/MToon10 の変換と、元へ戻す処理</summary>
    public sealed class MaterialConverter
    {
        private readonly ToonRampReader _rampReader;

        public Shader MToonShader { get; private set; }

        public MaterialConverter(Shader mtoonShader, ToonRampReader rampReader)
        {
            MToonShader = mtoonShader;
            _rampReader = rampReader;
        }

        /// <summary>無限色の変更などでランプの中身が変わったとき、読み直して控えへ入れる (ApplyGroup で反映される)</summary>
        public void RefreshRamp(Material m, MaterialBackup backup)
        {
            ReadRamp(m, backup);
        }

        /// <summary>menu のテクスチャ変更などで、読んだときと別の _ToonRamp に差し替わったか</summary>
        public bool IsRampReplaced(Material m, MaterialBackup backup)
        {
            return GetRampTextureId(GetRamp(m)) != backup.rampTextureId;
        }

        /// <summary>menu のテクスチャ変更などで、最後に ApplyGroup したときと _ShadowRateToon の有無が変わったか</summary>
        public bool IsShadowRatePresenceChanged(Material m, MaterialBackup backup)
        {
            return HasShadowRateToon(m) != backup.hasShadowRateToon;
        }

        public MaterialBackup Convert(Material m, PartGroup group, GroupSettings settings)
        {
            var shaderName = m.shader.name;
            var backup = new MaterialBackup
            {
                shader = m.shader,
                renderQueue = m.renderQueue,
                keywords = m.shaderKeywords,
                group = group,
                hadOutline = ToonMaterialClassifier.HasOutline(shaderName),
                outlineFromTex = ToonMaterialClassifier.UsesOutlineTex(shaderName),
                isCutout = ToonMaterialClassifier.IsCutout(shaderName),
                shadowRateToonV = ToonMaterialClassifier.ShadowRateToonV(shaderName),
                outlineWidth = m.HasProperty(MToonProps.OutlineWidth) ? m.GetFloat(MToonProps.OutlineWidth) : 0f,
                rimColor = m.HasProperty(MToonProps.RimColor) ? m.GetColor(MToonProps.RimColor) : Color.black,
                outlineColor = m.HasProperty(MToonProps.OutlineColor) ? m.GetColor(MToonProps.OutlineColor) : Color.black,
            };

            ReadRamp(m, backup);
            ReadRim(m, backup);
            var hasShadowTex = m.HasProperty(MToonProps.ShadowTex) && m.GetTexture(MToonProps.ShadowTex) != null;
            var mainTex = m.GetTexture(MToonProps.MainTex);

            try
            {
                SwitchShader(m, backup, settings);

                // 影側のテクスチャを持たないシェーダーはメインを影にも使う
                if (!hasShadowTex)
                {
                    m.SetTexture(MToonProps.ShadowTex, mainTex);
                    backup.copiedShadowTex = mainTex;
                }
            }
            catch
            {
                // シェーダーだけ MToon になった半端な状態を残さない
                RestoreUnchecked(m, backup);
                throw;
            }
            return backup;
        }

        /// <summary>
        /// ゲーム (menu の shader コマンド等) が Toony へ戻したマテリアルを再変換する。
        /// 戻すときにゲームの最新の選択へ戻れるよう、シェーダー・renderQueue・キーワードを控え直す。
        /// シェーダーの差し替えではキーワードが残るため、MToon が付けたものは除いて控える
        /// </summary>
        public void Reconvert(Material m, MaterialBackup backup, GroupSettings settings)
        {
            backup.shader = m.shader;
            backup.renderQueue = m.renderQueue;
            backup.keywords = MToonRenderState.StripOwnKeywords(m.shaderKeywords, backup.keywords);
            backup.hadOutline = ToonMaterialClassifier.HasOutline(m.shader.name);
            backup.outlineFromTex = ToonMaterialClassifier.UsesOutlineTex(m.shader.name);
            backup.isCutout = ToonMaterialClassifier.IsCutout(m.shader.name);
            backup.shadowRateToonV = ToonMaterialClassifier.ShadowRateToonV(m.shader.name);
            ReadRamp(m, backup);
            // _RimColor / _OutlineColor / _OutlineWidth は MToon も同名で書くため、読み直すと上書き後の値を控えてしまう
            ReadRim(m, backup);
            SwitchShader(m, backup, settings);
        }

        public void ApplyGroup(Material m, MaterialBackup backup, GroupSettings settings)
        {
            m.SetFloat(MToonProps.HiRateScale, settings.highlightScale);
            m.SetFloat(MToonProps.ShadingShiftFactor, settings.shadingShift);
            m.SetFloat(MToonProps.ShadingToonyFactor, settings.shadingToony);
            backup.hasShadowRateToon = HasShadowRateToon(m);
            m.SetFloat(MToonProps.UseShadowRateToon, ShadowRateParams.Use(settings.useGameShadowRate, backup.hasShadowRateToon) ? 1f : 0f);
            m.SetFloat(MToonProps.ShadowRateToonV, backup.shadowRateToonV);
            m.SetFloat(MToonProps.ShadowRateShift, settings.shadingShift);
            m.SetFloat(MToonProps.ShadeStrength, ShadowRateParams.ClampShadeStrength(settings.shadeStrength));
            var useRamp = ShadowRateParams.UseRamp(settings.useGameShadowRate, backup.rampTextureId != 0, backup.hasShadowRateToon);
            m.SetFloat(MToonProps.UseToonRamp, useRamp ? 1f : 0f);
            var rampLit = ShadowRateParams.RampLitColor(useRamp, backup.ramp);
            var rampShade = ShadowRateParams.RampShadeMultiplier(useRamp, backup.ramp);
            m.SetColor(MToonProps.ToonRampLitColor, ToColor(rampLit));
            m.SetColor(MToonProps.ShadeColor, ToColor(settings.shadeColor) * ToColor(rampShade));
            m.SetColor(MToonProps.ToonRampDarkColor, ToColor(backup.ramp.Dark));
            m.SetFloat(MToonProps.GiEqualization, settings.giEqualization);
            // 加算パスの係数のため、手編集の範囲外の値で他のライトの寄与を削らないよう抑える
            m.SetFloat(MToonProps.AdditionalLightShade, Mathf.Clamp01(settings.additionalLightShade));

            m.SetColor(MToonProps.RimColor, settings.rimColorOverride ? ToColor(settings.rimColor) : backup.rimColor);
            m.SetFloat(MToonProps.RimFresnelPower, RimParams.FresnelPower(backup.rimPower, settings.rimFresnelPowerScale));
            m.SetFloat(MToonProps.RimLift, RimParams.Lift(backup.rimShift, settings.rimLiftOffset));
            m.SetFloat(MToonProps.RimLightingMix, settings.rimLightingMix);

            var outline = backup.hadOutline && settings.outlineEnabled;
            var outlineMode = OutlineParams.Mode(outline);
            m.SetInt(MToonProps.OutlineWidthMode, outlineMode);
            m.SetFloat(MToonProps.OutlineWidth, settings.outlineWidth);
            m.SetFloat(MToonProps.OutlineNearDistance, settings.outlineNearDistance);
            m.SetFloat(MToonProps.OutlineLightingMix, settings.outlineLightingMix);
            var outlineSource = OutlineParams.Source(backup.outlineFromTex, settings.outlineColorOverride);
            m.SetFloat(MToonProps.UseOutlineTex, outlineSource == OutlineColorSource.Texture ? 1f : 0f);
            m.SetColor(MToonProps.OutlineColor,
                outlineSource == OutlineColorSource.Override ? ToColor(settings.outlineColor) : backup.outlineColor);

            // アウトラインの有無でキーワードが変わるため描画ステートも決め直す
            ApplyRenderState(m, backup, outlineMode);
        }

        /// <summary>
        /// 元へ戻す。変換後に他者 (SE の NPR 化、ゲームの Toony への巻き戻し) がシェーダーを変えていたら
        /// その選択を優先して何もしない。戻したら true
        /// </summary>
        public bool Restore(Material m, MaterialBackup backup)
        {
            if (m.shader != MToonShader)
            {
                return false;
            }
            RestoreUnchecked(m, backup);
            return true;
        }

        private static void RestoreUnchecked(Material m, MaterialBackup backup)
        {
            // 解放する影マップを Toony に残さない。強さも戻し、後で MToon にし直したとき古い行列で描かないようにする
            m.SetTexture(MToonProps.HairShadowTex, null);
            m.SetFloat(MToonProps.HairShadowStrength, 0f);
            m.shader = backup.shader;
            m.shaderKeywords = backup.keywords;
            m.SetOverrideTag(MToonProps.RenderTypeTag, "");
            m.SetFloat(MToonProps.OutlineWidth, backup.outlineWidth);
            m.SetColor(MToonProps.RimColor, backup.rimColor);
            m.SetColor(MToonProps.OutlineColor, backup.outlineColor);
            // ゲームが後から別の影テクスチャを入れていたらそれを残す
            if (backup.copiedShadowTex != null && m.GetTexture(MToonProps.ShadowTex) == backup.copiedShadowTex)
            {
                m.SetTexture(MToonProps.ShadowTex, null);
            }
            // シェーダーの代入で renderQueue は既定へ戻るため最後に書く
            m.renderQueue = backup.renderQueue;
        }

        private void SwitchShader(Material m, MaterialBackup backup, GroupSettings settings)
        {
            m.shader = MToonShader;
            // ゲームが Toony へ戻したマテリアルには前回の強さが残るため、前髪の影は次に描くまで外しておく
            m.SetFloat(MToonProps.HairShadowStrength, 0f);
            m.SetInt(MToonProps.AlphaMode, backup.isCutout ? MToonProps.AlphaModeCutout : MToonProps.AlphaModeOpaque);
            // Cutout はレースや髪の房など裏面も見える用途が多いため両面にする
            m.SetInt(MToonProps.DoubleSided, backup.isCutout ? 1 : 0);
            ApplyGroup(m, backup, settings);
            // 前髪の 980 など、ゲームが決めた描画順を保つ
            m.renderQueue = backup.renderQueue;
        }

        private static void ApplyRenderState(Material m, MaterialBackup backup, int outlineMode)
        {
            var hasParameterMap = m.GetTexture(MToonProps.ShadingShiftTex) != null
                || m.GetTexture(MToonProps.OutlineWidthTex) != null
                || m.GetTexture(MToonProps.UvAnimMaskTex) != null;
            var s = MToonRenderState.Resolve(
                backup.isCutout ? MToonProps.AlphaModeCutout : MToonProps.AlphaModeOpaque,
                backup.isCutout,
                outlineMode,
                hasParameterMap);

            m.SetOverrideTag(MToonProps.RenderTypeTag, s.renderType);
            m.SetInt(MToonProps.SrcBlend, s.srcBlend);
            m.SetInt(MToonProps.DstBlend, s.dstBlend);
            m.SetInt(MToonProps.ZWrite, s.zWrite);
            m.SetInt(MToonProps.AlphaToMask, s.alphaToMask);
            m.SetInt(MToonProps.CullMode, s.cullMode);
            foreach (var k in s.enabledKeywords)
            {
                m.EnableKeyword(k);
            }
            foreach (var k in s.disabledKeywords)
            {
                m.DisableKeyword(k);
            }
        }

        private void ReadRamp(Material m, MaterialBackup backup)
        {
            var ramp = GetRamp(m);
            backup.ramp = ramp != null ? _rampReader.Read(ramp) : ToonRampColors.White;
            backup.rampTextureId = GetRampTextureId(ramp);
            backup.rampIsRenderTexture = ramp is RenderTexture;
        }

        private static void ReadRim(Material m, MaterialBackup backup)
        {
            backup.rimPower = m.HasProperty(MToonProps.GameRimPower) ? m.GetFloat(MToonProps.GameRimPower) : RimParams.DefaultPower;
            backup.rimShift = m.HasProperty(MToonProps.GameRimShift) ? m.GetFloat(MToonProps.GameRimShift) : RimParams.DefaultShift;
        }

        /// <summary>_ToonRamp を持たない Toony もあるため、無ければ読まずに補正なし (白) とする</summary>
        private static Texture GetRamp(Material m)
        {
            return m.HasProperty(MToonProps.ToonRamp) ? m.GetTexture(MToonProps.ToonRamp) : null;
        }

        private static bool HasShadowRateToon(Material m)
        {
            return m.HasProperty(MToonProps.ShadowRateToon) && m.GetTexture(MToonProps.ShadowRateToon) != null;
        }

        private static int GetRampTextureId(Texture ramp)
        {
            return ramp != null ? ramp.GetInstanceID() : 0;
        }

        private static Color ToColor(ColorData c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
    }
}
