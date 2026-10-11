using System;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>差し替える前の瞳・eye_hi の板の状態。戻すときに使う</summary>
    public sealed class EyeMaterialBackup
    {
        public Shader shader;
        // シェーダーを代入すると renderQueue がシェーダーの既定へ戻るため控える
        public int renderQueue;
    }

    /// <summary>瞳の光沢のシェーダーへの差し替え・戻し・値の設定と、瞳の外接矩形の読み戻し</summary>
    public sealed class EyeConverter : IDisposable
    {
        // 個数が少ないときも MaxHighlightCount 個で渡す (SetVectorArray は最初に渡した長さで配列長が決まり、後から長くできない)
        private static readonly Vector4[] HighlightShape = new Vector4[EyeSettings.MaxHighlightCount];
        private static readonly Vector4[] HighlightParams = new Vector4[EyeSettings.MaxHighlightCount];
        private static readonly Vector4[] HighlightColor = new Vector4[EyeSettings.MaxHighlightCount];
        private static readonly Vector4[] HighlightGlow = new Vector4[EyeSettings.MaxHighlightCount];

        private Texture2D _readback;
        private byte[] _alpha;

        public Shader EyeShader { get; private set; }
        public Shader HiddenShader { get; private set; }
        public Shader EyeHiShader { get; private set; }

        public EyeConverter(Shader eye, Shader hidden, Shader eyeHi)
        {
            EyeShader = eye;
            HiddenShader = hidden;
            EyeHiShader = eyeHi;
        }

        public EyeMaterialBackup ToEye(Material m)
        {
            return Replace(m, EyeShader);
        }

        /// <summary>戻り値は差し替える前のシェーダーと renderQueue</summary>
        public static EyeMaterialBackup Replace(Material m, Shader shader)
        {
            var backup = new EyeMaterialBackup { shader = m.shader, renderQueue = m.renderQueue };
            SetShader(m, shader, backup.renderQueue);
            return backup;
        }

        public static void SetShader(Material m, Shader shader, int renderQueue)
        {
            m.shader = shader;
            m.renderQueue = renderQueue;
        }

        public void Restore(Material m, EyeMaterialBackup backup)
        {
            SetShader(m, backup.shader, backup.renderQueue);
        }

        public static void Apply(Material m, EyeSettings s)
        {
            m.SetFloat(MToonProps.EyeUpperShadow, s.upperShadow);
            m.SetColor(MToonProps.EyeUpperShadowColor, ToColor(s.upperShadowColor));
            m.SetFloat(MToonProps.EyeUpperShadowHeight, s.upperShadowHeight);
            m.SetFloat(MToonProps.EyeUpperShadowSoftness, s.upperShadowSoftness);
            m.SetFloat(MToonProps.EyeLowerGlow, s.lowerGlow);
            m.SetColor(MToonProps.EyeLowerGlowColor, ToColor(s.lowerGlowColor));
            m.SetFloat(MToonProps.EyeLowerGlowHeight, s.lowerGlowHeight);
            m.SetFloat(MToonProps.EyeLowerGlowSoftness, s.lowerGlowSoftness);

            var count = Math.Min(s.highlights.Count, EyeSettings.MaxHighlightCount);
            for (var i = 0; i < EyeSettings.MaxHighlightCount; i++)
            {
                if (i < count)
                {
                    var h = s.highlights[i];
                    HighlightShape[i] = new Vector4(h.x, h.y, h.width, h.height);
                    HighlightParams[i] = new Vector4(h.strength, h.roll * Mathf.Deg2Rad, h.glowWidth, h.glowHeight);
                    HighlightColor[i] = new Vector4(h.color.r, h.color.g, h.color.b, 0f);
                    HighlightGlow[i] = new Vector4(h.glowColor.r, h.glowColor.g, h.glowColor.b, h.glowColor.a * h.glowStrength);
                }
                else
                {
                    HighlightShape[i] = Vector4.zero;
                    HighlightParams[i] = Vector4.zero;
                    HighlightColor[i] = Vector4.zero;
                    HighlightGlow[i] = Vector4.zero;
                }
            }
            m.SetFloat(MToonProps.EyeHighlightCount, count);
            m.SetVectorArray(MToonProps.EyeHighlightShape, HighlightShape);
            m.SetVectorArray(MToonProps.EyeHighlightParams, HighlightParams);
            m.SetVectorArray(MToonProps.EyeHighlightColor, HighlightColor);
            m.SetVectorArray(MToonProps.EyeHighlightGlow, HighlightGlow);
            m.SetFloat(MToonProps.EyeParallax, s.parallax);
            m.SetFloat(MToonProps.EyeLidFollow, s.lidFollow);
            m.SetFloat(MToonProps.EyeLidCenter, s.lidCenter);
            m.SetFloat(MToonProps.EyeHighlightLightInfluence, s.highlightLightInfluence);
            m.SetFloat(MToonProps.EyeIrisLightInfluence, s.irisLightInfluence);
        }

        public static void ApplyEyeHi(Material m, EyeSettings s)
        {
            m.SetFloat(MToonProps.EyeHighlightLightInfluence, s.highlightLightInfluence);
        }

        public static void SetIrisRect(Material m, IrisRect r)
        {
            m.SetVector(MToonProps.EyeIrisRect, new Vector4(r.x, r.y, r.width, r.height));
        }

        /// <summary>closure はまぶたを閉じた割合 (0〜1、EyelidClosure.Sum)</summary>
        public static void SetLidClosure(Material m, float closure)
        {
            m.SetFloat(MToonProps.EyeLidClose, closure);
        }

        public static void SetGaze(Material m, Vector3 gaze)
        {
            m.SetVector(MToonProps.EyeGaze, gaze);
        }

        /// <summary>right / up は顔を正面から見たときの右・上 (ワールド)。0 ならテクスチャの向きのまま置く</summary>
        public static void SetHeadAxes(Material m, Vector3 right, Vector3 up)
        {
            m.SetVector(MToonProps.EyeHeadRight, right);
            m.SetVector(MToonProps.EyeHeadUp, up);
        }

        /// <summary>GPU から同期で読み戻す。テクスチャか瞳の menu が変わったときだけ呼ぶ。見つからなければ false で rect は UV 全体</summary>
        public bool TryReadIrisRect(Texture texture, out IrisRect rect)
        {
            if (texture == null)
            {
                rect = IrisRect.Full;
                return false;
            }
            const int size = IrisBounds.SampleSize;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                if (_readback == null)
                {
                    _readback = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                    _alpha = new byte[size * size];
                }
                _readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                var pixels = _readback.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                {
                    _alpha[i] = pixels[i].a;
                }
                return IrisBounds.TryCompute(_alpha, size, size, IrisBounds.Threshold, out rect);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        public void Dispose()
        {
            if (_readback != null)
            {
                UnityEngine.Object.Destroy(_readback);
                _readback = null;
            }
        }

        private static Color ToColor(ColorData c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
    }
}
