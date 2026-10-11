using System;
using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// UniVRM MToonValidator (MIT) の移植。AlphaMode 等からブレンド・カリング・キーワードを決める。
    /// 置き換え対象は不透明・Cutout だけなので半透明の分岐は持たない。
    /// renderQueue は元の値へ戻す方針のためここでは決めない
    /// </summary>
    public struct MToonRenderState
    {
        // UnityEngine.Rendering.BlendMode / CullMode の数値
        private const int BlendOne = 1;
        private const int BlendZero = 0;
        private const int CullOff = 0;
        private const int CullBack = 2;

        public int srcBlend;
        public int dstBlend;
        public int zWrite;
        public int alphaToMask;
        public int cullMode;
        public string renderType;
        public List<string> enabledKeywords;
        public List<string> disabledKeywords;

        public static MToonRenderState Resolve(int alphaMode, bool doubleSided, int outlineMode, bool hasParameterMap)
        {
            var s = new MToonRenderState
            {
                srcBlend = BlendOne,
                dstBlend = BlendZero,
                zWrite = 1,
                cullMode = doubleSided ? CullOff : CullBack,
                enabledKeywords = new List<string>(),
                disabledKeywords = new List<string>(),
            };

            switch (alphaMode)
            {
                case MToonProps.AlphaModeOpaque:
                    s.alphaToMask = 0;
                    s.renderType = "Opaque";
                    break;
                case MToonProps.AlphaModeCutout:
                    s.alphaToMask = 1;
                    s.renderType = "TransparentCutout";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(alphaMode), alphaMode, "不透明・Cutout 以外は未対応");
            }

            s.Toggle(MToonProps.KeywordAlphaTest, alphaMode == MToonProps.AlphaModeCutout);
            s.Toggle(MToonProps.KeywordAlphaBlend, false);
            s.Toggle(MToonProps.KeywordOutlineWorld, outlineMode == MToonProps.OutlineModeWorld);
            s.Toggle(MToonProps.KeywordOutlineScreen, outlineMode == MToonProps.OutlineModeScreen);
            s.Toggle(MToonProps.KeywordParameterMap, hasParameterMap);
            return s;
        }

        private static readonly string[] OwnKeywords =
        {
            MToonProps.KeywordAlphaTest,
            MToonProps.KeywordAlphaBlend,
            MToonProps.KeywordOutlineWorld,
            MToonProps.KeywordOutlineScreen,
            MToonProps.KeywordParameterMap,
        };

        /// <summary>
        /// シェーダーだけ Toony へ戻されたマテリアルのキーワードから、MToon が付けたものを除く。
        /// 変換前 (previous) から持っていたものは元の値として残す
        /// </summary>
        public static string[] StripOwnKeywords(string[] current, string[] previous)
        {
            var result = new List<string>(current.Length);
            foreach (var k in current)
            {
                if (Array.IndexOf(OwnKeywords, k) < 0 || Array.IndexOf(previous, k) >= 0)
                {
                    result.Add(k);
                }
            }
            return result.ToArray();
        }

        private void Toggle(string keyword, bool enabled)
        {
            (enabled ? enabledKeywords : disabledKeywords).Add(keyword);
        }
    }
}
