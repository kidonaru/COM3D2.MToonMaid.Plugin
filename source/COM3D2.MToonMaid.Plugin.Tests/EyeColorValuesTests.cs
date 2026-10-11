using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class EyeColorValuesTests
    {
        private static EyeColorValues Color(int hue)
        {
            return new EyeColorValues
            {
                mainHue = hue, mainChroma = 100, mainBrightness = 255, mainContrast = 100,
                shadowRate = 128, shadowHue = 10, shadowChroma = 20, shadowBrightness = 30, shadowContrast = 40,
            };
        }

        [Fact]
        public void 範囲外の値はエディットのスライダーの範囲へ丸める()
        {
            var v = new EyeColorValues
            {
                mainHue = 300, mainChroma = -5, mainBrightness = 600, mainContrast = 250,
                shadowRate = 999, shadowHue = -1, shadowChroma = 256, shadowBrightness = 511, shadowContrast = 201,
            }.Clamped();

            Assert.Equal(new[] { 255, 0, 510, 200, 255, 0, 255, 510, 200 },
                new[] { v.mainHue, v.mainChroma, v.mainBrightness, v.mainContrast, v.shadowRate, v.shadowHue, v.shadowChroma, v.shadowBrightness, v.shadowContrast });
        }

        [Fact]
        public void 範囲内の値は丸めても変わらない()
        {
            Assert.True(Color(10).Equals(Color(10).Clamped()));
        }

        [Fact]
        public void 左右が選ばれていて左右の値が違うときは右へ切り替える()
        {
            Assert.Equal(EyeColorSide.Right, EyeColorSides.ResolveSide(EyeColorSide.Both, Color(10), Color(20)));
        }

        [Fact]
        public void 左右が選ばれていて影の項目だけが違うときも右へ切り替える()
        {
            var right = Color(10);
            right.shadowContrast = 41;
            Assert.Equal(EyeColorSide.Right, EyeColorSides.ResolveSide(EyeColorSide.Both, Color(10), right));
        }

        [Fact]
        public void 左右が選ばれていて左右の値が同じなら左右のまま()
        {
            Assert.Equal(EyeColorSide.Both, EyeColorSides.ResolveSide(EyeColorSide.Both, Color(10), Color(10)));
        }

        [Theory]
        [InlineData(EyeColorSide.Left)]
        [InlineData(EyeColorSide.Right)]
        public void 左か右が選ばれていれば値が違ってもそのまま(EyeColorSide side)
        {
            Assert.Equal(side, EyeColorSides.ResolveSide(side, Color(10), Color(20)));
        }
    }
}
