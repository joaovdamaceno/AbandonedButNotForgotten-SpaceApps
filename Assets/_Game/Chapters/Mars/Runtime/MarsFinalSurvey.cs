using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum FinalSurveyPhase
    {
        Idle = 0,
        Approaching = 1,
        SunReference = 2,
        Valley = 3,
        AwaitingDust = 4,
        SunDust = 5,
        Done = 6,
    }

    /// <summary>
    /// Captura de quadro: renderiza a câmera do jogo numa RenderTexture do tamanho pedido (nunca acima de 960×540, nada de
    /// tela 4K temporária) e devolve um Sprite com uma textura própria, que o dono precisa destruir. Restaura a câmera.
    /// </summary>
    public static class MarsFrameCapture
    {
        public static Sprite Grab(Camera camera, Vector3 position, Quaternion rotation, int width, int height)
        {
            if (camera == null)
                return null;

            width = Mathf.Clamp(width, 16, FinalSurveyData.MaxImageWidth);
            height = Mathf.Clamp(height, 16, FinalSurveyData.MaxImageHeight);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return Blank(width, height); // sem placa de vídeo (testes headless, servidores): não há o que renderizar

            var transform = camera.transform;
            var savedPosition = transform.position;
            var savedRotation = transform.rotation;
            var savedTarget = camera.targetTexture;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                transform.SetPositionAndRotation(position, rotation);
                camera.targetTexture = rt;
                camera.Render(); // aquecimento: a primeira renderização de uma pose nova ainda não tem o mapa de sombra
                camera.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false) { name = "MarsCapture (runtime)" };
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            }
            finally
            {
                camera.targetTexture = savedTarget;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                transform.SetPositionAndRotation(savedPosition, savedRotation);
            }
        }

        static Sprite Blank(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false) { name = "MarsCapture (sem gráficos)" };
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(90, 70, 60, 255);

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Duas capturas lado a lado numa só textura (largura somada, até 960×540).</summary>
        public static Sprite SideBySide(Sprite left, Sprite right)
        {
            if (left == null || right == null)
                return null;

            var lt = left.texture;
            var rt = right.texture;
            var width = lt.width + rt.width;
            var height = Mathf.Max(lt.height, rt.height);
            if (width > FinalSurveyData.MaxImageWidth || height > FinalSurveyData.MaxImageHeight)
                return null;

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false) { name = "MarsComparison (runtime)" };
            texture.SetPixels(0, 0, lt.width, lt.height, lt.GetPixels());
            texture.SetPixels(lt.width, 0, rt.width, rt.height, rt.GetPixels());
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        public static void Release(Sprite sprite)
        {
            if (sprite == null)
                return;

            var texture = sprite.texture;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(sprite);
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>
    /// MAR-008 · M24–M28: o mirante, o Sol (referência e através da poeira) e o vale. M24: chegar a 3 m do mirante envia
    /// <c>mars.overlook.reached</c> e para o rover. M25 e M28: <b>E por 4 s</b> registra o Sol com enquadramento automático (a
    /// câmera vai para a pose guiada: não há disco solar para clicar). M26: <b>E por 4 s</b> fotografa o vale com a câmera do jogo
    /// (até 960×540); o jogador pode ajustar o quadro um pouco, sem que isso bloqueie nada.
    /// <para>
    /// Imagens (orçamento das fotos, <c>Docs/Production/MARS_EVIDENCE_BUDGET.md</c>): só o vale é foto do jogador. O Sol vira
    /// registro científico: duas observações com miniatura e a comparação lado a lado. Nenhum número inventado: a luz
    /// bloqueada aparece na própria imagem. Texturas criadas aqui são destruídas ao reiniciar. Soltar E, pausar ou perder foco
    /// conserva o trabalho. A antena e a escolha da memória são de <see cref="MarsTransmission"/>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsFinalSurvey : MonoBehaviour
    {
        public const string OverlookStepId = "M24";
        public const string SunStepId = "M25";
        public const string ValleyStepId = "M26";
        public const string DustStepId = "M28";
        public const string SunCandidateId = "mars.survey.sun";
        public const string ValleyCandidateId = "mars.survey.valley";
        const int CandidatePriority = 10;

        /// <summary>Âncoras da cena do mirante (R4/montagem). O prefab não as carrega.</summary>
        [Serializable]
        public sealed class Anchors
        {
            [Tooltip("Ponto no mirante: alvo da chegada e âncora do prompt.")] public Transform overlook;
            [Tooltip("Pose da câmera guiada que já enquadra o Sol.")] public Transform sunView;
            [Tooltip("Pose da câmera guiada que já enquadra o vale.")] public Transform valleyView;
        }

        [SerializeField] FinalSurveyData data;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] InteractionService interactionService;
        [SerializeField] Anchors anchors = new Anchors();
        [SerializeField] Transform operatorTransform;
        [SerializeField, Tooltip("A câmera do jogo (a mesma do CameraDirector).")] Camera captureCamera;

        readonly HashSet<string> raised = new HashSet<string>();
        readonly HeldWork work = new HeldWork();
        readonly List<Sprite> owned = new List<Sprite>();
        IInteractionService interaction;
        IActiveTimeSource activeTime;
        IEvidenceStore evidence;
        bool bound;
        bool holding;
        bool engaged;
        bool candidateRegistered;
        string candidateId;
        float lastHold = -1f;
        bool lastAvailable;
        Sprite sunReference;
        Sprite valleyPhoto;
        float adjustYaw;
        float adjustPitch;

        public FinalSurveyPhase Phase { get; private set; }
        public bool IsHolding => holding;
        public bool IsEngaged => engaged;
        public float WorkSeconds => work.Work;
        public float WorkFraction => work.Fraction;
        public FinalSurveyData Data => data;
        public Transform CurrentView { get; private set; }
        public Sprite ValleyPhoto => valleyPhoto;
        public Sprite SunReferenceImage => sunReference;
        public float FrameAdjustYawDegrees => adjustYaw;
        public float FrameAdjustPitchDegrees => adjustPitch;

        /// <summary>Distância horizontal ao mirante, para o HUD (seta quando fora da tela); infinita sem operador.</summary>
        public float DistanceToOverlookMeters
        {
            get
            {
                if (operatorTransform == null || anchors.overlook == null)
                    return float.PositiveInfinity;

                var delta = anchors.overlook.position - operatorTransform.position;
                delta.y = 0f;
                return delta.magnitude;
            }
        }

        public Transform OverlookAnchor => anchors.overlook;

        public event Action<FinalSurveyPhase> PhaseChanged;
        public event Action<bool> EngagedChanged;
        public event Action<Transform> ViewChanged;

        public void Initialize(MarsChapterFlow chapterFlow, IInteractionService interactionContract, IActiveTimeSource activeTimeSource,
            Anchors sceneAnchors, Transform operatorPosition, Camera gameCamera, IEvidenceStore evidenceStore = null)
        {
            Unbind();
            flow = chapterFlow;
            interaction = interactionContract;
            activeTime = activeTimeSource;
            anchors = sceneAnchors ?? new Anchors();
            operatorTransform = operatorPosition;
            captureCamera = gameCamera;
            evidence = evidenceStore;
            if (isActiveAndEnabled)
                Bind();
        }

        /// <summary>
        /// Ajuste OPCIONAL do quadro do vale (graus, limitado). Nunca é necessário para concluir; a UI/entrada liga isto ao mouse.
        /// </summary>
        public void AdjustFrame(float yawDegrees, float pitchDegrees)
        {
            var limit = data != null ? data.maxFrameAdjustDegrees : 15f;
            adjustYaw = Mathf.Clamp(yawDegrees, -limit, limit);
            adjustPitch = Mathf.Clamp(pitchDegrees, -limit, limit);
        }

        void OnEnable()
        {
            if (interaction == null && interactionService != null)
                interaction = interactionService;

            Bind();
        }

        void OnDisable() => Unbind();

        void OnDestroy() => ReleaseImages();

        void Bind()
        {
            if (bound)
                return;

            bound = true;
            if (interaction != null)
            {
                interaction.HoldStarted += OnHoldStarted;
                interaction.HoldProgress += OnHoldProgress;
                interaction.HoldCompleted += OnHoldCompleted;
                interaction.HoldCanceled += OnHoldCanceled;
            }

            if (flow != null)
            {
                flow.StepStarted += OnStepStarted;
                flow.StepEnded += OnStepEnded;
                if (flow.CurrentBinding != null)
                    OnStepStarted(flow.CurrentBinding);
            }
        }

        void Unbind()
        {
            if (!bound)
                return;

            bound = false;
            if (interaction != null)
            {
                interaction.HoldStarted -= OnHoldStarted;
                interaction.HoldProgress -= OnHoldProgress;
                interaction.HoldCompleted -= OnHoldCompleted;
                interaction.HoldCanceled -= OnHoldCanceled;
            }

            if (flow != null)
            {
                flow.StepStarted -= OnStepStarted;
                flow.StepEnded -= OnStepEnded;
            }

            ClearCandidate();
            holding = false;
            SetEngaged(false);
        }

        // ---------- Etapas ----------

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (binding == null)
                return;

            switch (binding.StepId)
            {
                case OverlookStepId:
                    ResetActivity();
                    SetPhase(FinalSurveyPhase.Approaching);
                    break;
                case SunStepId:
                    if (Phase != FinalSurveyPhase.Approaching)
                        ResetActivity(); // entrou em M25 sem passar por M24 (reinício, teste)

                    BeginCapture(FinalSurveyPhase.SunReference, anchors.sunView, data.sunHoldSeconds);
                    break;
                case ValleyStepId:
                    BeginCapture(FinalSurveyPhase.Valley, anchors.valleyView, data.valleyHoldSeconds);
                    break;
                case DustStepId:
                    BeginCapture(FinalSurveyPhase.SunDust, anchors.sunView, data.sunHoldSeconds);
                    break;
                default:
                    // Reinício do capítulo: voltou para uma etapa ANTES do mirante. Nenhum estado, imagem ou marca pode sobrar.
                    if (Phase != FinalSurveyPhase.Idle && IsBeforeSurvey(binding.StepId))
                        ResetActivity();

                    break;
            }
        }

        void OnStepEnded(string stepId)
        {
            if (stepId != OverlookStepId && stepId != SunStepId && stepId != ValleyStepId && stepId != DustStepId)
                return;

            ClearCandidate();
            holding = false;
            var completed = stepId == OverlookStepId ? raised.Contains(MarsChapterFlow.Events.OverlookReached)
                : stepId == SunStepId ? raised.Contains(MarsChapterFlow.Events.SunReferenceCaptured)
                : stepId == ValleyStepId ? raised.Contains(MarsChapterFlow.Events.ValleyCaptured)
                : raised.Contains(MarsChapterFlow.Events.SunDustCaptured);
            if (!completed)
            {
                ResetActivity();
                return;
            }

            SetView(null);
            if (stepId == ValleyStepId)
                SetPhase(FinalSurveyPhase.AwaitingDust);
            else if (stepId == DustStepId)
                SetPhase(FinalSurveyPhase.Done);
        }

        bool IsBeforeSurvey(string stepId)
        {
            if (flow == null)
                return false;

            var steps = flow.Steps;
            var current = -1;
            var first = -1;
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].StepId == stepId)
                    current = i;
                else if (steps[i].StepId == OverlookStepId)
                    first = i;
            }

            return current >= 0 && first >= 0 && current < first;
        }

        /// <summary>Zera o trabalho e os eventos e DESTRÓI as imagens criadas aqui (reinício de capítulo ou de etapa).</summary>
        public void ResetActivity()
        {
            ClearCandidate();
            holding = false;
            raised.Clear();
            work.Reset();
            adjustYaw = adjustPitch = 0f;
            SetEngaged(false);
            SetView(null);
            ReleaseImages();
            SetPhase(FinalSurveyPhase.Idle);
        }

        void ReleaseImages()
        {
            foreach (var sprite in owned)
                MarsFrameCapture.Release(sprite);

            owned.Clear();
            sunReference = null;
            valleyPhoto = null;
        }

        void BeginCapture(FinalSurveyPhase phase, Transform view, float holdSeconds)
        {
            work.Begin(holdSeconds);
            SetView(view);
            SetPhase(phase);
        }

        // ---------- Quadro ----------

        void Update()
        {
            Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO. Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (data == null)
                return;

            if (Phase == FinalSurveyPhase.Approaching && operatorTransform != null && anchors.overlook != null && DistanceToOverlookMeters <= data.reachMeters)
            {
                SetEngaged(true); // o rover para no mirante até o fim das capturas
                Raise(MarsChapterFlow.Events.OverlookReached);
            }

            RefreshCandidate();
        }

        void RefreshCandidate()
        {
            if (interaction == null || data == null)
                return;

            string wanted = null;
            string verb = null;
            string obj = null;
            if (Phase == FinalSurveyPhase.SunReference || Phase == FinalSurveyPhase.SunDust)
            {
                wanted = SunCandidateId; verb = data.sunVerbKey; obj = data.sunObjectKey;
            }
            else if (Phase == FinalSurveyPhase.Valley)
            {
                wanted = ValleyCandidateId; verb = data.valleyVerbKey; obj = data.valleyObjectKey;
            }

            if (wanted == null || holding)
            {
                if (wanted == null)
                    ClearCandidate();

                return;
            }

            var distance = DistanceToOverlookMeters;
            var inRange = float.IsInfinity(distance) || distance <= data.reachMeters * 1.5f;
            var hold = work.Remaining;
            if (candidateRegistered && candidateId == wanted && lastAvailable == inRange && Mathf.Approximately(lastHold, hold))
                return;

            if (candidateRegistered && candidateId != wanted)
                ClearCandidate();

            candidateRegistered = true;
            candidateId = wanted;
            lastAvailable = inRange;
            lastHold = hold;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = wanted, Anchor = anchors.overlook, VerbKey = verb, ObjectKey = obj, Available = inRange,
                DistanceMeters = float.IsInfinity(distance) ? 0f : distance, HoldSeconds = hold, Priority = CandidatePriority,
            });
        }

        void ClearCandidate()
        {
            if (!candidateRegistered)
                return;

            candidateRegistered = false;
            lastHold = -1f;
            if (interaction != null && candidateId != null)
                interaction.ClearCandidate(candidateId);

            candidateId = null;
        }

        // ---------- Eventos do InteractionService ----------

        bool IsMine(string id) =>
            (id == SunCandidateId && (Phase == FinalSurveyPhase.SunReference || Phase == FinalSurveyPhase.SunDust))
            || (id == ValleyCandidateId && Phase == FinalSurveyPhase.Valley);

        void OnHoldStarted(string id)
        {
            if (!IsMine(id))
                return;

            holding = true;
            work.Started(interaction.TryGetActive(out var active) && active.Id == id ? active.HoldSeconds : (float?)null);
        }

        void OnHoldProgress(string id, float progress)
        {
            if (holding && IsMine(id))
                work.Progress(progress);
        }

        void OnHoldCanceled(string id)
        {
            if (id != SunCandidateId && id != ValleyCandidateId)
                return;

            holding = false;
            work.Canceled();
            RefreshCandidate();
        }

        void OnHoldCompleted(string id)
        {
            if (!IsMine(id))
                return;

            holding = false;
            ClearCandidate();
            work.Progress(1f);
            if (Phase == FinalSurveyPhase.SunReference)
                CaptureSun(false);
            else if (Phase == FinalSurveyPhase.SunDust)
                CaptureSun(true);
            else
                CaptureValley();
        }

        void CaptureSun(bool dust)
        {
            // Enquadramento automático: a câmera vai para a pose guiada que já olha para o Sol (nada para clicar).
            var view = anchors.sunView;
            var sprite = view != null ? MarsFrameCapture.Grab(captureCamera, view.position, view.rotation, data.sunWidth, data.sunHeight) : null;
            if (sprite != null)
                owned.Add(sprite);

            TryAdd(data.SunRecord(dust, sprite));
            if (!dust)
            {
                sunReference = sprite;
                Raise(MarsChapterFlow.Events.SunReferenceCaptured);
                return;
            }

            // Comparação lado a lado com a primeira imagem: o registro científico (não é foto do jogador).
            var comparison = MarsFrameCapture.SideBySide(sunReference, sprite);
            if (comparison != null)
                owned.Add(comparison);

            TryAdd(data.SunComparison(comparison));
            Raise(MarsChapterFlow.Events.SunDustCaptured);
        }

        void CaptureValley()
        {
            var view = anchors.valleyView;
            Sprite photo = null;
            if (view != null)
            {
                // Ajuste opcional do jogador, limitado; sem ajuste vale o quadro guiado.
                var rotation = view.rotation * Quaternion.Euler(adjustPitch, adjustYaw, 0f);
                photo = MarsFrameCapture.Grab(captureCamera, view.position, rotation, data.valleyWidth, data.valleyHeight);
            }

            if (photo != null)
                owned.Add(photo);

            valleyPhoto = photo;
            TryAdd(data.ValleyPhoto(photo));
            Raise(MarsChapterFlow.Events.ValleyCaptured);
        }

        // O EvidenceStore lança acima de 3 fotos do jogador: a etapa nunca pode travar por isso.
        void TryAdd(EvidenceRecord record)
        {
            if (evidence == null)
                return;

            try
            {
                evidence.Add(record);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogWarning($"[MAR-008] {record.EvidenceId} não foi guardada: {e.Message}");
            }
        }

        void Raise(string eventId)
        {
            if (!raised.Add(eventId))
                return;

            if (flow != null)
                flow.Raise(eventId);
        }

        void SetPhase(FinalSurveyPhase phase)
        {
            if (Phase == phase)
                return;

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        void SetEngaged(bool value)
        {
            if (engaged == value)
                return;

            engaged = value;
            EngagedChanged?.Invoke(value);
        }

        void SetView(Transform view)
        {
            if (CurrentView == view)
                return;

            CurrentView = view;
            ViewChanged?.Invoke(view);
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            if (data == null)
                ContractValidation.Report(errors, nameof(MarsFinalSurvey), "falta FinalSurveyData.");
            else
                errors.AddRange(data.Validate());

            return errors;
        }

        /// <summary>Validação completa, com as âncoras e a câmera ligadas.</summary>
        public List<string> ValidateWiring()
        {
            var errors = ValidateSetup();
            const string owner = nameof(MarsFinalSurvey);
            if (anchors.overlook == null) ContractValidation.Report(errors, owner, "falta a âncora do mirante.");
            if (anchors.sunView == null) ContractValidation.Report(errors, owner, "falta a pose guiada do Sol.");
            if (anchors.valleyView == null) ContractValidation.Report(errors, owner, "falta a pose guiada do vale.");
            if (captureCamera == null) ContractValidation.Report(errors, owner, "falta a câmera do jogo para as capturas.");
            return errors;
        }
    }
}
