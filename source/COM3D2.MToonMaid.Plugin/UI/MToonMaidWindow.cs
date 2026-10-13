using System;
using System.Collections.Generic;
using COM3D2.MotionTimelineEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>メイドの選択・ON/OFF・共通と部位グループごとの調整値・対象スロット・瞳の光沢のウィンドウ</summary>
    public sealed class MToonMaidWindow : DockableWindowBase
    {
        public const int DefaultWidth = 380;
        public const int DefaultHeight = 640;

        private const int WindowId = 0x4D546F6E; // "MTon"
        private const float RowHeight = 20f;
        private const float LabelWidth = 110f;
        // 部位と瞳の 6 個を最小幅 380 の 1 行に収める幅。見出しが収まらないタブは DrawWrappedTabs が広げる
        private const float TabWidth = 52f;
        private const float ResetButtonWidth = 200f;
        private const float OverrideToggleWidth = 20f;
        private const float CentimetersPerMeter = 100f;
        // SceneEditor が内容の先頭の水平線の下に空けている量
        private const float ScrollTopSpace = 5f;
        // 0 番が共通、1 番以降が PartGroup の順
        private const int CommonTab = 0;
        // 瞳とプリセットのタブは PartGroup に対応しない
        private const int EyeTab = 5;
        private const int PresetTab = 6;

        private static readonly string[] TabLabels = { "共通", "肌", "顔", "髪", "衣装", "瞳", "プリセット" };
        private static readonly string[] ResolutionLabels = Array.ConvertAll(HairShadowProjection.Resolutions, r => r.ToString());
        private const float ChoiceWidth = 50f;
        // 8 個と [追加][削除] が最小幅 380 に収まる幅
        private const float DataTabWidth = 24f;
        private const float DataButtonWidth = 44f;
        private const float PairResetButtonWidth = 20f;
        private const float PairDragSensitivity = 0.002f;
        private const int EyeShadeSubTab = 0;
        private const int EyeHighlightSubTab = 1;
        private const int EyeColorSubTab = 2;
        private const int EyeScleraSubTab = 3;
        private const int EyePresetSubTab = 4;
        private static readonly string[] EyeSubTabLabels = { "影・明るさ", "ハイライト", "瞳の色", "白目", "プリセット" };
        private static readonly string[] EyeColorSideLabels = { "左右", "左", "右" };
        private const string EyeMainColorKey = "瞳/瞳の色/メイン";
        private const string EyeShadowColorKey = "瞳/瞳の色/影";
        private const float PresetNameLabelWidth = 40f;
        private const float SectionSpace = 6f;
        private const float SectionButtonWidth = 70f;
        // アクセント色は ON のトグルや選択中のタブと同じ色で、見出しが操作できる項目に見える
        private static readonly Color SectionColor = Color.white;
        // GUIStyle は OnGUI の外で作れない
        private static GUIStyle _sectionStyle;

        private readonly IMToonMaidHost _host;
        private readonly WindowPlacementConfig _placement;
        private readonly GUIView _rootView = new GUIView();
        private readonly GUIView _contentView = new GUIView();
        private readonly GUIComboBox<Maid> _maidComboBox = new GUIComboBox<Maid>();
        private readonly List<string> _slots = new List<string>();
        // スライダーと色のリセット先
        private readonly GroupSettings _default = GroupSettings.CreateDefault();
        private readonly EyeSettings _eyeDefault = EyeSettings.CreateDefault();
        // ハイライトの行のリセット先と、0 個のときに無効表示で見せる値
        private readonly EyeHighlightSettings _newHighlight = EyeSettings.CreateNewHighlight();
        // _newHighlight を表示に使うと、開いたままのカラーピッカーがリセット先の色を書き換えうる
        private readonly EyeHighlightSettings _emptyHighlight = EyeSettings.CreateNewHighlight();
        private int _width = DefaultWidth;
        private int _height = DefaultHeight;
        private int _tab = CommonTab;
        private readonly float[] _tabWidths = new float[TabLabels.Length];
        private readonly float[] _eyeSubTabWidths = new float[EyeSubTabLabels.Length];
        // 描いている最中の対象。null のグループは共通タブ
        private MaidSettings _settings;
        private PartGroup? _editGroup;
        // 描画中の変更は描画の後でまとめて本体へ伝える
        private bool _groupChanged;
        private bool _targetsChanged;
        private bool _eyeChanged;
        private bool? _enabledRequest;
        // コンボボックスのポップアップ中は全体を無効にする。行単位の無効表示の後はこの値へ戻す
        private bool _baseEnabled = true;
        // 選んでいるハイライトの番号。個数が減ったら描く前に HighlightSelection.Clamp で範囲へ寄せる
        private int _highlightIndex;
        // 選んでいるサブタブ。メイドを切り替えても変えない
        private int _eyeSubTab = EyeShadeSubTab;
        // 描画中に SetPartsColor を呼ぶと、ゲームのテクスチャの作り直し (Blit) が GUI の描画中に走るため、Update まで遅らせる
        private Maid _eyeColorMaid;
        private EyeColorSide _eyeColorSide;
        private EyeColorValues _eyeColorValues;
        private bool _eyeColorWritePending;
        private bool _eyeColorSidePending;
        private bool _eyeColorWarned;
        private readonly EyePickedColor _eyeMainPicked = new EyePickedColor();
        private readonly EyePickedColor _eyeShadowPicked = new EyePickedColor();
        // 瞳の色の行を最後に描いた対象。変わったら開いているピッカーを閉じる
        private Maid _eyeColorShownMaid;
        private EyeColorSide _eyeColorShownSide;
        private readonly EyeColorBaselines _eyeColorBaselines = new EyeColorBaselines();
        private EyeColorSnapshot _eyeColorRestore;
        private bool _eyeColorRestorePending;
        // 前の描画で出していたプリセットの一覧 (PresetStore)。出し始めに一覧を読み直す
        private object _shownPresets;
        private readonly PresetListState _eyePresetState = new PresetListState();
        private readonly PresetListState _presetState = new PresetListState();

        protected override int windowId => WindowId;
        protected override string windowTitle => "MToonMaid";
        // 色の行 (個別のトグル・ラベル・見本・HEX・R・編集) がスクロールバーと重ならない幅
        protected override int minWidth => 380;
        protected override int minHeight => 320;

        public MToonMaidWindow(IMToonMaidHost host, WindowPlacementConfig placement)
        {
            _host = host;
            _placement = placement;
            windowIndex = 0;
            _maidComboBox.getName = (maid, index) => maid != null && maid.status != null ? maid.status.fullNameJpStyle : "";
        }

        protected override void LoadPlacement(out int x, out int y, out int width, out int height)
        {
            _placement.Load(out x, out y, out width, out height);
        }

        protected override void StorePlacement(int x, int y, int width, int height)
        {
            _placement.Store(x, y, width, height);
        }

        public override void Init()
        {
            base.Init();
            _width = (int)localWindowRect.width;
            _height = (int)localWindowRect.height;
            InitView();
        }

        public override void Update()
        {
            base.Update();
            _placement.FlushIfReleased();
            FlushEyeColor();
        }

        public override void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            base.OnChangedSceneLevel(scene, sceneMode);
            // 追加読み込みで捨てると、編集の途中で編集前が編集後の値に変わる
            if (sceneMode == LoadSceneMode.Single)
            {
                // 控えを残すと、セーブをロードしたあとに前のセーブの色へ戻してしまう
                _eyeColorBaselines.Clear();
            }
        }

        private void FlushEyeColor()
        {
            var maid = _eyeColorMaid;
            var restore = _eyeColorRestorePending;
            var side = _eyeColorSidePending;
            var write = _eyeColorWritePending;
            // 書く前に降ろす (例外が出ても毎フレーム同じ書き込みを繰り返さない)
            _eyeColorRestorePending = false;
            _eyeColorSidePending = false;
            _eyeColorWritePending = false;
            if (maid == null || maid.body0 == null || !(restore || side || write))
            {
                return;
            }
            try
            {
                if (restore)
                {
                    MaidEyeColor.Restore(maid, _eyeColorRestore);
                    return;
                }
                if (side)
                {
                    MaidEyeColor.SetSide(maid, _eyeColorSide);
                }
                if (write)
                {
                    MaidEyeColor.Write(maid, _eyeColorSide, _eyeColorValues);
                }
            }
            catch (Exception e)
            {
                // 窓の Update はプラグインの Update の try の外で呼ばれ、例外を投げるとそのフレームの走査が止まる
                if (!_eyeColorWarned)
                {
                    _eyeColorWarned = true;
                    MToonMaidPlugin.Log.LogWarning("瞳の色を変えられませんでした: " + e);
                }
            }
        }

        protected override void OnSizeChanged(int width, int height)
        {
            _width = width;
            _height = height;
            InitView();
        }

        private void InitView()
        {
            _rootView.Init(0, 0, _width, _height);
            // SceneEditor の窓と同じく、左右と下に枠の幅を空けた内容領域 (contentRect と同じ式) に GUIView 既定のパディングで描く
            _contentView.Init(FRAME, HEADER_HEIGHT, _width - FRAME * 2, _height - HEADER_HEIGHT - FRAME);
            _contentView.parent = _rootView;
        }

        protected override void DrawContent()
        {
            _rootView.ResetLayout();
            DrawMain(_contentView);
            ComboBoxPopupWindow.instance.ProcessFocus(_rootView, this);
        }

        private void DrawMain(GUIView view)
        {
            view.ResetLayout();
            // 下の早期 return でプリセットを描かなかった描画の後に、出し始めとして一覧を読み直させる
            var presetsBefore = _shownPresets;
            _shownPresets = null;
            _baseEnabled = !ComboBoxPopupWindow.instance.IsOpenFor(this);
            view.SetEnabled(_baseEnabled);

            if (!_host.IsShaderAvailable)
            {
                view.DrawLabel("シェーダーを読み込めないか、URP が有効なため動作しません。ログを確認してください", -1, RowHeight * 3);
                return;
            }

            var maids = _host.GetActiveMaids();
            if (maids.Count == 0)
            {
                view.DrawLabel("表示中のメイドがいません", -1, RowHeight);
                return;
            }

            SyncMaidItems(maids);
            _maidComboBox.DrawButton("メイド", view);
            var maid = _maidComboBox.currentItem;
            if (maid == null)
            {
                return;
            }

            _settings = _host.GetSettings(maid);
            view.DrawToggle("MToon にする", _settings.enabled, -1, RowHeight, v => _enabledRequest = v,
                "このメイドのマテリアルを MToon のシェーダーに置き換える\nOFF で元のシェーダーへ戻す");
            view.DrawLabel("変換済みマテリアル: " + _host.GetConvertedCount(maid), -1, RowHeight);
            view.DrawHorizontalLine();

            _tab = DrawWrappedTabs(view, TabLabels, _tabWidths, _tab);
            var eyeTab = _tab == EyeTab;
            _editGroup = null;
            var presetTab = _tab == PresetTab;
            if (!eyeTab && !presetTab && _tab != CommonTab)
            {
                _editGroup = (PartGroup)(_tab - 1);
            }
            if (eyeTab)
            {
                var eye = _settings.eye;
                view.DrawToggle("瞳を光沢のシェーダーにする", eye.enabled, -1, RowHeight, _host.IsEyeGlossAvailable && _baseEnabled, v =>
                {
                    eye.enabled = v;
                    _eyeChanged = true;
                }, "瞳を専用のシェーダーに置き換え、影・明るさ・ハイライトを足せるようにする\nOFF では影・明るさ・ハイライトの設定が効かない");
                _eyeSubTab = DrawWrappedTabs(view, EyeSubTabLabels, _eyeSubTabWidths, _eyeSubTab);
            }
            if (presetTab)
            {
                ShowPresets(_host.Presets, _presetState, presetsBefore);
            }
            else if (eyeTab && _eyeSubTab == EyePresetSubTab)
            {
                ShowPresets(_host.EyePresets, _eyePresetState, presetsBefore);
            }

            view.DrawHorizontalLine(Color.gray);
            view.AddSpace(ScrollTopSpace);
            view.BeginScrollView();
            if (presetTab)
            {
                DrawPresets(view);
            }
            else if (eyeTab)
            {
                if (_eyeSubTab == EyeColorSubTab)
                {
                    DrawEyeColor(view, maid);
                }
                else if (_eyeSubTab == EyePresetSubTab)
                {
                    DrawEyePresets(view, maid);
                }
                else if (_eyeSubTab == EyeScleraSubTab)
                {
                    DrawScleraShadow(view, maid, _settings.eye, _eyeDefault);
                    DrawEyeResetButton(view);
                }
                else
                {
                    DrawEyeSettings(view);
                    DrawEyeResetButton(view);
                }
            }
            else
            {
                DrawSettings(view);
                DrawResetButton(view);
                if (_editGroup.HasValue)
                {
                    // スロットの数はメイドや着替えで変わるため、入力欄キャッシュの番号がずれないようスライダーより後に描く
                    DrawSlots(view, maid, _editGroup.Value);
                }
            }
            view.EndScrollView();

            Commit(maid);
        }

        // DrawTabs は全タブを同じ幅で描くため、短い見出しのタブまで最長の見出しの幅に広がって 1 行に収まらない
        private int DrawWrappedTabs(GUIView view, string[] labels, float[] widths, int current)
        {
            var maxWidth = view.viewRect.width - view.currentPos.x - view.padding.x;
            var rows = 1;
            var rowWidth = 0f;
            var totalWidth = 0f;
            for (var i = 0; i < labels.Length; i++)
            {
                var width = Mathf.Max(TabWidth, GUIView.CalcWidthCached(GUIView.gsButton, labels[i]));
                widths[i] = width;
                if (rowWidth > 0f && rowWidth + width > maxWidth)
                {
                    rows++;
                    rowWidth = 0f;
                }
                rowWidth += width;
                totalWidth += width;
            }

            view.BeginSubView(view.GetDrawRect(Mathf.Min(totalWidth, maxWidth), RowHeight * rows), GUIView.LayoutDirection.Horizontal);
            var sub = view.subView;
            for (var i = 0; i < labels.Length; i++)
            {
                var width = widths[i];
                if (sub.currentPos.x > 0f && sub.currentPos.x + width > sub.viewRect.width)
                {
                    sub.EndLayout();
                    sub.BeginLayout(GUIView.LayoutDirection.Horizontal);
                }
                if (sub.DrawButton(labels[i], width, RowHeight, true, i == current ? GUIView.option.accentColor : Color.white))
                {
                    current = i;
                }
            }
            view.EndSubView();
            // DrawTabs と同じ間隔
            view.AddSpace(5);
            return current;
        }

        private void SyncMaidItems(List<Maid> maids)
        {
            var current = _maidComboBox.currentItem;
            _maidComboBox.items.Clear();
            _maidComboBox.items.AddRange(maids);
            var index = current != null ? maids.IndexOf(current) : -1;
            _maidComboBox.currentIndex = index >= 0 ? index : 0;
        }

        /// <summary>共通タブは共通の値を、グループのタブはそのグループに効く項目だけを解決後の値で出す</summary>
        private void DrawSettings(GUIView view)
        {
            var shown = _editGroup.HasValue ? _settings.Resolve(_editGroup.Value) : _settings.common;
            // スライダーと色の「R」の戻し先。グループのタブでは共通の値 (個別のまま共通と同じ値になる)
            var def = _editGroup.HasValue ? _settings.common : _default;
            var prefix = TabLabels[_tab] + "/";
            // 部位専用の項目の戻し先。共通の値を持たないため、グループのタブでも既定へ戻す
            var groupOnlyDef = _default;

            DrawSection(view, "陰影");
            ColorRow(view, GroupSettingKeys.ShadeColor, true, prefix + "影色", "影色",
                "影側に掛ける色", shown.shadeColor, def.shadeColor, true, s => s.shadeColor);
            Slider(view, GroupSettingKeys.ShadeStrength, "陰影の濃さ",
                "主光源の陰影の濃さ\n0 で影が消えて光側一色、1 より上で影側をさらに濃くする\n追加ライトには効かない",
                shown.shadeStrength, 0f, GroupSettings.MaxShadeStrength, def.shadeStrength, (s, v) => s.shadeStrength = v);
            var gameShadowRate = shown.useGameShadowRate;
            Toggle(view, GroupSettingKeys.GameShadowRate, "ゲームの陰影",
                "主光源の陰影の境界をゲームのマテリアルの陰影テクスチャで決める\nOFF では法線とライトの向きから決める (境界の硬さが効く)",
                gameShadowRate, (s, v) => s.useGameShadowRate = v);
            Slider(view, GroupSettingKeys.ShadingShift, "境界のずらし",
                "陰影の境界をずらす量\nプラスで光側が広がり、マイナスで影側が広がる",
                shown.shadingShift, -1f, 1f, def.shadingShift, (s, v) => s.shadingShift = v);
            if (gameShadowRate)
            {
                // 表示名に足すとラベル幅 (110) に収まらない。DrawLabel は入力欄キャッシュを使わないため、条件つきで描いても番号はずれない
                view.DrawLabel("ON の間、境界の硬さは追加ライトと影の境界を持たない MOD にだけ効きます", -1, RowHeight);
            }
            Slider(view, GroupSettingKeys.ShadingToony, "境界の硬さ",
                "陰影の境界の硬さ\n1 でくっきり分かれ、0 でなめらかなグラデーションになる",
                shown.shadingToony, 0f, 1f, def.shadingToony, (s, v) => s.shadingToony = v);
            // 共通タブでも共通の「ゲームの陰影」で無効にすると、それを個別に ON にしたグループへ効く値を変えられない
            Toggle(view, GroupSettingKeys.FlatShadowRamp, "影側を一色に",
                "主光源の影側をゲームのランプの最も暗い色一色で塗る\nOFF では影の中もランプのグラデーションで明るさが変わる\n「ゲームの陰影」が ON のマテリアルにだけ効く",
                shown.flatShadowRamp, (s, v) => s.flatShadowRamp = v, !_editGroup.HasValue || gameShadowRate);
            Slider(view, GroupSettingKeys.GiEqualization, "環境光の均一化",
                "環境光の向きによる明るさの差をならす割合\n1 でどの向きの面も同じ明るさになる",
                shown.giEqualization, 0f, 1f, def.giEqualization, (s, v) => s.giEqualization = v);
            Slider(view, GroupSettingKeys.AdditionalLightShade, "追加ライトの影側",
                "追加ライトが影側を照らす明るさ (光側に対する割合)\n0 で影側を照らさず、1 で光側と同じ明るさ (色は影色)",
                shown.additionalLightShade, 0f, 1f, def.additionalLightShade, (s, v) => s.additionalLightShade = v);
            // 無効表示で残すと効かない項目が並んで読みにくい。行の増減で入力欄キャッシュの番号がずれるのはタブ切替時だけ
            if (Shows(GroupSettingKeys.ReceiveShadows))
            {
                Toggle(view, GroupSettingKeys.ReceiveShadows, "顔が影を受ける", "顔に他の物の落ち影を映す",
                    shown.receiveShadows, (s, v) => s.receiveShadows = v);
                Toggle(view, GroupSettingKeys.CastShadows, "顔が影を落とす", "顔の落ち影を他の物へ落とす",
                    shown.castShadows, (s, v) => s.castShadows = v);
            }
            if (Shows(GroupSettingKeys.FaceHalfShadow))
            {
                DrawFaceHalfShadowSettings(view, shown, groupOnlyDef);
            }
            if (Shows(GroupSettingKeys.HairShadow))
            {
                DrawHairShadowSettings(view, shown, groupOnlyDef);
            }
            if (Shows(GroupSettingKeys.HighlightScale))
            {
                DrawSection(view, "天使の輪");
                Slider(view, GroupSettingKeys.HighlightScale, "強さ",
                    "髪のハイライト (天使の輪) の強さ\nハイライトのテクスチャを持たない髪には効かない",
                    shown.highlightScale, 0f, 2f, groupOnlyDef.highlightScale, (s, v) => s.highlightScale = v);
            }

            DrawSection(view, "リム");
            Toggle(view, GroupSettingKeys.RimColor, "色を上書きする",
                "リムを下の色で描く\nOFF ではゲームのマテリアルのリム色を使う",
                shown.rimColorOverride, (s, v) => s.rimColorOverride = v);
            // 上書きしないときも行は隠さず無効表示にする (行の増減で入力欄キャッシュの番号がずれる)。
            // 個別のトグルは直前の行と同じキーなので出さない
            ColorRow(view, GroupSettingKeys.RimColor, false, prefix + "リム色", "リム色",
                "リムの色 (色を上書きするときだけ効く)", shown.rimColor, def.rimColor, shown.rimColorOverride, s => s.rimColor);
            Slider(view, GroupSettingKeys.RimFresnelPowerScale, "鋭さの倍率",
                "ゲームのリムの鋭さに掛ける倍率\n大きいほどリムが縁へ寄って細くなる",
                shown.rimFresnelPowerScale, 0f, 4f, def.rimFresnelPowerScale, (s, v) => s.rimFresnelPowerScale = v);
            Slider(view, GroupSettingKeys.RimLiftOffset, "持ち上げの加算",
                "ゲームのリムの持ち上げに足す量\nプラスでリムが内側まで広がる",
                shown.rimLiftOffset, -1f, 1f, def.rimLiftOffset, (s, v) => s.rimLiftOffset = v);
            Slider(view, GroupSettingKeys.RimLightingMix, "ライト色の影響",
                "リムにライトの明るさと色を掛ける割合\n0 でライトによらずリム色のまま光る",
                shown.rimLightingMix, 0f, 1f, def.rimLightingMix, (s, v) => s.rimLightingMix = v);

            DrawSection(view, "アウトライン");
            Toggle(view, GroupSettingKeys.OutlineEnabled, "アウトラインを描く",
                "輪郭線を描く\n元のシェーダーに線があるマテリアルにだけ効く",
                shown.outlineEnabled, (s, v) => s.outlineEnabled = v);
            // 既定の書式 (小数 2 桁) では 0.0008 が 0.00 に見えるため 4 桁で出す
            Slider(view, GroupSettingKeys.OutlineWidth, "幅", "線の太さ",
                shown.outlineWidth, 0f, 0.01f, def.outlineWidth, (s, v) => s.outlineWidth = v, FloatFieldType.F4);
            Slider(view, GroupSettingKeys.OutlineNearDistance, "頭打ちの距離",
                "カメラがこれより近いと画面上の線の太さを一定にする (m)\nこれより遠いと離れるほど細くなる\n大きくすると遠くの線が太くなり、0 で線が消える",
                shown.outlineNearDistance, 0f, 2f, def.outlineNearDistance, (s, v) => s.outlineNearDistance = v);
            Slider(view, GroupSettingKeys.OutlineLightingMix, "ライト色の影響",
                "線に面の色 (ライトの明るさを含む) を掛ける割合\n0 でライトによらず線の色のまま",
                shown.outlineLightingMix, 0f, 1f, def.outlineLightingMix, (s, v) => s.outlineLightingMix = v);
            Toggle(view, GroupSettingKeys.OutlineColor, "線の色を上書きする",
                "線を下の色で描く\nOFF ではゲームのマテリアルの線の色を使う",
                shown.outlineColorOverride, (s, v) => s.outlineColorOverride = v);
            ColorRow(view, GroupSettingKeys.OutlineColor, false, prefix + "線の色", "線の色",
                "線の色 (線の色を上書きするときだけ効く)", shown.outlineColor, def.outlineColor, shown.outlineColorOverride, s => s.outlineColor);
        }

        private bool Shows(string key)
        {
            return _editGroup.HasValue ? GroupSettingKeys.AppliesTo(key, _editGroup.Value) : !GroupSettingKeys.IsGroupOnly(key);
        }

        private void DrawFaceHalfShadowSettings(GUIView view, GroupSettings shown, GroupSettings def)
        {
            Toggle(view, GroupSettingKeys.FaceHalfShadow, "半面切り替えの影",
                "顔の自分の影を、面の向きではなく光の水平方向の角度で切り替える\n正面寄りの光で影なし、横からで光と反対側の半面、後ろ寄りで顔全体が影になる\n効くのは正面の範囲の面だけで、顎の下や耳は今どおり面の向きで影を付ける\n落ち影・前髪の影・追加ライトは今どおり",
                shown.faceHalfShadow, (s, v) => s.faceHalfShadow = v);
            // OFF の間に行を消すと、トグルの切り替えで入力欄キャッシュの番号がずれる
            var editable = shown.faceHalfShadow;
            Slider(view, GroupSettingKeys.FaceHalfAngle, "半面になる角度",
                "頭の正面から光までの水平の角度 (度) がこれを超えると、光と反対側の半面が影になる\n0 では光が正面をまたぐときに影の左右が一瞬で入れ替わる",
                shown.faceHalfAngle, 0f, GroupSettings.MaxFaceHalfAngle, def.faceHalfAngle, (s, v) => s.faceHalfAngle = v, editable: editable);
            Slider(view, GroupSettingKeys.FaceFullAngle, "全面になる角度",
                "頭の正面から光までの水平の角度 (度) がこれを超えると、顔全体が影になる\n半面になる角度より小さいときは半面になる角度として扱う\n180 では光が真後ろをまたぐときに影の左右が一瞬で入れ替わる",
                shown.faceFullAngle, 0f, GroupSettings.MaxFaceHalfAngle, def.faceFullAngle, (s, v) => s.faceFullAngle = v, editable: editable);
            Slider(view, GroupSettingKeys.FaceHalfFadeWidth, "フェード幅",
                "影を切り替える角度の幅 (度)\nこの幅の間で影がなめらかに濃くなり、0 で一瞬で切り替わる\n切り替わる角度から 0 や 180 までの 2 倍より広い分は効かない",
                shown.faceHalfFadeWidth, 0f, GroupSettings.MaxFaceHalfFadeWidth, def.faceHalfFadeWidth, (s, v) => s.faceHalfFadeWidth = v, editable: editable);
            // m のままでは 0.005 刻みの調整が既定の書式 (小数 2 桁) で見えない
            Slider(view, GroupSettingKeys.FaceHalfSoftness, "境目のぼかし",
                "左右の半面の境目をぼかす幅 (cm)\n0 でくっきり分かれる",
                shown.faceHalfSoftness * CentimetersPerMeter,
                0f, GroupSettings.MaxFaceHalfSoftness * CentimetersPerMeter,
                def.faceHalfSoftness * CentimetersPerMeter,
                (s, v) => s.faceHalfSoftness = v / CentimetersPerMeter, editable: editable);
            Slider(view, GroupSettingKeys.FaceFrontAngle, "正面の範囲",
                "顔の正面から面の向きまでの角度 (度) がこれ以内の面だけを半面・全面で切り替える\n外側の顎の下や耳は面の向きで影を付け、境目はなめらかにつなぐ\n180 で顔全体を半面・全面で切り替える",
                shown.faceFrontAngle, 0f, GroupSettings.MaxFaceFrontAngle, def.faceFrontAngle, (s, v) => s.faceFrontAngle = v, editable: editable);
            Slider(view, GroupSettingKeys.FaceFrontShadowStrength, "正面の影の濃さ",
                "正面の範囲の半面・全面の影の濃さ\n1 で影色まで暗くなり、0 で影が消える\n顎の下や耳の影・落ち影・前髪の影は変わらない",
                shown.faceFrontShadowStrength, 0f, GroupSettings.MaxFaceFrontShadowStrength, def.faceFrontShadowStrength, (s, v) => s.faceFrontShadowStrength = v, editable: editable);
        }

        private void DrawHairShadowSettings(GUIView view, GroupSettings shown, GroupSettings def)
        {
            // シェーダーを読めないときに行を消すと、設定ファイルの値が残っているのに項目が無い理由がわからない
            var editable = _host.IsHairShadowAvailable;
            DrawSection(view, "前髪の影");
            if (!editable)
            {
                view.DrawLabel("前髪の影のシェーダーを読めないため使えません (ログを確認してください)", -1, RowHeight);
            }
            Slider(view, GroupSettingKeys.HairShadow, "強さ", "前髪が顔へ落とす影の濃さ\n0 で影を作らない",
                shown.hairShadow, 0f, 1f, def.hairShadow, (s, v) => s.hairShadow = v, editable: editable);
            Toggle(view, GroupSettingKeys.HairShadowMode, "ライトの向きに合わせる",
                "主光源の向きから影を落とす\nOFF では頭の正面から落とし、ライトの向きによらない",
                shown.hairShadowMode == HairShadowMode.Light, (s, v) => s.hairShadowMode = v ? HairShadowMode.Light : HairShadowMode.Fixed, editable);
            // 既定の書式 (小数 2 桁) では 0.005 m 刻みの調整が見えないため 4 桁で出す
            Slider(view, GroupSettingKeys.HairShadowOffsetX, "左右", "影を左右へずらす量 (m)\nメイドから見て右がプラス",
                shown.hairShadowOffsetX, -GroupSettings.MaxHairShadowOffset, GroupSettings.MaxHairShadowOffset, def.hairShadowOffsetX,
                (s, v) => s.hairShadowOffsetX = v, FloatFieldType.F4, editable);
            Slider(view, GroupSettingKeys.HairShadowOffsetY, "上下", "影を上下へずらす量 (m)\n頭の上がプラス",
                shown.hairShadowOffsetY, -GroupSettings.MaxHairShadowOffset, GroupSettings.MaxHairShadowOffset, def.hairShadowOffsetY,
                (s, v) => s.hairShadowOffsetY = v, FloatFieldType.F4, editable);
            ChoiceRow(view, GroupSettingKeys.HairShadowResolution, "解像度", "影を描く画像の一辺の画素数\n大きいほど影の縁が細かくなり、重くなる",
                ResolutionLabels, Array.IndexOf(HairShadowProjection.Resolutions, HairShadowProjection.ClampResolution(shown.hairShadowResolution)),
                (s, i) => s.hairShadowResolution = HairShadowProjection.Resolutions[i], editable);
            Slider(view, GroupSettingKeys.HairShadowSoftness, "ぼかし", "影の縁をぼかす幅 (影を描く画像の画素数)",
                shown.hairShadowSoftness, 0f, GroupSettings.MaxHairShadowSoftness, def.hairShadowSoftness, (s, v) => s.hairShadowSoftness = v, editable: editable);
        }

        private void DrawResetButton(GUIView view)
        {
            if (!_editGroup.HasValue)
            {
                if (view.DrawButton("共通の調整値を既定に戻す", ResetButtonWidth, RowHeight))
                {
                    _settings.common = GroupSettings.CreateDefault();
                    _groupChanged = true;
                }
                return;
            }
            if (view.DrawButton("このグループを全て共通に戻す", ResetButtonWidth, RowHeight))
            {
                _settings.ClearOverrides(_editGroup.Value);
                _groupChanged = true;
            }
        }

        private void DrawSlots(GUIView view, Maid maid, PartGroup group)
        {
            var settings = _settings;
            // ホストのリストをそのまま回すと、コールバックからホストが一覧を作り直したとき外側の foreach が壊れる
            var slots = _slots;
            slots.Clear();
            slots.AddRange(_host.GetSlotCategories(maid, group));
            var allActive = true;
            for (var i = 0; i < slots.Count; i++)
            {
                allActive &= settings.IsSlotActive(slots[i]);
            }
            if (DrawSectionWithButton(view, "MToon にするスロット", allActive ? "全て OFF" : "全て ON", slots.Count > 0))
            {
                settings.SetSlotsActive(slots, !allActive);
                _targetsChanged = true;
            }
            foreach (var slot in slots)
            {
                view.DrawToggle(slot, settings.IsSlotActive(slot), -1, RowHeight, v =>
                {
                    settings.SetSlotExcluded(slot, !v);
                    _targetsChanged = true;
                }, "このスロットのマテリアルを MToon にする\nOFF では元のシェーダーのまま");
            }
        }

        /// <summary>瞳の行。ON/OFF やシェーダーの有無で行を増減させず無効表示にする (入力欄キャッシュの番号がずれるため)</summary>
        private void DrawEyeSettings(GUIView view)
        {
            var eye = _settings.eye;
            var def = _eyeDefault;
            var editable = _host.IsEyeGlossAvailable && eye.enabled;
            if (!_host.IsEyeGlossAvailable)
            {
                // DrawLabel は入力欄キャッシュを使わないため、条件つきで描いても番号はずれない
                view.DrawLabel("瞳のシェーダーを読めないため、瞳の項目は使えません (ログを確認してください)", -1, RowHeight);
            }
            if (_eyeSubTab == EyeShadeSubTab)
            {
                DrawEyeShade(view, eye, def, editable);
            }
            else if (_eyeSubTab == EyeHighlightSubTab)
            {
                DrawEyeHighlights(view, eye, def, editable);
            }
        }

        private void DrawEyeShade(GUIView view, EyeSettings eye, EyeSettings def, bool editable)
        {
            DrawSection(view, "瞳全体");
            EyeSlider(view, "ライトの影響", "瞳にライトの明るさを掛ける割合\n0 でライトによらずテクスチャの色のまま",
                eye.irisLightInfluence, 0f, 1f, def.irisLightInfluence, v => eye.irisLightInfluence = v, editable);

            DrawSection(view, "上まぶたの影");
            EyeSlider(view, "強さ", "瞳の上側を影の色で暗くする濃さ",
                eye.upperShadow, 0f, 1f, def.upperShadow, v => eye.upperShadow = v, editable);
            EyeColorRow(view, "瞳/上まぶたの影の色", "色", "瞳の上側に掛ける影の色",
                eye.upperShadowColor, def.upperShadowColor, editable);
            EyeSlider(view, "高さ", "影が瞳の上端から届く範囲 (瞳の高さに対する割合)",
                eye.upperShadowHeight, 0f, 1f, def.upperShadowHeight, v => eye.upperShadowHeight = v, editable);
            EyeSlider(view, "ぼかし", "影の下端をぼかす幅 (影の高さに対する割合)",
                eye.upperShadowSoftness, 0f, 1f, def.upperShadowSoftness, v => eye.upperShadowSoftness = v, editable);

            DrawSection(view, "下側の明るさ");
            EyeSlider(view, "強さ", "瞳の下側を明るくする強さ",
                eye.lowerGlow, 0f, 1f, def.lowerGlow, v => eye.lowerGlow = v, editable);
            EyeColorRow(view, "瞳/下側の明るさの色", "色", "瞳の下側を明るくする色 (スクリーンで重ねる)",
                eye.lowerGlowColor, def.lowerGlowColor, editable);
            EyeSlider(view, "高さ", "明るさが瞳の下端から届く範囲 (瞳の高さに対する割合)",
                eye.lowerGlowHeight, 0f, 1f, def.lowerGlowHeight, v => eye.lowerGlowHeight = v, editable);
            EyeSlider(view, "ぼかし", "明るさの上端をぼかす幅 (明るさの高さに対する割合)",
                eye.lowerGlowSoftness, 0f, 1f, def.lowerGlowSoftness, v => eye.lowerGlowSoftness = v, editable);
        }

        /// <summary>白目の影は瞳のシェーダーとは別の仕組み (MToon10) なので、瞳の光沢の ON/OFF やシェーダーの有無に関係なく編集できる</summary>
        private void DrawScleraShadow(GUIView view, Maid maid, EyeSettings eye, EyeSettings def)
        {
            DrawSection(view, "白目の影");
            if (eye.scleraShadowEnabled && !_host.IsScleraFound(maid))
            {
                view.DrawLabel("白目が見つかりません (顔を MToon にしていないか、対応していない顔です)", -1, RowHeight);
            }
            EyeToggle(view, "白目の影を描く", "テクスチャに描かれた白目の影の代わりに、下の設定の影を描く\nOFF ではテクスチャの描き込みのまま",
                eye.scleraShadowEnabled, v => eye.scleraShadowEnabled = v, true);
            var editable = eye.scleraShadowEnabled;
            EyeSlider(view, "強さ", "白目の上側を影の色で暗くする濃さ",
                eye.scleraShadow, 0f, 1f, def.scleraShadow, v => eye.scleraShadow = v, editable);
            EyeColorRow(view, "瞳/白目の影の色", "色", "白目の上側に掛ける影の色 (白目の色に乗算する)",
                eye.scleraShadowColor, def.scleraShadowColor, editable);
            EyeSlider(view, "高さ", "影が白目の上端から届く範囲 (白目の高さに対する割合)",
                eye.scleraShadowHeight, 0f, 1f, def.scleraShadowHeight, v => eye.scleraShadowHeight = v, editable);
            EyeSlider(view, "ぼかし", "影の下端をぼかす幅 (影の高さに対する割合)",
                eye.scleraShadowSoftness, 0f, 1f, def.scleraShadowSoftness, v => eye.scleraShadowSoftness = v, editable);
            EyeSlider(view, "反り", "白目の左右の端ほど影の下端を下げる量 (白目の高さに対する割合)",
                eye.scleraShadowBend, 0f, 1f, def.scleraShadowBend, v => eye.scleraShadowBend = v, editable);
            EyeSlider(view, "まぶたへの追従", "まぶたを閉じるほど影を下へずらす割合",
                eye.scleraLidFollow, 0f, 1f, def.scleraLidFollow, v => eye.scleraLidFollow = v, editable);
            EyeToggle(view, "白目の色を指定する", "白目の色を下の色にする\nOFF ではテクスチャの白目の白い部分の色",
                eye.scleraColorOverride, v => eye.scleraColorOverride = v, editable);
            EyeColorRow(view, "瞳/白目の色", "白目の色", "白目の地の色",
                eye.scleraColor, def.scleraColor, editable && eye.scleraColorOverride);
        }

        private void DrawEyeHighlights(GUIView view, EyeSettings eye, EyeSettings def, bool editable)
        {
            EyeToggle(view, "eye_hi を消す", "ゲームの瞳のハイライト (eye_hi) を表示しない\n下のハイライトだけで描きたいときに使う",
                eye.hideEyeHighlight, v => eye.hideEyeHighlight = v, editable);

            DrawSection(view, "ハイライト");
            // 番号の行より前で寄せる (範囲外のまま [削除] を押すと RemoveAt が例外になる)
            _highlightIndex = HighlightSelection.Clamp(_highlightIndex, eye.highlights.Count);
            DrawHighlightTabs(view, eye, editable);
            // 0 個のときも行は描く (入力欄キャッシュの番号がずれるため)。値は新規の既定を無効表示で見せる
            var hasHighlight = eye.highlights.Count > 0;
            var h = hasHighlight ? eye.highlights[_highlightIndex] : _emptyHighlight;
            var hd = _newHighlight;
            var hEditable = editable && hasHighlight;
            EyeSlider(view, "強さ", "このハイライトの濃さ",
                h.strength, 0f, 1f, hd.strength, v => h.strength = v, hEditable);
            // 番号ごとに別のキーにする (同じキーだと、ピッカーを開いたまま番号を変えたとき前の色を指したままになりうる)
            EyeColorRow(view, "瞳/ハイライト" + (_highlightIndex + 1) + "の色", "色", "ハイライトの楕円の内側の色",
                h.color, hd.color, hEditable);
            PairRow(view, "位置", "瞳の中心からの位置 (瞳の幅を 1 とした値)\nX は画面の右、Y は上がプラス", "X", "Y",
                h.x, h.y, -EyeSettings.MaxHighlightOffset, EyeSettings.MaxHighlightOffset, hd.x, hd.y,
                (a, b) => { h.x = a; h.y = b; }, hEditable);
            PairRow(view, "大きさ", "楕円の横 (W) と縦 (H) の直径 (瞳の幅を 1 とした値)", "W", "H",
                h.width, h.height, 0f, EyeSettings.MaxHighlightSize, hd.width, hd.height,
                (a, b) => { h.width = a; h.height = b; }, hEditable);
            EyeSlider(view, "ロール", "楕円の傾き (度)\n画面上で反時計回りがプラス",
                h.roll, -EyeSettings.MaxHighlightRoll, EyeSettings.MaxHighlightRoll, hd.roll, v => h.roll = v, hEditable);
            EyeColorRow(view, "瞳/ハイライト" + (_highlightIndex + 1) + "のにじみの色", "にじみの色",
                "楕円の縁から外へにじむ光の色\nアルファで不透明度を決める", h.glowColor, hd.glowColor, hEditable, true);
            EyeSlider(view, "にじみの強さ", "にじみの濃さの倍率 (1 で等倍)",
                h.glowStrength, 0f, EyeSettings.MaxGlowStrength, hd.glowStrength, v => h.glowStrength = v, hEditable);
            PairRow(view, "にじみの幅", "楕円の縁から外へにじませる幅\nW は横、H は縦の向き (瞳の幅を 1 とした値)", "W", "H",
                h.glowWidth, h.glowHeight, 0f, EyeSettings.MaxGlowWidth, hd.glowWidth, hd.glowHeight,
                (a, b) => { h.glowWidth = a; h.glowHeight = b; }, hEditable);

            DrawSection(view, "ハイライト全体");
            EyeSlider(view, "ライトの影響", "ハイライトに主光源と環境光の明るさを掛ける割合\n0 でライトによらず設定した色のまま",
                eye.highlightLightInfluence, 0f, 1f, def.highlightLightInfluence, v => eye.highlightLightInfluence = v, editable);

            DrawSection(view, "ハイライトの動き");
            EyeSlider(view, "視差", "視線の向きに合わせてハイライトを瞳の中でずらす量\nプラスとマイナスでずれる向きが逆になる",
                eye.parallax, -EyeSettings.MaxParallax, EyeSettings.MaxParallax, def.parallax, v => eye.parallax = v, editable);
            EyeSlider(view, "まぶたで中央へ", "まぶたを閉じるほどハイライトを中央の高さへ寄せる割合",
                eye.lidFollow, 0f, 1f, def.lidFollow, v => eye.lidFollow = v, editable);
            EyeSlider(view, "中央の高さ", "まぶたを閉じたときハイライトを寄せる先の高さ\n瞳の中心が 0 で上がプラス (瞳の幅を 1 とした値)",
                eye.lidCenter, -EyeSettings.MaxHighlightOffset, EyeSettings.MaxHighlightOffset, def.lidCenter, v => eye.lidCenter = v, editable);
        }

        /// <summary>
        /// ゲームの瞳のパーツカラー。ためた書き込みが無ければ毎回メイドの今の値を読み直す
        /// (メイドの切り替えやエディットでの変更に追従するため)。書き込みは Update で行う
        /// </summary>
        private void DrawEyeColor(GUIView view, Maid maid)
        {
            var pending = _eyeColorWritePending || _eyeColorSidePending || _eyeColorRestorePending;
            if (pending && _eyeColorMaid != maid)
            {
                // 前のメイドの書き込みが Update で済むまで描かない (ためた値を新しいメイドへ書かないため。1 フレームで終わる)
                return;
            }
            // 右へ切り替える処理 (ResolveAndStoreSide) より前に取る。後だと控えの左右の選択が右になる
            var baseline = _eyeColorBaselines.GetOrCapture(maid.status.guid, () => MaidEyeColor.ReadSnapshot(maid));
            var eye = _settings.eye;
            var editable = eye.colorEnabled;
            if (!pending)
            {
                _eyeColorMaid = maid;
                // OFF でも右への切り替えを保存すると、表示しただけでエディットの左右の選択が変わる
                _eyeColorSide = editable
                    ? MaidEyeColor.ResolveAndStoreSide(maid)
                    : EyeColorSides.ResolveSide(MaidEyeColor.GetSide(maid),
                        MaidEyeColor.Read(maid, EyeColorSide.Left), MaidEyeColor.Read(maid, EyeColorSide.Right));
                _eyeColorValues = MaidEyeColor.Read(maid, _eyeColorSide);
            }
            EyeToggle(view, "瞳の色を変更する", "下の値でゲームの瞳の色を変える\nプリセットにも保存し、ON で保存した瞳のプリセットだけが適用で色も変える\nOFF では瞳の色に触らない",
                eye.colorEnabled, v => eye.colorEnabled = v, true);
            view.SetEnabled(_baseEnabled && editable);
            view.BeginHorizontal();
            view.DrawLabel("変える目", LabelWidth, RowHeight,
                tooltip: "色を変える目\n左右は両目を同じ色にし、左・右はその目だけを変える\n左右へ切り替えると左の色を右へ写す (右の色は失われる)");
            var selected = view.DrawTabs((IList<string>)EyeColorSideLabels, (int)_eyeColorSide, ChoiceWidth, RowHeight);
            view.EndLayout();
            view.SetEnabled(_baseEnabled);
            if (selected != (int)_eyeColorSide)
            {
                _eyeColorSide = (EyeColorSide)selected;
                _eyeColorSidePending = true;
                _eyeColorValues = MaidEyeColor.Read(maid, _eyeColorSide);
            }

            if (_eyeColorShownMaid != maid || _eyeColorShownSide != _eyeColorSide)
            {
                // メイドや左右をキーに含めるとピッカーの題に出て読めない。閉じずにおくと前の対象の色を指したままになる
                CloseEyeColorPickers();
                _eyeColorShownMaid = maid;
                _eyeColorShownSide = _eyeColorSide;
            }

            var b = baseline.ValuesFor(_eyeColorSide);
            DrawSection(view, "メイン");
            EyePartsColorRow(view, EyeMainColorKey, "瞳の色 (ゲームのパーツカラー)\nテクスチャの中間の明るさの部分に出る色で選ぶ",
                _eyeMainPicked, _eyeColorValues.mainHue, _eyeColorValues.mainChroma, _eyeColorValues.mainBrightness,
                b.mainHue, b.mainChroma, b.mainBrightness,
                (h, s, v) => { _eyeColorValues.mainHue = h; _eyeColorValues.mainChroma = s; _eyeColorValues.mainBrightness = v; }, editable);
            EyeColorSlider(view, "コントラスト", "瞳の色の明暗の差 (ゲームのパーツカラー)",
                _eyeColorValues.mainContrast, EyeColorValues.MaxContrast, b.mainContrast, v => _eyeColorValues.mainContrast = v, editable);
            DrawSection(view, "影");
            EyeColorSlider(view, "割合", "瞳の影の色を混ぜる割合 (ゲームのパーツカラー)",
                _eyeColorValues.shadowRate, EyeColorValues.MaxShadowRate, b.shadowRate, v => _eyeColorValues.shadowRate = v, editable);
            EyePartsColorRow(view, EyeShadowColorKey, "瞳の影の色 (ゲームのパーツカラー)\nテクスチャの中間の明るさの部分に出る色で選ぶ",
                _eyeShadowPicked, _eyeColorValues.shadowHue, _eyeColorValues.shadowChroma, _eyeColorValues.shadowBrightness,
                b.shadowHue, b.shadowChroma, b.shadowBrightness,
                (h, s, v) => { _eyeColorValues.shadowHue = h; _eyeColorValues.shadowChroma = s; _eyeColorValues.shadowBrightness = v; }, editable);
            EyeColorSlider(view, "コントラスト", "瞳の影の色の明暗の差 (ゲームのパーツカラー)",
                _eyeColorValues.shadowContrast, EyeColorValues.MaxContrast, b.shadowContrast, v => _eyeColorValues.shadowContrast = v, editable);

            view.SetEnabled(_baseEnabled && editable);
            if (view.DrawButton("瞳の色を編集前に戻す", ResetButtonWidth, RowHeight))
            {
                RequestEyeColorRestore(baseline);
            }
            view.SetEnabled(_baseEnabled);
        }

        /// <summary>FlushEyeColor は戻す書き込みを他の書き込みより優先する</summary>
        private void RequestEyeColorRestore(EyeColorSnapshot baseline)
        {
            _eyeColorRestore = baseline;
            _eyeColorRestorePending = true;
            // 戻す値と食い違う書き込みを後から重ねない
            _eyeColorWritePending = false;
            _eyeColorSidePending = false;
            _eyeColorSide = baseline.side;
            _eyeColorValues = baseline.ValuesFor(baseline.side);
            // 左右の選択が変わらないと描画側では閉じず、開いたピッカーが戻す前の色を持ち続ける
            CloseEyeColorPickers();
        }

        private void CloseEyeColorPickers()
        {
            var picker = ColorPickerWindow.instance;
            if (picker.IsEditing(EyeMainColorKey) || picker.IsEditing(EyeShadowColorKey))
            {
                picker.Close();
            }
            _eyeMainPicked.Forget();
            _eyeShadowPicked.Forget();
        }

        /// <summary>
        /// 整数のスライダー。R は編集前の値へ戻す。
        /// 値はゲームのセーブに入るだけなので、_eyeChanged (プラグインの設定の保存) は立てない
        /// </summary>
        private void EyeColorSlider(GUIView view, string label, string tooltip, int value, int max, int defaultValue, Action<int> set,
            bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = LabelWidth,
                width = -1,
                fieldType = FloatFieldType.Int,
                snapStep = 1f,
                min = 0f,
                max = max,
                defaultValue = defaultValue,
                value = value,
                onChanged = v =>
                {
                    set(Mathf.RoundToInt(v));
                    _eyeColorWritePending = true;
                },
                tooltip = tooltip,
            });
        }

        /// <summary>パーツカラーの色相・彩度・明度を、瞳のテクスチャの明るさ 128 の位置に出る色 1 つで編集する。R は編集前の値へ戻す</summary>
        private void EyePartsColorRow(GUIView view, string colorKey, string tooltip, EyePickedColor picked, int hue, int chroma, int brightness,
            int defaultHue, int defaultChroma, int defaultBrightness, Action<int, int, int> set, bool editable)
        {
            var shown = ToColor(picked.Shown(hue, chroma, brightness));
            var reset = ToColor(EyeColorHsl.ToColor(defaultHue, defaultChroma, defaultBrightness));
            view.SetEnabled(_baseEnabled && editable);
            view.DrawColor(view.GetColorFieldCache(colorKey, false), shown, reset, c =>
            {
                if (c == reset)
                {
                    // 色から求め直すと、色で表せない明度 (127 未満・382 超え) が控えに戻らない
                    picked.Forget();
                    set(defaultHue, defaultChroma, defaultBrightness);
                    _eyeColorWritePending = true;
                    return;
                }
                int h = hue, s = chroma, b = brightness;
                var raw = new ColorData(c.r, c.g, c.b, 1f);
                EyeColorHsl.FromColor(raw, ref h, ref s, ref b);
                picked.Remember(raw, h, s, b);
                set(h, s, b);
                _eyeColorWritePending = true;
            }, "色", tooltip);
        }

        private void DrawEyeResetButton(GUIView view)
        {
            if (view.DrawButton("瞳の調整値を既定に戻す", ResetButtonWidth, RowHeight))
            {
                // ON/OFF は調整値ではないため残す
                var enabled = _settings.eye.enabled;
                var scleraEnabled = _settings.eye.scleraShadowEnabled;
                var colorEnabled = _settings.eye.colorEnabled;
                _settings.eye = EyeSettings.CreateDefault();
                _settings.eye.enabled = enabled;
                _settings.eye.scleraShadowEnabled = scleraEnabled;
                _settings.eye.colorEnabled = colorEnabled;
                _highlightIndex = 0;
                _eyeChanged = true;
            }
        }

        private sealed class PresetListState
        {
            public string Name = "";
            public string DeleteName;
        }

        private void ShowPresets<T>(PresetStore<T> store, PresetListState state, object shownBefore) where T : class, IPresetData
        {
            if (store != shownBefore)
            {
                store.Refresh();
                state.DeleteName = null;
            }
            _shownPresets = store;
        }

        private void DrawPresets(GUIView view)
        {
            DrawPresetList(view, _host.Presets, _presetState, true,
                name => _host.Presets.Save(name, MaidPreset.From(_settings)),
                name =>
                {
                    var preset = _host.Presets.Load(name);
                    if (preset == null)
                    {
                        return false;
                    }
                    _settings.ApplyPreset(preset);
                    _groupChanged = true;
                    return true;
                });
        }

        private void DrawEyePresets(GUIView view, Maid maid)
        {
            // 保存すると Update で書く前の色を読み、適用すると Update で書く前の色に上書きされるため、書き込みが済むまで押させない
            var ready = !(_eyeColorWritePending || _eyeColorSidePending || _eyeColorRestorePending);
            DrawPresetList(view, _host.EyePresets, _eyePresetState, ready,
                name => SaveEyePreset(maid, name),
                name => ApplyEyePreset(maid, name));
        }

        /// <summary>save / apply は成功したら true を返す。成功すると名前欄にその名前を入れる</summary>
        private void DrawPresetList<T>(GUIView view, PresetStore<T> store, PresetListState state, bool ready,
            Func<string, bool> save, Func<string, bool> apply) where T : class, IPresetData
        {
            var rowWidth = view.viewRect.width - view.padding.x * 2;
            var name = PresetStore.NormalizeName(state.Name);

            view.SetEnabled(_baseEnabled);
            view.BeginHorizontal();
            view.DrawLabel("名前", PresetNameLabelWidth, RowHeight);
            view.DrawTextField(state.Name, rowWidth - PresetNameLabelWidth - DataButtonWidth * 3 - view.margin * 4, RowHeight,
                v => state.Name = v);
            if (view.DrawButton(store.Exists(name) ? "上書き" : "保存", DataButtonWidth, RowHeight, ready && name.Length > 0))
            {
                if (save(name))
                {
                    state.Name = name;
                    state.DeleteName = null;
                }
            }
            if (view.DrawButton("開く", DataButtonWidth, RowHeight))
            {
                OpenPresetFolder(store);
            }
            if (view.DrawButton("更新", DataButtonWidth, RowHeight))
            {
                store.Refresh();
                state.DeleteName = null;
            }
            view.EndLayout();

            view.DrawHorizontalLine();
            var names = store.Names;
            if (names.Count == 0)
            {
                view.DrawLabel("保存したプリセットはありません", -1, RowHeight);
                return;
            }
            var nameWidth = rowWidth - DataButtonWidth * 2 - view.margin * 2;
            for (var i = 0; i < names.Count; i++)
            {
                var presetName = names[i];
                var confirming = presetName == state.DeleteName;
                var deleted = false;
                view.BeginHorizontal();
                if (view.DrawButton(presetName, nameWidth, RowHeight))
                {
                    state.Name = presetName;
                    state.DeleteName = null;
                }
                if (view.DrawButton("適用", DataButtonWidth, RowHeight, ready))
                {
                    state.DeleteName = null;
                    if (apply(presetName))
                    {
                        state.Name = presetName;
                    }
                }
                if (view.DrawButton(confirming ? "確定" : "削除", DataButtonWidth, RowHeight, true,
                    confirming ? GUIView.option.accentColor : (Color?)null))
                {
                    if (confirming)
                    {
                        store.Delete(presetName);
                        state.DeleteName = null;
                        deleted = true;
                    }
                    else
                    {
                        state.DeleteName = presetName;
                    }
                }
                view.EndLayout();
                if (deleted)
                {
                    // Delete が一覧を読み直すため、残りは次の描画で描く
                    break;
                }
            }
        }

        private static void OpenPresetFolder<T>(PresetStore<T> store) where T : class, IPresetData
        {
            try
            {
                // EnsureFolder を省くと、未保存でフォルダが無いとき OpenDirectory が警告だけで何も開かない
                MTEUtils.OpenDirectory(store.EnsureFolder());
            }
            catch (Exception e)
            {
                MToonMaidPlugin.Log.LogWarning("プリセットのフォルダを開けませんでした: " + e.Message);
            }
        }

        private bool SaveEyePreset(Maid maid, string name)
        {
            var preset = new EyePreset
            {
                eye = _settings.eye,
                color = MaidEyeColor.ReadSnapshot(maid),
            };
            return _host.EyePresets.Save(name, preset);
        }

        private bool ApplyEyePreset(Maid maid, string name)
        {
            var preset = _host.EyePresets.Load(name);
            if (preset == null)
            {
                return false;
            }
            // ON/OFF は調整値ではないため残す
            preset.eye.enabled = _settings.eye.enabled;
            _settings.eye = preset.eye;
            _highlightIndex = 0;
            _eyeChanged = true;
            if (preset.eye.colorEnabled)
            {
                // [瞳の色] を開く前に適用すると、控えが適用後の色になり [瞳の色を編集前に戻す] で戻れない
                _eyeColorBaselines.GetOrCapture(maid.status.guid, () => MaidEyeColor.ReadSnapshot(maid));
                _eyeColorMaid = maid;
                RequestEyeColorRestore(preset.color);
            }
            return true;
        }

        private void DrawHighlightTabs(GUIView view, EyeSettings eye, bool editable)
        {
            var list = eye.highlights;
            view.SetEnabled(_baseEnabled && editable);
            view.BeginHorizontal();
            for (var i = 0; i < list.Count; i++)
            {
                if (view.DrawButton((i + 1).ToString(), DataTabWidth, RowHeight, true,
                    i == _highlightIndex ? GUIView.option.accentColor : (Color?)null))
                {
                    _highlightIndex = i;
                }
            }
            if (view.DrawButton("追加", DataButtonWidth, RowHeight, list.Count < EyeSettings.MaxHighlightCount))
            {
                list.Add(EyeSettings.CreateNewHighlight());
                _highlightIndex = HighlightSelection.AfterAdd(list.Count);
                _eyeChanged = true;
            }
            if (view.DrawButton("削除", DataButtonWidth, RowHeight, list.Count > 0))
            {
                list.RemoveAt(_highlightIndex);
                // 同じ描画の後続の行が list[_highlightIndex] を読むため、ここで寄せる
                _highlightIndex = HighlightSelection.AfterRemove(_highlightIndex, list.Count);
                _eyeChanged = true;
            }
            view.EndLayout();
            view.SetEnabled(_baseEnabled);
        }

        /// <summary>
        /// ラベル + 軸名のドラッグラベルと入力欄 ×2 + リセットの 1 行。
        /// MTEUtils の DrawVector2Row は軸名が X / Y 固定で、大きさの W / H に使えない
        /// </summary>
        private void PairRow(GUIView view, string label, string tooltip, string nameA, string nameB, float a, float b, float min, float max,
            float defaultA, float defaultB, Action<float, float> set, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            var available = view.viewRect.width - view.padding.x * 2 - LabelWidth - PairResetButtonWidth - view.margin * 4;
            var fieldWidth = Mathf.Max(available / 2f, GUIView.Vector3DragLabelWidth + GUIView.Vector3FieldMinWidth);
            view.BeginHorizontal();
            view.DrawLabel(label, LabelWidth, RowHeight, tooltip: tooltip);
            PairField(view, nameA, a, min, max, fieldWidth, v => set(v, b));
            PairField(view, nameB, b, min, max, fieldWidth, v => set(a, v));
            if (view.DrawButton("R", PairResetButtonWidth, RowHeight))
            {
                set(defaultA, defaultB);
                _eyeChanged = true;
            }
            view.EndLayout();
            view.SetEnabled(_baseEnabled);
        }

        private void PairField(GUIView view, string name, float value, float min, float max, float width, Action<float> set)
        {
            view.DrawFloatField(new GUIView.FloatFieldOption
            {
                label = name,
                labelWidth = GUIView.Vector3DragLabelWidth,
                dragSensitivity = PairDragSensitivity,
                fieldType = FloatFieldType.F3,
                value = value,
                minValue = min,
                maxValue = max,
                width = width,
                height = RowHeight,
                onChanged = v =>
                {
                    set(Mathf.Clamp(v, min, max));
                    _eyeChanged = true;
                },
            });
        }

        private void EyeSlider(GUIView view, string label, string tooltip, float value, float min, float max, float defaultValue, Action<float> set, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = LabelWidth,
                width = -1,
                min = min,
                max = max,
                defaultValue = defaultValue,
                value = value,
                onChanged = v =>
                {
                    set(v);
                    _eyeChanged = true;
                },
                tooltip = tooltip,
            });
            view.SetEnabled(_baseEnabled);
        }

        private void EyeToggle(GUIView view, string label, string tooltip, bool value, Action<bool> set, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            view.DrawToggle(label, value, -1, RowHeight, v =>
            {
                set(v);
                _eyeChanged = true;
            }, tooltip);
            view.SetEnabled(_baseEnabled);
        }

        /// <summary>colorKey はカラーピッカーの同定に使うため、他のタブの色の行と重複させない</summary>
        private void EyeColorRow(GUIView view, string colorKey, string label, string tooltip, ColorData color, ColorData defaultColor, bool editable,
            bool hasAlpha = false)
        {
            view.SetEnabled(_baseEnabled && editable);
            view.DrawColor(view.GetColorFieldCache(colorKey, hasAlpha), ToColor(color), ToColor(defaultColor), c =>
            {
                color.r = c.r;
                color.g = c.g;
                color.b = c.b;
                if (hasAlpha)
                {
                    color.a = c.a;
                }
                _eyeChanged = true;
            }, label, tooltip);
            view.SetEnabled(_baseEnabled);
        }

        private void Commit(Maid maid)
        {
            if (_enabledRequest.HasValue)
            {
                _host.SetEnabled(maid, _enabledRequest.Value);
                _enabledRequest = null;
            }
            if (_targetsChanged)
            {
                _targetsChanged = false;
                _host.NotifyTargetsChanged(maid);
            }
            if (_eyeChanged)
            {
                _eyeChanged = false;
                _host.NotifyEyeChanged(maid);
            }
            if (_groupChanged)
            {
                _groupChanged = false;
                if (_editGroup.HasValue)
                {
                    _host.NotifyGroupChanged(maid, _editGroup.Value);
                }
                else
                {
                    // 共通は個別にしていない全グループに効く
                    foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
                    {
                        _host.NotifyGroupChanged(maid, group);
                    }
                }
            }
        }

        /// <summary>
        /// 値を書く先。共通タブは共通、グループのタブはその項目を個別にしたうえでグループ
        /// (個別にするとき今の共通の値を写すので、編集は見えていた値から当たる)
        /// </summary>
        private GroupSettings BeginEdit(string key)
        {
            if (!_editGroup.HasValue)
            {
                return _settings.common;
            }
            var group = _editGroup.Value;
            _settings.SetOverride(group, key, true);
            return _settings.Get(group);
        }

        /// <summary>グループのタブでは行の先頭に個別のトグル (showToggle が false なら同じ幅の空き) を置く</summary>
        private void BeginRow(GUIView view, string key, bool showToggle)
        {
            if (!_editGroup.HasValue)
            {
                return;
            }
            view.BeginHorizontal();
            // 部位専用の項目にトグルを出すと、外したときに戻る既定の値がどのタブにも見えない
            if (!showToggle || GroupSettingKeys.IsGroupOnly(key))
            {
                view.DrawEmpty(OverrideToggleWidth, RowHeight);
                return;
            }
            var group = _editGroup.Value;
            view.DrawToggle(_settings.Get(group).IsOverridden(key), OverrideToggleWidth, RowHeight, v =>
            {
                _settings.SetOverride(group, key, v);
                _groupChanged = true;
            }, "ON でこのタブだけの値を使う\nOFF で共通タブの値を使う");
        }

        private void EndRow(GUIView view)
        {
            if (_editGroup.HasValue)
            {
                view.EndLayout();
            }
        }

        private void Slider(GUIView view, string key, string label, string tooltip, float value, float min, float max, float defaultValue,
            Action<GroupSettings, float> set, FloatFieldType fieldType = FloatFieldType.Float, bool editable = true)
        {
            view.SetEnabled(_baseEnabled && editable);
            BeginRow(view, key, true);
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = LabelWidth,
                width = -1,
                fieldType = fieldType,
                min = min,
                max = max,
                defaultValue = defaultValue,
                value = value,
                onChanged = v =>
                {
                    set(BeginEdit(key), v);
                    _groupChanged = true;
                },
                tooltip = tooltip,
            });
            EndRow(view);
            view.SetEnabled(_baseEnabled);
        }

        private void Toggle(GUIView view, string key, string label, string tooltip, bool value, Action<GroupSettings, bool> set, bool editable = true)
        {
            view.SetEnabled(_baseEnabled && editable);
            BeginRow(view, key, true);
            view.DrawToggle(label, value, -1, RowHeight, v =>
            {
                set(BeginEdit(key), v);
                _groupChanged = true;
            }, tooltip);
            EndRow(view);
            view.SetEnabled(_baseEnabled);
        }

        /// <summary>ラベルの後ろに選択肢のボタンを並べる行。index は選択中の番号</summary>
        private void ChoiceRow(GUIView view, string key, string label, string tooltip, IList<string> labels, int index,
            Action<GroupSettings, int> set, bool editable)
        {
            view.SetEnabled(_baseEnabled && editable);
            // 共通タブでは BeginRow が横並びを始めないため、ここで始める
            if (_editGroup.HasValue)
            {
                BeginRow(view, key, true);
            }
            else
            {
                view.BeginHorizontal();
            }
            view.DrawLabel(label, LabelWidth, RowHeight, tooltip: tooltip);
            var selected = view.DrawTabs(labels, index, ChoiceWidth, RowHeight);
            if (selected != index)
            {
                set(BeginEdit(key), selected);
                _groupChanged = true;
            }
            view.EndLayout();
            view.SetEnabled(_baseEnabled);
        }

        /// <summary>
        /// colorKey はカラーピッカーの同定にも使うため、タブ間で重複させない。
        /// 行の全要素 (編集ボタンを含む) を無効にするため BeginEnabled ではなく SetEnabled を使う
        /// (BeginEnabled は直後の 1 要素にしか効かない)。
        /// 書くときは BeginEdit の先の色を取り直す (共通を使う行で見えている色は共通の色のため)
        /// </summary>
        private void ColorRow(GUIView view, string key, bool showToggle, string colorKey, string label, string tooltip, ColorData color,
            ColorData defaultColor, bool editable, Func<GroupSettings, ColorData> target)
        {
            BeginRow(view, key, showToggle);
            view.SetEnabled(_baseEnabled && editable);
            view.DrawColor(view.GetColorFieldCache(colorKey, false), ToColor(color), ToColor(defaultColor), c =>
            {
                var dest = target(BeginEdit(key));
                dest.r = c.r;
                dest.g = c.g;
                dest.b = c.b;
                _groupChanged = true;
            }, label, tooltip);
            view.SetEnabled(_baseEnabled);
            EndRow(view);
        }

        /// <summary>項目のまとまりの見出し。上に間を空け、太字と下線で行と見分ける</summary>
        private void DrawSection(GUIView view, string title)
        {
            BeginSection(view);
            view.DrawLabel(title, -1, RowHeight, SectionColor, SectionStyle);
            view.DrawHorizontalLine(SectionColor);
        }

        /// <summary>右端にボタンを置いた見出し。押されたら true を返す</summary>
        private bool DrawSectionWithButton(GUIView view, string title, string buttonLabel, bool buttonEnabled)
        {
            BeginSection(view);
            var titleWidthBesideButton = view.viewRect.width - view.padding.x * 2 - SectionButtonWidth - view.margin;
            view.BeginHorizontal();
            view.DrawLabel(title, titleWidthBesideButton, RowHeight, SectionColor, SectionStyle);
            var clicked = view.DrawButton(buttonLabel, SectionButtonWidth, RowHeight, buttonEnabled);
            view.EndLayout();
            view.DrawHorizontalLine(SectionColor);
            return clicked;
        }

        private void BeginSection(GUIView view)
        {
            view.SetEnabled(_baseEnabled);
            view.AddSpace(SectionSpace);
        }

        private static GUIStyle SectionStyle
        {
            get
            {
                if (_sectionStyle == null)
                {
                    _sectionStyle = new GUIStyle(GUIView.gsLabel) { fontStyle = FontStyle.Bold };
                }
                return _sectionStyle;
            }
        }

        private static Color ToColor(ColorData c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
    }
}
