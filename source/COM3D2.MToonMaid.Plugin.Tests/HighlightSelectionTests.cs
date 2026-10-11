using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class HighlightSelectionTests
    {
        [Theory]
        [InlineData(0, 3, 0)]
        [InlineData(2, 3, 2)]
        [InlineData(5, 3, 2)]
        [InlineData(-1, 3, 0)]
        [InlineData(7, 0, 0)]
        public void 選択は個数の範囲へ寄せる(int index, int count, int expected)
        {
            Assert.Equal(expected, HighlightSelection.Clamp(index, count));
        }

        [Fact]
        public void 追加すると最後の番号を選ぶ()
        {
            Assert.Equal(3, HighlightSelection.AfterAdd(4));
        }

        [Theory]
        // 先頭を消すと先頭のまま
        [InlineData(0, 2, 0)]
        // 途中を消すと 1 つ前
        [InlineData(1, 2, 0)]
        // 末尾を消すと新しい末尾
        [InlineData(2, 2, 1)]
        // 最後の 1 個を消しても 0
        [InlineData(0, 0, 0)]
        public void 削除すると1つ前を選ぶ(int removedIndex, int countAfter, int expected)
        {
            Assert.Equal(expected, HighlightSelection.AfterRemove(removedIndex, countAfter));
        }
    }
}
