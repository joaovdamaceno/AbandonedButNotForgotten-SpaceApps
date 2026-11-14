using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Âncoras (rigs) das sequências que só a edição conhece (M14, M15, M20, M23, M27). Cada rig tem o sujeito na origem e +Z para a frente;
    /// a integração (MAR-009) o pousa no sujeito real com <see cref="Bind"/> (cavidade, rover, meteorito, mirante), então os planos
    /// funcionam com qualquer cenário sem copiar posições. Rigs são transforms de referência, não câmeras.
    /// </summary>
    public sealed class MarsCameraAnchors : MonoBehaviour
    {
        public const string RigPrefix = "mars.cin.";

        [SerializeField] List<Transform> rigs = new List<Transform>();

        CameraDirector cameraDirector;

        public IReadOnlyList<Transform> Rigs => rigs;

        public static string RigIdFor(string stepId) => RigPrefix + stepId;

        public void Initialize(CameraDirector camera)
        {
            ReleaseRigs();
            cameraDirector = camera;
            if (cameraDirector == null)
                return;

            foreach (var rig in rigs)
            {
                if (rig != null)
                    cameraDirector.RegisterRig(rig.name, rig);
            }
        }

        void OnDisable() => ReleaseRigs();

        /// <summary>Desregistra os rigs do CameraDirector (desligamento ou troca de cena).</summary>
        public void ReleaseRigs()
        {
            if (cameraDirector == null)
                return;

            foreach (var rig in rigs)
            {
                if (rig != null)
                    cameraDirector.UnregisterRig(rig.name);
            }

            cameraDirector = null;
        }

        public Transform Find(string rigId)
        {
            foreach (var rig in rigs)
            {
                if (rig != null && rig.name == rigId)
                    return rig;
            }

            return null;
        }

        /// <summary>Põe o rig no sujeito (posição e giro em torno de Y; o plano fica sempre em pé).</summary>
        public bool Bind(string rigId, Transform subject)
        {
            var rig = Find(rigId);
            if (rig == null || subject == null)
                return false;

            rig.SetPositionAndRotation(subject.position, Quaternion.Euler(0f, subject.eulerAngles.y, 0f));
            return true;
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsCameraAnchors);
            var seen = new HashSet<string>();
            foreach (var rig in rigs)
            {
                if (rig == null)
                {
                    ContractValidation.Report(errors, owner, "rig nulo.");
                    continue;
                }

                ContractValidation.RequireId(errors, owner, "rig", rig.name);
                if (!seen.Add(rig.name))
                    ContractValidation.Report(errors, owner, $"rig '{rig.name}' duplicado.");
            }

            return errors;
        }
    }
}
