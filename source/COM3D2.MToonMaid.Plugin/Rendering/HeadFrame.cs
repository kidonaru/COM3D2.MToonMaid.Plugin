using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// 頭の向きと中心 (ワールド)。COM3D2 の Bip01 Head は顔の正面が up、頭のてっぺんが -right、メイドから見て右が -forward
    /// </summary>
    public static class HeadFrame
    {
        public static Vector3 FaceForward(Transform head)
        {
            return head.up;
        }

        public static Vector3 HeadTop(Transform head)
        {
            return -head.right;
        }

        public static Vector3 MaidRight(Transform head)
        {
            return -head.forward;
        }

        /// <summary>無ければ null</summary>
        public static SkinnedMeshRenderer FindHeadRenderer(TBody body)
        {
            var skin = body != null ? body.GetSlot(ToonMaterialClassifier.HeadSlot) : null;
            return skin != null && skin.obj != null ? skin.obj.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        }

        /// <summary>SkinnedMeshRenderer の localBounds はルートボーンの空間にある</summary>
        public static Transform BoundsSpace(SkinnedMeshRenderer r)
        {
            return r.rootBone != null ? r.rootBone : r.transform;
        }

        public static Vector3 Center(SkinnedMeshRenderer r)
        {
            // ワールドの AABB (Renderer.bounds) は頭の回転で大きさが変わり、中心も揺れる
            return BoundsSpace(r).TransformPoint(r.localBounds.center);
        }
    }
}
