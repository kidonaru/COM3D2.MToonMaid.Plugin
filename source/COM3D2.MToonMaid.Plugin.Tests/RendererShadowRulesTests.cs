using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class RendererShadowRulesTests
    {
        [Fact]
        public void 今の値と同じなら書かない()
        {
            Assert.False(RendererShadowRules.ShouldWrite(true, true));
            Assert.True(RendererShadowRules.ShouldWrite(false, true));
        }

        // 0 = Off, 1 = On, 2 = TwoSided, 3 = ShadowsOnly (ShadowCastingMode の値)
        [Theory]
        [InlineData(true, 2, 2)]
        [InlineData(true, 3, 3)]
        [InlineData(false, 0, 0)]
        public void 設定が元の値のONOFFと同じなら元のモードのまま(bool cast, int original, int expected)
        {
            Assert.Equal(expected, RendererShadowRules.ResolveCastMode(cast, original, original != 0, 1, 0));
        }

        [Theory]
        [InlineData(false, 2, 0)]
        [InlineData(true, 0, 1)]
        public void 設定が元の値のONOFFと違えばOnかOffにする(bool cast, int original, int expected)
        {
            Assert.Equal(expected, RendererShadowRules.ResolveCastMode(cast, original, original != 0, 1, 0));
        }

        [Fact]
        public void 戻すときは自分が書いた値のままなら元の値へ戻す()
        {
            Assert.True(RendererShadowRules.ResolveRestore(false, false, true));
            Assert.Equal(0, RendererShadowRules.ResolveRestore(1, 1, 0));
        }

        [Fact]
        public void 戻すときに他者が書き換えていたらその値を残す()
        {
            // MToonMaid が受けない (false) を書いた後、他プラグインが true にした
            Assert.True(RendererShadowRules.ResolveRestore(true, false, false));
            Assert.Equal(2, RendererShadowRules.ResolveRestore(2, 1, 0));
        }
    }
}
