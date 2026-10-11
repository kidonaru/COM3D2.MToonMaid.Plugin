using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>目タブで選んでいるハイライトの番号。個数が 0 のときも 0 を返す</summary>
    public static class HighlightSelection
    {
        public static int Clamp(int index, int count)
        {
            return Math.Max(0, Math.Min(index, count - 1));
        }

        public static int AfterAdd(int countAfter)
        {
            return countAfter - 1;
        }

        public static int AfterRemove(int removedIndex, int countAfter)
        {
            return Clamp(removedIndex - 1, countAfter);
        }
    }
}
