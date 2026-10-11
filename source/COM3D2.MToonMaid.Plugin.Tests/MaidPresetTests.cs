using System;
using System.Collections.Generic;
using System.IO;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class MaidPresetTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public MaidPresetTests()
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

        private PresetStore<MaidPreset> CreateStore()
        {
            return new PresetStore<MaidPreset>(_dir, "プリセット", _warnings.Add);
        }

        private static MaidSettings Filled()
        {
            var s = new MaidSettings();
            s.FillMissing();
            return s;
        }

        [Fact]
        public void 共通と部位の値と個別を保存して読み直せる()
        {
            var s = Filled();
            s.common.shadingShift = 0.3f;
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineWidth, true);
            s.hair.outlineWidth = 0.004f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.HairShadow, true);
            s.face.hairShadow = 0.4f;
            var store = CreateStore();

            Assert.True(store.Save("p", MaidPreset.From(s)));
            var loaded = store.Load("p");

            Assert.NotNull(loaded);
            Assert.Equal(0.3f, loaded.common.shadingShift);
            Assert.Equal(new[] { GroupSettingKeys.OutlineWidth }, loaded.hair.overrides);
            Assert.Equal(0.004f, loaded.hair.outlineWidth);
            Assert.Equal(0.4f, loaded.face.hairShadow);
            Assert.Equal(new[] { GroupSettingKeys.ShadingToony }, loaded.skin.overrides);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 適用すると共通と部位の値が入れ替わり瞳と除外スロットとONOFFは残る()
        {
            var source = Filled();
            source.common.shadingShift = 0.3f;
            source.SetOverride(PartGroup.Costume, GroupSettingKeys.ShadingShift, true);
            source.costume.shadingShift = -0.4f;
            var store = CreateStore();
            store.Save("p", MaidPreset.From(source));

            var target = Filled();
            target.enabled = true;
            target.SetSlotExcluded("accHat", true);
            target.eye.upperShadow = 0.1f;
            target.SetOverride(PartGroup.Hair, GroupSettingKeys.RimLiftOffset, true);

            target.ApplyPreset(store.Load("p"));

            Assert.Equal(0.3f, target.Resolve(PartGroup.Hair).shadingShift);
            Assert.Equal(-0.4f, target.Resolve(PartGroup.Costume).shadingShift);
            Assert.Empty(target.hair.overrides);
            Assert.True(target.enabled);
            Assert.Equal(new[] { "accHat" }, target.excludedSlots);
            Assert.Equal(0.1f, target.eye.upperShadow);
        }

        [Fact]
        public void 欠けたグループは新しいメイドと同じ既定で埋める()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "part.xml"),
                "<?xml version=\"1.0\"?><MToonMaidPreset version=\"1\"><common><shadingShift>0.2</shadingShift></common></MToonMaidPreset>");

            var loaded = CreateStore().Load("part");

            Assert.Equal(0.2f, loaded.common.shadingShift);
            Assert.NotNull(loaded.common.shadeColor);
            Assert.NotNull(loaded.hair.rimColor);
            // 肌だけ境界の硬さを個別にする既定は新しいメイドと揃える
            Assert.Equal(Filled().skin.overrides, loaded.skin.overrides);
            Assert.Empty(loaded.face.overrides);
        }

        [Fact]
        public void 一覧は名前順()
        {
            var store = CreateStore();
            store.Save("b", MaidPreset.From(Filled()));
            store.Save("A", MaidPreset.From(Filled()));

            Assert.Equal(new[] { "A", "b" }, store.Names);
        }

        [Fact]
        public void 読めないファイルはプリセットの名前で警告してnull()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "broken.xml"), "<not-xml");

            Assert.Null(CreateStore().Load("broken"));
            Assert.Single(_warnings);
            Assert.StartsWith("プリセット", _warnings[0]);
        }
    }
}
