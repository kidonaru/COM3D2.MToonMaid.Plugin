using System.Xml.Serialization;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>XML 用の色。UnityEngine.Color は読み取り専用プロパティを持ち XmlSerializer に向かないため別に持つ</summary>
    public class ColorData
    {
        [XmlAttribute] public float r;
        [XmlAttribute] public float g;
        [XmlAttribute] public float b;
        [XmlAttribute] public float a = 1f;

        public ColorData()
        {
        }

        public ColorData(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public ColorData Clone()
        {
            return new ColorData(r, g, b, a);
        }
    }
}
