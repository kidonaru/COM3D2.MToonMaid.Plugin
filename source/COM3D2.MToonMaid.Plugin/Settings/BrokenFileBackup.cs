using System;
using System.IO;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>読めない設定ファイルを path + ".bak" へ移す。次の保存で上書きして中身を失わないため</summary>
    internal static class BrokenFileBackup
    {
        public static void MoveAside(string path, Exception error, Action<string> warn)
        {
            var backup = path + ".bak";
            try
            {
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
                File.Move(path, backup);
                warn("設定ファイルを読めないため " + backup + " へ退避しました: " + error.Message);
            }
            catch (Exception moveError)
            {
                // 退避できなくても読み込みは失敗させない (毎フレームの例外で全メイドの処理が止まるため)
                warn("設定ファイルを読めず、" + backup + " への退避にも失敗しました: " + error.Message + " / " + moveError.Message);
            }
        }
    }
}
