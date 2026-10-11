using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace COM3D2.MToonMaid.Plugin
{
    /// <summary>
    /// メイド 1 人分の顔 (head スロット) の Renderer に、影を受ける・落とすを書き、元の値へ戻す。
    /// 元の値は Renderer ごとに最初に書く前の値
    /// </summary>
    public sealed class FaceShadowApplier
    {
        private sealed class State
        {
            public Renderer renderer;
            public bool originalReceive;
            public ShadowCastingMode originalCast;
            public bool writtenReceive;
            public ShadowCastingMode writtenCast;
        }

        private readonly Dictionary<int, State> _states = new Dictionary<int, State>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _removeBuffer = new List<int>();

        /// <summary>
        /// renderers に face の値を書く。face が null (MToon にしない・head を除外) なら全て戻す。
        /// renderers に無くなった Renderer (着替え・破棄) は戻して控えを捨てる
        /// </summary>
        public void Apply(List<Renderer> renderers, GroupSettings face)
        {
            if (face == null)
            {
                RestoreAll();
                return;
            }

            _seen.Clear();
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                var id = renderer.GetInstanceID();
                _seen.Add(id);
                State state;
                if (!_states.TryGetValue(id, out state))
                {
                    state = new State
                    {
                        renderer = renderer,
                        originalReceive = renderer.receiveShadows,
                        originalCast = renderer.shadowCastingMode,
                    };
                    _states.Add(id, state);
                }
                WriteTo(state, face);
            }

            _removeBuffer.Clear();
            foreach (var pair in _states)
            {
                if (!_seen.Contains(pair.Key))
                {
                    _removeBuffer.Add(pair.Key);
                }
            }
            foreach (var id in _removeBuffer)
            {
                Restore(_states[id]);
                _states.Remove(id);
            }
        }

        /// <summary>設定を変えたとき。控えのある Renderer へ走査を待たずに書く</summary>
        public void ApplySettings(GroupSettings face)
        {
            foreach (var state in _states.Values)
            {
                if (state.renderer != null)
                {
                    WriteTo(state, face);
                }
            }
        }

        public void RestoreAll()
        {
            foreach (var state in _states.Values)
            {
                Restore(state);
            }
            _states.Clear();
        }

        /// <summary>Maid ごと破棄されたとき。Renderer も破棄済みで戻す先が無い</summary>
        public void Forget()
        {
            _states.Clear();
        }

        private static void WriteTo(State state, GroupSettings face)
        {
            state.writtenReceive = face.receiveShadows;
            state.writtenCast = RendererShadowRules.ResolveCastMode(face.castShadows, state.originalCast,
                state.originalCast != ShadowCastingMode.Off, ShadowCastingMode.On, ShadowCastingMode.Off);
            SetReceive(state.renderer, state.writtenReceive);
            SetCast(state.renderer, state.writtenCast);
        }

        private static void Restore(State state)
        {
            var renderer = state.renderer;
            if (renderer == null)
            {
                return;
            }
            SetReceive(renderer, RendererShadowRules.ResolveRestore(renderer.receiveShadows, state.writtenReceive, state.originalReceive));
            SetCast(renderer, RendererShadowRules.ResolveRestore(renderer.shadowCastingMode, state.writtenCast, state.originalCast));
        }

        private static void SetReceive(Renderer renderer, bool value)
        {
            if (RendererShadowRules.ShouldWrite(value, renderer.receiveShadows))
            {
                renderer.receiveShadows = value;
            }
        }

        private static void SetCast(Renderer renderer, ShadowCastingMode value)
        {
            if (RendererShadowRules.ShouldWrite(value, renderer.shadowCastingMode))
            {
                renderer.shadowCastingMode = value;
            }
        }
    }
}
