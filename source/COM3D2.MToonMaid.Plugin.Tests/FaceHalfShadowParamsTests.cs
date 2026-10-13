using System;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class FaceHalfShadowParamsTests
    {
        private const double Deg = Math.PI / 180.0;

        [Theory]
        [InlineData(true, PartGroup.Face, true)]
        [InlineData(false, PartGroup.Face, false)]
        [InlineData(true, PartGroup.Skin, false)]
        [InlineData(true, PartGroup.Hair, false)]
        [InlineData(true, PartGroup.Costume, false)]
        public void 顔のグループでONのときだけ有効にする(bool on, PartGroup group, bool expected)
        {
            Assert.Equal(expected, FaceHalfShadowParams.Enabled(on, group));
        }

        [Theory]
        [InlineData(45f, 45.0)]
        [InlineData(0f, 0.0)]
        [InlineData(180f, 180.0)]
        [InlineData(-10f, 0.0)]
        [InlineData(200f, 180.0)]
        [InlineData(float.NaN, 80.0)]
        [InlineData(float.PositiveInfinity, 80.0)]
        public void 半面の角度はラジアンにし0から180度へ丸め非有限は既定にする(float degrees, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.HalfAngle(degrees), 5);
        }

        [Theory]
        [InlineData(45f, 135f, 135.0)]
        [InlineData(90f, 60f, 90.0)]
        [InlineData(45f, float.NaN, 110.0)]
        [InlineData(150f, float.NaN, 150.0)]
        [InlineData(float.NaN, 30f, 80.0)]
        [InlineData(45f, 300f, 180.0)]
        public void 全面の角度は半面の角度より小さくしない(float half, float full, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.FullAngle(half, full), 5);
        }

        [Theory]
        [InlineData(30f, 30.0)]
        [InlineData(0f, 0.0)]
        [InlineData(120f, 90.0)]
        [InlineData(-5f, 0.0)]
        [InlineData(float.NaN, 50.0)]
        public void フェード幅はラジアンにし0から90度へ丸める(float degrees, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.HalfFadeWidth(90f, degrees), 5);
        }

        // 正面 (θ=0) で光と反対側の半面が少しでも影だと、光が正面をまたいで左右が入れ替わる瞬間に影が飛ぶ
        [Theory]
        [InlineData(10f, 30f, 20.0)]
        [InlineData(0f, 30f, 0.0)]
        [InlineData(45f, 30f, 30.0)]
        [InlineData(170f, 30f, 20.0)]
        public void 半面のフェード幅は0度と180度から半面の角度までの2倍までに縮める(float half, float width, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.HalfFadeWidth(half, width), 5);
        }

        // 真後ろ (θ=180°) で全面が濃さ 1 に届かないと、光が真後ろをまたぐ瞬間に影が飛ぶ
        [Theory]
        [InlineData(45f, 170f, 30f, 20.0)]
        [InlineData(45f, 180f, 30f, 0.0)]
        [InlineData(90f, 60f, 90f, 90.0)]
        [InlineData(5f, 135f, 30f, 30.0)]
        public void 全面のフェード幅は180度から全面の角度までの2倍までに縮め半面の角度には縛られない(float half, float full, float width, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.FullFadeWidth(half, full, width), 5);
        }

        [Theory]
        [InlineData(60f, 60.0)]
        [InlineData(0f, 0.0)]
        [InlineData(180f, 180.0)]
        [InlineData(-10f, 0.0)]
        [InlineData(200f, 180.0)]
        [InlineData(float.NaN, 60.0)]
        [InlineData(float.NegativeInfinity, 60.0)]
        public void 正面の範囲はラジアンにし0から180度へ丸め非有限は既定にする(float degrees, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees * Deg, FaceHalfShadowParams.FrontAngle(degrees), 5);
        }

        [Theory]
        [InlineData(0.5f, 0.5f)]
        [InlineData(0f, 0f)]
        [InlineData(1f, 1f)]
        [InlineData(-0.2f, 0f)]
        [InlineData(1.5f, 1f)]
        [InlineData(float.NaN, 1f)]
        [InlineData(float.PositiveInfinity, 1f)]
        public void 正面の影の濃さは0から1へ丸め非有限は既定にする(float strength, float expected)
        {
            Assert.Equal(expected, FaceHalfShadowParams.FrontShadowStrength(strength), 6);
        }

        [Theory]
        [InlineData(0.005f, 0.005f)]
        [InlineData(0f, 0f)]
        [InlineData(0.05f, 0.02f)]
        [InlineData(-1f, 0f)]
        [InlineData(float.NaN, 0.005f)]
        [InlineData(float.NegativeInfinity, 0.005f)]
        public void ぼかしは0から2cmへ丸め非有限は既定にする(float meters, float expected)
        {
            Assert.Equal(expected, FaceHalfShadowParams.Softness(meters), 6);
        }
    }
}
