using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class EyePickedColorTests
    {
        [Fact]
        public void 選んだ色から求めた値のままなら選んだ色をそのまま見せる()
        {
            var picked = new EyePickedColor();
            var gray = new ColorData(0.5f, 0.5f, 0.5f, 1f);
            picked.Remember(gray, 40, 0, 255);

            Assert.Same(gray, picked.Shown(40, 0, 255));
        }

        [Fact]
        public void 値が変わったら値から求めた色を見せる()
        {
            var picked = new EyePickedColor();
            picked.Remember(new ColorData(0.5f, 0.5f, 0.5f, 1f), 40, 0, 255);

            var shown = picked.Shown(85, 255, 255);

            Assert.InRange(shown.g, 0.99f, 1f);
            Assert.InRange(shown.r, 0f, 0.01f);
        }

        [Fact]
        public void 一度値が変わったら元の値へ戻っても選んだ色は見せない()
        {
            var picked = new EyePickedColor();
            var gray = new ColorData(0.5f, 0.5f, 0.5f, 1f);
            picked.Remember(gray, 40, 0, 255);
            picked.Shown(85, 255, 255);

            Assert.NotSame(gray, picked.Shown(40, 0, 255));
        }

        [Fact]
        public void 忘れたあとは値から求めた色を見せる()
        {
            var picked = new EyePickedColor();
            var gray = new ColorData(0.5f, 0.5f, 0.5f, 1f);
            picked.Remember(gray, 40, 0, 255);
            picked.Forget();

            Assert.NotSame(gray, picked.Shown(40, 0, 255));
        }
    }
}
