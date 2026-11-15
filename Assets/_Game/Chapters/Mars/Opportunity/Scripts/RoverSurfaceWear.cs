using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    public sealed class RoverSurfaceWear : MonoBehaviour
    {
        struct Surface { public Renderer renderer;public int index;public Color color;public float roughness,metal; }
        Surface[] surfaces;MaterialPropertyBlock block;
        static readonly int Tint=Shader.PropertyToID("baseColorFactor"),Roughness=Shader.PropertyToID("roughnessFactor"),Metal=Shader.PropertyToID("metallicFactor");
        public void SetDust(float amount)
        {
            if(block==null)block=new MaterialPropertyBlock();
            if(surfaces==null)
            {
                var cache=new System.Collections.Generic.List<Surface>();
                foreach(var r in GetComponentsInChildren<Renderer>(true))for(int i=0;i<r.sharedMaterials.Length;i++){var m=r.sharedMaterials[i];if(m&&m.HasProperty(Tint))cache.Add(new Surface{renderer=r,index=i,color=m.GetColor(Tint),roughness=m.HasProperty(Roughness)?m.GetFloat(Roughness):.5f,metal=m.HasProperty(Metal)?m.GetFloat(Metal):0});}
                surfaces=cache.ToArray();
            }
            foreach(var s in surfaces){s.renderer.GetPropertyBlock(block,s.index);block.SetColor(Tint,Color.Lerp(s.color,s.color*new Color(.83f,.73f,.59f,1),amount));block.SetFloat(Roughness,Mathf.Lerp(s.roughness,.85f,amount));block.SetFloat(Metal,Mathf.Lerp(s.metal,.25f,amount));s.renderer.SetPropertyBlock(block,s.index);}
        }
    }
}
