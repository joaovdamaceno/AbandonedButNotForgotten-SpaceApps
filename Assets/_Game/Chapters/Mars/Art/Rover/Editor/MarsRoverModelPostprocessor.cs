using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-011: o FBX sai do Blender com eixo Y declarado e sem <c>bake_space_transform</c>, então o
    /// <c>Bake Axis Conversion</c> da Unity não tem o que converter: a conversão fica como rotação de 90° no nó raiz e
    /// filhos/malhas continuam no referencial Z-up do Blender. Este pós-processador assa na importação a rotação da raiz
    /// mais 180° em Y (frente Blender −Y → Unity +Z, esquerda Blender +X → Unity −X, ver <c>conversion.json</c>) nas
    /// posições dos nós e nos vértices, deixando raiz e nós com rotação local identidade (pose de repouso do RIG.md).
    /// Também remove o LODGroup que o importador cria na raiz: cada nó tem o seu (<see cref="MarsRoverVisualAssets.EnsureLodGroups"/>).
    /// </summary>
    public sealed class MarsRoverModelPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPostprocessModel(GameObject root)
        {
            if (assetPath != MarsRoverVisualAssets.ModelPath) return;

            var rootLod = root.GetComponent<LODGroup>();
            if (rootLod != null) Object.DestroyImmediate(rootLod, true);

            var rotation = Quaternion.Euler(0f, 180f, 0f) * root.transform.localRotation;
            root.transform.localRotation = Quaternion.identity;

            var descendants = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in descendants)
            {
                if (t == root.transform) continue;
                if (Quaternion.Angle(t.localRotation, Quaternion.identity) > 0.01f)
                {
                    Debug.LogError($"[MAR-011] {assetPath}: nó {t.name} com rotação local; o bake de eixo supõe identidade.", root);
                    return;
                }
            }

            var meshes = new HashSet<Mesh>();
            foreach (var t in descendants)
            {
                if (t == root.transform) continue;
                t.localPosition = rotation * t.localPosition;
                var filter = t.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
            }
            foreach (var mesh in meshes) Rotate(mesh, rotation);
        }

        static void Rotate(Mesh mesh, Quaternion rotation)
        {
            var vertices = mesh.vertices;
            for (var i = 0; i < vertices.Length; i++) vertices[i] = rotation * vertices[i];
            mesh.vertices = vertices;

            var normals = mesh.normals;
            if (normals.Length > 0)
            {
                for (var i = 0; i < normals.Length; i++) normals[i] = rotation * normals[i];
                mesh.normals = normals;
            }

            var tangents = mesh.tangents;
            if (tangents.Length > 0)
            {
                for (var i = 0; i < tangents.Length; i++)
                {
                    var t = rotation * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                }
                mesh.tangents = tangents;
            }
            mesh.RecalculateBounds();
        }
    }
}
