using System;
using System.Xml.Serialization;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>瞳のプリセット 1 個分。光沢の設定 (eye.enabled は適用しない) と、ゲームの瞳のパーツカラー</summary>
    [XmlRoot("MToonMaidEyePreset")]
    public class EyePreset : IPresetData
    {
        public const int CurrentVersion = 1;

        [XmlAttribute] public int version = CurrentVersion;
        public EyeSettings eye;
        public EyeColorSnapshot color;

        public void StampCurrentVersion()
        {
            version = CurrentVersion;
        }

        public void FillMissing()
        {
            if (eye == null)
            {
                eye = EyeSettings.CreateDefault();
            }
            else
            {
                eye.FillMissing();
            }
            color.left = color.left.Clamped();
            color.right = color.right.Clamped();
            if (!Enum.IsDefined(typeof(EyeColorSide), color.side))
            {
                color.side = EyeColorSide.Both;
            }
        }
    }
}
