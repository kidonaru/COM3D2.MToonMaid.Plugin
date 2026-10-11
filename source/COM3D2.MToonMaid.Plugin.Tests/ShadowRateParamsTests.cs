using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class ShadowRateParamsTests
    {
        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        public void ゲームの影の境界はONでテクスチャがあるときだけ使う(bool gameShadowRate, bool hasTexture, bool expected)
        {
            Assert.Equal(expected, ShadowRateParams.Use(gameShadowRate, hasTexture));
        }

        [Theory]
        [InlineData(true, true, true, true)]
        [InlineData(true, false, true, false)]
        [InlineData(true, true, false, false)]
        [InlineData(false, true, true, false)]
        public void ランプはONでランプと影の境界の両方があるときだけ読む(bool gameShadowRate, bool hasRamp, bool hasShadowRate, bool expected)
        {
            Assert.Equal(expected, ShadowRateParams.UseRamp(gameShadowRate, hasRamp, hasShadowRate));
        }

        private static ToonRampColors Ramp()
        {
            // 影側 (0.4, 0.2, 0.2)、光側 (0.9, 0.8, 0.7)
            return ToonRampColors.FromRow(new[] { 0.4f, 0.2f, 0.2f, 1f, 0.9f, 0.8f, 0.7f, 1f }, 2);
        }

        [Fact]
        public void ランプを読むときは2色の近似を白にする()
        {
            var lit = ShadowRateParams.RampLitColor(true, Ramp());
            var shade = ShadowRateParams.RampShadeMultiplier(true, Ramp());
            Assert.Equal(new[] { 1f, 1f, 1f }, new[] { lit.r, lit.g, lit.b });
            Assert.Equal(new[] { 1f, 1f, 1f }, new[] { shade.r, shade.g, shade.b });
        }

        [Fact]
        public void ランプを読まないときは光側と影側の色を使う()
        {
            var lit = ShadowRateParams.RampLitColor(false, Ramp());
            var shade = ShadowRateParams.RampShadeMultiplier(false, Ramp());
            Assert.Equal(new[] { 0.9f, 0.8f, 0.7f }, new[] { lit.r, lit.g, lit.b });
            Assert.Equal(new[] { 0.4f, 0.2f, 0.2f }, new[] { shade.r, shade.g, shade.b });
        }

        [Theory]
        [InlineData(0.4f, 0.4f)]
        [InlineData(1.5f, 1.5f)]
        [InlineData(3f, 2f)]
        [InlineData(-1f, 0f)]
        [InlineData(float.NaN, 1f)]
        [InlineData(float.PositiveInfinity, 1f)]
        public void 陰影の濃さは0から2へ丸め非有限は1にする(float value, float expected)
        {
            Assert.Equal(expected, ShadowRateParams.ClampShadeStrength(value));
        }
    }
}
