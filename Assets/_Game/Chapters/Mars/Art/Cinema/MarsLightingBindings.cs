using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Perfis de exposição por região (MAR-018). O <see cref="MarsSequenceConductor"/> aplica o perfil da região da etapa e restaura os
    /// valores originais ao sair, cancelar ou reiniciar: pausa e reinício recuperam a exposição. Atmosfera dinâmica (poeira) continua no
    /// <c>MarsAtmosphereController</c> (MAR-016).
    /// </summary>
    [CreateAssetMenu(fileName = "MarsLightingBindings", menuName = "Abandoned/Mars Lighting Bindings")]
    public sealed class MarsLightingBindings : ScriptableObject
    {
        public const float MinSunIntensity = 0.2f;
        public const float MaxSunIntensity = 2.5f;

        [SerializeField] List<MarsLightingProfile> profiles = new List<MarsLightingProfile>();

        public IReadOnlyList<MarsLightingProfile> Profiles => profiles;

        public void SetProfiles(IEnumerable<MarsLightingProfile> value) => profiles = new List<MarsLightingProfile>(value);

        public bool TryGet(string regionId, out MarsLightingProfile profile)
        {
            for (var i = 0; i < profiles.Count; i++)
            {
                if (profiles[i].regionId == regionId)
                {
                    profile = profiles[i];
                    return true;
                }
            }

            profile = default;
            return false;
        }

        public List<string> Validate(IEnumerable<string> requiredRegions)
        {
            var errors = new List<string>();
            const string owner = nameof(MarsLightingBindings);
            var seen = new HashSet<string>();
            foreach (var p in profiles)
            {
                ContractValidation.RequireId(errors, owner, "regionId", p.regionId);
                if (!seen.Add(p.regionId))
                    ContractValidation.Report(errors, owner, $"região '{p.regionId}' duplicada.");
                if (p.sunIntensity < MinSunIntensity || p.sunIntensity > MaxSunIntensity)
                    ContractValidation.Report(errors, owner, $"região '{p.regionId}': Sol {p.sunIntensity} fora de {MinSunIntensity}–{MaxSunIntensity} (sem apagar nem estourar).");
            }

            foreach (var region in requiredRegions)
            {
                if (!seen.Contains(region))
                    ContractValidation.Report(errors, owner, $"falta o perfil da região '{region}'.");
            }

            return errors;
        }
    }
}
