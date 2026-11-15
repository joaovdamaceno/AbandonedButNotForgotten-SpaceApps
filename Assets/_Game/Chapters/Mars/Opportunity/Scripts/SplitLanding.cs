using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>
    /// The twin landings. Spirit (Gusev Crater) and Opportunity (Meridiani Planum) descend at the same time in a
    /// side-by-side split screen (Spirit on the left, Opportunity on the right); the right view then grows to fill the screen
    /// and play continues with Opportunity. Spirit's lander, capsule and rover are runtime copies of Opportunity's
    /// hardware placed elsewhere on the terrain and kept on their own layer, so each camera sees only its own site.
    /// The Spirit rover copy is also reused as a static prop in the opening laboratory and in the 2010 memorial.
    /// </summary>
    public sealed class SplitLanding : MonoBehaviour
    {
        public const int SpiritLayer = 30, OpportunityLayer = 31;
        public const float SplitHold = 12.6f, ExpandEnd = 15.2f;
        public ChapterDirector chapter;
        public Vector3 spiritOffset = new Vector3(105, 0, -70);
        [Tooltip("Distância mínima (m) entre o ponto de pouso da Spirit e qualquer pedra: o módulo, os airbags e os quiques cabem nela.")]
        public float rockClearance = 10f;
        [Tooltip("Até onde (m) procurar areia livre em volta de spawn2004 + spiritOffset quando o ponto nominal tem pedra ou declive.")]
        public float siteSearchRadius = 90f;
        [Tooltip("Pedras decorativas ao redor do ponto de pouso da Spirit (entre 13 e 24 m, fora da zona livre).")]
        public int sceneryRocks = 7;

        public bool Active { get; private set; }
        /// <summary>0 = two half-width views, 1 = Opportunity fills the screen.</summary>
        public float Expand { get; private set; }
        public bool LabelsVisible => Active && Expand < .55f;
        /// <summary>Opportunity's share of the screen width.</summary>
        public float OpportunityWidth => Mathf.Lerp(.5f, 1f, Expand);
        /// <summary>Lens scale for the tall half-width views: a little wider so each lander still fits the frame.</summary>
        public float LensScale => Active ? Mathf.Lerp(1.15f, 1f, Expand) : 1f;

        Camera spiritCamera;
        Transform capsule, shield, lander, airbagRoot, bridle, spiritRover;
        GameObject parachute;
        ParticleSystem brake, dust;
        AirbagLanderRig rig;
        RoverPresentationRig fold;
        Vector3 origin;
        readonly List<GameObject> sceneryClones = new List<GameObject>();
        int lastImpact = -1;
        readonly List<KeyValuePair<GameObject, int>> restoredLayers = new List<KeyValuePair<GameObject, int>>();
        bool built;

        static Transform Counterpart(Transform original, Transform originalRoot, Transform cloneRoot)
        {
            var path = new Stack<int>();
            for (var t = original; t != null && t != originalRoot; t = t.parent) path.Push(t.GetSiblingIndex());
            var result = cloneRoot;
            foreach (int i in path) result = result.GetChild(i);
            return result;
        }

        static void SetLayers(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        void Build()
        {
            if (built) return;
            built = true;
            var study = chapter.investigation;
            var parent = chapter.lander.parent;
            var capsuleClone = Instantiate(study.entryCapsule, parent);
            capsuleClone.name = "Spirit • aeroshell";
            capsule = capsuleClone.transform;
            shield = Counterpart(study.entryShield.transform, study.entryCapsule.transform, capsule);
            parachute = Counterpart(study.entryParachute.transform, study.entryCapsule.transform, capsule).gameObject;
            brake = Counterpart(study.entryBrake.transform, study.entryCapsule.transform, capsule).GetComponent<ParticleSystem>();
            var bridleClone = Instantiate(study.entryBridle.gameObject, study.entryBridle.parent);
            bridleClone.name = "Spirit • lowering bridle";
            bridle = bridleClone.transform;
            var landerClone = Instantiate(chapter.lander.gameObject, parent);
            landerClone.name = "Spirit • lander within airbags";
            lander = landerClone.transform;
            rig = landerClone.GetComponent<AirbagLanderRig>();
            airbagRoot = Counterpart(chapter.airbagRoot, chapter.lander, lander);
            var dustClone = Instantiate(chapter.landingDust.gameObject, chapter.landingDust.transform.parent);
            dustClone.name = "Spirit • landing dust";
            dust = dustClone.GetComponent<ParticleSystem>();
            foreach (var go in new[] { capsuleClone, bridleClone, landerClone, dustClone }) SetLayers(go, SpiritLayer);
            capsuleClone.SetActive(false); bridleClone.SetActive(false); landerClone.SetActive(false);

            var main = chapter.cameras.view;
            var cameraObject = new GameObject("Camera • Spirit (split screen)");
            cameraObject.transform.SetParent(transform, false);
            spiritCamera = cameraObject.AddComponent<Camera>();
            spiritCamera.CopyFrom(main);
            spiritCamera.depth = main.depth - 1;
            spiritCamera.cullingMask = ~(1 << OpportunityLayer);
            var data = spiritCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.requiresDepthTexture = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            spiritCamera.enabled = false;
        }

        /// <summary>A static copy of the rover. Created on demand; stays inactive until a scene asks for it.</summary>
        public Transform EnsureRover()
        {
            if (spiritRover) return spiritRover;
            var source = chapter.rover.gameObject;
            bool wasActive = source.activeSelf;
            source.SetActive(false);
            var clone = Instantiate(source, source.transform.parent);
            source.SetActive(wasActive);
            clone.name = "Spirit • NASA MER-A (static copy)";
            foreach (var behaviour in clone.GetComponents<RoverController>()) behaviour.enabled = false;
            foreach (var controller in clone.GetComponents<CharacterController>()) controller.enabled = false;
            foreach (var c in clone.GetComponentsInChildren<Component>(true))
                if (c is ReflectionProbe || c is TrailRenderer || c is AudioSource || c is ParticleSystem || c is Light) Destroy(c);
            fold = clone.GetComponent<RoverPresentationRig>();
            spiritRover = clone.transform;
            clone.SetActive(false);
            return spiritRover;
        }

        /// <summary>Shows Spirit's rover as a prop (laboratory, 2010 memorial) or hides it.</summary>
        public void ShowSpirit(bool visible, Vector3 position, float yaw, float tilt = 0, float sink = 0)
        {
            if (!chapter) return;
            var r = EnsureRover();
            if (!visible) { if (r.gameObject.activeSelf) r.gameObject.SetActive(false); return; }
            if (!r.gameObject.activeSelf) { SetLayers(r.gameObject, 0); r.gameObject.SetActive(true); }
            var ground = chapter.rover.TerrainPoint(position);
            r.SetPositionAndRotation(ground - Vector3.up * sink, Quaternion.Euler(tilt, yaw, tilt * .4f));
            if (fold) fold.Fold(0);
        }

        public void Begin()
        {
            Build();
            var rocks = CollectRocks(chapter.spawn2004.position + spiritOffset);
            origin = ChooseSandSite(chapter.spawn2004.position + spiritOffset, rocks);
            origin = chapter.rover.TerrainPoint(origin);
            PlaceScenery(rocks);
            Active = true; Expand = 0; lastImpact = -1;
            restoredLayers.Clear();
            var study = chapter.investigation;
            foreach (var go in new[] { study.entryCapsule, study.entryBridle.gameObject, chapter.lander.gameObject, chapter.landingDust.gameObject, chapter.rover.gameObject })
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) { restoredLayers.Add(new KeyValuePair<GameObject, int>(t.gameObject, t.gameObject.layer)); t.gameObject.layer = OpportunityLayer; }
            var r = EnsureRover(); SetLayers(r.gameObject, SpiritLayer);
            r.gameObject.SetActive(true);
            lander.gameObject.SetActive(true);
            spiritCamera.enabled = true;
            Apply(0, 0);
        }

        public void End()
        {
            foreach (var pair in restoredLayers) if (pair.Key) pair.Key.layer = pair.Value;
            restoredLayers.Clear();
            ClearScenery();
            if (!built) { Active = false; return; }
            Active = false; Expand = 1;
            if (capsule) capsule.gameObject.SetActive(false);
            if (bridle) bridle.gameObject.SetActive(false);
            if (lander) lander.gameObject.SetActive(false);
            if (dust) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (spiritCamera) spiritCamera.enabled = false;
            if (spiritRover) { spiritRover.gameObject.SetActive(false); SetLayers(spiritRover.gameObject, 0); }
            var main = chapter.cameras ? chapter.cameras.view : null;
            if (main) main.rect = new Rect(0, 0, 1, 1);
        }

        /// <summary>Called every frame of the landing film with the film clock (0–18 s).</summary>
        public void Tick(float time)
        {
            if (!Active) return;
            Expand = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(SplitHold, ExpandEnd, time));
            Apply(time, CompactLandingClock.SourceTime(time));
        }


        // ---------- Local de pouso da Spirit: sempre areia, nunca dentro de uma pedra ----------

        static readonly string[] RockWords = { "rock", "outcrop", "mckittrick", "meteorite", "ledge", "boulder", "stone" };

        struct RockInfo
        {
            public MeshRenderer Renderer;
            public Bounds Bounds;
        }

        /// <summary>Pedras e afloramentos da cena perto do ponto nominal (por nome do objeto). Ignora o hardware da Spirit e peças minúsculas.</summary>
        List<RockInfo> CollectRocks(Vector3 around)
        {
            var result = new List<RockInfo>();
            float reach = siteSearchRadius + rockClearance + 40f;
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var lower = renderer.gameObject.name.ToLowerInvariant();
                if (lower.Contains("spirit")) continue;
                var isRock = false;
                foreach (var word in RockWords) isRock |= lower.Contains(word);
                if (!isRock) continue;

                var b = renderer.bounds;
                if (Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) < .15f) continue;
                if (Mathf.Abs(b.center.x - around.x) > reach || Mathf.Abs(b.center.z - around.z) > reach) continue;
                result.Add(new RockInfo { Renderer = renderer, Bounds = b });
            }

            return result;
        }

        bool GroundAt(Vector3 p, out float y)
        {
            var ground = chapter.rover.eagleGround;
            if (ground && ground.Raycast(new Ray(new Vector3(p.x, p.y + 60, p.z), Vector3.down), out var hit, 120)) { y = hit.point.y; return true; }
            y = p.y; return false;
        }

        /// <summary>Chão contínuo e quase plano, sem pedra dentro de <see cref="rockClearance"/> (distância no plano até a caixa de cada pedra).</summary>
        bool IsClearSand(Vector3 p, List<RockInfo> rocks)
        {
            if (!GroundAt(p, out var y0)) return false;
            float low = y0, high = y0;
            for (var i = 0; i < 8; i++)
            {
                var a = i * 45f * Mathf.Deg2Rad;
                if (!GroundAt(p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 7f, out var y)) return false;
                low = Mathf.Min(low, y); high = Mathf.Max(high, y);
            }

            if (high - low > 1f) return false;
            foreach (var rock in rocks)
            {
                var b = rock.Bounds;
                var dx = Mathf.Max(Mathf.Max(b.min.x - p.x, 0f), p.x - b.max.x);
                var dz = Mathf.Max(Mathf.Max(b.min.z - p.z, 0f), p.z - b.max.z);
                if (dx * dx + dz * dz < rockClearance * rockClearance) return false;
            }

            return true;
        }

        /// <summary>O ponto nominal se estiver limpo; senão o ponto limpo mais próximo dele, em anéis crescentes, longe do pouso da Opportunity.</summary>
        Vector3 ChooseSandSite(Vector3 nominal, List<RockInfo> rocks)
        {
            var opportunity = chapter.spawn2004.position;
            bool FarFromOpportunity(Vector3 p) => new Vector2(p.x - opportunity.x, p.z - opportunity.z).magnitude >= 60f;
            if (FarFromOpportunity(nominal) && IsClearSand(nominal, rocks)) return nominal;

            for (var radius = 8f; radius <= siteSearchRadius; radius += 6f)
            {
                var steps = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / 8f));
                for (var i = 0; i < steps; i++)
                {
                    var a = i * 2f * Mathf.PI / steps;
                    var candidate = nominal + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                    if (FarFromOpportunity(candidate) && IsClearSand(candidate, rocks))
                    {
                        Debug.Log($"[SplitLanding] Spirit: ponto nominal com pedra/declive; pouso movido {radius:0} m para areia livre {candidate}.");
                        return candidate;
                    }
                }
            }

            Debug.LogWarning("[SplitLanding] Spirit: nenhuma areia livre encontrada no raio de busca; mantendo o ponto nominal.");
            return nominal;
        }

        /// <summary>Algumas pedras do próprio cenário copiadas (só para a câmera da Spirit) em volta do pouso, fora da zona livre e apoiadas no chão.</summary>
        void PlaceScenery(List<RockInfo> rocks)
        {
            ClearScenery();
            if (sceneryRocks <= 0) return;
            var sources = new List<MeshRenderer>();
            foreach (var rock in rocks)
            {
                var filter = rock.Renderer.GetComponent<MeshFilter>();
                var size = Mathf.Max(rock.Bounds.size.x, Mathf.Max(rock.Bounds.size.y, rock.Bounds.size.z));
                if (rock.Renderer.gameObject.activeInHierarchy && filter && filter.sharedMesh && size >= .5f && size <= 3.5f) sources.Add(rock.Renderer);
            }

            if (sources.Count == 0) return;
            var random = new System.Random(2004);
            for (var i = 0; i < sceneryRocks; i++)
            {
                var source = sources[random.Next(sources.Count)];
                var angle = (i + (float)random.NextDouble() * .6f) * 2f * Mathf.PI / sceneryRocks;
                var radius = Mathf.Lerp(13f, 24f, (float)random.NextDouble());
                var spot = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (!GroundAt(spot, out var groundY)) continue;

                var copy = new GameObject("Spirit • scenery rock " + i);
                copy.transform.SetParent(transform, false);
                copy.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var meshRenderer = copy.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = source.sharedMaterials;
                copy.transform.SetPositionAndRotation(spot, Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.up) * source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                var lift = (groundY - .06f) - meshRenderer.bounds.min.y;
                copy.transform.position += Vector3.up * lift;
                copy.layer = SpiritLayer;
                sceneryClones.Add(copy);
            }
        }

        void ClearScenery()
        {
            foreach (var clone in sceneryClones) if (clone) Destroy(clone);
            sceneryClones.Clear();
        }

        void Apply(float time, float t)
        {
            var main = chapter.cameras.view;
            float w = OpportunityWidth;
            main.rect = new Rect(1 - w, 0, w, 1);
            bool showSpirit = w < .999f;
            spiritCamera.enabled = showSpirit;
            spiritCamera.rect = new Rect(0, 0, 1 - w, 1);
            spiritCamera.depth = main.depth - 1;
            if (!showSpirit) return;

            Vector3 upper = origin + EntryFlight.Upper(t), payload = origin + EntryFlight.Payload(t, rig.landingClearance);
            capsule.gameObject.SetActive(t < 26);
            capsule.SetPositionAndRotation(upper, Quaternion.Euler(8, 35, -4));
            shield.localPosition = new Vector3(Mathf.Max(0, t - 6) * .7f, -1.3f - Mathf.Max(0, t - 6) * 2.8f, 0);
            parachute.SetActive(t >= 6); parachute.transform.localScale = Vector3.one * EntryFlight.Ease(6, 7.2f, t);
            var emission = brake.emission; emission.rateOverTime = t > 14 && t < 18 ? 22 : 0;
            lander.SetPositionAndRotation(payload, EntryFlight.Rotation(t));
            rig.Pose(EntryFlight.Deflation(t), EntryFlight.Opening(t));
            airbagRoot.localScale = Vector3.one * EntryFlight.Inflation(t);
            bridle.gameObject.SetActive(t >= 8 && t < 18);
            var cable = bridle.GetComponent<LineRenderer>();
            cable.SetPosition(0, upper - Vector3.up);
            cable.SetPosition(1, lander.TransformPoint(Vector3.up * (rig.deckHeight + rig.closedRise)));
            spiritRover.SetPositionAndRotation(lander.TransformPoint(Vector3.up * (rig.deckHeight + .035f)), lander.rotation);
            if (fold) fold.Fold(1 - EntryFlight.Deployed(t));
            int impact = EntryFlight.Impact(t);
            if (t < 38 && impact >= 0 && lastImpact != impact)
            {
                lastImpact = impact;
                dust.transform.position = chapter.rover.TerrainPoint(payload) + Vector3.up * .03f;
                dust.Emit(impact == 0 ? 30 : 14);
            }
            Shot(time, t, upper, payload, out var position, out var look, out float lens);
            spiritCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
            spiritCamera.fieldOfView = lens * LensScale;
        }

        // Same editorial beats as Opportunity's lens, turned to a different angle so the two views do not mirror each other.
        void Shot(float time, float t, Vector3 upper, Vector3 payload, out Vector3 position, out Vector3 look, out float lens)
        {
            var turn = Quaternion.Euler(0, 75, 0);
            if (time < 4.5f) { position = upper + turn * new Vector3(-38, 14, -55); look = Vector3.Lerp(upper, payload, .25f) + Vector3.up * 3; lens = 42; }
            else if (time < 7.5f) { position = origin + turn * new Vector3(-14, 5, -17); look = payload + Vector3.up * .9f; lens = 46; }
            else if (time < 13.5f)
            {
                position = origin + turn * Vector3.Lerp(new Vector3(-10, 4, 10), new Vector3(-8, 3.5f, 8), Mathf.InverseLerp(7.5f, 13.5f, time));
                look = payload + Vector3.up * .8f; lens = 42;
            }
            else
            {
                position = origin + turn * Vector3.Lerp(new Vector3(-9, 5, -8), new Vector3(-6, 3.2f, -6), Mathf.InverseLerp(13.5f, 18, time));
                look = payload + Vector3.up * .9f; lens = 42;
            }
            var ground = chapter.rover.TerrainPoint(position);
            if (ground != position) position.y = Mathf.Max(position.y, ground.y + 1.2f);
        }
    }
}
