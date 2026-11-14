using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Relógio ATIVO da sessão: cresce só em Playing não pausado. Dossiê, carregamento, conclusão e pausa não contam;
    /// ações e minigames contam. É a <see cref="IActiveTimeSource"/> de interação e sequências; nunca usa Time.timeScale.
    /// Executa antes dos consumidores (DefaultExecutionOrder) para que todos leiam o mesmo delta no quadro.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class SessionClock : MonoBehaviour, IActiveTimeSource
    {
        /// <summary>
        /// Maior delta aceito por quadro. Ao voltar de uma aba suspensa o navegador entrega um quadro enorme; sem limite,
        /// holds e sequências pulariam. Abaixo de 10 FPS o tempo ativo é subestimado, o que é o lado seguro.
        /// </summary>
        public const float MaxFrameDeltaSeconds = 0.1f;

        readonly HashSet<string> pauseSources = new HashSet<string>();
        double activeSeconds;

        public bool IsRunning { get; private set; }
        public bool IsPaused => pauseSources.Count > 0;
        public float ActiveSeconds => (float)activeSeconds;
        public float ActiveDeltaSeconds { get; private set; }
        public IReadOnlyCollection<string> PauseSources => pauseSources;

        void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>Entrada efetiva no capítulo. Não zera: use <see cref="ResetActive"/> para uma partida nova.</summary>
        public void StartActive() => IsRunning = true;

        public void StopActive()
        {
            IsRunning = false;
            ActiveDeltaSeconds = 0f;
        }

        /// <summary>Partida nova: zera tempo, pausas e delta, e para o relógio.</summary>
        public void ResetActive()
        {
            activeSeconds = 0d;
            ActiveDeltaSeconds = 0f;
            IsRunning = false;
            pauseSources.Clear();
        }

        /// <summary>Pausa enquanto houver ao menos uma fonte (menu, foco…). Congela já neste quadro.</summary>
        public void Pause(string source)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Informe a fonte da pausa.", nameof(source));

            pauseSources.Add(source);
            ActiveDeltaSeconds = 0f;
        }

        /// <summary>Remove todas as fontes de pausa sem mexer no tempo acumulado (ex.: ao concluir o capítulo).</summary>
        public void ClearPauses() => pauseSources.Clear();

        public void Resume(string source)
        {
            if (source != null)
                pauseSources.Remove(source);
        }

        /// <summary>Avança com tempo real não escalado; chamado por Update (testes chamam direto).</summary>
        public void Tick(float realDeltaSeconds)
        {
            if (!IsRunning || IsPaused || !(realDeltaSeconds > 0f) || float.IsInfinity(realDeltaSeconds))
            {
                ActiveDeltaSeconds = 0f;
                return;
            }

            ActiveDeltaSeconds = Math.Min(realDeltaSeconds, MaxFrameDeltaSeconds);
            activeSeconds += ActiveDeltaSeconds;
        }
    }
}
