using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor.Tests
{
    public sealed class SurveyorSoilFrameRateTests
    {
        [Test]
        public void SameStrokeRemovesEquivalentMaterialAt60And144Hz()
        {
            var slow = Stroke(60);
            var fast = Stroke(144);
            Assert.That(slow.x, Is.GreaterThan(.0001f), "A real stroke removes material.");
            Assert.That(fast.x, Is.EqualTo(slow.x).Within(slow.x * .035f));
            Assert.That(fast.y, Is.EqualTo(slow.y).Within(.002f));
            Assert.That(fast.z, Is.EqualTo(slow.z).Within(.0005f));
        }

        static Vector3 Stroke(int fps)
        {
            var go = new GameObject("Frame-rate soil test");
            var mesh = new Mesh();
            try
            {
                const int columns = 41, rows = 21;
                var vertices = new Vector3[columns * rows];
                var triangles = new int[(columns - 1) * (rows - 1) * 6];
                for (int z = 0; z < rows; z++)
                    for (int x = 0; x < columns; x++) vertices[z * columns + x] = new Vector3((x - 20) * .02f, 0f, (z - 10) * .02f);
                int t = 0;
                for (int z = 0; z < rows - 1; z++)
                    for (int x = 0; x < columns - 1; x++)
                    {
                        int a = z * columns + x;
                        triangles[t++] = a; triangles[t++] = a + columns; triangles[t++] = a + 1;
                        triangles[t++] = a + 1; triangles[t++] = a + columns; triangles[t++] = a + columns + 1;
                    }
                mesh.vertices = vertices; mesh.triangles = triangles;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var soil = go.AddComponent<SurveyorSoilSurface>(); soil.Configure(go.GetComponent<MeshFilter>()); soil.Initialize();
                const float speed = .055f;
                Vector3 from = new Vector3(-.275f, 0f, 0f);
                for (int frame = 1; frame <= fps * 10; frame++)
                {
                    var to = new Vector3(-.275f + frame * speed / fps, 0f, 0f);
                    soil.Cut(from, to, .075f); from = to;
                }
                var result = new Vector3(soil.RemovedCubicMeters, soil.TravelMeters, soil.DeepestMeters);
                Object.DestroyImmediate(soil.RuntimeMesh);
                return result;
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); }
        }
    }
}
