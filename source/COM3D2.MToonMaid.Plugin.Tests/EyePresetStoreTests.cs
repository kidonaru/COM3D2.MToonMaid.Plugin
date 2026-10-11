using System;
using System.Collections.Generic;
using System.IO;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class EyePresetStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public EyePresetStoreTests()
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

        private static EyePreset CreatePreset()
        {
            var eye = EyeSettings.CreateDefault();
            eye.enabled = true;
            eye.upperShadow = 0.2f;
            for (var i = 0; i < 3; i++)
            {
                eye.highlights.Add(EyeSettings.CreateNewHighlight());
            }
            eye.highlights[0].color = new ColorData(0.1f, 0.2f, 0.3f, 1f);
            return new EyePreset
            {
                eye = eye,
                color = new EyeColorSnapshot
                {
                    left = new EyeColorValues { mainHue = 10, mainBrightness = 300, shadowRate = 40 },
                    right = new EyeColorValues { mainHue = 20, shadowContrast = 150 },
                    side = EyeColorSide.Right,
                },
            };
        }

        [Fact]
        public void 保存して読み直すと同じ値になる()
        {
            var store = new PresetStore<EyePreset>(_dir, "瞳のプリセット", _warnings.Add);

            var preset = CreatePreset();
            preset.eye.scleraShadowEnabled = true;
            preset.eye.scleraShadowBend = 0.6f;
            Assert.True(store.Save("青い瞳", preset));
            var loaded = store.Load("青い瞳");

            Assert.NotNull(loaded);
            Assert.Equal(0.2f, loaded.eye.upperShadow);
            Assert.True(loaded.eye.scleraShadowEnabled);
            Assert.Equal(0.6f, loaded.eye.scleraShadowBend);
            Assert.Equal(3, loaded.eye.highlights.Count);
            Assert.Equal(0.2f, loaded.eye.highlights[0].color.g);
            Assert.Equal(10, loaded.color.left.mainHue);
            Assert.Equal(300, loaded.color.left.mainBrightness);
            Assert.Equal(150, loaded.color.right.shadowContrast);
            Assert.Equal(EyeColorSide.Right, loaded.color.side);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 一覧は名前順で保存と削除に追従する()
        {
            var store = new PresetStore<EyePreset>(_dir, "瞳のプリセット", _warnings.Add);
            store.Refresh();
            Assert.Empty(store.Names);

            store.Save("b", CreatePreset());
            store.Save("A", CreatePreset());
            Assert.Equal(new[] { "A", "b" }, store.Names);
            Assert.True(store.Exists("a"));

            store.Delete("A");
            Assert.Equal(new[] { "b" }, store.Names);
            Assert.False(store.Exists("A"));
        }

        [Fact]
        public void 同じ名前で保存すると上書きする()
        {
            var store = new PresetStore<EyePreset>(_dir, "瞳のプリセット", _warnings.Add);
            store.Save("p", CreatePreset());
            var second = CreatePreset();
            second.eye.upperShadow = 0.9f;

            store.Save("p", second);

            Assert.Single(store.Names);
            Assert.Equal(0.9f, store.Load("p").eye.upperShadow);
        }

        [Fact]
        public void ファイル名に使えない文字は置き換え空の名前は保存しない()
        {
            var store = new PresetStore<EyePreset>(_dir, "瞳のプリセット", _warnings.Add);

            Assert.Equal("a_b", PresetStore.NormalizeName(" a/b "));
            Assert.False(store.Save("  ", CreatePreset()));
            Assert.True(store.Save("a/b", CreatePreset()));
            Assert.Equal(new[] { "a_b" }, store.Names);
            Assert.NotNull(store.Load("a/b"));
        }

        [Fact]
        public void 範囲外の値は丸める()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "wide.xml"),
                "<?xml version=\"1.0\"?><MToonMaidEyePreset version=\"1\"><eye><upperShadow>5</upperShadow></eye>"
                + "<color><left><mainBrightness>9999</mainBrightness></left></color></MToonMaidEyePreset>");
            var store = new PresetStore<EyePreset>(_dir, "瞳のプリセット", _warnings.Add);

            var wide = store.Load("wide");

            Assert.Equal(1f, wide.eye.upperShadow);
            Assert.NotNull(wide.eye.highlights);
            Assert.Equal(EyeColorValues.MaxBrightness, wide.color.left.mainBrightness);
        }

        [Fact]
        public void 読めないファイルは警告してnull()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "broken.xml"), "<not-xml");
            var store = new PresetStore<EyePreset>(_dir, "瞳のプリセット", _warnings.Add);

            Assert.Null(store.Load("broken"));
            Assert.Null(store.Load("missing"));
            Assert.Equal(2, _warnings.Count);
        }
    }
}
