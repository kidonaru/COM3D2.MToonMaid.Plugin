using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class IrisBoundsTests
    {
        // 8×8。0 行目が v = 0
        private static byte[] Image(int x0, int y0, int x1, int y1, byte value)
        {
            var a = new byte[64];
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    a[y * 8 + x] = value;
                }
            }
            return a;
        }

        [Fact]
        public void 不透明な画素の外接矩形をUVで返す()
        {
            IrisRect r;
            Assert.True(IrisBounds.TryCompute(Image(2, 1, 5, 6, 255), 8, 8, 128, out r));

            Assert.Equal(2f / 8, r.x, 5);
            Assert.Equal(1f / 8, r.y, 5);
            Assert.Equal(4f / 8, r.width, 5);
            Assert.Equal(6f / 8, r.height, 5);
        }

        [Fact]
        public void 閾値ちょうどの画素は含めない()
        {
            var a = Image(2, 2, 3, 3, 255);
            a[7 * 8 + 7] = 128;

            IrisRect r;
            Assert.True(IrisBounds.TryCompute(a, 8, 8, 128, out r));

            Assert.Equal(2f / 8, r.x, 5);
            Assert.Equal(2f / 8, r.width, 5);
        }

        [Fact]
        public void 全面透明なら見つからずUV全体()
        {
            IrisRect r;
            Assert.False(IrisBounds.TryCompute(new byte[64], 8, 8, 128, out r));

            Assert.Equal(IrisRect.Full.x, r.x);
            Assert.Equal(IrisRect.Full.y, r.y);
            Assert.Equal(IrisRect.Full.width, r.width);
            Assert.Equal(IrisRect.Full.height, r.height);
        }

        [Fact]
        public void 端に接する矩形()
        {
            IrisRect r;
            Assert.True(IrisBounds.TryCompute(Image(0, 0, 7, 7, 200), 8, 8, 128, out r));

            Assert.Equal(0f, r.x, 5);
            Assert.Equal(0f, r.y, 5);
            Assert.Equal(1f, r.width, 5);
            Assert.Equal(1f, r.height, 5);
        }
    }
}
