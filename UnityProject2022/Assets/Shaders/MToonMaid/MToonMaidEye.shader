// MToonMaid: 瞳の光沢。元の CM3D2/Toony_Lighted_Trans と同じ描画順・ブレンドで、影なしに明るさだけライトへ追従させ、
// 上まぶたの影・下側の明るさ・顔基準のハイライト (白目へはみ出してよい) を重ねる。値はプラグインが入れる
Shader "COM3D2/MToonMaidEye"
{
    Properties
    {
        // 既定は EyeSettings と揃える (プラグインが毎回値を入れるため、ずれても見た目は EyeSettings 側になる)
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _IrisRect ("Iris Rect", Vector) = (0, 0, 1, 1)
        _UpperShadow ("Upper Shadow", Range(0, 1)) = 0.75
        _UpperShadowColor ("Upper Shadow Color", Color) = (0.251, 0.188, 0.416, 1)
        _UpperShadowHeight ("Upper Shadow Height", Range(0, 1)) = 0.5
        _UpperShadowSoftness ("Upper Shadow Softness", Range(0, 1)) = 0.6
        _LowerGlow ("Lower Glow", Range(0, 1)) = 0.55
        _LowerGlowColor ("Lower Glow Color", Color) = (0.780, 0.839, 1, 1)
        _LowerGlowHeight ("Lower Glow Height", Range(0, 1)) = 0.5
        _LowerGlowSoftness ("Lower Glow Softness", Range(0, 1)) = 0.7
        _LidFollow ("Lid Follow", Range(0, 1)) = 0.5
        _LidCenter ("Lid Center", Range(-0.5, 0.5)) = 0
        _LidClose ("Lid Close", Range(0, 1)) = 0
        _HighlightCount ("Highlight Count", Float) = 0
        _HighlightLightInfluence ("Highlight Light Influence", Range(0, 1)) = 1
        _IrisLightInfluence ("Iris Light Influence", Range(0, 1)) = 1
        _Parallax ("Parallax", Range(-1, 1)) = 0.3
        _EyeGaze ("Eye Gaze (world)", Vector) = (0, 0, 0, 0)
        _HeadRight ("Head Right (world)", Vector) = (0, 0, 0, 0)
        _HeadUp ("Head Up (world)", Vector) = (0, 0, 0, 0)
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Lighting.cginc"
    #include "AutoLight.cginc"
    #include "../MToon10/vrmc_materials_mtoon_compat.hlsl"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    half4 _Color;
    float4 _IrisRect;
    half _UpperShadow;
    half4 _UpperShadowColor;
    half _UpperShadowHeight;
    half _UpperShadowSoftness;
    half _LowerGlow;
    half4 _LowerGlowColor;
    half _LowerGlowHeight;
    half _LowerGlowSoftness;
    half _LidFollow;
    half _LidCenter;
    half _LidClose;
    // 長さはプラグインの EyeSettings.MaxHighlightCount と揃える
    #define MAX_HIGHLIGHTS 8
    float _HighlightCount;
    half _HighlightLightInfluence;
    half _IrisLightInfluence;
    // Shape = (x, y, 幅, 高さ) / Params = (強さ, ロール [rad], にじみの幅, にじみの高さ) / Color = (芯の色 rgb, -) / Glow = (にじみの色 rgb, 不透明度 × にじみの強さ)
    float4 _HighlightShape[MAX_HIGHLIGHTS];
    float4 _HighlightParams[MAX_HIGHLIGHTS];
    float4 _HighlightColor[MAX_HIGHLIGHTS];
    float4 _HighlightGlow[MAX_HIGHLIGHTS];
    half _Parallax;
    float4 _EyeGaze;
    float4 _HeadRight;
    float4 _HeadUp;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 worldPos : TEXCOORD1;
        UNITY_LIGHTING_COORDS(2, 3)
    };

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        UNITY_TRANSFER_LIGHTING(o, v.uv);
        return o;
    }

    // テクスチャの向きのまま置くと、左右の目で UV が鏡像のとき、ハイライトが顔の別の側へ出る。
    // ビューの右・上を軸にすると、カメラを回したときハイライトが瞳の上を動く。
    // 画面の 1 画素あたりの q と headPos の変化の比から写像を求めるので、画面の y の向きの差は打ち消し合う
    float2 ToHeadAligned(float2 q, float2 headPos)
    {
        const float2 qx = ddx(q);
        const float2 qy = ddy(q);
        const float2 px = ddx(headPos);
        const float2 py = ddy(headPos);
        const float detP = px.x * py.y - py.x * px.y;
        if (abs(detP) < 1e-14)
        {
            return q;
        }
        // M = [qx qy] * inverse([px py]) の列。顔の右・上が q のどちら向きか
        // right = ∂q/∂head.x
        const float2 right = (qx * py.y - qy * px.y) / detP;
        // up = ∂q/∂head.y
        const float2 up = (qy * px.x - qx * py.x) / detP;
        const float handed = (right.x * up.y - right.y * up.x) < 0 ? -1 : 1;
        const float2 r = right * rsqrt(max(dot(right, right), 1e-12));
        const float2 u = float2(-r.y, r.x) * handed;
        return float2(dot(q, r), dot(q, u));
    }

    // pixel は約 1 画素ぶんの瞳の座標で、芯の縁はその幅だけなめらかにする
    half3 Highlight(float2 p, float4 shape, float4 params, half3 coreColor, half4 glowColor, float pixel)
    {
        if (min(shape.z, shape.w) <= 0)
        {
            return 0;
        }
        const float2 radii = shape.zw * 0.5;
        float2 d = p - shape.xy;
        float s;
        float c;
        sincos(params.y, s, c);
        d = float2(c * d.x + s * d.y, -s * d.x + c * d.y);
        // 楕円の縁までの正確な距離は重いため f / |∇f| で近似する (f = 楕円で正規化した距離 - 1)。
        // (f × 短い方の半径) にすると、細長い楕円で長い軸の向きにだけ芯の縁とにじみが伸びる
        const float2 e = d / radii;
        const float r = length(e);
        const float dist = (r - 1) / max(length(e / radii) / max(r, 1e-5), 1e-5);
        const half core = saturate(0.5 - dist / pixel);
        // 幅・高さが両方 0 のときも芯の縁の内側を 1 にすると、にじみ無しの縁に色の輪が出る
        half glow = 0;
        if (max(params.z, params.w) > 0)
        {
            // 縁までの距離をにじみの幅で割る形にすると、幅と高さが違うとき斜めの向きの幅が決まらない。
            // 外縁を 1 画素より薄くすると、幅 0 の向きでにじみが段差で切れる
            const float ro = length(d / (radii + max(params.zw, pixel)));
            // 芯の縁 (0) から外縁の楕円 (1) の間の位置
            const float t = (r - 1) * ro / max(r - ro, 1e-5);
            glow = r <= 1 ? 1 : pow(1 - saturate(t), 2);
        }
        return saturate(lerp(glowColor.rgb * (glowColor.a * glow), coreColor, core) * params.x);
    }

    // shine はハイライトの倍率。追加ライトのパスで 0 にする (追加ライトの数だけ白が重なって飛ぶため)
    half3 ApplyGloss(half3 albedo, float2 uv, float3 worldPos, half shine, out half3 highlight)
    {
        // 0〜1 に正規化した瞳の座標 (上下はテクスチャの向き = 頭の上下)
        const float2 n = (uv - _IrisRect.xy) / max(_IrisRect.zw, 1e-4);
        // 瞳の幅を 1 とした中心からの座標。高さを幅で割り、丸が瞳の縦横比で潰れないようにする
        const float2 q = (n - 0.5) * float2(1, _IrisRect.w / max(_IrisRect.z, 1e-4));
        // ddx / ddy は分岐 (ハイライトの個数) より前で求める
        // ワールド座標のまま射影すると、原点から離れた所で ddx / ddy が float の刻みに埋もれ、ハイライトの向きがちらつく
        const float3 rel = worldPos - _WorldSpaceCameraPos;
        const float2 headPos = float2(dot(rel, _HeadRight.xyz), dot(rel, _HeadUp.xyz));
        const float2 s = ToHeadAligned(q, headPos);
        const float pixel = max(length(fwidth(q)), 1e-5);

        const half upperEdge = 1 - _UpperShadowHeight;
        const half upperMask = smoothstep(upperEdge, upperEdge + max(_UpperShadowSoftness * _UpperShadowHeight, 1e-4), n.y) * _UpperShadow;
        albedo = lerp(albedo, albedo * _UpperShadowColor.rgb, upperMask);

        const half lowerMask = 1 - smoothstep(_LowerGlowHeight - max(_LowerGlowSoftness * _LowerGlowHeight, 1e-4), _LowerGlowHeight, n.y);
        albedo = 1 - (1 - albedo) * (1 - _LowerGlowColor.rgb * (lowerMask * _LowerGlow));

        // 視差: 目が顔の右 (上) を向くと、ハイライトは瞳の中で左 (下) へ寄る。
        // 視線をビュー座標へ写すと、目を動かさなくてもカメラの角度でハイライトが動く
        const float2 gaze = float2(dot(_EyeGaze.xyz, _HeadRight.xyz), dot(_EyeGaze.xyz, _HeadUp.xyz));
        // 一律に下へずらすと、笑顔のように下から閉じる目では下のハイライトがまぶたに隠れる
        const float lidScale = 1 - _LidFollow * _LidClose;
        // 視差の Y を縮めないと、目を上下へ向けているときハイライトが寄せる先 (_LidCenter) からずれる
        const float2 p = s + gaze * _Parallax * float2(1, lidScale);

        // 足し合わせると、重なった所やにじみの色が白へ飽和するため、ハイライト同士もスクリーン合成で重ねる
        half3 remain = 1;
        [unroll]
        for (int i = 0; i < MAX_HIGHLIGHTS; i++)
        {
            if (i < _HighlightCount && _HighlightParams[i].x > 0)
            {
                float4 shape = _HighlightShape[i];
                shape.y = lerp(_LidCenter, shape.y, lidScale);
                remain *= 1 - Highlight(p, shape, _HighlightParams[i], _HighlightColor[i].rgb, _HighlightGlow[i], pixel);
            }
        }
        highlight = 1 - remain;
        highlight *= shine;
        return albedo;
    }
    ENDCG

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
            #pragma fragment fragBase

            half4 fragBase(v2f i) : SV_Target
            {
                const half4 tex = tex2D(_MainTex, i.uv) * _Color;
                half3 highlight;
                const half3 albedo = ApplyGloss(tex.rgb, i.uv, i.worldPos, 1, highlight);
                // 顔の MToon の光側と同じく、主光源の色に上下で均した環境光を足す (光と影の境界は入れない)
                const half3 ambient = (ShadeSH9(half4(0, 1, 0, 1)) + ShadeSH9(half4(0, -1, 0, 1))) * 0.5;
                const half3 light = _LightColor0.rgb + ambient;
                // ハイライトを瞳のアルファで切り抜くと白目へはみ出せない。
                // 瞳の上は加算だとにじみの色が白へ飽和するため、スクリーン合成 (1 - (1 - 瞳)(1 - ハイライト)) にする。
                // 白目の上はブレンドが下の色を読めないため、濃さ (RGB の最大) で重ねる (灰色ならスクリーンと一致する)
                const half3 lit = saturate(highlight * lerp(1, light, _HighlightLightInfluence));
                const half coverage = max(lit.r, max(lit.g, lit.b));
                const half alpha = saturate(tex.a + coverage * (1 - tex.a));
                const half3 color = (albedo * lerp(1, light, _IrisLightInfluence) * tex.a * (1 - lit) + lit) / max(alpha, 1e-4);
                return half4(color, alpha);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAdd
            #pragma multi_compile_fwdadd

            half4 fragAdd(v2f i) : SV_Target
            {
                const half4 tex = tex2D(_MainTex, i.uv) * _Color;
                half3 highlight;
                const half3 albedo = ApplyGloss(tex.rgb, i.uv, i.worldPos, 0, highlight);
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                // 追加ライトに影響の割合を掛けないと、0 でも追加ライトの分だけ明るくなり、ライトを無視した色にならない
                return half4(albedo * _LightColor0.rgb * (atten * _IrisLightInfluence), tex.a);
            }
            ENDCG
        }
    }
}
