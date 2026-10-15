using System;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class HairShadowProjectionTests
    {
        private const int Precision = 4;
        private static readonly float[] Center = { 1f, 2f, 3f };
        private static readonly float[] Up = { 0f, 1f, 0f };
        private static readonly float[] Forward = { 0f, 0f, 1f };

        private static float[] Offset(float[] p, float[] dir, float scale)
        {
            return new[] { p[0] + dir[0] * scale, p[1] + dir[1] * scale, p[2] + dir[2] * scale };
        }

        [Fact]
        public void 中心は影マップの真ん中に写る()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);
            var p = HairShadowProjection.Apply(m, Center);

            Assert.Equal(0.5f, p[0], Precision);
            Assert.Equal(0.5f, p[1], Precision);
            Assert.Equal(0.5f, p[2], Precision);
        }

        [Fact]
        public void 光に近いほど深度が小さく半径の端で0と1になる()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);

            Assert.Equal(0f, HairShadowProjection.Apply(m, Offset(Center, Forward, 0.3f))[2], Precision);
            Assert.Equal(1f, HairShadowProjection.Apply(m, Offset(Center, Forward, -0.3f))[2], Precision);
        }

        [Fact]
        public void 上向きの半径の端はvが1になる()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);
            var p = HairShadowProjection.Apply(m, Offset(Center, Up, 0.3f));

            Assert.Equal(0.5f, p[0], Precision);
            Assert.Equal(1f, p[1], Precision);
            Assert.Equal(0.5f, p[2], Precision);
        }

        [Fact]
        public void 光と上向きが平行なら予備の上向きを使う()
        {
            // 真上からの光。上向きの候補 (頭のてっぺん) と平行になる
            var m = HairShadowProjection.Create(Center, Up, Up, Forward, 0.3f);
            var p = HairShadowProjection.Apply(m, Offset(Center, Forward, 0.3f));

            Assert.False(float.IsNaN(p[0]));
            Assert.Equal(1f, p[1], Precision);
            Assert.Equal(0f, HairShadowProjection.Apply(m, Offset(Center, Up, 0.3f))[2], Precision);
        }

        [Fact]
        public void 影はoffsetの向きへ動き動いた先の点が元の点のUVを読み深度は変わらない()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);
            var offset = new[] { 0.02f, -0.01f, 0f };
            var shifted = HairShadowProjection.Offset(m, offset);
            var p = new[] { 1.05f, 2.1f, 2.9f };
            var moved = new[] { p[0] + offset[0], p[1] + offset[1], p[2] + offset[2] };

            var original = HairShadowProjection.Apply(m, p);
            var sampled = HairShadowProjection.Apply(shifted, moved);

            Assert.Equal(original[0], sampled[0], Precision);
            Assert.Equal(original[1], sampled[1], Precision);
            Assert.Equal(HairShadowProjection.Apply(m, moved)[2], sampled[2], Precision);
        }

        [Fact]
        public void 斜めの光でも顔の面の上では影が設定どおりずれる()
        {
            // 顔の面 (z = 3) に沿ったずらし。光は斜め
            var toLight = new[] { 0.5f, 0.3f, 1f };
            var m = HairShadowProjection.Create(Center, toLight, Up, Forward, 0.3f);
            var offset = new[] { 0.02f, -0.01f, 0f };
            var shifted = HairShadowProjection.Offset(m, offset);
            var h = new[] { 1.03f, 2.04f, 3f };
            var moved = new[] { h[0] + offset[0], h[1] + offset[1], h[2] + offset[2] };

            var original = HairShadowProjection.Apply(m, h);
            var sampled = HairShadowProjection.Apply(shifted, moved);

            Assert.Equal(original[0], sampled[0], Precision);
            Assert.Equal(original[1], sampled[1], Precision);
        }

        [Fact]
        public void 光の向きに沿ったずらしは影の位置を変えない()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);
            var shifted = HairShadowProjection.Offset(m, new[] { 0f, 0f, 0.05f });

            var a = HairShadowProjection.Apply(m, Center);
            var b = HairShadowProjection.Apply(shifted, Center);
            Assert.Equal(a[0], b[0], Precision);
            Assert.Equal(a[1], b[1], Precision);
        }

        [Theory]
        [InlineData(512, 512)]
        [InlineData(4096, 4096)]
        [InlineData(256, 512)]
        [InlineData(700, 512)]
        [InlineData(800, 1024)]
        [InlineData(3000, 2048)]
        [InlineData(99999, 4096)]
        [InlineData(0, 512)]
        public void 解像度は一覧の最も近い値へ丸める(int value, int expected)
        {
            Assert.Equal(expected, HairShadowProjection.ClampResolution(value));
        }

        [Theory]
        [InlineData(0f, true, false, false)]
        [InlineData(0.5f, true, false, true)]
        [InlineData(0.5f, false, false, false)]
        [InlineData(0.5f, true, true, false)]
        [InlineData(-1f, true, false, false)]
        public void 強さが0より大きくシェーダーがあり再試行待ちでなければ描く(float strength, bool caster, bool waiting, bool expected)
        {
            Assert.Equal(expected, HairShadowProjection.ShouldDraw(strength, caster, waiting));
        }

        [Theory]
        [InlineData(1f, true, 0.098f, 0.098f)]
        [InlineData(0.5f, true, 0.8f, 0.4f)]
        [InlineData(1f, true, 1f, 1f)]
        [InlineData(1f, false, 1f, 0f)]
        [InlineData(2f, true, 1f, 2f)]
        [InlineData(2f, true, 0.5f, 1f)]
        [InlineData(3f, true, 2f, 2f)]
        [InlineData(0f, true, 1f, 0f)]
        [InlineData(-1f, true, 1f, 0f)]
        [InlineData(1f, true, float.NaN, 0f)]
        [InlineData(float.NaN, true, 1f, 0f)]
        public void 前髪の影の濃さは主光源の落ち影の濃さを掛け影を落とさないライトでは0(float hairShadow, bool lightCastsShadows, float lightShadowStrength, float expected)
        {
            Assert.Equal(expected, HairShadowProjection.LinkedStrength(hairShadow, lightCastsShadows, lightShadowStrength), Precision);
        }

        [Fact]
        public void クリップ空間では0から1が負1から1になる()
        {
            var m = HairShadowProjection.Create(Center, Forward, Up, Forward, 0.3f);
            var clip = HairShadowProjection.ToClip(m);

            var c = HairShadowProjection.Apply(clip, Center);
            Assert.Equal(0f, c[0], Precision);
            Assert.Equal(0f, c[1], Precision);
            Assert.Equal(0f, c[2], Precision);

            var top = HairShadowProjection.Apply(clip, Offset(Center, Up, 0.3f));
            Assert.Equal(1f, top[1], Precision);
            var near = HairShadowProjection.Apply(clip, Offset(Center, Forward, 0.3f));
            Assert.Equal(-1f, near[2], Precision);
        }
    }
}
