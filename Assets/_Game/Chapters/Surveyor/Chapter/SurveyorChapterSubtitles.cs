using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorChapterSubtitles:MonoBehaviour
    {
        SubtitlePresenter presenter;SurveyorSubtitles data;SessionClock clock;SettingsStore settings;SubtitleCue original,shown;SurveyorSubtitles.CueBinding binding;float time;bool second;
        public void Initialize(SubtitlePresenter target,SurveyorSubtitles catalog,SessionClock session,SettingsStore preferences){presenter=target;data=catalog;clock=session;settings=preferences;}
        public void Show(SubtitleCue cue)
        {
            if(original!=null)Hide(original);original=cue;time=0;second=false;binding=null;
            if(data.TryGetCue(cue.TextKey,out var b)&&b.HasPages&&settings.Current.textScale>1.1f)binding=b;
            shown=binding==null?cue:SubtitleCue.Triggered(cue.CueId+".p1",binding.pageKeys[0],"surveyor.subtitle.page",binding.pageMinimumSeconds[0]);presenter.ShowCue(shown);
        }
        public void Hide(SubtitleCue cue){if(original==null||original.CueId!=cue.CueId)return;presenter.HideCue(shown);original=shown=null;binding=null;}
        void Update()=>Tick(clock!=null?clock.ActiveDeltaSeconds:0);
        public void Tick(float activeDeltaSeconds)
        {
            if(binding==null||second)return;time+=activeDeltaSeconds;if(time<binding.pageMinimumSeconds[0])return;
            presenter.HideCue(shown);second=true;shown=SubtitleCue.Triggered(original.CueId+".p2",binding.pageKeys[1],"surveyor.subtitle.page.next",binding.pageMinimumSeconds[1]);presenter.ShowCue(shown);
        }
        void OnDisable(){if(original!=null)Hide(original);}
    }
}
