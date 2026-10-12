using System;
using System.IO;
using System.Xml.Serialization;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// Config\MToonMaid.xml の読み書き。コンストラクタで読み、ファイルが無いか壊れていれば
    /// 既定値で書き出す (利用者がキーなどを書き換えられるよう、ファイルは常に置いておく)
    /// </summary>
    public sealed class PluginConfigStore
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(PluginConfig));

        private readonly string _path;
        private readonly Action<string> _warn;
        // 壊れたファイルを退避できなかった。保存すると利用者の書いた中身が消えるため、このセッションでは書かない
        private bool _keepBrokenFile;

        public PluginConfig Config { get; }

        public PluginConfigStore(string path, Action<string> warn)
        {
            _path = path;
            _warn = warn;
            var loaded = Load();
            Config = loaded ?? new PluginConfig();
            if (loaded == null)
            {
                Save();
            }
        }

        /// <summary>失敗しても例外を出さず警告する (保存できないだけでプラグインを止めないため)</summary>
        public void Save()
        {
            if (_keepBrokenFile)
            {
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                // 直接書くと、書き込み中に落ちたとき既存のファイルが壊れる
                var temp = _path + ".tmp";
                using (var stream = File.Create(temp))
                {
                    Serializer.Serialize(stream, Config);
                }
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
                File.Move(temp, _path);
            }
            catch (Exception e)
            {
                _warn("設定ファイルの保存に失敗しました: " + _path + " " + e.Message);
                try
                {
                    File.Delete(_path + ".tmp");
                }
                catch (Exception)
                {
                    // 後始末の失敗は次の保存で上書きされるだけなので、警告を重ねない
                }
            }
        }

        private PluginConfig Load()
        {
            if (!File.Exists(_path))
            {
                return null;
            }
            try
            {
                using (var stream = File.OpenRead(_path))
                {
                    return (PluginConfig)Serializer.Deserialize(stream);
                }
            }
            catch (Exception e)
            {
                _keepBrokenFile = !BrokenFileBackup.MoveAside(_path, e, _warn);
                return null;
            }
        }
    }
}
