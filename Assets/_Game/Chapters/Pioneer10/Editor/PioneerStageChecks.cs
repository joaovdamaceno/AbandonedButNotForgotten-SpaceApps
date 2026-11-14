using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-003 · Verificações geométricas do palco, sem alterar a cena: câmera fora do volume varrido pela sonda em
    /// rotação, fora de Júpiter e acima do chão; coordenadas pequenas (sem jitter); planos consecutivos distintos.
    /// </summary>
    public static class PioneerStageChecks
    {
        public const float ProbeClearanceMeters = 0.5f;
        public const float JupiterClearanceMeters = 5f;
        public const float GroundClearanceMeters = 1f;
        public const float MaxCoordinateMeters = 1000f;
        public const float SampleSpacingMeters = 0.12f;

        // Dois planos consecutivos na mesma região são "quase iguais" se TODAS as diferenças forem pequenas.
        public const float NearSameAngleDegrees = 15f;
        public const float NearSameDistanceMeters = 4f;
        public const float NearSameFovDegrees = 8f;

        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Lines = new List<string>();
            public readonly Dictionary<string, float> ProbeClearance = new Dictionary<string, float>();
            public float LargestCoordinateMeters;
        }

        public static Report Run(PioneerPresentation presentation)
        {
            var report = new Report();
            var profile = SweptProfile(presentation.ProbeSpin);
            var anchors = presentation.Anchors;
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor?.pose == null)
                    continue;

                var position = anchor.pose.position;
                report.LargestCoordinateMeters = Mathf.Max(report.LargestCoordinateMeters, MaxAbs(position));
                if (MaxAbs(position) > MaxCoordinateMeters)
                    report.Errors.Add($"anchor '{anchor.anchorId}' a {MaxAbs(position):0} m da origem (> {MaxCoordinateMeters} m).");

                if (anchor.region == PioneerStageRegion.DeepSpace)
                {
                    var clearance = ProbeClearance(presentation.ProbeSpin, profile, position);
                    report.ProbeClearance[anchor.anchorId] = clearance;
                    if (clearance < ProbeClearanceMeters)
                        report.Errors.Add($"anchor '{anchor.anchorId}': câmera a {clearance:0.00} m da sonda girando (mínimo {ProbeClearanceMeters} m).");

                    if (presentation.Jupiter != null)
                    {
                        var surface = Vector3.Distance(position, presentation.Jupiter.position) - presentation.Jupiter.lossyScale.x;
                        if (surface < JupiterClearanceMeters)
                            report.Errors.Add($"anchor '{anchor.anchorId}': câmera dentro/rente a Júpiter ({surface:0.0} m).");
                    }
                }
                else
                {
                    var groundY = GroundHeight(presentation, anchor.region);
                    if (groundY.HasValue && position.y - groundY.Value < GroundClearanceMeters)
                        report.Errors.Add($"anchor '{anchor.anchorId}': câmera a {position.y - groundY.Value:0.00} m do chão (mínimo {GroundClearanceMeters} m).");
                }

                if (anchor.HasUiFrame && (anchor.uiFrameViewport.xMin < 0f || anchor.uiFrameViewport.yMin < 0f || anchor.uiFrameViewport.xMax > 1f || anchor.uiFrameViewport.yMax > 1f))
                    report.Errors.Add($"anchor '{anchor.anchorId}': quadro de UI fora do viewport.");

                if (i > 0 && anchors[i - 1]?.pose != null)
                {
                    var previous = anchors[i - 1];
                    var angle = Vector3.Angle(previous.pose.forward, anchor.pose.forward);
                    var distance = Vector3.Distance(previous.pose.position, position);
                    var fov = Mathf.Abs(previous.fieldOfViewDegrees - anchor.fieldOfViewDegrees);
                    report.Lines.Add(string.Format(CultureInfo.InvariantCulture, "{0} → {1}: Δângulo {2:0}°, Δposição {3:0.0} m, ΔFOV {4:0}°, região {5}",
                        previous.anchorId, anchor.anchorId, angle, distance, fov, previous.region == anchor.region ? "igual" : "muda"));
                    if (previous.region == anchor.region && angle < NearSameAngleDegrees && distance < NearSameDistanceMeters && fov < NearSameFovDegrees)
                        report.Errors.Add($"planos consecutivos quase iguais: {previous.anchorId} → {anchor.anchorId}.");
                }
            }

            return report;
        }

        static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));

        /// <summary>
        /// Pontos da superfície da sonda em coordenadas (z axial, r radial) do eixo de rotação. Cada ponto varre um círculo
        /// ao girar; a distância da câmera a esse círculo é a distância no meio-plano (z, r).
        /// </summary>
        public static List<Vector2> SweptProfile(Transform spin)
        {
            var points = new List<Vector2>();
            if (spin == null)
                return points;

            foreach (var filter in spin.GetComponentsInChildren<MeshFilter>(false))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;

                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var toSpin = spin.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (var t = 0; t < triangles.Length; t += 3)
                {
                    var a = toSpin.MultiplyPoint3x4(vertices[triangles[t]]);
                    var b = toSpin.MultiplyPoint3x4(vertices[triangles[t + 1]]);
                    var c = toSpin.MultiplyPoint3x4(vertices[triangles[t + 2]]);
                    var longest = Mathf.Max(Vector3.Distance(a, b), Mathf.Max(Vector3.Distance(b, c), Vector3.Distance(c, a)));
                    var n = Mathf.Clamp(Mathf.CeilToInt(longest / SampleSpacingMeters), 1, 80);
                    for (var i = 0; i <= n; i++)
                    {
                        for (var j = 0; j <= n - i; j++)
                        {
                            var p = a + (b - a) * (i / (float)n) + (c - a) * (j / (float)n);
                            points.Add(new Vector2(p.z, new Vector2(p.x, p.y).magnitude));
                        }
                    }
                }
            }

            return points;
        }

        public static float ProbeClearance(Transform spin, List<Vector2> profile, Vector3 worldPoint)
        {
            if (spin == null || profile.Count == 0)
                return float.PositiveInfinity;

            var local = spin.InverseTransformPoint(worldPoint);
            var point = new Vector2(local.z, new Vector2(local.x, local.y).magnitude);
            return Mathf.Sqrt(profile.Min(p => (p - point).sqrMagnitude));
        }

        static float? GroundHeight(PioneerPresentation presentation, PioneerStageRegion region)
        {
            var stage = presentation.transform.Find("Regions/" + region);
            var ground = stage != null ? stage.Find("Ground") : null;
            return ground != null ? ground.position.y : (float?)null;
        }
    }
}
