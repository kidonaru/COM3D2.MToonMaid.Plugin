// MToonMaid: eye_hi の板 (SkinHi) を残すシェーダー。描画順とブレンドは CM3D2/Lighted_Trans に合わせてある。_HighlightLightInfluence はプラグインが毎回入れる
Shader "COM3D2/MToonMaidEyeHi"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _HighlightLightInfluence ("Highlight Light Influence", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _HighlightLightInfluence;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                const half4 tex = tex2D(_MainTex, i.uv);
                // 元のシェーダーのように法線で陰を付けると、ライトの向きで板の形の黒い塊になる
                const half3 ambient = (ShadeSH9(half4(0, 1, 0, 1)) + ShadeSH9(half4(0, -1, 0, 1))) * 0.5;
                const half3 light = _LightColor0.rgb + ambient;
                return half4(saturate(tex.rgb * lerp(1, light, _HighlightLightInfluence)), tex.a);
            }
            ENDCG
        }
    }
}
