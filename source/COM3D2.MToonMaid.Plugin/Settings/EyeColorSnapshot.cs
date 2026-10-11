using System;
using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>瞳の色の編集前の控え。左右の目の値と左右の選択</summary>
    public struct EyeColorSnapshot
    {
        public EyeColorValues left;
        public EyeColorValues right;
        public EyeColorSide side;

        /// <summary>左右 (Both) では左の値</summary>
        public EyeColorValues ValuesFor(EyeColorSide shown)
        {
            return shown == EyeColorSide.Right ? right : left;
        }
    }

    /// <summary>メイドごとの編集前の控え。シーンが変わったら Clear する</summary>
    public sealed class EyeColorBaselines
    {
        private readonly Dictionary<string, EyeColorSnapshot> _snapshots = new Dictionary<string, EyeColorSnapshot>();

        public EyeColorSnapshot GetOrCapture(string key, Func<EyeColorSnapshot> capture)
        {
            EyeColorSnapshot snapshot;
            if (!_snapshots.TryGetValue(key, out snapshot))
            {
                snapshot = capture();
                _snapshots[key] = snapshot;
            }
            return snapshot;
        }

        public void Clear()
        {
            _snapshots.Clear();
        }
    }
}
