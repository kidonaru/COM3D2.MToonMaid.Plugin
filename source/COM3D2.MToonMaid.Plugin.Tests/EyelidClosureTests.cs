using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class EyelidClosureTests
    {
        private static readonly float[] Values = { 0.7f, 0.2f, 0.3f, 0.9f, 0.5f };

        [Fact]
        public void 閉じのブレンド値を合計する()
        {
            Assert.Equal(0.5f, EyelidClosure.Sum(new[] { new[] { 1, 2 } }, i => Values[i]), 3);
        }

        [Fact]
        public void 番号0は未登録として数えない()
        {
            // 先頭でも 0 は「そのブレンドが無い」の印 (ゲームも番号 0 を数えない)
            Assert.Equal(0.3f, EyelidClosure.Sum(new[] { new[] { 0, 2, 0 } }, i => Values[i]), 3);
        }

        [Fact]
        public void 全ての顔タイプの行を合計する()
        {
            // 今の顔タイプ以外の行はゲームが 0 にするため、合計すれば今の行の値になる
            var values = new[] { 0f, 0f, 0f, 0.4f, 0.1f };
            var rows = new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 0, 0 } };
            Assert.Equal(0.5f, EyelidClosure.Sum(rows, i => values[i]), 3);
        }

        [Fact]
        public void 合計は1で頭打ちにする()
        {
            Assert.Equal(1f, EyelidClosure.Sum(new[] { new[] { 3, 4 } }, i => Values[i]));
        }

        [Fact]
        public void 行が無ければ0()
        {
            Assert.Equal(0f, EyelidClosure.Sum(null, i => 1f));
            Assert.Equal(0f, EyelidClosure.Sum(new[] { (int[])null, new int[0] }, i => 1f));
        }

        [Fact]
        public void 負の値やNaNは0として扱う()
        {
            var values = new[] { 0f, -0.5f, float.NaN, 0.4f };
            Assert.Equal(0.4f, EyelidClosure.Sum(new[] { new[] { 1, 2, 3 } }, i => values[i]), 3);
        }
    }
}
