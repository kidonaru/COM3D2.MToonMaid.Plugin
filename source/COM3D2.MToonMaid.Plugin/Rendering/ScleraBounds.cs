using System;
using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 白目を含むマテリアルのうち白目の UV の範囲を、瞳のメッシュの外接箱に入る三角形から求める。
    /// Unity の型を使わない (テストの net48 では UnityEngine.CoreModule を読めないため)
    /// </summary>
    public static class ScleraBounds
    {
        /// <summary>
        /// positions は頂点ごとの x, y, z、uvs は u, v。eyeTriangles は目ごとのサブメッシュの頂点番号で、目ごとに外接箱を作る。
        /// 3 頂点がどれか 1 つの箱の内側 (境界を含む) にある白目の三角形の UV の外接矩形を返す。無ければ false で rect は全部 0
        /// </summary>
        public static bool TryCompute(float[] positions, float[] uvs, IList<int[]> eyeTriangles, int[] scleraTriangles, out IrisRect rect)
        {
            rect = default(IrisRect);
            var boxes = new List<float[]>();
            foreach (var eye in eyeTriangles)
            {
                if (eye != null && eye.Length > 0)
                {
                    boxes.Add(Box(positions, eye));
                }
            }
            if (boxes.Count == 0 || scleraTriangles == null)
            {
                return false;
            }

            var minU = float.MaxValue;
            var minV = float.MaxValue;
            var maxU = float.MinValue;
            var maxV = float.MinValue;
            var found = false;
            for (var t = 0; t + 2 < scleraTriangles.Length; t += 3)
            {
                if (!InsideAnyBox(positions, boxes, scleraTriangles, t))
                {
                    continue;
                }
                found = true;
                for (var j = 0; j < 3; j++)
                {
                    var i = scleraTriangles[t + j];
                    minU = Math.Min(minU, uvs[i * 2]);
                    maxU = Math.Max(maxU, uvs[i * 2]);
                    minV = Math.Min(minV, uvs[i * 2 + 1]);
                    maxV = Math.Max(maxV, uvs[i * 2 + 1]);
                }
            }
            if (!found)
            {
                return false;
            }
            rect = new IrisRect(minU, minV, maxU - minU, maxV - minV);
            return true;
        }

        // 戻り値は min x, y, z, max x, y, z
        private static float[] Box(float[] positions, int[] indices)
        {
            var box = new[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue };
            foreach (var i in indices)
            {
                for (var a = 0; a < 3; a++)
                {
                    var p = positions[i * 3 + a];
                    box[a] = Math.Min(box[a], p);
                    box[a + 3] = Math.Max(box[a + 3], p);
                }
            }
            return box;
        }

        private static bool InsideAnyBox(float[] positions, List<float[]> boxes, int[] triangles, int t)
        {
            foreach (var box in boxes)
            {
                if (Inside(positions, box, triangles[t]) && Inside(positions, box, triangles[t + 1]) && Inside(positions, box, triangles[t + 2]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Inside(float[] positions, float[] box, int i)
        {
            for (var a = 0; a < 3; a++)
            {
                var p = positions[i * 3 + a];
                if (p < box[a] || p > box[a + 3])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
