using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// BepInEx の ManualLogSource と同じ名前のメソッドで UnityEngine.Debug へ出す。
    /// BepInEx の LogOutput.log には Unity Log として残る
    /// </summary>
    internal sealed class PluginLogger
    {
        private readonly string _prefix;

        public PluginLogger(string name)
        {
            _prefix = "[" + name + "] ";
        }

        public void LogInfo(string message)
        {
            Debug.Log(_prefix + message);
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning(_prefix + message);
        }

        public void LogError(string message)
        {
            Debug.LogError(_prefix + message);
        }
    }
}
