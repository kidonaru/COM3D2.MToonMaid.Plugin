using System;
using System.Xml.Serialization;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 瞳以外のプリセット 1 個分。共通と各グループの調整値 (個別にした項目を含む)。
    /// メイドの ON/OFF・除外スロット・瞳は持たない
    /// </summary>
    [XmlRoot("MToonMaidPreset")]
    public class MaidPreset : IPresetData
    {
        public const int CurrentVersion = 1;

        [XmlAttribute] public int version = CurrentVersion;
        public GroupSettings common;
        public GroupSettings skin;
        public GroupSettings face;
        public GroupSettings hair;
        public GroupSettings costume;

        /// <summary>保存用。settings の実体を写さずに参照する</summary>
        public static MaidPreset From(MaidSettings settings)
        {
            var preset = new MaidPreset { common = settings.common };
            foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
            {
                preset.Set(group, settings.Get(group));
            }
            return preset;
        }

        public GroupSettings Get(PartGroup group)
        {
            switch (group)
            {
                case PartGroup.Skin: return skin;
                case PartGroup.Face: return face;
                case PartGroup.Hair: return hair;
                case PartGroup.Costume: return costume;
                default: throw new ArgumentOutOfRangeException(nameof(group), group, null);
            }
        }

        private void Set(PartGroup group, GroupSettings value)
        {
            switch (group)
            {
                case PartGroup.Skin: skin = value; break;
                case PartGroup.Face: face = value; break;
                case PartGroup.Hair: hair = value; break;
                case PartGroup.Costume: costume = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(group), group, null);
            }
        }

        public void StampCurrentVersion()
        {
            version = CurrentVersion;
        }

        public void FillMissing()
        {
            if (common == null)
            {
                common = GroupSettings.CreateDefault();
            }
            else
            {
                common.FillMissing();
            }
            foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
            {
                var g = Get(group);
                if (g == null)
                {
                    Set(group, MaidSettings.CreateGroupDefault(group));
                }
                else
                {
                    g.FillMissing();
                }
            }
        }
    }
}
