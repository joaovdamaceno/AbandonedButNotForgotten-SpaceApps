using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Hastes articuláveis do modelo da Pioneer 10.</summary>
    public enum PioneerBoom
    {
        RtgBoomA = 0,
        RtgBoomB = 1,
        MagnetometerBoom = 2,
    }

    /// <summary>Dobradiça de uma haste: o Transform é o pivô; o eixo está no espaço do pai (Spin_Root).</summary>
    [Serializable]
    public sealed class PioneerBoomHinge
    {
        public PioneerBoom boom;
        public Transform pivot;
        [Tooltip("Eixo de rotação no espaço do Spin_Root. HIPÓTESE não verificada em referência: perpendicular à haste.")]
        public Vector3 axis = Vector3.up;
        [Min(0f), Tooltip("Ângulo máximo permitido, em graus. Limite de rig, não dado histórico.")] public float maxDegrees = 90f;
        public bool axisVerified;
    }

    /// <summary>
    /// PIO-002 · Rig do modelo da Pioneer 10 (prefab <c>PioneerActors</c>). É só hierarquia e pivôs; não decide ciência,
    /// não lê entrada e não conhece estados do capítulo.
    /// <list type="bullet">
    /// <item><c>Spin_Root</c>: fica sobre o eixo de spin (= eixo da antena, +Z). Tudo que gira é filho dele.</item>
    /// <item>Hastes (<c>RtgBoomA/B</c>, <c>MagnetometerBoom</c>) têm pivô na dobradiça. A pose do modelo (ângulo 0) é a
    /// configuração de CRUZEIRO; o roteiro só permite desdobrar hastes onde a referência de lançamento comprovar, então
    /// nenhum capítulo anima o ângulo hoje. O rig só oferece o gancho.</item>
    /// <item>Sockets: pontos nomeados que as atividades usam (mesmos nomes do palco da PIO-003). Os das hastes
    /// (<c>RtgBoomA/B</c>, <c>MagnetometerBoom</c>) são filhos da dobradiça e acompanham a articulação.</item>
    /// <item><c>Plaque</c> é nó próprio para o close (IMPORT_RULES §4).</item>
    /// </list>
    /// </summary>
    public sealed class PioneerProbeRig : MonoBehaviour
    {
        public static readonly string[] RequiredSockets =
        {
            "Reflector", "Feed", "EquipmentCompartment", "RtgBoomA", "RtgBoomB", "MagnetometerBoom", "Plaque", "Photopolarimeter",
        };

        [SerializeField, Tooltip("Raiz que gira em torno do eixo da antena (+Z local).")] Transform spinRoot;
        [SerializeField] Transform plaque;
        [SerializeField, Tooltip("Sockets nomeados. Os das hastes ficam sob a dobradiça da haste, para acompanhá-la ao articular.")]
        List<Transform> sockets = new List<Transform>();
        [SerializeField] List<PioneerBoomHinge> hinges = new List<PioneerBoomHinge>();

        Quaternion spinRest = Quaternion.identity;
        bool restCaptured;
        readonly Dictionary<PioneerBoom, Quaternion> hingeRest = new Dictionary<PioneerBoom, Quaternion>();

        public Transform SpinRoot => spinRoot;
        public Transform Plaque => plaque;
        public IReadOnlyList<PioneerBoomHinge> Hinges => hinges;

        void Awake() => CaptureRest();

        void CaptureRest()
        {
            if (restCaptured)
                return;

            restCaptured = true;
            if (spinRoot != null)
                spinRest = spinRoot.localRotation;
            foreach (var hinge in hinges)
            {
                if (hinge?.pivot != null)
                    hingeRest[hinge.boom] = hinge.pivot.localRotation;
            }
        }

        /// <summary>Ângulo de spin absoluto, em graus, em torno do eixo da antena (parte da pose de repouso).</summary>
        public void SetSpinDegrees(float degrees)
        {
            CaptureRest();
            if (spinRoot != null)
                spinRoot.localRotation = spinRest * Quaternion.AngleAxis(degrees, Vector3.forward);
        }

        /// <summary>Gira a haste em torno da dobradiça, entre 0 (cruzeiro, pose do modelo) e o limite do rig.</summary>
        public bool SetBoomDegrees(PioneerBoom boom, float degrees)
        {
            CaptureRest();
            var hinge = FindHinge(boom);
            if (hinge?.pivot == null)
                return false;

            var clamped = Mathf.Clamp(degrees, 0f, hinge.maxDegrees);
            hinge.pivot.localRotation = hingeRest[boom] * Quaternion.AngleAxis(clamped, hinge.axis.normalized);
            return true;
        }

        /// <summary>Todas as hastes na configuração de cruzeiro (pose do modelo).</summary>
        public void SetCruise()
        {
            foreach (PioneerBoom boom in Enum.GetValues(typeof(PioneerBoom)))
                SetBoomDegrees(boom, 0f);
        }

        public PioneerBoomHinge FindHinge(PioneerBoom boom)
        {
            foreach (var hinge in hinges)
            {
                if (hinge != null && hinge.boom == boom)
                    return hinge;
            }

            return null;
        }

        public bool TryGetSocket(string socketName, out Transform socket)
        {
            socket = null;
            if (string.IsNullOrEmpty(socketName))
                return false;

            foreach (var candidate in sockets)
            {
                if (candidate != null && candidate.name == socketName)
                {
                    socket = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Erros de montagem do rig. Lista vazia = conforme.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = "PioneerProbeRig";
            if (spinRoot == null)
                ContractValidation.Report(errors, owner, "Spin_Root ausente.");
            if (plaque == null)
                ContractValidation.Report(errors, owner, "nó Plaque ausente (IMPORT_RULES §4).");

            foreach (var name in RequiredSockets)
            {
                if (!TryGetSocket(name, out _))
                    ContractValidation.Report(errors, owner, $"socket '{name}' ausente.");
            }

            var seen = new HashSet<PioneerBoom>();
            foreach (var hinge in hinges)
            {
                if (hinge == null || hinge.pivot == null)
                {
                    ContractValidation.Report(errors, owner, "dobradiça sem pivô.");
                    continue;
                }

                if (!seen.Add(hinge.boom))
                    ContractValidation.Report(errors, owner, $"dobradiça '{hinge.boom}' duplicada.");
                if (hinge.axis.sqrMagnitude < 1e-6f)
                    ContractValidation.Report(errors, owner, $"dobradiça '{hinge.boom}' sem eixo.");
                if (spinRoot != null && !hinge.pivot.IsChildOf(spinRoot))
                    ContractValidation.Report(errors, owner, $"dobradiça '{hinge.boom}' fora de Spin_Root (não giraria com a sonda).");
            }

            foreach (PioneerBoom boom in Enum.GetValues(typeof(PioneerBoom)))
            {
                if (!seen.Contains(boom))
                    ContractValidation.Report(errors, owner, $"falta a dobradiça de '{boom}'.");
            }

            return errors;
        }
    }
}
