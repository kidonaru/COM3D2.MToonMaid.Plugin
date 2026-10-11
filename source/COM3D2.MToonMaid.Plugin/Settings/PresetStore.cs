using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>PresetStore で読み書きするプリセット 1 個分</summary>
    public interface IPresetData
    {
        /// <summary>保存の直前に呼ぶ。version を今の版にする</summary>
        void StampCurrentVersion();

        /// <summary>読み込みの直後に呼ぶ。欠けた値を既定で埋める</summary>
        void FillMissing();
    }

    public static class PresetStore
    {
        // Path.GetInvalidFileNameChars は呼ぶたびに配列を作り、名前欄は描画ごとに正規化する
        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

        /// <summary>ファイル名に使えない文字を _ に置き換え、前後の空白を除いた名前。空なら保存できない</summary>
        public static string NormalizeName(string name)
        {
            if (name == null)
            {
                return "";
            }
            foreach (var c in InvalidNameChars)
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }

    /// <summary>
    /// プリセットを名前ごとに 1 ファイルで読み書きする。名前の一覧は Refresh で読み直すまで使い回す
    /// (描画ごとにフォルダを読まないため)
    /// </summary>
    public sealed class PresetStore<T> where T : class, IPresetData
    {
        private const string Extension = ".xml";

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(T));

        private readonly string _directory;
        private readonly string _label;
        private readonly Action<string> _warn;
        private readonly List<string> _names = new List<string>();
        private readonly ReadOnlyCollection<string> _readOnlyNames;

        /// <summary>label は警告文の先頭に出すプリセットの呼び名</summary>
        public PresetStore(string directory, string label, Action<string> warn)
        {
            _directory = directory;
            _label = label;
            _warn = warn;
            _readOnlyNames = _names.AsReadOnly();
        }

        /// <summary>保存先のフォルダを無ければ作って、そのパスを返す。作れなければ例外を投げる</summary>
        public string EnsureFolder()
        {
            Directory.CreateDirectory(_directory);
            return _directory;
        }

        // IReadOnlyList にすると、.NET 3.5 (2.0 版) にその型が無くビルドできない
        /// <summary>名前順。Refresh・Save・Delete で更新する</summary>
        public ReadOnlyCollection<string> Names => _readOnlyNames;

        public void Refresh()
        {
            _names.Clear();
            if (!Directory.Exists(_directory))
            {
                return;
            }
            try
            {
                foreach (var path in Directory.GetFiles(_directory, "*" + Extension))
                {
                    _names.Add(Path.GetFileNameWithoutExtension(path));
                }
            }
            catch (Exception e)
            {
                _warn(_label + "の一覧を読めませんでした: " + e.Message);
            }
            _names.Sort(StringComparer.OrdinalIgnoreCase);
        }

        public bool Exists(string name)
        {
            var normalized = PresetStore.NormalizeName(name);
            return _names.Exists(n => string.Equals(n, normalized, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>同じ名前があれば上書きする。失敗したら警告して false</summary>
        public bool Save(string name, T preset)
        {
            var normalized = PresetStore.NormalizeName(name);
            if (normalized.Length == 0)
            {
                return false;
            }
            var path = GetPath(normalized);
            var temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_directory);
                preset.StampCurrentVersion();
                // 書き込み中に落ちても既存のプリセットを壊さないよう一時ファイルへ書いてから置き換える
                using (var stream = File.Create(temp))
                {
                    Serializer.Serialize(stream, preset);
                }
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                _warn(_label + "を保存できませんでした: " + normalized + " " + e.Message);
                return false;
            }
            Refresh();
            return true;
        }

        /// <summary>毎回ファイルから読み直す (返した実体を適用先がそのまま持てるように)。読めなければ警告して null</summary>
        public T Load(string name)
        {
            var path = GetPath(PresetStore.NormalizeName(name));
            try
            {
                T preset;
                using (var stream = File.OpenRead(path))
                {
                    preset = (T)Serializer.Deserialize(stream);
                }
                preset.FillMissing();
                return preset;
            }
            catch (Exception e)
            {
                _warn(_label + "を読めませんでした: " + path + " " + e.Message);
                return null;
            }
        }

        public bool Delete(string name)
        {
            var path = GetPath(PresetStore.NormalizeName(name));
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                _warn(_label + "を削除できませんでした: " + path + " " + e.Message);
                return false;
            }
            Refresh();
            return true;
        }

        private string GetPath(string normalizedName)
        {
            return Path.Combine(_directory, normalizedName + Extension);
        }
    }
}
