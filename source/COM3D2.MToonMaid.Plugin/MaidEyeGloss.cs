using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の瞳と eye_hi の板の差し替え。MToon の控え (MaidMToonController._entries) とは別に持ち、
    /// ランプの処理に混ぜない
    /// </summary>
    public sealed class MaidEyeGloss
    {
        private sealed class Entry
        {
            public Material material;
            public EyeMaterialBackup backup;
            public TBodySkin skin;
            public int materialIndex;
            // true なら瞳、false なら eye_hi の板
            public bool isEye;
            // 差し替えた先 (瞳は EyeShader、eye_hi の板は HiddenShader か EyeHiShader)
            public Shader shader;
            // 外接矩形を求めたときの _MainTex と瞳の menu。どちらかが変わったら求め直す
            public int irisTextureId;
            public string irisEyeFile;
            // 外接矩形が見つからなかった最初のフレーム。見つかっていれば -1
            public int irisFirstMissFrame = -1;
        }

        // 回数で区切ると、走査の間隔を縮めたときに RT が描かれる前に UV 全体で固定される。
        // 上限を設けないと、全面透明のテクスチャで毎走査 GPU から読み戻す
        private const int MaxIrisMissFrames = 120;

        private readonly Maid _maid;
        private readonly EyeConverter _converter;
        private readonly Action<string> _warn;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        // 例外になったマテリアル。毎回同じ警告を出さないよう以後は飛ばす
        private readonly HashSet<int> _failed = new HashSet<int>();
        // 読み戻しの失敗は毎走査で繰り返しうるため 1 回だけ知らせる
        private bool _irisWarned;
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _removeBuffer = new List<int>();
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        private readonly List<Material> _materialBuffer = new List<Material>();

        public MaidEyeGloss(Maid maid, EyeConverter converter, Action<string> warn)
        {
            _maid = maid;
            _converter = converter;
            _warn = warn;
        }

        public void Scan(EyeSettings settings)
        {
            RemoveDestroyed();
            if (!settings.enabled)
            {
                RestoreAll();
                return;
            }
            var skin = _maid.body0.GetSlot(ToonMaterialClassifier.HeadSlot);
            if (skin == null || skin.obj == null)
            {
                return;
            }
            var eyeFile = _maid.GetProp(MPN.eye).strFileName;
            var hasRightHighlight = !string.IsNullOrEmpty(_maid.GetProp(MPN.eye_hi_r).strFileName);

            _seen.Clear();
            skin.obj.GetComponentsInChildren(true, _rendererBuffer);
            foreach (var renderer in _rendererBuffer)
            {
                GameCompat.GetSharedMaterials(renderer, _materialBuffer);
                for (var k = 0; k < _materialBuffer.Count; k++)
                {
                    var m = _materialBuffer[k];
                    if (m == null)
                    {
                        continue;
                    }
                    var id = m.GetInstanceID();
                    _seen.Add(id);
                    if (_failed.Contains(id))
                    {
                        continue;
                    }
                    try
                    {
                        ScanMaterial(m, id, skin, k, settings, eyeFile, hasRightHighlight);
                    }
                    catch (Exception e)
                    {
                        _failed.Add(id);
                        _warn("瞳のマテリアルの差し替えに失敗したため以後は飛ばします: " + m.name + " " + e.Message);
                    }
                }
            }
            // 頭の付け替えで外れたマテリアル。破棄されていなければ元へ戻す
            RestoreWhere(e => !_seen.Contains(e.material.GetInstanceID()));
        }

        private void ScanMaterial(Material m, int id, TBodySkin skin, int index, EyeSettings settings, string eyeFile, bool hasRightHighlight)
        {
            Entry entry;
            _entries.TryGetValue(id, out entry);
            var category = skin.Category;
            if (ToonMaterialClassifier.IsEyeMaterial(category, index, m.shader.name) || (entry != null && entry.isEye))
            {
                ScanEye(m, id, entry, skin, index, settings, eyeFile);
                return;
            }
            // 差し替えた後は今のシェーダーが自分のものなので、控えの元のシェーダー名で判定する
            var originalShader = entry != null ? entry.backup.shader : m.shader;
            var isHighlight = originalShader != null
                && ToonMaterialClassifier.IsEyeHighlightMaterial(category, index, hasRightHighlight, originalShader.name);
            if (!isHighlight)
            {
                if (entry != null)
                {
                    Release(id, entry);
                }
                return;
            }
            var target = settings.hideEyeHighlight ? _converter.HiddenShader : _converter.EyeHiShader;
            if (entry == null)
            {
                entry = new Entry { material = m, backup = EyeConverter.Replace(m, target), skin = skin, materialIndex = index, shader = target };
                _entries[id] = entry;
            }
            else if (m.shader != entry.shader)
            {
                // 別のシェーダーにされても差し替え直すと、他者が選んだシェーダーを毎走査で上書きし続ける
                if (!ToonMaterialClassifier.IsEyeHighlightMaterial(category, index, hasRightHighlight, m.shader.name))
                {
                    Release(id, entry);
                    return;
                }
                entry.backup = EyeConverter.Replace(m, target);
                SetEyeHiTarget(entry, target);
            }
            else if (target != entry.shader)
            {
                EyeConverter.SetShader(m, target, entry.backup.renderQueue);
                SetEyeHiTarget(entry, target);
            }
            else
            {
                return;
            }
            if (target == _converter.EyeHiShader)
            {
                EyeConverter.ApplyEyeHi(m, settings);
            }
        }

        private static void SetEyeHiTarget(Entry entry, Shader target)
        {
            // 切り替える前に控えた戻し先がもう一方の自分のシェーダーのままだと、OFF でゲームが自分のシェーダーへ戻す
            GameShaderBackup.Replace(entry.skin, entry.materialIndex, entry.shader, target);
            entry.shader = target;
        }

        private void ScanEye(Material m, int id, Entry entry, TBodySkin skin, int index, EyeSettings settings, string eyeFile)
        {
            if (entry == null)
            {
                entry = new Entry { material = m, backup = _converter.ToEye(m), skin = skin, materialIndex = index, isEye = true, shader = _converter.EyeShader };
                _entries[id] = entry;
                EyeConverter.Apply(m, settings);
            }
            else if (m.shader != _converter.EyeShader)
            {
                // 別のシェーダーにされても差し替え直すと、他者が選んだシェーダーを上書きする
                if (!ToonMaterialClassifier.IsEyeShader(m.shader.name))
                {
                    Release(id, entry);
                    return;
                }
                entry.backup = _converter.ToEye(m);
                EyeConverter.Apply(m, settings);
            }

            var texture = m.GetTexture(MToonProps.MainTex);
            var textureId = texture != null ? texture.GetInstanceID() : 0;
            if (textureId != entry.irisTextureId || eyeFile != entry.irisEyeFile)
            {
                var found = TryReadIrisRect(texture, out var rect);
                EyeConverter.SetIrisRect(m, rect);
                if (!found && entry.irisFirstMissFrame < 0)
                {
                    entry.irisFirstMissFrame = Time.frameCount;
                }
                // すぐ鍵を記録すると、まだ描かれていない無限色の RT (全面透明) のまま固定される
                if (found || Time.frameCount - entry.irisFirstMissFrame >= MaxIrisMissFrames)
                {
                    entry.irisTextureId = textureId;
                    entry.irisEyeFile = eyeFile;
                    entry.irisFirstMissFrame = -1;
                }
            }
        }

        /// <summary>読み戻しの例外を差し替えの失敗 (_failed) と分ける。混ぜると瞳が差し替えたまま走査から外れる</summary>
        private bool TryReadIrisRect(Texture texture, out IrisRect rect)
        {
            try
            {
                return _converter.TryReadIrisRect(texture, out rect);
            }
            catch (Exception e)
            {
                if (!_irisWarned)
                {
                    _irisWarned = true;
                    _warn("瞳の範囲を読み戻せないため、テクスチャ全体を瞳とみなします: " + e.Message);
                }
                rect = IrisRect.Full;
                return false;
            }
        }

        /// <summary>ウィンドウで値を変えた。差し替えの有無は次の走査で合わせる</summary>
        public void Apply(EyeSettings settings)
        {
            foreach (var e in _entries.Values)
            {
                if (e.material == null || e.material.shader != e.shader)
                {
                    continue;
                }
                if (e.isEye)
                {
                    EyeConverter.Apply(e.material, settings);
                }
                else if (e.shader == _converter.EyeHiShader)
                {
                    EyeConverter.ApplyEyeHi(e.material, settings);
                }
            }
        }

        /// <summary>カメラの描画の前に呼ぶ (フレームに 1 回)。視線をワールドで瞳へ入れる。closure はまぶたを閉じた割合</summary>
        public void UpdateGaze(float closure)
        {
            var body = _maid.body0;
            if (_entries.Count == 0 || body == null)
            {
                return;
            }
            var head = body.trsHead;
            // head.up を上にすると、頭のボーンの up は顔の正面を向いているため、ハイライトが顔の前後へ動く
            var headRight = head != null ? head.forward : Vector3.zero;
            var headUp = head != null ? -head.right : Vector3.zero;
            foreach (var e in _entries.Values)
            {
                if (e.isEye && e.material != null && e.material.shader == _converter.EyeShader)
                {
                    EyeConverter.SetGaze(e.material, ComputeGaze(body, e.materialIndex));
                    EyeConverter.SetHeadAxes(e.material, headRight, headUp);
                    EyeConverter.SetLidClosure(e.material, closure);
                }
            }
        }

        /// <summary>目のボーンが無ければ 0 (シェーダーでは視差なし)</summary>
        private static Vector3 ComputeGaze(TBody body, int materialIndex)
        {
            var left = materialIndex == ToonMaterialClassifier.LeftEyeMaterialIndex;
            var eye = left ? body.trsEyeL : body.trsEyeR;
            if (eye == null || eye.parent == null || body.trsHead == null)
            {
                return Vector3.zero;
            }
            var rest = left ? body.quaDefEyeL : body.quaDefEyeR;
            // 目のボーンの軸をそのまま使うと、外向きに傾けて置かれた分だけ視線が常に外へずれる。既定の向きからの回転だけを顔の正面に掛ける
            var delta = eye.rotation * Quaternion.Inverse(eye.parent.rotation * rest);
            return delta * body.trsHead.up;
        }

        public void RestoreAll()
        {
            foreach (var e in _entries.Values)
            {
                RestoreEntry(e);
            }
            _entries.Clear();
            _failed.Clear();
        }

        /// <summary>メイドごと破棄された。マテリアルも破棄済みなので戻さずに控えだけを捨てる</summary>
        public void Forget()
        {
            _entries.Clear();
            _failed.Clear();
        }

        private void RestoreEntry(Entry e)
        {
            if (e.material == null)
            {
                return;
            }
            // 他者が別のシェーダーにしていたら、元へ戻すとその変更を上書きする
            if (e.material.shader == e.shader)
            {
                _converter.Restore(e.material, e.backup);
            }
            // 今のシェーダーで照合すると、shader コマンドで元へ戻された直後の OFF で、ゲームの戻し先に自分のシェーダーが残る
            GameShaderBackup.Replace(e.skin, e.materialIndex, e.shader, e.backup.shader);
        }

        private void Release(int id, Entry e)
        {
            RestoreEntry(e);
            _entries.Remove(id);
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

        private void RestoreWhere(Func<Entry, bool> predicate)
        {
            _removeBuffer.Clear();
            foreach (var pair in _entries)
            {
                if (predicate(pair.Value))
                {
                    _removeBuffer.Add(pair.Key);
                }
            }
            foreach (var id in _removeBuffer)
            {
                Release(id, _entries[id]);
            }
        }
    }
}
