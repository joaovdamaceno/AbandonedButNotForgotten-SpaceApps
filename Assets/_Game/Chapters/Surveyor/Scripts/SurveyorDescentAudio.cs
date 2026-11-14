using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Sonificação estrutural original: não é vento lunar ou gravação da sonda.</summary>
    public sealed class SurveyorDescentAudio:MonoBehaviour,IDisposable
    {
        SurveyorDescent flow;SessionClock clock;AudioSource engine,impacts;AudioClip first,second,third;SettingsData settings;
        bool bound,paused;
        public void Configure(SurveyorDescent controller,SessionClock session,AudioSource motor,AudioSource contact,AudioClip a,AudioClip b,AudioClip c,SettingsData data)
        {
            Dispose();flow=controller;clock=session;engine=motor;impacts=contact;first=a;second=b;third=c;settings=data;
            bound=true;flow.GroundContact+=OnContact;flow.Changed+=Refresh;
        }
        public void ApplySettings(SettingsData data){settings=data;Refresh();}
        void OnContact(int number)
        {if(Application.isPlaying && settings.masterVolume*settings.sfxVolume>0)impacts.PlayOneShot(number==1?first:number==2?second:third,.32f*settings.masterVolume*settings.sfxVolume);}
        void Refresh()
        {
            if(!Application.isPlaying || engine==null || settings==null)return;
            if(!flow.Running){engine.Stop();impacts.Stop();engine.volume=0;return;}
            if(flow.IsPaused!=paused){paused=flow.IsPaused;if(paused){engine.Pause();impacts.Pause();}else{engine.UnPause();impacts.UnPause();}}
            if(paused)return;
            float target=flow.EnginesCutOff?0:.065f*settings.masterVolume*settings.sfxVolume;
            if(target>0&&!engine.isPlaying)engine.Play();engine.volume=Mathf.MoveTowards(engine.volume,target,clock.ActiveDeltaSeconds*.30f);
            if(engine.volume<=0&&target<=0)engine.Stop();
            if(flow.TimeSeconds>=flow.QuietAt)impacts.Stop();
        }
        public void Dispose()
        {
            if(engine!=null)engine.Stop();if(impacts!=null)impacts.Stop();if(!bound)return;bound=false;flow.GroundContact-=OnContact;flow.Changed-=Refresh;
        }
        void OnDisable()=>Dispose();
    }
}
