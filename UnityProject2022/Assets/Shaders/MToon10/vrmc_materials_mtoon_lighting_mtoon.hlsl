#ifndef VRMC_MATERIALS_MTOON_LIGHTING_MTOON_INCLUDED
#define VRMC_MATERIALS_MTOON_LIGHTING_MTOON_INCLUDED

#include "./vrmc_materials_mtoon_render_pipeline.hlsl"
#include "./vrmc_materials_mtoon_define.hlsl"
#include "./vrmc_materials_mtoon_utility.hlsl"
#include "./vrmc_materials_mtoon_input.hlsl"
#include "./vrmc_materials_mtoon_lighting_unity.hlsl"

struct MToonInput
{
    float2 uv;
    float3 positionWS;
    half3 normalWS;
    half3 viewDirWS;
    half3 litColor;
    // COM3D2: _MainTex × _Color。Toony の環境光はランプを掛けないため、光側の色を掛ける前の色を持つ
    half3 baseColor;
    half alpha;
    // COM3D2: 白目の範囲の内側か。内側なら影側も _ShadowTex の代わりに scleraColor を使う (_Color は掛けない)
    bool isSclera;
    half3 scleraColor;
};

// COM3D2: 白目 (口のアトラスの _ScleraRect の内側) の色。内側なら true で、白目の色に影を重ねた色 (_Color は掛けない) を返す。
// 白目の色は分岐の中で読むため、勾配の要らない LOD 0 で読む
inline bool MToon_GetScleraColor(const float2 uv, out half3 color)
{
    color = 0;
    if (_ScleraRect.z <= 0 || _ScleraRect.w <= 0)
    {
        return false;
    }
    const float2 n = (uv - _ScleraRect.xy) / _ScleraRect.zw;
    if (n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1)
    {
        return false;
    }
    // 白目の中央・下から 1 割の所は、アトラスで白く塗られている (上側は描き込みの影)
    const half3 base = _ScleraColor.a > 0
        ? _ScleraColor.rgb
        : MTOON_SAMPLE_TEXTURE2D_LOD(_MainTex, _ScleraRect.xy + _ScleraRect.zw * float2(0.5, 0.1), 0).rgb;
    const float x = 2 * n.x - 1;
    const half edge = 1 - _ScleraShadowHeight - _ScleraShadowBend * x * x - _LidClose * _ScleraLidFollow;
    const half mask = smoothstep(edge, edge + max(_ScleraShadowSoftness * _ScleraShadowHeight, 1e-4), n.y) * _ScleraShadow;
    color = lerp(base, base * _ScleraShadowColor.rgb, mask);
    return true;
}

inline half GetMToonLighting_Reflectance_ShadingShift(const MToonInput input)
{
    if (MToon_IsParameterMapOn())
    {
        return MTOON_SAMPLE_TEXTURE2D(_ShadingShiftTex, input.uv).r * _ShadingShiftTexScale + _ShadingShiftFactor;
    }
    else
    {
        return _ShadingShiftFactor;
    }
}

// COM3D2: 実測した Toony の陰影の座標。u = N·L * 0.5 + 0.5 で、通常は (u, u)、Muscle は (u, 0.75)。_ShadowRateToon とランプが同じ座標を読む
inline float2 GetMToonLighting_ToonUvFromU(const float u)
{
    return float2(u, _ShadowRateToonV < 0 ? u : _ShadowRateToonV);
}

inline float2 GetMToonLighting_ToonUv(const half dotNL)
{
    return GetMToonLighting_ToonUvFromU(saturate((dotNL + _ShadowRateShift) * 0.5 + 0.5));
}

// 半テクセルより外を読むと、Repeat のランプは反対の端の画素と混ざる
inline float2 MToon_ClampToTexels(const float2 uv, const float4 texelSize)
{
    const float2 halfTexel = texelSize.xy * 0.5;
    return clamp(uv, halfTexel, 1 - halfTexel);
}

// R が _ShadowTex → _MainTex の割合。座標が N·L で飛ぶため、ミップの選択を避けて SampleLevel で読む
inline half GetMToonLighting_ShadowRate(const half dotNL)
{
    const float2 uv = MToon_ClampToTexels(GetMToonLighting_ToonUv(dotNL), _ShadowRateToon_TexelSize);
    return _ShadowRateToon.SampleLevel(sampler_ShadowRateToon, uv, 0).r;
}

inline half GetMToonLighting_DotNL(const UnityLighting lighting, const MToonInput input)
{
    return dot(input.normalWS, lighting.directLightDirection);
}

// COM3D2: 前髪の影。プラグインが前髪だけを光の向きから投影した深度 (_HairShadowTex) と比べ、前髪の陰の割合を返す。
// 書き込み側も同じ _HairShadowMatrix で深度を出すため、逆 Z の差を受けない。
// 深度は頭の半径 × 1.2 の立方体を 0〜1 にした値で、0.004 は約 2mm (ぼかし 0 のときの値)。顔が自分の前髪の深度と重なる所で影が出ないよう前へずらす
#define MTOON_HAIR_SHADOW_BIAS 0.004

// 深度そのものを双線形で読むと、前髪の縁で手前と奥の深度が混ざって影が浮く。比較してから混ぜる。
// 分岐の中で読むため、勾配の要らない LOD 0 で読む (影マップはミップマップなし)
inline half SampleHairShadowPcf(const float2 uv, const float d)
{
    const float2 texel = _HairShadowTex_TexelSize.xy;
    const float2 st = uv * _HairShadowTex_TexelSize.zw - 0.5;
    const float2 f = frac(st);
    const float2 base = (floor(st) + 0.5) * texel;
    const half s00 = step(_HairShadowTex.SampleLevel(sampler_HairShadowTex, base, 0).r, d);
    const half s10 = step(_HairShadowTex.SampleLevel(sampler_HairShadowTex, base + float2(texel.x, 0), 0).r, d);
    const half s01 = step(_HairShadowTex.SampleLevel(sampler_HairShadowTex, base + float2(0, texel.y), 0).r, d);
    const half s11 = step(_HairShadowTex.SampleLevel(sampler_HairShadowTex, base + texel, 0).r, d);
    return lerp(lerp(s00, s10, f.x), lerp(s01, s11, f.x), f.y);
}

inline half GetMToonLighting_HairShadow(const MToonInput input)
{
    if (_HairShadowStrength <= 0 || MToon_IsOutlinePass())
    {
        return 0;
    }
    const float3 p = mul(_HairShadowMatrix, float4(input.positionWS, 1)).xyz;
    // 投影の立方体の外は前髪を描いていない。奥側 (z > 1) も、クリアした 1 と比べて影になるのを防ぐ
    if (any(p.xy < 0) || any(p.xy > 1) || p.z > 1)
    {
        return 0;
    }
    const float2 texel = _HairShadowTex_TexelSize.xy;
    // 増やす分を固定値にすると、高い解像度では前髪が顔に近い所の影まで抜け、低い解像度では前髪が食い込む所で足りない。
    // 深度は xyz を同じ倍率で写しているため、ぼかしで離れる距離 (テクセル) をそのまま深度の誤差の上限 (45°) とみなす
    const float d = p.z - (MTOON_HAIR_SHADOW_BIAS + _HairShadowSoftness * texel.x);
    if (_HairShadowSoftness <= 0)
    {
        return SampleHairShadowPcf(p.xy, d) * _HairShadowStrength;
    }
    // PCF 1 回だとぼかしの幅を 1 画素より広げられない。読む点の間隔を 1 画素より広げると、点の間に平らな帯ができて縁が二重に見える
    const int n = min((int)ceil(_HairShadowSoftness) + 1, 4);
    const float2 spacing = texel * (_HairShadowSoftness / (n - 1));
    const float2 start = p.xy - texel * (_HairShadowSoftness * 0.5);
    half s = 0;
    [loop]
    for (int y = 0; y < n; y++)
    {
        [loop]
        for (int x = 0; x < n; x++)
        {
            s += SampleHairShadowPcf(start + float2(x, y) * spacing, d);
        }
    }
    return s / (n * n) * _HairShadowStrength;
}

// COM3D2: 陰影の濃さ 0 のときのランプの色 (u = 1)。影側の色 (_ToonRampDarkColor) の逆として固定の白にすると、
// ランプの光側が暗い髪 (黒髪で 0.58) が明るくなりすぎる
inline half3 GetMToonLighting_ToonRampLit()
{
    const float2 uv = MToon_ClampToTexels(GetMToonLighting_ToonUvFromU(1), _ToonRamp_TexelSize);
    return _ToonRamp.SampleLevel(sampler_ShadowRateToon, uv, 0).rgb;
}

// COM3D2: ランプを N·L の座標だけで読むと、落ち影と前髪の影の中が光側のままで、2 色の近似 (OFF) より明るく色も抜ける。
// _ShadowRateToon の影側まで寄せると、自分の影が落ちる裏側でランプの境界の帯が消える。
// アウトラインは線の後ろの面の落ち影を拾うため、前髪の影と同じく見ない

inline half3 GetMToonLighting_ToonRamp(const UnityLighting lighting, const MToonInput input, const half dotNL, const half hairShadow, const half shadeStrength)
{
    const float2 uv = MToon_ClampToTexels(GetMToonLighting_ToonUv(dotNL), _ToonRamp_TexelSize);
    const half3 ramp = _ToonRamp.SampleLevel(sampler_ShadowRateToon, uv, 0).rgb;
    const half castShadow = MToon_IsOutlinePass() ? 1 : lighting.directLightAttenuation.x;
    const half unshadowed = lerp(1, castShadow * (1 - hairShadow), GetMToonLighting_ShadowRate(dotNL));
    return max(0, lerp(GetMToonLighting_ToonRampLit(), lerp(_ToonRampDarkColor.rgb, ramp, unshadowed), shadeStrength));
}

inline half GetMToonLighting_Shade(const UnityLighting lighting, const MToonInput input, const half dotNL, const half hairShadow)
{
    const half shadeShift = GetMToonLighting_Reflectance_ShadingShift(input);
    const half shadeToony = _ShadingToonyFactor;

    if (MToon_IsPbrCorrectOn())
    {
        const half shadeInput = dotNL;
        return mtoon_linearstep(-1.0 + shadeToony, +1.0 - shadeToony, shadeInput + shadeShift);
    }

    if (MToon_IsForwardBasePass())
    {
        const half shadeInput = lerp(-1, 1, mtoon_linearstep(-1, 1, dotNL));
        // [branch] の if で OFF のときの読み込みを省こうとすると、シェーダーコンパイラが内部エラーで止まる
        const half toonShade = _UseShadowRateToon > 0.5
            ? GetMToonLighting_ShadowRate(dotNL)
            : mtoon_linearstep(-1.0 + shadeToony, +1.0 - shadeToony, shadeInput + shadeShift);
        return toonShade * lighting.directLightAttenuation.x * (1 - hairShadow);
    }
    else
    {
        const half shadeInput = dotNL;
        return mtoon_linearstep(-1.0 + shadeToony, +1.0 - shadeToony, shadeInput + shadeShift);
    }
}

// COM3D2: 追加ライトは本家の「半分の明るさ + 影側を N・L で暗くする」をやめ、
// 光側は減衰だけ、影側は _AdditionalLightShade 倍で平らに照らす (追加ライトだけの演出でもトゥーンの段差を出す)
inline half GetMToonLighting_Shadow(const UnityLighting lighting, const half dotNL, const half shade)
{
    if (MToon_IsPbrCorrectOn())
    {
        return lighting.directLightAttenuation.x * step(0, dotNL);
    }

    if (MToon_IsForwardBasePass())
    {
        return 1;
    }
    else
    {
        return lighting.directLightAttenuation.x * lerp(_AdditionalLightShade, 1, shade);
    }
}

// COM3D2: Toony の髪のハイライト (天使の輪)。実測した Toony の式:
//   _HiTex.rgb(uv) * _HiTex.a(uv) * _HiRate * pow(saturate(N.H), _HiPow) をアルベドへ加算してから陰影を掛ける (影側にも掛かる)
//   無限色の _HiTex は RGB が一様な髪色で、輪の形はアルファにある
// Toony のアウトラインは単色なので、アウトラインのパスでは足さない
inline half3 GetMToonLighting_Highlight(const UnityLighting unityLight, const MToonInput input)
{
    const half rate = _HiRate * _HiRateScale;
    if (rate <= 0 || MToon_IsOutlinePass())
    {
        return 0;
    }
    // 逆光ではライトと視線の和がほぼ 0 になり、half の normalize が NaN になるため長さの下限を設ける
    const float3 halfSum = (float3)unityLight.directLightDirection + (float3)input.viewDirWS;
    const float3 halfDir = halfSum * rsqrt(max(dot(halfSum, halfSum), 1e-4));
    const half dotNH = saturate(dot(input.normalWS, halfDir));
    const half4 hiTex = MTOON_SAMPLE_TEXTURE2D(_HiTex, input.uv);
    return hiTex.rgb * hiTex.a * rate * pow(dotNH, max(_HiPow, 0.001));
}

// COM3D2: shadeDepth は 1 で元の色、1 より大きいと影側の色が濃くなる (追加ライトは 1 を渡す)。
// ハイライトまで外挿すると、影色が光側の半分より暗いとき影の中の天使の輪が暗い帯に反転する
inline half3 GetMToonLighting_DirectLighting(const UnityLighting unityLight, const MToonInput input, const half shade, const half shadow, const half3 rampColor, const half shadeDepth)
{
    const half3 highlight = GetMToonLighting_Highlight(unityLight, input);
    const half3 litColor = input.litColor + highlight * _ToonRampLitColor.rgb;
    const half3 shadowTex = input.isSclera ? input.scleraColor : MTOON_SAMPLE_TEXTURE2D(_ShadowTex, input.uv).rgb;
    const half3 shadeBase = max(0, lerp(input.litColor, shadowTex * _ShadeColor.rgb, shadeDepth));
    const half3 shadeColor = shadeBase + highlight * _ShadeColor.rgb;
    const half3 albedo = lerp(shadeColor, litColor, shade);

    return albedo * rampColor * unityLight.directLightColor * shadow;
}

inline half3 GetMToonLighting_GlobalIllumination(const UnityLighting unityLight, const MToonInput input)
{
    if (MToon_IsForwardBasePass())
    {
        return input.baseColor * lerp(unityLight.indirectLight, unityLight.indirectLightEqualized, _GiEqualization);
    }
    else
    {
        return 0;
    }
}

inline half3 GetMToonLighting_Emissive(const MToonInput input)
{
    if (MToon_IsForwardBasePass())
    {
        if (MToon_IsEmissiveMapOn())
        {
            return MTOON_SAMPLE_TEXTURE2D(_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
        }
        return _EmissionColor.rgb;
    }
    return 0;
}

inline half3 GetMToonLighting_Rim_Matcap(const MToonInput input)
{
    if (MToon_IsRimMapOn())
    {
        const half3 worldUpWS = half3(0, 1, 0);
        // TODO: use view space axis if abs(dot(viewDir, worldUp)) == 1.0
        const half3 matcapRightAxisWS = normalize(cross(input.viewDirWS, worldUpWS));
        const half3 matcapUpAxisWS = normalize(cross(matcapRightAxisWS, input.viewDirWS));
        const half2 matcapUv = float2(dot(matcapRightAxisWS, input.normalWS), dot(matcapUpAxisWS, input.normalWS)) * 0.5 + 0.5;
        
        return _MatcapColor.rgb * MTOON_SAMPLE_TEXTURE2D(_MatcapTex, matcapUv).rgb;
    }
    return 0;
}

// COM3D2: 影側のリムに _ShadeColor の色を混ぜると、Toony では消える影側のリムが光って見える
// 本家の主光源のパス (shadow のみ) だと影の係数が常に 1 で、影側にも光側と同じ明るさのリムが出てゲームより明るくなる
inline half3 GetMToonLighting_Rim(const UnityLighting unityLight, const MToonInput input, const half shade, const half shadow, const half3 rampColor)
{
    const half3 parametricRimFactor = pow(saturate(1.0 - dot(input.normalWS, input.viewDirWS) + _RimLift), max(_RimFresnelPower, EPSILON_FP16)) * _RimColor.rgb;
    const half3 matcapFactor = GetMToonLighting_Rim_Matcap(input);
    const half3 directLightingFactor = unityLight.directLightColor * shadow;

    half3 rimLightingFactor;
    if (MToon_IsForwardBasePass())
    {
        const half3 indirectLightingFactor = unityLight.indirectLight;
        const half3 toonyDirect = directLightingFactor * shade * _ToonRampLitColor.rgb * rampColor;

        rimLightingFactor = lerp(half3(1, 1, 1), toonyDirect + indirectLightingFactor, _RimLightingMix);
    }
    else
    {
        rimLightingFactor = lerp(half3(0, 0, 0), directLightingFactor, _RimLightingMix);
    }

    if (MToon_IsRimMapOn())
    {
        return (matcapFactor + parametricRimFactor) * rimLightingFactor * MTOON_SAMPLE_TEXTURE2D(_RimTex, input.uv).rgb;
    }
    return (matcapFactor + parametricRimFactor) * rimLightingFactor;
}

half4 GetMToonLighting(const UnityLighting unityLight, const MToonInput input)
{
    const half dotNL = GetMToonLighting_DotNL(unityLight, input);
    const half hairShadow = MToon_IsForwardBasePass() ? GetMToonLighting_HairShadow(input) : 0;
    // COM3D2: 追加ライトの影側は _AdditionalLightShade で決めるため、陰影の濃さは ForwardBase だけに掛ける
    const half shadeStrength = MToon_IsForwardBasePass() ? _ShadeStrength : 1;
    // 1 より上で陰影の割合を外挿すると、影が濃くならずに境界が光側へ広がる
    const half shade = lerp(1, GetMToonLighting_Shade(unityLight, input, dotNL, hairShadow), min(shadeStrength, 1));
    const half shadeDepth = max(shadeStrength, 1);
    const half shadow = GetMToonLighting_Shadow(unityLight, dotNL, shade);
    // COM3D2: アウトラインのパスも ForwardBase なので、線に混ぜる陰影にもランプが掛かる (環境光・追加ライトには掛けない)
    const half3 rampColor = MToon_IsForwardBasePass() && _UseToonRamp > 0.5 ? GetMToonLighting_ToonRamp(unityLight, input, dotNL, hairShadow, shadeStrength) : half3(1, 1, 1);

    const half3 direct = GetMToonLighting_DirectLighting(unityLight, input, shade, shadow, rampColor, shadeDepth);
    const half3 indirect = GetMToonLighting_GlobalIllumination(unityLight, input);
    const half3 lighting = direct + indirect;
    const half3 emissive = GetMToonLighting_Emissive(input);
    const half3 rim = GetMToonLighting_Rim(unityLight, input, shade, shadow, rampColor);

    const half3 baseCol = lighting + emissive + rim;

    if (MToon_IsOutlinePass())
    {
        // COM3D2: ゲームの *_Outline_Tex 系は _OutlineColor ではなく _OutlineTex の色で線を描く
        // _OutlineColor を白にしてテクスチャへ掛ける形にすると、他者がシェーダーを差し替えたとき白い線が残る
        const half3 lineCol = lerp(_OutlineColor.rgb, MTOON_SAMPLE_TEXTURE2D(_OutlineTex, input.uv).rgb, _UseOutlineTex);
        const half3 outlineCol = lineCol * lerp(half3(1, 1, 1), baseCol, _OutlineLightingMix);
        return half4(outlineCol, input.alpha);
    }
    return half4(baseCol, input.alpha);
}

#ifdef MTOON_URP

inline half GetMToonURPAdditionalLighting_Shadow(const UnityLighting lighting, const half dotNL)
{
    if (MToon_IsPbrCorrectOn())
    {
        return lighting.directLightAttenuation.x * step(0, dotNL);
    }

    return lighting.directLightAttenuation.x * 0.5 * (min(0, dotNL) + 1);
}

inline half GetMToonURPAdditionalLighting_Shade(const UnityLighting lighting, const MToonInput input, const half dotNL)
{
    const half shadeShift = GetMToonLighting_Reflectance_ShadingShift(input);
    const half shadeToony = _ShadingToonyFactor;

    if (MToon_IsPbrCorrectOn())
    {
        const half shadeInput = dotNL;
        return mtoon_linearstep(-1.0 + shadeToony, +1.0 - shadeToony, shadeInput + shadeShift);
    }

    const half shadeInput = dotNL;
    return mtoon_linearstep(-1.0 + shadeToony, +1.0 - shadeToony, shadeInput + shadeShift);
}

half4 GetMToonURPAdditionalLighting(const UnityLighting unityLight, const MToonInput input)
{
    const half dotNL = GetMToonLighting_DotNL(unityLight, input);
    const half shade = GetMToonURPAdditionalLighting_Shade(unityLight, input, dotNL);
    const half shadow = GetMToonURPAdditionalLighting_Shadow(unityLight, dotNL);

    const half3 direct = GetMToonLighting_DirectLighting(unityLight, input, shade, shadow, half3(1, 1, 1), 1);
    const half3 lighting = direct;
    
    const half3 baseCol = lighting;

    return half4(baseCol, input.alpha);
}

#endif


#endif
