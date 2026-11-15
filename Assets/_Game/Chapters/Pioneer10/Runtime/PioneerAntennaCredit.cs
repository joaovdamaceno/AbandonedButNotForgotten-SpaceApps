using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Cena 8 · a antena de 70 m aparece com a sua descrição: é o modelo 3D oficial da NASA (Deep Space Network 70-meter, NASA 3D Resources),
    /// com o crédito do arquivo. A linha fica no HUD enquanto a sequência da escuta toca.
    /// </summary>
    public sealed class PioneerAntennaCredit : MonoBehaviour
    {
        public const string Key = "PIO.UI.dsn_credit";

        [SerializeField] CinemaHudView hud;
        [SerializeField, Tooltip("Sequência em que a antena aparece (PIO_Listening).")] SequenceDefinition sequence;

        SequenceDirector director;
        Func<string, string> text;
        bool shown;

        public bool IsShown => shown;

        public void Initialize(SequenceDirector sequenceDirector, Func<string, string> textResolver)
        {
            director = sequenceDirector;
            text = textResolver;
        }

        void Update() => Tick();

        void OnDisable() => Hide();

        /// <summary>Mostra a linha enquanto a escuta toca. Testes chamam direto.</summary>
        public void Tick()
        {
            if (hud == null || director == null || sequence == null)
                return;

            if (director.IsPlaying && director.Current == sequence)
            {
                var value = text != null ? text(Key) : null;
                hud.SetModelCredit(true, string.IsNullOrEmpty(value) ? Key : value);
                shown = true;
            }
            else
            {
                Hide();
            }
        }

        public void Hide()
        {
            if (!shown)
                return;

            shown = false;
            if (hud != null)
                hud.SetModelCredit(false, string.Empty);
        }
    }
}
