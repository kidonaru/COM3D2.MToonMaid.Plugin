using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    public sealed class GUIOption : GUIOptionBase
    {
        // MTEUtils 既定の緑だと、同じ画面に並ぶ SceneEditor のシアンと選択色が食い違う
        public override Color accentColor => Color.cyan;
    }
}
