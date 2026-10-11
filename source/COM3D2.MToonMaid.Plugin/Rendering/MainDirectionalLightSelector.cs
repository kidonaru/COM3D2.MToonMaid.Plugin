using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>平行光 1 つ分の、主光源の選択に要る値 (テストの net48 で Unity の Light を使えないため写す)</summary>
    public struct DirectionalLightInfo
    {
        public bool enabled;
        public int cullingMask;
        // LightRenderMode.ForceVertex (Not Important)。ForwardBase のピクセルライトに選ばれない
        public bool notImportant;
        public float intensity;
        public float r;
        public float g;
        public float b;
        // RenderSettings.sun
        public bool isSun;
    }

    /// <summary>
    /// Unity のビルトインのフォワード描画が ForwardBase に選ぶ平行光を真似る。
    /// 顔のレイヤーを照らす候補のうち sun があればそれ、無ければ強さ × 色の輝度が最大のもの
    /// </summary>
    public static class MainDirectionalLightSelector
    {
        // 色の輝度の係数 (Rec.601。Unity の Color.grayscale と同じ)
        private const float LumaR = 0.299f;
        private const float LumaG = 0.587f;
        private const float LumaB = 0.114f;

        public static int Select(IList<DirectionalLightInfo> lights, int layer)
        {
            var best = -1;
            var bestBrightness = float.NegativeInfinity;
            for (var i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (!l.enabled || l.notImportant || (l.cullingMask & (1 << layer)) == 0)
                {
                    continue;
                }
                if (l.isSun)
                {
                    return i;
                }
                var brightness = l.intensity * (LumaR * l.r + LumaG * l.g + LumaB * l.b);
                if (brightness > bestBrightness)
                {
                    best = i;
                    bestBrightness = brightness;
                }
            }
            return best;
        }
    }
}
