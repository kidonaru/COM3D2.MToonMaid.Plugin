using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class EyeColorSnapshotTests
    {
        private static EyeColorSnapshot Snapshot(int leftHue, int rightHue, EyeColorSide side)
        {
            return new EyeColorSnapshot
            {
                left = new EyeColorValues { mainHue = leftHue },
                right = new EyeColorValues { mainHue = rightHue },
                side = side,
            };
        }

        [Theory]
        [InlineData(EyeColorSide.Both, 10)]
        [InlineData(EyeColorSide.Left, 10)]
        [InlineData(EyeColorSide.Right, 20)]
        public void 表示している目の控えの値を返し左右では左を返す(EyeColorSide shown, int expectedHue)
        {
            Assert.Equal(expectedHue, Snapshot(10, 20, EyeColorSide.Both).ValuesFor(shown).mainHue);
        }

        [Fact]
        public void 同じメイドの控えは最初の1回だけ取る()
        {
            var baselines = new EyeColorBaselines();
            var calls = 0;
            baselines.GetOrCapture("a", () => { calls++; return Snapshot(10, 10, EyeColorSide.Both); });

            var second = baselines.GetOrCapture("a", () => { calls++; return Snapshot(99, 99, EyeColorSide.Right); });

            Assert.Equal(1, calls);
            Assert.Equal(10, second.left.mainHue);
            Assert.Equal(EyeColorSide.Both, second.side);
        }

        [Fact]
        public void メイドごとに別の控えを持つ()
        {
            var baselines = new EyeColorBaselines();
            baselines.GetOrCapture("a", () => Snapshot(10, 10, EyeColorSide.Both));

            var b = baselines.GetOrCapture("b", () => Snapshot(30, 40, EyeColorSide.Left));

            Assert.Equal(30, b.left.mainHue);
            Assert.Equal(10, baselines.GetOrCapture("a", () => Snapshot(99, 99, EyeColorSide.Both)).left.mainHue);
        }

        [Fact]
        public void 捨てたあとは控えを取り直す()
        {
            var baselines = new EyeColorBaselines();
            baselines.GetOrCapture("a", () => Snapshot(10, 10, EyeColorSide.Both));
            baselines.Clear();

            var again = baselines.GetOrCapture("a", () => Snapshot(50, 60, EyeColorSide.Right));

            Assert.Equal(50, again.left.mainHue);
            Assert.Equal(EyeColorSide.Right, again.side);
        }
    }
}
