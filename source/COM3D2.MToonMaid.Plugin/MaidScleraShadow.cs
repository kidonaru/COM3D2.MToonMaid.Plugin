using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の白目の影。MToon にした白目を含むマテリアルへ、白目の範囲と設定値を入れる。
    /// シェーダーの差し替え・復元は MaidMToonController が行い、ここでは _Sclera* と _LidClose だけを書く
    /// </summary>
    public sealed class MaidScleraShadow
    {
        private readonly Maid _maid;
        private readonly Shader _mtoonShader;
        private readonly Action<string> _warn;
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        private readonly List<Material> _materialBuffer = new List<Material>();
        private readonly List<int[]> _eyeTriangles = new List<int[]>();
        // メッシュとマテリアル番号の組ごとの範囲 (見つからなければ null)。直近 1 件だけにすると、白目の候補が複数あるとき走査のたびに頂点を読み直す
        private readonly Dictionary<long, IrisRect?> _rects = new Dictionary<long, IrisRect?>();
        private Material _material;
        private IrisRect _rect;
        // 失敗は毎走査で繰り返しうるため 1 回だけ知らせる
        private bool _warned;

        /// <summary>直前の走査で、白目のマテリアルと範囲が見つかったか。白目の影が OFF なら探さず false</summary>
        public bool IsFound { get; private set; }

        public MaidScleraShadow(Maid maid, Shader mtoonShader, Action<string> warn)
        {
            _maid = maid;
            _mtoonShader = mtoonShader;
            _warn = warn;
        }

        public void Scan(EyeSettings settings)
        {
            // OFF でも探すと、全メイドで走査のたびに顔のレンダラーとマテリアル名を読み続ける
            if (!settings.scleraShadowEnabled)
            {
                Clear();
                return;
            }
            var found = Find();
            if (found != _material)
            {
                Clear();
                _material = found;
            }
            IsFound = found != null;
            Apply(settings);
        }

        private Material Find()
        {
            var skin = _maid.body0.GetSlot(ToonMaterialClassifier.HeadSlot);
            if (skin == null || skin.obj == null)
            {
                return null;
            }
            skin.obj.GetComponentsInChildren(true, _rendererBuffer);
            foreach (var renderer in _rendererBuffer)
            {
                var smr = renderer as SkinnedMeshRenderer;
                if (smr == null || smr.sharedMesh == null)
                {
                    continue;
                }
                GameCompat.GetSharedMaterials(renderer, _materialBuffer);
                for (var k = 0; k < _materialBuffer.Count; k++)
                {
                    var m = _materialBuffer[k];
                    IrisRect rect;
                    if (m != null && m.shader == _mtoonShader && ToonMaterialClassifier.IsScleraMaterialName(m.name)
                        && TryGetRect(smr.sharedMesh, k, out rect))
                    {
                        _rect = rect;
                        return m;
                    }
                }
            }
            return null;
        }

        private bool TryGetRect(Mesh mesh, int index, out IrisRect rect)
        {
            var key = ((long)mesh.GetInstanceID() << 32) | (uint)index;
            IrisRect? cached;
            if (!_rects.TryGetValue(key, out cached))
            {
                IrisRect computed;
                try
                {
                    cached = ComputeRect(mesh, index, out computed) ? computed : (IrisRect?)null;
                }
                catch (Exception e)
                {
                    // 記録せずに投げ直すと、壊れたメッシュの頂点の写しと例外を走査のたびに繰り返し、他の候補も調べられない
                    cached = null;
                    if (!_warned)
                    {
                        _warned = true;
                        _warn("白目の範囲を求められないため白目の影を描きません: " + e.Message);
                    }
                }
                _rects[key] = cached;
            }
            rect = cached.GetValueOrDefault();
            return cached.HasValue;
        }

        private bool ComputeRect(Mesh mesh, int index, out IrisRect rect)
        {
            rect = default(IrisRect);
            const int rightEye = ToonMaterialClassifier.RightEyeMaterialIndex;
            // 白目は両目より後のサブメッシュにある
            if (index >= mesh.subMeshCount || rightEye >= mesh.subMeshCount || index <= rightEye)
            {
                return false;
            }
            var vertices = BindPoseVertices(mesh);
            var uv = mesh.uv;
            if (uv.Length != vertices.Length)
            {
                return false;
            }
            var positions = new float[vertices.Length * 3];
            var uvs = new float[uv.Length * 2];
            for (var i = 0; i < vertices.Length; i++)
            {
                positions[i * 3] = vertices[i].x;
                positions[i * 3 + 1] = vertices[i].y;
                positions[i * 3 + 2] = vertices[i].z;
                uvs[i * 2] = uv[i].x;
                uvs[i * 2 + 1] = uv[i].y;
            }
            _eyeTriangles.Clear();
            _eyeTriangles.Add(mesh.GetTriangles(ToonMaterialClassifier.LeftEyeMaterialIndex));
            _eyeTriangles.Add(mesh.GetTriangles(rightEye));
            return ScleraBounds.TryCompute(positions, uvs, _eyeTriangles, mesh.GetTriangles(index), out rect);
        }

        // mesh.vertices だけを読むと、TMorph が表情を書き込んだ後の頂点になり、走査した時の表情で範囲が変わる。
        // 2.0 の TMorph.m_mesh は private なので、同じメッシュかは頂点数で確かめる
        private Vector3[] BindPoseVertices(Mesh mesh)
        {
            var face = _maid.body0.Face;
            var original = face != null && face.morph != null ? face.morph.m_vOriVert : null;
            return original != null && original.Length == mesh.vertexCount ? original : mesh.vertices;
        }

        /// <summary>白目の影が OFF なら範囲を 0 にして、描き込みの見た目に戻す</summary>
        public void Apply(EyeSettings s)
        {
            if (_material == null)
            {
                return;
            }
            if (!s.scleraShadowEnabled)
            {
                _material.SetVector(MToonProps.ScleraRect, Vector4.zero);
                return;
            }
            _material.SetVector(MToonProps.ScleraRect, new Vector4(_rect.x, _rect.y, _rect.width, _rect.height));
            // アルファ 0 はシェーダーで「テクスチャの色を使う」の印。ON/OFF を別のプロパティにすると、色と組で入れ忘れうる
            _material.SetColor(MToonProps.ScleraColor, s.scleraColorOverride
                ? new Color(s.scleraColor.r, s.scleraColor.g, s.scleraColor.b, 1f)
                : new Color(1f, 1f, 1f, 0f));
            _material.SetFloat(MToonProps.ScleraShadow, s.scleraShadow);
            _material.SetColor(MToonProps.ScleraShadowColor, new Color(s.scleraShadowColor.r, s.scleraShadowColor.g, s.scleraShadowColor.b, 1f));
            _material.SetFloat(MToonProps.ScleraShadowHeight, s.scleraShadowHeight);
            _material.SetFloat(MToonProps.ScleraShadowSoftness, s.scleraShadowSoftness);
            _material.SetFloat(MToonProps.ScleraShadowBend, s.scleraShadowBend);
            _material.SetFloat(MToonProps.ScleraLidFollow, s.scleraLidFollow);
        }

        /// <summary>カメラの描画の前に呼ぶ (フレームに 1 回)。closure はまぶたを閉じた割合</summary>
        public void UpdateLid(float closure)
        {
            if (_material != null)
            {
                _material.SetFloat(MToonProps.EyeLidClose, closure);
            }
        }

        /// <summary>入れた範囲を消し、描き込みの見た目へ戻す</summary>
        public void Clear()
        {
            // Unity の == は破棄済みオブジェクトを null と判定する
            if (_material != null)
            {
                _material.SetVector(MToonProps.ScleraRect, Vector4.zero);
            }
            _material = null;
            IsFound = false;
        }

        /// <summary>メイドごと破棄された。マテリアルも破棄済みなので触らずに捨てる</summary>
        public void Forget()
        {
            _material = null;
            _rects.Clear();
            IsFound = false;
        }
    }
}
