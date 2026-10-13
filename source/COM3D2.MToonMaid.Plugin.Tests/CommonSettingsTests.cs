using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class CommonSettingsTests : IDisposable
    {
        private static readonly XmlSerializer GroupSettingsSerializer = new XmlSerializer(typeof(GroupSettings));

        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public CommonSettingsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidCommonTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        private static MaidSettings Filled()
        {
            var s = new MaidSettings();
            s.FillMissing();
            return s;
        }

        /// <summary>key の単位を既定の実体へそれぞれ写し、XML にして比べる</summary>
        private static bool XmlFieldEquals(GroupSettings a, GroupSettings b, string key)
        {
            var x = GroupSettings.CreateDefault();
            var y = GroupSettings.CreateDefault();
            x.CopyField(key, a);
            y.CopyField(key, b);
            return ToXml(x) == ToXml(y);
        }

        private static string ToXml(GroupSettings g)
        {
            using (var writer = new StringWriter())
            {
                GroupSettingsSerializer.Serialize(writer, g);
                return writer.ToString();
            }
        }

        /// <summary>全項目を既定と違う値にする。XmlFieldEquals で全項目が既定と違うことを確かめてから使う</summary>
        private static void ChangeAll(GroupSettings g)
        {
            g.shadeColor.r = 0.5f;
            g.shadeStrength = 0.4f;
            g.useGameShadowRate = false;
            g.shadingShift = 0.3f;
            g.shadingToony = 0.5f;
            g.giEqualization = 0.1f;
            g.additionalLightShade = 0.9f;
            g.hairShadow = 0.6f;
            g.hairShadowMode = HairShadowMode.Light;
            g.hairShadowOffsetX = -0.02f;
            g.hairShadowOffsetY = 0.03f;
            g.hairShadowResolution = 1024;
            g.hairShadowSoftness = 2.5f;
            g.highlightScale = 2f;
            g.rimColorOverride = true;
            g.rimColor.r = 0.7f;
            g.rimFresnelPowerScale = 0.5f;
            g.rimLiftOffset = 0.2f;
            g.rimLightingMix = 0.2f;
            g.outlineEnabled = false;
            g.outlineWidth = 0.005f;
            g.outlineNearDistance = 0.3f;
            g.outlineLightingMix = 0.3f;
            g.outlineColorOverride = true;
            g.outlineColor.g = 0.7f;
            g.receiveShadows = true;
            g.castShadows = false;
            g.flatShadowRamp = true;
            g.faceHalfShadow = false;
            g.faceHalfAngle = 30f;
            g.faceFullAngle = 150f;
            g.faceHalfFadeWidth = 10f;
            g.faceHalfSoftness = 0.01f;
            g.faceFrontAngle = 80f;
            g.faceFrontShadowStrength = 0.8f;
        }

        [Fact]
        public void 影側を一色には既定でOFFで共通から継承する()
        {
            Assert.Contains(GroupSettingKeys.FlatShadowRamp, GroupSettingKeys.All);
            Assert.False(GroupSettingKeys.IsGroupOnly(GroupSettingKeys.FlatShadowRamp));
            Assert.False(GroupSettings.CreateDefault().flatShadowRamp);

            var s = Filled();
            s.common.flatShadowRamp = true;

            Assert.True(s.Resolve(PartGroup.Hair).flatShadowRamp);
        }

        [Fact]
        public void 新しいメイドは顔だけ影側を一色にする()
        {
            var s = Filled();

            Assert.Contains(GroupSettingKeys.FlatShadowRamp, s.face.overrides);
            Assert.True(s.Resolve(PartGroup.Face).flatShadowRamp);
            Assert.False(s.Resolve(PartGroup.Skin).flatShadowRamp);
            Assert.False(s.Resolve(PartGroup.Hair).flatShadowRamp);
            Assert.False(s.Resolve(PartGroup.Costume).flatShadowRamp);
        }

        [Fact]
        public void 影側を一色にの要素が無いファイルはOFFで読む()
        {
            var path = Path.Combine(_dir, "old.xml");
            MaidSettingsStore.Save(path, Filled());
            var xml = File.ReadAllText(path);
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"\s*<flatShadowRamp>\w+</flatShadowRamp>", "")
                .Replace("<string>flatShadowRamp</string>", "");
            Assert.DoesNotContain("flatShadowRamp", xml);
            File.WriteAllText(path, xml);

            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            Assert.False(loaded.Resolve(PartGroup.Face).flatShadowRamp);
        }

        private static readonly string[] FaceHalfKeys =
        {
            GroupSettingKeys.FaceHalfShadow, GroupSettingKeys.FaceHalfAngle, GroupSettingKeys.FaceFullAngle,
            GroupSettingKeys.FaceHalfFadeWidth, GroupSettingKeys.FaceHalfSoftness, GroupSettingKeys.FaceFrontAngle,
            GroupSettingKeys.FaceFrontShadowStrength,
        };

        [Fact]
        public void 半面切り替えの影は既定でONの顔専用の項目()
        {
            foreach (var key in FaceHalfKeys)
            {
                Assert.Contains(key, GroupSettingKeys.All);
                Assert.True(GroupSettingKeys.IsGroupOnly(key));
                Assert.True(GroupSettingKeys.AppliesTo(key, PartGroup.Face));
                Assert.False(GroupSettingKeys.AppliesTo(key, PartGroup.Skin));
                Assert.False(GroupSettingKeys.AppliesTo(key, PartGroup.Hair));
                Assert.False(GroupSettingKeys.AppliesTo(key, PartGroup.Costume));
            }
            var def = GroupSettings.CreateDefault();
            Assert.True(def.faceHalfShadow);
            Assert.Equal(80f, def.faceHalfAngle);
            Assert.Equal(110f, def.faceFullAngle);
            Assert.Equal(50f, def.faceHalfFadeWidth);
            Assert.Equal(0.005f, def.faceHalfSoftness);
            Assert.Equal(60f, def.faceFrontAngle);
            Assert.Equal(1f, def.faceFrontShadowStrength);
            Assert.Equal(180f, GroupSettings.MaxFaceHalfAngle);
            Assert.Equal(180f, GroupSettings.MaxFaceFrontAngle);
            Assert.Equal(1f, GroupSettings.MaxFaceFrontShadowStrength);
            Assert.Equal(90f, GroupSettings.MaxFaceHalfFadeWidth);
            Assert.Equal(0.02f, GroupSettings.MaxFaceHalfSoftness);
        }

        [Fact]
        public void 半面切り替えの影は共通の値を継承しない()
        {
            var s = Filled();
            // 共通タブには出ないが、手編集で共通に値があっても顔へ流れないこと
            s.common.faceHalfShadow = false;
            s.common.faceHalfAngle = 10f;

            Assert.True(s.Resolve(PartGroup.Face).faceHalfShadow);
            Assert.Equal(80f, s.Resolve(PartGroup.Face).faceHalfAngle);
        }

        [Fact]
        public void 半面切り替えの影を保存して読み直せる()
        {
            var path = Path.Combine(_dir, "facehalf.xml");
            var s = Filled();
            foreach (var key in FaceHalfKeys)
            {
                s.SetOverride(PartGroup.Face, key, true);
            }
            s.face.faceHalfShadow = false;
            s.face.faceHalfAngle = 50f;
            s.face.faceFullAngle = 120f;
            s.face.faceHalfFadeWidth = 0f;
            s.face.faceHalfSoftness = 0.012f;
            s.face.faceFrontAngle = 90f;
            s.face.faceFrontShadowStrength = 0.3f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            var face = loaded.Resolve(PartGroup.Face);
            Assert.False(face.faceHalfShadow);
            Assert.Equal(50f, face.faceHalfAngle);
            Assert.Equal(120f, face.faceFullAngle);
            Assert.Equal(0f, face.faceHalfFadeWidth);
            Assert.Equal(0.012f, face.faceHalfSoftness);
            Assert.Equal(90f, face.faceFrontAngle);
            Assert.Equal(0.3f, face.faceFrontShadowStrength);
            Assert.True(loaded.Resolve(PartGroup.Skin).faceHalfShadow);
        }

        [Fact]
        public void 半面切り替えの影の要素が無いファイルは既定で読む()
        {
            var path = Path.Combine(_dir, "old-facehalf.xml");
            MaidSettingsStore.Save(path, Filled());
            var xml = File.ReadAllText(path);
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"\s*<(faceHalf\w*|faceFullAngle|faceFront\w*)>[^<]*</\1>", "");
            Assert.DoesNotContain("faceHalf", xml);
            Assert.DoesNotContain("faceFullAngle", xml);
            Assert.DoesNotContain("faceFront", xml);
            File.WriteAllText(path, xml);

            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            var face = loaded.Resolve(PartGroup.Face);
            Assert.True(face.faceHalfShadow);
            Assert.Equal(80f, face.faceHalfAngle);
            Assert.Equal(110f, face.faceFullAngle);
            Assert.Equal(50f, face.faceHalfFadeWidth);
            Assert.Equal(0.005f, face.faceHalfSoftness);
            Assert.Equal(60f, face.faceFrontAngle);
            Assert.Equal(1f, face.faceFrontShadowStrength);
        }

        [Fact]
        public void アウトラインのライト色の影響の既定は半分()
        {
            Assert.Equal(0.5f, GroupSettings.CreateDefault().outlineLightingMix);
        }

        [Fact]
        public void リムのライト色の影響の既定はゲームの式どおりの1()
        {
            Assert.Equal(1f, GroupSettings.CreateDefault().rimLightingMix);
        }

        [Fact]
        public void ゲームの影の境界は既定でONでずらしは0()
        {
            var d = GroupSettings.CreateDefault();
            Assert.True(d.useGameShadowRate);
            Assert.Equal(0f, d.shadingShift);
        }

        [Fact]
        public void 既定では各グループは共通の値を使う()
        {
            var s = Filled();
            s.common.shadingShift = 0.3f;
            s.common.shadeColor.g = 0.5f;

            Assert.Equal(0.3f, s.Resolve(PartGroup.Hair).shadingShift);
            Assert.Equal(0.5f, s.Resolve(PartGroup.Skin).shadeColor.g);
            Assert.Empty(s.hair.overrides);
        }

        [Fact]
        public void 個別にした項目はグループの値を使い共通の変更が効かない()
        {
            var s = Filled();
            s.common.shadingShift = 0.3f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);

            // 個別にした時点の共通の値を写す (見た目を変えずに個別へ切り替える)
            Assert.Equal(0.3f, s.face.shadingShift);

            s.face.shadingShift = 0.2f;
            s.common.shadingShift = 0.4f;

            Assert.Equal(0.2f, s.Resolve(PartGroup.Face).shadingShift);
            Assert.Equal(0.4f, s.Resolve(PartGroup.Skin).shadingShift);
            Assert.True(s.face.IsOverridden(GroupSettingKeys.ShadingShift));
        }

        [Fact]
        public void 個別を外すと共通の値に戻る()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadingShift, true);
            s.hair.shadingShift = 0.2f;

            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadingShift, false);

            Assert.Equal(s.common.shadingShift, s.Resolve(PartGroup.Hair).shadingShift);
            Assert.Empty(s.hair.overrides);
        }

        [Fact]
        public void 個別にするのを繰り返してもキーは1つ()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadingShift, true);
            s.hair.shadingShift = 0.2f;
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadingShift, true);

            Assert.Single(s.hair.overrides);
            // 既に個別なら共通の値で上書きしない
            Assert.Equal(0.2f, s.hair.shadingShift);
        }

        [Fact]
        public void 個別にした色は共通と別の色になる()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadeColor, true);
            s.hair.shadeColor.r = 0.1f;

            Assert.NotSame(s.common.shadeColor, s.hair.shadeColor);
            Assert.Equal(1f, s.common.shadeColor.r);
            Assert.Equal(1f, s.Resolve(PartGroup.Skin).shadeColor.r);
            Assert.Equal(0.1f, s.Resolve(PartGroup.Hair).shadeColor.r);
        }

        [Fact]
        public void リム色は上書きの有無と色をまとめて継承する()
        {
            var s = Filled();
            s.common.rimColorOverride = true;
            s.common.rimColor.r = 1f;

            Assert.True(s.Resolve(PartGroup.Hair).rimColorOverride);
            Assert.Equal(1f, s.Resolve(PartGroup.Hair).rimColor.r);

            s.SetOverride(PartGroup.Hair, GroupSettingKeys.RimColor, true);
            s.hair.rimColorOverride = false;

            Assert.False(s.Resolve(PartGroup.Hair).rimColorOverride);
            Assert.True(s.Resolve(PartGroup.Skin).rimColorOverride);
        }

        [Fact]
        public void 全項目を個別にすると共通をどう変えてもグループの値のまま()
        {
            var s = Filled();
            var def = GroupSettings.CreateDefault();
            foreach (var key in GroupSettingKeys.All)
            {
                s.SetOverride(PartGroup.Costume, key, true);
            }

            ChangeAll(s.common);

            foreach (var key in GroupSettingKeys.All)
            {
                // ChangeAll が全項目を変えていること (変え漏れがあると下の確認が空振りする)
                Assert.False(XmlFieldEquals(s.common, def, key), key);
                Assert.True(XmlFieldEquals(s.Resolve(PartGroup.Costume), def, key), key);
                var skinSource = s.skin.IsOverridden(key) ? s.skin : GroupSettingKeys.IsGroupOnly(key) ? def : s.common;
                Assert.True(XmlFieldEquals(s.Resolve(PartGroup.Skin), skinSource, key), key);
            }
        }

        [Fact]
        public void 新しいメイドは肌だけ陰影の境界の鋭さを個別に0_8にする()
        {
            var s = Filled();

            Assert.Equal(new[] { GroupSettingKeys.ShadingToony }, s.skin.overrides);
            Assert.Equal(0.8f, s.Resolve(PartGroup.Skin).shadingToony);
            Assert.Equal(new[] { GroupSettingKeys.FlatShadowRamp, GroupSettingKeys.ShadeStrength }, s.face.overrides);
            Assert.Empty(s.hair.overrides);
            Assert.Empty(s.costume.overrides);
            Assert.Equal(GroupSettings.CreateDefault().shadingToony, s.Resolve(PartGroup.Face).shadingToony);
        }

        [Fact]
        public void 新しいメイドは顔だけ陰影の濃さを個別に0_8にする()
        {
            var s = Filled();

            Assert.Equal(0.8f, s.Resolve(PartGroup.Face).shadeStrength);
            Assert.Equal(GroupSettings.CreateDefault().shadeStrength, s.Resolve(PartGroup.Skin).shadeStrength);
            Assert.Equal(GroupSettings.CreateDefault().shadeStrength, s.Resolve(PartGroup.Hair).shadeStrength);
            Assert.Equal(GroupSettings.CreateDefault().shadeStrength, s.Resolve(PartGroup.Costume).shadeStrength);
        }

        [Fact]
        public void 共通に戻すと全項目の個別が外れる()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadingShift, true);
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineColor, true);

            s.ClearOverrides(PartGroup.Hair);

            Assert.Empty(s.hair.overrides);
        }

        [Fact]
        public void 共通と個別を保存して読み直せる()
        {
            var path = Path.Combine(_dir, "v3.xml");
            var s = Filled();
            s.common.shadingShift = 0.3f;
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineWidth, true);
            s.hair.outlineWidth = 0.004f;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(0.3f, loaded.common.shadingShift);
            Assert.Equal(new[] { GroupSettingKeys.OutlineWidth }, loaded.hair.overrides);
            Assert.Equal(0.004f, loaded.Resolve(PartGroup.Hair).outlineWidth);
            Assert.Equal(0.3f, loaded.Resolve(PartGroup.Hair).shadingShift);
            Assert.Empty(loaded.common.overrides);
        }

        [Fact]
        public void 追加ライトの影側は既定1()
        {
            Assert.Contains(GroupSettingKeys.AdditionalLightShade, GroupSettingKeys.All);
            Assert.Equal(1f, GroupSettings.CreateDefault().additionalLightShade);
        }

        [Fact]
        public void 陰影の濃さは既定1_5()
        {
            Assert.Contains(GroupSettingKeys.ShadeStrength, GroupSettingKeys.All);
            Assert.Equal(1.5f, GroupSettings.CreateDefault().shadeStrength);
        }

        [Fact]
        public void 頭打ちの距離の既定はゲームの線と同じ()
        {
            Assert.Contains(GroupSettingKeys.OutlineNearDistance, GroupSettingKeys.All);
            var def = GroupSettings.CreateDefault();
            Assert.Equal(0.7f, def.outlineNearDistance);
            Assert.DoesNotContain("outlineWidthScale", GroupSettingKeys.All);
            Assert.Equal(0.0008f, def.outlineWidth);
        }

        [Fact]
        public void 頭打ちの距離を継承する()
        {
            var s = Filled();
            s.common.outlineNearDistance = 0.5f;
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineNearDistance, true);
            s.hair.outlineNearDistance = 0f;

            Assert.Equal(0f, s.Resolve(PartGroup.Hair).outlineNearDistance);
            Assert.Equal(0.5f, s.Resolve(PartGroup.Skin).outlineNearDistance);
        }

        [Fact]
        public void 旧形式の幅の基準を含む設定を読める()
        {
            var path = Path.Combine(_dir, "outline-legacy.xml");
            var s = Filled();
            // 空の overrides は <overrides /> と書かれ、旧キーを差し込めない
            s.SetOverride(PartGroup.Hair, GroupSettingKeys.OutlineWidth, true);
            MaidSettingsStore.Save(path, s);
            var xml = File.ReadAllText(path)
                .Replace("<outlineNearDistance>0.7</outlineNearDistance>",
                    "<outlineWidthMode>Screen</outlineWidthMode><outlineScreenWidth>0.004</outlineScreenWidth>")
                .Replace("<string>outlineWidth</string>", "<string>outlineWidth</string><string>outlineWidthMode</string>");
            Assert.DoesNotContain("outlineNearDistance", xml);
            Assert.Contains("<string>outlineWidthMode</string>", xml);
            File.WriteAllText(path, xml);

            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Empty(_warnings);
            Assert.Equal(0.7f, loaded.common.outlineNearDistance);
            Assert.Equal(0.7f, loaded.Resolve(PartGroup.Costume).outlineNearDistance);
        }

        [Fact]
        public void 影を受ける落とすは既定で受けずに落とす()
        {
            Assert.Contains(GroupSettingKeys.ReceiveShadows, GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.CastShadows, GroupSettingKeys.All);
            var def = GroupSettings.CreateDefault();
            Assert.False(def.receiveShadows);
            Assert.True(def.castShadows);
        }

        [Fact]
        public void 影を受ける落とすは共通を継承せず個別なら顔の値のまま()
        {
            var s = Filled();
            s.common.receiveShadows = true;
            s.common.castShadows = false;

            Assert.False(s.Resolve(PartGroup.Face).receiveShadows);
            Assert.True(s.Resolve(PartGroup.Face).castShadows);

            s.SetOverride(PartGroup.Face, GroupSettingKeys.ReceiveShadows, true);
            s.SetOverride(PartGroup.Face, GroupSettingKeys.CastShadows, true);
            s.face.receiveShadows = true;
            s.face.castShadows = false;

            Assert.True(s.Resolve(PartGroup.Face).receiveShadows);
            Assert.False(s.Resolve(PartGroup.Face).castShadows);
        }

        [Fact]
        public void 個別を外してから再び個別にすると共通の値から始まる()
        {
            var s = Filled();
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);
            s.face.shadingShift = 0.2f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, false);
            s.common.shadingShift = 0.6f;

            s.SetOverride(PartGroup.Face, GroupSettingKeys.ShadingShift, true);

            Assert.Equal(0.6f, s.face.shadingShift);
        }

        [Fact]
        public void 重複したキーも個別を外せる()
        {
            var s = Filled();
            s.hair.overrides.Add(GroupSettingKeys.ShadingShift);
            s.hair.overrides.Add(GroupSettingKeys.ShadingShift);

            s.SetOverride(PartGroup.Hair, GroupSettingKeys.ShadingShift, false);

            Assert.Empty(s.hair.overrides);
        }

        [Fact]
        public void 解決の結果はグループごとに使い回し共通の変更を次の解決で拾う()
        {
            var s = Filled();
            var first = s.Resolve(PartGroup.Hair);
            s.common.shadingShift = 0.5f;
            var second = s.Resolve(PartGroup.Hair);

            Assert.Same(first, second);
            Assert.NotSame(first, s.Resolve(PartGroup.Skin));
            Assert.Equal(0.5f, second.shadingShift);
        }

        [Fact]
        public void 前髪の影は既定で濃さ1固定右へ4mmずらし()
        {
            Assert.Contains(GroupSettingKeys.HairShadow, GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.HairShadowMode, GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.HairShadowOffsetX, GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.HairShadowOffsetY, GroupSettingKeys.All);
            Assert.DoesNotContain("hairShadowAngle", GroupSettingKeys.All);
            Assert.DoesNotContain("hairShadowYaw", GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.HairShadowResolution, GroupSettingKeys.All);
            Assert.Contains(GroupSettingKeys.HairShadowSoftness, GroupSettingKeys.All);
            Assert.Equal(33, GroupSettingKeys.All.Length);
            var def = GroupSettings.CreateDefault();
            Assert.Equal(1f, def.hairShadow);
            Assert.Equal(HairShadowMode.Fixed, def.hairShadowMode);
            Assert.Equal(0.004f, def.hairShadowOffsetX);
            Assert.Equal(0.02f, GroupSettings.MaxHairShadowOffset);
            Assert.Equal(0f, def.hairShadowOffsetY);
            Assert.Equal(2048, def.hairShadowResolution);
            Assert.Equal(1f, def.hairShadowSoftness);
        }

        [Fact]
        public void 前髪の影の向きを保存して読み直せる()
        {
            var path = Path.Combine(_dir, "v3hs.xml");
            var s = Filled();
            s.SetOverride(PartGroup.Face, GroupSettingKeys.HairShadowMode, true);
            s.face.hairShadowMode = HairShadowMode.Light;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.HairShadow, true);
            s.face.hairShadow = 0.8f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.HairShadowOffsetX, true);
            s.face.hairShadowOffsetX = -0.015f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.HairShadowOffsetY, true);
            s.face.hairShadowOffsetY = 0.02f;
            s.SetOverride(PartGroup.Face, GroupSettingKeys.HairShadowResolution, true);
            s.face.hairShadowResolution = 4096;

            MaidSettingsStore.Save(path, s);
            var loaded = MaidSettingsStore.Load(path, _warnings.Add);

            Assert.Equal(-0.015f, loaded.Resolve(PartGroup.Face).hairShadowOffsetX);
            Assert.Equal(4096, loaded.Resolve(PartGroup.Face).hairShadowResolution);
            Assert.Equal(2048, loaded.Resolve(PartGroup.Skin).hairShadowResolution);

            Assert.Equal(HairShadowMode.Light, loaded.Resolve(PartGroup.Face).hairShadowMode);
            Assert.Equal(HairShadowMode.Fixed, loaded.Resolve(PartGroup.Skin).hairShadowMode);
            Assert.Equal(0.8f, loaded.Resolve(PartGroup.Face).hairShadow);
            Assert.Equal(0.02f, loaded.Resolve(PartGroup.Face).hairShadowOffsetY);
        }

        [Fact]
        public void 部位専用の項目を個別にすると既定の値から始まり外すと既定に戻る()
        {
            var s = Filled();
            s.common.highlightScale = 1.8f;

            s.SetOverride(PartGroup.Hair, GroupSettingKeys.HighlightScale, true);
            Assert.Equal(1f, s.hair.highlightScale);
            s.hair.highlightScale = 0.3f;
            Assert.Equal(0.3f, s.Resolve(PartGroup.Hair).highlightScale);

            s.ClearOverrides(PartGroup.Hair);
            Assert.Equal(1f, s.Resolve(PartGroup.Hair).highlightScale);
        }

        [Fact]
        public void 部位専用の項目は一部のグループにだけ出し他の項目は全グループに出す()
        {
            var groups = (PartGroup[])Enum.GetValues(typeof(PartGroup));
            foreach (var key in GroupSettingKeys.All)
            {
                var count = Array.FindAll(groups, g => GroupSettingKeys.AppliesTo(key, g)).Length;
                if (GroupSettingKeys.IsGroupOnly(key))
                {
                    Assert.InRange(count, 1, groups.Length - 1);
                }
                else
                {
                    Assert.Equal(groups.Length, count);
                }
            }
        }
    }
}
