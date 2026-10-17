#ifndef VRMC_MATERIALS_MTOON_INPUT_INCLUDED
#define VRMC_MATERIALS_MTOON_INPUT_INCLUDED

#include "./vrmc_materials_mtoon_render_pipeline.hlsl"

// Textures
MTOON_DECLARE_TEX2D(_MainTex);
MTOON_DECLARE_TEX2D(_ShadowTex);
MTOON_DECLARE_TEX2D(_HiTex);
MTOON_DECLARE_TEX2D(_BumpMap);
MTOON_DECLARE_TEX2D(_ShadingShiftTex);
MTOON_DECLARE_TEX2D(_ShadowRateToon);
// COM3D2: 専用のサンプラーを作ると D3D11 の上限 (16) に近づくため、_ShadowRateToon のサンプラーで読む
UNITY_DECLARE_TEX2D_NOSAMPLER(_ToonRamp);
MTOON_DECLARE_TEX2D(_EmissionMap);
MTOON_DECLARE_TEX2D(_MatcapTex);
MTOON_DECLARE_TEX2D(_RimTex);
MTOON_DECLARE_TEX2D(_OutlineWidthTex);
MTOON_DECLARE_TEX2D(_OutlineTex);
// NOTE: "tex2d() * _Time.y" returns mediump value if sampler is half precision in Android VR platform
MTOON_DECLARE_TEX2D_FLOAT(_UvAnimMaskTex);
// COM3D2: 前髪の影。深度を比べるため float で読む
MTOON_DECLARE_TEX2D_FLOAT(_HairShadowTex);

CBUFFER_START(UnityPerMaterial)
// Vector
float4 _MainTex_ST;
float4 _HairShadowTex_TexelSize;
float4 _ShadowRateToon_TexelSize;
float4 _ToonRamp_TexelSize;
float4x4 _HairShadowMatrix;
float4 _ScleraRect;
float4 _FaceHalfCenter;
float4 _FaceHalfForward;
float4 _FaceHalfUp;
// Colors
half4 _Color;
half4 _ShadeColor;
half4 _EmissionColor;
half4 _MatcapColor;
half4 _RimColor;
half4 _OutlineColor;
half4 _ToonRampLitColor;
half4 _ToonRampDarkColor;
half4 _ScleraColor;
half4 _ScleraShadowColor;
// Floats
half _Cutoff;
half _BumpScale;
half _ShadingShiftFactor;
half _ShadingShiftTexScale;
half _ShadingToonyFactor;
half _UseShadowRateToon;
half _ShadowRateToonV;
half _ShadowRateShift;
half _ShadeStrength;
half _UseToonRamp;
half _FlatShadowRamp;
half _FaceHalfShadow;
float _FaceHalfAngle;
float _FaceFullAngle;
float _FaceHalfFadeWidth;
float _FaceFullFadeWidth;
float _FaceHalfSoftness;
float _FaceFrontAngle;
float _FaceFrontShadowStrength;
half _GiEqualization;
half _AdditionalLightShade;
half _HairShadowStrength;
half _HairShadowSoftness;
half _ScleraShadow;
half _ScleraShadowHeight;
half _ScleraShadowSoftness;
half _ScleraShadowBend;
half _ScleraShadowRoll;
half _ScleraLidFollow;
half _LidClose;
half _RimFresnelPower;
half _RimLift;
half _RimLightingMix;
half _OutlineWidth;
half _OutlineLightingMix;
half _OutlineNearDistance;
half _UseOutlineTex;
half _HiRate;
half _HiPow;
half _HiRateScale;
float _UvAnimScrollXSpeed;
float _UvAnimScrollYSpeed;
float _UvAnimRotationSpeed;
CBUFFER_END

// No Using on shader
// half _AlphaMode;
// half _TransparentWithZWrite;
// half _RenderQueueOffset;
// half _DoubleSided;
// half _OutlineWidthMode;

#endif
