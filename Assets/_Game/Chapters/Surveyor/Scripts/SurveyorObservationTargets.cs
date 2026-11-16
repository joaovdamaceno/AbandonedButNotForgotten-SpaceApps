using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    [CreateAssetMenu(menuName = "Space Apps/Surveyor/Observation targets")]
    public sealed class SurveyorObservationTargets : ScriptableObject
    {
        [Serializable]
        public sealed class Target
        {
            public string anchorId, nameKey, resultKey, subtitleKey, evidenceId;
        }

        [SerializeField] Target[] targets = Array.Empty<Target>();
        [SerializeField] float wideFovDegrees = 25.3f, detailFovDegrees = 6.43f;
        [SerializeField] float slewDegreesPerSecond = 35f, readingSeconds = 5f;
        [SerializeField] Vector2 frameHalfSize = new Vector2(.28f, .28f);
        public IReadOnlyList<Target> Targets => targets;
        public float WideFovDegrees => wideFovDegrees;
        public float DetailFovDegrees => detailFovDegrees;
        public float SlewDegreesPerSecond => slewDegreesPerSecond;
        public float ReadingSeconds => readingSeconds;
        public Vector2 FrameHalfSize => frameHalfSize;

        public static SurveyorObservationTargets CreateReference()
        {
            var value = CreateInstance<SurveyorObservationTargets>();
            value.targets = new[]
            {
                new Target { anchorId="SUR.LandingMarks", nameKey="obs.marks", resultKey="obs.marks.result", subtitleKey="SUR-S11", evidenceId="surveyor.photo.marks" },
                new Target { anchorId="SUR.CraterSlope", nameKey="obs.slope", resultKey="obs.slope.result", subtitleKey="SUR-S12", evidenceId="surveyor.photo.slope" },
                new Target { anchorId="SUR.Footpad", nameKey="obs.foot", resultKey="obs.foot.result", subtitleKey="SUR-S13", evidenceId="surveyor.photo.foot" }
            };
            return value;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            if (targets == null || targets.Length != 3) errors.Add("Observation needs exactly three targets.");
            foreach (var target in targets ?? Array.Empty<Target>())
                if (target == null || !ContractValidation.IsValidId(target.anchorId) ||
                    !ContractValidation.IsValidId(target.evidenceId) || !ids.Add(target.evidenceId) ||
                    !ContractValidation.IsValidId(target.nameKey) || !ContractValidation.IsValidId(target.resultKey) ||
                    !ContractValidation.IsValidId(target.subtitleKey)) errors.Add("Invalid observation target.");
            if (!(detailFovDegrees > 0 && detailFovDegrees < wideFovDegrees && wideFovDegrees < 90) ||
                !(slewDegreesPerSecond > 0 && readingSeconds >= 5) ||
                frameHalfSize.x <= 0 || frameHalfSize.y <= 0 || frameHalfSize.x >= .5f || frameHalfSize.y >= .5f)
                errors.Add("Invalid lens, speed, framing or reading configuration.");
            return errors;
        }
    }
}
