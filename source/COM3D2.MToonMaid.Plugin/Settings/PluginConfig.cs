namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>メイドによらない全体設定 (Config\MToonMaid.xml)。windowWidth / windowHeight の 0 は未設定</summary>
    public class PluginConfig
    {
        public int scanInterval = 10;
        public string toggleWindow = "Ctrl+Shift+M";
        public int windowX = -1;
        public int windowY = -1;
        // 既定サイズを入れると UI の定数に依存するため、0 を未設定として持つ
        public int windowWidth;
        public int windowHeight;
    }
}
