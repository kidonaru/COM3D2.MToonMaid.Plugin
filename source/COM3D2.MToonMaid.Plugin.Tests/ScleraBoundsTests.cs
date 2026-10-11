using System.Collections.Generic;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class ScleraBoundsTests
    {
        // 頂点 0〜2 = 目の三角形 (x 0〜1, y 0〜1, z 0)。3〜5 = 箱の内側の白目、6〜8 = 1 頂点だけ箱の外の白目
        private static readonly float[] Positions =
        {
            0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f,
            0.1f, 0.1f, 0f, 0.5f, 0.1f, 0f, 0.1f, 0.5f, 0f,
            0.2f, 0.2f, 0f, 2f, 0.2f, 0f, 0.2f, 0.4f, 0f,
        };

        private static readonly float[] Uvs =
        {
            0f, 0f, 0f, 0f, 0f, 0f,
            0.1f, 0.2f, 0.3f, 0.2f, 0.1f, 0.4f,
            0.8f, 0.8f, 0.9f, 0.9f, 0.8f, 0.9f,
        };

        private static readonly int[] Eye = { 0, 1, 2 };

        [Fact]
        public void 目の箱の内側の三角形のUVの外接矩形を返す()
        {
            IrisRect r;
            Assert.True(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { Eye }, new[] { 3, 4, 5 }, out r));

            Assert.Equal(0.1f, r.x, 5);
            Assert.Equal(0.2f, r.y, 5);
            Assert.Equal(0.2f, r.width, 5);
            Assert.Equal(0.2f, r.height, 5);
        }

        [Fact]
        public void 一頂点でも箱の外の三角形は含めない()
        {
            IrisRect r;
            Assert.True(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { Eye }, new[] { 3, 4, 5, 6, 7, 8 }, out r));

            Assert.Equal(0.1f, r.x, 5);
            Assert.Equal(0.2f, r.width, 5);
        }

        [Fact]
        public void 箱の境界上の頂点は内側とみなす()
        {
            var positions = (float[])Positions.Clone();
            // 頂点 4 を箱の角 (1, 0, 0) へ
            positions[12] = 1f;
            positions[13] = 0f;

            IrisRect r;
            Assert.True(ScleraBounds.TryCompute(positions, Uvs, new List<int[]> { Eye }, new[] { 3, 4, 5 }, out r));
        }

        [Fact]
        public void 目ごとの箱の間にある三角形は含めない()
        {
            // 左目 x 0〜1、右目 x 3〜4。白目の三角形は x 1.5〜2.5 (両目を 1 つの箱にすると内側になる)
            var positions = new float[]
            {
                0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f,
                3f, 0f, 0f, 4f, 0f, 0f, 3f, 1f, 0f,
                1.5f, 0.1f, 0f, 2.5f, 0.1f, 0f, 2f, 0.5f, 0f,
            };
            var uvs = new float[18];

            IrisRect r;
            Assert.False(ScleraBounds.TryCompute(positions, uvs,
                new List<int[]> { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } }, new[] { 6, 7, 8 }, out r));
        }

        [Fact]
        public void 左右の目の箱それぞれの三角形をまとめた外接矩形を返す()
        {
            // 左目 x 0〜1、右目 x 3〜4。白目の三角形は左右に 1 つずつ (実機と同じく UV は近い範囲)
            var positions = new float[]
            {
                0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f,
                3f, 0f, 0f, 4f, 0f, 0f, 3f, 1f, 0f,
                0.1f, 0.1f, 0f, 0.5f, 0.1f, 0f, 0.1f, 0.5f, 0f,
                3.1f, 0.1f, 0f, 3.5f, 0.1f, 0f, 3.1f, 0.5f, 0f,
            };
            var uvs = new float[]
            {
                0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f, 0f,
                0.1f, 0.1f, 0.3f, 0.1f, 0.1f, 0.3f,
                0.2f, 0.2f, 0.4f, 0.2f, 0.2f, 0.5f,
            };

            IrisRect r;
            Assert.True(ScleraBounds.TryCompute(positions, uvs,
                new List<int[]> { new[] { 0, 1, 2 }, new[] { 3, 4, 5 } }, new[] { 6, 7, 8, 9, 10, 11 }, out r));

            Assert.Equal(0.1f, r.x, 5);
            Assert.Equal(0.1f, r.y, 5);
            Assert.Equal(0.3f, r.width, 5);
            Assert.Equal(0.4f, r.height, 5);
        }

        [Fact]
        public void 該当する三角形が無ければfalseで全部0()
        {
            IrisRect r;
            Assert.False(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { Eye }, new[] { 6, 7, 8 }, out r));

            Assert.Equal(0f, r.width);
            Assert.Equal(0f, r.height);
        }

        [Fact]
        public void 目や白目の三角形が空ならfalse()
        {
            IrisRect r;
            Assert.False(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]>(), new[] { 3, 4, 5 }, out r));
            Assert.False(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { new int[0] }, new[] { 3, 4, 5 }, out r));
            Assert.False(ScleraBounds.TryCompute(Positions, Uvs, new List<int[]> { Eye }, new int[0], out r));
        }
    }
}
