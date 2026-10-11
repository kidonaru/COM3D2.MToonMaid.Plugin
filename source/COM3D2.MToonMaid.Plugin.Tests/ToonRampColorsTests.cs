using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class ToonRampColorsTests
    {
        [Fact]
        public void 左ほど暗いランプは左端が影側で右端が光側()
        {
            var row = new[]
            {
                0.40f, 0.41f, 0.42f, 1f,
                0.50f, 0.50f, 0.50f, 1f,
                0.58f, 0.59f, 0.60f, 1f,
            };

            var c = ToonRampColors.FromRow(row, 3);

            Assert.Equal(0.40f, c.Dark.r);
            Assert.Equal(0.42f, c.Dark.b);
            Assert.Equal(0.58f, c.Lit.r);
            Assert.Equal(0.60f, c.Lit.b);
            Assert.Equal(1f, c.Lit.a);
        }

        [Fact]
        public void 影側は左端ではなく最も暗い画素()
        {
            // 肌のランプは左から 3 割あたりが最も赤く暗く、左端は白へ戻る
            var row = new[]
            {
                1.00f, 0.96f, 0.96f, 1f,
                0.98f, 0.80f, 0.83f, 1f,
                1.00f, 0.91f, 0.91f, 1f,
                1.00f, 1.00f, 1.00f, 1f,
            };

            var c = ToonRampColors.FromRow(row, 4);

            Assert.Equal(0.98f, c.Dark.r);
            Assert.Equal(0.80f, c.Dark.g);
            Assert.Equal(0.83f, c.Dark.b);
            Assert.Equal(1f, c.Dark.a);
            Assert.Equal(1f, c.Lit.g);
        }

        [Fact]
        public void 透明な画素は影側に選ばない()
        {
            // 端に透明な余白 (RGBA=0) を持つ MOD ランプで影が真っ黒にならないように
            var row = new[]
            {
                0f, 0f, 0f, 0f,
                0.98f, 0.80f, 0.83f, 1f,
                1.00f, 1.00f, 1.00f, 1f,
            };

            var c = ToonRampColors.FromRow(row, 3);

            Assert.Equal(0.80f, c.Dark.g);
        }

        [Fact]
        public void 全画素が透明なら左端を影側にする()
        {
            var row = new[]
            {
                0.5f, 0.5f, 0.5f, 0f,
                0.2f, 0.2f, 0.2f, 0f,
            };

            var c = ToonRampColors.FromRow(row, 2);

            Assert.Equal(0.5f, c.Dark.g);
        }

        [Fact]
        public void 幅が足りなければ白()
        {
            var c = ToonRampColors.FromRow(new[] { 0.5f, 0.5f, 0.5f }, 1);

            Assert.Same(ToonRampColors.White, c);
        }

        [Fact]
        public void 空なら白()
        {
            Assert.Same(ToonRampColors.White, ToonRampColors.FromRow(new float[0], 0));
            Assert.Same(ToonRampColors.White, ToonRampColors.FromRow(null, 4));
        }

        [Fact]
        public void 白は光側も影側も1()
        {
            Assert.Equal(1f, ToonRampColors.White.Lit.g);
            Assert.Equal(1f, ToonRampColors.White.Dark.g);
        }
    }
}
