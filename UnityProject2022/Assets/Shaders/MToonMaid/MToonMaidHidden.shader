// MToonMaid: 何も描かない。eye_hi の板 (SkinHi) を瞳の光沢と二重にしないよう、マテリアルや menu に触らずに消す
Shader "Hidden/COM3D2/MToonMaidHidden"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Pass
        {
            ZWrite Off
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                clip(-1);
                return 0;
            }
            ENDCG
        }
    }
}
