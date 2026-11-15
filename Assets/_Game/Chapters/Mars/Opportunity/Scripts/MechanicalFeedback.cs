using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    // Gameplay drives the layers: removal, actuator motion, gusts and actual transmission.
    public sealed class MechanicalFeedback : MonoBehaviour
    {
        public ChapterDirector chapter;
        public AudioSource free,contact,load,servo,gust,grains,uplink,chassis,transients;
        public AudioClip startMotor,stopMotor,suspension;
        public AudioSource character;public AudioClip awakeResponse,noticeResponse,settleResponse;
        float lastResponse=-10;bool labAwake;
        bool grinding;float lastRattle;Vector3 previousTool;Quaternion previousMast,previousBody;ChapterStage previousStage;
        AudioSource[] sources;
        void Start()
        {
            sources=new[]{free,contact,load,servo,gust,grains,uplink,chassis,transients,character};
            foreach(var s in sources)if(s&&s.loop&&s.gameObject.activeInHierarchy)s.Play();
            previousTool=chapter.rover.tool.localPosition;previousBody=chapter.rover.visual.localRotation;previousMast=chapter.film.mast.localRotation;
        }
        public void Pause(bool paused)
        {if(sources==null)return;foreach(var s in sources)if(s){if(paused)s.Pause();else s.UnPause();}}
        void Update()
        {
            if(chapter.Paused||Time.deltaTime<=0)return;
            foreach(var source in sources)if(source&&source.loop&&source.gameObject.activeInHierarchy&&!source.isPlaying)source.Play();
            var m=chapter.science;var sound=chapter.sound;var stage=chapter.Stage;
            if(stage!=previousStage)
            {
                if(stage==ChapterStage.Intro){grinding=false;transients.Stop();chassis.Stop();}
                if(stage==ChapterStage.Intro)labAwake=false;
                if(stage==ChapterStage.Activation)Respond(awakeResponse,.38f);
                if(stage==ChapterStage.Discovery)Respond(noticeResponse,.30f);
                if(stage==ChapterStage.CommunicationLost)Respond(settleResponse,.24f);
                previousStage=stage;previousTool=chapter.rover.tool.localPosition;
            }
            if(stage==ChapterStage.Intro&&chapter.Elapsed>23.8f&&!labAwake){labAwake=true;Respond(awakeResponse,.34f);}
            bool working=m.Active?m.Working:chapter.film.ToolOperating;
            float pressure=m.Active?m.Pressure:.52f,removal=m.Active?m.RemovalFeedback:.7f;
            if(working&&!grinding)transients.PlayOneShot(startMotor,GameSettings.Effects*.44f);
            if(!working&&grinding)transients.PlayOneShot(stopMotor,GameSettings.Effects*.35f);
            grinding=working;
            float envelope=GameSettings.Effects*sound.CinematicEnvelope;
            Level(free,working?.15f*(1-removal*.6f):0,envelope);
            Level(contact,working?removal*.38f:0,envelope);
            Level(load,working?Mathf.InverseLerp(m.Active?m.PressureHigh:.72f,1,pressure)*.23f:0,envelope);
            free.pitch=Mathf.Lerp(.75f,1.12f,m.Active?m.ToolRPM/1450:1);
            contact.pitch=Mathf.Lerp(1.15f,.82f,pressure);load.pitch=Mathf.Lerp(1,.7f,pressure);
            float movement=Vector3.Distance(chapter.rover.tool.localPosition,previousTool)/Mathf.Max(.001f,Time.deltaTime);
            movement+=Quaternion.Angle(chapter.film.mast.localRotation,previousMast)/Mathf.Max(.001f,Time.deltaTime)*.007f;
            previousTool=chapter.rover.tool.localPosition;previousMast=chapter.film.mast.localRotation;
            bool machinery=chapter.rover.gameObject.activeInHierarchy&&chapter.rover.Power>.02f&&!sound.QuietEnvironment;
            Level(servo,machinery?Mathf.Clamp01(movement*2)*.20f:0,envelope);servo.pitch=.82f+Mathf.Clamp01(movement)*.25f;
            float wind=chapter.environment.OnSurface&&!chapter.environment.Memorial?chapter.environment.Storm:0;
            float burst=chapter.weather?chapter.weather.Gust:0;
            Level(gust,wind*burst*.20f,envelope);Level(grains,wind*wind*(.018f+burst*.075f),envelope);
            gust.panStereo=(Mathf.PerlinNoise(Time.time*.11f,4)-.5f)*.6f;grains.panStereo=(Mathf.PerlinNoise(8,Time.time*.29f)-.5f)*.75f;
            Level(uplink,chapter.journey.Transmitting?.16f:0,envelope);
            float tilt=Quaternion.Angle(previousBody,chapter.rover.visual.localRotation);previousBody=chapter.rover.visual.localRotation;
            if(machinery&&Mathf.Abs(chapter.rover.Speed)>.3f&&tilt>.12f&&Time.time-lastRattle>.65f)
            {lastRattle=Time.time;chassis.pitch=.91f+Mathf.PerlinNoise(Time.time,2)*.2f;chassis.PlayOneShot(suspension,GameSettings.Effects*.22f);}
            sound.Focus=Mathf.MoveTowards(sound.Focus,working?.82f:wind>.5f?Mathf.Lerp(1,.92f,burst):1,Time.deltaTime*.6f);
        }
        void Respond(AudioClip clip,float level)
        {if(!character||!clip||chapter.Paused||Time.time-lastResponse<2.2f)return;lastResponse=Time.time;character.pitch=1;character.PlayOneShot(clip,GameSettings.Effects*level);}
        public void AcknowledgePhoto()=>Respond(noticeResponse,.26f);
        public void Observe()=>Respond(noticeResponse,.18f);
        static void Level(AudioSource source,float value,float envelope)
        {if(source)source.volume=Mathf.MoveTowards(source.volume,value*envelope,Time.deltaTime*.8f);}
    }
}
