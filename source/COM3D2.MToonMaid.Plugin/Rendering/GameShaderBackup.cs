using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// ゲームの TBodySkin.m_dicBackupShader (menu の shader コマンドが戻し先として控えるシェーダー) の補正。
    /// 変換中に shader コマンドが来ると戻し先が MToon になり、OFF の後の RestoreShader で管理外の MToon に戻されるため、
    /// OFF のときに元のシェーダーへ差し替える
    /// </summary>
    internal static class GameShaderBackup
    {
        private static readonly FieldInfo BackupField =
            typeof(TBodySkin).GetField("m_dicBackupShader", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool IsAvailable => BackupField != null;

        public static void Replace(TBodySkin skin, int materialIndex, Shader from, Shader to)
        {
            if (BackupField == null || skin == null)
            {
                return;
            }
            var backups = BackupField.GetValue(skin) as Dictionary<int, Shader>;
            Shader current;
            if (backups != null && backups.TryGetValue(materialIndex, out current) && current == from)
            {
                backups[materialIndex] = to;
            }
        }
    }
}
