using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public enum ScoopPhase { Locked, Approaching, Ready, Digging, Retracting, Complete }
    public enum ScoopRetractStage { None, Vertical, Lateral, Rest }

    /// <summary>
    /// SUR-009. Escavação de quatro passes operada por E. Soltar E congela a deformação e o áudio e conserva o avanço;
    /// o recolhimento sobe 8 cm, recua lateralmente e só depois volta ao repouso. Não conclui a etapa: a próxima ação do
    /// solo (SUR-010) e o <see cref="ChapterDirector"/> decidem a saída de <see cref="StepId"/>.
    /// A matemática de sulco e de trajetória fica em <see cref="Shape"/> e <see cref="Path"/> (sem Transform) para ser testada isolada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorScoop : MonoBehaviour, IDisposable
    {
        public const string StepId = "Sample";
        public const string HoldId = "sur.scoop.dig";
        public const string PreparedTrigger = "surveyor.scoop.prepared", GrowingTrigger = "surveyor.scoop.growing";
        public const string PreparedCueId = "SUR-S14", GrowingCueId = "SUR-S15";
        public const float PreparedReadSeconds = 5f, GrowingReadSeconds = 6f;

        public const float TotalDigSeconds = 12f, PassSeconds = 3f, StepSeconds = 1f, StepEaseSeconds = .35f;
        public const int PassCount = 4, StepCount = 12;
        public const float ApproachSeconds = 2.4f, VerticalSeconds = 1f, LateralSeconds = 1.5f, RestSeconds = 1.6f;
        public const float RetractSeconds = VerticalSeconds + LateralSeconds + RestSeconds;
        public const float LiftMeters = .08f, PassLiftMeters = .05f, AboveHeightMeters = .25f, LateralMeters = .32f;
        public const float ClearanceMeters = .02f, ResumeBlendSeconds = .3f;
        public const float DigTiltDegrees = 25f, TravelTiltDegrees = 8f;
        public const float LunarGravityMetersPerSecondSquared = 1.62f;
        public const int GrainPoolSize = 24, GrainsPerStep = 3;
        public const float GrainRestSeconds = 1.5f;

        // Contrato de nomes dos prefabs Trench e ScoopRig (resolvido uma vez em Configure, nunca por quadro).
        public const string TileName = "Tile", GrooveName = "Groove", PhotoTargetName = "PhotoTarget";
        public const string ShoulderPath = "Shoulder", ElbowPath = "Shoulder/Elbow", WristPath = "Shoulder/Elbow/Wrist";
        public const string ScoopToolPath = "Shoulder/Elbow/Wrist/Scoop", TipPath = "Shoulder/Elbow/Wrist/Scoop/Tip";
        public const string RestPosePath = "Poses/Rest", AbovePosePath = "Poses/Above", ContactPosePath = "Poses/Contact";
        public const string GrainsPath = "Grains", MotorPath = "Motor";
        public static readonly string[] BlendShapeNames = { "Pass1", "Pass2", "Pass3", "Pass4" };

        /// <summary>Sulco local: x ao longo do sulco, z na largura, y para cima, origem no centro, plano tangente do terreno.</summary>
        public static class Shape
        {
            public const float LengthMeters = .62f, MaxDepthMeters = .18f, BermMeters = .025f, BucketHalfWidthMeters = .045f, BucketLengthMeters = .14f;
            /// <summary>Extremos do sulco no eixo x: entrada em rampa suave (pá arrasta de StartX a EndX), saída curta.</summary>
            public const float GrooveStartX = -.34f, GrooveEndX = .28f, EntryRampMeters = .30f, ExitRampMeters = .12f;
            public const float DragStartX = -.30f, DragEndX = .20f;
            public static float HalfWidthMeters(int pass) => .1f + .0125f * (pass - 1);

            static float Longitudinal(float x)
            {
                if (x <= GrooveStartX || x >= GrooveEndX) return 0f;
                float a = Mathf.Clamp01((x - GrooveStartX) / EntryRampMeters);
                float b = Mathf.Clamp01((GrooveEndX - x) / ExitRampMeters);
                float entry = Mathf.Lerp(a, Mathf.SmoothStep(0f, 1f, a), .35f);
                return Mathf.Min(entry, Mathf.SmoothStep(0f, 1f, b));
            }

            /// <summary>Menor altura permitida para a ponta da pá: nenhum ponto do balde (que fica atrás da ponta e sobe com a inclinação) entra no relevo.</summary>
            public static float MinTipHeight(float passes, float x, float z, float tiltDegrees)
            {
                float tilt = tiltDegrees * Mathf.Deg2Rad, cos = Mathf.Cos(tilt), sin = Mathf.Sin(tilt);
                float need = float.NegativeInfinity;
                for (int i = 0; i <= 6; i++)
                {
                    float d = BucketLengthMeters * i / 6f;
                    for (int k = -1; k <= 1; k++)
                        need = Mathf.Max(need, Height(passes, x - cos * d, z + k * BucketHalfWidthMeters) - sin * d);
                }
                return need + .0005f;
            }
            public static float DepthMeters(float passes) => MaxDepthMeters * Mathf.Clamp(passes, 0f, PassCount) / PassCount;

            /// <summary>Relevo acumulado depois de <paramref name="pass"/> passes completos, em metros acima do plano local.</summary>
            public static float PassHeight(int pass, float x, float z)
            {
                if (pass <= 0) return 0f;
                if (pass > PassCount) pass = PassCount;
                float halfWidth = HalfWidthMeters(pass);
                float center = (GrooveStartX + GrooveEndX) * .5f, halfLength = (GrooveEndX - GrooveStartX) * .5f;
                float ax = Mathf.Abs(x - center) / halfLength, az = Mathf.Abs(z) / halfWidth;
                float cavity = 0f;
                if (az < 1f) cavity = Longitudinal(x) * Mathf.SmoothStep(0f, 1f, (1f - az) / .55f);
                float berm = 0f;
                if (ax < 1.25f && az > .85f && az < 1.9f)
                {
                    float ring = Mathf.Exp(-Mathf.Pow((az - 1.18f) / .22f, 2f));
                    float taper = 1f - Mathf.SmoothStep(0f, 1f, (ax - .6f) / .65f);
                    berm = BermMeters * pass / PassCount * ring * taper;
                }
                return -MaxDepthMeters * pass / PassCount * cavity + berm;
            }

            /// <summary>Relevo com passes fracionários: interpolação linear dos passes vizinhos, igual ao blend shape.</summary>
            public static float Height(float passes, float x, float z)
            {
                passes = Mathf.Clamp(passes, 0f, PassCount);
                int low = Mathf.FloorToInt(passes);
                if (low >= PassCount) return PassHeight(PassCount, x, z);
                float fraction = passes - low;
                float a = PassHeight(low, x, z);
                return fraction <= 0f ? a : Mathf.Lerp(a, PassHeight(low + 1, x, z), fraction);
            }

            /// <summary>Metade da área escavada e das bordas, para enquadrar a foto de resultado na mesma região.</summary>
            public static Vector3 FootprintSizeMeters => new Vector3(LengthMeters + .3f, MaxDepthMeters + BermMeters, 2f * HalfWidthMeters(PassCount) * 1.9f);
        }

        /// <summary>Trajetória da ponta da pá no quadro local do sulco. Função pura do tempo e do estado.</summary>
        public sealed class Path
        {
            public readonly float SideSign;
            public readonly Vector3 Rest, Above;
            public Vector3 DragDirection => Vector3.right;
            public float StartX => Shape.DragStartX;
            public float EndX => Shape.DragEndX;

            public Path(float sideSign, Vector3 restLocal)
            {
                SideSign = sideSign < 0f ? -1f : 1f;
                Rest = restLocal;
                Above = new Vector3(StartX, AboveHeightMeters, 0f);
            }

            static float Smooth(float u) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u));

            /// <summary>Passes acumulados (0–4). Doze mordidas de 1 s, cada uma suavizada em 0,35 s: sempre há mudança visível em ≤1,2 s.</summary>
            public static float PassesAt(float digSeconds)
            {
                digSeconds = Mathf.Clamp(digSeconds, 0f, TotalDigSeconds);
                if (digSeconds >= TotalDigSeconds) return PassCount;
                float whole = Mathf.Floor(digSeconds / StepSeconds);
                float fraction = digSeconds / StepSeconds - whole;
                float steps = whole + Smooth(fraction * StepSeconds / StepEaseSeconds);
                return Mathf.Min(StepCount, steps) * PassCount / StepCount;
            }

            /// <summary>Quantas mordidas já começaram (1 no primeiro instante de escavação; 0 antes).</summary>
            public static int BitesStarted(float digSeconds) =>
                digSeconds <= 0f ? 0 : Mathf.Min(StepCount, Mathf.FloorToInt(Mathf.Min(digSeconds, TotalDigSeconds - .0001f) / StepSeconds) + 1);

            public Vector3 ApproachTip(float seconds, out float tiltDegrees)
            {
                float u = Smooth(seconds / ApproachSeconds);
                tiltDegrees = Mathf.Lerp(0f, TravelTiltDegrees, u);
                return Vector3.Lerp(Rest, Above, u);
            }

            /// <summary>Ponta no relevo: sobe só o necessário para que o balde inteiro, inclinado, não entre no terreno.</summary>
            static Vector3 Ride(float x, float passes, float lift, float tiltDegrees) =>
                new Vector3(x, Shape.MinTipHeight(passes, x, 0f, tiltDegrees) + lift, 0f);

            /// <summary>Quatro passes de 3 s: descer, arrastar, levantar 5 cm e voltar; o quarto termina em contato.</summary>
            public Vector3 DigTip(float digSeconds, out float tiltDegrees)
            {
                float t = Mathf.Clamp(digSeconds, 0f, TotalDigSeconds);
                int pass = Mathf.Min(PassCount - 1, Mathf.FloorToInt(t / PassSeconds));
                float u = Mathf.Clamp01((t - pass * PassSeconds) / PassSeconds);
                float passes = PassesAt(t);
                const float lowerEnd = .15f, dragEnd = .75f;
                tiltDegrees = TravelTiltDegrees;
                if (u < lowerEnd)
                {
                    float k = Smooth(u / lowerEnd);
                    tiltDegrees = Mathf.Lerp(TravelTiltDegrees, DigTiltDegrees, k);
                    Vector3 from = pass == 0 ? Above : Ride(StartX, pass, PassLiftMeters, TravelTiltDegrees);
                    return Vector3.Lerp(from, Ride(StartX, passes, 0f, tiltDegrees), k);
                }
                if (u < dragEnd)
                {
                    float x = Mathf.Lerp(StartX, EndX, Smooth((u - lowerEnd) / (dragEnd - lowerEnd)));
                    tiltDegrees = DigTiltDegrees;
                    return Ride(x, passes, 0f, tiltDegrees);
                }
                float w = (u - dragEnd) / (1f - dragEnd);
                if (pass == PassCount - 1)
                {
                    tiltDegrees = DigTiltDegrees;
                    return Ride(EndX, passes, 0f, tiltDegrees);
                }
                if (w < .4f)
                {
                    float k = Smooth(w / .4f);
                    tiltDegrees = Mathf.Lerp(DigTiltDegrees, TravelTiltDegrees, k);
                    return Ride(EndX, passes, PassLiftMeters * k, tiltDegrees);
                }
                float back = Smooth((w - .4f) / .6f);
                return Ride(Mathf.Lerp(EndX, StartX, back), passes, PassLiftMeters, tiltDegrees);
            }

            static Vector3 Safe(Vector3 p, float passes, float tiltDegrees)
            {
                p.y = Mathf.Max(p.y, Shape.MinTipHeight(passes, p.x, p.z, tiltDegrees) + ClearanceMeters);
                return p;
            }

            /// <summary>Contato → subida vertical de 8 cm → retração lateral → repouso. O balde nunca entra no relevo deformado.</summary>
            public Vector3 RetractTip(float seconds, Vector3 start, float passes, out ScoopRetractStage stage, out float tiltDegrees)
            {
                tiltDegrees = TravelTiltDegrees;
                Vector3 lifted = start + Vector3.up * LiftMeters;
                if (seconds < VerticalSeconds)
                {
                    stage = ScoopRetractStage.Vertical;
                    float k = Smooth(seconds / VerticalSeconds);
                    tiltDegrees = Mathf.Lerp(DigTiltDegrees, TravelTiltDegrees, k);
                    return Vector3.Lerp(start, lifted, k);
                }
                Vector3 side = Safe(new Vector3(start.x, lifted.y, start.z + SideSign * LateralMeters), passes, TravelTiltDegrees);
                if (seconds < VerticalSeconds + LateralSeconds)
                {
                    stage = ScoopRetractStage.Lateral;
                    return Safe(Vector3.Lerp(lifted, side, Smooth((seconds - VerticalSeconds) / LateralSeconds)), passes, TravelTiltDegrees);
                }
                stage = ScoopRetractStage.Rest;
                float r = Smooth((seconds - VerticalSeconds - LateralSeconds) / RestSeconds);
                return Safe(Vector3.Lerp(side, Rest, r), passes, TravelTiltDegrees);
            }
        }

        /// <summary>Cinemática de dois elos no plano que contém o ombro, o alvo e o polo. Devolve false quando o alvo foi limitado.</summary>
        public static bool SolveTwoBone(Vector3 shoulder, Vector3 target, float upperLength, float foreLength, Vector3 pole, out Vector3 elbow)
        {
            Vector3 d = target - shoulder;
            float length = d.magnitude;
            float min = Mathf.Abs(upperLength - foreLength) + .001f, max = upperLength + foreLength - .001f;
            bool reachable = length >= min && length <= max;
            if (length < 1e-5f) { elbow = shoulder + pole.normalized * upperLength; return false; }
            Vector3 dir = d / length;
            length = Mathf.Clamp(length, min, max);
            Vector3 side = pole - dir * Vector3.Dot(pole, dir);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(dir, Vector3.right);
            side.Normalize();
            float cos = Mathf.Clamp((upperLength * upperLength + length * length - foreLength * foreLength) / (2f * upperLength * length), -1f, 1f);
            float sin = Mathf.Sqrt(1f - cos * cos);
            elbow = shoulder + upperLength * (dir * cos + side * sin);
            return reachable;
        }

        // ---- serviços e referências ----
        ChapterDirector owner; SessionClock clock; InputRouter input; InteractionService service;
        Transform trench, photoTarget, shoulder, elbow, wrist, tool, tip, restPose, abovePose, contactPose, grainRoot;
        SkinnedMeshRenderer groove; MeshRenderer tile; AudioSource motor; Renderer[] replacedGround, hiddenNative; Transform[] grains;
        int[] blendIndex; Path path; float upperLength, foreLength; Vector3 tipInTool;
        IDisposable inputLease; bool configured, installed, bound, running, ownsLease;
        InteractionCandidate candidate; bool offered; float attemptStart;
        int generation;

        // ---- estado ----
        float digSeconds, retractSeconds, approachSeconds, resumeStartSeconds = -1f;
        ScoopRetractStage retractStage; Vector3 retractStart, hoverPoint; float retractPasses;
        bool retractCompletes, resumePending, motorPaused;
        int bitesFired; float cueRemaining;
        readonly Queue<SubtitleCue> pendingCues = new Queue<SubtitleCue>();
        SubtitleCue activeCue;
        readonly SubtitleCue preparedCue = SubtitleCue.Triggered(PreparedCueId, PreparedCueId, PreparedTrigger, PreparedReadSeconds);
        readonly SubtitleCue growingCue = SubtitleCue.Triggered(GrowingCueId, GrowingCueId, GrowingTrigger, GrowingReadSeconds, priority: 1);
        Vector3[] grainPosition, grainVelocity; float[] grainAge; byte[] grainState;
        float motorVolume = .06f;

        public event Action Changed;
        public event Action<int> DepthStepped;
        public event Action Completed;
        public event Action<SubtitleCue> CueEntered, CueExited;

        public ScoopPhase Phase { get; private set; }
        public bool Running => running;
        public bool IsPaused => owner != null && owner.IsPaused;
        public bool IsHolding => offered && service != null && service.HoldingId == candidate.Id;
        public bool CanDig => offered && service != null && service.CanBeginHold(candidate.Id);
        public bool NeedsRelease => offered && service != null && service.AwaitingRelease;
        public bool IsDigPaused => Phase == ScoopPhase.Digging && digSeconds > 0f && !IsHolding;
        public bool Canceled { get; private set; }
        public float DigSeconds => digSeconds;
        public float DigFraction => Mathf.Clamp01(digSeconds / TotalDigSeconds);
        public float Passes => Path.PassesAt(digSeconds);
        public int PassIndex => digSeconds <= 0f ? 0 : Mathf.Min(PassCount, Mathf.FloorToInt(Mathf.Min(digSeconds, TotalDigSeconds - .0001f) / PassSeconds) + 1);
        public int BitesStarted => Path.BitesStarted(digSeconds);
        public float DepthMeters => Shape.DepthMeters(Passes);
        public float RetractFraction => Mathf.Clamp01(retractSeconds / RetractSeconds);
        public ScoopRetractStage RetractStage => retractStage;
        public int ActiveGrains { get { int n = 0; if (grainState != null) for (int i = 0; i < grainState.Length; i++) if (grainState[i] != 0) n++; return n; } }
        public string SubtitleKey => activeCue?.TextKey;
        public Transform TrenchCenter => photoTarget;
        public Transform TrenchRoot => trench;
        public Vector3 TipWorld => tip != null ? tip.position : Vector3.zero;
        public bool MotorShouldPlay => running && !IsPaused && (IsHolding && Phase == ScoopPhase.Digging || Phase == ScoopPhase.Retracting);
        public float MotorVolume { get => motorVolume; set => motorVolume = Mathf.Clamp01(value); }
        /// <summary>Folga em metros entre a ponta e o relevo deformado no quadro local do sulco (QA: nunca negativa).</summary>
        public float TipClearanceMeters { get; private set; }
        public float ReachErrorMeters { get; private set; }
        public Path Trajectory => path;
        public Bounds FootprintWorldBounds => trench == null ? default : new Bounds(photoTarget != null ? photoTarget.position : trench.position, Shape.FootprintSizeMeters);

        public void Configure(ChapterDirector chapter, SessionClock session, InputRouter router, InteractionService interactions,
            GameObject trenchInstance, GameObject rigInstance, Renderer[] groundToReplace, Renderer[] nativeScoopParts)
        {
            if (chapter == null || session == null || router == null || interactions == null)
                throw new ArgumentException("SurveyorScoop: serviços Core ausentes.");
            ConfigurePreview(trenchInstance, rigInstance, groundToReplace, nativeScoopParts);
            owner = chapter; clock = session; input = router; service = interactions;
            Bind();
            if (owner.State == AppState.Playing && owner.CurrentStepId == StepId) Enter();
        }

        /// <summary>Só a parte visual (sulco, braço, grãos), sem serviços de jogo: capturas de QA e conferência de montagem no Editor.
        /// Use <see cref="PreviewDig"/> e <see cref="PreviewRetract"/> depois dela.</summary>
        public void ConfigurePreview(GameObject trenchInstance, GameObject rigInstance, Renderer[] groundToReplace, Renderer[] nativeScoopParts)
        {
            Dispose();
            replacedGround = groundToReplace ?? Array.Empty<Renderer>(); hiddenNative = nativeScoopParts ?? Array.Empty<Renderer>();
            var errors = Resolve(trenchInstance, rigInstance);
            if (errors.Count > 0) throw new ArgumentException("SurveyorScoop:\n" + string.Join("\n", errors));
            configured = true;
            Install(true);
            ResetState(true);
        }

        List<string> Resolve(GameObject trenchInstance, GameObject rigInstance)
        {
            var errors = new List<string>();
            if (trenchInstance == null) errors.Add("Instância de Trench ausente.");
            if (rigInstance == null) errors.Add("Instância de ScoopRig ausente.");
            if (errors.Count > 0) return errors;
            trench = trenchInstance.transform; var rig = rigInstance.transform;
            tile = Find<MeshRenderer>(trench, TileName, errors); groove = Find<SkinnedMeshRenderer>(trench, GrooveName, errors);
            photoTarget = Find<Transform>(trench, PhotoTargetName, errors);
            shoulder = Find<Transform>(rig, ShoulderPath, errors); elbow = Find<Transform>(rig, ElbowPath, errors); wrist = Find<Transform>(rig, WristPath, errors);
            tool = Find<Transform>(rig, ScoopToolPath, errors); tip = Find<Transform>(rig, TipPath, errors);
            restPose = Find<Transform>(rig, RestPosePath, errors); abovePose = Find<Transform>(rig, AbovePosePath, errors); contactPose = Find<Transform>(rig, ContactPosePath, errors);
            grainRoot = Find<Transform>(rig, GrainsPath, errors); motor = Find<AudioSource>(rig, MotorPath, errors);
            if (errors.Count > 0) return errors;
            blendIndex = new int[BlendShapeNames.Length];
            var mesh = groove.sharedMesh;
            for (int i = 0; i < blendIndex.Length; i++)
            {
                blendIndex[i] = mesh != null ? mesh.GetBlendShapeIndex(BlendShapeNames[i]) : -1;
                if (blendIndex[i] < 0) errors.Add("Blend shape ausente: " + BlendShapeNames[i]);
            }
            if (grainRoot.childCount < GrainPoolSize) errors.Add("O ScoopRig precisa de " + GrainPoolSize + " grãos.");
            if (errors.Count > 0) return errors;
            grains = new Transform[GrainPoolSize];
            for (int i = 0; i < GrainPoolSize; i++) grains[i] = grainRoot.GetChild(i);
            grainPosition = new Vector3[GrainPoolSize]; grainVelocity = new Vector3[GrainPoolSize]; grainAge = new float[GrainPoolSize]; grainState = new byte[GrainPoolSize];
            upperLength = elbow.localPosition.magnitude; foreLength = wrist.localPosition.magnitude; tipInTool = tool.InverseTransformPoint(tip.position);
            if (upperLength < .2f || foreLength < .2f) errors.Add("Elos do braço inválidos.");
            // Toda a trajetória vive no quadro do sulco; o ombro decide de que lado a pá arrasta e recua.
            Vector3 shoulderLocal = trench.InverseTransformPoint(shoulder.position);
            path = new Path(shoulderLocal.z, trench.InverseTransformPoint(restPose.position));
            abovePose.position = trench.TransformPoint(path.Above);
            contactPose.position = trench.TransformPoint(new Vector3(path.StartX, 0f, 0f));
            foreach (var group in new[] { abovePose, contactPose, restPose })
                group.rotation = trench.rotation * Quaternion.LookRotation(path.DragDirection, Vector3.up);
            // A foto usa a mesma âncora: o prefab foi assado para a âncora Trench do SUR-002.
            return errors;
        }

        static T Find<T>(Transform root, string path, List<string> errors) where T : Component
        {
            var child = root.Find(path);
            var component = child != null ? child.GetComponent<T>() : null;
            if (component == null) errors.Add("Caminho ausente em " + root.name + ": " + path + " (" + typeof(T).Name + ")");
            return component;
        }

        /// <summary>Conferência de montagem para o Editor/testes: âncora, alcance e relevo.</summary>
        public List<string> ValidateSetup(Transform trenchAnchor, float anchorToleranceMeters = .06f)
        {
            var errors = new List<string>();
            if (!configured) { errors.Add("Configure antes de validar."); return errors; }
            if (trenchAnchor == null) errors.Add("Âncora SUR.Trench ausente.");
            else
            {
                var delta = photoTarget.position - trenchAnchor.position; delta.y = 0f;
                if (delta.magnitude > anchorToleranceMeters) errors.Add("O Trench foi assado para outra âncora (" + delta.magnitude.ToString("0.000") + " m).");
            }
            float reach = upperLength + foreLength - .01f;
            foreach (var p in new[] { path.Rest, path.Above, new Vector3(path.StartX, 0, 0), new Vector3(path.EndX, -Shape.DepthMeters(PassCount), 0), new Vector3(path.EndX, LiftMeters, path.SideSign * LateralMeters) })
                if ((trench.TransformPoint(p) - shoulder.position).magnitude > reach) errors.Add("Ponto fora do alcance do braço: " + p);
            return errors;
        }

        // ---- ciclo de vida ----
        void Install(bool on)
        {
            if (!configured || installed == on) return;
            installed = on;
            foreach (var r in replacedGround) if (r != null) r.enabled = !on;
            foreach (var r in hiddenNative) if (r != null) r.enabled = !on;
            tile.enabled = on; groove.enabled = on; shoulder.gameObject.SetActive(on); trench.gameObject.SetActive(on);
            if (!on) { motor.Stop(); for (int i = 0; i < grains.Length; i++) grains[i].gameObject.SetActive(false); }
        }

        void Bind()
        {
            if (bound || owner == null) return;
            bound = true;
            owner.StepEntered += OnStep; owner.ChapterUnloading += OnUnloading; owner.PausedChanged += OnPausedChanged;
            service.HoldStarted += OnHoldStarted; service.HoldProgress += OnHoldProgress; service.HoldCanceled += OnHoldCanceled; service.HoldCompleted += OnHoldCompleted;
            input.InteractDown += OnFreshPress;
        }

        void Unbind()
        {
            if (!bound) return;
            bound = false;
            owner.StepEntered -= OnStep; owner.ChapterUnloading -= OnUnloading; owner.PausedChanged -= OnPausedChanged;
            service.HoldStarted -= OnHoldStarted; service.HoldProgress -= OnHoldProgress; service.HoldCanceled -= OnHoldCanceled; service.HoldCompleted -= OnHoldCompleted;
            input.InteractDown -= OnFreshPress;
        }

        void OnStep(string id) { if (id == StepId) Enter(); else if (running) Stop(false); }
        void OnUnloading(string id) => Stop(true);
        void OnPausedChanged(bool paused) { RefreshMotor(); Changed?.Invoke(); }

        void Enter()
        {
            if (!configured) return;
            StopInternal(false);
            running = true; generation++; Canceled = false;
            inputLease = input.Acquire(InputContext.Panel, "SUR-009.Scoop"); ownsLease = true;
            if (Phase == ScoopPhase.Complete) { Changed?.Invoke(); return; }
            BeginApproach();
            Changed?.Invoke();
        }

        void BeginApproach()
        {
            approachSeconds = 0f; retractSeconds = 0f; retractStage = ScoopRetractStage.None;
            Phase = ScoopPhase.Approaching; Canceled = false;
            resumeStartSeconds = -1f; resumePending = false;
            if (digSeconds > 0f)
            {
                float tilt; Vector3 resume = path.DigTip(digSeconds, out tilt);
                hoverPoint = resume + Vector3.up * .12f; resumePending = true;
            }
            ApplyApproach(0f);
        }

        /// <summary>Cancelamento animado: a pá sobe, recua e volta ao repouso sem atravessar o relevo; o avanço do sulco é mantido.</summary>
        public void Cancel()
        {
            if (!running || Phase == ScoopPhase.Retracting || Phase == ScoopPhase.Complete || Phase == ScoopPhase.Locked) return;
            DropOffer();
            Canceled = true;
            BeginRetract(false);
        }

        void BeginRetract(bool completes)
        {
            float tilt;
            DropOffer();
            retractPasses = Passes;
            if (Phase == ScoopPhase.Approaching) { retractStart = path.ApproachTip(approachSeconds, out tilt); }
            else if (resumeStartSeconds >= 0f && digSeconds - resumeStartSeconds < ResumeBlendSeconds) retractStart = ResumeAwareDigTip(out tilt);
            else retractStart = path.DigTip(digSeconds, out tilt);
            retractCompletes = completes; retractSeconds = 0f; retractStage = ScoopRetractStage.Vertical;
            Phase = ScoopPhase.Retracting;
            RefreshMotor();
            ApplyRetract(0f);
            Changed?.Invoke();
        }

        Vector3 ResumeAwareDigTip(out float tilt)
        {
            Vector3 dig = path.DigTip(digSeconds, out tilt);
            if (resumeStartSeconds < 0f) return dig;
            float w = 1f - Mathf.SmoothStep(0f, 1f, (digSeconds - resumeStartSeconds) / ResumeBlendSeconds);
            return w <= 0f ? dig : Vector3.Lerp(dig, hoverPoint, w);
        }

        void StopInternal(bool reset)
        {
            generation++;
            running = false; DropOffer();
            if (service != null && service.HoldingId != null && service.HoldingId.StartsWith("sur.scoop.")) service.CancelHold();
            inputLease?.Dispose(); inputLease = null; ownsLease = false;
            if (activeCue != null) { var cue = activeCue; activeCue = null; CueExited?.Invoke(cue); }
            pendingCues.Clear(); cueRemaining = 0f;
            if (motor != null) { motor.Stop(); motorPaused = false; }
            if (grainState != null) for (int i = 0; i < grainState.Length; i++) { grainState[i] = 0; grains[i].gameObject.SetActive(false); }
        }

        /// <summary>Sai da etapa. Sem <paramref name="reset"/>, o sulco e a pose final permanecem para as próximas cenas; a pá nunca fica abaixo do relevo.</summary>
        public void Stop(bool reset)
        {
            if (!configured) { StopInternal(reset); return; }
            StopInternal(reset);
            if (reset) { ResetState(true); }
            else if (Phase == ScoopPhase.Retracting)
            {
                Phase = retractCompletes ? ScoopPhase.Complete : ScoopPhase.Locked; ApplyRest();
                if (Phase == ScoopPhase.Complete) Completed?.Invoke();
            }
            else if (Phase != ScoopPhase.Complete) { Phase = ScoopPhase.Locked; ApplyRest(); }
            Changed?.Invoke();
        }

        void ResetState(bool apply)
        {
            digSeconds = 0f; retractSeconds = 0f; approachSeconds = 0f; bitesFired = 0; resumeStartSeconds = -1f;
            Phase = ScoopPhase.Locked; retractStage = ScoopRetractStage.None; Canceled = false; retractCompletes = false;
            for (int i = 0; i < grainState.Length; i++) { grainState[i] = 0; grains[i].gameObject.SetActive(false); }
            if (apply) { ApplyWeights(0f); ApplyRest(); }
        }

        void ApplyRest()
        {
            retractSeconds = RetractSeconds;
            Pose(path.Rest, TravelTiltDegrees);
            ApplyWeights(Passes);
        }

        // ---- interação ----
        void Offer()
        {
            if (offered || !running) return;
            candidate = new InteractionCandidate
            {
                Id = HoldId, VerbKey = "scoop.dig", ObjectKey = "scoop.trench", Anchor = photoTarget, Available = true, Priority = 100,
                HoldSeconds = Mathf.Max(0f, TotalDigSeconds - digSeconds), DistanceMeters = Vector3.Distance(shoulder.position, photoTarget.position),
            };
            offered = true;
            service.SetCandidate(candidate);
            if (input.IsInteractPhysicallyHeld) service.RequireRelease();
        }

        void DropOffer()
        {
            if (!offered) return;
            offered = false;
            if (service != null) service.ClearCandidate(HoldId);
        }

        /// <summary>Equivalente de botão: o mesmo hold do E.</summary>
        public bool BeginDig() => CanDig && service.BeginHold(candidate.Id);
        public void ReleaseDig() { if (service != null && service.Mode == HoldMode.Hold && IsHolding) service.CancelHold(); }

        void OnFreshPress()
        {
            if (running && offered && service.AwaitingRelease && !IsHolding && !IsPaused)
            { service.NotifyInteractReleased(); BeginDig(); }
        }

        bool Mine(string id) => offered && id == HoldId;

        void OnHoldStarted(string id)
        {
            if (!Mine(id) || (Phase != ScoopPhase.Ready && Phase != ScoopPhase.Digging)) return;
            attemptStart = digSeconds;
            Phase = ScoopPhase.Digging;
            if (resumePending) { resumeStartSeconds = digSeconds; resumePending = false; }
            AdvanceDig(digSeconds, true);
            RefreshMotor(); Changed?.Invoke();
        }

        void OnHoldProgress(string id, float value)
        {
            if (!Mine(id) || Phase != ScoopPhase.Digging) return;
            AdvanceDig(attemptStart + (TotalDigSeconds - attemptStart) * Mathf.Clamp01(value), false);
        }

        void OnHoldCanceled(string id)
        {
            if (!Mine(id) || Phase != ScoopPhase.Digging) { if (Mine(id)) RegisterRemaining(); return; }
            RegisterRemaining();
            RefreshMotor(); Changed?.Invoke();
        }

        void OnHoldCompleted(string id)
        {
            if (!Mine(id) || Phase != ScoopPhase.Digging) return;
            AdvanceDig(TotalDigSeconds, false);
            BeginRetract(true);
        }

        void RegisterRemaining()
        {
            if (!offered) return;
            candidate.HoldSeconds = Mathf.Max(0f, TotalDigSeconds - digSeconds);
            service.SetCandidate(candidate);
            if (input.IsInteractPhysicallyHeld) service.RequireRelease();
        }

        // ---- avanço ----
        void Update() => Tick(clock != null ? clock.ActiveDeltaSeconds : 0f);

        /// <summary>Avança com tempo ativo. A escavação em si avança pelo hold do <see cref="InteractionService"/>; aqui ficam aproximação, recolhimento, grãos e legendas.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (!running || IsPaused || !(activeDeltaSeconds > 0f)) return;
            float dt = Mathf.Min(activeDeltaSeconds, SessionClock.MaxFrameDeltaSeconds);
            switch (Phase)
            {
                case ScoopPhase.Approaching:
                    approachSeconds += dt;
                    ApplyApproach(approachSeconds);
                    if (approachSeconds >= ApproachSeconds) { Phase = ScoopPhase.Ready; Offer(); Raise(preparedCue); Changed?.Invoke(); }
                    break;
                case ScoopPhase.Retracting:
                    retractSeconds = Mathf.Min(RetractSeconds, retractSeconds + dt);
                    ApplyRetract(retractSeconds);
                    if (retractSeconds >= RetractSeconds) FinishRetract();
                    break;
            }
            StepGrains(dt); StepCue(dt); RefreshMotor();
        }

        void FinishRetract()
        {
            if (retractCompletes)
            {
                Phase = ScoopPhase.Complete; retractStage = ScoopRetractStage.None;
                DropOffer(); Completed?.Invoke(); Changed?.Invoke();
                return;
            }
            // Cancelado: a pá está no repouso e o avanço do sulco foi mantido; uma nova oferta recomeça pela aproximação.
            BeginApproach();
            Changed?.Invoke();
        }

        void AdvanceDig(float seconds, bool force)
        {
            seconds = Mathf.Clamp(seconds, 0f, TotalDigSeconds);
            if (!force && Mathf.Approximately(seconds, digSeconds)) return;
            digSeconds = Mathf.Max(digSeconds, seconds);
            float tilt; Vector3 tipLocal = ResumeAwareDigTip(out tilt);
            Pose(tipLocal, tilt); ApplyWeights(Passes);
            int bites = BitesStarted;
            while (bitesFired < bites)
            {
                bitesFired++;
                EmitGrains(bitesFired, tipLocal);
                DepthStepped?.Invoke(bitesFired);
                if (bitesFired == 1) Raise(growingCue);
            }
            Changed?.Invoke();
        }

        void ApplyApproach(float seconds)
        {
            float tilt; Vector3 tipLocal = path.ApproachTip(seconds, out tilt);
            if (digSeconds > 0f)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seconds / ApproachSeconds));
                tipLocal = Vector3.Lerp(path.Rest, hoverPoint, u);
            }
            Pose(tipLocal, tilt); ApplyWeights(Passes);
        }

        void ApplyRetract(float seconds)
        {
            float tilt; ScoopRetractStage stage;
            Vector3 tipLocal = path.RetractTip(seconds, retractStart, retractPasses, out stage, out tilt);
            retractStage = stage;
            Pose(tipLocal, tilt); ApplyWeights(retractPasses);
        }

        // ---- pose e malha ----
        /// <summary>SUR-010 consumes the same arm only after digging and safe retraction have finished.
        /// Coordinates are local to Trench; this never changes excavation progress or grants input.</summary>
        public bool PositionForSoilTest(Vector3 tipLocal, float tiltDegrees = TravelTiltDegrees)
        {
            if (!configured || Phase != ScoopPhase.Complete || IsPaused) return false;
            tipLocal.y = Mathf.Max(tipLocal.y, Shape.MinTipHeight(Passes, tipLocal.x, tipLocal.z, tiltDegrees));
            Pose(tipLocal, tiltDegrees);
            return true;
        }

        void Pose(Vector3 tipLocal, float tiltDegrees)
        {
            Quaternion local = Quaternion.LookRotation(path.DragDirection, Vector3.up) * Quaternion.Euler(tiltDegrees, 0f, 0f);
            Quaternion world = trench.rotation * local;
            Vector3 tipWorld = trench.TransformPoint(tipLocal);
            Vector3 wristWorld = tipWorld - world * tipInTool;
            Vector3 elbowWorld;
            bool reachable = SolveTwoBone(shoulder.position, wristWorld, upperLength, foreLength, trench.up, out elbowWorld);
            Vector3 pole = trench.up;
            shoulder.rotation = Quaternion.LookRotation(elbowWorld - shoulder.position, pole);
            elbow.rotation = Quaternion.LookRotation(wristWorld - elbowWorld, pole);
            tool.rotation = world;
            ReachErrorMeters = Vector3.Distance(tip.position, tipWorld);
            TipClearanceMeters = tipLocal.y - Shape.Height(Passes, tipLocal.x, tipLocal.z);
        }

        void ApplyWeights(float passes)
        {
            passes = Mathf.Clamp(passes, 0f, PassCount);
            for (int i = 0; i < blendIndex.Length; i++)
                groove.SetBlendShapeWeight(blendIndex[i], 100f * Mathf.Clamp01(passes - i));
        }

        // ---- grãos balísticos ----
        static float Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u + 1274126177u;
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        void EmitGrains(int bite, Vector3 origin)
        {
            for (int n = 0; n < GrainsPerStep; n++)
            {
                int slot = FreeGrain(); if (slot < 0) return;
                float angle = (Hash01(bite, n * 7 + 1) - .5f) * 2.4f;
                float speed = .55f + .35f * Hash01(bite, n * 7 + 2);
                float up = .55f + .45f * Hash01(bite, n * 7 + 3);
                Vector3 sideways = new Vector3(Mathf.Cos(angle) * .4f, 0f, Mathf.Sin(angle)) * speed * .6f;
                grainPosition[slot] = origin + new Vector3(0f, .012f, 0f);
                grainVelocity[slot] = sideways + Vector3.up * up;
                grainAge[slot] = 0f; grainState[slot] = 1;
                grains[slot].gameObject.SetActive(true);
                grains[slot].position = trench.TransformPoint(grainPosition[slot]);
            }
        }

        int FreeGrain()
        {
            for (int i = 0; i < grainState.Length; i++) if (grainState[i] == 0) return i;
            int oldest = 0; for (int i = 1; i < grainAge.Length; i++) if (grainAge[i] > grainAge[oldest]) oldest = i;
            return oldest;
        }

        void StepGrains(float dt)
        {
            for (int i = 0; i < grainState.Length; i++)
            {
                if (grainState[i] == 0) continue;
                grainAge[i] += dt;
                if (grainState[i] == 1)
                {
                    grainVelocity[i] += Vector3.down * (LunarGravityMetersPerSecondSquared * dt);
                    grainPosition[i] += grainVelocity[i] * dt;
                    float ground = Shape.Height(Passes, grainPosition[i].x, grainPosition[i].z) + .006f;
                    if (grainPosition[i].y <= ground && grainVelocity[i].y < 0f)
                    { grainPosition[i].y = ground; grainVelocity[i] = Vector3.zero; grainState[i] = 2; grainAge[i] = 0f; }
                }
                else if (grainAge[i] >= GrainRestSeconds) { grainState[i] = 0; grains[i].gameObject.SetActive(false); continue; }
                grains[i].position = trench.TransformPoint(grainPosition[i]);
            }
        }

        // ---- legendas e áudio ----
        void Raise(SubtitleCue cue)
        {
            if (activeCue == null) { Present(cue); return; }
            pendingCues.Enqueue(cue);
        }

        void Present(SubtitleCue cue)
        {
            activeCue = cue; cueRemaining = cue.MinimumReadSeconds;
            CueEntered?.Invoke(cue); Changed?.Invoke();
        }

        void StepCue(float dt)
        {
            if (activeCue == null) return;
            cueRemaining -= dt;
            if (cueRemaining > 0f) return;
            var done = activeCue; activeCue = null; CueExited?.Invoke(done);
            if (pendingCues.Count > 0) Present(pendingCues.Dequeue());
            Changed?.Invoke();
        }

        void RefreshMotor()
        {
            if (motor == null || !Application.isPlaying) return;
            bool play = MotorShouldPlay;
            motor.volume = play ? motorVolume : 0f;
            if (play)
            {
                if (motorPaused) { motor.UnPause(); motorPaused = false; }
                else if (!motor.isPlaying) motor.Play();
            }
            else if (motor.isPlaying) { motor.Pause(); motorPaused = true; }
        }

        public void Dispose()
        {
            if (!configured) return;
            Stop(true);
            Unbind();
            Install(false);
            configured = false;
        }

        void OnEnable() { if (configured) { Install(true); Bind(); } }
        void OnDisable() => Dispose();

        // ---- QA / Editor ----
        /// <summary>Posiciona a pá e o sulco em um instante da escavação (0–12 s) sem alterar o estado de jogo. Para capturas a cada 10%.</summary>
        public void PreviewDig(float seconds)
        {
            if (!configured) return;
            if (running) return;
            float tilt; digSeconds = Mathf.Clamp(seconds, 0f, TotalDigSeconds);
            Vector3 tipLocal = path.DigTip(digSeconds, out tilt);
            Pose(tipLocal, tilt); ApplyWeights(Passes);
        }

        /// <summary>Posiciona o recolhimento em 0–1 a partir do contato final da quarta passada.</summary>
        public void PreviewRetract(float fraction)
        {
            if (!configured || running) return;
            float tilt; ScoopRetractStage stage;
            digSeconds = TotalDigSeconds;
            Vector3 start = path.DigTip(TotalDigSeconds, out tilt);
            Vector3 tipLocal = path.RetractTip(Mathf.Clamp01(fraction) * RetractSeconds, start, PassCount, out stage, out tilt);
            Pose(tipLocal, tilt); ApplyWeights(PassCount);
        }
    }
}
