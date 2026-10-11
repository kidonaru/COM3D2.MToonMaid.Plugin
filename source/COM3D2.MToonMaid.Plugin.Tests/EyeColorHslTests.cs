using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class EyeColorHslTests
    {
        private const float Tolerance = 2f / 255f;

        private static void AssertColor(int r, int g, int b, ColorData c)
        {
            Assert.InRange(c.r, r / 255f - Tolerance, r / 255f + Tolerance);
            Assert.InRange(c.g, g / 255f - Tolerance, g / 255f + Tolerance);
            Assert.InRange(c.b, b / 255f - Tolerance, b / 255f + Tolerance);
        }

        [Theory]
        [InlineData(0, 255, 255, 254, 1, 1)]
        [InlineData(85, 255, 255, 1, 254, 1)]
        [InlineData(0, 128, 255, 191, 64, 64)]
        [InlineData(8, 67, 266, 169, 119, 108)]
        [InlineData(0, 255, 191, 128, 0, 0)]
        [InlineData(0, 255, 383, 255, 255, 255)]
        public void ゲームの色テーブルの明るさ128の位置と同じ色になる(int hue, int chroma, int brightness, int r, int g, int b)
        {
            AssertColor(r, g, b, EyeColorHsl.ToColor(hue, chroma, brightness));
        }

        [Theory]
        [InlineData(600)]
        [InlineData(-420)]
        public void 色相が範囲外でも一周として表示する(int hue)
        {
            var expected = EyeColorHsl.ToColor(((hue % 255) + 255) % 255, 255, 255);
            var c = EyeColorHsl.ToColor(hue, 255, 255);
            Assert.Equal(new[] { expected.r, expected.g, expected.b }, new[] { c.r, c.g, c.b }, new FloatTolerance());
        }

        private sealed class FloatTolerance : System.Collections.Generic.IEqualityComparer<float>
        {
            public bool Equals(float a, float b)
            {
                return System.Math.Abs(a - b) < 1e-4f;
            }

            public int GetHashCode(float v)
            {
                return 0;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(126)]
        public void 明度が下限より下は黒で表示する(int brightness)
        {
            AssertColor(0, 0, 0, EyeColorHsl.ToColor(40, 200, brightness));
        }

        [Theory]
        [InlineData(383)]
        [InlineData(510)]
        public void 明度が上限より上は白で表示する(int brightness)
        {
            AssertColor(255, 255, 255, EyeColorHsl.ToColor(40, 200, brightness));
        }

        [Fact]
        public void 範囲内の値は色を経由しても変わらない()
        {
            foreach (var hue in new[] { 0, 8, 43, 85, 128, 170, 254 })
            foreach (var chroma in new[] { 1, 67, 128, 255 })
            foreach (var brightness in new[] { 127, 128, 200, 266, 381, 382 })
            {
                int h = -1, s = -1, b = -1;
                if (brightness == EyeColorHsl.ColorMinBrightness || brightness == EyeColorHsl.ColorMaxBrightness)
                {
                    // 黒と白は色相と彩度を残すため、元の値を入れておく
                    h = hue;
                    s = chroma;
                }
                EyeColorHsl.FromColor(EyeColorHsl.ToColor(hue, chroma, brightness), ref h, ref s, ref b);
                Assert.Equal(new[] { hue, chroma, brightness }, new[] { h, s, b });
            }
        }

        [Fact]
        public void 色相255は0と同じ赤として表示し選び直すと0になる()
        {
            int h = -1, s = -1, b = -1;
            EyeColorHsl.FromColor(EyeColorHsl.ToColor(255, 255, 255), ref h, ref s, ref b);
            Assert.Equal(new[] { 0, 255, 255 }, new[] { h, s, b });
        }

        [Fact]
        public void 純色の赤は色相0彩度255明度255になる()
        {
            int h = 99, s = 99, b = 99;
            EyeColorHsl.FromColor(new ColorData(1f, 0f, 0f, 1f), ref h, ref s, ref b);
            Assert.Equal(new[] { 0, 255, 255 }, new[] { h, s, b });
        }

        [Fact]
        public void 灰色を選ぶと色相を残して彩度を0にする()
        {
            int h = 40, s = 100, b = 300;
            EyeColorHsl.FromColor(new ColorData(0.5f, 0.5f, 0.5f, 1f), ref h, ref s, ref b);
            Assert.Equal(new[] { 40, 0, 255 }, new[] { h, s, b });
        }

        [Theory]
        [InlineData(0f, 127)]
        [InlineData(1f, 382)]
        public void 黒か白を選ぶと色相と彩度を残す(float v, int brightness)
        {
            int h = 40, s = 100, b = 300;
            EyeColorHsl.FromColor(new ColorData(v, v, v, 1f), ref h, ref s, ref b);
            Assert.Equal(new[] { 40, 100, brightness }, new[] { h, s, b });
        }
    }
}
