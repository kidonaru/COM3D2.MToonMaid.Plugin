using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>COM3D2/MToon10・前髪の影・瞳の光沢のシェーダーを mtoon_bundle にまとめる。build-bundle.bat から batchmode で呼ぶ</summary>
public static class BuildMToonBundle
{
    private const string OutputDir = "Bundles";

    public static void Run()
    {
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "mtoon_bundle",
                assetNames = new[]
                {
                    "Assets/Shaders/MToon10/vrmc_materials_mtoon.shader",
                    "Assets/Shaders/MToonMaid/HairShadowCaster.shader",
                    "Assets/Shaders/MToonMaid/MToonMaidEye.shader",
                    "Assets/Shaders/MToonMaid/MToonMaidHidden.shader",
                    "Assets/Shaders/MToonMaid/MToonMaidEyeHi.shader",
                },
            },
        };
#if !UNITY_2017_1_OR_NEWER
        // COM3D2 (2.0) のゲームは d3d11 で動く。既定の d3d9 も作ると、MToon10 の SampleLevel (sampler_*) が d3d9 に無くエラーになる
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
#endif
        Directory.CreateDirectory(OutputDir);
        BuildPipeline.BuildAssetBundles(OutputDir, builds, BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        Debug.Log("mtoon_bundle を作成しました");
    }
}
