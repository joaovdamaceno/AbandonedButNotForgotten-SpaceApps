using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    [System.Serializable] public sealed class MusicCue {public string id;public AudioClip clip;public float gain=1,fade=2;public bool loop=true;}
    public sealed class MissionAudio : MonoBehaviour
    {
        public AudioSource musicA,musicB,wind,drive,abrasion,ui;
        public AudioClip opening,exploration,goodbye,confirmation,warning,cameraClick,landingImpact;
        public AudioClip airbagRelease;
        public AudioClip radioCall;public MechanicalFeedback feedback;
        public MusicCue[] cues;
        AudioSource current,fading;float fade,fadeSeconds=2,gain=1,gainTarget=1,oldGain=1,windLevel,driveLevel,toolLevel;bool surface,paused;
        public float CinematicEnvelope{get;set;}=1;public bool QuietEnvironment{get;set;}
        public float Focus{get;set;}=1;
        public string CurrentCue{get;private set;}
        public static float CalmPresence(float time)
        {float p=Mathf.Repeat(time+3,38);return Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,10,p))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(17,24,p)));}
        public static float WindTarget(float storm,float time,bool science)
        {float calm=.002f+.022f*CalmPresence(time);return Mathf.Lerp(calm,.105f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.95f,storm)))*(science?.18f:1);}
        public void Cue(string id)
        {
            if(id==CurrentCue)return;
            if(cues!=null)foreach(var c in cues)if(c.id==id){CurrentCue=id;Switch(c.clip,c.fade,c.gain,c.loop);return;}
        }
        public void Enter(ChapterStage stage)
        {
            surface=stage>=ChapterStage.Landing&&stage<=ChapterStage.Transition;
            switch(stage)
            {
                case ChapterStage.Intro:Cue("orbit");break;
                case ChapterStage.Landing:Cue("descent");break;
                case ChapterStage.Activation:Cue("arrival");break;
                case ChapterStage.HeatShieldExploration:Cue("explore");break;
                case ChapterStage.Exploration:Cue("explore");break;
                case ChapterStage.RockAnalysis:Cue("science");break;
                case ChapterStage.FinalExploration:Cue("valley");break;
                case ChapterStage.DustStorm:Cue("storm");break;
                case ChapterStage.ChapterEnd:Cue("farewell");break;
            }
        }
        void Switch(AudioClip clip,float seconds,float level,bool loop)
        {
            if(current&&current.clip==clip){gainTarget=level;return;}
            if(fading)fading.Stop();fading=current;oldGain=gain;gain=gainTarget=level;current=current==musicA?musicB:musicA;
            current.Stop();current.clip=clip;current.loop=loop;current.volume=0;if(clip)current.Play();fade=0;fadeSeconds=Mathf.Max(.15f,seconds);
        }
        public void Crossfade(AudioClip clip){CurrentCue="";Switch(clip,2,1,true);}
        public void Levels(float storm,float speed,bool silent)
        {
            if(paused)return;
            if(drive.gameObject.activeInHierarchy&&!drive.isPlaying)drive.Play();if(abrasion.gameObject.activeInHierarchy&&!abrasion.isPlaying)abrasion.Play();
            windLevel=surface?WindTarget(storm,feedback?feedback.chapter.Elapsed:Time.time,feedback&&(feedback.chapter.science.Active||feedback.chapter.investigation.Active)):0;
            if(QuietEnvironment)windLevel=0;driveLevel=Mathf.Clamp01(Mathf.Abs(speed))*.16f;drive.pitch=.65f+Mathf.Abs(speed)*.25f;
            if(silent){driveLevel=toolLevel=0;windLevel*=.38f;}
        }
        public void Tool(float amount){toolLevel=feedback?0:amount*.2f;abrasion.pitch=.7f+amount*.6f;}
        public void Confirm(){if(confirmation)ui.PlayOneShot(confirmation,GameSettings.Interface*.4f);}
        public void Warn(){if(warning)ui.PlayOneShot(warning,GameSettings.Interface*.32f);}
        public void Radio(bool response){StartCoroutine(RadioSequence(response));}
        System.Collections.IEnumerator RadioSequence(bool response){ui.pitch=.9f;ui.PlayOneShot(radioCall?radioCall:confirmation,GameSettings.Interface*.3f);if(response){yield return new WaitForSeconds(.72f);ui.pitch=1.15f;ui.PlayOneShot(radioCall?radioCall:confirmation,GameSettings.Interface*.18f);}yield return new WaitForSeconds(.4f);ui.pitch=1;}
        public void Photograph(){if(cameraClick)ui.PlayOneShot(cameraClick,GameSettings.Interface*.45f);if(feedback)feedback.AcknowledgePhoto();}
        public void Impact(float strength){if(landingImpact)ui.PlayOneShot(landingImpact,GameSettings.Effects*strength*.55f);}
        public void ReleasePressure(){if(airbagRelease)ui.PlayOneShot(airbagRelease,GameSettings.Effects*.38f);}
        public void Pause(bool value){paused=value;foreach(var s in new[]{musicA,musicB,wind,drive,abrasion}){if(value)s.Pause();else s.UnPause();}if(feedback)feedback.Pause(value);}
        void Start(){wind.Play();if(drive.gameObject.activeInHierarchy)drive.Play();if(abrasion.gameObject.activeInHierarchy)abrasion.Play();}
        void Update()
        {
            AudioListener.volume=GameSettings.Master;if(paused)return;fade=Mathf.Min(1,fade+Time.unscaledDeltaTime/fadeSeconds);
            gain=Mathf.MoveTowards(gain,gainTarget,Time.unscaledDeltaTime*.18f);
            if(current)current.volume=Mathf.Sin(fade*Mathf.PI*.5f)*GameSettings.Music*gain*CinematicEnvelope*Focus;
            if(fading){fading.volume=Mathf.Cos(fade*Mathf.PI*.5f)*GameSettings.Music*oldGain*CinematicEnvelope*Focus;if(fade==1){fading.Stop();fading=null;}}
            wind.volume=Mathf.Lerp(wind.volume,windLevel*GameSettings.Effects*CinematicEnvelope,Time.unscaledDeltaTime*2);drive.volume=Mathf.Lerp(drive.volume,driveLevel*GameSettings.Effects*CinematicEnvelope,Time.unscaledDeltaTime*6);abrasion.volume=Mathf.Lerp(abrasion.volume,toolLevel*GameSettings.Effects*CinematicEnvelope,Time.unscaledDeltaTime*6);
        }
    }
}
