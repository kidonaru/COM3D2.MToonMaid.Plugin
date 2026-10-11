using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 変換前のマテリアルの状態。OFF で元へ戻すために使う。
    /// _OutlineWidth / _RimColor / _OutlineColor は Toony と MToon で同名のため変換で上書きされる。
    /// 戻さないとゲームのアウトライン幅やリム色が壊れるので値も控える
    /// </summary>
    public sealed class MaterialBackup
    {
        public Shader shader;
        public int renderQueue;
        public string[] keywords;
        public PartGroup group;
        public bool hadOutline;
        public bool outlineFromTex;
        public bool isCutout;
        public float shadowRateToonV;
        // 最後に ApplyGroup したときの _ShadowRateToon の有無
        public bool hasShadowRateToon;
        public float outlineWidth;
        public Color rimColor;
        public float rimPower;
        public float rimShift;
        public Color outlineColor;
        // 変換で _ShadowTex に入れた _MainTex。OFF で外さないと Toony がそれを影テクスチャとして描く
        public Texture copiedShadowTex;
        // _ToonRamp の色。ゲームは RenderTexture をプールで使い回すため、テクスチャ単位ではなくマテリアルごとに持つ
        public ToonRampColors ramp = ToonRampColors.White;
        // 読んだときの _ToonRamp。menu のテクスチャ変更で差し替わったら読み直す (0 はランプなし)
        public int rampTextureId;
        // 無限色のランプ (RenderTexture) なら、無限色の変更で中身が描き直される
        public bool rampIsRenderTexture;
    }
}
