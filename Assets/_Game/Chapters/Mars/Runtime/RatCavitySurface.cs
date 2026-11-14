using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-004: disco circular e raso que a RAT abre na rocha (cerca de 45 mm de diâmetro, até 5 mm de profundidade; MS09).
    /// Não é uma sondagem: a malha é um único disco local, com três faixas de remoção (borda, anel intermediário e centro)
    /// que descem em sequência sobreposta, então a borda se define primeiro e o interior depois. A profundidade só depende
    /// do progresso real de trabalho recebido em <see cref="SetProgress"/>; sem progresso a malha não muda.
    /// <para>
    /// O disco é a única região de interesse: <see cref="Contains"/> e <see cref="ClampToDisc"/> garantem que nenhum ponto
    /// de análise (APXS/Mössbauer, MAR-005) caia fora dele. A ferramenta nunca deve passar abaixo de
    /// <see cref="DeepestMeters"/>: o piso é a única superfície que ela segue.
    /// </para>
    /// Eixo local Y = normal da rocha; escala do transform deve ser 1 (metros reais).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RatCavitySurface : MonoBehaviour
    {
        public const int BandCount = 3;
        public const float DiameterMeters = 0.045f;
        public const float MaxCapacityMeters = 0.005f;

        const float EdgeWidth = 0.06f;
        const float BandBlendWidth = 0.03f;
        const float OuterBandInnerFraction = 0.70f;
        const float MiddleBandInnerFraction = 0.40f;
        static readonly float[] BandStart = { 0f, 0.2f, 0.4f };
        const float BandSpan = 0.6f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(0.005f), Tooltip("Raio do disco em metros. 0,0225 m = 45 mm de diâmetro.")] float radiusMeters = DiameterMeters * 0.5f;
        [SerializeField, Range(0.001f, MaxCapacityMeters), Tooltip("Profundidade final no piso, no máximo 5 mm.")] float maxDepthMeters = MaxCapacityMeters;
        [SerializeField, Range(8, 48)] int ringCount = 24;
        [SerializeField, Range(12, 64)] int segmentCount = 32;
        [SerializeField, Tooltip("Cor da superfície alterada antes do esmerilhamento.")] Color weatheredColor = new Color(0.43f, 0.27f, 0.2f, 1f);
        [SerializeField, Tooltip("Cor do material exposto no piso da cavidade.")] Color freshColor = new Color(0.68f, 0.5f, 0.4f, 1f);

        Mesh mesh;
        Vector3[] vertices;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        float progress;
        float lastAppliedProgress = -1f;
        readonly float[] bandProgress = new float[BandCount];

        public float RadiusMeters => radiusMeters;
        public float MaxDepthMeters => maxDepthMeters;

        /// <summary>Progresso de trabalho, 0–1.</summary>
        public float Progress01 => progress;

        /// <summary>Normal da rocha no centro do disco, em mundo.</summary>
        public Vector3 Normal => transform.up;

        /// <summary>Maior profundidade do piso agora, em metros (centro do disco). A ferramenta fica acima disso.</summary>
        public float DeepestMeters => maxDepthMeters * bandProgress[BandCount - 1];

        /// <summary>Profundidade atual do piso na faixa i: 0 = borda, 1 = anel intermediário, 2 = centro.</summary>
        public float BandDepthMeters(int band) => maxDepthMeters * bandProgress[Mathf.Clamp(band, 0, BandCount - 1)];

        public float BandProgress01(int band) => bandProgress[Mathf.Clamp(band, 0, BandCount - 1)];

        public Mesh Mesh => mesh;

        void Awake() => EnsureMesh();

        void OnDestroy()
        {
            if (mesh != null)
                DestroyImmediate(mesh);
        }

        /// <summary>
        /// Define o progresso físico (0–1). Só quem mede o trabalho real chama; igual ao anterior não reconstrói a malha.
        /// O valor pode diminuir apenas em <see cref="ResetSurface"/>.
        /// </summary>
        public void SetProgress(float value01)
        {
            EnsureMesh();
            progress = Mathf.Clamp01(value01);
            if (Mathf.Approximately(progress, lastAppliedProgress))
                return;

            lastAppliedProgress = progress;
            for (var i = 0; i < BandCount; i++)
                bandProgress[i] = Smooth(Mathf.Clamp01((progress - BandStart[i]) / BandSpan));

            Rebuild();
        }

        public void ResetSurface()
        {
            lastAppliedProgress = -1f;
            SetProgress(0f);
        }

        /// <summary>Profundidade do piso (metros, positiva) a esta distância do centro. Zero fora do disco.</summary>
        public float SampleDepthMeters(float distanceFromCenterMeters)
        {
            var rn = distanceFromCenterMeters / radiusMeters;
            return maxDepthMeters * Depth01(rn);
        }

        /// <summary>O ponto está sobre o disco (raio e altura próximos da superfície)?</summary>
        public bool Contains(Vector3 worldPoint, float heightToleranceMeters = 0.02f)
        {
            var local = transform.InverseTransformPoint(worldPoint);
            return new Vector2(local.x, local.z).magnitude <= radiusMeters && Mathf.Abs(local.y) <= heightToleranceMeters;
        }

        /// <summary>Leva um ponto qualquer para dentro do disco, projetado na superfície local. Pontos de análise usam isto.</summary>
        public Vector3 ClampToDisc(Vector3 worldPoint)
        {
            var local = transform.InverseTransformPoint(worldPoint);
            var planar = new Vector2(local.x, local.z);
            if (planar.magnitude > radiusMeters)
                planar = planar.normalized * radiusMeters;

            return transform.TransformPoint(new Vector3(planar.x, -SampleDepthMeters(planar.magnitude), planar.y));
        }

        void EnsureMesh()
        {
            if (mesh != null)
                return;

            mesh = new Mesh { name = "RatCavity (runtime)" };
            mesh.MarkDynamic();
            var vertexCount = 1 + ringCount * segmentCount;
            vertices = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var triangles = new List<int>(segmentCount * (1 + (ringCount - 1) * 2) * 3);

            uv[0] = new Vector2(0.5f, 0.5f);
            for (var r = 1; r <= ringCount; r++)
            {
                for (var s = 0; s < segmentCount; s++)
                {
                    var angle = s * Mathf.PI * 2f / segmentCount;
                    var rn = (float)r / ringCount;
                    uv[Index(r, s)] = new Vector2(0.5f + Mathf.Cos(angle) * rn * 0.5f, 0.5f + Mathf.Sin(angle) * rn * 0.5f);
                }
            }

            for (var s = 0; s < segmentCount; s++)
            {
                var next = (s + 1) % segmentCount;
                triangles.Add(0);
                triangles.Add(Index(1, next));
                triangles.Add(Index(1, s));
            }

            for (var r = 1; r < ringCount; r++)
            {
                for (var s = 0; s < segmentCount; s++)
                {
                    var next = (s + 1) % segmentCount;
                    var a = Index(r, s);
                    var b = Index(r, next);
                    var c = Index(r + 1, s);
                    var d = Index(r + 1, next);
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.SetTriangles(triangles, 0);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            bandProgress[0] = bandProgress[1] = bandProgress[2] = 0f;
            Rebuild();
        }

        int Index(int ring, int segment) => 1 + (ring - 1) * segmentCount + segment;

        void Rebuild()
        {
            vertices[0] = new Vector3(0f, -maxDepthMeters * Depth01(0f), 0f);
            for (var r = 1; r <= ringCount; r++)
            {
                var rn = (float)r / ringCount;
                var radius = rn * radiusMeters;
                var y = -maxDepthMeters * Depth01(rn);
                for (var s = 0; s < segmentCount; s++)
                {
                    var angle = s * Mathf.PI * 2f / segmentCount;
                    vertices[Index(r, s)] = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                }
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (meshRenderer != null)
            {
                meshRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(weatheredColor, freshColor, progress));
                meshRenderer.SetPropertyBlock(block);
            }
        }

        // 0 na superfície (borda do disco); nas faixas, a fração da profundidade já removida.
        float Depth01(float rn)
        {
            if (rn >= 1f)
                return 0f;

            var v = bandProgress[2];
            v = Mathf.Lerp(v, bandProgress[1], Smooth((rn - (MiddleBandInnerFraction - BandBlendWidth)) / (2f * BandBlendWidth)));
            v = Mathf.Lerp(v, bandProgress[0], Smooth((rn - (OuterBandInnerFraction - BandBlendWidth)) / (2f * BandBlendWidth)));
            return v * Smooth((1f - rn) / EdgeWidth);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Faixas, medidas e malha. Vazio = pronto. Chamado pelo teste do prefab.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(RatCavitySurface);
            if (radiusMeters > 0.03f)
                ContractValidation.Report(errors, owner, $"raio {radiusMeters} m maior que o previsto (~22,5 mm): não é uma cavidade rasa de RAT.");

            if (maxDepthMeters > MaxCapacityMeters + 1e-6f)
                ContractValidation.Report(errors, owner, $"profundidade {maxDepthMeters} m passa de 5 mm.");

            if (!Mathf.Approximately(transform.lossyScale.x, 1f) || !Mathf.Approximately(transform.lossyScale.y, 1f) || !Mathf.Approximately(transform.lossyScale.z, 1f))
                ContractValidation.Report(errors, owner, "a escala acumulada deve ser 1 (as medidas são metros reais).");

            return errors;
        }
    }
}
