using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Extends picture windows for natural speech without changing authored camera/actor coordinates.</summary>
    public sealed class NarrationTimeMap
    {
        readonly List<float> actual = new List<float> { 0f };
        readonly List<float> authored = new List<float> { 0f };
        public float DurationSeconds => actual[actual.Count - 1];

        public void AddWindow(float authoredEndSeconds, float baselineDurationSeconds, float speechDurationSeconds)
        {
            if (authoredEndSeconds <= authored[authored.Count - 1])
                throw new ArgumentException("Narration windows must advance the authored clock.");
            var duration = Mathf.Max(.01f, baselineDurationSeconds, speechDurationSeconds > 0 ? speechDurationSeconds + .35f : 0f);
            actual.Add(actual[actual.Count - 1] + duration); authored.Add(authoredEndSeconds);
        }
        public float ToAuthored(float actualSeconds)
        {
            for (var i = 1; i < actual.Count; i++)
                if (actualSeconds <= actual[i])
                    return Mathf.Lerp(authored[i - 1], authored[i], Mathf.InverseLerp(actual[i - 1], actual[i], actualSeconds));
            return authored[authored.Count - 1] + actualSeconds - actual[actual.Count - 1];
        }
    }
}
