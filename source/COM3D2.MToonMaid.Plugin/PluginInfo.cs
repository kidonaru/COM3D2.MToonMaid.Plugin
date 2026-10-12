namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// プラグインのメタ情報。PluginVersion はリリース時のバージョンの単一ソースで、
    /// bump-version.bat / release.bat がここを参照する
    /// </summary>
    internal static class PluginInfo
    {
        public const string PluginGuid = "com.kidonaru.com3d2.mtoonmaid";
        public const string PluginName = "MToonMaid";
        public const string PluginFullName = "COM3D2." + PluginName + ".Plugin";
        public const string PluginVersion = "1.0.0.0";
    }
}
