using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Abandoned.SpaceApps.Opportunity
{
    public sealed class MarsEnvironment : MonoBehaviour
    {
        public GameObject eagle,valley,space,travel,heatShield;public Light sun;public Material sky;public ParticleSystem driftingDust,stormDust;
        public Transform planet,dustFront;public RoverController rover;public Volume post;public ReflectionProbe reflections;
        public float Storm{get;private set;}public bool OnSurface{get;private set;}
        public bool Indoors{get;set;}
        public bool Memorial{get;set;}
        public float Gust{get;set;}
        public bool StoryLighting{get;set;}
        public Vector3 StorySun{get;set;}
        public float StoryExposure{get;set;}
        float targetStorm;ColorAdjustments grade;Vignette vignette;bool year;
        void Awake(){if(post){post.profile.TryGet(out grade);post.profile.TryGet(out vignette);}}
        public void Region(bool year2018,bool orbital)
        {
            bool changed=OnSurface==orbital||year!=year2018;year=year2018;
            if(travel)travel.SetActive(false);if(heatShield)heatShield.SetActive(false);
            eagle.SetActive(!year2018&&!orbital);valley.SetActive(year2018&&!orbital);space.SetActive(orbital);OnSurface=!orbital;
            RenderSettings.skybox=orbital?null:sky;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;
            if(changed)sun.transform.rotation=orbital?Quaternion.Euler(22,-38,0):Quaternion.Euler(year2018?19:24,year2018?-64:-37,0);
            if(!orbital&&!driftingDust.isPlaying)driftingDust.Play();if(orbital){driftingDust.Stop();stormDust.Stop();}
            if(dustFront)dustFront.gameObject.SetActive(!orbital&&year2018);
            if(!orbital&&reflections&&changed)StartCoroutine(RefreshReflection());
            if(changed)rover.GetComponent<RoverSurfaceWear>()?.SetDust(year2018?.5f:0);
        }
        public void Hardware(bool enabled){if(!heatShield)return;heatShield.SetActive(enabled);if(enabled){eagle.SetActive(false);valley.SetActive(false);travel.SetActive(false);}}
        public void Travel(bool enabled) {if(!travel)return;travel.SetActive(enabled);if(enabled){eagle.SetActive(false);valley.SetActive(false);}}
        System.Collections.IEnumerator RefreshReflection(){yield return null;yield return null;if(OnSurface&&reflections&&reflections.isActiveAndEnabled)reflections.RenderProbe();}
        public void SetStorm(float value,bool immediate=false){targetStorm=Mathf.Clamp01(value);if(immediate)Storm=targetStorm;}
        void Update()
        {
            if(Time.timeScale==0)return;Storm=Mathf.MoveTowards(Storm,targetStorm,Time.deltaTime*.12f);
            driftingDust.transform.position=rover.transform.position+Vector3.up*1.4f;stormDust.transform.position=rover.transform.position+Vector3.up*4;
            RenderSettings.reflectionIntensity=Memorial?0:1;
            if(Memorial)
            {
                Shader.SetGlobalFloat("_AtmosphericDensity",0);Shader.SetGlobalFloat("_DustLevel",0);
                RenderSettings.skybox=null;RenderSettings.ambientProbe=new SphericalHarmonicsL2();
                RenderSettings.ambientSkyColor=RenderSettings.ambientEquatorColor=RenderSettings.ambientGroundColor=Color.black;
                sun.intensity=0;if(grade){grade.postExposure.value=-.35f;grade.saturation.value=-12;}
                if(vignette)vignette.intensity.value=.12f;return;
            }
            RenderSettings.skybox=OnSurface?sky:null;
            if(StoryLighting&&OnSurface)sun.transform.rotation=Quaternion.Slerp(sun.transform.rotation,Quaternion.Euler(StorySun),1-Mathf.Exp(-Time.deltaTime*1.8f));
            Shader.SetGlobalVector("_SunDirection",-sun.transform.forward);Shader.SetGlobalFloat("_DustLevel",OnSurface?Storm:0);
            RenderSettings.fog=false;
            if(OnSurface)
            {
                var air=Color.Lerp(new Color(.43f,.33f,.25f),new Color(.29f,.215f,.15f),Storm);
                Shader.SetGlobalFloat("_AtmosphericDensity",Mathf.Lerp(.0018f,.079f,Storm)+Gust*Storm*.009f);Shader.SetGlobalColor("_AtmosphericColor",air);
                RenderSettings.ambientSkyColor=Color.Lerp(new Color(.38f,.43f,.5f),new Color(.14f,.14f,.15f),Storm);
                RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.42f,.32f,.24f),new Color(.16f,.14f,.12f),Storm);RenderSettings.ambientGroundColor=new Color(.13f,.1f,.08f);
                var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(Color.Lerp(new Color(.16f,.18f,.21f),new Color(.07f,.07f,.075f),Storm));ambient.AddDirectionalLight(Vector3.up,Color.Lerp(new Color(.39f,.43f,.49f),new Color(.18f,.17f,.15f),Storm),.55f);ambient.AddDirectionalLight(Vector3.down,new Color(.38f,.25f,.16f),.16f);RenderSettings.ambientProbe=ambient;
                sun.intensity=Mathf.Lerp(2,.045f,Mathf.Pow(Storm,.7f));sun.color=Color.Lerp(new Color(1,.88f,.73f),new Color(.74f,.63f,.5f),Storm);sun.shadowStrength=Mathf.Lerp(.82f,.06f,Storm);
                sky.SetFloat("_Dust",Storm);var emission=stormDust.emission;emission.rateOverTime=Mathf.Lerp(0,145,Storm);var main=stormDust.main;main.startSpeed=Mathf.Lerp(1,5,Storm);
                if(Storm>.01f&&!stormDust.isPlaying)stormDust.Play();
                if(grade){grade.postExposure.value=Mathf.Lerp(.1f,-.05f,Storm)+(StoryLighting?StoryExposure:0);grade.contrast.value=Mathf.Lerp(13,5,Storm);grade.saturation.value=Mathf.Lerp(-6,-17,Storm);}if(vignette)vignette.intensity.value=Mathf.Lerp(.12f,.20f,Storm);
                if(dustFront){float approach=Mathf.InverseLerp(.15f,.95f,Storm);dustFront.position=new Vector3(1100,0,Mathf.Lerp(185,55,approach));}
            }
            else
            {
                if(Indoors){sun.intensity=.2f;Shader.SetGlobalFloat("_AtmosphericDensity",0);RenderSettings.ambientSkyColor=new Color(.42f,.48f,.53f);RenderSettings.ambientEquatorColor=new Color(.29f,.32f,.34f);RenderSettings.ambientGroundColor=new Color(.12f,.15f,.17f);var fill=new SphericalHarmonicsL2();fill.AddAmbientLight(new Color(.23f,.26f,.29f));RenderSettings.ambientProbe=fill;if(grade){grade.postExposure.value=.2f;grade.saturation.value=-7;}return;}
                sun.intensity=2.8f;sun.color=new Color(1,.85f,.72f);Shader.SetGlobalFloat("_AtmosphericDensity",0);RenderSettings.ambientSkyColor=new Color(.035f,.045f,.065f);RenderSettings.ambientEquatorColor=RenderSettings.ambientGroundColor=Color.black;
                if(grade){grade.postExposure.value=.1f;grade.saturation.value=-5;}if(planet)planet.Rotate(Vector3.up,Time.deltaTime*.25f,Space.Self);
            }
        }
        public void TimeLapse(float normalized){sun.transform.rotation=Quaternion.Euler(15+Mathf.Sin(normalized*Mathf.PI)*30,-80+normalized*140,0);}
    }
}
