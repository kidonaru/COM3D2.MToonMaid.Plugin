using BepInEx.Configuration;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// ウィンドウの位置とサイズを BepInEx の設定へ保存する。
    /// DockableWindowBase はドラッグ中に毎フレーム StorePlacement を呼ぶため、
    /// マウスを離してからまとめて書く (設定は値の代入ごとにファイルへ書かれる)
    /// </summary>
    public sealed class WindowPlacementConfig
    {
        private readonly ConfigFile _config;
        private readonly ConfigEntry<int> _x;
        private readonly ConfigEntry<int> _y;
        private readonly ConfigEntry<int> _width;
        private readonly ConfigEntry<int> _height;
        private int _pendingX;
        private int _pendingY;
        private int _pendingWidth;
        private int _pendingHeight;
        private bool _dirty;

        public WindowPlacementConfig(ConfigFile config, int defaultWidth, int defaultHeight)
        {
            _config = config;
            // 負の位置は画面中央 (DockableWindowBase.Init の扱い)
            _x = config.Bind("Window", "X", -1, "ウィンドウの X 座標 (負なら画面中央)");
            _y = config.Bind("Window", "Y", -1, "ウィンドウの Y 座標 (負なら画面中央)");
            _width = config.Bind("Window", "Width", defaultWidth, "ウィンドウの幅");
            _height = config.Bind("Window", "Height", defaultHeight, "ウィンドウの高さ");
        }

        public void Load(out int x, out int y, out int width, out int height)
        {
            x = _x.Value;
            y = _y.Value;
            width = _width.Value;
            height = _height.Value;
        }

        public void Store(int x, int y, int width, int height)
        {
            _pendingX = x;
            _pendingY = y;
            _pendingWidth = width;
            _pendingHeight = height;
            _dirty = true;
        }

        public void FlushIfReleased()
        {
            if (!_dirty || Input.GetMouseButton(0))
            {
                return;
            }
            _dirty = false;
            var saveOnSet = _config.SaveOnConfigSet;
            _config.SaveOnConfigSet = false;
            _x.Value = _pendingX;
            _y.Value = _pendingY;
            _width.Value = _pendingWidth;
            _height.Value = _pendingHeight;
            _config.SaveOnConfigSet = saveOnSet;
            _config.Save();
        }
    }
}
