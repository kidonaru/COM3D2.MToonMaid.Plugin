using MaidStatus;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>値はゲームのセーブに入る。左右の選択はエディットと共有する maid.status.eyePartsTab</summary>
    public static class MaidEyeColor
    {
        public static EyeColorSide GetSide(Maid maid)
        {
            switch (maid.status.eyePartsTab)
            {
                case EyePartsTab.L: return EyeColorSide.Left;
                case EyePartsTab.R: return EyeColorSide.Right;
                default: return EyeColorSide.Both;
            }
        }

        /// <summary>左右へ切り替えたときは左を右へ写す (エディットと同じ。右の色は失われる)</summary>
        public static void SetSide(Maid maid, EyeColorSide side)
        {
            maid.status.eyePartsTab = ToTab(side);
            if (side == EyeColorSide.Both)
            {
                // Write で写すと右だけ範囲へ丸まり、範囲外の左と食い違って次の描画で右へ戻る
                Set(maid, MaidParts.PARTS_COLOR.EYE_R, Read(maid, EyeColorSide.Left));
            }
        }

        /// <summary>左右の値が違うのに左右が選ばれていれば右へ切り替えて返す</summary>
        public static EyeColorSide ResolveAndStoreSide(Maid maid)
        {
            var stored = GetSide(maid);
            var side = EyeColorSides.ResolveSide(stored, Read(maid, EyeColorSide.Left), Read(maid, EyeColorSide.Right));
            if (side != stored)
            {
                maid.status.eyePartsTab = EyePartsTab.R;
            }
            return side;
        }

        public static EyeColorSnapshot ReadSnapshot(Maid maid)
        {
            return new EyeColorSnapshot
            {
                left = Read(maid, EyeColorSide.Left),
                right = Read(maid, EyeColorSide.Right),
                side = GetSide(maid),
            };
        }

        /// <summary>左右の値と左右の選択を控えのまま戻す (範囲へ丸めない。左を右へ写さない)</summary>
        public static void Restore(Maid maid, EyeColorSnapshot snapshot)
        {
            maid.status.eyePartsTab = ToTab(snapshot.side);
            Set(maid, MaidParts.PARTS_COLOR.EYE_L, snapshot.left);
            Set(maid, MaidParts.PARTS_COLOR.EYE_R, snapshot.right);
        }

        /// <summary>左右 (Both) では左の値</summary>
        public static EyeColorValues Read(Maid maid, EyeColorSide side)
        {
            var c = maid.Parts.GetPartsColor(side == EyeColorSide.Right ? MaidParts.PARTS_COLOR.EYE_R : MaidParts.PARTS_COLOR.EYE_L);
            return new EyeColorValues
            {
                mainHue = c.m_nMainHue,
                mainChroma = c.m_nMainChroma,
                mainBrightness = c.m_nMainBrightness,
                mainContrast = c.m_nMainContrast,
                shadowRate = c.m_nShadowRate,
                shadowHue = c.m_nShadowHue,
                shadowChroma = c.m_nShadowChroma,
                shadowBrightness = c.m_nShadowBrightness,
                shadowContrast = c.m_nShadowContrast,
            };
        }

        /// <summary>範囲へ丸めて書く。左右 (Both) では両目へ書く</summary>
        public static void Write(Maid maid, EyeColorSide side, EyeColorValues values)
        {
            var v = values.Clamped();
            if (side != EyeColorSide.Right)
            {
                Set(maid, MaidParts.PARTS_COLOR.EYE_L, v);
            }
            if (side != EyeColorSide.Left)
            {
                Set(maid, MaidParts.PARTS_COLOR.EYE_R, v);
            }
        }

        private static void Set(Maid maid, MaidParts.PARTS_COLOR type, EyeColorValues v)
        {
            // 新しい PartsColor を作ると m_bUse が落ち、セーブに残る値が変わる
            var c = maid.Parts.GetPartsColor(type);
            c.m_nMainHue = v.mainHue;
            c.m_nMainChroma = v.mainChroma;
            c.m_nMainBrightness = v.mainBrightness;
            c.m_nMainContrast = v.mainContrast;
            c.m_nShadowRate = v.shadowRate;
            c.m_nShadowHue = v.shadowHue;
            c.m_nShadowChroma = v.shadowChroma;
            c.m_nShadowBrightness = v.shadowBrightness;
            c.m_nShadowContrast = v.shadowContrast;
            maid.Parts.SetPartsColor(type, c);
        }

        private static EyePartsTab ToTab(EyeColorSide side)
        {
            switch (side)
            {
                case EyeColorSide.Left: return EyePartsTab.L;
                case EyeColorSide.Right: return EyePartsTab.R;
                default: return EyePartsTab.LR;
            }
        }
    }
}
