using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class ToonMaterialClassifierTests
    {
        [Theory]
        [InlineData("CM3D2/Toony_Lighted", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Hair_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Cutout_AtC", true)]
        [InlineData("CM3D2/Toony_Lighted_Trans", false)]
        [InlineData("CM3D2/Toony_Lighted_Trans_NoZ", false)]
        [InlineData("CM3D2/Lighted_Trans", false)]
        [InlineData("com3d2mod/ToonyLighted", false)]
        [InlineData("COM3D2/MToon10", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsTarget(string shaderName, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsTarget(shaderName));
        }

        [Theory]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Hair_Outline", true)]
        [InlineData("CM3D2/Toony_Lighted", false)]
        [InlineData("CM3D2/Toony_Lighted_Cutout_AtC", false)]
        public void HasOutline(string shaderName, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.HasOutline(shaderName));
        }

        [Theory]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Hair_Outline_Tex", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex_Texwidth", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline", false)]
        [InlineData("CM3D2/Toony_Lighted_Hair_Outline", false)]
        [InlineData("CM3D2/Toony_Lighted", false)]
        [InlineData(null, false)]
        public void UsesOutlineTex(string shaderName, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.UsesOutlineTex(shaderName));
        }

        [Theory]
        [InlineData("CM3D2/Toony_Lighted_Cutout_AtC", true)]
        [InlineData("CM3D2/Toony_Lighted_Outline", false)]
        public void IsCutout(string shaderName, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsCutout(shaderName));
        }

        [Theory]
        [InlineData("CM3D2/Toony_Lighted_Muscle_Outline", 0.75f)]
        [InlineData("CM3D2/Toony_Lighted_Muscle_Outline_Tex", 0.75f)]
        [InlineData("CM3D2/Toony_Lighted_Outline_Tex", ToonMaterialClassifier.DiagonalShadowRateV)]
        [InlineData("CM3D2/Toony_Lighted", ToonMaterialClassifier.DiagonalShadowRateV)]
        [InlineData(null, ToonMaterialClassifier.DiagonalShadowRateV)]
        public void 影の割合を読む行はMuscleだけ固定(string shaderName, float expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.ShadowRateToonV(shaderName));
        }

        [Theory]
        [InlineData("body", PartGroup.Skin)]
        [InlineData("head", PartGroup.Face)]
        [InlineData("hairF", PartGroup.Hair)]
        [InlineData("hairR", PartGroup.Hair)]
        [InlineData("hairS", PartGroup.Hair)]
        [InlineData("hairT", PartGroup.Hair)]
        [InlineData("hairAho", PartGroup.Hair)]
        [InlineData("onepiece", PartGroup.Costume)]
        [InlineData("chikubi", PartGroup.Costume)]
        [InlineData("accHat", PartGroup.Costume)]
        [InlineData(null, PartGroup.Costume)]
        public void GetGroup(string category, PartGroup expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.GetGroup(category));
        }

        [Theory]
        [InlineData(PartGroup.Face, true)]
        [InlineData(PartGroup.Skin, false)]
        [InlineData(PartGroup.Hair, false)]
        [InlineData(PartGroup.Costume, false)]
        public void 前髪の影を受けるのは顔だけ(PartGroup group, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.UsesHairShadow(group));
        }

        [Theory]
        [InlineData(PartGroup.Face, true)]
        [InlineData(PartGroup.Skin, false)]
        [InlineData(PartGroup.Hair, false)]
        [InlineData(PartGroup.Costume, false)]
        public void 影を受ける落とすが効くのは顔だけ(PartGroup group, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.UsesRendererShadow(group));
        }

        [Theory]
        [InlineData("head", 0, "CM3D2/Toony_Lighted_Trans", true)]
        [InlineData("head", 1, "CM3D2/Toony_Lighted_Trans", true)]
        [InlineData("head", 2, "CM3D2/Toony_Lighted_Trans", false)]
        [InlineData("head", 0, "CM3D2/Toony_Lighted_Trans_NoZ", false)]
        [InlineData("head", 0, "CM3D2/Lighted_Trans", false)]
        [InlineData("head", 0, "COM3D2/MToonMaidEye", false)]
        [InlineData("head", 0, null, false)]
        [InlineData("body", 0, "CM3D2/Toony_Lighted_Trans", false)]
        [InlineData(null, 0, "CM3D2/Toony_Lighted_Trans", false)]
        public void 瞳は顔の番号0と1の半透明トゥーンだけ(string slot, int index, string shader, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsEyeMaterial(slot, index, shader));
        }

        [Theory]
        [InlineData("head", 2, false, "CM3D2/Lighted_Trans", true)]
        [InlineData("head", 2, true, "CM3D2/Lighted_Trans", true)]
        [InlineData("head", 7, true, "CM3D2/Lighted_Trans", true)]
        [InlineData("head", 7, false, "CM3D2/Lighted_Trans", false)]
        // 古い顔に eye_hi_r の値だけが残っているときの bukkake
        [InlineData("head", 7, true, "CM3D2/Toony_Lighted_Trans_NoZ", false)]
        // 顔 MOD で番号 2 が SkinHi でない
        [InlineData("head", 2, false, "CM3D2/Toony_Lighted", false)]
        [InlineData("head", 2, false, null, false)]
        [InlineData("head", 3, true, "CM3D2/Lighted_Trans", false)]
        [InlineData("head", 0, true, "CM3D2/Lighted_Trans", false)]
        [InlineData("body", 2, false, "CM3D2/Lighted_Trans", false)]
        public void ハイライトの板は顔の番号2と右目用の7のSkinHi(string slot, int index, bool hasRight, string shader, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsEyeHighlightMaterial(slot, index, hasRight, shader));
        }

        [Theory]
        [InlineData("Face006_Mouth", true)]
        [InlineData("face003_mouth (Instance)", true)]
        [InlineData("Face007_GP_EyeWhite (Instance)", true)]
        [InlineData("face_tokinagare_Skin (Instance)", false)]
        [InlineData("Face007_GP_EyeL (Instance)", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void 白目のマテリアルは名前にMouthかEyeWhiteを含むもの(string name, bool expected)
        {
            Assert.Equal(expected, ToonMaterialClassifier.IsScleraMaterialName(name));
        }
    }
}
