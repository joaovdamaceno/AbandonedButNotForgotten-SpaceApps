using System;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// O ÚNICO relógio de uma sequência: câmera, atores, legendas, eventos e término leem este tempo.
    /// Avança só com tempo ATIVO (zero na pausa da sessão) e também pode ser pausado localmente.
    /// Nunca anda para trás durante a execução; reiniciar é começar outra execução.
    /// </summary>
    public sealed class SequenceClock
    {
        public float TimeSeconds { get; private set; }
        public float DurationSeconds { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }

        public float NormalizedTime => DurationSeconds > 0f ? Math.Min(1f, TimeSeconds / DurationSeconds) : 1f;

        public void Start(float durationSeconds)
        {
            DurationSeconds = Math.Max(0f, durationSeconds);
            TimeSeconds = 0f;
            IsPaused = false;
            IsRunning = true;
        }

        /// <summary>Soma tempo ativo. Ignora valores não positivos e qualquer avanço quando parado ou pausado.</summary>
        public void Advance(float activeDeltaSeconds)
        {
            if (!IsRunning || IsPaused || !(activeDeltaSeconds > 0f) || float.IsInfinity(activeDeltaSeconds))
                return;

            TimeSeconds += activeDeltaSeconds;
        }

        /// <summary>Salto para frente (skip permitido). Pedidos para trás são ignorados.</summary>
        public void JumpForwardTo(float timeSeconds)
        {
            if (IsRunning && timeSeconds > TimeSeconds)
                TimeSeconds = timeSeconds;
        }

        public void Pause()
        {
            if (IsRunning)
                IsPaused = true;
        }

        public void Resume() => IsPaused = false;

        public void Stop()
        {
            IsRunning = false;
            IsPaused = false;
        }
    }
}
