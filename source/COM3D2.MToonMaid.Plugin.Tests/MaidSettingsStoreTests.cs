using System;
using System.Collections.Generic;
using System.IO;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class MaidSettingsStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public MaidSettingsStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        [Fact]
        public void 保存して読み直すと同じ値になる()
        {
            var path = Path.Combine(_dir, "a.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.enabled = true;
            s.hair.shadingShift = 0.3f;
            s.hair.rimColorOverride = true;
            s.hair.rimColor = new ColorData(0.1f, 0.2f, 0.3f, 1f);
            s.hair.useGameShadowRate = false;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.True(loaded.enabled);
            Assert.Equal(0.3f, loaded.hair.shadingShift);
            Assert.False(loaded.hair.useGameShadowRate);
            Assert.True(loaded.hair.rimColorOverride);
            Assert.Equal(0.2f, loaded.hair.rimColor.g);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void ファイルが無ければOFFの既定値()
        {
            var store = new MaidSettingsStore(_dir, _warnings.Add);
            var s = store.Get("missing-guid");

            Assert.False(s.enabled);
            Assert.NotNull(s.skin);
            Assert.NotNull(s.costume.shadeColor);
        }

        [Fact]
        public void 欠けた項目は既定値で補う()
        {
            var path = Path.Combine(_dir, "b.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"1\"><enabled>true</enabled>" +
                "<hair><shadingShift>0.5</shadingShift></hair></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.True(s.enabled);
            Assert.Equal(0.5f, s.hair.shadingShift);
            Assert.Equal(0.95f, s.hair.shadingToony);          // フィールド初期値
            Assert.True(s.hair.useGameShadowRate);
            Assert.NotNull(s.hair.shadeColor);                  // null だった参照型は補う
            Assert.Equal(GroupSettings.CreateDefault().shadeColor.g, s.skin.shadeColor.g);
        }

        [Fact]
        public void 版1のファイルは顔の陰影の濃さを個別の0_8にする()
        {
            var path = Path.Combine(_dir, "v1.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"1\"><enabled>true</enabled>" +
                "<face><shadeStrength>1.5</shadeStrength><overrides /></face></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Contains(GroupSettingKeys.ShadeStrength, s.face.overrides);
            Assert.Equal(0.8f, s.Resolve(PartGroup.Face).shadeStrength);
            Assert.Equal(GroupSettings.CreateDefault().shadeStrength, s.Resolve(PartGroup.Skin).shadeStrength);
        }

        [Fact]
        public void 版1で顔の陰影の濃さを個別にしていれば値を残す()
        {
            var path = Path.Combine(_dir, "v1-override.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"1\"><enabled>true</enabled>" +
                "<face><shadeStrength>1.2</shadeStrength><overrides><string>shadeStrength</string></overrides></face></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(new[] { GroupSettingKeys.ShadeStrength }, s.face.overrides);
            Assert.Equal(1.2f, s.Resolve(PartGroup.Face).shadeStrength);
        }

        [Fact]
        public void 版2のファイルは顔の陰影の濃さを個別にし直さない()
        {
            var path = Path.Combine(_dir, "v2.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"2\"><enabled>true</enabled>" +
                "<face><shadeStrength>1.5</shadeStrength><overrides /></face></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(s.face.overrides);
            Assert.Equal(GroupSettings.CreateDefault().shadeStrength, s.Resolve(PartGroup.Face).shadeStrength);
        }

        [Theory]
        [InlineData(2, 1f, 1.5f)]
        [InlineData(1, 1f, 1.5f)]
        [InlineData(2, 0.6f, 0.6f)]
        [InlineData(3, 1f, 1f)]
        public void 版3より前で前髪の影の強さを個別の1にしていれば新しい既定の1_5にする(int version, float hairShadow, float expected)
        {
            var path = Path.Combine(_dir, "hairshadow-v" + version + "-" + hairShadow + ".xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\"?><MToonMaid version=\"" + version + "\"><enabled>true</enabled>" +
                "<face><hairShadow>" + hairShadow.ToString(System.Globalization.CultureInfo.InvariantCulture) + "</hairShadow>" +
                "<overrides><string>hairShadow</string></overrides></face></MToonMaid>");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(expected, s.Resolve(PartGroup.Face).hairShadow);
        }

        [Fact]
        public void 壊れたファイルはbakへ退避してOFF()
        {
            var path = Path.Combine(_dir, "c.xml");
            File.WriteAllText(path, "<MToonMaid><enabled>tr");

            var s = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.False(s.enabled);
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".bak"));
            Assert.Single(_warnings);
        }

        [Fact]
        public void 変更から1秒経つまで書かない()
        {
            var store = new MaidSettingsStore(_dir, _warnings.Add);
            var s = store.Get("g1");
            s.enabled = true;

            store.MarkDirty("g1", 10f);
            store.FlushIfDue(10.5f);
            Assert.False(File.Exists(Path.Combine(_dir, "g1.xml")));

            store.FlushIfDue(11.1f);
            Assert.True(File.Exists(Path.Combine(_dir, "g1.xml")));
        }

        [Fact]
        public void ファイル名に使えない文字はGUIDから除く()
        {
            var store = new MaidSettingsStore(_dir, _warnings.Add);
            store.Get("a/b");
            store.MarkDirty("a/b", 0f);
            store.FlushAll();

            Assert.True(File.Exists(Path.Combine(_dir, "a_b.xml")));
        }

        [Fact]
        public void 読み込んでいないGUIDのMarkDirtyは無視する()
        {
            var store = new MaidSettingsStore(_dir, _warnings.Add);
            store.MarkDirty("never-loaded", 0f);
            store.FlushAll();

            Assert.False(File.Exists(Path.Combine(_dir, "never-loaded.xml")));
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 上書き保存で一時ファイルが残らない()
        {
            var path = Path.Combine(_dir, "d.xml");
            var s = new MaidSettings();
            s.FillMissing();
            MaidSettingsStore.Save(path, s);
            s.enabled = true;
            MaidSettingsStore.Save(path, s);

            Assert.True(MaidSettingsStore.Load(path, _warnings.Add).enabled);
            Assert.False(File.Exists(path + ".tmp"));
        }

        [Fact]
        public void スロットの既定は対象()
        {
            var s = new MaidSettings();
            s.FillMissing();

            Assert.Equal(1f, s.hair.highlightScale);
            Assert.True(s.IsSlotActive("hairF"));
            Assert.Equal(1f, GroupSettings.CreateDefault().shadeColor.g);
        }

        [Fact]
        public void 除外したスロットだけ対象外()
        {
            var s = new MaidSettings();
            s.FillMissing();
            s.SetSlotExcluded("accHat", true);
            s.SetSlotExcluded("accHat", true);

            Assert.False(s.IsSlotActive("accHat"));
            Assert.True(s.IsSlotActive("wear"));
            Assert.Single(s.excludedSlots);

            s.SetSlotExcluded("accHat", false);
            Assert.True(s.IsSlotActive("accHat"));
        }

        [Fact]
        public void スロットを一括でOFFにしてONに戻せる()
        {
            var s = new MaidSettings();
            s.FillMissing();
            var listed = new[] { "accHat", "wear" };

            s.SetSlotsActive(listed, false);
            Assert.False(s.IsSlotActive("accHat"));
            Assert.False(s.IsSlotActive("wear"));

            s.SetSlotsActive(listed, true);
            Assert.True(s.IsSlotActive("accHat"));
            Assert.True(s.IsSlotActive("wear"));
            Assert.Empty(s.excludedSlots);
        }

        [Fact]
        public void 除外スロットを保存して読み直せる()
        {
            var path = Path.Combine(_dir, "e.xml");
            var s = new MaidSettings();
            s.FillMissing();
            s.hair.highlightScale = 0.5f;
            s.SetSlotExcluded("accHat", true);

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(0.5f, loaded.hair.highlightScale);
            Assert.Equal(new[] { "accHat" }, loaded.excludedSlots);
            Assert.Equal(MaidSettings.CurrentVersion, loaded.version);
        }
    }
}
