using System.Collections.Generic;
using System.IO;
using System.Reflection;
using COM3D2.MotionTimelineEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityInjector;
using UnityInjector.Attributes;

namespace COM3D2.MToonMaid.Plugin
{
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
        private WindowManager _windowManager;
        private bool _windowInitFailed;
        private GameObject _gearIcon;
        private byte[] _gearIconPng;
        private bool _gearIconPngLoaded;
        // 枠色の更新は Texture2D.Apply を伴うため、表示状態が変わったときだけ行う
        private bool? _gearIconVisible;
        private MaterialConverter _converter;
        // 前髪の深度を書くシェーダー。読めなければ null で前髪の影を使わない
        private Shader _hairShadowCaster;
        // 瞳の光沢。シェーダーを読めなければ null
        private EyeConverter _eyeConverter;
        // 前髪の影と瞳の視線を更新したフレーム。カメラが複数あってもフレームに 1 回にする
        private int _preCullFrame = -1;
        // 前髪の影・瞳の視線の更新の例外は、それぞれ 1 回だけ知らせる
        private bool _hairShadowWarned;
        private bool _eyeGazeWarned;
        private bool _faceHalfShadowWarned;
        private bool _shaderLoadPending;
        private MaidSettingsStore _store;
        private PresetStore<EyePreset> _eyePresets;
        private PresetStore<MaidPreset> _presets;
        // Maid インスタンスごと。非表示の間も控えを失わないよう可視性では外さない
        private readonly Dictionary<Maid, MaidMToonController> _controllers = new Dictionary<Maid, MaidMToonController>();
        private readonly List<Maid> _removeBuffer = new List<Maid>();
        private readonly List<Maid> _activeMaids = new List<Maid>();
        private readonly List<string> _slotCategories = new List<string>();
        private readonly List<TBodySkin> _skinBuffer = new List<TBodySkin>();

        public bool IsShaderAvailable => _converter != null;
        public bool IsHairShadowAvailable => _hairShadowCaster != null;
        public bool IsEyeGlossAvailable => _eyeConverter != null;
        public PresetStore<EyePreset> EyePresets => _eyePresets;
        public PresetStore<MaidPreset> Presets => _presets;

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
            // 設定ファイルと同じフォルダに置くと、GUID のファイルと名前のファイルが混ざる
            _eyePresets = new PresetStore<EyePreset>(Path.Combine(configDirectory, "EyePresets"), "瞳のプリセット", Log.LogWarning);
            _presets = new PresetStore<MaidPreset>(Path.Combine(configDirectory, "Presets"), "プリセット", Log.LogWarning);

            // MToon10 の built-in 版は URP では描画されない (ゲーム側に URP の切り替えがある)。
            // ウィンドウで理由を見せるため enabled は落とさない
            if (GameCompat.IsScriptableRenderPipelineActive())
            {
                Log.LogError("URP が有効なため MToonMaid は動作しません");
                return;
            }

            // Awake (プラグインのロード中) で AssetBundle からシェーダーを読むと
            // LoadAllAssets がネイティブでクラッシュするため、最初の Update まで遅らせる
            _shaderLoadPending = true;
        }

        private void LoadShader()
        {
            _shaderLoadPending = false;
            var shaders = MToonShaderProvider.Load(Log);
            if (shaders != null)
            {
                _converter = new MaterialConverter(shaders.mtoon, new ToonRampReader(Log.LogWarning));
                _hairShadowCaster = shaders.hairShadowCaster;
                if (shaders.eye != null)
                {
                    _eyeConverter = new EyeConverter(shaders.eye, shaders.hidden, shaders.eyeHi);
                }
                Log.LogInfo("MToonMaid 有効化");
            }
        }

        /// <summary>
        /// GUIView の静的初期化が Texture2D を作り、Awake (プラグインのロード中) ではネイティブでクラッシュするため、
        /// ウィンドウは最初の Update で作る
        /// </summary>
        private bool EnsureWindowManager()
        {
            if (_windowManager != null || _windowInitFailed)
            {
                return _windowManager != null;
            }
            try
            {
                var manager = new WindowManager(this,
                    new WindowPlacementConfig(_config, MToonMaidWindow.DefaultWidth, MToonMaidWindow.DefaultHeight),
                    _config.Config);
                manager.Init();
                _windowManager = manager;
                return true;
            }
            catch (System.Exception e)
            {
                // UI が無くても設定済みのメイドの変換と保存は続けられるよう、UI だけを諦める
                _windowInitFailed = true;
                Log.LogError("ウィンドウを作れないため UI なしで動作します: " + e);
                return false;
            }
        }

        private void Update()
        {
            if (EnsureWindowManager())
            {
                if (_toggleKey != null && _toggleKey.GetKeyDown())
                {
                    ToggleWindow();
                }
                EnsureGearIcon();
                _windowManager.Update();
            }

            if (_shaderLoadPending)
            {
                LoadShader();
            }
            if (_converter == null)
            {
                return;
            }

            AttachControllers();
            TickControllers();
            _store.FlushIfDue(Time.realtimeSinceStartup);
        }

        private void OnApplicationQuit()
        {
            _store?.FlushAll();
        }

        private void LateUpdate()
        {
            _windowManager?.LateUpdate();
        }

        private void OnGUI()
        {
            _windowManager?.OnGUI();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Camera.onPreCull -= OnCameraPreCull;
            foreach (var controller in _controllers.Values)
            {
                controller.ReleaseHairShadow();
            }
            _eyeConverter?.Dispose();
            _windowManager?.OnPluginDisable();
        }

        /// <summary>
        /// 揺れもの (前髪の物理) と目の動きが LateUpdate で決まった後に描くため、カメラの描画の直前に前髪の影・顔の半面切り替えの影・瞳の視線を更新する。
        /// カメラが複数あってもフレームに 1 回にする
        /// </summary>
        private void OnCameraPreCull(Camera camera)
        {
            if (_converter == null || _preCullFrame == Time.frameCount)
            {
                return;
            }
            _preCullFrame = Time.frameCount;
            foreach (var pair in _controllers)
            {
                // 破棄済みや別キャラは次の Update の TickControllers が片付ける
                if (pair.Key == null || pair.Key.status == null || pair.Key.status.guid != pair.Value.Guid)
                {
                    continue;
                }
                var face = _store.Get(pair.Value.Guid).Resolve(PartGroup.Face);
                // カメラのコールバックで例外を外へ出すと他の購読者が呼ばれず、毎フレーム全カメラでログが出る
                // 同じ try にすると、片方の例外でもう片方が止まり、警告も片方しか出ない
                try
                {
                    pair.Value.RenderHairShadow(face);
                }
                catch (System.Exception e)
                {
                    if (!_hairShadowWarned)
                    {
                        _hairShadowWarned = true;
                        Log.LogWarning("前髪の影の更新に失敗しました: " + e);
                    }
                }
                try
                {
                    pair.Value.UpdateFaceHalfShadow(face);
                }
                catch (System.Exception e)
                {
                    if (!_faceHalfShadowWarned)
                    {
                        _faceHalfShadowWarned = true;
                        Log.LogWarning("顔の半面切り替えの影の更新に失敗しました: " + e);
                    }
                }
                try
                {
                    pair.Value.UpdateEyeGaze();
                }
                catch (System.Exception e)
                {
                    if (!_eyeGazeWarned)
                    {
                        _eyeGazeWarned = true;
                        Log.LogWarning("瞳の視線の更新に失敗しました: " + e);
                    }
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _windowManager?.OnChangedSceneLevel(scene, mode);
        }

        private void ToggleWindow()
        {
            var window = _windowManager.MainWindow;
            window.isShowWnd = !window.isShowWnd;
        }

        /// <summary>空ならキーを使わない設定として null。押すキーが無い (修飾キーだけ・読めない) 文字列も警告して null</summary>
        private static KeyBind ParseToggleKey(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            var key = new KeyBind(text);
            // KeyBind.GetKeyDown は keyCode が None だと修飾キーを押している間ずっと true を返し、毎フレーム開閉してしまう
            // KeyBind は Enum.Parse で読むため、"999" のような数値も通ってしまい、二度と押せないキーになる
            if (key.keyCode == KeyCode.None || !System.Enum.IsDefined(typeof(KeyCode), key.keyCode))
            {
                Log.LogWarning("toggleWindow のキー \"" + text + "\" を読めないため、キーでは開閉できません (例: Ctrl+Shift+M)");
                return null;
            }
            return key;
        }

        /// <summary>ギアメニューのアイコン。シーン遷移で破棄されるため無ければ作り直す</summary>
        private void EnsureGearIcon()
        {
            if (_gearIcon == null && COM3D2.MotionTimelineEditor.GearMenu.Buttons.IsReady)
            {
                _gearIconVisible = null;
                _gearIcon = COM3D2.MotionTimelineEditor.GearMenu.Buttons.Add(
                    PluginInfo.PluginName, PluginInfo.PluginName, LoadGearIconPng(),
                    go => ToggleWindow());
            }
            var visible = _windowManager.MainWindow.isShowWnd;
            if (_gearIcon != null && _gearIconVisible != visible)
            {
                _gearIconVisible = visible;
                COM3D2.MotionTimelineEditor.GearMenu.Buttons.SetFrameColor(_gearIcon, visible ? Color.blue : Color.white);
            }
        }

        /// <summary>埋め込みのアイコン PNG。読めなければ null（ギアメニューのシステムアイコンになる）</summary>
        private byte[] LoadGearIconPng()
        {
            if (_gearIconPngLoaded)
            {
                return _gearIconPng;
            }
            _gearIconPngLoaded = true;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GearIconResourceName))
            {
                if (stream == null)
                {
                    Log.LogWarning("埋め込みリソース " + GearIconResourceName + " が見つかりません。システムアイコンで表示します");
                    return null;
                }
                _gearIconPng = new byte[stream.Length];
                stream.Read(_gearIconPng, 0, _gearIconPng.Length);
            }
            return _gearIconPng;
        }

        /// <summary>表示中で ON のメイドに Controller を付ける</summary>
        private void AttachControllers()
        {
            foreach (var maid in GetActiveMaids())
            {
                if (_controllers.ContainsKey(maid))
                {
                    continue;
                }
                var guid = maid.status.guid;
                if (_store.Get(guid).enabled)
                {
                    _controllers[maid] = new MaidMToonController(maid, guid, _converter, _hairShadowCaster, _eyeConverter, Log.LogWarning);
                }
            }
        }

        private void TickControllers()
        {
            _removeBuffer.Clear();
            foreach (var pair in _controllers)
            {
                // 破棄済みの Maid (Unity の == は null 扱い)。マテリアルも破棄済みなので戻さず、作ったテクスチャだけ解放する
                if (pair.Key == null)
                {
                    pair.Value.DestroyOwnedTextures();
                    _removeBuffer.Add(pair.Key);
                    continue;
                }

                // ストック Maid の使い回しで別キャラが入った、または OFF にされた。ON のキャラ以外を変換しない
                var status = pair.Key.status;
                var settings = _store.Get(pair.Value.Guid);
                if (status == null || status.guid != pair.Value.Guid || !settings.enabled)
                {
                    pair.Value.RestoreAll();
                    _removeBuffer.Add(pair.Key);
                    continue;
                }

                pair.Value.ScanInterval = Mathf.Max(1, _config.Config.scanInterval);
                pair.Value.Tick(settings);
            }
            foreach (var maid in _removeBuffer)
            {
                _controllers.Remove(maid);
            }
        }

        public List<Maid> GetActiveMaids()
        {
            _activeMaids.Clear();
            var mgr = GameMain.Instance != null ? GameMain.Instance.CharacterMgr : null;
            if (mgr == null)
            {
                return _activeMaids;
            }
            for (var i = 0; i < mgr.GetMaidCount(); i++)
            {
                var maid = mgr.GetMaid(i);
                // 生成直後は body0 / status が揃っていないことがあるため、揃ったメイドだけを扱う
                if (maid != null && maid.Visible && maid.body0 != null && maid.status != null)
                {
                    _activeMaids.Add(maid);
                }
            }
            return _activeMaids;
        }

        public MaidSettings GetSettings(Maid maid)
        {
            return _store.Get(maid.status.guid);
        }

        public void SetEnabled(Maid maid, bool value)
        {
            var settings = GetSettings(maid);
            if (settings.enabled == value)
            {
                return;
            }
            settings.enabled = value;
            _store.MarkDirty(maid.status.guid, Time.realtimeSinceStartup);

            MaidMToonController controller;
            if (!value && _controllers.TryGetValue(maid, out controller))
            {
                controller.RestoreAll();
                _controllers.Remove(maid);
            }
            // ON は次の Update の AttachControllers で付く
        }

        public void NotifyGroupChanged(Maid maid, PartGroup group)
        {
            _store.MarkDirty(maid.status.guid, Time.realtimeSinceStartup);
            MaidMToonController controller;
            if (_controllers.TryGetValue(maid, out controller))
            {
                controller.ApplyGroup(group, GetSettings(maid).Resolve(group));
            }
        }

        public int GetConvertedCount(Maid maid)
        {
            MaidMToonController controller;
            return _controllers.TryGetValue(maid, out controller) ? controller.ConvertedCount : 0;
        }

        public bool IsScleraFound(Maid maid)
        {
            MaidMToonController controller;
            return _controllers.TryGetValue(maid, out controller) && controller.IsScleraFound;
        }

        public List<string> GetSlotCategories(Maid maid, PartGroup group)
        {
            _slotCategories.Clear();
            GameCompat.CollectSkins(maid.body0, _skinBuffer);
            foreach (var skin in _skinBuffer)
            {
                AddSlotCategory(skin.Category, group);
            }
            foreach (var excluded in GetSettings(maid).excludedSlots)
            {
                AddSlotCategory(excluded, group);
            }
            _slotCategories.Sort(System.StringComparer.Ordinal);
            return _slotCategories;
        }

        private void AddSlotCategory(string category, PartGroup group)
        {
            if (!string.IsNullOrEmpty(category)
                && ToonMaterialClassifier.GetGroup(category) == group
                && !_slotCategories.Contains(category))
            {
                _slotCategories.Add(category);
            }
        }

        public void NotifyTargetsChanged(Maid maid)
        {
            _store.MarkDirty(maid.status.guid, Time.realtimeSinceStartup);
            MaidMToonController controller;
            if (_controllers.TryGetValue(maid, out controller))
            {
                controller.RequestScan();
            }
        }

        public void NotifyEyeChanged(Maid maid)
        {
            _store.MarkDirty(maid.status.guid, Time.realtimeSinceStartup);
            MaidMToonController controller;
            if (_controllers.TryGetValue(maid, out controller))
            {
                controller.ApplyEye(GetSettings(maid).eye);
            }
        }
    }
}
