using System.Collections.Generic;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class MainDirectionalLightSelectorTests
    {
        private const int FaceLayer = 10;

        private static DirectionalLightInfo Light(float intensity, int mask = -1, bool sun = false)
        {
            return new DirectionalLightInfo
            {
                enabled = true, cullingMask = mask, intensity = intensity, r = 1f, g = 1f, b = 1f, isSun = sun,
            };
        }

        [Fact]
        public void 最も明るい平行光を選ぶ()
        {
            var lights = new List<DirectionalLightInfo> { Light(0.5f), Light(1.2f), Light(0.8f) };
            Assert.Equal(1, MainDirectionalLightSelector.Select(lights, FaceLayer));
        }

        [Fact]
        public void 明るさは強さと色の輝度の積()
        {
            var red = Light(1f);
            red.g = 0f;
            red.b = 0f;
            var gray = Light(0.5f);
            // 赤 1.0 の輝度 0.299 < 白 0.5
            Assert.Equal(1, MainDirectionalLightSelector.Select(new List<DirectionalLightInfo> { red, gray }, FaceLayer));
        }

        [Fact]
        public void 顔のレイヤーを含まない光と無効な光と頂点ライトは選ばない()
        {
            var otherLayer = Light(5f, mask: 1 << 11);
            var disabled = Light(4f);
            disabled.enabled = false;
            var vertex = Light(3f);
            vertex.notImportant = true;
            var lights = new List<DirectionalLightInfo> { otherLayer, disabled, vertex, Light(0.1f) };

            Assert.Equal(3, MainDirectionalLightSelector.Select(lights, FaceLayer));
        }

        [Fact]
        public void sunが候補にあればより明るい光があってもsunを選ぶ()
        {
            var lights = new List<DirectionalLightInfo> { Light(3f), Light(0.8f, sun: true) };
            Assert.Equal(1, MainDirectionalLightSelector.Select(lights, FaceLayer));
        }

        [Fact]
        public void sunでも顔のレイヤーを含まなければ明るい光を選ぶ()
        {
            var lights = new List<DirectionalLightInfo> { Light(3f), Light(0.8f, mask: 1 << 11, sun: true) };
            Assert.Equal(0, MainDirectionalLightSelector.Select(lights, FaceLayer));
        }

        [Fact]
        public void 候補が無ければ負1()
        {
            Assert.Equal(-1, MainDirectionalLightSelector.Select(new List<DirectionalLightInfo>(), FaceLayer));
            Assert.Equal(-1, MainDirectionalLightSelector.Select(new List<DirectionalLightInfo> { Light(1f, mask: 0) }, FaceLayer));
        }
    }
}
