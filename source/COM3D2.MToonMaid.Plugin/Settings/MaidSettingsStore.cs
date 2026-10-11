using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// GUID ごとの設定ファイルの読み書き。読み込みはメイドごとに初回だけ、
    /// 書き込みはスライダー操作中に毎フレーム書かないよう変更から SaveDelay 秒後にまとめる
    /// </summary>
    public class MaidSettingsStore
    {
        public const float SaveDelay = 1f;

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(MaidSettings));

        private readonly string _directory;
        private readonly Action<string> _warn;
        private readonly Dictionary<string, MaidSettings> _cache = new Dictionary<string, MaidSettings>();
        private readonly HashSet<string> _dirty = new HashSet<string>();
        private float _lastChange;

        public MaidSettingsStore(string directory, Action<string> warn)
        {
            _directory = directory;
            _warn = warn;
        }

        public MaidSettings Get(string guid)
        {
            MaidSettings settings;
            if (!_cache.TryGetValue(guid, out settings))
            {
                settings = Load(GetPath(guid), _warn);
                _cache[guid] = settings;
            }
            return settings;
        }

        public void MarkDirty(string guid, float now)
        {
            _dirty.Add(guid);
            _lastChange = now;
        }

        public void FlushIfDue(float now)
        {
            if (_dirty.Count > 0 && now - _lastChange >= SaveDelay)
            {
                FlushAll();
            }
        }

        public void FlushAll()
        {
            if (_dirty.Count == 0)
            {
                return;
            }
            Directory.CreateDirectory(_directory);
            var failed = new List<string>();
            foreach (var guid in _dirty)
            {
                MaidSettings settings;
                if (!_cache.TryGetValue(guid, out settings))
                {
                    continue;
                }
                try
                {
                    Save(GetPath(guid), settings);
                }
                catch (Exception e)
                {
                    // 次の FlushIfDue で再試行する
                    failed.Add(guid);
                    _warn("設定の保存に失敗しました: " + guid + " " + e.Message);
                }
            }
            _dirty.Clear();
            foreach (var guid in failed)
            {
                _dirty.Add(guid);
            }
        }

        private string GetPath(string guid)
        {
            var name = guid;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return Path.Combine(_directory, name + ".xml");
        }

        internal static MaidSettings Load(string path, Action<string> warn)
        {
            MaidSettings settings = null;
            if (File.Exists(path))
            {
                try
                {
                    using (var stream = File.OpenRead(path))
                    {
                        settings = (MaidSettings)Serializer.Deserialize(stream);
                    }
                }
                catch (Exception e)
                {
                    // 壊れたファイルを次の保存で上書きしないよう退避し、このメイドは OFF で始める
                    var backup = path + ".bak";
                    try
                    {
                        if (File.Exists(backup))
                        {
                            File.Delete(backup);
                        }
                        File.Move(path, backup);
                        warn("設定ファイルを読めないため " + backup + " へ退避しました: " + e.Message);
                    }
                    catch (Exception moveError)
                    {
                        // 退避できなくても読み込みは失敗させない (毎フレームの例外で全メイドの処理が止まるため)
                        warn("設定ファイルを読めず、" + backup + " への退避にも失敗しました: " + e.Message + " / " + moveError.Message);
                    }
                    settings = null;
                }
            }

            if (settings == null)
            {
                settings = new MaidSettings();
            }
            settings.FillMissing();
            return settings;
        }

        internal static void Save(string path, MaidSettings settings)
        {
            settings.version = MaidSettings.CurrentVersion;
            // 書き込み中に落ちても既存のファイルを壊さないよう一時ファイルへ書いてから置き換える
            var temp = path + ".tmp";
            using (var stream = File.Create(temp))
            {
                Serializer.Serialize(stream, settings);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);
        }
    }
}
