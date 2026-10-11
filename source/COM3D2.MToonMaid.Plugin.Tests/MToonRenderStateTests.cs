using System;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class MToonRenderStateTests
    {
        [Fact]
        public void 不透明()
        {
            var s = MToonRenderState.Resolve(MToonProps.AlphaModeOpaque, false, MToonProps.OutlineModeNone, false);

            Assert.Equal(1, s.srcBlend);   // BlendMode.One
            Assert.Equal(0, s.dstBlend);   // BlendMode.Zero
            Assert.Equal(1, s.zWrite);
            Assert.Equal(0, s.alphaToMask);
            Assert.Equal(2, s.cullMode);   // Back
            Assert.Equal("Opaque", s.renderType);
            Assert.Contains(MToonProps.KeywordAlphaTest, s.disabledKeywords);
            Assert.Contains(MToonProps.KeywordOutlineWorld, s.disabledKeywords);
            Assert.Contains(MToonProps.KeywordOutlineScreen, s.disabledKeywords);
        }

        [Fact]
        public void 画面基準のアウトラインのキーワード()
        {
            var s = MToonRenderState.Resolve(MToonProps.AlphaModeOpaque, false, MToonProps.OutlineModeScreen, false);

            Assert.Contains(MToonProps.KeywordOutlineScreen, s.enabledKeywords);
            Assert.Contains(MToonProps.KeywordOutlineWorld, s.disabledKeywords);
        }

        [Fact]
        public void Cutoutは両面でアルファテスト()
        {
            var s = MToonRenderState.Resolve(MToonProps.AlphaModeCutout, true, MToonProps.OutlineModeNone, false);

            Assert.Equal(1, s.alphaToMask);
            Assert.Equal(0, s.cullMode);   // Off
            Assert.Equal("TransparentCutout", s.renderType);
            Assert.Contains(MToonProps.KeywordAlphaTest, s.enabledKeywords);
        }

        [Fact]
        public void アウトラインとパラメーターマップのキーワード()
        {
            var s = MToonRenderState.Resolve(MToonProps.AlphaModeOpaque, false, MToonProps.OutlineModeWorld, true);

            Assert.Contains(MToonProps.KeywordOutlineWorld, s.enabledKeywords);
            Assert.Contains(MToonProps.KeywordOutlineScreen, s.disabledKeywords);
            Assert.Contains(MToonProps.KeywordParameterMap, s.enabledKeywords);
        }

        [Fact]
        public void 巻き戻し後のキーワードからMToonが付けたものだけを除く()
        {
            // ゲームがシェーダーだけ Toony へ戻すと、MToon が付けたキーワードが残る
            var current = new[] { "_ALPHAPREMULTIPLY_ON", MToonProps.KeywordOutlineWorld, MToonProps.KeywordAlphaTest };
            var previous = new[] { "_ALPHAPREMULTIPLY_ON", MToonProps.KeywordAlphaTest };

            var result = MToonRenderState.StripOwnKeywords(current, previous);

            Assert.Equal(new[] { "_ALPHAPREMULTIPLY_ON", MToonProps.KeywordAlphaTest }, result);
        }

        [Fact]
        public void 未対応のアルファモードは例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MToonRenderState.Resolve(2, false, MToonProps.OutlineModeNone, false));
        }
    }
}
