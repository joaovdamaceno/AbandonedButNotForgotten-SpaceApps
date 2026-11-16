using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Letreiro visível num intervalo do pouso (segundos desde o início de M04). O texto vem da chave localizada, nunca de C#.</summary>
    [Serializable]
    public struct MarsLandingLabel
    {
        [SerializeField] string textKey;
        [SerializeField] float startSeconds;
        [SerializeField] float endSeconds;
        [SerializeField] MarsLandingLabelAnchor anchor;

        public MarsLandingLabel(string textKey, float startSeconds, float endSeconds, MarsLandingLabelAnchor anchor)
        {
            this.textKey = textKey;
            this.startSeconds = startSeconds;
            this.endSeconds = endSeconds;
            this.anchor = anchor;
        }

        public string TextKey => textKey;
        public float StartSeconds => startSeconds;
        public float EndSeconds => endSeconds;
        public MarsLandingLabelAnchor Anchor => anchor;

        public bool IsVisibleAt(float timeSeconds) => timeSeconds >= startSeconds && timeSeconds < endSeconds;
    }
}
