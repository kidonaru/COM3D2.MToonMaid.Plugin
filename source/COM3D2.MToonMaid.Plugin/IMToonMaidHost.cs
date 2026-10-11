using System.Collections.Generic;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>ウィンドウからプラグイン本体への操作</summary>
    public interface IMToonMaidHost
    {
        bool IsShaderAvailable { get; }

        /// <summary>前髪の影のシェーダーを読めたか。false ならウィンドウの前髪の影の行を無効表示にする</summary>
        bool IsHairShadowAvailable { get; }

        /// <summary>瞳の光沢のシェーダーを読めたか。false ならウィンドウの瞳タブを無効表示にする</summary>
        bool IsEyeGlossAvailable { get; }

        PresetStore<EyePreset> EyePresets { get; }
        PresetStore<MaidPreset> Presets { get; }
        List<Maid> GetActiveMaids();
        MaidSettings GetSettings(Maid maid);
        void SetEnabled(Maid maid, bool enabled);
        void NotifyGroupChanged(Maid maid, PartGroup group);
        int GetConvertedCount(Maid maid);

        /// <summary>直前の走査で白目のマテリアルと範囲が見つかったか。コントローラーが無ければ false</summary>
        bool IsScleraFound(Maid maid);

        /// <summary>
        /// 指定グループのスロット (名前順)。今着けているものに加え、除外中で今は着けていないものも含める
        /// (外した後も除外を解除できるように)。戻り値は次の呼び出しで上書きされる共有バッファなので保持しない
        /// </summary>
        List<string> GetSlotCategories(Maid maid, PartGroup group);

        /// <summary>スロットの除外を変えた。保存し、間隔を待たずに走査させる</summary>
        void NotifyTargetsChanged(Maid maid);

        /// <summary>瞳の設定を変えた。保存し、値を入れて間隔を待たずに走査させる</summary>
        void NotifyEyeChanged(Maid maid);
    }
}
