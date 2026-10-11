// MToonMaid: 前髪の影マップへ前髪の深度を書く。値はプラグインが CommandBuffer のグローバルで渡す。
// 元のマテリアルの値を拾わないよう Properties は持たない (Properties に書いた名前はマテリアルの値が優先される)
Shader "Hidden/COM3D2/MToonHairShadowCaster"
{
    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MToonHairShadowCasterTex;
            float4 _MToonHairShadowCasterTex_ST;
            // 負なら切り抜かない (Cutout でないマテリアル)
            float _MToonHairShadowCasterCutoff;
            // ワールド → (u, v, 深度)。顔のシェーダーの _HairShadowMatrix と同じ値
            float4x4 _MToonHairShadowCasterMatrix;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float depth : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                const float4 positionWS = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, positionWS);
                o.uv = v.texcoord.xy * _MToonHairShadowCasterTex_ST.xy + _MToonHairShadowCasterTex_ST.zw;
                o.depth = mul(_MToonHairShadowCasterMatrix, positionWS).z;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                if (_MToonHairShadowCasterCutoff >= 0)
                {
                    clip(tex2D(_MToonHairShadowCasterTex, i.uv).a - _MToonHairShadowCasterCutoff);
                }
                return i.depth;
            }
            ENDCG
        }
    }
}
