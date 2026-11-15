#if OPPORTUNITY_VIDEO_MEDIA
using UnityEngine;
using UnityEngine.Video;
namespace Abandoned.SpaceApps.Opportunity
{
    // Two offline inserts: laboratory at Intro+20, then the final portrait.
    public sealed class OfflineCinema:MonoBehaviour
    {
        public ChapterDirector chapter;
        public VideoClip opening,portrait;
        [HideInInspector] public VideoClip landing; // Unused field, intentionally unassigned.
        public float laboratoryStart=20;
        sealed class Channel {public VideoPlayer player;public RenderTexture texture;public bool primed,ended;}
        readonly Channel[] channels=new Channel[2];Channel active;ChapterStage stage;bool paused;
        float Offset=>stage==ChapterStage.Intro?laboratoryStart:0;
        public bool PreviewMode{get;private set;}
        public bool Active=>active!=null&&!PreviewMode;
        public bool Ready=>Active&&active.primed;
        public bool Waiting=>Active&&!active.primed;
        public Texture Picture=>Ready?active.texture:Texture2D.blackTexture;
        public double PlaybackTime=>Ready?Offset+active.player.time:Offset;
        void Awake()
        {
            for(int i=0;i<channels.Length;i++)
            {
                var c=new Channel();channels[i]=c;c.texture=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name="Offline insert "+i};c.texture.Create();
                var p=c.player=gameObject.AddComponent<VideoPlayer>();p.playOnAwake=false;p.isLooping=false;p.waitForFirstFrame=true;p.skipOnDrop=false;p.audioOutputMode=VideoAudioOutputMode.None;
                p.renderMode=VideoRenderMode.RenderTexture;p.targetTexture=c.texture;p.sendFrameReadyEvents=true;
                p.prepareCompleted+=v=>{if(!paused||c!=active)v.Play();};
                p.frameReady+=(v,f)=>{if(!c.primed){c.primed=true;if(c!=active||paused)v.Pause();}};
                p.loopPointReached+=v=>c.ended=true;
                p.errorReceived+=(v,message)=>{Debug.LogError("[Offline Cinema] "+message);if(c==active)active=null;};
            }
        }
        Channel Prepare(VideoClip clip)
        {
            if(!clip||PreviewMode)return null;
            foreach(var c in channels)if(c.player.clip==clip)
            {
                if(c!=active&&c.player.isPrepared&&(c.ended||c.player.time>.08))
                {c.primed=c.ended=false;c.player.time=0;c.player.Play();}
                return c;
            }
            var slot=channels[0]==active?channels[1]:channels[0];slot.player.Stop();slot.primed=slot.ended=false;slot.player.clip=clip;slot.player.Prepare();return slot;
        }
        void Begin(VideoClip clip){active=Prepare(clip);if(active!=null){active.ended=false;if(active.primed&&!paused)active.player.Play();}}
        public void Enter(ChapterStage next)
        {
            if(active!=null)active.player.Pause();active=null;stage=next;if(PreviewMode)return;
            if(next==ChapterStage.Farewell)Begin(portrait);
        }
        public float Clock(float previous,float dt,float duration)
        {
            if(false&&!PreviewMode&&stage==ChapterStage.Intro&&opening&&!Active) // The opening is rendered in real time; the laboratory video insert is disabled.
            {if(previous<laboratoryStart)return Mathf.Min(laboratoryStart,previous+dt);Begin(opening);}
            if(!Active)return previous+dt;
            return active.ended?duration:Mathf.Min(duration,(float)PlaybackTime);
        }
        public void Seek(double chapterTime)
        {
            if(!Active&&stage==ChapterStage.Intro&&chapterTime>=laboratoryStart)Begin(opening);
            if(active!=null&&active.player.isPrepared)active.player.time=System.Math.Max(0,System.Math.Min(active.player.length-.05,chapterTime-Offset));
        }
        public void SetPaused(bool value){paused=value;if(active==null)return;if(value)active.player.Pause();else if(active.player.isPrepared)active.player.Play();}
        public void SetPreview(bool value){PreviewMode=value;if(value){foreach(var c in channels)c.player.Pause();active=null;}}
        void Update(){if(!PreviewMode&&chapter.Stage==ChapterStage.CommunicationLost&&chapter.Elapsed>0)Prepare(portrait);}
        void OnDestroy(){foreach(var c in channels)if(c!=null){c.player.Stop();c.texture.Release();Destroy(c.texture);}}
    }
}

#endif
