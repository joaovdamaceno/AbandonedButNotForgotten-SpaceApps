using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    public sealed class ToolStressFeedback:MonoBehaviour
    {
        public RockAbrasionMinigame science;public Renderer collar;public ParticleSystem heatVapour;
        MaterialPropertyBlock block;Color cold;
        void Awake(){block=new MaterialPropertyBlock();cold=collar.sharedMaterial.GetColor("_BaseColor");}
        void LateUpdate()
        {
            if(Time.timeScale==0)return;
            float heat=science.Active?science.Temperature:0;
            collar.GetPropertyBlock(block);block.SetColor("_BaseColor",Color.Lerp(cold,new Color(.78f,.31f,.055f),Mathf.SmoothStep(0,1,heat)));block.SetColor("_EmissionColor",new Color(.34f,.10f,.008f)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,heat)));collar.SetPropertyBlock(block);
            var emission=heatVapour.emission;emission.rateOverTime=heat>.42f?Mathf.InverseLerp(.42f,1,heat)*9:0;
            if(heat>.42f&&!heatVapour.isPlaying)heatVapour.Play();
            if(!science.Active)heatVapour.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
