// UniVRM (https://github.com/vrm-c/UniVRM) の VRM10/MToon10 を COM3D2 / COM3D2.5 向けに改変 (MIT License)
// - シェーダー名を COM3D2/MToon10 に変更
// - _ShadeTex をゲームの Toony シェーダーと同じ _ShadowTex へ改名
// - Toony の _ToonRamp の光側の色を掛ける _ToonRampLitColor を追加
// - Toony の髪のハイライト (天使の輪: _HiTex / _HiRate / _HiPow、倍率 _HiRateScale) を追加
// - 追加ライト (ForwardAdd) の減衰を、光側は減衰だけ・影側は _AdditionalLightShade 倍の平らな明るさに変更
// - プラグインが前髪だけを投影した深度 (_HairShadowTex / _HairShadowMatrix / _HairShadowStrength) で、ForwardBase の陰影に前髪の影を掛ける。_HairShadowStrength が 1 より上の分は、前髪の影の中だけ _ShadeStrength を掛け増して影を濃くする
// - _UseOutlineTex が 1 のとき、輪郭の色を _OutlineColor ではなくゲームの線用テクスチャ (_OutlineTex) から取る
// - _UseShadowRateToon が 1 のとき、ForwardBase の陰影をゲームの _ShadowRateToon (Toony と同じ N·L の座標) で決める
// - _UseToonRamp が 1 のとき、ゲームの _ToonRamp を _ShadowRateToon と同じ座標で読み、主光源の分とリムに掛ける。環境光にはランプの光側の色を掛けない
// - _FlatShadowRamp が 1 のとき、主光源の影側ではランプを最も暗い色 (_ToonRampDarkColor) 一色にする (影の中に N·L のグラデーションを出さない)
// - _FaceHalfShadow が 1 で頭の向き (_FaceHalfForward) が入っている間、ForwardBase の自分の影を N·L ではなく主光源の水平の角度で「影なし → 光と反対側の半面 → 全面」と切り替える。法線が顔の正面から _FaceFrontAngle より外れた面 (顎の下・耳など) は N·L の影へつなぐ。正面の範囲の半面・全面の影の濃さは _FaceFrontShadowStrength で決める
// - _ShadeStrength で ForwardBase の陰影 (落ち影・前髪の影・ランプの暗さを含む) を光側へ寄せる。1 より上は影側の色を光側から遠ざけて濃くする
// - ワールド基準の輪郭の幅に min(ビュー深度, _OutlineNearDistance) を掛け、近くでは画面上の太さを頭打ちにする (ゲームの Toony の線と同じ)
// - ワールド基準の輪郭の幅に、正規化しない法線を逆転置行列で写した長さを掛ける (ゲームの Toony の線と同じく、スキンメッシュはボーンのスケールで太く、MeshRenderer は拡大で細くなる)
// - COM3D2 (2.0) の Unity 5.6 でもコンパイルできるよう、URP の include と URP 専用の depth 系のファイルを消し、5.6 に無いマクロを vrmc_materials_mtoon_compat.hlsl で補う
// - ForwardBase / ForwardAdd のパスで UNITY_PASS_FORWARDBASE / UNITY_PASS_FORWARDADD が未定義なら定義する (Unity 5.6 は LightMode から定義しない)
// - _ScleraRect の内側 (口のアトラスの白目) は、テクスチャの代わりに白目の色へ _ScleraShadow* の影を重ねた色で描く (光側・影側とも)
Shader "COM3D2/MToon10"
{
    Properties
    {
        // Rendering
        _AlphaMode ("alphaMode", Int) = 0
        _TransparentWithZWrite ("mtoon.transparentWithZWrite", Int) = 0
        _Cutoff ("alphaCutoff", Range(0, 1)) = 0.5 // Unity specified name
        _RenderQueueOffset ("mtoon.renderQueueOffsetNumber", Int) = 0
        _DoubleSided ("doubleSided", Int) = 0

        // Lighting
        _Color ("pbrMetallicRoughness.baseColorFactor", Color) = (1, 1, 1, 1) // Unity specified name
        _MainTex ("pbrMetallicRoughness.baseColorTexture", 2D) = "white" {} // Unity specified name
        _ShadeColor ("mtoon.shadeColorFactor", Color) = (1, 1, 1, 1)
        _ShadowTex ("mtoon.shadeMultiplyTexture", 2D) = "white" {}
        // COM3D2: ゲームの Toony のランプ。ゲームと同名で、シェーダーを差し替えても値 (無限色の RenderTexture を含む) が引き継がれる
        _ToonRamp ("Toon Ramp", 2D) = "white" {}
        // COM3D2: 1 なら _ToonRamp を読んで主光源の分に掛ける (プラグインが設定する)
        _UseToonRamp ("Use Toon Ramp", Float) = 0
        // COM3D2: Toony の _ToonRamp の光側の色 (プラグインが設定する)
        _ToonRampLitColor ("ToonRamp Lit Color", Color) = (1, 1, 1, 1)
        // COM3D2: _ToonRamp の最も暗い画素の色。前髪の影の分をこの色へ寄せる (プラグインが設定する)
        _ToonRampDarkColor ("ToonRamp Dark Color", Color) = (1, 1, 1, 1)
        // COM3D2: 1 なら主光源の影側 (_ShadowRateToon が 0 の側) でランプを N·L で読まず _ToonRampDarkColor 一色にする (プラグインが設定する)
        _FlatShadowRamp ("Flat Shadow Ramp", Float) = 0
        // COM3D2: Toony の髪のハイライト。_HiTex / _HiRate / _HiPow はゲームと同名で、シェーダーを差し替えても値が引き継がれる
        _HiTex ("Highlight Texture", 2D) = "black" {}
        _HiRate ("Highlight Rate", Float) = 0
        _HiPow ("Highlight Power", Float) = 0.001
        _HiRateScale ("Highlight Rate Scale", Float) = 1
        [Normal] _BumpMap ("normalTexture", 2D) = "bump" {} // Unity specified name
        _BumpScale ("normalTexture.scale", Float) = 1.0 // Unity specified name
        _ShadingShiftFactor ("mtoon.shadingShiftFactor", Range(-1, 1)) = -0.05
        _ShadingShiftTex ("mtoon.shadingShiftTexture", 2D) = "black" {} // channel R
        _ShadingShiftTexScale ("mtoon.shadingShiftTexture.scale", Float) = 1
        _ShadingToonyFactor ("mtoon.shadingToonyFactor", Range(0, 1)) = 0.95
        // COM3D2: ゲームの Toony の影の割合 (陰影の境界)。ゲームと同名で、シェーダーを差し替えても値 (無限色の RenderTexture を含む) が引き継がれる
        _ShadowRateToon ("Shadow Rate Toon", 2D) = "white" {}
        // COM3D2: 1 なら ForwardBase の陰影を _ShadowRateToon で決める。V が負なら u と同じ値で読む。ずらしは N·L へ足す (プラグインが設定する)
        _UseShadowRateToon ("Use Shadow Rate Toon", Float) = 0
        _ShadowRateToonV ("Shadow Rate Toon V", Float) = -1
        _ShadowRateShift ("Shadow Rate Shift", Range(-1, 1)) = 0
        // COM3D2: 主光源の陰影の濃さ。0 で光側一色、1 で元の陰影、2 で光側との差が 2 倍 (プラグインが設定する)
        _ShadeStrength ("Shade Strength", Range(0, 2)) = 1
        // COM3D2: 白目の範囲 (UV の外接矩形、zw が 0 なら無効) と、白目に重ねる影。白目の色のアルファが 0 ならテクスチャの白目の下側の色を使う (プラグインが設定する)
        _ScleraRect ("Sclera Rect", Vector) = (0, 0, 0, 0)
        _ScleraColor ("Sclera Color", Color) = (1, 1, 1, 0)
        _ScleraShadow ("Sclera Shadow", Range(0, 1)) = 0
        _ScleraShadowColor ("Sclera Shadow Color", Color) = (0.878, 0.827, 0.788, 1)
        _ScleraShadowHeight ("Sclera Shadow Height", Range(0, 1)) = 0.5
        _ScleraShadowSoftness ("Sclera Shadow Softness", Range(0, 1)) = 0.15
        _ScleraShadowBend ("Sclera Shadow Bend", Range(0, 1)) = 0.2
        _ScleraShadowRoll ("Sclera Shadow Roll", Range(-1, 1)) = 0
        _ScleraLidFollow ("Sclera Lid Follow", Range(0, 1)) = 0.2
        _LidClose ("Lid Close", Range(0, 1)) = 0

        // GI
        _GiEqualization ("mtoon.giEqualizationFactor", Range(0, 1)) = 0.9
        // COM3D2: 追加ライトの影側の明るさ (プラグインが設定する)
        _AdditionalLightShade ("Additional Light Shade", Range(0, 1)) = 1
        // COM3D2: 前髪の影 (プラグインが設定する。行列 _HairShadowMatrix は Properties に書けないため SetMatrix で入れる)
        _HairShadowTex ("Hair Shadow Depth", 2D) = "white" {}
        _HairShadowStrength ("Hair Shadow Strength", Range(0, 2)) = 0
        _HairShadowSoftness ("Hair Shadow Softness (texels)", Range(0, 3)) = 1
        // COM3D2: 顔の半面切り替えの影。角度・幅はラジアン、ぼかしは m (プラグインが設定する)
        _FaceHalfShadow ("Face Half Shadow", Float) = 0
        _FaceHalfAngle ("Face Half Angle (rad)", Float) = 1.3962634
        _FaceFullAngle ("Face Full Angle (rad)", Float) = 1.9198622
        _FaceHalfFadeWidth ("Face Half Fade Width (rad)", Float) = 0.8726646
        _FaceFullFadeWidth ("Face Full Fade Width (rad)", Float) = 0.8726646
        _FaceHalfSoftness ("Face Half Softness (m)", Float) = 0.005
        _FaceFrontAngle ("Face Front Angle (rad)", Float) = 1.0471976
        _FaceFrontShadowStrength ("Face Front Shadow Strength", Float) = 1
        // COM3D2: 頭の中心・正面・頭の上 (ワールド)。正面が 0 の間は N·L の影で描く (プラグインが毎フレーム設定する)
        _FaceHalfCenter ("Face Half Center", Vector) = (0, 0, 0, 0)
        _FaceHalfForward ("Face Half Forward", Vector) = (0, 0, 0, 0)
        _FaceHalfUp ("Face Half Up", Vector) = (0, 0, 0, 0)

        // Emission
        [HDR] _EmissionColor ("emissiveFactor", Color) = (0, 0, 0, 1) // Unity specified name
        _EmissionMap ("emissiveTexture", 2D) = "white" {} // Unity specified name

        // Rim Lighting
        _MatcapColor ("mtoon.matcapFactor", Color) = (0, 0, 0, 1) // 仕様のデフォルト値は白だが、過去の仕様違反 UniVRM 実装アプリケーションのために黒とする。 https://github.com/vrm-c/UniVRM/pull/2594
        _MatcapTex ("mtoon.matcapTexture", 2D) = "black" {}
        _RimColor ("mtoon.parametricRimColorFactor", Color) = (0, 0, 0, 1)
        _RimFresnelPower ("mtoon.parametricRimFresnelPowerFactor", Range(0, 100)) = 5.0
        _RimLift ("mtoon.parametricRimLiftFactor", Range(0, 1)) = 0
        _RimTex ("mtoon.rimMultiplyTexture", 2D) = "white" {}
        _RimLightingMix ("mtoon.rimLightingMixFactor", Range(0, 1)) = 1

        // Outline
        _OutlineWidthMode ("mtoon.outlineWidthMode", Int) = 0
        [PowerSlider(2.2)] _OutlineWidth ("mtoon.outlineWidthFactor", Range(0, 0.05)) = 0
        _OutlineWidthTex ("mtoon.outlineWidthMultiplyTexture", 2D) = "white" {} // channel G
        _OutlineColor ("mtoon.outlineColorFactor", Color) = (0, 0, 0, 1)
        // COM3D2: ゲームの *_Outline_Tex 系の線の色。ゲームと同名で、シェーダーを差し替えても値 (無限色の RenderTexture を含む) が引き継がれる
        _OutlineTex ("Outline Texture", 2D) = "white" {}
        _UseOutlineTex ("Use Outline Texture", Float) = 0
        _OutlineLightingMix ("mtoon.outlineLightingMixFactor", Range(0, 1)) = 1
        // COM3D2: ワールド基準の輪郭の幅に min(ビュー深度, これ) を掛ける (m)。これより近いと画面上の太さが頭打ちになり、0 以下では線が消える (プラグインが設定する)
        _OutlineNearDistance ("Outline Near Distance", Float) = 0.7

        // UV Animation
        _UvAnimMaskTex ("mtoon.uvAnimationMaskTexture", 2D) = "white" {} // channel B
        _UvAnimScrollXSpeed ("mtoon.uvAnimationScrollXSpeedFactor", Float) = 0
        _UvAnimScrollYSpeed ("mtoon.uvAnimationScrollYSpeedFactor", Float) = 0
        _UvAnimRotationSpeed ("mtoon.uvAnimationRotationSpeedFactor", Float) = 0

        // Unity ShaderPass Mode
        _M_CullMode ("_CullMode", Float) = 2.0
        _M_SrcBlend ("_SrcBlend", Float) = 1.0
        _M_DstBlend ("_DstBlend", Float) = 0.0
        _M_ZWrite ("_ZWrite", Float) = 1.0
        _M_AlphaToMask ("_AlphaToMask", Float) = 0.0

        // etc
        _M_DebugMode ("_DebugMode", Float) = 0.0

        // for Editor
        _M_EditMode ("_EditMode", Float) = 0.0
    }

    // Shader Model 3.0
    SubShader
    {
        Tags { "RenderType" = "Opaque"  "Queue" = "Geometry" }

        // Built-in Forward Base Pass
        Pass
        {
            Name "FORWARD_BASE"
            Tags { "LightMode" = "ForwardBase" }

            Cull [_M_CullMode]
            Blend [_M_SrcBlend] [_M_DstBlend]
            ZWrite [_M_ZWrite]
            ZTest LEqual
            BlendOp Add, Max
            AlphaToMask [_M_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_fwdbase nolightmap nodynlightmap nodirlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON
            #pragma multi_compile __ _NORMALMAP
            #pragma multi_compile __ _MTOON_EMISSIVEMAP
            #pragma multi_compile __ _MTOON_RIMMAP
            #pragma multi_compile __ _MTOON_PARAMETERMAP

            #pragma vertex MToonVertex
            #pragma fragment MToonFragment

            // LightMode から定義させるだけにすると、Unity 5.6 (COM3D2) では定義されず、主光源が追加ライトの式で描かれる
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif

            #include "./vrmc_materials_mtoon_forward_vertex.hlsl"
            #include "./vrmc_materials_mtoon_forward_fragment.hlsl"
            ENDHLSL
        }

        // Built-in Forward Base Pass: OUTLINE
        Pass
        {
            Name "FORWARD_BASE_OUTLINE"
            Tags { "LightMode" = "ForwardBase" }

            Cull Front
            Blend [_M_SrcBlend] [_M_DstBlend]
            ZWrite [_M_ZWrite]
            ZTest LEqual
            Offset 1, 1
            BlendOp Add, Max
            AlphaToMask [_M_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_fwdbase nolightmap nodynlightmap nodirlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON
            #pragma multi_compile __ _NORMALMAP
            #pragma multi_compile __ _MTOON_EMISSIVEMAP
            #pragma multi_compile __ _MTOON_RIMMAP
            #pragma multi_compile __ _MTOON_PARAMETERMAP
            #pragma multi_compile __ _MTOON_OUTLINE_WORLD _MTOON_OUTLINE_SCREEN

            #pragma vertex MToonVertex
            #pragma fragment MToonFragment

            // LightMode から定義させるだけにすると、Unity 5.6 (COM3D2) では定義されず、主光源が追加ライトの式で描かれる
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif
            #define MTOON_PASS_OUTLINE

            #include "./vrmc_materials_mtoon_forward_vertex.hlsl"
            #include "./vrmc_materials_mtoon_forward_fragment.hlsl"
            ENDHLSL
        }

        // Built-in Forward Add Pass
        Pass
        {
            Name "FORWARD_ADD"
            Tags { "LightMode" = "ForwardAdd" }

            Cull [_M_CullMode]
            Blend [_M_SrcBlend] One
            ZWrite Off
            ZTest LEqual
            BlendOp Add, Max
            AlphaToMask [_M_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_fwdadd_fullshadows nolightmap nodynlightmap nodirlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON
            #pragma multi_compile __ _NORMALMAP
            #pragma multi_compile __ _MTOON_EMISSIVEMAP
            #pragma multi_compile __ _MTOON_RIMMAP
            #pragma multi_compile __ _MTOON_PARAMETERMAP

            #pragma vertex MToonVertex
            #pragma fragment MToonFragment

            // LightMode から定義させるだけにすると、Unity 5.6 (COM3D2) では定義されず、フォグの色が加算され追加ライトの数だけ明るくなる
            #ifndef UNITY_PASS_FORWARDADD
            #define UNITY_PASS_FORWARDADD
            #endif

            #include "./vrmc_materials_mtoon_forward_vertex.hlsl"
            #include "./vrmc_materials_mtoon_forward_fragment.hlsl"
            ENDHLSL
        }

        //  Shadow rendering pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_M_CullMode]
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma target 3.0

            // Unity defined keywords
            #pragma multi_compile_shadowcaster nolightmap nodynlightmap nodirlightmap novertexlight
            #pragma multi_compile_instancing

            #pragma multi_compile __ _ALPHATEST_ON _ALPHABLEND_ON

            // Use unity standard shadow implementation.
            // internal usage:
            //     keywords: _ALPHATEST_ON _ALPHABLEND_ON
            //     variables: _MainTex.a _Color.a _Cutoff
            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster

            #include "UnityStandardShadow.cginc"
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
    CustomEditor "VRM10.MToon10.Editor.MToonInspector"
}
