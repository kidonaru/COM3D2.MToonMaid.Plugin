using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>COM3D2 (2.0) と COM3D2.5 で形の違うゲーム・Unity の API をそろえる</summary>
    internal static class GameCompat
    {
#if COM3D2
        public const string GameName = "COM3D2 (2.0)";
        private const string UnityVersionPrefix = "5.";
#else
        public const string GameName = "COM3D2.5";
        private const string UnityVersionPrefix = "2022.";
#endif

        /// <summary>この DLL が対象とするゲームの Unity か。unityVersion は Application.unityVersion</summary>
        public static bool IsExpectedUnityVersion(string unityVersion)
        {
            return unityVersion != null && unityVersion.StartsWith(UnityVersionPrefix, StringComparison.Ordinal);
        }

        /// <summary>全スロット (2.5 は CRC のサブスロットを含む) の、obj が読み込まれた TBodySkin を buffer へ入れる。buffer は先に空にする</summary>
        public static void CollectSkins(TBody body, List<TBodySkin> buffer)
        {
            buffer.Clear();
            var slots = body.goSlot;
#if COM3D2
            foreach (var skin in slots)
            {
                AddIfLoaded(skin, buffer);
            }
#else
            for (var i = 0; i < slots.Count; i++)
            {
                for (var j = 0; j < slots.CountChildren(i); j++)
                {
                    AddIfLoaded(slots[i, j], buffer);
                }
            }
#endif
        }

        /// <summary>slotNo のスロット (2.5 はそのサブスロット全部) の、obj が読み込まれた TBodySkin を buffer へ入れる。buffer は先に空にする</summary>
        public static void CollectSkins(TBody body, int slotNo, List<TBodySkin> buffer)
        {
            buffer.Clear();
#if COM3D2
            AddIfLoaded(body.goSlot[slotNo], buffer);
#else
            var slots = body.goSlot;
            for (var j = 0; j < slots.CountChildren(slotNo); j++)
            {
                AddIfLoaded(slots[slotNo, j], buffer);
            }
#endif
        }

        private static void AddIfLoaded(TBodySkin skin, List<TBodySkin> buffer)
        {
            if (skin != null && skin.obj != null)
            {
                buffer.Add(skin);
            }
        }

        public static void GetSharedMaterials(Renderer renderer, List<Material> buffer)
        {
#if COM3D2
            // Unity 5.6 には List を受ける GetSharedMaterials が無い。materials で取ると複製を作るため、配列を毎回作っても sharedMaterials を読む
            buffer.Clear();
            buffer.AddRange(renderer.sharedMaterials);
#else
            renderer.GetSharedMaterials(buffer);
#endif
        }

        public static bool IsScriptableRenderPipelineActive()
        {
#if COM3D2
            // GraphicsSettings.currentRenderPipeline は Unity 5.6 に無い (2.0 に URP の切り替えも無い)
            return false;
#else
            return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
#endif
        }
    }
}
