using System;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>顔のブレンドからまぶたを閉じた割合 (0〜1) を読む。メイド 1 人につき 1 個持つ</summary>
    public sealed class LidClosureReader
    {
        // 毎フレーム morph.GetBlendValues を渡すと、デリゲートの生成で GC が出る
        private TMorph _morph;
        private Func<int, float> _blendValue;

        /// <summary>顔のブレンドが読めなければ 0 (シェーダーではまぶたに合わせて動かさない)</summary>
        public float Read(TBody body)
        {
            var morph = body != null && body.Face != null ? body.Face.morph : null;
            if (morph == null || morph.BlendDataIdx_EyeClose == null)
            {
                return 0f;
            }
            if (morph != _morph)
            {
                _morph = morph;
                _blendValue = morph.GetBlendValues;
            }
            return EyelidClosure.Sum(morph.BlendDataIdx_EyeClose, _blendValue);
        }
    }
}
