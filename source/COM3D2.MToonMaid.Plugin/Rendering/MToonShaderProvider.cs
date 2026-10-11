using System.IO;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>バンドルから読んだシェーダー。前髪のシェーダーは読めなければ null (前髪の影だけを無効にする)</summary>
    public sealed class MToonShaders
    {
        public Shader mtoon;
        public Shader hairShadowCaster;
        // 瞳の光沢と eye_hi を消す・残すシェーダー。どれかが読めなければすべて null (瞳の機能だけを無効にする)
        public Shader eye;
        public Shader hidden;
        public Shader eyeHi;
    }

    /// <summary>DLL に埋め込んだ mtoon_bundle から COM3D2/MToon10・前髪の影・瞳の光沢のシェーダーを読む</summary>
    public static class MToonShaderProvider
    {
        private const string ResourceName = "mtoon_bundle";

        /// <summary>MToon を読めない・今のグラフィックス API で描けないときは null。シェーダーは読み込んだまま保持する</summary>
        public static MToonShaders Load(ManualLogSource log)
        {
            byte[] bytes;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    log.LogError("埋め込みリソース " + ResourceName + " が見つかりません。build-bundle.bat の実行漏れです");
                    return null;
                }
                bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
            }

            var bundle = AssetBundle.LoadFromMemory(bytes);
            if (bundle == null)
            {
                log.LogError(ResourceName + " を AssetBundle として読めませんでした");
                return null;
            }

            Shader found = null;
            Shader caster = null;
            Shader eye = null;
            Shader hidden = null;
            Shader eyeHi = null;
            foreach (var shader in bundle.LoadAllAssets<Shader>())
            {
                if (shader.name == MToonProps.ShaderName)
                {
                    found = shader;
                }
                else if (shader.name == MToonProps.HairShadowCasterShaderName)
                {
                    caster = shader;
                }
                else if (shader.name == MToonProps.EyeShaderName)
                {
                    eye = shader;
                }
                else if (shader.name == MToonProps.HiddenShaderName)
                {
                    hidden = shader;
                }
                else if (shader.name == MToonProps.EyeHiShaderName)
                {
                    eyeHi = shader;
                }
            }
            // 読み込んだシェーダーは残し、バンドルのファイル領域だけ解放する
            bundle.Unload(false);

            if (found == null)
            {
                log.LogError(ResourceName + " に " + MToonProps.ShaderName + " が含まれていません");
                return null;
            }
            if (!found.isSupported)
            {
                log.LogError(MToonProps.ShaderName + " は現在のグラフィックス API (" + SystemInfo.graphicsDeviceType + ") で使えません");
                return null;
            }

            if (caster == null)
            {
                log.LogError(ResourceName + " に " + MToonProps.HairShadowCasterShaderName + " が含まれていないため、前髪の影は使えません");
            }
            else if (!caster.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
            {
                log.LogError("前髪の影のシェーダーか RFloat の RenderTexture が現在のグラフィックス API (" + SystemInfo.graphicsDeviceType + ") で使えないため、前髪の影は使えません");
                caster = null;
            }
            if (eye == null || hidden == null || eyeHi == null)
            {
                log.LogError(ResourceName + " に瞳の光沢のシェーダーが含まれていないため、瞳の光沢は使えません");
                eye = null;
                hidden = null;
                eyeHi = null;
            }
            else if (!eye.isSupported || !hidden.isSupported || !eyeHi.isSupported)
            {
                log.LogError("瞳の光沢のシェーダーが現在のグラフィックス API (" + SystemInfo.graphicsDeviceType + ") で使えないため、瞳の光沢は使えません");
                eye = null;
                hidden = null;
                eyeHi = null;
            }
            return new MToonShaders { mtoon = found, hairShadowCaster = caster, eye = eye, hidden = hidden, eyeHi = eyeHi };
        }
    }
}
