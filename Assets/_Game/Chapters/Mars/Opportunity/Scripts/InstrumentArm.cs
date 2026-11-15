using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>
    /// Braço IDD do modelo MER (RIG.md). O capítulo move <see cref="target"/> (a ponta da RAT, em coordenadas do mundo) e o braço real leva
    /// <c>Anchor_RAT_Contact</c> até lá com a broca na vertical. Os ângulos estão na convenção da Unity (o clipe <c>RigDemo</c> do RIG.md está
    /// na do Blender, com o sinal de Y trocado): o ombro abre para +Y, o cotovelo para −Y.
    /// <para>Sequência: guardado → desdobra por um clipe fixo (que não toca o rover nem a rocha à distância de
    /// <see cref="RoverController.WorkDistance"/>) → pronto, com a ferramenta erguida à frente → segue o alvo por cinemática inversa. A volta é o inverso.</para>
    /// <para>Geometria: cotovelo e punho giram em Y; com punho = −90° − cotovelo o eixo da torre fica paralelo ao da elevação do ombro e
    /// torre = −elevação mantém a broca apontada para baixo. Restam azimute, elevação e cotovelo para a posição (solução fechada + bisseção).</para>
    /// </summary>
    public sealed class InstrumentArm : MonoBehaviour
    {
        /// <summary>Alcance horizontal útil da ponta da RAT a partir do ombro (m), com a broca na vertical.</summary>
        public const float Reach = .76f;
        const float EngageDistance = .05f, UnfoldSeconds = 1.6f, TrackSpeed = 85f;
        const float ReadyYaw = 73, ReadyLift = -33, ReadyElbow = -100;
        const float MinLift = -36, MaxLift = 4, MinElbow = -168, MaxElbow = -15, MinYaw = 20, MaxYaw = 125;
        // Fora deste setor à frente do ombro o braço espera na pose pronta, em vez de girar por baixo do deck.
        const float MinAzimuth = -40, MaxAzimuth = 50, MinDistance = .38f;
        [SerializeField] Transform shoulder, elbow, wrist, turret, rat, contact, target;
        public Transform Shoulder => shoulder;
        Vector3 restLocal, upperArm, foreArm, hand;
        float tool, unfold, yaw, lift, elbowYaw, wristYaw, turretPitch;
        bool initialized;

        void Initialize()
        {
            restLocal = target.localPosition;
            upperArm = elbow.localPosition;
            foreArm = wrist.localPosition;
            hand = turret.localPosition;
            tool = -(rat.localPosition.y + contact.localPosition.y);
            initialized = true;
        }

        void LateUpdate()
        {
            if (!initialized) Initialize();
            bool engaged = Vector3.Distance(target.position, target.parent.TransformPoint(restLocal)) > EngageDistance;
            float dt = Time.deltaTime;
            if (engaged && unfold < 1)
            {
                unfold = Mathf.MoveTowards(unfold, 1, dt / UnfoldSeconds);
                Clip(unfold);
            }
            else if (engaged)
            {
                Track(dt, true);
            }
            else if (unfold < 1 || Track(dt, false))
            {
                // Só dobra depois de voltar à pose pronta.
                unfold = Mathf.MoveTowards(unfold, 0, dt / UnfoldSeconds);
                Clip(unfold);
            }

            shoulder.localRotation = Quaternion.Euler(0, yaw, lift);
            elbow.localRotation = Quaternion.Euler(0, elbowYaw, 0);
            wrist.localRotation = Quaternion.Euler(0, wristYaw, 0);
            turret.localRotation = Quaternion.Euler(turretPitch, 0, 0);
        }

        /// <summary>Clipe guardado (0) → pronto (1): gira o ombro com o braço dobrado, ergue, e só então abre o cotovelo.</summary>
        void Clip(float u)
        {
            float swing = Mathf.SmoothStep(0, 1, u / .42f), raise = Mathf.SmoothStep(0, 1, (u - .42f) / .26f), open = Mathf.SmoothStep(0, 1, (u - .62f) / .38f);
            yaw = ReadyYaw * swing;
            lift = ReadyLift * raise;
            elbowYaw = ReadyElbow * open;
            wristYaw = (-90 - elbowYaw) * swing;
            turretPitch = -lift;
        }

        /// <summary>Aproxima as juntas da solução para o alvo (<paramref name="follow"/>) ou da pose pronta. Devolve se já chegou.</summary>
        bool Track(float dt, bool follow)
        {
            float goalYaw = ReadyYaw, goalLift = ReadyLift, goalElbow = ReadyElbow;
            if (follow) Solve(shoulder.parent.InverseTransformPoint(target.position) - shoulder.localPosition, ref goalYaw, ref goalLift, ref goalElbow);
            float step = TrackSpeed * dt;
            yaw = Mathf.MoveTowards(yaw, goalYaw, step);
            lift = Mathf.MoveTowards(lift, goalLift, step);
            elbowYaw = Mathf.MoveTowards(elbowYaw, goalElbow, step * 1.5f);
            wristYaw = -90 - elbowYaw;
            turretPitch = -lift;
            return Mathf.Abs(yaw - goalYaw) + Mathf.Abs(lift - goalLift) + Mathf.Abs(elbowYaw - goalElbow) < .5f;
        }

        /// <summary>
        /// Solução para a ponta em <paramref name="goal"/> (relativo ao ombro, no referencial do corpo). Alvo alto ou longe demais: a ponta
        /// fica na direção dele, na altura e distância máximas. Alvo fora do setor de trabalho: mantém a pose pronta recebida.
        /// </summary>
        void Solve(Vector3 goal, ref float goalYaw, ref float goalLift, ref float goalElbow)
        {
            float distance = new Vector2(goal.x, goal.z).magnitude, azimuth = Mathf.Atan2(goal.x, goal.z) * Mathf.Rad2Deg;
            if (distance < MinDistance || azimuth < MinAzimuth || azimuth > MaxAzimuth) return;

            // O alcance cresce com a abertura do cotovelo: bisseção no ângulo do cotovelo.
            float folded = MaxElbow, open = MinElbow, across = 0, along = 0, tilt = 0;
            for (int i = 0; i < 14; i++)
            {
                goalElbow = (folded + open) * .5f;
                Chain(goalElbow, goal.y, out along, out across, out tilt);
                if (new Vector2(along, across).magnitude < distance) folded = goalElbow; else open = goalElbow;
            }

            goalLift = tilt;
            goalYaw = Mathf.Clamp(Mathf.DeltaAngle(Mathf.Atan2(along, across) * Mathf.Rad2Deg, azimuth), MinYaw, MaxYaw);
        }

        /// <summary>
        /// Para um ângulo de cotovelo: elevação do ombro que põe a ponta na altura pedida (limitada) e a posição horizontal da ponta
        /// no referencial do ombro antes do azimute (<paramref name="along"/> em X, <paramref name="across"/> em Z).
        /// </summary>
        void Chain(float elbowAngle, float height, out float along, out float across, out float tilt)
        {
            // Ombro → torre, com o punho em −90° − cotovelo (a torre fica sempre a −90° do braço).
            Vector3 reach = upperArm + Quaternion.Euler(0, elbowAngle, 0) * foreArm + Quaternion.Euler(0, -90, 0) * hand;
            // Altura da ponta = reach.x·sen(e) + reach.y·cos(e) − ferramenta, com e = elevação.
            float radius = new Vector2(reach.x, reach.y).magnitude, phase = Mathf.Atan2(reach.y, reach.x);
            float sine = Mathf.Asin(Mathf.Clamp((height + tool) / radius, -1, 1));
            float first = Mathf.DeltaAngle(0, (sine - phase) * Mathf.Rad2Deg), second = Mathf.DeltaAngle(0, (Mathf.PI - sine - phase) * Mathf.Rad2Deg);
            tilt = Mathf.Clamp(Mathf.Abs(first) < Mathf.Abs(second) ? first : second, MinLift, MaxLift);
            float radians = tilt * Mathf.Deg2Rad;
            along = reach.x * Mathf.Cos(radians) - reach.y * Mathf.Sin(radians);
            across = reach.z;
        }
    }
}
