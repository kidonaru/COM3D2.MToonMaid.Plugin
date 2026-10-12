# MToonMaid の UnityInjector 化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task (このワークスペースでは subagent-driven-development は使わない). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MToonMaid を BepInEx プラグイン（`BaseUnityPlugin`）から UnityInjector プラグイン（`PluginBase`）へ置き換え、配置・設定・配布物を PostEffects と同じ `Sybaris\UnityInjector\` 形式にそろえる。

**Architecture:** BepInEx の API（`ConfigFile` / `ConfigEntry` / `KeyboardShortcut` / `ManualLogSource` / `Paths`）を、XML の全体設定ファイル（`PluginConfigStore`）・MTEUtils の `KeyBind`・`UnityEngine.Debug` へ流すロガー（`PluginLogger`）に置き換える。参照アセンブリは PostEffects と同じく、2.5 は `BepInEx.UnityInjectorLoader.dll`、2.0 は `Sybaris\lib\UnityInjector.dll`。ビルド出力・配布 zip のフォルダは `UnityInjector`（2.0）/ `UnityInjector (COM3D2.5)`（2.5）にする。

**Tech Stack:** C# 7.3 / net471・net35 / UnityInjector（BepInEx.UnityInjectorLoader 経由）/ xunit（net48）/ MTEUtils（submodule、編集しない）

**Spec:** ユーザー指示（本会話）。決定事項:
- 設定フォルダは `Sybaris\UnityInjector\Config\MToonMaid\`、全体設定は `Sybaris\UnityInjector\Config\MToonMaid.xml`
- DLL 名は `COM3D2.MToonMaid.Plugin.dll`
- v1.0.0.0（BepInEx 版）の設定は引き継がない。README にも引き継ぎ方を書かない

## Global Constraints

- DLL 名（AssemblyName）: `COM3D2.MToonMaid.Plugin`。2.5 版・2.0 版で同名
- 全体設定: `<DLL のフォルダ>\Config\MToonMaid.xml`。メイド設定: `<DLL のフォルダ>\Config\MToonMaid\<GUID>.xml`。プリセット: `...\Config\MToonMaid\Presets\`、瞳のプリセット: `...\Config\MToonMaid\EyePresets\`
- `<DLL のフォルダ>` は `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)`（PostEffects の `PluginUtils.UserDataPath` と同じ求め方）
- ホットキーの既定は `Ctrl+Shift+M`、走査間隔の既定は 10、ウィンドウ位置の既定は -1（画面中央）
- プラグイン属性は PostEffects と同じ `PluginFilter("COM3D2x64")` / `PluginName(PluginInfo.PluginFullName)` / `PluginVersion(PluginInfo.PluginVersion)`
- コードコメント・ログは日本語。コメントは Why not だけ（ユーザーの CLAUDE.md）
- `deploy.bat` / `deploy.ps1` は実行しない（文面の編集だけ行う）。git worktree は使わない
- 旧 BepInEx 版の設定移行・旧 DLL の検出はしない（スコープ外）

## Review Focus

1. 全体設定ファイルが壊れている（手で編集して XML が崩れた） → 例外で止まらず既定値で起動し、壊れたファイルは `.bak` へ退避して上書きしない → Task 1 のテスト
2. 全体設定ファイルが無い（初回起動） → 既定値のファイルを作り、利用者がキーを書き換えられる → Task 1 のテスト
3. 古い/手書きの XML で要素が欠けている（例: `windowWidth` が無い） → 欠けた項目だけ既定値になる → Task 1 のテスト（欠けた幅は 0 で読まれ、Task 2 で既定サイズへ置き換える）
4. ホットキー文字列が不正（`Ctrl+Shft+M`）や修飾キーだけ（`Ctrl+Shift`） → 開閉キーが効かなくなるだけで起動は続き、警告ログが出る（修飾キーを押している間に毎フレーム開閉しない） → Task 5 Step 3 の 7 で実機確認
5. シーン遷移後もプラグインが生きていて、ウィンドウ・ギアアイコン・変換が続く → Task 5 の実機確認

---

## File Structure

| ファイル | 変更 | 責務 |
|---|---|---|
| `source/COM3D2.MToonMaid.Plugin/Settings/PluginConfig.cs` | 新規 | 全体設定の値（XML 直列化対象） |
| `source/COM3D2.MToonMaid.Plugin/Settings/PluginConfigStore.cs` | 新規 | `MToonMaid.xml` の読み書き（無ければ作る・壊れていれば退避） |
| `source/COM3D2.MToonMaid.Plugin/Settings/BrokenFileBackup.cs` | 新規 | 読めない設定ファイルを `.bak` へ退避する（`MaidSettingsStore` から抽出して共用） |
| `source/COM3D2.MToonMaid.Plugin/Settings/MaidSettingsStore.cs` | 変更 | 退避処理を `BrokenFileBackup` へ置き換え |
| `source/COM3D2.MToonMaid.Plugin/UI/WindowPlacementConfig.cs` | 書き換え | ウィンドウ位置を `PluginConfigStore` へ保存 |
| `source/COM3D2.MToonMaid.Plugin/PluginLogger.cs` | 新規 | `ManualLogSource` 互換の `LogInfo/LogWarning/LogError` を `UnityEngine.Debug` へ流す |
| `source/COM3D2.MToonMaid.Plugin/MToonMaidPlugin.cs` | 変更 | `PluginBase` 化、設定・ホットキー・ログの置き換え |
| `source/COM3D2.MToonMaid.Plugin/Rendering/MToonShaderProvider.cs` | 変更 | 引数を `PluginLogger` に |
| `source/COM3D2.MToonMaid.Plugin/PluginInfo.cs` | 変更 | `PluginGuid` を削除 |
| `source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj` | 変更 | AssemblyName・参照・`KeyBind.cs` のリンク |
| `source/COM3D2.MToonMaid.Plugin/build.ps1` | 変更 | 出力先フォルダとゲームへのコピー先 |
| `release.bat` / `.gitignore` / `deploy.ps1` / `README.md` | 変更 | 配布構成と説明 |
| `BepInEx/config/COM3D2.MToonMaid/**` → `UnityInjector/Config/MToonMaid/**` | 移動 | 同梱プリセット |
| `source/COM3D2.MToonMaid.Plugin.Tests/PluginConfigStoreTests.cs` | 新規 | Task 1 のテスト |
| `source/COM3D2.MToonMaid.Plugin.Tests/WindowPlacementConfigTests.cs` | 新規 | Task 2 のテスト |

---

### Task 1: 全体設定ファイル（PluginConfig / PluginConfigStore）

**Files:**
- Create: `source/COM3D2.MToonMaid.Plugin/Settings/PluginConfig.cs`
- Create: `source/COM3D2.MToonMaid.Plugin/Settings/PluginConfigStore.cs`
- Create: `source/COM3D2.MToonMaid.Plugin/Settings/BrokenFileBackup.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Settings/MaidSettingsStore.cs`（`Load` の catch 節）
- Test: `source/COM3D2.MToonMaid.Plugin.Tests/PluginConfigStoreTests.cs`

**Interfaces:**
- Produces:
  - `public class PluginConfig { public int scanInterval = 10; public string toggleWindow = "Ctrl+Shift+M"; public int windowX = -1; public int windowY = -1; public int windowWidth; public int windowHeight; }`（幅・高さの 0 は「未設定 = 既定サイズ」）
  - `public sealed class PluginConfigStore { public PluginConfigStore(string path, Action<string> warn); public PluginConfig Config { get; } public void Save(); }` — コンストラクタで読み込み、ファイルが無いか壊れていれば既定値で書き出す。`Save` は失敗しても例外を出さず `warn` する
  - `internal static class BrokenFileBackup { public static void MoveAside(string path, Exception error, Action<string> warn); }`

- [ ] **Step 1: 失敗するテストを書く**

`source/COM3D2.MToonMaid.Plugin.Tests/PluginConfigStoreTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class PluginConfigStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _path;
        private readonly List<string> _warnings = new List<string>();

        public PluginConfigStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidTests_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "MToonMaid.xml");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Fact]
        public void ファイルが無ければ既定値で作る()
        {
            var store = new PluginConfigStore(_path, _warnings.Add);

            Assert.Equal(10, store.Config.scanInterval);
            Assert.Equal("Ctrl+Shift+M", store.Config.toggleWindow);
            Assert.Equal(-1, store.Config.windowX);
            Assert.Equal(-1, store.Config.windowY);
            Assert.True(File.Exists(_path));
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 保存して読み直すと同じ値になる()
        {
            var store = new PluginConfigStore(_path, _warnings.Add);
            store.Config.scanInterval = 3;
            store.Config.toggleWindow = "Alt+F2";
            store.Config.windowX = 100;
            store.Config.windowY = 200;
            store.Config.windowWidth = 640;
            store.Config.windowHeight = 480;
            store.Save();

            var loaded = new PluginConfigStore(_path, _warnings.Add).Config;

            Assert.Equal(3, loaded.scanInterval);
            Assert.Equal("Alt+F2", loaded.toggleWindow);
            Assert.Equal(100, loaded.windowX);
            Assert.Equal(200, loaded.windowY);
            Assert.Equal(640, loaded.windowWidth);
            Assert.Equal(480, loaded.windowHeight);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 欠けた要素は既定値で読む()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "<?xml version=\"1.0\"?><PluginConfig><scanInterval>5</scanInterval></PluginConfig>");

            var config = new PluginConfigStore(_path, _warnings.Add).Config;

            Assert.Equal(5, config.scanInterval);
            Assert.Equal("Ctrl+Shift+M", config.toggleWindow);
            Assert.Equal(-1, config.windowX);
            Assert.Equal(0, config.windowWidth);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void 壊れたファイルは退避して既定値で作り直す()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "<PluginConfig><scanInterval>");

            var config = new PluginConfigStore(_path, _warnings.Add).Config;

            Assert.Equal(10, config.scanInterval);
            Assert.Equal("<PluginConfig><scanInterval>", File.ReadAllText(_path + ".bak"));
            Assert.True(File.Exists(_path));
            Assert.Single(_warnings);
        }

        [Fact]
        public void 保存できなくても例外を出さず警告する()
        {
            var store = new PluginConfigStore(_path, _warnings.Add);
            // 保存先と同名のフォルダを作り、ファイルとして書けなくする
            File.Delete(_path);
            Directory.CreateDirectory(_path);

            store.Save();

            Assert.Single(_warnings);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:\COM3D2_5 --filter FullyQualifiedName~PluginConfigStoreTests`
Expected: ビルドエラー（`PluginConfigStore` が無い）

- [ ] **Step 3: BrokenFileBackup を抽出する**

`source/COM3D2.MToonMaid.Plugin/Settings/BrokenFileBackup.cs`:

```csharp
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
```

`MaidSettingsStore.Load` の catch 節を次に置き換える（コメント「壊れたファイルを次の保存で上書きしないよう退避し、このメイドは OFF で始める」は残す）:

```csharp
                catch (Exception e)
                {
                    // 壊れたファイルを次の保存で上書きしないよう退避し、このメイドは OFF で始める
                    BrokenFileBackup.MoveAside(path, e, warn);
                    settings = null;
                }
```

- [ ] **Step 4: PluginConfig と PluginConfigStore を書く**

`source/COM3D2.MToonMaid.Plugin/Settings/PluginConfig.cs`:

```csharp
namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// メイドによらない全体設定 (Config\MToonMaid.xml)。
    /// windowWidth / windowHeight の 0 は未設定で、WindowPlacementConfig がウィンドウの既定サイズにする
    /// </summary>
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
```

`source/COM3D2.MToonMaid.Plugin/Settings/PluginConfigStore.cs`:

```csharp
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
                BrokenFileBackup.MoveAside(_path, e, _warn);
                return null;
            }
        }
    }
}
```

`Save` の一時ファイル → 置き換えは `MaidSettingsStore.Save` と同じ作法。

- [ ] **Step 5: テストが通ることを確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:\COM3D2_5`
Expected: 全テスト PASS（`MaidSettingsStoreTests` の退避のテストも含む）

- [ ] **Step 6: コミット**

```bash
git add source/COM3D2.MToonMaid.Plugin/Settings source/COM3D2.MToonMaid.Plugin.Tests/PluginConfigStoreTests.cs
git commit  # commit スキルで。例: feat(config): 全体設定を XML の MToonMaid.xml で持つ
```

---

### Task 2: ウィンドウ位置を PluginConfigStore へ保存する

**Files:**
- Modify (書き換え): `source/COM3D2.MToonMaid.Plugin/UI/WindowPlacementConfig.cs`
- Test: `source/COM3D2.MToonMaid.Plugin.Tests/WindowPlacementConfigTests.cs`

**Interfaces:**
- Consumes: `PluginConfigStore`（Task 1）
- Produces: `public WindowPlacementConfig(PluginConfigStore store, int defaultWidth, int defaultHeight)`。`Load(out x, out y, out width, out height)` / `Store(...)` / `FlushIfReleased()` の名前と引数は今のまま（`MToonMaidWindow` は変えない）

- [ ] **Step 1: 失敗するテストを書く**

`FlushIfReleased` は `Input` を使うためテストしない。`Load` の既定サイズの補完だけを確かめる。

`source/COM3D2.MToonMaid.Plugin.Tests/WindowPlacementConfigTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using COM3D2.MToonMaid.Plugin;
using Xunit;

namespace COM3D2.MToonMaid.Plugin.Tests
{
    public class WindowPlacementConfigTests : IDisposable
    {
        private readonly string _dir;
        private readonly List<string> _warnings = new List<string>();

        public WindowPlacementConfigTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MToonMaidTests_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Fact]
        public void 未設定のサイズは既定サイズで読む()
        {
            var store = new PluginConfigStore(Path.Combine(_dir, "MToonMaid.xml"), _warnings.Add);
            var placement = new WindowPlacementConfig(store, 400, 600);

            int x, y, width, height;
            placement.Load(out x, out y, out width, out height);

            Assert.Equal(-1, x);
            Assert.Equal(-1, y);
            Assert.Equal(400, width);
            Assert.Equal(600, height);
        }

        [Fact]
        public void 保存済みのサイズはそのまま読む()
        {
            var store = new PluginConfigStore(Path.Combine(_dir, "MToonMaid.xml"), _warnings.Add);
            store.Config.windowX = 10;
            store.Config.windowY = 20;
            store.Config.windowWidth = 300;
            store.Config.windowHeight = 500;
            var placement = new WindowPlacementConfig(store, 400, 600);

            int x, y, width, height;
            placement.Load(out x, out y, out width, out height);

            Assert.Equal(10, x);
            Assert.Equal(20, y);
            Assert.Equal(300, width);
            Assert.Equal(500, height);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確かめる**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:\COM3D2_5 --filter FullyQualifiedName~WindowPlacementConfigTests`
Expected: ビルドエラー（コンストラクタの引数が `ConfigFile`）

- [ ] **Step 3: WindowPlacementConfig を書き換える**

`source/COM3D2.MToonMaid.Plugin/UI/WindowPlacementConfig.cs` 全体:

```csharp
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// ウィンドウの位置とサイズを Config\MToonMaid.xml へ保存する。
    /// DockableWindowBase はドラッグ中に毎フレーム StorePlacement を呼ぶため、
    /// マウスを離してからまとめて書く
    /// </summary>
    public sealed class WindowPlacementConfig
    {
        private readonly PluginConfigStore _store;
        private readonly int _defaultWidth;
        private readonly int _defaultHeight;
        private int _pendingX;
        private int _pendingY;
        private int _pendingWidth;
        private int _pendingHeight;
        private bool _dirty;

        public WindowPlacementConfig(PluginConfigStore store, int defaultWidth, int defaultHeight)
        {
            _store = store;
            _defaultWidth = defaultWidth;
            _defaultHeight = defaultHeight;
        }

        /// <summary>負の位置は画面中央 (DockableWindowBase.Init の扱い)</summary>
        public void Load(out int x, out int y, out int width, out int height)
        {
            var config = _store.Config;
            x = config.windowX;
            y = config.windowY;
            width = config.windowWidth > 0 ? config.windowWidth : _defaultWidth;
            height = config.windowHeight > 0 ? config.windowHeight : _defaultHeight;
        }

        public void Store(int x, int y, int width, int height)
        {
            _pendingX = x;
            _pendingY = y;
            _pendingWidth = width;
            _pendingHeight = height;
            _dirty = true;
        }

        public void FlushIfReleased()
        {
            if (!_dirty || Input.GetMouseButton(0))
            {
                return;
            }
            _dirty = false;
            var config = _store.Config;
            config.windowX = _pendingX;
            config.windowY = _pendingY;
            config.windowWidth = _pendingWidth;
            config.windowHeight = _pendingHeight;
            _store.Save();
        }
    }
}
```

- [ ] **Step 4: テストを通す**

この時点では `MToonMaidPlugin.cs` がまだ `new WindowPlacementConfig(Config, ...)` を呼んでおりビルドが通らない。Task 3 と同時にビルドが通るので、**Step 4・5 は Task 3 の Step 8 でまとめて行う**（Task 2 単体ではコミットしない）。

---

### Task 3: PluginBase 化（プラグイン本体・ロガー・csproj）

**Files:**
- Create: `source/COM3D2.MToonMaid.Plugin/PluginLogger.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/MToonMaidPlugin.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/Rendering/MToonShaderProvider.cs:1-25`
- Modify: `source/COM3D2.MToonMaid.Plugin/PluginInfo.cs`
- Modify: `source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj`

**Interfaces:**
- Consumes: `PluginConfigStore` / `PluginConfig`（Task 1）、`WindowPlacementConfig(PluginConfigStore, int, int)`（Task 2）、MTEUtils の `COM3D2.MotionTimelineEditor.KeyBind`（`new KeyBind(string)`、`GetKeyDown()`、`ToString()`）
- Produces: `internal sealed class PluginLogger { public PluginLogger(string name); public void LogInfo(string); public void LogWarning(string); public void LogError(string); }`、`MToonMaidPlugin.Log` の型は `PluginLogger`（`MToonMaidWindow.cs` の `MToonMaidPlugin.Log.LogWarning(...)` はそのまま通る）

- [ ] **Step 1: PluginLogger を書く**

`source/COM3D2.MToonMaid.Plugin/PluginLogger.cs`:

```csharp
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
```

- [ ] **Step 2: MToonShaderProvider の引数を置き換える**

`source/COM3D2.MToonMaid.Plugin/Rendering/MToonShaderProvider.cs` の `using BepInEx.Logging;` を削除し、`public static MToonShaders Load(ManualLogSource log)` を `internal static MToonShaders Load(PluginLogger log)` にする（`PluginLogger` が internal のため。呼び出し側は同じアセンブリなので影響なし）。本文の `log.LogError(...)` は変えない。

- [ ] **Step 3: PluginInfo から GUID を消す**

`source/COM3D2.MToonMaid.Plugin/PluginInfo.cs` の `public const string PluginGuid = "com.kidonaru.com3d2.mtoonmaid";` の行を削除する（BepInEx 専用で UnityInjector では使わない）。

- [ ] **Step 4: csproj を UnityInjector 向けにする**

`source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj`:

1. `<AssemblyName>COM3D2.MToonMaid</AssemblyName>` → `<AssemblyName>COM3D2.MToonMaid.Plugin</AssemblyName>`
2. MTEUtils のリンク列（`GUIView.cs` の行の後など、アルファベット順の位置）に追加:
   ```xml
       <Compile Include="..\..\MTEUtils\KeyBind.cs" Link="MTEUtils\KeyBind.cs" />
   ```
3. 共通参照の `<Reference Include="BepInEx">...</Reference>` の行を削除
4. net471 の ItemGroup（`UnityEngine.CoreModule` などが並ぶところ）の先頭に追加:
   ```xml
       <!-- COM3D2.5 には UnityInjector.dll の実体が無く、UnityInjector.PluginBase 等は BepInEx.UnityInjectorLoader.dll にある -->
       <Reference Include="BepInEx.UnityInjectorLoader"><HintPath>$(GameDir)\BepInEx\plugins\UnityInjectorLoader\BepInEx.UnityInjectorLoader.dll</HintPath><Private>false</Private></Reference>
   ```
5. net35 の ItemGroup（`Microsoft.NETFramework.ReferenceAssemblies` の PackageReference があるところ）に追加:
   ```xml
       <Reference Include="UnityInjector"><HintPath>$(GameDir)\Sybaris\lib\UnityInjector.dll</HintPath><Private>false</Private></Reference>
   ```

- [ ] **Step 5: MToonMaidPlugin を PluginBase にする**

`source/COM3D2.MToonMaid.Plugin/MToonMaidPlugin.cs` の変更点（それ以外は変えない）:

using:
```csharp
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using COM3D2.MotionTimelineEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityInjector;
using UnityInjector.Attributes;
```
（`BepInEx` / `BepInEx.Configuration` / `BepInEx.Logging` を削除。`COM3D2.MotionTimelineEditor` を足したので、既存の `COM3D2.MotionTimelineEditor.GearMenu.Buttons...` の完全修飾はそのままでよい）

クラス宣言とフィールド:
```csharp
    [
        PluginFilter("COM3D2x64"),
        PluginName(PluginInfo.PluginFullName),
        PluginVersion(PluginInfo.PluginVersion)
    ]
    public class MToonMaidPlugin : PluginBase, IMToonMaidHost
    {
        private const string GearIconResourceName = "gear_icon.png";

        internal static readonly PluginLogger Log = new PluginLogger(PluginInfo.PluginName);

        private PluginConfigStore _config;
        // 読めない・押すキーが無い設定なら null (キーでは開閉しない)
        private KeyBind _toggleKey;
```
（`_scanInterval` / `_toggleKey` の `ConfigEntry` フィールドを上の 2 つに置き換える）

`Awake` の冒頭〜ストア作成まで:
```csharp
        private void Awake()
        {
            // 2.5 版と 2.0 版は DLL 名が同じなので、取り違えて入れると、ゲームの型の違い (goSlot など) で毎フレーム例外になる
            if (!GameCompat.IsExpectedUnityVersion(Application.unityVersion))
            {
                Log.LogError("この MToonMaid は " + GameCompat.GameName + " 用です。Unity " + Application.unityVersion + " では動作しません。ゲームに合った版を入れてください");
                enabled = false;
                return;
            }

            DontDestroyOnLoad(this);
            var configRoot = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Config");
            _config = new PluginConfigStore(Path.Combine(configRoot, PluginInfo.PluginName + ".xml"), Log.LogWarning);
            _toggleKey = ParseToggleKey(_config.Config.toggleWindow);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Camera.onPreCull += OnCameraPreCull;
            var configDirectory = Path.Combine(configRoot, PluginInfo.PluginName);
            _store = new MaidSettingsStore(configDirectory, Log.LogWarning);
```
（以降の `_eyePresets` / `_presets` 作成・URP 判定・`_shaderLoadPending = true` はそのまま。`Log = Logger;` の行は削除）

`ToggleWindow` メソッドの後に追加:

```csharp
        /// <summary>空ならキーを使わない設定として null。押すキーが無い (修飾キーだけ・読めない) 文字列も警告して null</summary>
        private static KeyBind ParseToggleKey(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            var key = new KeyBind(text);
            // KeyBind.GetKeyDown は keyCode が None だと修飾キーを押している間ずっと true を返し、毎フレーム開閉してしまう
            if (key.keyCode == KeyCode.None)
            {
                Log.LogWarning("toggleWindow のキー \"" + text + "\" を読めないため、キーでは開閉できません (例: Ctrl+Shift+M)");
                return null;
            }
            return key;
        }
```

`DontDestroyOnLoad(this)` を入れる理由（コメントは書かない。コミットメッセージに書く）: PostEffects と同じく、ローダーの GameObject の扱いに依存せずシーン遷移で破棄されないようにする。

コメントの書き換え（2 か所）: 「Awake (Chainloader 実行中)」を「Awake (プラグインのロード中)」にする（`Awake` 内のシェーダー読み込みの遅延と、`EnsureWindowManager` の `<summary>`）。

`EnsureWindowManager` の `new WindowPlacementConfig(Config, MToonMaidWindow.DefaultWidth, MToonMaidWindow.DefaultHeight)` → `new WindowPlacementConfig(_config, MToonMaidWindow.DefaultWidth, MToonMaidWindow.DefaultHeight)`

`Update` の `if (_toggleKey.Value.IsDown())` → `if (_toggleKey != null && _toggleKey.GetKeyDown())`

`TickControllers` の `pair.Value.ScanInterval = Mathf.Max(1, _scanInterval.Value);` → `pair.Value.ScanInterval = Mathf.Max(1, _config.Config.scanInterval);`

- [ ] **Step 6: BepInEx の残りを検索する**

Run: `rg -n "BepInEx|ConfigEntry|ManualLogSource|KeyboardShortcut|Paths\.|Logger\b|PluginGuid" source --type cs`
Expected: `MToonMaidWindow.cs` の「BepInEx のログを確認してください」の 3 行だけ（ログは UnityInjectorLoader 経由でも BepInEx の LogOutput.log に出るため、文言は変えない）

- [ ] **Step 7: 両方の版をビルドする（ゲームへはコピーしない）**

Run:
```
dotnet build source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj -c Debug -f net471 -p:COM3D25_DIR=W:\COM3D2_5
dotnet build source/COM3D2.MToonMaid.Plugin/COM3D2.MToonMaid.Plugin.csproj -c Debug -f net35 -p:COM3D2_DIR=W:\COM3D2
```
Expected: どちらも警告の増加なしで成功し、`bin\Debug\net471\COM3D2.MToonMaid.Plugin.dll` と `bin\Debug\net35\COM3D2.MToonMaid.Plugin.dll` ができる

- [ ] **Step 8: テストを流す（Task 2 の Step 4・5 を兼ねる）**

Run: `dotnet test source/COM3D2.MToonMaid.Plugin.Tests -p:COM3D25_DIR=W:\COM3D2_5`
Expected: 全テスト PASS（`WindowPlacementConfigTests` 含む）

- [ ] **Step 9: コミット（Task 2 と合わせて 1 コミット）**

```bash
git add source/
git commit  # commit スキルで。例: refactor!: BepInEx プラグインから UnityInjector プラグインへ置き換える
```

---

### Task 4: ビルド出力・配布物・README を UnityInjector の構成にする

**Files:**
- Modify: `source/COM3D2.MToonMaid.Plugin/build.ps1`
- Modify: `release.bat`
- Modify: `.gitignore`
- Modify: `deploy.ps1`（Release Note の文面だけ。**実行しない**）
- Modify: `README.md`
- Move: `BepInEx/config/COM3D2.MToonMaid/` → `UnityInjector/Config/MToonMaid/`

**Interfaces:**
- Consumes: Task 3 の DLL 名 `COM3D2.MToonMaid.Plugin.dll`
- Produces: リポジトリ内の出力 `UnityInjector (COM3D2.5)\COM3D2.MToonMaid.Plugin.dll`（2.5）/ `UnityInjector\COM3D2.MToonMaid.Plugin.dll`（2.0）、同梱プリセット `UnityInjector\Config\MToonMaid\`。配布 zip の中は `UnityInjector\`（2.0）と `UnityInjector (COM3D2.5)\`（2.5）で、どちらも DLL と `Config\MToonMaid\` を持つ

- [ ] **Step 1: 同梱プリセットを移す**

```bash
mkdir -p UnityInjector/Config
git mv BepInEx/config/COM3D2.MToonMaid UnityInjector/Config/MToonMaid
```
そのあと `BepInEx\` と `BepInEx (COM3D2)\` に残るのが ignore 済みのビルド出力（`plugins\*.dll`）だけであることを `git status --ignored BepInEx "BepInEx (COM3D2)"` で確かめてから、2 つのフォルダを削除する。

- [ ] **Step 2: build.ps1 を書き換える**

`source/COM3D2.MToonMaid.Plugin/build.ps1`:
- `.DESCRIPTION`: 「リポジトリ内のコピー先は 2.5 版が UnityInjector (COM3D2.5)、2.0 版が UnityInjector。」「ゲームへのコピーは、ゲーム起動中は DLL がロックされるため失敗しても続行する。」
- `$assemblyName = 'COM3D2.MToonMaid.Plugin'`
- `RepoOut = 'UnityInjector (COM3D2.5)'`（2.5）/ `RepoOut = 'UnityInjector'`（2.0）
- `$gameOut = Join-Path $b.GameDir "Sybaris\UnityInjector\$assemblyName.dll"`
- コメント「フォルダだけを宛先にすると、plugins が無いときに plugins という名前のファイルとしてコピーされる」の `plugins` を `UnityInjector` にする

- [ ] **Step 3: release.bat を書き換える**

`release.bat`:
- `set ASSEMBLY_NAME=COM3D2.MToonMaid.Plugin`
- 「配布パッケージ」の段を次にする（リポジトリと zip のフォルダ名が一致したので、取り違え防止の SRC/PKG の読み替えとそのコメントは消す。DLL はフォルダ直下）:

```bat
rem ============ 配布パッケージ ============
set PKG_COM3D2=output\%PLUGIN_NAME%\UnityInjector
set PKG_COM3D25=output\%PLUGIN_NAME%\UnityInjector (COM3D2.5)
md "%PKG_COM3D2%"
copy /y "UnityInjector\%ASSEMBLY_NAME%.dll" "%PKG_COM3D2%\" >nul
if %ERRORLEVEL% neq 0 (
    echo COM3D2 版の dll のコピーに失敗しました
    exit /b 1
)
md "%PKG_COM3D25%"
copy /y "UnityInjector (COM3D2.5)\%ASSEMBLY_NAME%.dll" "%PKG_COM3D25%\" >nul
if %ERRORLEVEL% neq 0 (
    echo COM3D2.5 版の dll のコピーに失敗しました
    exit /b 1
)
for %%D in ("%PKG_COM3D2%" "%PKG_COM3D25%") do (
    xcopy /e /i /y /q "UnityInjector\Config" "%%~D\Config\" >nul
    if errorlevel 1 (
        echo %%~D へのプリセットのコピーに失敗しました
        exit /b 1
    )
)
```
- `check_runtime` の 2 行のパスを `"%PKG_COM3D2%\%ASSEMBLY_NAME%.dll"` / `"%PKG_COM3D25%\%ASSEMBLY_NAME%.dll"` にする（その上のコメント「2 つの dll は同名なので…」は残す）

- [ ] **Step 4: .gitignore を書き換える**

```
# ビルド成果物。build.bat が毎回生成するため追跡しない
UnityInjector/*.dll
UnityInjector (COM3D2.5)/*.dll
```
（`BepInEx/plugins/*.dll` / `BepInEx (COM3D2)/plugins/*.dll` の 2 行を置き換える）

- [ ] **Step 5: deploy.ps1 の Release Note 文面を直す（実行はしない）**

`deploy.ps1:150-157` 付近を読み、次の意味の文面にする:
- 「zip を解凍し、お使いのゲームに対応したフォルダの中身を、ゲームのインストールフォルダの ``Sybaris\UnityInjector`` フォルダへ上書きコピーしてください。」
- 表: **COM3D2.5** → ``UnityInjector (COM3D2.5)``、**COM3D2 (2.0)** → ``UnityInjector``
- 「COM3D2.5 では zip 直下の ``UnityInjector`` フォルダ (2.0 用) は使いません。」

- [ ] **Step 6: README を書き換える**

`README.md`（Release Notes の過去の記述は変えない）:

インストール方法（「zip を解凍し…」から「- `README.txt`」の説明まで）:
```markdown
zip を解凍し、ゲームに合わせて次のフォルダの中身を、ゲームのインストールフォルダの`Sybaris\UnityInjector`フォルダへ上書きコピーしてください。

- COM3D2.5: `UnityInjector (COM3D2.5)`フォルダ (zip 直下の`UnityInjector`フォルダは 2.0 用なので使いません)
- COM3D2 (2.0): `UnityInjector`フォルダ

各ファイルの説明:
- `UnityInjector (COM3D2.5)/COM3D2.MToonMaid.Plugin.dll`
  - COM3D2.5 用のプラグインの本体。シェーダーも dll に含まれています
- `UnityInjector/COM3D2.MToonMaid.Plugin.dll`
  - COM3D2 (2.0) 用のプラグインの本体。シェーダーも dll に含まれています
- `UnityInjector (COM3D2.5)/Config/MToonMaid/`、`UnityInjector/Config/MToonMaid/`
  - 同梱のプリセット (中身はどちらも同じです)
```
（`LICENSE-UniVRM.txt` / `README.txt` の行は残す）

動作環境の 2 行:
```markdown
- COM3D2.5 + BepInEx 5 + UnityInjectorLoader（UnityInjector プラグインが動く環境）
- COM3D2 (2.0) + BepInEx 5 + UnityInjectorLoader
```
（「5.4.23 より古い BepInEx では『targets a wrong version of BepInEx』の警告…」の注記は BepInEx プラグイン特有なので消す。DirectX 11 の行は残す）

起動方法の括弧書き:
```markdown
（`Sybaris/UnityInjector/Config/MToonMaid.xml`の`toggleWindow`でキーを変更できます。例: `Ctrl+Shift+M`、`Alt+F2`）
```

設定ファイルの節:
```markdown
| パス | 内容 |
|---|---|
| `Sybaris/UnityInjector/Config/MToonMaid.xml` | 全体の設定（下表） |
| `Sybaris/UnityInjector/Config/MToonMaid/` | メイドごとの設定 |
| `Sybaris/UnityInjector/Config/MToonMaid/Presets/` | プリセットタブのプリセット（1 個 1 ファイル） |
| `Sybaris/UnityInjector/Config/MToonMaid/EyePresets/` | 瞳の [プリセット]（1 個 1 ファイル） |

全体の設定（ゲームを起動していないときに編集してください）:

| 項目 | 既定 | 効果 |
|---|---|---|
| `toggleWindow` | `Ctrl+Shift+M` | ウィンドウの開閉キー |
| `scanInterval` | 10 | マテリアルを走査する間隔（フレーム）。小さいほど着替え直後の追従が速く、負荷が増える |
```
「ゲームを起動していないときに」の理由: ウィンドウを動かすとプラグインがファイルを書き直し、起動中の手編集が消えるため。

開発者向け:
- 「(自動でゲームの`BepInEx\plugins`内にコピーされます。…」→「(自動でゲームの`Sybaris\UnityInjector`内にコピーされます。…」

README 内の `BepInEx` の残りを `rg -n "BepInEx|COM3D2\.MToonMaid\.dll|\.cfg" README.md` で確かめ、Release Notes 以外に残っていないこと（動作環境の BepInEx 5 は除く）。

- [ ] **Step 7: release.bat を通す**

Run: `.\release.bat`（ゲーム停止中ならゲームへ DLL がコピーされる点に注意。Task 5 で実機へ入れるので問題ない。**deploy.bat は実行しない**）
Expected: 「ビルドに成功しました」。`output\COM3D2.MToonMaid.Plugin-v1.0.0.0.zip` を展開して、次の構成であることを確かめる:
```
COM3D2.MToonMaid.Plugin/
  UnityInjector/COM3D2.MToonMaid.Plugin.dll
  UnityInjector/Config/MToonMaid/{Presets,EyePresets}/...
  UnityInjector (COM3D2.5)/COM3D2.MToonMaid.Plugin.dll
  UnityInjector (COM3D2.5)/Config/MToonMaid/{Presets,EyePresets}/...
  LICENSE-UniVRM.txt
  README.txt
```

- [ ] **Step 8: コミット**

```bash
git add -A BepInEx UnityInjector .gitignore release.bat deploy.ps1 README.md source/COM3D2.MToonMaid.Plugin/build.ps1
git commit  # commit スキルで。例: chore(release): 配布物とビルド出力を UnityInjector の構成にする
```

---

### Task 5: 実機確認（COM3D2.5、可能なら 2.0）

**Files:** なし（ゲームフォルダの DLL 入れ替えのみ）

- [ ] **Step 1: 旧 BepInEx 版を外す**

両方のゲームの `BepInEx\plugins\COM3D2.MToonMaid.dll` を `COM3D2.MToonMaid.dll.old` へ改名する（同じプラグインが二重に動いて同じマテリアルを変換するのを避ける。削除ではなく改名にして戻せるようにする）。ゲーム起動中はロックで失敗するので、`com3d25-devbridge:restart-verify` スキルの手順でゲームを終了してから行う。

- [ ] **Step 2: 2.5 へ入れて起動する**

`restart-verify` スキルに従い、ゲーム停止中に `.\debug.bat com3d25` → 起動 → セーブをロード。

- [ ] **Step 3: 確認する（`eval_csharp` / `tail_log` / `screenshot`）**

1. `tail_log` に `[MToonMaid] MToonMaid 有効化` が出て、`MToonMaid` 関連のエラーが無い
2. `W:\COM3D2_5\Sybaris\UnityInjector\Config\MToonMaid.xml` が既定値で作られている
3. `Ctrl+Shift+M` とギアメニューのアイコンでウィンドウが開閉する
4. メイドの「MToon にする」を ON にすると見た目が変わり、`Config\MToonMaid\<GUID>.xml` が作られる。プリセットタブで同梱プリセットが見える（`Config\MToonMaid\Presets\` へ同梱分を手でコピーしておく）
5. ウィンドウを動かして離すと `MToonMaid.xml` の `windowX/Y` が変わる
6. シーンを切り替えても（例: エディット → メイン画面 → エディット）ウィンドウ・アイコン・変換が続く
7. `MToonMaid.xml` の `toggleWindow` を `Ctrl+Shft+M` にして再起動すると、警告ログが出てキーでは開かず、アイコンでは開ける。`Ctrl+Shift` でも同じで、Ctrl+Shift を押し続けてもウィンドウが点滅しない（確認後に元へ戻す）

- [ ] **Step 4: 2.0 で確認する（BepInExMcp :18650 が使える場合）**

memory の [[com3d2-20-support-feasibility]] の手順で 2.0 を起動し、`W:\COM3D2\Sybaris\UnityInjector\COM3D2.MToonMaid.Plugin.dll` を入れて Step 3 の 1〜4 を確かめる。2.0 を起動できない場合は、未確認であることをユーザーへの報告に書く。

- [ ] **Step 5: 問題が出たら修正してコミット**

問題が出なければコミットは無い。

## レビュー却下メモ

- 旧 BepInEx 版 DLL を消す案内を README・Release Note に足す — ユーザーが旧版からの引き継ぎは「記載も不要」と決めたため、計画では足さない（要否はユーザーに確認する）
- 起動中に手で編集した値がウィンドウ位置の保存で消える — README に「ゲームを起動していないときに編集」と書くことで対処済み
- Task 2 が単体でビルド・テストできない — Task 3 Step 8 でまとめて確かめる手順になっており、変更不要
