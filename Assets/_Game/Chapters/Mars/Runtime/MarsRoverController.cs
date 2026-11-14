using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Nível de ajuda de orientação: nenhum, reforço da indicação (12 s sem progresso) e oferta de ajuda (25 s).</summary>
    public enum MarsAssistLevel
    {
        None = 0,
        Reinforce = 1,
        Offer = 2,
    }

    /// <summary>
    /// MAR-002: controle da Opportunity em terceira pessoa. WASD/setas movem pelo <see cref="InputRouter"/> (único leitor
    /// de entrada), a câmera acompanha com amortecimento e o mouse é opcional: nenhum objetivo exige habilidade de mira.
    /// <para>
    /// Garantias: aceleração e giro suaves (sem teleporte nem giro instantâneo para poses impostas, ver
    /// <see cref="AlignTo"/>); colisão pelo <see cref="CharacterController"/>, e a velocidade cai quando o cenário
    /// bloqueia; o rover para durante instrumento (<see cref="SetLocked"/>), pausa (tempo ativo zero), filme e
    /// qualquer contexto de entrada diferente de Gameplay; a câmera só é escrita quando o <see cref="CameraDirector"/>
    /// não está sob controle de uma sequência, e a retomada parte da pose atual (sem salto).
    /// </para>
    /// <para>
    /// Ajuda (H): enquadra o alvo atual sem teleportar o rover nem executar ciência; sem alvo, recentraliza a câmera atrás
    /// do rover (recuperação de orientação). Metas de chegada (<see cref="ReachGoal"/>) avisam o
    /// <see cref="MarsChapterFlow"/> da etapa correspondente (M10 → <c>mars.outcrop.reached</c>) e têm a mesma escada de
    /// ajuda de 12 s/25 s. Nenhuma frase de jogo está aqui: a UI traduz as chaves dos objetivos.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class MarsRoverController : MonoBehaviour
    {
        /// <summary>Meta de chegada de uma etapa de condução: ao entrar no raio, o evento é enviado ao fluxo, uma vez por entrada.</summary>
        [Serializable]
        public sealed class ReachGoal
        {
            [Tooltip("Etapa de Marte em que a meta vale, por exemplo M10.")] public string stepId = "M10";
            [Tooltip("Evento do MarsChapterFlow, por exemplo mars.outcrop.reached.")] public string eventId = MarsChapterFlow.Events.OutcropReached;
            [Tooltip("Âncora visível do destino (corredor largo, sem pedra bloqueando).")] public Transform target;
            [Min(0.5f), Tooltip("Raio de chegada em metros, no plano horizontal.")] public float radiusMeters = 2.5f;
        }

        /// <summary>Escada de ajuda: conta tempo ativo sem progresso e informa o nível. Qualquer progresso zera.</summary>
        public sealed class AssistTimer
        {
            public const float ReinforceSeconds = 12f;
            public const float OfferSeconds = 25f;

            public float IdleSeconds { get; private set; }

            public MarsAssistLevel Level =>
                IdleSeconds >= OfferSeconds ? MarsAssistLevel.Offer
                : IdleSeconds >= ReinforceSeconds ? MarsAssistLevel.Reinforce
                : MarsAssistLevel.None;

            /// <summary>Soma tempo ATIVO (zero na pausa). Devolve o nível resultante.</summary>
            public MarsAssistLevel Advance(float activeDeltaSeconds)
            {
                if (activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds))
                    IdleSeconds += activeDeltaSeconds;

                return Level;
            }

            public void NoteProgress() => IdleSeconds = 0f;
        }

        const float MinimumGoalProgressMeters = 0.5f;

        [Header("Ligações (preenchidas pela integração; testes usam Initialize)")]
        [SerializeField] InputRouter inputRouter;
        [SerializeField] CameraDirector cameraDirector;
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] CharacterController character;

        [Header("Movimento (metros, segundos, graus)")]
        [SerializeField, Min(0.1f)] float maxForwardSpeedMetersPerSecond = 1.6f;
        [SerializeField, Min(0.1f)] float maxReverseSpeedMetersPerSecond = 0.8f;
        [SerializeField, Min(0.1f)] float accelerationMetersPerSecondSquared = 2.4f;
        [SerializeField, Min(0.1f)] float brakingMetersPerSecondSquared = 4f;
        [SerializeField, Min(1f)] float maxTurnDegreesPerSecond = 55f;
        [SerializeField, Min(1f)] float turnAccelerationDegreesPerSecondSquared = 140f;
        [SerializeField, Min(1f), Tooltip("Velocidade máxima de giro quando uma pose é pedida por AlignTo. Nunca instantâneo.")]
        float alignDegreesPerSecond = 60f;
        [SerializeField, Min(0f), Tooltip("Gravidade de Marte, 3,71 m/s², só para manter o rover no chão.")]
        float gravityMetersPerSecondSquared = 3.71f;

        [Header("Câmera")]
        [SerializeField, Min(1f)] float followDistanceMeters = 5.5f;
        [SerializeField, Min(0f)] float pivotHeightMeters = 0.9f;
        [SerializeField, Range(0f, 60f)] float defaultPitchDegrees = 16f;
        [SerializeField, Range(-30f, 60f)] float minPitchDegrees = 4f;
        [SerializeField, Range(0f, 80f)] float maxPitchDegrees = 40f;
        [SerializeField, Min(0.01f)] float followSmoothSeconds = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("Graus por unidade de delta do mouse, antes da sensibilidade.")] float orbitDegreesPerCount = 0.1f;
        [SerializeField, Min(0f)] float orbitReturnDelaySeconds = 2.5f;
        [SerializeField, Min(1f)] float orbitReturnDegreesPerSecond = 70f;
        [SerializeField, Min(1f)] float frameDegreesPerSecond = 110f;
        [SerializeField, Min(0f)] float frameHoldSeconds = 3f;
        [SerializeField, Min(0.05f)] float cameraCollisionRadiusMeters = 0.3f;
        [SerializeField, Min(0f)] float cameraGroundClearanceMeters = 0.4f;
        [SerializeField] LayerMask cameraCollisionMask = ~0;
        [SerializeField, Range(0.1f, 3f)] float lookSensitivity = 1f;

        [Header("Metas de chegada")]
        [SerializeField] List<ReachGoal> reachGoals = new List<ReachGoal>();

        readonly HashSet<string> lockReasons = new HashSet<string>();
        readonly HashSet<ReachGoal> raisedGoals = new HashSet<ReachGoal>();
        readonly AssistTimer goalAssist = new AssistTimer();
        IActiveTimeSource activeTime;
        bool bound;

        float speedMetersPerSecond;
        float turnDegreesPerSecond;
        float verticalSpeed;
        float orbitYawDegrees;
        float orbitPitchDegrees;
        float orbitIdleSeconds;
        float frameHoldRemaining;
        float? frameYawGoal;
        Transform frameTarget;
        bool hasAlignGoal;
        Vector3 alignDirection;
        Vector3 smoothedCameraPosition;
        bool cameraNeedsResync = true;
        float lastDeltaSeconds;
        ReachGoal activeGoal;
        float bestGoalDistance;
        MarsAssistLevel lastReportedGoalLevel;

        /// <summary>Metros efetivamente percorridos no plano (colisão não conta deslocamento que não aconteceu).</summary>
        public float DrivenMeters { get; private set; }

        /// <summary>Graus de olhar manual acumulados (mouse). Opcional para qualquer objetivo.</summary>
        public float LookedDegrees { get; private set; }

        public float SpeedMetersPerSecond => speedMetersPerSecond;
        public float TurnDegreesPerSecond => turnDegreesPerSecond;
        public float MaxForwardSpeedMetersPerSecond => maxForwardSpeedMetersPerSecond;
        public float MaxTurnDegreesPerSecond => maxTurnDegreesPerSecond;
        public float OrbitYawDegrees => orbitYawDegrees;
        public bool IsLocked => lockReasons.Count > 0;
        public bool IsAligning => hasAlignGoal;
        public Transform FrameTarget => frameTarget;
        public MarsAssistLevel GoalAssistLevel => activeGoal != null ? goalAssist.Level : MarsAssistLevel.None;
        public ReachGoal ActiveGoal => activeGoal;
        public IReadOnlyList<ReachGoal> ReachGoals => reachGoals;
        public float LookSensitivity { get => lookSensitivity; set => lookSensitivity = Mathf.Clamp(value, 0.1f, 3f); }

        /// <summary>H foi aceito (contexto Gameplay): a câmera enquadra o alvo ou recentra atrás do rover.</summary>
        public event Action FrameRequested;

        /// <summary>A pose pedida por <see cref="AlignTo"/> foi alcançada.</summary>
        public event Action Aligned;

        /// <summary>Uma meta de chegada foi enviada ao fluxo. Argumento: eventId.</summary>
        public event Action<string> GoalReached;

        /// <summary>A escada de ajuda da meta atual mudou de nível.</summary>
        public event Action<MarsAssistLevel> GoalAssistChanged;

        void Reset() => character = GetComponent<CharacterController>();

        void Awake()
        {
            if (character == null)
                character = GetComponent<CharacterController>();
        }

        void OnEnable()
        {
            orbitPitchDegrees = Mathf.Clamp(defaultPitchDegrees, minPitchDegrees, maxPitchDegrees);
            cameraNeedsResync = true;
            Bind();
        }

        void OnDisable() => Unbind();

        /// <summary>
        /// Liga as dependências (testes e integração por código). <paramref name="activeTimeSource"/> é o SessionClock:
        /// sem ele o rover não se move (lado seguro, igual ao InteractionService).
        /// </summary>
        public void Initialize(InputRouter router, IActiveTimeSource activeTimeSource, CameraDirector director, MarsChapterFlow chapterFlow)
        {
            Unbind();
            inputRouter = router;
            activeTime = activeTimeSource;
            cameraDirector = director;
            flow = chapterFlow;
            if (isActiveAndEnabled)
                Bind();
        }

        /// <summary>Fonte de tempo ativo quando o resto já está ligado pelo Inspector.</summary>
        public void SetActiveTimeSource(IActiveTimeSource source) => activeTime = source;

        void Bind()
        {
            if (bound)
                return;

            bound = true;
            if (inputRouter != null)
            {
                inputRouter.HelpDown += OnHelpDown;
                inputRouter.InputCleared += OnInputCleared;
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
            if (inputRouter != null)
            {
                inputRouter.HelpDown -= OnHelpDown;
                inputRouter.InputCleared -= OnInputCleared;
            }

            if (flow != null)
            {
                flow.StepStarted -= OnStepStarted;
                flow.StepEnded -= OnStepEnded;
            }

            StopImmediately();
            EndGoal();
        }

        // ---- API para instrumentos, tutorial e integração ----

        /// <summary>
        /// Trava o rover (instrumento em uso, filme). Várias razões podem coexistir; destravar exige remover todas.
        /// Travar para na hora: o rover não desliza para dentro do instrumento.
        /// </summary>
        public void SetLocked(string reason, bool locked)
        {
            if (string.IsNullOrEmpty(reason))
                throw new ArgumentException("Informe quem trava o rover.", nameof(reason));

            if (locked)
            {
                lockReasons.Add(reason);
                StopImmediately();
            }
            else
            {
                lockReasons.Remove(reason);
            }
        }

        /// <summary>Destrava tudo e zera o movimento (reinício de etapa ou cena).</summary>
        public void ClearLocks()
        {
            lockReasons.Clear();
            StopImmediately();
        }

        /// <summary>
        /// Pede que o rover se oriente para uma direção horizontal, girando no máximo <c>alignDegreesPerSecond</c>.
        /// Nunca é instantâneo; a pose imposta é alcançada em alguns quadros e <see cref="Aligned"/> avisa.
        /// </summary>
        public void AlignTo(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 1e-6f)
                return;

            alignDirection = worldDirection.normalized;
            hasAlignGoal = true;
        }

        public void CancelAlign() => hasAlignGoal = false;

        /// <summary>Alvo que H enquadra. Nulo = H apenas recentra a câmera atrás do rover.</summary>
        public void SetFrameTarget(Transform target) => frameTarget = target;

        /// <summary>Equivalente a H, para a ajuda oferecida pela UI.</summary>
        public void RequestFrame()
        {
            frameHoldRemaining = frameHoldSeconds;
            frameYawGoal = frameTarget != null ? YawTo(frameTarget.position) : 0f;
            orbitIdleSeconds = 0f;
            FrameRequested?.Invoke();
        }

        /// <summary>Coloca o rover numa pose (início do capítulo, reinício) sem herdar velocidade nem contadores.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            if (character == null)
                character = GetComponent<CharacterController>();

            var wasEnabled = character != null && character.enabled;
            if (character != null)
                character.enabled = false;

            transform.SetPositionAndRotation(position, rotation);
            if (character != null)
                character.enabled = wasEnabled;

            StopImmediately();
            verticalSpeed = 0f;
            orbitYawDegrees = 0f;
            hasAlignGoal = false;
            cameraNeedsResync = true;
        }

        /// <summary>Zera contadores de condução e olhar (o tutorial mede a partir do começo de cada tentativa).</summary>
        public void ResetCounters()
        {
            DrivenMeters = 0f;
            LookedDegrees = 0f;
        }

        // ---- Quadro ----

        void Update()
        {
            Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);
        }

        void LateUpdate() => TickCamera(lastDeltaSeconds);

        /// <summary>Avança um quadro com tempo ATIVO. Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            var dt = activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds) ? activeDeltaSeconds : 0f;
            lastDeltaSeconds = dt;

            var gameplay = inputRouter != null && inputRouter.Context == InputContext.Gameplay;
            var inputAllowed = gameplay && !IsLocked;
            var move = inputAllowed ? inputRouter.Move : Vector2.zero;
            var look = inputAllowed ? inputRouter.Look : Vector2.zero;

            if (IsLocked)
                StopImmediately();
            else
                TickDrive(move, dt);

            TickOrbit(look, dt);
            TickGoals(dt);
        }

        void TickDrive(Vector2 move, float dt)
        {
            if (dt <= 0f)
                return;

            var steering = hasAlignGoal ? 0f : move.x;
            StepMotion(ref speedMetersPerSecond, ref turnDegreesPerSecond, move.y, steering, dt,
                maxForwardSpeedMetersPerSecond, maxReverseSpeedMetersPerSecond, accelerationMetersPerSecondSquared,
                brakingMetersPerSecondSquared, maxTurnDegreesPerSecond, turnAccelerationDegreesPerSecondSquared);

            if (hasAlignGoal)
                TickAlign(dt);
            else if (Mathf.Abs(turnDegreesPerSecond) > 0f)
                transform.Rotate(0f, turnDegreesPerSecond * dt, 0f, Space.World);

            var forward = transform.forward;
            var delta = forward * (speedMetersPerSecond * dt);
            verticalSpeed = character != null && character.isGrounded ? -1f : verticalSpeed - gravityMetersPerSecondSquared * dt;
            delta.y = verticalSpeed * dt;

            var before = transform.position;
            if (character != null && character.enabled)
                character.Move(delta);
            else
                transform.position += delta;

            var actual = transform.position - before;
            actual.y = 0f;
            DrivenMeters += actual.magnitude;

            // Bloqueado pelo cenário: a velocidade cai para a que realmente aconteceu, sem acumular empurrão.
            var along = Vector3.Dot(actual, forward) / dt;
            if (speedMetersPerSecond > 0f)
                speedMetersPerSecond = Mathf.Min(speedMetersPerSecond, Mathf.Max(0f, along));
            else if (speedMetersPerSecond < 0f)
                speedMetersPerSecond = Mathf.Max(speedMetersPerSecond, Mathf.Min(0f, along));
        }

        void TickAlign(float dt)
        {
            var current = transform.forward;
            current.y = 0f;
            var angle = Vector3.SignedAngle(current, alignDirection, Vector3.up);
            var step = Mathf.Min(Mathf.Abs(angle), alignDegreesPerSecond * dt) * Mathf.Sign(angle);
            transform.Rotate(0f, step, 0f, Space.World);
            if (Mathf.Abs(angle) - Mathf.Abs(step) <= 0.01f)
            {
                hasAlignGoal = false;
                Aligned?.Invoke();
            }
        }

        /// <summary>
        /// Cinemática do rover, sem estado oculto. Velocidade e giro chegam ao alvo por rampas (aceleração ao partir,
        /// frenagem ao soltar ou inverter): nada muda de valor num único quadro. Não exige Shift nem precisão.
        /// </summary>
        public static void StepMotion(ref float speed, ref float turnRate, float forwardInput, float steerInput, float dt,
            float maxForward, float maxReverse, float acceleration, float braking, float maxTurn, float turnAcceleration)
        {
            var forward = Mathf.Clamp(forwardInput, -1f, 1f);
            var target = forward >= 0f ? forward * maxForward : forward * maxReverse;
            var speedingUp = Mathf.Abs(target) > Mathf.Abs(speed) && Mathf.Sign(target) == Mathf.Sign(speed == 0f ? target : speed);
            speed = Mathf.MoveTowards(speed, target, (speedingUp ? acceleration : braking) * dt);

            var targetTurn = Mathf.Clamp(steerInput, -1f, 1f) * maxTurn;
            turnRate = Mathf.MoveTowards(turnRate, targetTurn, turnAcceleration * dt);
        }

        void StopImmediately()
        {
            speedMetersPerSecond = 0f;
            turnDegreesPerSecond = 0f;
        }

        // ---- Câmera ----

        void TickOrbit(Vector2 look, float dt)
        {
            if (look.sqrMagnitude > 0f)
            {
                var yawDelta = look.x * orbitDegreesPerCount * lookSensitivity;
                var pitchDelta = -look.y * orbitDegreesPerCount * lookSensitivity;
                orbitYawDegrees = Mathf.DeltaAngle(0f, orbitYawDegrees + yawDelta);
                orbitPitchDegrees = Mathf.Clamp(orbitPitchDegrees + pitchDelta, minPitchDegrees, maxPitchDegrees);
                LookedDegrees += Mathf.Abs(yawDelta) + Mathf.Abs(pitchDelta);
                orbitIdleSeconds = 0f;
                frameYawGoal = null;
                frameHoldRemaining = 0f;
                return;
            }

            if (dt <= 0f)
                return;

            if (frameYawGoal.HasValue)
            {
                orbitYawDegrees = Mathf.MoveTowardsAngle(orbitYawDegrees, frameYawGoal.Value, frameDegreesPerSecond * dt);
                if (Mathf.Abs(Mathf.DeltaAngle(orbitYawDegrees, frameYawGoal.Value)) < 0.5f)
                {
                    frameHoldRemaining -= dt;
                    if (frameHoldRemaining <= 0f)
                        frameYawGoal = null;
                }

                return;
            }

            orbitIdleSeconds += dt;
            if (orbitIdleSeconds < orbitReturnDelaySeconds)
                return;

            var step = orbitReturnDegreesPerSecond * dt;
            orbitYawDegrees = Mathf.MoveTowardsAngle(orbitYawDegrees, 0f, step);
            orbitPitchDegrees = Mathf.MoveTowards(orbitPitchDegrees, Mathf.Clamp(defaultPitchDegrees, minPitchDegrees, maxPitchDegrees), step);
        }

        float YawTo(Vector3 worldPoint)
        {
            var direction = worldPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
                return 0f;

            var forward = transform.forward;
            forward.y = 0f;
            return Vector3.SignedAngle(forward, direction, Vector3.up);
        }

        /// <summary>Pose da câmera de acompanhamento para o estado atual (sem suavização e sem colisão).</summary>
        public CameraPose ComputeIdealCameraPose(float fieldOfViewDegrees)
        {
            var pivot = transform.position + Vector3.up * pivotHeightMeters;
            var rotation = Quaternion.Euler(orbitPitchDegrees, transform.eulerAngles.y + orbitYawDegrees, 0f);
            var position = pivot - rotation * Vector3.forward * followDistanceMeters;
            return new CameraPose(position, rotation, fieldOfViewDegrees);
        }

        /// <summary>
        /// Acompanha o rover com a câmera (depois do movimento do quadro). Não escreve na câmera enquanto uma sequência a controla.
        /// Público para testes; LateUpdate chama com o último delta ativo.
        /// </summary>
        public void TickCamera(float dt)
        {
            if (cameraDirector == null || cameraDirector.TargetCamera == null)
                return;

            if (cameraDirector.IsControlled)
            {
                cameraNeedsResync = true; // uma sequência é dona da câmera; ao voltar, parte da pose em que ela terminou
                return;
            }

            var camera = cameraDirector.TargetCamera;
            var pivot = transform.position + Vector3.up * pivotHeightMeters;
            var ideal = ComputeIdealCameraPose(camera.fieldOfView);

            if (cameraNeedsResync)
            {
                smoothedCameraPosition = camera.transform.position;
                cameraNeedsResync = false;
            }

            var blend = dt > 0f ? 1f - Mathf.Exp(-dt / followSmoothSeconds) : 0f;
            smoothedCameraPosition = Vector3.Lerp(smoothedCameraPosition, ideal.Position, blend);
            var position = ResolveCameraCollision(pivot, smoothedCameraPosition);
            position = cameraDirector.ClampAboveGround(position, cameraGroundClearanceMeters);

            var toPivot = pivot - position;
            var targetRotation = toPivot.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toPivot, Vector3.up) : ideal.Rotation;
            var rotation = Quaternion.Slerp(camera.transform.rotation, targetRotation, blend);
            cameraDirector.Apply(new CameraPose(position, rotation, camera.fieldOfView));
        }

        Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desired)
        {
            var offset = desired - pivot;
            var distance = offset.magnitude;
            if (distance < 1e-4f)
                return desired;

            var direction = offset / distance;
            return Physics.SphereCast(pivot, cameraCollisionRadiusMeters, direction, out var hit, distance, cameraCollisionMask, QueryTriggerInteraction.Ignore)
                ? pivot + direction * Mathf.Max(0.5f, hit.distance)
                : desired;
        }

        // ---- Metas de chegada ----

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            EndGoal();
            raisedGoals.Clear();
            if (binding == null)
                return;

            foreach (var goal in reachGoals)
            {
                if (goal != null && goal.target != null && goal.stepId == binding.StepId)
                {
                    BeginGoal(goal);
                    break;
                }
            }
        }

        void OnStepEnded(string stepId)
        {
            if (activeGoal != null && activeGoal.stepId == stepId)
                EndGoal();
        }

        void BeginGoal(ReachGoal goal)
        {
            activeGoal = goal;
            goalAssist.NoteProgress();
            bestGoalDistance = HorizontalDistance(goal.target.position);
            lastReportedGoalLevel = MarsAssistLevel.None;
            frameTarget = goal.target;
        }

        void EndGoal()
        {
            if (activeGoal == null)
                return;

            if (frameTarget == activeGoal.target)
                frameTarget = null;

            activeGoal = null;
            goalAssist.NoteProgress();
            if (lastReportedGoalLevel != MarsAssistLevel.None)
            {
                lastReportedGoalLevel = MarsAssistLevel.None;
                GoalAssistChanged?.Invoke(MarsAssistLevel.None);
            }
        }

        void TickGoals(float dt)
        {
            if (activeGoal == null || flow == null)
                return;

            var goal = activeGoal;
            var distance = HorizontalDistance(goal.target.position);
            if (distance <= goal.radiusMeters)
            {
                if (raisedGoals.Add(goal) && flow.Raise(goal.eventId))
                    GoalReached?.Invoke(goal.eventId);

                return;
            }

            if (distance < bestGoalDistance - MinimumGoalProgressMeters)
            {
                bestGoalDistance = distance;
                goalAssist.NoteProgress();
            }

            var level = goalAssist.Advance(dt);
            if (level != lastReportedGoalLevel)
            {
                lastReportedGoalLevel = level;
                GoalAssistChanged?.Invoke(level);
            }
        }

        float HorizontalDistance(Vector3 worldPoint)
        {
            var delta = worldPoint - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        // ---- Entrada ----

        void OnHelpDown() => RequestFrame();

        void OnInputCleared()
        {
            // Foco perdido: nenhuma tecla continua valendo e o rover não segue deslizando.
            if (speedMetersPerSecond != 0f || turnDegreesPerSecond != 0f)
                StopImmediately();
        }

        /// <summary>Referências, faixas e metas. Vazio = pronto. Chamado pelo teste do prefab e pelo montador de cena.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsRoverController);
            if (GetComponent<CharacterController>() == null)
                ContractValidation.Report(errors, owner, "falta CharacterController (colisão do rover).");

            ContractValidation.RequirePositive(errors, owner, nameof(maxForwardSpeedMetersPerSecond), maxForwardSpeedMetersPerSecond);
            ContractValidation.RequirePositive(errors, owner, nameof(maxReverseSpeedMetersPerSecond), maxReverseSpeedMetersPerSecond);
            ContractValidation.RequirePositive(errors, owner, nameof(accelerationMetersPerSecondSquared), accelerationMetersPerSecondSquared);
            ContractValidation.RequirePositive(errors, owner, nameof(brakingMetersPerSecondSquared), brakingMetersPerSecondSquared);
            ContractValidation.RequirePositive(errors, owner, nameof(maxTurnDegreesPerSecond), maxTurnDegreesPerSecond);
            ContractValidation.RequirePositive(errors, owner, nameof(turnAccelerationDegreesPerSecondSquared), turnAccelerationDegreesPerSecondSquared);
            ContractValidation.RequirePositive(errors, owner, nameof(alignDegreesPerSecond), alignDegreesPerSecond);
            ContractValidation.RequirePositive(errors, owner, nameof(followDistanceMeters), followDistanceMeters);
            if (minPitchDegrees > maxPitchDegrees)
                ContractValidation.Report(errors, owner, "minPitchDegrees maior que maxPitchDegrees.");

            foreach (var goal in reachGoals)
            {
                if (goal == null)
                {
                    ContractValidation.Report(errors, owner, "meta de chegada vazia.");
                    continue;
                }

                ContractValidation.RequireId(errors, owner, "reachGoal.stepId", goal.stepId);
                ContractValidation.RequireId(errors, owner, "reachGoal.eventId", goal.eventId);
                ContractValidation.RequirePositive(errors, owner, "reachGoal.radiusMeters", goal.radiusMeters);
                if (goal.target == null)
                    ContractValidation.Report(errors, owner, $"meta '{goal.eventId}' sem âncora de destino.");
            }

            return errors;
        }
    }
}
