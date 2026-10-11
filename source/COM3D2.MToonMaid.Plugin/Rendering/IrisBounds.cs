namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>瞳の外接矩形 (UV)。x, y が最小の角</summary>
    public struct IrisRect
    {
        public static readonly IrisRect Full = new IrisRect(0f, 0f, 1f, 1f);

        public float x;
        public float y;
        public float width;
        public float height;

        public IrisRect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    /// <summary>
    /// 瞳のテクスチャのアルファから外接矩形を求める。Unity の型を使わない (テストの net48 では UnityEngine.CoreModule を読めないため)
    /// </summary>
    public static class IrisBounds
    {
        public const int SampleSize = 64;
        public const byte Threshold = 128;

        /// <summary>
        /// alpha は行優先で 0 行目が v = 0 (Texture2D.GetPixels32 と同じ並び)。threshold を超える画素を瞳とみなす。
        /// 見つからなければ false で rect は UV 全体
        /// </summary>
        public static bool TryCompute(byte[] alpha, int width, int height, byte threshold, out IrisRect rect)
        {
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (alpha[y * width + x] <= threshold)
                    {
                        continue;
                    }
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0)
            {
                rect = IrisRect.Full;
                return false;
            }
            rect = new IrisRect(
                (float)minX / width, (float)minY / height,
                (float)(maxX - minX + 1) / width, (float)(maxY - minY + 1) / height);
            return true;
        }
    }
}
