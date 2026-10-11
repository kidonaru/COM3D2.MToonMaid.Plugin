using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class RimParamsTests
    {
        [Fact]
        public void 鋭さはゲームの値に倍率を掛ける()
        {
            Assert.Equal(25f, RimParams.FresnelPower(25f, 1f));
            Assert.Equal(12.5f, RimParams.FresnelPower(25f, 0.5f));
        }

        [Fact]
        public void 鋭さは負にならない()
        {
            Assert.Equal(0f, RimParams.FresnelPower(25f, -1f));
            Assert.Equal(0f, RimParams.FresnelPower(-3f, 1f));
        }

        [Fact]
        public void 持ち上げはゲームの値に加算する()
        {
            Assert.Equal(0.2f, RimParams.Lift(0f, 0.2f), 5);
            Assert.Equal(-0.1f, RimParams.Lift(0.1f, -0.2f), 5);
        }
    }
}
