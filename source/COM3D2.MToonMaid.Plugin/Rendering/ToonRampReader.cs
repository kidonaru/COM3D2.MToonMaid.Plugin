using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// _ToonRamp の中央の行を GPU から読み出す。ランプは読み取り不可のことが多いため Blit してから読む。
    /// ゲームは RenderTexture をプールで使い回すため、テクスチャ単位ではキャッシュしない (呼び出し側がマテリアルごとに持つ)
    /// </summary>
    public sealed class ToonRampReader
    {
        private readonly Action<string> _warn;
        private bool _warned;

        public ToonRampReader(Action<string> warn)
        {
            _warn = warn;
        }

        public ToonRampColors Read(Texture ramp)
        {
            if (ramp == null || ramp.width < 1 || ramp.height < 1)
            {
                return ToonRampColors.White;
            }
            var width = ramp.width;
            var row = ramp.height / 2;
            // ゲームは Gamma 色空間。シェーダーが見る値そのものを読むため Linear 指定で変換させない
            var rt = RenderTexture.GetTemporary(width, ramp.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            Texture2D buffer = null;
            try
            {
                Graphics.Blit(ramp, rt);
                RenderTexture.active = rt;
                buffer = new Texture2D(width, 1, TextureFormat.RGBA32, false, true);
                buffer.ReadPixels(new Rect(0, row, width, 1), 0, 0);
                buffer.Apply(false);
                var pixels = buffer.GetPixels();
                var rgba = new float[pixels.Length * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    rgba[i * 4] = pixels[i].r;
                    rgba[i * 4 + 1] = pixels[i].g;
                    rgba[i * 4 + 2] = pixels[i].b;
                    rgba[i * 4 + 3] = pixels[i].a;
                }
                return ToonRampColors.FromRow(rgba, width);
            }
            catch (Exception e)
            {
                // 読めないランプは補正なし (白) で描く。毎回の警告は避ける
                if (!_warned)
                {
                    _warned = true;
                    _warn("ランプを読み出せないため補正せずに描きます: " + ramp.name + " " + e.Message);
                }
                return ToonRampColors.White;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (buffer != null)
                {
                    Object.Destroy(buffer);
                }
            }
        }
    }
}
