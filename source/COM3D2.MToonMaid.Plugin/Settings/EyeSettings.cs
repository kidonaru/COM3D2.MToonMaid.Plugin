using System;
using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 瞳のハイライト 1 個分。位置・大きさは瞳の幅を 1 とした値で、位置は瞳の中心から画面の右・上がプラス。
    /// 大きさは楕円の直径、ロールは度 (画面上で反時計回りがプラス)、にじみの幅 / 高さは楕円の横 / 縦の軸の向きに縁から外へ広げる幅。
    /// color は楕円の内側 (芯) の色、glowColor のアルファはにじみの不透明度。glowStrength はにじみの濃さの倍率 (1 で等倍)
    /// </summary>
    public class EyeHighlightSettings
    {
        // XML で要素が欠けたときの値
        public float strength = 1f;
        public float x;
        public float y;
        public float width = 0.1f;
        public float height = 0.1f;
        public float roll;
        public ColorData color;
        public ColorData glowColor;
        public float glowStrength = 1f;
        public float glowWidth = 0.05f;
        public float glowHeight = 0.05f;
    }

    /// <summary>
    /// メイド 1 人分の瞳の光沢の設定。共通の値の継承には乗せない。
    /// 参照型 (色・ハイライト) は null のまま読み、FillMissing で補う
    /// </summary>
    public class EyeSettings
    {
        // シェーダーの配列の長さと揃える
        public const int MaxHighlightCount = 8;
        public const float MaxHighlightOffset = 0.5f;
        public const float MaxHighlightSize = 1f;
        public const float MaxHighlightRoll = 180f;
        public const float MaxGlowWidth = 0.3f;
        // 1 までだとにじみの色のアルファと同じく薄めることしかできない
        public const float MaxGlowStrength = 2f;
        public const float MaxParallax = 1f;

        public bool enabled = true;
        public bool hideEyeHighlight;
        /// <summary>ON のときだけゲームの瞳のパーツカラーを書く。瞳のプリセットの適用で色を変えるかはプリセット側のこの値で決まる。OFF ならゲームの値のまま触らない</summary>
        public bool colorEnabled;
        /// <summary>瞳に主光源・環境光・追加ライトの明るさを掛ける割合。0 はライトに関係なくテクスチャの色のまま</summary>
        public float irisLightInfluence = 0.85f;

        public float upperShadow;
        public ColorData upperShadowColor;
        public float upperShadowHeight = 0.5f;
        public float upperShadowSoftness = 0.6f;

        public float lowerGlow;
        public ColorData lowerGlowColor;
        public float lowerGlowHeight = 0.5f;
        public float lowerGlowSoftness = 0.7f;

        // 初期値を入れておくと XmlSerializer が読んだ要素を後ろへ足し、既定と二重になる
        public List<EyeHighlightSettings> highlights;

        public float parallax = -0.1f;
        /// <summary>まぶたを閉じた割合 (0〜1) にこの値を掛けた割合だけ、ハイライトの Y を lidCenter へ寄せる</summary>
        public float lidFollow = 0.4f;
        /// <summary>まぶたを閉じたときハイライトを寄せる先の Y (瞳の幅を 1 とした値で、中心が 0、上がプラス)</summary>
        public float lidCenter = -0.15f;
        /// <summary>ハイライトに主光源と環境光の明るさを掛ける割合。0 はライトに関係なく設定した色のまま</summary>
        public float highlightLightInfluence = 0.5f;
        /// <summary>白目の影を描く。OFF なら白目はテクスチャの描き込みのまま</summary>
        public bool scleraShadowEnabled;
        /// <summary>白目の色を scleraColor にする。OFF ならテクスチャの白目の白い部分の色</summary>
        public bool scleraColorOverride;
        public ColorData scleraColor;
        public float scleraShadow = 1f;
        public ColorData scleraShadowColor;
        public float scleraShadowHeight = 0.5f;
        public float scleraShadowSoftness = 0.15f;
        /// <summary>白目の左右の端ほど影の縁を下げる量 (白目の高さに対する割合)</summary>
        public float scleraShadowBend = 0.2f;
        /// <summary>まぶたを閉じた割合 (0〜1) にこの値を掛けた分 (白目の高さに対する割合) だけ影を下へずらす</summary>
        public float scleraLidFollow = 0.2f;

        public static EyeSettings CreateDefault()
        {
            var s = new EyeSettings();
            s.FillMissing();
            return s;
        }

        /// <summary>null の参照型を既定で埋め、ハイライトを上限へ切り詰め、値を範囲へ丸める</summary>
        public void FillMissing()
        {
            if (upperShadowColor == null)
            {
                upperShadowColor = new ColorData(0x34 / 255f, 0x6C / 255f, 0xDF / 255f, 1f);
            }
            if (lowerGlowColor == null)
            {
                lowerGlowColor = new ColorData(1f, 0xB9 / 255f, 0x37 / 255f, 1f);
            }
            if (scleraColor == null)
            {
                scleraColor = new ColorData(0xD1 / 255f, 0xD4 / 255f, 0xE9 / 255f, 1f);
            }
            if (scleraShadowColor == null)
            {
                // 実機の口のアトラスの描き込み (白 (246,243,237)・影 (216,201,187)) の比。強さ 1 で元の濃さになる
                scleraShadowColor = new ColorData(0xE0 / 255f, 0xD3 / 255f, 0xC9 / 255f, 1f);
            }
            if (highlights == null)
            {
                highlights = new List<EyeHighlightSettings>();
            }
            highlights.RemoveAll(h => h == null);
            if (highlights.Count > MaxHighlightCount)
            {
                highlights.RemoveRange(MaxHighlightCount, highlights.Count - MaxHighlightCount);
            }
            foreach (var h in highlights)
            {
                if (h.color == null)
                {
                    h.color = DefaultCoreColor();
                }
                if (h.glowColor == null)
                {
                    h.glowColor = DefaultGlowColor();
                }
            }
            ClampValues();
        }

        /// <summary>[追加] で足すハイライト。瞳の中心の小さな丸</summary>
        public static EyeHighlightSettings CreateNewHighlight()
        {
            return new EyeHighlightSettings { color = DefaultCoreColor(), glowColor = DefaultGlowColor() };
        }

        private static ColorData DefaultCoreColor()
        {
            return new ColorData(1f, 1f, 1f, 1f);
        }

        private static ColorData DefaultGlowColor()
        {
            return new ColorData(0xA0 / 255f, 0xB4 / 255f, 1f, 1f);
        }

        private void ClampValues()
        {
            upperShadow = Clamp01(upperShadow);
            upperShadowHeight = Clamp01(upperShadowHeight);
            upperShadowSoftness = Clamp01(upperShadowSoftness);
            lowerGlow = Clamp01(lowerGlow);
            lowerGlowHeight = Clamp01(lowerGlowHeight);
            lowerGlowSoftness = Clamp01(lowerGlowSoftness);
            scleraShadow = Clamp01(scleraShadow);
            scleraShadowHeight = Clamp01(scleraShadowHeight);
            scleraShadowSoftness = Clamp01(scleraShadowSoftness);
            scleraShadowBend = Clamp01(scleraShadowBend);
            scleraLidFollow = Clamp01(scleraLidFollow);
            // 下限 (-1) に寄せると、壊れた値で視差が最大の逆向きになる
            parallax = float.IsNaN(parallax) ? 0f : Clamp(parallax, -MaxParallax, MaxParallax);
            lidFollow = Clamp01(lidFollow);
            // 下限 (-0.5) に寄せると、壊れた値で寄せる先が瞳の下端になる
            lidCenter = float.IsNaN(lidCenter) ? 0f : Clamp(lidCenter, -MaxHighlightOffset, MaxHighlightOffset);
            // 下限 (0) に寄せると、壊れた値でライトを無視した見た目に変わる
            highlightLightInfluence = float.IsNaN(highlightLightInfluence) ? 1f : Clamp01(highlightLightInfluence);
            irisLightInfluence = float.IsNaN(irisLightInfluence) ? 1f : Clamp01(irisLightInfluence);
            foreach (var h in highlights)
            {
                h.strength = Clamp01(h.strength);
                h.x = Clamp(h.x, -MaxHighlightOffset, MaxHighlightOffset);
                h.y = Clamp(h.y, -MaxHighlightOffset, MaxHighlightOffset);
                h.width = Clamp(h.width, 0f, MaxHighlightSize);
                h.height = Clamp(h.height, 0f, MaxHighlightSize);
                h.roll = Clamp(h.roll, -MaxHighlightRoll, MaxHighlightRoll);
                h.glowWidth = Clamp(h.glowWidth, 0f, MaxGlowWidth);
                h.glowHeight = Clamp(h.glowHeight, 0f, MaxGlowWidth);
                h.glowStrength = Clamp(h.glowStrength, 0f, MaxGlowStrength);
            }
        }

        private static float Clamp01(float v)
        {
            return Clamp(v, 0f, 1f);
        }

        private static float Clamp(float v, float min, float max)
        {
            // Math.Min / Max は NaN をそのまま返し、手編集の NaN がシェーダーまで届いて瞳が破綻する
            return float.IsNaN(v) ? min : Math.Max(min, Math.Min(max, v));
        }
    }
}
