using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class OutlineParamsTests
    {
        [Theory]
        [InlineData(true, false, OutlineColorSource.Texture)]
        [InlineData(false, false, OutlineColorSource.Game)]
        [InlineData(true, true, OutlineColorSource.Override)]
        [InlineData(false, true, OutlineColorSource.Override)]
        public void 線の色の出どころ(bool fromTex, bool colorOverride, OutlineColorSource expected)
        {
            Assert.Equal(expected, OutlineParams.Source(fromTex, colorOverride));
        }

        [Theory]
        [InlineData(false, MToonProps.OutlineModeNone)]
        [InlineData(true, MToonProps.OutlineModeWorld)]
        public void 線の基準(bool outline, int expected)
        {
            Assert.Equal(expected, OutlineParams.Mode(outline));
        }
    }
}
