using System;
using System.Collections.Generic;
using System.IO;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class EyeSettingsTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public EyeSettingsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidEyeTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        private MaidSettings LoadXml(string xml)
        {
            var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, xml);
            return MaidSettingsStore.Load(path, _warnings.Add);
        }

        [Fact]
        public void eye要素の無いファイルはONの既定()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><enabled>true</enabled></MToonMaid>");

            Assert.NotNull(s.eye);
            Assert.True(s.eye.enabled);
            Assert.False(s.eye.hideEyeHighlight);
            Assert.Equal(0f, s.eye.upperShadow);
            Assert.Equal(0f, s.eye.lowerGlow);
            Assert.NotNull(s.eye.upperShadowColor);
            Assert.NotNull(s.eye.lowerGlowColor);
            Assert.Equal(0.4f, s.eye.lidFollow);
            Assert.Equal(-0.15f, s.eye.lidCenter);
            Assert.Equal(0.5f, s.eye.highlightLightInfluence);
            Assert.Equal(0.85f, s.eye.irisLightInfluence);
            Assert.Empty(s.eye.highlights);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 空のhighlightsは0個のまま()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye><highlights /></eye></MToonMaid>");

            Assert.Empty(s.eye.highlights);
            s.eye.FillMissing();
            Assert.Empty(s.eye.highlights);
        }

        [Fact]
        public void ハイライト0個を保存して読み直しても0個()
        {
            var path = Path.Combine(_dir, "empty.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.eye.highlights.Clear();

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(loaded.eye.highlights);
        }

        [Fact]
        public void 廃止した映り込みの要素は読み飛ばす()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye><reflection><strength>0.4</strength></reflection><parallax>0.2</parallax></eye></MToonMaid>");

            Assert.Equal(0.2f, s.eye.parallax);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 上限を超えるハイライトは捨て欠けた値は新規の既定で埋める()
        {
            var item = "<EyeHighlightSettings><x>0.1</x></EyeHighlightSettings>";
            var xml = "<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye><highlights>";
            for (var i = 0; i < EyeSettings.MaxHighlightCount + 2; i++)
            {
                xml += item;
            }
            var s = LoadXml(xml + "</highlights></eye></MToonMaid>");

            Assert.Equal(EyeSettings.MaxHighlightCount, s.eye.highlights.Count);
            var h = s.eye.highlights[0];
            var def = EyeSettings.CreateNewHighlight();
            Assert.Equal(0.1f, h.x);
            Assert.Equal(def.width, h.width);
            Assert.Equal(def.glowWidth, h.glowWidth);
            Assert.NotNull(h.glowColor);
        }

        [Fact]
        public void 色の無いハイライトは白の芯にしにじみの色のアルファは残す()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye><highlights>"
                + "<EyeHighlightSettings><glowColor r=\"1\" g=\"0\" b=\"0\" a=\"0.4\" /></EyeHighlightSettings>"
                + "</highlights></eye></MToonMaid>");

            var h = s.eye.highlights[0];
            Assert.Equal(new[] { 1f, 1f, 1f }, new[] { h.color.r, h.color.g, h.color.b });
            Assert.Equal(0.4f, h.glowColor.a);
        }

        [Fact]
        public void 追加するハイライトのにじみの高さは幅と同じ()
        {
            var h = EyeSettings.CreateNewHighlight();

            Assert.Equal(h.glowWidth, h.glowHeight);
        }

        [Fact]
        public void 範囲外の値とNaNは丸める()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye>"
                + "<upperShadow>5</upperShadow><parallax>NaN</parallax>"
                + "<lidFollow>3</lidFollow>"
                + "<highlights><EyeHighlightSettings><x>-3</x><width>4</width><roll>400</roll><glowWidth>1</glowWidth><glowHeight>-1</glowHeight><glowStrength>5</glowStrength></EyeHighlightSettings></highlights>"
                + "</eye></MToonMaid>");

            Assert.Equal(1f, s.eye.upperShadow);
            // 視差の NaN は下限 (-1) ではなく 0 (視差なし)
            Assert.Equal(0f, s.eye.parallax);
            Assert.Equal(1f, s.eye.lidFollow);
            var h = s.eye.highlights[0];
            Assert.Equal(-EyeSettings.MaxHighlightOffset, h.x);
            Assert.Equal(EyeSettings.MaxHighlightSize, h.width);
            Assert.Equal(EyeSettings.MaxHighlightRoll, h.roll);
            Assert.Equal(EyeSettings.MaxGlowWidth, h.glowWidth);
            Assert.Equal(0f, h.glowHeight);
            Assert.Equal(EyeSettings.MaxGlowStrength, h.glowStrength);
        }

        [Theory]
        [InlineData("-0.5", -0.5f)]
        [InlineData("-3", -1f)]
        [InlineData("3", 1f)]
        public void 視差はマイナスを許し範囲へ丸める(string value, float expected)
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye><parallax>" + value + "</parallax></eye></MToonMaid>");

            Assert.Equal(expected, s.eye.parallax);
        }

        [Theory]
        [InlineData("0.2", 0.2f)]
        [InlineData("-3", -EyeSettings.MaxHighlightOffset)]
        [InlineData("3", EyeSettings.MaxHighlightOffset)]
        // NaN は下限ではなく 0 (瞳の中心)
        [InlineData("NaN", 0f)]
        public void 中央の高さはマイナスを許し範囲へ丸める(string value, float expected)
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye><lidCenter>" + value + "</lidCenter></eye></MToonMaid>");

            Assert.Equal(expected, s.eye.lidCenter);
        }

        [Theory]
        [InlineData("0.4", 0.4f)]
        [InlineData("-3", 0f)]
        [InlineData("3", 1f)]
        // NaN は下限 (ライトを無視) ではなく 1 (既定と同じ見た目)
        [InlineData("NaN", 1f)]
        public void ハイライトへのライトの影響は範囲へ丸める(string value, float expected)
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye><highlightLightInfluence>" + value + "</highlightLightInfluence></eye></MToonMaid>");

            Assert.Equal(expected, s.eye.highlightLightInfluence);
        }

        [Theory]
        [InlineData("0.4", 0.4f)]
        [InlineData("-3", 0f)]
        [InlineData("3", 1f)]
        // NaN は下限 (ライトを無視) ではなく 1 (既定と同じ見た目)
        [InlineData("NaN", 1f)]
        public void 瞳へのライトの影響は範囲へ丸める(string value, float expected)
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye><irisLightInfluence>" + value + "</irisLightInfluence></eye></MToonMaid>");

            Assert.Equal(expected, s.eye.irisLightInfluence);
        }

        [Fact]
        public void 保存して読み直すと同じ値になる()
        {
            var path = Path.Combine(_dir, "eye.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.eye.enabled = false;
            s.eye.lidFollow = 0.8f;
            s.eye.lidCenter = -0.1f;
            s.eye.highlightLightInfluence = 0.3f;
            s.eye.irisLightInfluence = 0.6f;
            for (var i = 0; i < 4; i++)
            {
                s.eye.highlights.Add(EyeSettings.CreateNewHighlight());
            }
            s.eye.highlights[1].roll = 45f;
            s.eye.highlights[1].glowHeight = 0.2f;
            s.eye.highlights[1].glowStrength = 1.5f;
            s.eye.highlights[1].glowColor = new ColorData(0.4f, 0.5f, 0.6f, 0.3f);
            s.eye.highlights[1].color = new ColorData(0.9f, 0.8f, 0.7f, 1f);
            s.eye.highlights.RemoveAt(2);

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.False(loaded.eye.enabled);
            Assert.Equal(0.8f, loaded.eye.lidFollow);
            Assert.Equal(-0.1f, loaded.eye.lidCenter);
            Assert.Equal(0.3f, loaded.eye.highlightLightInfluence);
            Assert.Equal(0.6f, loaded.eye.irisLightInfluence);
            Assert.Equal(3, loaded.eye.highlights.Count);
            Assert.Equal(45f, loaded.eye.highlights[1].roll);
            Assert.Equal(0.2f, loaded.eye.highlights[1].glowHeight);
            Assert.Equal(1.5f, loaded.eye.highlights[1].glowStrength);
            Assert.Equal(0.5f, loaded.eye.highlights[1].glowColor.g);
            Assert.Equal(0.3f, loaded.eye.highlights[1].glowColor.a);
            Assert.Equal(0.8f, loaded.eye.highlights[1].color.g);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 白目の影の要素の無いファイルは既定()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye /></MToonMaid>");

            Assert.False(s.eye.scleraShadowEnabled);
            Assert.False(s.eye.scleraColorOverride);
            Assert.Equal(0xD1 / 255f, s.eye.scleraColor.r, 5);
            Assert.Equal(0xD4 / 255f, s.eye.scleraColor.g, 5);
            Assert.Equal(0xE9 / 255f, s.eye.scleraColor.b, 5);
            Assert.Equal(1f, s.eye.scleraShadow);
            Assert.Equal(0xE0 / 255f, s.eye.scleraShadowColor.r, 5);
            Assert.Equal(0xD3 / 255f, s.eye.scleraShadowColor.g, 5);
            Assert.Equal(0xC9 / 255f, s.eye.scleraShadowColor.b, 5);
            Assert.Equal(0.5f, s.eye.scleraShadowHeight);
            Assert.Equal(0.15f, s.eye.scleraShadowSoftness);
            Assert.Equal(0.2f, s.eye.scleraShadowBend);
            Assert.Equal(0.2f, s.eye.scleraLidFollow);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 白目の影の範囲外の値とNaNは丸める()
        {
            var s = LoadXml("<?xml version=\"1.0\"?><MToonMaid version=\"1\"><eye>"
                + "<scleraShadow>3</scleraShadow><scleraShadowHeight>-1</scleraShadowHeight>"
                + "<scleraShadowSoftness>NaN</scleraShadowSoftness><scleraShadowBend>2</scleraShadowBend>"
                + "<scleraLidFollow>-0.5</scleraLidFollow>"
                + "</eye></MToonMaid>");

            Assert.Equal(1f, s.eye.scleraShadow);
            Assert.Equal(0f, s.eye.scleraShadowHeight);
            Assert.Equal(0f, s.eye.scleraShadowSoftness);
            Assert.Equal(1f, s.eye.scleraShadowBend);
            Assert.Equal(0f, s.eye.scleraLidFollow);
        }

        [Fact]
        public void 白目の影を保存して読み直すと同じ値になる()
        {
            var path = Path.Combine(_dir, "sclera.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.eye.scleraShadowEnabled = true;
            s.eye.scleraColorOverride = true;
            s.eye.scleraColor = new ColorData(0.9f, 0.95f, 1f, 1f);
            s.eye.scleraShadow = 0.7f;
            s.eye.scleraShadowColor = new ColorData(0.5f, 0.6f, 0.7f, 1f);
            s.eye.scleraShadowHeight = 0.3f;
            s.eye.scleraShadowSoftness = 0.4f;
            s.eye.scleraShadowBend = 0.6f;
            s.eye.scleraLidFollow = 0.8f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.True(loaded.eye.scleraShadowEnabled);
            Assert.True(loaded.eye.scleraColorOverride);
            Assert.Equal(0.95f, loaded.eye.scleraColor.g);
            Assert.Equal(0.7f, loaded.eye.scleraShadow);
            Assert.Equal(0.6f, loaded.eye.scleraShadowColor.g);
            Assert.Equal(0.3f, loaded.eye.scleraShadowHeight);
            Assert.Equal(0.4f, loaded.eye.scleraShadowSoftness);
            Assert.Equal(0.6f, loaded.eye.scleraShadowBend);
            Assert.Equal(0.8f, loaded.eye.scleraLidFollow);
            Assert.Empty(_warnings);
        }
    }
}
