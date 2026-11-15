using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, fase 5 · Barras de cinema (letterbox) pela interface. Nos estados que são só cinema, duas barras pretas deslizam do
    /// alto e de baixo do quadro (7% da altura cada) e saem quando o estado acaba; nas duas interações (a foto e a sintonia de rádio)
    /// e no memorial da despedida (que tem botões) elas ficam recolhidas para dar espaço à interface. As barras ficam abaixo das
    /// legendas (o HUD tem ordem 12 e as legendas 20), então o texto cai sobre o preto.
    /// </summary>
    public sealed class PioneerLetterbox : MonoBehaviour
    {
        /// <summary>Altura de cada barra, como fração da altura do quadro.</summary>
        public const float BarFraction = 0.07f;
        /// <summary>Tempo para as barras entrarem ou saírem por inteiro.</summary>
        public const float SlideSeconds = 0.8f;

        /// <summary>Estados do capítulo que são só cinema. A despedida sai do cinema no memorial (<see cref="PioneerFirstsHud.EndSeconds"/>).</summary>
        public static readonly string[] CinemaStates =
        {
            "PIO.Launch", "PIO.Belt", "PIO.JupiterApproach", "PIO.Closest", "PIO.Plaque", "PIO.Outbound", "PIO.Farewell",
        };

        const string FarewellState = "PIO.Farewell";

        [SerializeField] CinemaHudView hud;

        SequenceDirector director;
        Func<string> currentState;
        IActiveTimeSource time;
        float progress;

        public float Progress => progress;

        public void Initialize(SequenceDirector sequenceDirector, Func<string> stateGetter, IActiveTimeSource activeTime)
        {
            director = sequenceDirector;
            currentState = stateGetter;
            time = activeTime;
            progress = 0f;
            Apply();
        }

        /// <summary>As barras devem estar fechadas no estado <paramref name="state"/>, <paramref name="seconds"/> depois do começo da sequência?</summary>
        public static bool ShouldShow(string state, float seconds)
        {
            if (Array.IndexOf(CinemaStates, state) < 0)
                return false;
            return state != FarewellState || seconds < PioneerFirstsHud.EndSeconds;
        }

        /// <summary>Move o progresso (0 = recolhidas, 1 = fechadas) em direção ao alvo, a <c>1/SlideSeconds</c> por segundo.</summary>
        public static float Advance(float current, bool show, float deltaSeconds) =>
            Mathf.MoveTowards(current, show ? 1f : 0f, Mathf.Max(0f, deltaSeconds) / SlideSeconds);

        void Update() => Tick(time != null ? time.ActiveDeltaSeconds : Time.unscaledDeltaTime);

        void OnDisable()
        {
            progress = 0f;
            Apply();
        }

        /// <summary>Avança as barras em <paramref name="deltaSeconds"/> pelo estado atual. Testes chamam direto.</summary>
        public void Tick(float deltaSeconds)
        {
            if (hud == null || currentState == null)
                return;

            var playing = director != null && director.IsPlaying;
            var show = playing && ShouldShow(currentState(), director.TimeSeconds);
            var next = Advance(progress, show, deltaSeconds);
            if (!Mathf.Approximately(next, progress))
            {
                progress = next;
                Apply();
            }
        }

        void Apply()
        {
            if (hud != null)
                hud.SetLetterbox(progress, BarFraction);
        }
    }
}
