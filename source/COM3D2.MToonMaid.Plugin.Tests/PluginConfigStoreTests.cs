using System;
using System.Collections.Generic;
using System.IO;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class PluginConfigStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _path;
        private readonly List<string> _warnings = new List<string>();

        public PluginConfigStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidTests_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "MToonMaid.xml");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Fact]
        public void ファイルが無ければ既定値で作る()
        {
            var store = new PluginConfigStore(_path, _warnings.Add);

            Assert.Equal(10, store.Config.scanInterval);
            Assert.Equal("Ctrl+Shift+M", store.Config.toggleWindow);
            Assert.Equal(-1, store.Config.windowX);
            Assert.Equal(-1, store.Config.windowY);
            Assert.True(File.Exists(_path));
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 保存して読み直すと同じ値になる()
        {
            var store = new PluginConfigStore(_path, _warnings.Add);
            store.Config.scanInterval = 3;
            store.Config.toggleWindow = "Alt+F2";
            store.Config.windowX = 100;
            store.Config.windowY = 200;
            store.Config.windowWidth = 640;
            store.Config.windowHeight = 480;
            store.Save();

            var loaded = new PluginConfigStore(_path, _warnings.Add).Config;

            Assert.Equal(3, loaded.scanInterval);
            Assert.Equal("Alt+F2", loaded.toggleWindow);
            Assert.Equal(100, loaded.windowX);
            Assert.Equal(200, loaded.windowY);
            Assert.Equal(640, loaded.windowWidth);
            Assert.Equal(480, loaded.windowHeight);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 欠けた要素は既定値で読む()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "<?xml version=\"1.0\"?><PluginConfig><scanInterval>5</scanInterval></PluginConfig>");

            var config = new PluginConfigStore(_path, _warnings.Add).Config;

            Assert.Equal(5, config.scanInterval);
            Assert.Equal("Ctrl+Shift+M", config.toggleWindow);
            Assert.Equal(-1, config.windowX);
            Assert.Equal(0, config.windowWidth);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 壊れたファイルは退避して既定値で作り直す()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "<PluginConfig><scanInterval>");

            var config = new PluginConfigStore(_path, _warnings.Add).Config;

            Assert.Equal(10, config.scanInterval);
            Assert.Equal("<PluginConfig><scanInterval>", File.ReadAllText(_path + ".bak"));
            Assert.True(File.Exists(_path));
            Assert.Single(_warnings);
        }

        [Fact]
        public void 保存できなくても例外を出さず警告する()
        {
            var store = new PluginConfigStore(_path, _warnings.Add);
            // 保存先と同名のフォルダを作り、ファイルとして書けなくする
            File.Delete(_path);
            Directory.CreateDirectory(_path);

            store.Save();

            Assert.Single(_warnings);
        }
    }
}
