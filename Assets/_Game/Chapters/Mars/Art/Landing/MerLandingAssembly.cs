using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Um palco de pouso (Spirit em Gusev ou Opportunity em Meridiani): cápsula, escudo, paraquedas, tether, módulo com 28 lobos de
    /// airbag, pétalas e o rover dobrado dentro. <see cref="Apply"/> posiciona tudo a partir de <see cref="MerLandingChoreography"/>;
    /// o componente não tem relógio nem entrada: quem manda o tempo é o <see cref="MarsDualLandingDirector"/>.
    /// Todos os atores permanecem na cena durante o pouso inteiro (nada some entre planos); só o paraquedas e o tether
    /// aparecem quando saem de dentro do casco.
    /// </summary>
    public sealed class MerLandingAssembly : MonoBehaviour
    {
        const float DeflatedLobeHeightScale = 0.05f;
        const float DeflatedRingRadiusMeters = 2.9f * MerLandingLayout.HardwareScale;

        [SerializeField] MerLandingSite site;
        [SerializeField] Transform capsule;
        [SerializeField] Transform heatShield;
        [SerializeField] Transform lander;
        [SerializeField] Transform parachute;
        [SerializeField] Transform tether;
        [SerializeField] Transform fireball;
        [SerializeField] Renderer fireballRenderer;
        [SerializeField] Transform[] retroFlames = Array.Empty<Transform>();
        [SerializeField] Transform[] lobes = Array.Empty<Transform>();
        [SerializeField] Transform core;
        [SerializeField] Transform[] petals = Array.Empty<Transform>();
        [SerializeField] Transform roverRoot;
        [SerializeField, Tooltip("Onde o rover para ao sair do módulo; o jogo (M07) começa aqui.")] Transform egressAnchor;
        [SerializeField] Transform rockerLeft;
        [SerializeField] Transform rockerRight;
        [SerializeField] Transform bogieLeft;
        [SerializeField] Transform bogieRight;
        [SerializeField] Transform mastHead;
        [SerializeField] Transform antennaDish;
        [SerializeField, Tooltip("Rocker: graus de dobra no rover guardado (rodas recolhidas).")] float rockerFoldDegrees = 32f;
        [SerializeField] float bogieFoldDegrees = -48f;
        [SerializeField] float mastFoldDegrees = 75f;
        [SerializeField] float dishFoldDegrees = 70f;

        MaterialPropertyBlock fireBlock;
        MerLandingPose lastPose;
        List<MerLandingLayout.Lobe> layoutLobes;
        MerLandingLayout.Petal[] layoutPetals;

        public MerLandingSite Site => site;
        public Transform Capsule => capsule;
        public Transform HeatShield => heatShield;
        public Transform Lander => lander;
        public Transform Parachute => parachute;
        public Transform Tether => tether;
        public Transform RoverRoot => roverRoot;
        public Transform EgressAnchor => egressAnchor;
        public IReadOnlyList<Transform> Lobes => lobes;
        public IReadOnlyList<Transform> Petals => petals;
        public Transform Core => core;
        public MerLandingPose LastPose => lastPose;

        public Transform RockerLeft => rockerLeft;
        public Transform MastHead => mastHead;

        /// <summary>Posiciona todos os atores no instante <paramref name="timeSeconds"/> (desde o início de M04). Idempotente.</summary>
        public void Apply(float timeSeconds)
        {
            EnsureLayout();
            var pose = MerLandingChoreography.Sample(timeSeconds, site);
            lastPose = pose;

            Place(capsule, pose.Capsule);
            Place(heatShield, pose.HeatShield);
            Place(lander, pose.Lander);
            Place(parachute, pose.Parachute);
            if (parachute != null)
                parachute.gameObject.SetActive(pose.Parachute.Visible);

            ApplyTether(pose);
            ApplyFire(pose);
            ApplyLobes(pose);
            ApplyPetals(pose);
            ApplyRover(pose);
        }

        void EnsureLayout()
        {
            if (layoutLobes == null)
                layoutLobes = MerLandingLayout.Lobes();
            if (layoutPetals == null)
                layoutPetals = MerLandingLayout.Petals();
        }

        static void Place(Transform target, in MerLandingActor actor)
        {
            if (target == null)
                return;

            target.localPosition = actor.Position;
            target.localRotation = actor.Rotation;
            target.localScale = actor.Scale;
        }

        void ApplyTether(in MerLandingPose pose)
        {
            if (tether == null)
                return;

            tether.gameObject.SetActive(pose.TetherVisible);
            var delta = pose.TetherBottom - pose.TetherTop;
            var length = delta.magnitude;
            if (length < 1e-3f)
                return;

            tether.localPosition = (pose.TetherTop + pose.TetherBottom) * 0.5f;
            tether.localRotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            tether.localScale = new Vector3(1f, length * 0.5f, 1f); // malha de cilindro com altura 2
        }

        void ApplyFire(in MerLandingPose pose)
        {
            if (fireball != null)
            {
                fireball.gameObject.SetActive(pose.Fireball > 0.001f);
                fireball.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, pose.Fireball);
                if (fireballRenderer != null)
                {
                    if (fireBlock == null)
                        fireBlock = new MaterialPropertyBlock();
                    fireballRenderer.GetPropertyBlock(fireBlock);
                    fireBlock.SetColor("_BaseColor", new Color(1f, 0.62f, 0.3f, 0.6f * pose.Fireball));
                    fireballRenderer.SetPropertyBlock(fireBlock);
                }
            }

            foreach (var flame in retroFlames)
            {
                if (flame == null)
                    continue;

                flame.gameObject.SetActive(pose.Retro > 0.001f);
                flame.localScale = new Vector3(1f, Mathf.Max(0.001f, pose.Retro), 1f);
            }
        }

        void ApplyLobes(in MerLandingPose pose)
        {
            var count = Mathf.Min(lobes.Length, layoutLobes.Count);
            var inflate = Mathf.Lerp(0.06f, 1f, pose.AirbagInflation);
            var deflate = pose.AirbagDeflation;
            for (var i = 0; i < count; i++)
            {
                var lobe = lobes[i];
                if (lobe == null)
                    continue;

                var rest = layoutLobes[i].Center;
                var horizontal = new Vector2(rest.x, rest.z);
                var direction = horizontal.sqrMagnitude > 1e-4f ? horizontal.normalized : DirectionFor(i);
                var flatRadius = Mathf.Max(horizontal.magnitude * 1.35f, DeflatedRingRadiusMeters);
                var flatXz = direction * flatRadius;
                var flat = new Vector3(flatXz.x, -MerLandingLayout.RestCenterHeightDeflated + MerLandingLayout.LobeRadiusMeters * DeflatedLobeHeightScale, flatXz.y);
                lobe.localPosition = Vector3.Lerp(rest, flat, deflate);
                var sxz = Mathf.Lerp(1f, 1.1f, deflate);
                var sy = Mathf.Lerp(1f, DeflatedLobeHeightScale, deflate);
                lobe.localScale = new Vector3(sxz, sy, sxz) * (inflate * MerLandingLayout.LobeRadiusMeters * 2f);
            }

            if (core != null)
            {
                core.gameObject.SetActive(deflate < 0.999f);
                core.localScale = Vector3.one * (inflate * MerLandingLayout.CoreRadiusMeters * 2f * (1f - deflate));
            }
        }

        static Vector2 DirectionFor(int index)
        {
            var angle = (index * 137.5f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        void ApplyPetals(in MerLandingPose pose)
        {
            var count = Mathf.Min(petals.Length, layoutPetals.Length);
            for (var i = 0; i < count; i++)
            {
                if (petals[i] == null)
                    continue;

                petals[i].localPosition = layoutPetals[i].HingeMidpoint;
                petals[i].localRotation = layoutPetals[i].ClosedRotation * Quaternion.Euler(MerLandingLayout.PetalOpenDegrees * pose.PetalOpen, 0f, 0f);
            }
        }

        void ApplyRover(in MerLandingPose pose)
        {
            if (roverRoot == null)
                return;

            var unfold = pose.RoverUnfold;
            roverRoot.localPosition = MerLandingLayout.RoverLocalPosition;
            roverRoot.localRotation = Quaternion.identity;
            roverRoot.localScale = Vector3.one * Mathf.Lerp(MerLandingLayout.StowedRoverScale, 1f, unfold);
            if (pose.RoverDrive > 0.0001f)
            {
                // Saída do módulo: o rover dirige pela pétala-rampa até o chão e segue o terreno (quadro do palco → mundo).
                MerLandingChoreography.RoverWorld(pose, out var driven, out var drivenRotation, out _, site);
                roverRoot.position = transform.TransformPoint(driven);
                roverRoot.rotation = transform.rotation * drivenRotation;
            }

            var fold = 1f - unfold;
            if (rockerLeft != null) rockerLeft.localRotation = Quaternion.Euler(rockerFoldDegrees * fold, 0f, 0f);
            if (rockerRight != null) rockerRight.localRotation = Quaternion.Euler(-rockerFoldDegrees * fold, 0f, 0f);
            if (bogieLeft != null) bogieLeft.localRotation = Quaternion.Euler(bogieFoldDegrees * fold, 0f, 0f);
            if (bogieRight != null) bogieRight.localRotation = Quaternion.Euler(bogieFoldDegrees * fold, 0f, 0f);
            if (mastHead != null) mastHead.localRotation = Quaternion.Euler(mastFoldDegrees * fold, 0f, 0f);
            if (antennaDish != null) antennaDish.localRotation = Quaternion.Euler(dishFoldDegrees * fold, 0f, 0f);
        }

        /// <summary>Centro do lobo <paramref name="index"/> no quadro do palco (já com inflação e achatamento).</summary>
        public Vector3 LobeCenter(int index) => lobes[index].position;

        /// <summary>Raio efetivo do lobo (maior eixo da escala local ÷ 2 da malha de diâmetro 1).</summary>
        public float LobeRadius(int index)
        {
            var s = lobes[index].lossyScale;
            return Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 0.5f;
        }

        /// <summary>Cantos (mundo) das caixas orientadas dos renderers do rover, para teste de contenção.</summary>
        public List<Vector3> RoverBoundsCorners()
        {
            var result = new List<Vector3>();
            if (roverRoot == null)
                return result;

            foreach (var renderer in roverRoot.GetComponentsInChildren<Renderer>(true))
            {
                var b = renderer.localBounds;
                var matrix = renderer.transform.localToWorldMatrix;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    result.Add(matrix.MultiplyPoint3x4(corner));
                }
            }

            return result;
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MerLandingAssembly);
            void Need(object value, string field)
            {
                if (value == null || (value is UnityEngine.Object o && o == null))
                    ContractValidation.Report(errors, owner, $"{field} não atribuído.");
            }

            Need(capsule, nameof(capsule));
            Need(heatShield, nameof(heatShield));
            Need(lander, nameof(lander));
            Need(parachute, nameof(parachute));
            Need(tether, nameof(tether));
            Need(fireball, nameof(fireball));
            Need(fireballRenderer, nameof(fireballRenderer));
            Need(core, nameof(core));
            Need(roverRoot, nameof(roverRoot));
            var expectedLobes = MerLandingLayout.Lobes().Count;
            if (lobes == null || lobes.Length != expectedLobes)
                ContractValidation.Report(errors, owner, $"esperados {expectedLobes} lobos de airbag, há {(lobes == null ? 0 : lobes.Length)}.");
            if (petals == null || petals.Length != 3)
                ContractValidation.Report(errors, owner, "esperadas 3 pétalas laterais.");
            if (retroFlames == null || retroFlames.Length == 0)
                ContractValidation.Report(errors, owner, "faltam as chamas dos retrofoguetes.");
            Need(rockerLeft, nameof(rockerLeft));
            Need(rockerRight, nameof(rockerRight));
            Need(bogieLeft, nameof(bogieLeft));
            Need(bogieRight, nameof(bogieRight));
            Need(mastHead, nameof(mastHead));
            Need(antennaDish, nameof(antennaDish));
            return errors;
        }
    }
}
