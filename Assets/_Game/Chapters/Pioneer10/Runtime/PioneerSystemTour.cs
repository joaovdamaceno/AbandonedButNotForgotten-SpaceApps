using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Cenas 2 e 7 · a sonda no Sistema Solar esquemático em 3D (sol, planetas, cinturão e órbitas do palco). Na cena 2 (8–19 s do cinturão)
    /// ela sai da órbita de Marte e entra no cinturão de asteroides; na cena 7 anda pelas órbitas de Júpiter a Netuno e além, no ritmo dos
    /// anos do <see cref="PioneerYearsHud"/>, e a órbita recém-cruzada acende. A sonda ganha um farol de brilho, para dar para vê-la na
    /// escala do mapa, e os rótulos (planetas, título e a nota "sem escala") são desenhados pelo HUD sobre a imagem, sem fundo.
    /// </summary>
    public sealed class PioneerSystemTour : MonoBehaviour
    {
        /// <summary>A sonda é ampliada para ser vista na escala do mapa (o refletor real tem 2,74 m).</summary>
        public const float ProbeScaleFactor = 4f;
        public static readonly Rect OverlayFrame = new Rect(0f, 0f, 1f, 1f);

        [SerializeField] PioneerPresentation presentation;
        [SerializeField] CinemaHudView hud;
        [SerializeField, Tooltip("Sequência do cinturão (PIO_BeltCinema).")] SequenceDefinition beltSequence;
        [SerializeField, Tooltip("Sequência dos anos (PIO_Outbound).")] SequenceDefinition outboundSequence;
        [SerializeField, Tooltip("Pai do giro da sonda (Probe): é ele que anda no mapa.")] Transform probe;
        [SerializeField, Tooltip("Farol de brilho da sonda no mapa.")] Transform beacon;
        [SerializeField, Tooltip("Centro do Sistema Solar no espaço profundo.")] Vector3 center;
        [SerializeField, Tooltip("Órbitas na ordem: Terra, Marte, Júpiter, Saturno, Urano, Netuno.")] LineRenderer[] orbits = new LineRenderer[0];
        [SerializeField, Tooltip("Planetas na mesma ordem das órbitas.")] Transform[] planets = new Transform[0];
        [SerializeField, Tooltip("Um ponto sobre o cinturão, para o rótulo dele.")] Transform beltMarker;
        [SerializeField, Tooltip("A linha dourada do caminho da sonda; o fim dela segue o trecho de cada cena.")] LineRenderer probePath;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color0 = Shader.PropertyToID("_Color");
        static readonly Color OrbitColor = new Color(0.46f, 0.52f, 0.62f, 1f);
        static readonly Color PulseColor = new Color(1f, 0.84f, 0.42f, 1f);

        SequenceDirector director;
        Func<string, string> text;
        Vector3 baseProbePosition, baseProbeScale;
        bool captured, shown;
        MaterialPropertyBlock block;

        public bool IsShown => shown;

        public void Initialize(SequenceDirector sequenceDirector, Func<string, string> textResolver)
        {
            director = sequenceDirector;
            text = textResolver;
            Capture();
        }

        /// <summary>Distância (UA) da sonda ao Sol e quais rótulos e órbita pulsam, conforme a sequência e o instante.</summary>
        public bool TryGetState(SequenceDefinition current, float seconds, out SystemMapElement.State state)
        {
            state = default;
            if (current == null)
                return false;

            if (current == beltSequence && PioneerBeltCounter.MapVisibleAt(seconds))
            {
                state = PioneerBeltCounter.MapStateAt(seconds, Resolve);
                return true;
            }

            if (current == outboundSequence)
            {
                state = PioneerYearsHud.MapStateAt(seconds, Resolve);
                return true;
            }

            return false;
        }

        /// <summary>Posição da sonda no mapa para a distância dada: sobre a linha do caminho.</summary>
        public Vector3 ProbePositionFor(float au) => center + Direction() * RadiusFor(au);

        Vector3 Direction()
        {
            var angle = SystemMapElement.PathAngleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        public const float MetersPerRootAu = 28f;
        static float RadiusFor(float au) => MetersPerRootAu * Mathf.Sqrt(Mathf.Max(0f, au));

        void Update() => Tick();

        void OnDisable() => Hide();

        /// <summary>Atualiza a sonda, as órbitas e os rótulos pelo relógio da sequência. Testes chamam direto.</summary>
        public void Tick()
        {
            if (presentation == null || director == null || probe == null)
                return;

            if (!director.IsPlaying || !TryGetState(director.Current, director.TimeSeconds, out var state))
            {
                Hide();
                return;
            }

            Apply(state);
        }

        /// <summary>Põe o mapa como no estado dado (sem ler a sequência).</summary>
        public void Apply(in SystemMapElement.State state)
        {
            Capture();
            probe.position = ProbePositionFor(state.ProbeAu);
            probe.localScale = baseProbeScale * ProbeScaleFactor;
            var camera = presentation.CameraDirector != null ? presentation.CameraDirector.TargetCamera : null;
            if (beacon != null)
            {
                beacon.position = probe.position;
                if (camera != null)
                    beacon.rotation = camera.transform.rotation;
            }

            PulseOrbits(state);
            if (probePath != null && probePath.positionCount >= 2 && state.PathEndAu > 0f)
                probePath.SetPosition(1, Direction() * RadiusFor(state.PathEndAu)); // cena 2: acaba na borda do campo; cena 7: vai até Netuno e além
            shown = true;
            if (hud != null)
                hud.SetSystemMap(true, WithOverlay(state, camera), OverlayFrame);
        }

        SystemMapElement.State WithOverlay(SystemMapElement.State state, Camera camera)
        {
            state.Overlay = true;
            var inner = state.Mode == SystemMapElement.MapMode.Inner;
            // Ordem dos rótulos: interno = Mercúrio, Vênus, Terra, Marte, cinturão, Júpiter; externo = Júpiter, Saturno, Urano, Netuno.
            var targets = new Transform[6];
            if (inner)
            {
                targets[0] = PlanetAt(0);
                targets[1] = PlanetAt(1);
                targets[2] = PlanetAt(2);
                targets[3] = PlanetAt(3);
                targets[4] = beltMarker;
                targets[5] = PlanetAt(4);
            }
            else
            {
                for (var i = 0; i < 4; i++)
                    targets[i] = PlanetAt(4 + i);
            }

            state.LabelViewport = new Vector2[6];
            for (var i = 0; i < 6; i++)
                state.LabelViewport[i] = Viewport(camera, targets[i] != null ? targets[i].position : Vector3.zero);
            state.ProbeViewport = Viewport(camera, probe.position);
            return state;
        }

        Transform PlanetAt(int index) => planets != null && index < planets.Length ? planets[index] : null;

        /// <summary>Posição no quadro (0–1, origem em cima à esquerda) de um ponto do mundo.</summary>
        static Vector2 Viewport(Camera camera, Vector3 world)
        {
            if (camera == null)
                return new Vector2(0.5f, 0.5f);
            var point = camera.WorldToViewportPoint(world);
            return new Vector2(point.x, 1f - point.y);
        }

        void PulseOrbits(in SystemMapElement.State state)
        {
            if (orbits == null)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();

            var inner = state.Mode == SystemMapElement.MapMode.Inner;
            for (var i = 0; i < orbits.Length; i++)
            {
                if (orbits[i] == null)
                    continue;

                // Índice da órbita no mapa: interno 0 Mercúrio, 1 Vênus, 2 Terra, 3 Marte, 4 cinturão (sem órbita), 5 Júpiter; externo 0 Júpiter … 3 Netuno.
                var ring = inner ? (i <= 3 ? i : i == 4 ? 5 : -1) : (i >= 4 ? i - 4 : -1);
                var pulse = ring >= 0 && ring == state.PulseRing ? Mathf.Sin(Mathf.Clamp01(state.Pulse01) * Mathf.PI) : 0f;
                var color = Color.Lerp(OrbitColor, PulseColor, pulse);
                orbits[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                block.SetColor(Color0, color);
                orbits[i].SetPropertyBlock(block);
                orbits[i].widthMultiplier = 0.7f + pulse * 1.6f;
            }
        }

        public void Hide()
        {
            if (!shown)
                return;

            shown = false;
            if (probe != null)
            {
                probe.position = baseProbePosition;
                probe.localScale = baseProbeScale;
            }

            if (hud != null)
                hud.SetSystemMap(false, default, OverlayFrame);
        }

        string Resolve(string key)
        {
            var value = text != null ? text(key) : null;
            return string.IsNullOrEmpty(value) ? key : value;
        }

        void Capture()
        {
            if (captured || probe == null)
                return;

            baseProbePosition = probe.position;
            baseProbeScale = probe.localScale;
            captured = true;
        }
    }
}
