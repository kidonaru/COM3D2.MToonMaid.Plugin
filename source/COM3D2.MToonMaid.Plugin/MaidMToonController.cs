using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の監視。非 busy の間に定期走査し、Toony を変換・再変換する。
    /// フックを使わず状態を揃える方式のため、作り直し直後の数フレームはバニラの見た目が出うる
    /// </summary>
    public sealed class MaidMToonController
    {
        private sealed class Entry
        {
            public Material material;
            public MaterialBackup backup;
            // ゲームの shader コマンドの戻し先を補正するため、どのスロットの何番目のマテリアルかを持つ
            public TBodySkin skin;
            public int materialIndex;
        }

        // 前髪の影の描画が例外になったら、この間隔 (フレーム) をあけてもう一度試す (着替え中の破棄済みレンダラー等の一過性の失敗で止めきらない)
        private const int HairShadowRetryFrames = 300;

        private readonly MaterialConverter _converter;
        private readonly Shader _hairShadowCaster;
        private readonly Action<string> _warn;
        private HairShadowRenderer _hairShadow;
        // 瞳のシェーダーを読めなければ null
        private readonly MaidEyeGloss _eyeGloss;
        private readonly MaidScleraShadow _sclera;
        private readonly LidClosureReader _lidClosure = new LidClosureReader();
        // このフレームより前は描かない (失敗の後の待ち)
        private int _hairShadowRetryFrame;
        // 失敗の警告は 1 回だけ出す
        private bool _hairShadowWarned;
        private readonly List<Material> _faceMaterials = new List<Material>();
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        // 変換で例外になったマテリアル。毎回同じ警告を出さないよう以後は飛ばす
        private readonly HashSet<int> _failed = new HashSet<int>();
        // 控えに無いのに MToon になっているマテリアル (renderer.materials による複製等)。OFF で戻せないため 1 回だけ知らせる
        private readonly HashSet<int> _unknownReported = new HashSet<int>();
        private readonly List<int> _removeBuffer = new List<int>();
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        // sharedMaterials は呼ぶたびに配列を作るため、再利用バッファへ受ける (2.0 版は GetSharedMaterials(List) が無く配列を作る)
        private readonly List<Material> _materialBuffer = new List<Material>();
        private readonly List<TBodySkin> _skinBuffer = new List<TBodySkin>();
        private readonly FaceShadowApplier _faceShadow = new FaceShadowApplier();
        private readonly List<Renderer> _headRenderers = new List<Renderer>();
        private static readonly int FaceHalfCenterId = Shader.PropertyToID(MToonProps.FaceHalfCenter);
        private static readonly int FaceHalfForwardId = Shader.PropertyToID(MToonProps.FaceHalfForward);
        private static readonly int FaceHalfUpId = Shader.PropertyToID(MToonProps.FaceHalfUp);
        // 顔へ頭の向きを入れたか。OFF にしたとき 1 回だけ 0 に戻すため (毎フレーム全マテリアルへ 0 を書かない)
        private bool _faceHalfWritten;

        private bool _wasBusy = true;
        private int _framesSinceScan;
        private bool _scanRequested;
        // 無限色の設定値の署名。変わったらランプを読み直す
        private int? _partsColorSignature;
        // ランプの読み直しの失敗は毎走査で繰り返しうるため 1 回だけ知らせる
        private bool _rampRefreshWarned;

        public Maid Maid { get; private set; }
        public string Guid { get; private set; }
        public int ScanInterval { get; set; }
        public int ConvertedCount => _entries.Count;
        public bool IsScleraFound => _sclera.IsFound;

        public MaidMToonController(Maid maid, string guid, MaterialConverter converter, Shader hairShadowCaster, EyeConverter eyeConverter, Action<string> warn)
        {
            Maid = maid;
            Guid = guid;
            _converter = converter;
            _hairShadowCaster = hairShadowCaster;
            _warn = warn;
            ScanInterval = 10;
            _eyeGloss = eyeConverter != null ? new MaidEyeGloss(maid, eyeConverter, warn) : null;
            _sclera = new MaidScleraShadow(maid, converter.MToonShader, warn);
        }

        public void Tick(MaidSettings settings)
        {
            if (Maid.IsBusy || !Maid.Visible || Maid.body0 == null)
            {
                _wasBusy = true;
                return;
            }
            if (!_wasBusy && !_scanRequested && ++_framesSinceScan < ScanInterval)
            {
                return;
            }
            _wasBusy = false;
            _scanRequested = false;
            _framesSinceScan = 0;
            Scan(settings);
        }

        /// <summary>対象のグループ・スロットが変わったとき、次の Tick で間隔を待たずに走査させる</summary>
        public void RequestScan()
        {
            _scanRequested = true;
        }

        /// <summary>瞳の設定を変えた。値はすぐ入れ、差し替えの有無 (ON/OFF・eye_hi) は間隔を待たずに走査で合わせる</summary>
        public void ApplyEye(EyeSettings settings)
        {
            _eyeGloss?.Apply(settings);
            _sclera.Apply(settings);
            _scanRequested = true;
        }

        /// <summary>カメラの描画の前に呼ぶ (フレームに 1 回)。目の動きに合わせて、瞳へ視線、瞳と白目の影へまぶたの閉じ具合を入れる</summary>
        public void UpdateEyeGaze()
        {
            var closure = _lidClosure.Read(Maid.body0);
            _eyeGloss?.UpdateGaze(closure);
            _sclera.UpdateLid(closure);
        }

        public void ApplyGroup(PartGroup group, GroupSettings settings)
        {
            if (ToonMaterialClassifier.UsesRendererShadow(group))
            {
                _faceShadow.ApplySettings(settings);
            }
            // 顔の設定を変えたら、失敗の後の待ちをやめてもう一度試す
            if (ToonMaterialClassifier.UsesHairShadow(group))
            {
                _hairShadowRetryFrame = 0;
            }
            foreach (var e in _entries.Values)
            {
                if (e.material != null && e.backup.group == group && e.material.shader == _converter.MToonShader)
                {
                    _converter.ApplyGroup(e.material, e.backup, settings);
                }
            }
        }

        public void RestoreAll()
        {
            ReleaseHairShadow();
            _faceShadow.RestoreAll();
            _eyeGloss?.RestoreAll();
            _sclera.Clear();
            foreach (var e in _entries.Values)
            {
                RestoreEntry(e);
            }
            _entries.Clear();
            _failed.Clear();
            _unknownReported.Clear();
        }

        private void RestoreEntry(Entry e)
        {
            if (e.material != null)
            {
                _converter.Restore(e.material, e.backup);
            }
            GameShaderBackup.Replace(e.skin, e.materialIndex, _converter.MToonShader, e.backup.shader);
        }

        private void Scan(MaidSettings settings)
        {
            RemoveDestroyed();
            RefreshRampsIfPartsColorChanged(settings);

            _headRenderers.Clear();
            GroupSettings headSettings = null;
            GameCompat.CollectSkins(Maid.body0, _skinBuffer);
            foreach (var skin in _skinBuffer)
            {
                var group = ToonMaterialClassifier.GetGroup(skin.Category);
                var active = settings.IsSlotActive(skin.Category);
                // 解決はスロットごとに 1 回でよい (マテリアルごとに呼ぶと全項目の写しを繰り返す)
                var groupSettings = active ? settings.Resolve(group) : null;
                skin.obj.GetComponentsInChildren(true, _rendererBuffer);
                if (active && skin.Category == ToonMaterialClassifier.HeadSlot && ToonMaterialClassifier.UsesRendererShadow(group))
                {
                    headSettings = groupSettings;
                    _headRenderers.AddRange(_rendererBuffer);
                }
                foreach (var renderer in _rendererBuffer)
                {
                    GameCompat.GetSharedMaterials(renderer, _materialBuffer);
                    for (var k = 0; k < _materialBuffer.Count; k++)
                    {
                        if (_materialBuffer[k] == null)
                        {
                            continue;
                        }
                        if (active)
                        {
                            ScanMaterial(_materialBuffer[k], skin, k, group, groupSettings);
                        }
                        else
                        {
                            RestoreIfConverted(_materialBuffer[k]);
                        }
                    }
                }
            }
            _faceShadow.Apply(_headRenderers, headSettings);
            _eyeGloss?.Scan(settings.eye);
            _sclera.Scan(settings.eye);
        }

        private void ScanMaterial(Material m, TBodySkin skin, int materialIndex, PartGroup group, GroupSettings groupSettings)
        {
            var id = m.GetInstanceID();
            if (_failed.Contains(id))
            {
                return;
            }

            try
            {
                Entry entry;
                if (_entries.TryGetValue(id, out entry))
                {
                    // menu の shader コマンド等で Toony へ戻された
                    if (m.shader != _converter.MToonShader && ToonMaterialClassifier.IsTarget(m.shader.name))
                    {
                        _converter.Reconvert(m, entry.backup, groupSettings);
                    }
                    // menu のテクスチャ変更は MToon のまま _ToonRamp だけを差し替える
                    else if (m.shader == _converter.MToonShader && _converter.IsRampReplaced(m, entry.backup))
                    {
                        RefreshRamp(entry, groupSettings);
                    }
                    // menu のテクスチャ変更で _ShadowRateToon の有無が変わったら、使うかどうかを決め直す
                    else if (m.shader == _converter.MToonShader && _converter.IsShadowRatePresenceChanged(m, entry.backup))
                    {
                        _converter.ApplyGroup(m, entry.backup, groupSettings);
                    }
                }
                else if (ToonMaterialClassifier.IsTarget(m.shader.name))
                {
                    var backup = _converter.Convert(m, group, groupSettings);
                    _entries[id] = new Entry { material = m, backup = backup, skin = skin, materialIndex = materialIndex };
                }
                else if (m.shader == _converter.MToonShader && _unknownReported.Add(id))
                {
                    _warn("控えに無い MToon のマテリアルがあります (OFF で戻せません): " + m.name);
                }
            }
            catch (Exception e)
            {
                _failed.Add(id);
                _warn("マテリアルの変換に失敗したため以後は飛ばします: " + m.name + " " + e.Message);
            }
        }

        private void RemoveDestroyed()
        {
            _removeBuffer.Clear();
            foreach (var pair in _entries)
            {
                // Unity の == は破棄済みオブジェクトを null と判定する
                if (pair.Value.material == null)
                {
                    _removeBuffer.Add(pair.Key);
                }
            }
            foreach (var id in _removeBuffer)
            {
                _entries.Remove(id);
            }
        }

        /// <summary>
        /// Maid ごと破棄されたとき、プラグインが作ったテクスチャだけを解放する。
        /// マテリアルは破棄済みで戻せないが、自分で作ったテクスチャは参照が切れてもネイティブ側に残る
        /// </summary>
        public void DestroyOwnedTextures()
        {
            // マテリアルは破棄済みのため外す先は無く、影マップだけが解放される
            ReleaseHairShadow();
            _faceShadow.Forget();
            _eyeGloss?.Forget();
            _sclera.Forget();
            _entries.Clear();
        }

        /// <summary>
        /// カメラの描画の前に呼ぶ (フレームに 1 回)。半面切り替えの影が ON の間、顔へ頭の中心と向きを入れる。
        /// OFF や頭が無いときは向きを 0 にし、シェーダーを N·L の影へ戻す
        /// </summary>
        public void UpdateFaceHalfShadow(GroupSettings face)
        {
            var body = Maid.body0;
            var head = body != null ? body.trsHead : null;
            var headRenderer = face.faceHalfShadow ? HeadFrame.FindHeadRenderer(body) : null;
            if (head == null || headRenderer == null)
            {
                ClearFaceHalfShadow();
                return;
            }
            CollectFaceMaterials();
            var center = HeadFrame.Center(headRenderer);
            var forward = HeadFrame.FaceForward(head);
            var up = HeadFrame.HeadTop(head);
            foreach (var m in _faceMaterials)
            {
                m.SetVector(FaceHalfCenterId, center);
                m.SetVector(FaceHalfForwardId, forward);
                m.SetVector(FaceHalfUpId, up);
            }
            _faceHalfWritten = true;
        }

        private void ClearFaceHalfShadow()
        {
            if (!_faceHalfWritten)
            {
                return;
            }
            CollectFaceMaterials();
            foreach (var m in _faceMaterials)
            {
                m.SetVector(FaceHalfForwardId, Vector4.zero);
            }
            _faceHalfWritten = false;
        }

        /// <summary>
        /// カメラの描画の前に呼ぶ (フレームに 1 回)。前髪の影を描いて顔へ入れる。
        /// 描かないときは影マップを解放し、顔の強さを 0 にする
        /// </summary>
        public void RenderHairShadow(GroupSettings face)
        {
            var waiting = Time.frameCount < _hairShadowRetryFrame;
            // 既定 (前髪の影 0) では毎フレーム全マテリアルを走査しないよう、集める前に判定する
            if (!HairShadowProjection.ShouldDraw(face.hairShadow, _hairShadowCaster != null, waiting))
            {
                ReleaseHairShadow();
                return;
            }
            CollectFaceMaterials();
            if (_faceMaterials.Count == 0)
            {
                ReleaseHairShadow();
                return;
            }
            // 着替えなどの最中はレンダラーが作り直されるため描かず、顔から影を外す (影マップは次に描くまで保持する)
            if (Maid.IsBusy)
            {
                HairShadowRenderer.Clear(_faceMaterials);
                return;
            }
            try
            {
                if (_hairShadow == null)
                {
                    _hairShadow = new HairShadowRenderer(_hairShadowCaster);
                }
                if (!_hairShadow.Render(Maid, face, _faceMaterials))
                {
                    HairShadowRenderer.Clear(_faceMaterials);
                }
            }
            catch (Exception ex)
            {
                // 毎フレーム同じ例外を出さないよう、間をあけてもう一度試す
                _hairShadowRetryFrame = Time.frameCount + HairShadowRetryFrames;
                ReleaseHairShadow();
                if (!_hairShadowWarned)
                {
                    _hairShadowWarned = true;
                    _warn("前髪の影を描けないため一時的に止めます (しばらくして、または設定を変えるともう一度試します): " + ex.Message);
                }
            }
        }

        /// <summary>影マップを解放し、MToon のままの顔から影を外す</summary>
        public void ReleaseHairShadow()
        {
            if (_hairShadow == null)
            {
                return;
            }
            CollectFaceMaterials();
            HairShadowRenderer.Clear(_faceMaterials);
            _hairShadow.Dispose();
            _hairShadow = null;
        }

        private void CollectFaceMaterials()
        {
            _faceMaterials.Clear();
            foreach (var e in _entries.Values)
            {
                if (e.material != null && e.material.shader == _converter.MToonShader
                    && ToonMaterialClassifier.UsesHairShadow(e.backup.group))
                {
                    _faceMaterials.Add(e.material);
                }
            }
        }

        /// <summary>スロットの除外で対象外になったマテリアルを元へ戻す</summary>
        private void RestoreIfConverted(Material m)
        {
            var id = m.GetInstanceID();
            Entry entry;
            if (_entries.TryGetValue(id, out entry))
            {
                RestoreEntry(entry);
                _entries.Remove(id);
            }
        }

        /// <summary>
        /// 無限色のランプは同じ RenderTexture へ描き直され、変化を知らせる値も増えない。
        /// メイドの無限色の設定値が変わったらランプを読み直し、変換済みのマテリアルへ入れ直す
        /// </summary>
        private void RefreshRampsIfPartsColorChanged(MaidSettings settings)
        {
            try
            {
                var signature = ComputePartsColorSignature();
                var changed = _partsColorSignature.HasValue && _partsColorSignature.Value != signature;
                _partsColorSignature = signature;
                if (!changed)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                WarnRampRefreshFailed(ex);
                return;
            }

            foreach (var entry in _entries.Values)
            {
                // 固定の Texture2D のランプは無限色で中身が変わらない。GPU からの同期読み出しを減らすため飛ばす
                if (entry.backup.rampIsRenderTexture)
                {
                    RefreshRamp(entry, settings.Resolve(entry.backup.group));
                }
            }
        }

        /// <summary>1 件の失敗で残りのマテリアルが古いランプのまま残らないよう、例外はマテリアルごとに止める</summary>
        private void RefreshRamp(Entry entry, GroupSettings groupSettings)
        {
            if (entry.material == null || entry.material.shader != _converter.MToonShader)
            {
                return;
            }
            try
            {
                _converter.RefreshRamp(entry.material, entry.backup);
                _converter.ApplyGroup(entry.material, entry.backup, groupSettings);
            }
            catch (Exception ex)
            {
                WarnRampRefreshFailed(ex);
            }
        }

        private void WarnRampRefreshFailed(Exception ex)
        {
            // 走査と設定の保存を止めないよう、ここで止めて 1 回だけ知らせる
            if (!_rampRefreshWarned)
            {
                _rampRefreshWarned = true;
                _warn("ランプの読み直しに失敗しました: " + ex.Message);
            }
        }

        private int ComputePartsColorSignature()
        {
            var parts = Maid.Parts;
            var hash = PartsColorSignature.Seed;
            for (var i = 0; i < (int)MaidParts.PARTS_COLOR.MAX; i++)
            {
                var c = parts.GetPartsColor((MaidParts.PARTS_COLOR)i);
                hash = PartsColorSignature.Mix(hash, c.m_bUse, c.m_nMainHue, c.m_nMainChroma, c.m_nMainBrightness, c.m_nMainContrast,
                    c.m_nShadowRate, c.m_nShadowHue, c.m_nShadowChroma, c.m_nShadowBrightness, c.m_nShadowContrast);
            }
            return hash;
        }
    }
}
