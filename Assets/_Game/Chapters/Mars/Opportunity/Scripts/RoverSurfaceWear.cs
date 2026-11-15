using System.Collections.Generic;
using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>Poeira marciana sobre os materiais URP/Lit do rover (<c>_BaseColor</c> e multiplicador de <c>_Smoothness</c>), por MaterialPropertyBlock.</summary>
    public sealed class RoverSurfaceWear : MonoBehaviour
    {
        struct Surface { public Renderer renderer; public int index; public Color color; public float smoothness; }
        static readonly int Tint = Shader.PropertyToID("_BaseColor"), Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly Color Dust = new Color(.83f, .73f, .59f, 1);
        const float DustySmoothness = .35f;
        Surface[] surfaces;
        MaterialPropertyBlock block;

        public void SetDust(float amount)
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (surfaces == null) surfaces = Collect();
            foreach (var s in surfaces)
            {
                s.renderer.GetPropertyBlock(block, s.index);
                block.SetColor(Tint, Color.Lerp(s.color, s.color * Dust, amount));
                block.SetFloat(Smoothness, Mathf.Lerp(s.smoothness, s.smoothness * DustySmoothness, amount));
                s.renderer.SetPropertyBlock(block, s.index);
            }
        }

        Surface[] Collect()
        {
            var cache = new List<Surface>();
            // Só malhas do rover: partículas e rastros também têm _BaseColor e não devem ganhar poeira.
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (m && m.HasProperty(Tint))
                        cache.Add(new Surface { renderer = r, index = i, color = m.GetColor(Tint), smoothness = m.HasProperty(Smoothness) ? m.GetFloat(Smoothness) : 1f });
                }
            }
            return cache.ToArray();
        }
    }
}
