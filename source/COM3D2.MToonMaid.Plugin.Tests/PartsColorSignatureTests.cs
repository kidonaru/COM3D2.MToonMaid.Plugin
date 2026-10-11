using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class PartsColorSignatureTests
    {
        private static int Of(bool use, int mainHue, int shadowContrast)
        {
            return PartsColorSignature.Mix(PartsColorSignature.Seed, use, mainHue, 100, 200, 50, 3, 10, 20, 30, shadowContrast);
        }

        [Fact]
        public void 同じ値なら同じ署名()
        {
            Assert.Equal(Of(true, 10, 40), Of(true, 10, 40));
        }

        [Fact]
        public void 色相が変われば署名が変わる()
        {
            Assert.NotEqual(Of(true, 10, 40), Of(true, 11, 40));
        }

        [Fact]
        public void 使用フラグと最後のフィールドも署名に入る()
        {
            Assert.NotEqual(Of(true, 10, 40), Of(false, 10, 40));
            Assert.NotEqual(Of(true, 10, 40), Of(true, 10, 41));
        }

        [Fact]
        public void 順に混ぜると前の値も効く()
        {
            var a = PartsColorSignature.Mix(Of(true, 1, 0), false, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var b = PartsColorSignature.Mix(Of(true, 2, 0), false, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.NotEqual(a, b);
        }
    }
}
