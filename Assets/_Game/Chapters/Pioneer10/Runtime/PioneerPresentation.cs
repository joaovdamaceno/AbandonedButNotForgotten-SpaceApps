using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Região 3D do palco. Só uma fica ativa por vez (custo e RAM).</summary>
    public enum PioneerStageRegion
    {
        DeepSpace = 0,
        LaunchSite = 1,
        GroundStation = 2,
    }

    /// <summary>
    /// Ponto de vista nomeado pela função. A pose é o próprio Transform; ele também é registrado como rig no
    /// <see cref="CameraDirector"/>, de modo que um <see cref="ShotDefinition"/> com <c>rigId = anchorId</c> descreve um
    /// movimento relativo a este anchor (keyframe zero = pose do anchor).
    /// </summary>
    [Serializable]
    public sealed class PioneerStageAnchor
    {
        [Tooltip("ID estável, igual ao estado (PIO.Imaging) ou estado + plano (PIO.Launch.Pad).")] public string anchorId;
        [Tooltip("Estado do capítulo a que o anchor pertence.")] public string stateId;
        [Tooltip("Função do plano, para quem monta a sequência (não é texto de jogo).")] public string function;
        [Tooltip("Pose da câmera neste anchor.")] public Transform pose;
        [Range(10f, 120f)] public float fieldOfViewDegrees = 40f;
        public PioneerStageRegion region;
        [Tooltip("Grupo de luz: direção, cor e intensidade de key e fill.")] public string lightGroupId;
        public bool showJupiter;
        public bool showAldebaranMarker;
        [Tooltip("Socket da sonda que fica voltado para o alvo ao entrar no anchor (ex.: placa para a câmera). Vazio = mantém a rotação.")]
        public string spinAlignSocket;
        [Tooltip("Alvo do alinhamento: um alvo de enquadramento (PIO.Jupiter) ou vazio = a câmera deste anchor.")]
        public string spinAlignTowards;
        [Range(0f, 1f), Tooltip("Fração da rotação visual de cruzeiro neste anchor (1 = cruzeiro).")]
        public float spinScale = 1f;
        [Tooltip("Área reservada ao quadro de UI (viewport normalizado). Largura zero = nenhum quadro.")]
        public Rect uiFrameViewport;

        public bool IsStateAnchor => anchorId == stateId;
        public bool HasUiFrame => uiFrameViewport.width > 0f && uiFrameViewport.height > 0f;
    }

    /// <summary>Iluminação de um grupo de planos: uma key sem sombra e um fill sem sombra.</summary>
    [Serializable]
    public sealed class PioneerLightGroup
    {
        public string groupId;
        [Tooltip("Rotação de mundo da key (direção em que a luz viaja).")] public Vector3 keyEulerDegrees;
        public Color keyColor = Color.white;
        [Min(0f)] public float keyIntensity = 1f;
        public Vector3 fillEulerDegrees;
        public Color fillColor = Color.white;
        [Min(0f)] public float fillIntensity = 0.1f;
    }

    /// <summary>
    /// PIO-003 · Palco único da Pioneer 10: rotação visual da sonda, região ativa, eixo de luz por grupo de planos,
    /// céu compartilhado e os anchors dos oito estados. Não aprova ciência, não altera objetivos e não lê teclado:
    /// recebe um anchor (de uma sequência, do controlador do capítulo ou da prévia) e compõe o palco.
    /// Distâncias astronômicas nunca viram coordenadas da Unity: Júpiter é uma esfera de composição a centenas de
    /// metros, e órbitas/DSN são diagramas 2D ou silhuetas (PIO-010/PIO-011).
    /// </summary>
    public sealed class PioneerPresentation : MonoBehaviour
    {
        public const string ProbeTargetId = "PIO.Probe";
        public const string JupiterTargetId = "PIO.Jupiter";
        public const string AldebaranTargetId = "PIO.Aldebaran";

        /// <summary>Estados do capítulo na ordem do roteiro (Docs/Chapters/02_PIONEER_10.md).</summary>
        public static readonly string[] StateIds =
        {
            "PIO.Launch", "PIO.Belt", "PIO.JupiterApproach", "PIO.Imaging",
            "PIO.Plaque", "PIO.Outbound", "PIO.Listening", "PIO.Farewell",
        };

        [Header("Câmera")]
        [SerializeField, Tooltip("A única câmera do capítulo (CORE-004).")] CameraDirector cameraDirector;

        [Header("Sonda")]
        [SerializeField, Tooltip("Gira em torno do eixo Z local (eixo da antena = eixo de rotação, P-S05).")] Transform probeSpin;
        [SerializeField, Tooltip("Sockets nomeados da sonda (Reflector, Plaque, Photopolarimeter, RtgBoomA…). O modelo definitivo (PIO-002) preserva estes nomes.")]
        List<Transform> probeSockets = new List<Transform>();
        [SerializeField, Tooltip("Rotação visual de cruzeiro, em graus por segundo. Valor de direção, não telemetria.")] float spinDegreesPerSecond = 24f;
        [SerializeField, Range(0f, 1f), Tooltip("Fração da rotação mantida com movimento reduzido.")] float reducedMotionSpinScale = 0.35f;

        [Header("Cenário")]
        [SerializeField] GameObject[] regionRoots = new GameObject[3];
        [SerializeField] Transform jupiter;
        [SerializeField, Tooltip("Ponto avermelhado distante; acompanha a câmera, como uma estrela no infinito.")] Transform aldebaranMarker;
        [SerializeField, Tooltip("Direção de mundo do marcador (direção geral de Aldebaran no palco, não posição calculada).")] Vector3 aldebaranDirection = new Vector3(0.2f, 0.08f, -1f);
        [SerializeField, Min(10f)] float aldebaranDistanceMeters = 1100f;
        [SerializeField, Tooltip("Esfera de céu centrada na câmera: sem paralaxe e sem milhares de objetos.")] Transform sky;

        [Header("Luz")]
        [SerializeField] Light keyLight;
        [SerializeField] Light fillLight;
        [SerializeField] List<PioneerLightGroup> lightGroups = new List<PioneerLightGroup>();

        [Header("Anchors")]
        [SerializeField] List<PioneerStageAnchor> anchors = new List<PioneerStageAnchor>();
        [SerializeField, Tooltip("Anchor composto ao abrir a cena isolada, se ninguém compôs antes (prévia/desenvolvimento).")] string startAnchorId = "PIO.Launch";

        IActiveTimeSource timeSource;
        SequenceDirector boundDirector;
        PioneerStageAnchor current;
        bool reducedMotion;
        bool registered;

        public IReadOnlyList<PioneerStageAnchor> Anchors => anchors;
        public IReadOnlyList<PioneerLightGroup> LightGroups => lightGroups;
        public PioneerStageAnchor Current => current;
        public CameraDirector CameraDirector => cameraDirector;
        public Transform ProbeSpin => probeSpin;
        public Transform Jupiter => jupiter;
        public Transform Sky => sky;
        public Transform AldebaranMarker => aldebaranMarker;
        public bool ReducedMotion => reducedMotion;
        public float SpinDegreesPerSecond => spinDegreesPerSecond;

        /// <summary>Rotação visual efetiva no anchor atual (graus/s), já com movimento reduzido.</summary>
        public float EffectiveSpinDegreesPerSecond =>
            spinDegreesPerSecond * (current?.spinScale ?? 1f) * (reducedMotion ? reducedMotionSpinScale : 1f);

        /// <summary>Tempo ativo (SessionClock): a rotação congela na pausa. Sem fonte (cena isolada), usa o delta do quadro.</summary>
        public void SetTimeSource(IActiveTimeSource source) => timeSource = source;

        /// <summary>Preferência de movimento reduzido (SettingsData.reducedMotion): rotação mais lenta, nenhuma vibração.</summary>
        public void SetReducedMotion(bool value) => reducedMotion = value;

        public bool TryGetAnchor(string anchorId, out PioneerStageAnchor anchor)
        {
            anchor = null;
            if (string.IsNullOrEmpty(anchorId))
                return false;

            foreach (var candidate in anchors)
            {
                if (candidate != null && candidate.anchorId == anchorId)
                {
                    anchor = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetSocket(string socketName, out Transform socket)
        {
            socket = null;
            if (string.IsNullOrEmpty(socketName))
                return false;

            foreach (var candidate in probeSockets)
            {
                if (candidate != null && candidate.name == socketName)
                {
                    socket = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Raiz da região (DeepSpace, LaunchSite, GroundStation): onde as tarefas penduram seus sockets.</summary>
        public Transform RegionRoot(PioneerStageRegion region)
        {
            var index = (int)region;
            return regionRoots != null && index >= 0 && index < regionRoots.Length && regionRoots[index] != null
                ? regionRoots[index].transform
                : null;
        }

        /// <summary>
        /// Segue uma sequência: a cada plano cujo rig é um anchor do palco, compõe região, luz e Júpiter desse anchor.
        /// A câmera continua com a sequência (ShowAnchor não a move enquanto a sequência detém o controle).
        /// </summary>
        public void BindSequenceDirector(SequenceDirector director)
        {
            if (boundDirector == director)
                return;

            UnbindSequenceDirector();
            boundDirector = director;
            if (boundDirector != null)
                boundDirector.ShotStarted += OnShotStarted;
        }

        public void UnbindSequenceDirector()
        {
            if (boundDirector != null)
                boundDirector.ShotStarted -= OnShotStarted;
            boundDirector = null;
        }

        /// <summary>Compõe o palco para o plano da sequência que está no instante (prévia de Editor, sem eventos).</summary>
        public bool ShowShotAt(SequenceDefinition sequence, float timeSeconds)
        {
            if (sequence == null)
                return false;

            var start = 0f;
            foreach (var shot in sequence.Shots)
            {
                if (timeSeconds < start + shot.DurationSeconds || shot == sequence.Shots[sequence.Shots.Count - 1])
                    return ShowAnchor(shot.RigId);
                start += shot.DurationSeconds;
            }

            return false;
        }

        void OnShotStarted(string shotId)
        {
            var sequence = boundDirector != null ? boundDirector.Current : null;
            if (sequence == null)
                return;

            foreach (var shot in sequence.Shots)
            {
                if (shot.ShotId == shotId)
                {
                    ShowAnchor(shot.RigId); // rig que não é anchor (ex.: "world") é ignorado
                    return;
                }
            }
        }

        /// <summary>Viewport reservado ao quadro de UI do anchor (ex.: ImagingPanel da PIO-006). Falso se não houver.</summary>
        public bool TryGetUiFrame(string anchorId, out Rect viewport)
        {
            viewport = default;
            if (!TryGetAnchor(anchorId, out var anchor) || !anchor.HasUiFrame)
                return false;

            viewport = anchor.uiFrameViewport;
            return true;
        }

        /// <summary>
        /// Compõe o palco para o anchor: região, luz, Júpiter, marcador e alinhamento da rotação. A câmera só é posicionada
        /// quando nenhuma sequência detém o controle (CameraDirector.IsControlled); com sequência, ela usa o anchor como rig.
        /// </summary>
        public bool ShowAnchor(string anchorId)
        {
            if (!TryGetAnchor(anchorId, out var anchor) || anchor.pose == null)
                return false;

            current = anchor;
            for (var i = 0; i < regionRoots.Length; i++)
            {
                if (regionRoots[i] != null)
                    regionRoots[i].SetActive(i == (int)anchor.region);
            }

            if (jupiter != null)
                jupiter.gameObject.SetActive(anchor.showJupiter && anchor.region == PioneerStageRegion.DeepSpace);
            if (aldebaranMarker != null)
                aldebaranMarker.gameObject.SetActive(anchor.showAldebaranMarker);

            ApplyLightGroup(anchor.lightGroupId);
            AlignSpin(anchor);

            if (cameraDirector != null && !cameraDirector.IsControlled)
                cameraDirector.Apply(PoseOf(anchor));

            SyncToCamera();
            return true;
        }

        public CameraPose PoseOf(PioneerStageAnchor anchor) =>
            new CameraPose(anchor.pose.position, anchor.pose.rotation, anchor.fieldOfViewDegrees);

        /// <summary>
        /// Plano parado no anchor, pronto para uma <see cref="SequenceDefinition"/>: rig = anchor, keyframes em zero.
        /// Movimentos curtos (dolly) partem deste formato trocando o keyframe final.
        /// </summary>
        public ShotDefinition CreateHoldShot(string anchorId, float durationSeconds)
        {
            if (!TryGetAnchor(anchorId, out var anchor))
                throw new ArgumentException($"Anchor '{anchorId}' não existe no palco da Pioneer.");

            var fov = anchor.fieldOfViewDegrees;
            return new ShotDefinition(anchorId + ".Hold", durationSeconds, anchorId,
                new[]
                {
                    new ShotKeyframe(0f, Vector3.zero, Vector3.zero, fov),
                    new ShotKeyframe(durationSeconds, Vector3.zero, Vector3.zero, fov),
                },
                easing: ShotEasing.Linear, minGroundClearanceMeters: 0f);
        }

        public bool ApplyLightGroup(string groupId)
        {
            PioneerLightGroup group = null;
            foreach (var candidate in lightGroups)
            {
                if (candidate != null && candidate.groupId == groupId)
                {
                    group = candidate;
                    break;
                }
            }

            if (group == null)
                return false;

            if (keyLight != null)
            {
                keyLight.transform.rotation = Quaternion.Euler(group.keyEulerDegrees);
                keyLight.color = group.keyColor;
                keyLight.intensity = group.keyIntensity;
            }

            if (fillLight != null)
            {
                fillLight.transform.rotation = Quaternion.Euler(group.fillEulerDegrees);
                fillLight.color = group.fillColor;
                fillLight.intensity = group.fillIntensity;
            }

            return true;
        }

        /// <summary>Avança a rotação visual. Chamado por Update; exposto para testes e prévia determinística.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (probeSpin == null || activeDeltaSeconds <= 0f)
                return;

            probeSpin.Rotate(0f, 0f, EffectiveSpinDegreesPerSecond * activeDeltaSeconds, Space.Self);
        }

        /// <summary>Céu e marcador de Aldebaran acompanham a câmera: ficam "no infinito", sem coordenadas enormes.</summary>
        public void SyncToCamera()
        {
            var camera = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (camera == null)
                return;

            var position = camera.transform.position;
            if (sky != null)
                sky.position = position;
            if (aldebaranMarker != null)
            {
                var direction = aldebaranDirection.sqrMagnitude > 1e-6f ? aldebaranDirection.normalized : Vector3.forward;
                aldebaranMarker.position = position + direction * aldebaranDistanceMeters;
                aldebaranMarker.rotation = Quaternion.LookRotation(direction);
            }
        }

        void AlignSpin(PioneerStageAnchor anchor)
        {
            if (probeSpin == null || string.IsNullOrEmpty(anchor.spinAlignSocket) || !TryGetSocket(anchor.spinAlignSocket, out var socket))
                return;

            Vector3 target;
            if (string.IsNullOrEmpty(anchor.spinAlignTowards))
                target = anchor.pose.position;
            else if (anchor.spinAlignTowards == JupiterTargetId && jupiter != null)
                target = jupiter.position;
            else
                return;

            // Ângulo, no plano perpendicular ao eixo de rotação, entre a direção radial do socket e a do alvo.
            var axis = probeSpin.forward;
            var center = probeSpin.position;
            var from = Vector3.ProjectOnPlane(socket.position - center, axis);
            var to = Vector3.ProjectOnPlane(target - center, axis);
            if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f)
                return;

            probeSpin.Rotate(axis, Vector3.SignedAngle(from, to, axis), Space.World);
        }

        void OnEnable() => RegisterWithCameraDirector();

        void OnDisable()
        {
            UnregisterFromCameraDirector();
            UnbindSequenceDirector();
        }

        /// <summary>Registra cada anchor como rig e os alvos (sonda, sockets, Júpiter, Aldebaran) no CameraDirector. Idempotente.</summary>
        public void RegisterWithCameraDirector()
        {
            if (cameraDirector == null || registered)
                return;

            foreach (var anchor in anchors)
            {
                if (anchor?.pose != null && ContractValidation.IsValidId(anchor.anchorId))
                    cameraDirector.RegisterRig(anchor.anchorId, anchor.pose);
            }

            if (probeSpin != null)
                cameraDirector.RegisterLookTarget(ProbeTargetId, probeSpin);
            if (jupiter != null)
                cameraDirector.RegisterLookTarget(JupiterTargetId, jupiter);
            if (aldebaranMarker != null)
                cameraDirector.RegisterLookTarget(AldebaranTargetId, aldebaranMarker);
            foreach (var socket in probeSockets)
            {
                if (socket != null)
                    cameraDirector.RegisterLookTarget("PIO.Probe." + socket.name, socket);
            }

            registered = true;
        }

        public void UnregisterFromCameraDirector()
        {
            if (cameraDirector == null || !registered)
                return;

            foreach (var anchor in anchors)
            {
                if (anchor != null)
                    cameraDirector.UnregisterRig(anchor.anchorId);
            }

            cameraDirector.UnregisterLookTarget(ProbeTargetId);
            cameraDirector.UnregisterLookTarget(JupiterTargetId);
            cameraDirector.UnregisterLookTarget(AldebaranTargetId);
            foreach (var socket in probeSockets)
            {
                if (socket != null)
                    cameraDirector.UnregisterLookTarget("PIO.Probe." + socket.name);
            }

            registered = false;
        }

        void Start()
        {
            if (current == null && !string.IsNullOrEmpty(startAnchorId))
                ShowAnchor(startAnchorId);
        }

        void Update()
        {
            // Fonte destruída (ex.: troca de capítulo) não lança exceção: volta ao delta do quadro até nova fonte.
            if (timeSource is UnityEngine.Object unityObject && unityObject == null)
                timeSource = null;

            Tick(timeSource != null ? timeSource.ActiveDeltaSeconds : Time.deltaTime);
        }

        void LateUpdate() => SyncToCamera();

        /// <summary>Erros de montagem: anchors, grupos de luz e referências. Lista vazia = conforme.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = "PioneerPresentation";
            if (cameraDirector == null || cameraDirector.TargetCamera == null)
                ContractValidation.Report(errors, owner, "CameraDirector/câmera ausente.");
            if (probeSpin == null)
                ContractValidation.Report(errors, owner, "probeSpin ausente.");
            if (jupiter == null || sky == null || aldebaranMarker == null || keyLight == null || fillLight == null)
                ContractValidation.Report(errors, owner, "Júpiter, céu, marcador de Aldebaran ou luzes ausentes.");
            if (regionRoots == null || regionRoots.Length != 3 || Array.Exists(regionRoots, r => r == null))
                ContractValidation.Report(errors, owner, "as três regiões precisam de raiz.");

            var groups = new HashSet<string>();
            foreach (var group in lightGroups)
            {
                ContractValidation.RequireId(errors, owner, "lightGroups[].groupId", group?.groupId);
                if (group != null && !groups.Add(group.groupId))
                    ContractValidation.Report(errors, owner, $"grupo de luz '{group.groupId}' duplicado.");
            }

            var ids = new HashSet<string>();
            foreach (var anchor in anchors)
            {
                if (anchor == null)
                {
                    ContractValidation.Report(errors, owner, "anchor nulo.");
                    continue;
                }

                var who = $"anchor '{anchor.anchorId}'";
                ContractValidation.RequireId(errors, owner, who + ".anchorId", anchor.anchorId);
                if (!ids.Add(anchor.anchorId ?? ""))
                    ContractValidation.Report(errors, owner, who + " duplicado.");
                if (Array.IndexOf(StateIds, anchor.stateId) < 0)
                    ContractValidation.Report(errors, owner, $"{who}: estado '{anchor.stateId}' desconhecido.");
                if (anchor.pose == null)
                    ContractValidation.Report(errors, owner, who + ": pose ausente.");
                if (string.IsNullOrWhiteSpace(anchor.function))
                    ContractValidation.Report(errors, owner, who + ": função não descrita.");
                if (!groups.Contains(anchor.lightGroupId ?? ""))
                    ContractValidation.Report(errors, owner, $"{who}: grupo de luz '{anchor.lightGroupId}' inexistente.");
                if (!string.IsNullOrEmpty(anchor.spinAlignSocket) && !TryGetSocket(anchor.spinAlignSocket, out _))
                    ContractValidation.Report(errors, owner, $"{who}: socket '{anchor.spinAlignSocket}' inexistente.");
                if (anchor.showJupiter && anchor.region != PioneerStageRegion.DeepSpace)
                    ContractValidation.Report(errors, owner, $"{who}: Júpiter só existe no espaço.");
            }

            foreach (var state in StateIds)
            {
                if (!ids.Contains(state))
                    ContractValidation.Report(errors, owner, $"falta o anchor do estado {state}.");
            }

            return errors;
        }
    }
}
