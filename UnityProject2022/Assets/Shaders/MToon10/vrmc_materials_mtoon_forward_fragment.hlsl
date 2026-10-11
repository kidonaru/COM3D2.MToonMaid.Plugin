#ifndef VRMC_MATERIALS_MTOON_FORWARD_FRAGMENT_INCLUDED
#define VRMC_MATERIALS_MTOON_FORWARD_FRAGMENT_INCLUDED

#include "./vrmc_materials_mtoon_render_pipeline.hlsl"
#include "./vrmc_materials_mtoon_define.hlsl"
#include "./vrmc_materials_mtoon_utility.hlsl"
#include "./vrmc_materials_mtoon_input.hlsl"
#include "./vrmc_materials_mtoon_attribute.hlsl"
#include "./vrmc_materials_mtoon_geometry_uv.hlsl"
#include "./vrmc_materials_mtoon_geometry_alpha.hlsl"
#include "./vrmc_materials_mtoon_geometry_normal.hlsl"
#include "./vrmc_materials_mtoon_lighting_unity.hlsl"
#include "./vrmc_materials_mtoon_lighting_mtoon.hlsl"

half4 MToonFragment(const FragmentInput fragmentInput) : SV_Target
{
    if (MToon_IsOutlinePass() && MToon_IsOutlineModeDisabled())
    {
        clip(-1);
    }

    const Varyings input = fragmentInput.varyings;
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // Get MToon UV (with UVAnimation)
    const float2 uv = GetMToonGeometry_Uv(input.uv);

    // Get LitColor with Alpha
    half4 litColor = MTOON_SAMPLE_TEXTURE2D(_MainTex, uv) * _Color;
    half3 scleraColor = 0;
    bool isSclera = false;
    // アウトラインのパスでも塗り替えると、白目の縁の線の色まで変わる
    #if !defined(MTOON_PASS_OUTLINE)
    isSclera = MToon_GetScleraColor(uv, scleraColor);
    if (isSclera)
    {
        litColor.rgb = scleraColor * _Color.rgb;
    }
    #endif

    // Alpha Test
    const half alpha = GetMToonGeometry_Alpha(litColor);

    // Get Normal
    const float3 normalWS = GetMToonGeometry_Normal(input, fragmentInput.facing, uv);

    // Get Unity Lighting
    const UnityLighting unityLighting = GetUnityLighting(input, normalWS);

    // Get MToon Lighting
    MToonInput mtoonInput;
    mtoonInput.uv = uv;
    mtoonInput.positionWS = input.positionWS;
    mtoonInput.normalWS = normalWS;
    mtoonInput.viewDirWS = normalize(input.viewDirWS);
    // COM3D2: Toony のランプの光側の明るさに揃える (アルファには掛けない)
    mtoonInput.litColor = litColor.rgb * _ToonRampLitColor.rgb;
    mtoonInput.baseColor = litColor.rgb;
    mtoonInput.alpha = alpha;
    mtoonInput.isSclera = isSclera;
    mtoonInput.scleraColor = scleraColor;
    half4 col = GetMToonLighting(unityLighting, mtoonInput);

    #if defined(MTOON_URP) && defined(_ADDITIONAL_LIGHTS) && !defined(MTOON_PASS_OUTLINE)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if defined(USE_FORWARD_PLUS)
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.pos);
    inputData.positionWS = input.positionWS;
    LIGHT_LOOP_BEGIN(pixelLightCount)
        UnityLighting additionalUnityLighting = GetAdditionalUnityLighting(input, normalWS, lightIndex);
        col.rgb += GetMToonURPAdditionalLighting(additionalUnityLighting, mtoonInput).rgb;
    LIGHT_LOOP_END
    #else
    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
    {
        UnityLighting additionalUnityLighting = GetAdditionalUnityLighting(input, normalWS, lightIndex);
        col.rgb += GetMToonURPAdditionalLighting(additionalUnityLighting, mtoonInput).rgb;
    }
    #endif
    #endif

    // Apply Fog
    #ifdef MTOON_URP
    float fogCoord = input.fogFactorAndVertexLight.x;
    col.rgb = MixFog(col.rgb, fogCoord);
    #else
    UNITY_APPLY_FOG(fragmentInput.varyings.fogCoord, col);
    #endif

    return col;
}

#endif