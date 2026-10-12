using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// ウィンドウの位置とサイズを Config\MToonMaid.xml へ保存する。
    /// DockableWindowBase はドラッグ中に毎フレーム StorePlacement を呼ぶため、
    /// マウスを離してからまとめて書く
    /// </summary>
    public sealed class WindowPlacementConfig
    {
        private readonly PluginConfigStore _store;
        private readonly int _defaultWidth;
        private readonly int _defaultHeight;
        private int _pendingX;
        private int _pendingY;
        private int _pendingWidth;
        private int _pendingHeight;
        private bool _dirty;

        public WindowPlacementConfig(PluginConfigStore store, int defaultWidth, int defaultHeight)
        {
            _store = store;
            _defaultWidth = defaultWidth;
            _defaultHeight = defaultHeight;
        }

        /// <summary>負の位置は画面中央 (DockableWindowBase.Init の扱い)</summary>
        public void Load(out int x, out int y, out int width, out int height)
        {
            var config = _store.Config;
            x = config.windowX;
            y = config.windowY;
            width = config.windowWidth > 0 ? config.windowWidth : _defaultWidth;
            height = config.windowHeight > 0 ? config.windowHeight : _defaultHeight;
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
            var config = _store.Config;
            config.windowX = _pendingX;
            config.windowY = _pendingY;
            config.windowWidth = _pendingWidth;
            config.windowHeight = _pendingHeight;
            _store.Save();
        }
    }
}
