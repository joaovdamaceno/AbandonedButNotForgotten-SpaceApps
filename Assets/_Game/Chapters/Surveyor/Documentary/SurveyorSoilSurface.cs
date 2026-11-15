using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Local deformation driven by the bucket's swept contact, never by a hold timer.</summary>
    public sealed class SurveyorSoilSurface : MonoBehaviour
    {
        [SerializeField] MeshFilter surface;
        Mesh mesh;
        Vector3[] baseline, vertices;
        float[] removed, vertexArea;
        float dirtySeconds;
        bool dirty;
        public float RemovedCubicMeters { get; private set; }
        public float TravelMeters { get; private set; }
        public float DeepestMeters { get; private set; }
        public Mesh RuntimeMesh => mesh;
        public void Configure(MeshFilter filter) => surface = filter;
        public void Initialize()
        {
            if (mesh != null) return;
            mesh = Instantiate(surface.sharedMesh); mesh.name = "Player lunar excavation";
            surface.sharedMesh = mesh; baseline = mesh.vertices; vertices = (Vector3[])baseline.Clone();
            removed = new float[vertices.Length]; vertexArea = new float[vertices.Length]; mesh.MarkDynamic();
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                float thirdArea = Vector3.Cross(baseline[b] - baseline[a], baseline[c] - baseline[a]).magnitude / 6f;
                vertexArea[a] += thirdArea; vertexArea[b] += thirdArea; vertexArea[c] += thirdArea;
            }
        }
        public float Height(float x, float z)
        {
            // The original patch is regular in the site plane but tilted locally: inverse-distance samples.
            float total = 0f, value = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                float dx = x - vertices[i].x, dz = z - vertices[i].z, d = dx * dx + dz * dz;
                if (d < .00001f) return vertices[i].y;
                if (d > .003f) continue;
                float w = 1f / (d + .00001f); value += vertices[i].y * w; total += w;
            }
            return total > 0f ? value / total : 0f;
        }
        public float Cut(Vector3 from, Vector3 to, float depthMeters)
        {
            Initialize(); float distance = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
            // A physical stroke must work above 100 Hz too: .055 m/s advances less than .8 mm per frame.
            if (distance < .000001f || distance > .08f) return 0f;
            float depth = Mathf.Clamp(depthMeters, 0f, .10f), volume = 0f;
            Vector2 a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z), delta = b - a;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 p = new Vector2(baseline[i].x, baseline[i].z);
                float along = Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude);
                float d = Vector2.Distance(p, a + delta * along);
                // Flat bottom matches the 9 cm bucket; a V-shaped brush would collide with its own uncut sides.
                float target = Mathf.Min(depth, removed[i] + distance * .16f * (1f - Mathf.SmoothStep(0f, 1f, (d - .047f) / .028f)));
                float amount = Mathf.Max(0f, target - removed[i]);
                if (amount > 0f) { removed[i] += amount; volume += amount * vertexArea[i]; DeepestMeters = Mathf.Max(DeepestMeters, removed[i]); }
                // A low berm follows each real stroke; fixed perimeter remains untouched.
                float berm = .010f * Mathf.Exp(-Mathf.Pow((d - .075f) / .018f, 2f)) * Mathf.Clamp01(depth / .08f);
                float edge = Mathf.Clamp01((.49f - Mathf.Abs(p.x)) / .1f) * Mathf.Clamp01((.30f - Mathf.Abs(p.y)) / .07f);
                vertices[i].y = baseline[i].y - removed[i] + (removed[i] < .001f ? berm * edge : 0f);
            }
            if (volume > 0f) { RemovedCubicMeters += volume; TravelMeters += distance; dirty = true; }
            return volume;
        }
        public void Press(Vector3 point, float strength)
        {
            Initialize();
            for (int i = 0; i < vertices.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(point.x, point.z), new Vector2(baseline[i].x, baseline[i].z));
                if (d > .055f) continue;
                float amount = Mathf.Min(.018f, strength) * (1f - d / .055f);
                removed[i] = Mathf.Max(removed[i], amount); vertices[i].y = baseline[i].y - removed[i];
            }
            dirty = true;
        }
        public void Flush(float activeDeltaSeconds)
        {
            dirtySeconds += activeDeltaSeconds;
            if (!dirty || dirtySeconds < .05f) return;
            dirty = false; dirtySeconds = 0f; mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
