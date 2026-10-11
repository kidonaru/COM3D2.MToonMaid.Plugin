using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class GameCompatTests
    {
        // テストは net471 (COM3D2.5 版) に対して走るため、2.5 版の判定だけを確かめる
        [Theory]
        [InlineData("2022.3.62f2", true)]
        [InlineData("5.6.4f1", false)]
        [InlineData("2018.4.0f1", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsExpectedUnityVersion(string unityVersion, bool expected)
        {
            Assert.Equal(expected, GameCompat.IsExpectedUnityVersion(unityVersion));
        }
    }
}
