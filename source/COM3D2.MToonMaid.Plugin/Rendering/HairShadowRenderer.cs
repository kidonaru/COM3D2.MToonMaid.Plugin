using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の前髪の影マップ。前髪 (hairF) を影を落とす向きから正射影で描いて深度を書き、
    /// 顔の MToon マテリアルへ影マップと行列を入れる
    /// </summary>
    public sealed class HairShadowRenderer : IDisposable
    {
        private const int DepthBits = 16;
        // head スロットのバウンズの対角の半分に掛ける余白 (前髪の毛先が頭のバウンズからはみ出しても収まるように)
        private const float RadiusScale = 1.2f;
        private const string HairFrontSlot = "hairF";
        // シーン中のライトを取り直す間隔 (フレーム)。SceneEditor でライトを足してから効くまでの遅れになる
        private const int LightRefreshInterval = 30;

        private static Light[] _lights;
        private static int _lightsFrame;

        private static readonly int CasterTexId = Shader.PropertyToID(MToonProps.CasterTex);
        private static readonly int CasterTexStId = Shader.PropertyToID(MToonProps.CasterTexSt);
        private static readonly int CasterCutoffId = Shader.PropertyToID(MToonProps.CasterCutoff);
        private static readonly int CasterMatrixId = Shader.PropertyToID(MToonProps.CasterMatrix);
        private static readonly int HairShadowTexId = Shader.PropertyToID(MToonProps.HairShadowTex);
        private static readonly int HairShadowMatrixId = Shader.PropertyToID(MToonProps.HairShadowMatrix);
        private static readonly int HairShadowStrengthId = Shader.PropertyToID(MToonProps.HairShadowStrength);
        private static readonly int HairShadowSoftnessId = Shader.PropertyToID(MToonProps.HairShadowSoftness);
        private static readonly int MainTexId = Shader.PropertyToID(MToonProps.MainTex);
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int AlphaModeId = Shader.PropertyToID(MToonProps.AlphaMode);

        private readonly Material _casterMaterial;
        private readonly CommandBuffer _commands = new CommandBuffer { name = "MToonMaid_HairShadow" };
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        private readonly List<Material> _materialBuffer = new List<Material>();
        private readonly List<TBodySkin> _skinBuffer = new List<TBodySkin>();
        private readonly List<DirectionalLightInfo> _lightInfos = new List<DirectionalLightInfo>();
        // _lightInfos と同じ順の平行光
        private readonly List<Light> _lightCandidates = new List<Light>();
        private RenderTexture _texture;

        public HairShadowRenderer(Shader casterShader)
        {
            _casterMaterial = new Material(casterShader) { name = "MToonMaid_HairShadowCaster", hideFlags = HideFlags.DontSave };
        }

        /// <summary>描けたら true。頭・前髪・主光源が無いとき、主光源の落ち影が無い (濃さ 0) ときは false で、何も描かない</summary>
        public bool Render(Maid maid, GroupSettings face, List<Material> faceMaterials)
        {
            var body = maid.body0;
            var head = body != null ? body.trsHead : null;
            var headRenderer = HeadFrame.FindHeadRenderer(body);
            if (head == null || headRenderer == null || faceMaterials.Count == 0)
            {
                return false;
            }

            // 主光源をライト追従のときだけ探すと、固定の向きで落ち影の濃さを取れず連動しない。
            // 前髪を集めた後に判定すると、落ち影が無い間も毎フレーム前髪のレンダラーを走査する
            var light = FindMainLight(headRenderer.gameObject.layer);
            var strength = light != null
                ? HairShadowProjection.LinkedStrength(face.hairShadow, CastsShadows(light), light.shadowStrength)
                : 0f;
            if (strength <= 0f || !CollectHairRenderers(body))
            {
                return false;
            }
            var toLight = ToLight(face, head, light);

            // ワールドの AABB は頭の回転で大きさが変わり影マップの解像度が脈動するため、回転によらないローカルのバウンズから取る
            var boundsSpace = HeadFrame.BoundsSpace(headRenderer);
            var radius = headRenderer.localBounds.extents.magnitude * MaxAbs(boundsSpace.lossyScale) * RadiusScale;
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                return false;
            }
            // 頭のボーンの原点は首寄りにあるため、余白が片側で削れないようバウンズの中心を投影の中心にする
            var center = HeadFrame.Center(headRenderer);
            var projection = HairShadowProjection.Create(ToArray(center), toLight, ToArray(HeadFrame.HeadTop(head)), ToArray(HeadFrame.FaceForward(head)), radius);
            var worldToShadow = ToMatrix(projection);
            var sampleMatrix = ToMatrix(HairShadowProjection.Offset(projection, ToArray(WorldOffset(face, head))));

            DrawCasters(worldToShadow, ToMatrix(HairShadowProjection.ToClip(projection)), HairShadowProjection.ClampResolution(face.hairShadowResolution));

            var softness = ClampFinite(face.hairShadowSoftness, 0f, GroupSettings.MaxHairShadowSoftness);
            foreach (var m in faceMaterials)
            {
                m.SetTexture(HairShadowTexId, _texture);
                m.SetMatrix(HairShadowMatrixId, sampleMatrix);
                m.SetFloat(HairShadowStrengthId, strength);
                m.SetFloat(HairShadowSoftnessId, softness);
            }
            return true;
        }

        /// <summary>顔のマテリアルから影を外す (MToon のまま前髪の影だけを止めるとき)</summary>
        public static void Clear(List<Material> faceMaterials)
        {
            foreach (var m in faceMaterials)
            {
                m.SetFloat(HairShadowStrengthId, 0f);
                m.SetTexture(HairShadowTexId, null);
            }
        }

        public void Dispose()
        {
            _commands.Release();
            ReleaseTexture();
            UnityEngine.Object.Destroy(_casterMaterial);
        }

        /// <summary>顔から光へ向かう向き</summary>
        private static float[] ToLight(GroupSettings face, Transform head, Light light)
        {
            return ToArray(face.hairShadowMode == HairShadowMode.Light ? -light.transform.forward : HeadFrame.FaceForward(head));
        }

        private static bool CastsShadows(Light light)
        {
            return light.shadows != LightShadows.None
                && QualitySettings.shadows != ShadowQuality.Disable
                && QualitySettings.shadowDistance > 0f;
        }

        /// <summary>影をずらす量 (ワールドの m)。頭の傾きに追従して顔に対して同じ向きへずれる</summary>
        private static Vector3 WorldOffset(GroupSettings face, Transform head)
        {
            const float max = GroupSettings.MaxHairShadowOffset;
            return HeadFrame.MaidRight(head) * ClampFinite(face.hairShadowOffsetX, -max, max) + HeadFrame.HeadTop(head) * ClampFinite(face.hairShadowOffsetY, -max, max);
        }

        private static float ClampFinite(float value, float min, float max)
        {
            // Mathf.Clamp だけだと、手で編集した設定ファイルの NaN を通し、シェーダーへ渡す行列やぼかしが NaN になる
            return float.IsNaN(value) ? 0f : Mathf.Clamp(value, min, max);
        }

        /// <summary>前髪スロットの全サブスロットのレンダラーを集める。無ければ false</summary>
        private bool CollectHairRenderers(TBody body)
        {
            _rendererBuffer.Clear();
            if (!body.IsSlotNo(HairFrontSlot))
            {
                return false;
            }
            GameCompat.CollectSkins(body, body.GetSlotNo(HairFrontSlot), _skinBuffer);
            foreach (var skin in _skinBuffer)
            {
                // GetComponentsInChildren(list) はリストを上書きするため、サブスロットごとに足していく
                foreach (var r in skin.obj.GetComponentsInChildren<Renderer>(true))
                {
                    _rendererBuffer.Add(r);
                }
            }
            return _skinBuffer.Count > 0;
        }

        private void DrawCasters(Matrix4x4 worldToShadow, Matrix4x4 clip, int resolution)
        {
            EnsureTexture(resolution);
            _commands.Clear();
            _commands.SetRenderTarget(_texture);
            // 何も無い所は最も遠い (1) にして影にしない
            _commands.ClearRenderTarget(true, true, Color.white);
            // CommandBuffer は渡した OpenGL 流の行列をプラットフォーム向け (RT の上下・逆 Z) へ自分で直す。
            // GL.GetGPUProjectionMatrix を通すと二重に直り、影マップが上下反転する (実機で確認)
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, clip);
            _commands.SetGlobalMatrix(CasterMatrixId, worldToShadow);
            foreach (var renderer in _rendererBuffer)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                GameCompat.GetSharedMaterials(renderer, _materialBuffer);
                for (var i = 0; i < _materialBuffer.Count; i++)
                {
                    var m = _materialBuffer[i];
                    if (m == null)
                    {
                        continue;
                    }
                    var tex = m.HasProperty(MainTexId) ? m.GetTexture(MainTexId) : null;
                    var scale = tex != null ? m.GetTextureScale(MainTexId) : Vector2.one;
                    var offset = tex != null ? m.GetTextureOffset(MainTexId) : Vector2.zero;
                    _commands.SetGlobalTexture(CasterTexId, tex != null ? tex : Texture2D.whiteTexture);
                    _commands.SetGlobalVector(CasterTexStId, new Vector4(scale.x, scale.y, offset.x, offset.y));
                    _commands.SetGlobalFloat(CasterCutoffId, IsCutout(m) && m.HasProperty(CutoffId) ? m.GetFloat(CutoffId) : -1f);
                    _commands.DrawRenderer(renderer, _casterMaterial, i, 0);
                }
            }
            Graphics.ExecuteCommandBuffer(_commands);
        }

        private void EnsureTexture(int resolution)
        {
            // 解像度を変えたら作り直す。デバイスの喪失などで中身が消えたときも作り直す
            if (_texture != null && _texture.width != resolution)
            {
                ReleaseTexture();
            }
            if (_texture != null && _texture.IsCreated())
            {
                return;
            }
            if (_texture == null)
            {
                _texture = new RenderTexture(resolution, resolution, DepthBits, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                {
                    name = "MToonMaid_HairShadow",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    hideFlags = HideFlags.DontSave,
                };
            }
            _texture.Create();
        }

        private void ReleaseTexture()
        {
            if (_texture != null)
            {
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
        }

        private Light FindMainLight(int layer)
        {
            // Light.GetLights は Unity 2022 で非推奨。FindObjectsOfType は重いため、全メイドで共有して間隔をあけて取り直す
            if (_lights == null || Time.frameCount - _lightsFrame >= LightRefreshInterval)
            {
                _lights = UnityEngine.Object.FindObjectsOfType<Light>();
                _lightsFrame = Time.frameCount;
            }
            var sun = RenderSettings.sun;
            _lightInfos.Clear();
            _lightCandidates.Clear();
            foreach (var l in _lights)
            {
                // 取り直すまでの間に破棄されたライト (Unity の == は破棄済みを null と判定する)
                if (l == null || l.type != LightType.Directional)
                {
                    continue;
                }
                _lightCandidates.Add(l);
                var c = l.color;
                _lightInfos.Add(new DirectionalLightInfo
                {
                    enabled = l.isActiveAndEnabled,
                    cullingMask = l.cullingMask,
                    notImportant = l.renderMode == LightRenderMode.ForceVertex,
                    intensity = l.intensity,
                    r = c.r,
                    g = c.g,
                    b = c.b,
                    isSun = l == sun,
                });
            }
            var index = MainDirectionalLightSelector.Select(_lightInfos, layer);
            return index >= 0 ? _lightCandidates[index] : null;
        }

        private static bool IsCutout(Material m)
        {
            // MToon にした前髪は名前から判断できないため、変換時に入れた _AlphaMode を見る
            if (m.HasProperty(AlphaModeId) && m.shader.name == MToonProps.ShaderName)
            {
                return m.GetInt(AlphaModeId) == MToonProps.AlphaModeCutout;
            }
            return ToonMaterialClassifier.IsCutout(m.shader.name);
        }

        private static float MaxAbs(Vector3 v)
        {
            return Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
        }

        private static float[] ToArray(Vector3 v)
        {
            return new[] { v.x, v.y, v.z };
        }

        private static Matrix4x4 ToMatrix(float[] m)
        {
            var r = new Matrix4x4();
            for (var row = 0; row < 4; row++)
            {
                r.SetRow(row, new Vector4(m[row * 4], m[row * 4 + 1], m[row * 4 + 2], m[row * 4 + 3]));
            }
            return r;
        }
    }
}
