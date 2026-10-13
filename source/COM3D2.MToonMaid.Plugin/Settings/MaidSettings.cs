using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>メイド 1 人分の設定。GUID ごとに 1 ファイルで保存する</summary>
    [XmlRoot("MToonMaid")]
    public class MaidSettings
    {
        public const int CurrentVersion = 2;
        private const float SkinShadingToony = 0.8f;
        private const float FaceShadeStrength = 0.8f;
        private const int FaceShadeStrengthVersion = 2;

        [XmlAttribute] public int version = CurrentVersion;
        public bool enabled;
        public GroupSettings skin;
        public GroupSettings face;
        public GroupSettings hair;
        public GroupSettings costume;
        // 共通の調整値。グループで個別にしていない項目はこの値で描く
        public GroupSettings common;
        // MToon にしないスロット (TBodySkin.Category)
        public List<string> excludedSlots = new List<string>();
        // 瞳の光沢。メイドの enabled が親スイッチ
        public EyeSettings eye;
        // Resolve の結果。マテリアルの走査ごとに呼ばれるため、グループごとに使い回す
        private readonly GroupSettings[] _resolved = new GroupSettings[Enum.GetValues(typeof(PartGroup)).Length];
        // 部位専用の項目の継承元。書き換えない
        private static readonly GroupSettings Defaults = GroupSettings.CreateDefault();

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

        public void Set(PartGroup group, GroupSettings value)
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

        /// <summary>欠けたグループ・共通と、それぞれの null の参照型を既定で埋める</summary>
        public void FillMissing()
        {
            foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
            {
                var g = Get(group);
                if (g == null)
                {
                    Set(group, CreateGroupDefault(group));
                }
                else
                {
                    g.FillMissing();
                }
            }
            if (common == null)
            {
                common = GroupSettings.CreateDefault();
            }
            else
            {
                common.FillMissing();
            }
            if (excludedSlots == null)
            {
                excludedSlots = new List<string>();
            }
            if (eye == null)
            {
                eye = EyeSettings.CreateDefault();
            }
            else
            {
                eye.FillMissing();
            }
        }

        // 肌の鋭さ・顔の陰影の濃さを GroupSettings のフィールド初期値にすると、共通の既定が変わって他のグループにも効く
        internal static GroupSettings CreateGroupDefault(PartGroup group)
        {
            var g = GroupSettings.CreateDefault();
            if (group == PartGroup.Skin)
            {
                g.shadingToony = SkinShadingToony;
                g.overrides.Add(GroupSettingKeys.ShadingToony);
            }
            else if (group == PartGroup.Face)
            {
                g.shadeStrength = FaceShadeStrength;
                g.overrides.Add(GroupSettingKeys.ShadeStrength);
            }
            return g;
        }

        /// <summary>読み込んだ直後、FillMissing の後に呼ぶ</summary>
        internal void Upgrade()
        {
            // 個別にした値まで 0.8 にすると、ユーザーが選んだ顔の濃さが消える
            if (version < FaceShadeStrengthVersion && !face.IsOverridden(GroupSettingKeys.ShadeStrength))
            {
                face.shadeStrength = FaceShadeStrength;
                face.overrides.Add(GroupSettingKeys.ShadeStrength);
            }
            version = CurrentVersion;
        }

        /// <summary>
        /// マテリアルへ入れる値。項目ごとに、グループで個別ならグループの値、そうでなければ共通の値 (部位専用の項目は既定の値) を使う。
        /// 返す実体は使い回すため、呼び出し側は保持も書き換えもしない
        /// </summary>
        public GroupSettings Resolve(PartGroup group)
        {
            var g = Get(group);
            var resolved = _resolved[(int)group];
            if (resolved == null)
            {
                resolved = GroupSettings.CreateDefault();
                _resolved[(int)group] = resolved;
            }
            foreach (var key in GroupSettingKeys.All)
            {
                resolved.CopyField(key, g.IsOverridden(key) ? g : InheritedFrom(key));
            }
            return resolved;
        }

        /// <summary>個別にするときは今の継承元の値を写してから切り替え、見た目を変えない</summary>
        public void SetOverride(PartGroup group, string key, bool overridden)
        {
            var g = Get(group);
            if (overridden)
            {
                if (!g.IsOverridden(key))
                {
                    g.CopyField(key, InheritedFrom(key));
                    g.overrides.Add(key);
                }
            }
            else
            {
                // 手編集で重複したキーも残さない
                g.overrides.RemoveAll(k => k == key);
            }
        }

        private GroupSettings InheritedFrom(string key)
        {
            return GroupSettingKeys.IsGroupOnly(key) ? Defaults : common;
        }

        // 個別を外すだけにすると、部位の既定で個別にしている項目 (顔の陰影の濃さ・肌の境界の硬さ) まで共通の値へ変わる
        public void ResetGroup(PartGroup group)
        {
            Set(group, CreateGroupDefault(group));
        }

        /// <summary>共通と各グループをプリセットの実体に差し替える。preset は FillMissing 済みで、ほかと共有しないこと</summary>
        public void ApplyPreset(MaidPreset preset)
        {
            common = preset.common;
            foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
            {
                Set(group, preset.Get(group));
            }
        }

        public bool IsSlotActive(string slotCategory)
        {
            return slotCategory == null || !excludedSlots.Contains(slotCategory);
        }

        public void SetSlotsActive(IEnumerable<string> slots, bool active)
        {
            foreach (var slot in slots)
            {
                SetSlotExcluded(slot, !active);
            }
        }

        public void SetSlotExcluded(string slotCategory, bool excluded)
        {
            if (excluded)
            {
                if (!excludedSlots.Contains(slotCategory))
                {
                    excludedSlots.Add(slotCategory);
                }
            }
            else
            {
                excludedSlots.Remove(slotCategory);
            }
        }
    }
}
