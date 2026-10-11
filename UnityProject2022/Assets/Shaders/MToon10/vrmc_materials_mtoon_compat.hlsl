#ifndef VRMC_MATERIALS_MTOON_COMPAT_INCLUDED
#define VRMC_MATERIALS_MTOON_COMPAT_INCLUDED

// COM3D2: Unity 5.6 に無い名前を旧名で補う (COM3D2 (2.0) 用)。
// 本体を旧名へ書き換えると、2022 で UNITY_TRANSFER_LIGHTING の lightmap UV の受け渡しが無くなり、2.5 の描画が変わる
#if UNITY_VERSION < 201700
#define UNITY_LIGHTING_COORDS(idx1, idx2) LIGHTING_COORDS(idx1, idx2)
#define UNITY_TRANSFER_LIGHTING(a, coord) TRANSFER_VERTEX_TO_FRAGMENT(a)
// 5.6 の UnpackScaleNormal で代えると、DXT5nm 前提で .wy を読むため RGB の法線マップで x が 1 に張り付き、2022 と結果が変わる
inline half3 MToon_Compat_UnpackNormalWithScale(half4 packednormal, half bumpScale)
{
#if !defined(UNITY_NO_DXT5nm)
    packednormal.x *= packednormal.w;
#endif
    half3 normal;
    normal.xy = (packednormal.xy * 2 - 1) * bumpScale;
    normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
    return normal;
}
#define UnpackNormalWithScale(packednormal, bumpScale) MToon_Compat_UnpackNormalWithScale(packednormal, bumpScale)
// _MainTex.SampleLevel を直接書くと、5.6 の DX9 形式 (sampler2D) の宣言になる API でコンパイルできない
#ifndef UNITY_SAMPLE_TEX2D_LOD
#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(UNITY_COMPILER_HLSLCC) || defined(SHADER_API_PSSL)
#define UNITY_SAMPLE_TEX2D_LOD(tex, coord, lod) tex.SampleLevel(sampler##tex, coord, lod)
#else
#define UNITY_SAMPLE_TEX2D_LOD(tex, coord, lod) tex2Dlod(tex, float4(coord, 0, lod))
#endif
#endif
#endif

#endif
