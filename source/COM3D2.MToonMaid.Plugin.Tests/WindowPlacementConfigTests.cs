using System;
using System.Collections.Generic;
using System.IO;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class WindowPlacementConfigTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public WindowPlacementConfigTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidTests_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Fact]
        public void 未設定のサイズは既定サイズで読む()
        {
            var store = new PluginConfigStore(Path.Combine(_dir, "MToonMaid.xml"), _warnings.Add);
            var placement = new WindowPlacementConfig(store, 400, 600);

            int x, y, width, height;
            placement.Load(out x, out y, out width, out height);

            Assert.Equal(-1, x);
            Assert.Equal(-1, y);
            Assert.Equal(400, width);
            Assert.Equal(600, height);
        }

        [Fact]
        public void 保存済みのサイズはそのまま読む()
        {
            var store = new PluginConfigStore(Path.Combine(_dir, "MToonMaid.xml"), _warnings.Add);
            store.Config.windowX = 10;
            store.Config.windowY = 20;
            store.Config.windowWidth = 300;
            store.Config.windowHeight = 500;
            var placement = new WindowPlacementConfig(store, 400, 600);

            int x, y, width, height;
            placement.Load(out x, out y, out width, out height);

            Assert.Equal(10, x);
            Assert.Equal(20, y);
            Assert.Equal(300, width);
            Assert.Equal(500, height);
        }
    }
}
