using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
namespace Abandoned.SpaceApps.Opportunity
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionHUD : MonoBehaviour
    {
        public ChapterDirector chapter;
        VisualElement tutorialPanel, assistance;
        Label tutorialTitle,tutorialBody,assistanceText;
        public Texture2D ratPhoto, routeMap, panorama;
        VisualElement root,flight,cinema,science,pause,settings,controls,credits,journal,end,fade,marker,photo,memory,photoFlash;
        VisualElement endActions,thermal,thermalFill,safeBand,rockHint;
        VisualElement pressureFill,grindFill,energyFill,cursor,featureA,featureB,energyBox,memoryChoices;
        Image remembrance,chosenImage;
        Label exploreHint,scienceFindings,objective,distance,location,help,energy,caption,heading,story,scienceTitle,scienceBody,scienceReadout,notification,skip,photoCount,memoryTitle;
        VisualElement splitView,splitDivider,splitTopLabel,splitBottomLabel,letterTop,letterBottom;Label splitTopName,splitTopPlace,splitBottomName,splitBottomPlace,splitNote,fieldGoal,scienceGoal;
        float toastTime,objectiveAge;
        string toastKey,objectiveKey,activeLanguage,lastFilmTitle;
        Font filmSans;VisualElement filmVeil;Image orbitEvidence;Label orbitCredit;
        bool journalOpen,wasChoosing;
        VisualElement field,fieldFill,fieldReticle,fieldPictures,fieldCenter,framingFill;Label fieldTitle,fieldBody,fieldStatus,comparisonA,comparisonB;Image fieldA,fieldB,archiveImage;Label archiveCredit;
        readonly System.Collections.Generic.Dictionary<FieldStudy,Image> studyAlbum=new System.Collections.Generic.Dictionary<FieldStudy,Image>();
        int galleryVersion=-1,selected=-1;
        public VisualElement Root=>root;
        void OnEnable()
        {
            root=GetComponent<UIDocument>().rootVisualElement;root.Clear();studyAlbum.Clear();root.StretchToParentSize();root.styleSheets.Add(Resources.Load<StyleSheet>("MissionUI"));root.AddToClassList("root");
            var font=Resources.Load<Font>("Fonts/OutfitMedium");if(font!=null)root.style.unityFontDefinition=FontDefinition.FromFont(font);
            filmSans=Resources.Load<Font>("Fonts/OutfitMedium");Build();
        }
        VisualElement Box(VisualElement parent,string classes)
        {var e=new VisualElement();foreach(var c in classes.Split(' '))if(c.Length>0)e.AddToClassList(c);parent.Add(e);return e;}
        Label Text(VisualElement parent,string value,string classes="")
        {
            var e=new Label(value);foreach(var c in classes.Split(' '))if(c.Length>0)e.AddToClassList(c);parent.Add(e);
            if(classes.Contains("display")){var f=Resources.Load<Font>("Fonts/CormorantGaramond");if(f!=null)e.style.unityFontDefinition=FontDefinition.FromFont(f);}return e;
        }
        Button Button(VisualElement parent,string key,System.Action action,string classes="")
        {var b=new Button(()=>{chapter.sound.Confirm();action();}){text=Localization.Get(key),userData=key};b.AddToClassList("button");foreach(var c in classes.Split(' '))if(c.Length>0)b.AddToClassList(c);parent.Add(b);return b;}
        void Build()
        {
            Box(root,"edge-shade").pickingMode=PickingMode.Ignore;
            flight=Box(root,"flight");flight.pickingMode=PickingMode.Ignore;
            var id=Box(flight,"identity");Text(id,"MER-B   /   OPPORTUNITY","eyebrow");location=Text(id,"","small muted");
            var goal=Box(flight,"goal");objective=Text(goal,"","objective");distance=Text(goal,"","small muted");
            help=Text(flight,"","controls");energyBox=Box(flight,"solar");energy=Text(energyBox,"","eyebrow");energyFill=Box(Box(energyBox,"energy-track"),"energy-fill");
            marker=Box(flight,"marker");Box(marker,"marker-halo");Box(marker,"marker-diamond");Box(marker,"marker-bar");Box(marker,"marker-dot");Text(marker,"‹","marker-glyph");marker.pickingMode=PickingMode.Ignore;foreach(var part in marker.Children())part.pickingMode=PickingMode.Ignore;rockHint=Box(flight,"interaction-hint");rockHint.name="rock-interaction";Text(rockHint,"E","interaction-key");Text(rockHint,"","interaction-name").name="rock-interaction-label";Text(rockHint,"","interaction-sub").name="rock-interaction-sub";
            cinema=Box(root,"cinema");cinema.pickingMode=PickingMode.Ignore;
            filmVeil=Box(cinema,"film-veil");filmVeil.pickingMode=PickingMode.Ignore;
            letterTop=Box(cinema,"letterbox top");letterBottom=Box(cinema,"letterbox bottom");var words=Box(cinema,"cinematic-words");
            heading=Text(words,"","display cinematic-heading");story=Text(words,"","cinematic-story");caption=Text(cinema,"","caption");skip=Text(cinema,"","skip");
            orbitEvidence=new Image{scaleMode=ScaleMode.ScaleToFit};orbitEvidence.AddToClassList("orbital-evidence");cinema.Add(orbitEvidence);orbitCredit=Text(cinema,"","orbital-credit");
            remembrance=new Image{scaleMode=ScaleMode.ScaleAndCrop};remembrance.AddToClassList("remembrance");cinema.Add(remembrance);
            splitView=Box(root,"split-view");splitView.pickingMode=PickingMode.Ignore;splitDivider=Box(splitView,"split-divider");
            splitTopLabel=Box(splitView,"split-label");splitTopName=Text(splitTopLabel,"","split-name");splitTopPlace=Text(splitTopLabel,"","split-place");
            splitBottomLabel=Box(splitView,"split-label");splitBottomName=Text(splitBottomLabel,"","split-name");splitBottomPlace=Text(splitBottomLabel,"","split-place");
            splitNote=Text(splitView,"","split-note");foreach(var part in splitView.Children())part.pickingMode=PickingMode.Ignore;
            science=Box(root,"science");science.pickingMode=PickingMode.Ignore;Text(science,"RAT   /   MCKITTRICK","science-id eyebrow");
            cursor=Box(science,"tool-cursor");featureA=Box(science,"feature");Text(featureA,"01","feature-number");featureB=Box(science,"feature");Text(featureB,"02","feature-number");
            var instruction=Box(science,"science-instruction");scienceTitle=Text(instruction,"","objective");scienceBody=Text(instruction,"","science-body");scienceGoal=Text(instruction,"","goal-line");
            var tool=Box(science,"tool-readout");pressureFill=Box(Box(tool,"pressure-track"),"pressure-fill");safeBand=Box(pressureFill.parent,"safe-band");thermal=Box(tool,"thermal-cue");Text(thermal,"","small").name="thermal-label";thermalFill=Box(Box(thermal,"progress-track"),"thermal-fill");
            scienceReadout=Text(tool,"","small");scienceFindings=Text(tool,"","small");scienceFindings.style.whiteSpace=WhiteSpace.Normal;scienceFindings.style.marginTop=8;grindFill=Box(Box(tool,"progress-track"),"progress-fill");
            photo=Box(root,"photo-view");photo.pickingMode=PickingMode.Ignore;Box(photo,"viewfinder");Text(photo,"PANCAM   /   MER-B","photo-id eyebrow");Text(photo,"","photo-help").name="photo-help";photoCount=Text(photo,"","photo-count small");
            memory=Box(root,"memory-screen");var memoryCard=Box(memory,"memory-card");Text(memoryCard,"","eyebrow").name="memory-eyebrow";
            memoryTitle=Text(memoryCard,"","display memory-title");chosenImage=new Image{scaleMode=ScaleMode.ScaleToFit};chosenImage.AddToClassList("chosen-image");memoryCard.Add(chosenImage);
            memoryChoices=Box(memoryCard,"memory-choices row");var actions=Box(memoryCard,"row");Button(actions,"photo.send",()=>chapter.journey.ConfirmTransmission(),"primary");Button(actions,"photo.continue",()=>chapter.journey.CancelTransmission());
            field=Box(root,"field-study");field.pickingMode=PickingMode.Ignore;fieldTitle=Text(field,"","field-title");fieldBody=Text(field,"","field-body");fieldGoal=Text(field,"","field-goal");fieldReticle=Box(field,"field-reticle");fieldCenter=Box(field,"field-center");framingFill=Box(Box(field,"framing-track"),"framing-fill");fieldStatus=Text(field,"","field-status");fieldFill=Box(Box(field,"field-track"),"field-progress");fieldPictures=Box(field,"field-comparison");fieldA=new Image{scaleMode=ScaleMode.ScaleToFit};fieldB=new Image{scaleMode=ScaleMode.ScaleToFit};fieldPictures.Add(fieldA);fieldPictures.Add(fieldB);comparisonA=Text(fieldA,"","comparison-label");comparisonB=Text(fieldB,"","comparison-label");
            archiveImage=new Image{scaleMode=ScaleMode.ScaleToFit};archiveImage.AddToClassList("field-archive");field.Add(archiveImage);archiveCredit=Text(field,"","field-credit");
            tutorialPanel=Box(flight,"tutorial-card");tutorialPanel.name="tutorial-card";
            tutorialTitle=Text(tutorialPanel,"","tutorial-title");tutorialBody=Text(tutorialPanel,"","tutorial-body");
            assistance=Box(root,"assistance-card");assistance.name="assistance-card";assistance.pickingMode=PickingMode.Ignore;assistanceText=Text(assistance,"","assistance-text");
            var pauseButton=Button(flight,"v13.pause.short",()=>chapter.Pause(true),"flight-pause");pauseButton.pickingMode=PickingMode.Position;
            pause=Box(root,"modal-backdrop");pause.name="pause-modal";var pc=Box(pause,"modal-card");Text(pc,"OPPORTUNITY","eyebrow");Text(pc,"","display modal-title").name="pause-title";Button(pc,"pause.resume",()=>chapter.Pause(false),"primary");Button(pc,"menu.settings",()=>Open(settings));Button(pc,"v14.controls.button",()=>Open(controls));Button(pc,"pause.restart",chapter.RestartChapter);Button(pc,"v13.tutorial.repeat",()=>{chapter.RestartChapter();chapter.tutorial.RestartTutorial();});Button(pc,"pause.menu",chapter.Leave);
            settings=Box(root,"modal-backdrop");var sc=Box(settings,"settings-card");Text(sc,"","display modal-title").name="settings-title";
            var scroll=new ScrollView();sc.Add(scroll);var language=new DropdownField("Idioma / Language",new System.Collections.Generic.List<string>{"Português (Brasil)","English"},GameSettings.English?1:0);
            language.RegisterValueChangedCallback(e=>{GameSettings.English=e.newValue=="English";RefreshText();});scroll.Add(language);
            QualitySelector.AddQualitySelector(scroll);
            Slider(scroll,"settings.master",GameSettings.Master,0,1,v=>GameSettings.Master=v);Slider(scroll,"settings.music",GameSettings.Music,0,1,v=>GameSettings.Music=v);Slider(scroll,"settings.effects",GameSettings.Effects,0,1,v=>GameSettings.Effects=v);Slider(scroll,"settings.ui",GameSettings.Interface,0,1,v=>GameSettings.Interface=v);Slider(scroll,"settings.sensitivity",GameSettings.Sensitivity,.25f,2.5f,v=>GameSettings.Sensitivity=v);
            Toggle(scroll,"settings.invert",GameSettings.Invert,v=>GameSettings.Invert=v);Toggle(scroll,"settings.easy",GameSettings.Easy,v=>GameSettings.Easy=v);Toggle(scroll,"settings.motion",GameSettings.ReducedMotion,v=>GameSettings.ReducedMotion=v);Toggle(scroll,"settings.subtitles",GameSettings.Subtitles,v=>GameSettings.Subtitles=v);Toggle(scroll,"v14.settings.voice",GameSettings.Voice,v=>GameSettings.Voice=v);Toggle(scroll,"settings.large",GameSettings.LargeText,v=>{GameSettings.LargeText=v;RefreshText();});
            Button(sc,"back",()=>{PlayerPrefs.Save();Show(settings,false);});
            controls=Box(root,"modal-backdrop");var kc=Box(controls,"controls-card");Text(kc,"","display modal-title").name="controls-title";var ks=new ScrollView();kc.Add(ks);
            foreach(var cid in new[]{"drive","fast","mouse","e","f","space","r","h","tab","p","rat"}){var crow=Box(ks,"controls-row");var ckey=Text(crow,"","controls-key");ckey.userData="v14.controls."+cid;var cdesc=Text(crow,"","controls-desc");cdesc.userData="v14.controls."+cid+".d";}
            Button(kc,"back",()=>Show(controls,false));
            credits=Box(root,"modal-backdrop");var cc=Box(credits,"credits-card");Text(cc,"","display modal-title").name="credits-title";var cs=new ScrollView();cc.Add(cs);Text(cs,"","credits-body").name="credits-body";Button(cc,"back",()=>Show(credits,false));
            journal=Box(root,"modal-backdrop");var jc=Box(journal,"journal-card");Text(jc,"","display modal-title").name="journal-title";var js=new ScrollView();jc.Add(js);Text(js,"","journal-body").name="journal-body";
            AddPhoto(js,ratPhoto,"McKittrick · NASA/JPL");AddPhoto(js,routeMap,"45,16 km · NASA/JPL-Caltech/MSSS");AddPhoto(js,panorama,"Perseverance Valley · NASA/JPL-Caltech/Cornell/ASU");AddPhoto(js,chapter.investigation.berryArchive,"Berry Bowl · NASA/JPL-Caltech/Cornell");AddPhoto(js,chapter.investigation.shieldArchive,"Escudo térmico / Heat shield · NASA/JPL/Cornell");AddPhoto(js,chapter.investigation.solarArchive,"Pancam · NASA/JPL-Caltech/Cornell/ASU");foreach(FieldStudy kind in new[]{FieldStudy.Berries,FieldStudy.Shield,FieldStudy.Meteorite,FieldStudy.SunClear,FieldStudy.SunDust}){var card=Box(js,"study-album-card");var image=new Image{scaleMode=ScaleMode.ScaleToFit};image.AddToClassList("journal-image");card.Add(image);Text(card,Localization.Get("v10.study."+kind.ToString().ToLowerInvariant()),"small");studyAlbum.Add(kind,image);}
            Button(jc,"back",ToggleJournal);
            end=Box(root,"end-screen");var endWords=Box(end,"cinematic-words memorial-title");var endTitle=Text(endWords,"","cinematic-heading end-name");endTitle.name="end-title";endTitle.style.fontSize=56;endTitle.style.letterSpacing=6;endActions=Box(end,"end-actions row");Button(endActions,"pause.restart",chapter.RestartChapter);Button(endActions,"menu.credits",()=>Open(credits));Button(endActions,"pause.menu",chapter.Leave);
            exploreHint=Text(root,"","explore-hint");exploreHint.pickingMode=PickingMode.Ignore;notification=Text(root,"","toast");notification.pickingMode=PickingMode.Ignore;photoFlash=Box(root,"photo-flash");photoFlash.pickingMode=PickingMode.Ignore;fade=Box(root,"fade");fade.pickingMode=PickingMode.Ignore;
            foreach(var e in new[]{pause,settings,controls,credits,journal,memory,end,tutorialPanel,assistance,rockHint,splitView})Show(e,false);RefreshText();
        }
        void Slider(VisualElement p,string key,float value,float min,float max,System.Action<float> setter)
        {var s=new Slider(Localization.Get(key),min,max){value=value,userData=key};s.RegisterValueChangedCallback(e=>setter(e.newValue));p.Add(s);}
        void Toggle(VisualElement p,string key,bool value,System.Action<bool> setter)
        {var t=new Toggle(Localization.Get(key)){value=value,userData=key};t.RegisterValueChangedCallback(e=>setter(e.newValue));p.Add(t);}
        void AddPhoto(VisualElement p,Texture2D texture,string credit)
        {var i=new Image{image=texture,scaleMode=ScaleMode.ScaleToFit};i.AddToClassList("journal-image");p.Add(i);Text(p,credit,"small muted");}
        static void Show(VisualElement e,bool b){if(e!=null)e.style.display=b?DisplayStyle.Flex:DisplayStyle.None;}
        void Open(VisualElement e){QualitySelector.RefreshQualitySelectors(e);Show(e,true);e.BringToFront();}
        void Set(string name,string key){var l=root.Q<Label>(name);if(l!=null)l.text=Localization.Get(key);}
        public void RefreshText()
        {
            if(root==null)return;activeLanguage=GameSettings.English.ToString();root.EnableInClassList("large-text",GameSettings.LargeText);
            QualitySelector.RefreshQualitySelectors(root);
            root.Query<Button>().ForEach(b=>{if(b.userData is string k)b.text=Localization.Get(k);});root.Query<Label>(className:"controls-key").ForEach(b=>{if(b.userData is string k)b.text=Localization.Get(k);});root.Query<Label>(className:"controls-desc").ForEach(b=>{if(b.userData is string k)b.text=Localization.Get(k);});root.Query<Slider>().ForEach(b=>{if(b.userData is string k)b.label=Localization.Get(k);});root.Query<Toggle>().ForEach(b=>{if(b.userData is string k)b.label=Localization.Get(k);});
            foreach(var pair in new[]{new[]{"pause-title","pause.title"},new[]{"end-title","v3.opportunity"},new[]{"settings-title","menu.settings"},new[]{"controls-title","v14.controls.title"},new[]{"credits-title","menu.credits"},new[]{"credits-body","credits.body"},new[]{"journal-title","journal.title"},new[]{"journal-body","journal.body"},new[]{"end-label","end.label"},new[]{"end-subtitle","end.subtitle"},new[]{"photo-help","photo.controls"},new[]{"memory-eyebrow","photo.memory"}})Set(pair[0],pair[1]);
        }
        public void ShowPause(bool visible)
        {Show(pause,visible);if(!visible){journalOpen=false;foreach(var e in new[]{journal,settings,controls,credits})Show(e,false);}}
        public void ToggleJournal(){bool value=!journalOpen;chapter.Pause(value);journalOpen=value;foreach(var entry in studyAlbum){entry.Value.image=chapter.investigation.FrameFor(entry.Key);entry.Value.parent.Q<Label>().text=Localization.Get("v10.study."+entry.Key.ToString().ToLowerInvariant());Show(entry.Value.parent,entry.Value.image);}
            Show(journal,value);if(value)Show(pause,false);}
        public void HideForCapture(bool hidden)=>root.style.visibility=hidden?Visibility.Hidden:Visibility.Visible;
        public void ObjectiveChanged(){objectiveAge=0;}
        public void Toast(string key,float seconds=4.5f){toastKey=key;toastTime=seconds;}
        void Update()
        {
            if(chapter==null||root==null||chapter.journey==null)return;
            if(activeLanguage!=GameSettings.English.ToString())RefreshText();
            if(GameInput.Press(Key.P)&&chapter.Stage!=ChapterStage.ChapterEnd)chapter.Pause(!chapter.Paused);
            var s=chapter.Stage;var j=chapter.journey;var p=j.photography;var fieldStudy=chapter.investigation;bool drive=s==ChapterStage.Exploration||s==ChapterStage.HeatShieldExploration||j.FinalAct;
            endActions.style.opacity=Mathf.Clamp01((chapter.Elapsed-2)/1.5f);endActions.pickingMode=s==ChapterStage.ChapterEnd&&chapter.Elapsed>2?PickingMode.Position:PickingMode.Ignore;foreach(var button in endActions.Children())button.SetEnabled(chapter.Elapsed>2);
            Show(end,s==ChapterStage.ChapterEnd);Show(flight,drive&&!fieldStudy.Active&&!p.Active&&!j.ChoosingMemory);Show(field,fieldStudy.Active);if(fieldStudy.Active)UpdateField();Show(science,s==ChapterStage.RockAnalysis);Show(cinema,chapter.Cinematic);Show(photo,p.Active);Show(memory,j.ChoosingMemory);
            UpdateSplit();ApplyPlateScale();
            // Antes da primeira observação: perto da rocha das esferas, um aviso no meio da tela.
            var act=chapter.interactions?chapter.interactions.Current:default;
            bool explore=act.Valid&&act.Available&&act.Id=="berries"&&!fieldStudy.Active&&!chapter.Paused;
            if(explore)exploreHint.text=Localization.Get("v17.hint.explore");Show(exploreHint,explore);
            if(drive)UpdateFlight();if(s==ChapterStage.RockAnalysis)UpdateScience();if(chapter.Cinematic)UpdateCinema();
            var tutor=chapter.tutorial;bool offer=tutor&&tutor.HelpOffered&&!chapter.Paused&&!chapter.Cinematic&&s!=ChapterStage.ChapterEnd;
            Show(assistance,offer);assistance.EnableInClassList("strong",tutor&&tutor.StrongHelp);if(offer)assistanceText.text=Localization.Get(tutor.HelpBodyKey);
            photoCount.text=j.FinalAct?Localization.Get(j.PanoramaCount>0?"v13.photo.recorded":"v13.photo.required"):$"{p.Frames.Count}   ·   PANCAM";
            if(j.ChoosingMemory)UpdateMemory();else wasChoosing=false;
            toastTime=Mathf.Max(0,toastTime-Time.unscaledDeltaTime);notification.text=Localization.Get(toastKey);notification.style.opacity=p.Active||chapter.investigation.Active||chapter.Cinematic?0:Mathf.Clamp01(toastTime);
            photoFlash.style.opacity=drive&&!GameSettings.ReducedMotion?p.Flash*.18f:0;
            float t=chapter.Elapsed,alpha=0;
            if(s==ChapterStage.Intro)alpha=Mathf.Clamp01(1-t/1.2f)+Mathf.InverseLerp(chapter.film.SequenceDuration-.8f,chapter.film.SequenceDuration,t);
            if(s==ChapterStage.FinalExploration)alpha=Mathf.Clamp01(1-t/.7f);
            if(s==ChapterStage.Transition)alpha=t<6.8f?Mathf.InverseLerp(6.15f,6.8f,t):1-Mathf.InverseLerp(6.8f,7.5f,t);
            fade.style.opacity=Mathf.Clamp01(alpha);
        }
        void UpdateSplit()
        {
            var split=chapter.split;bool visible=split&&split.LabelsVisible&&s_IsLanding();
            Show(splitView,visible);Show(letterTop,!(split&&split.Active&&split.Expand<1));Show(letterBottom,!(split&&split.Active&&split.Expand<1));
            if(!visible)return;
            // Lado a lado: Spirit à esquerda, Opportunity à direita, linha vertical entre as duas.
            float w=split.OpportunityWidth,left=(1-w)*100,fade=Mathf.Clamp01(chapter.Elapsed/.9f)*(1-Mathf.InverseLerp(.15f,.55f,split.Expand));
            splitView.style.opacity=fade;
            var d=splitDivider.style;d.left=Length.Percent(left);d.right=StyleKeyword.Auto;d.top=0;d.bottom=0;d.width=4;d.height=StyleKeyword.Auto;d.marginTop=0;d.marginLeft=-2;
            splitTopLabel.style.left=Length.Percent(2.5f);splitTopLabel.style.top=Length.Percent(5f);
            splitBottomLabel.style.left=Length.Percent(left+2.5f);splitBottomLabel.style.top=Length.Percent(5f);
            var n=splitNote.style;n.left=Length.Percent(2.5f);n.right=StyleKeyword.Auto;n.top=Length.Percent(15f);n.bottom=StyleKeyword.Auto;n.maxWidth=Length.Percent(Mathf.Max(10f,left-5f));n.unityTextAlign=TextAnchor.UpperLeft;
            splitTopName.text=Localization.Get("v15.split.spirit");splitTopPlace.text=Localization.Get("v15.split.spirit.place");
            splitBottomName.text=Localization.Get("v15.split.opportunity");splitBottomPlace.text=Localization.Get("v15.split.opportunity.place");
            splitNote.text="";Show(splitNote,false);
        }
        // A cena usa outra escala de interface (1920×1080, meio a meio entre largura e altura); as placas de legenda são medidas na escala
        // da abertura (1280×720 pela altura) para ficarem do mesmo tamanho em qualquer resolução.
        void ApplyPlateScale()
        {
            float w=Mathf.Max(1,Screen.width),h=Mathf.Max(1,Screen.height);
            float hud=Mathf.Pow(2,Mathf.Lerp(Mathf.Log(w/1920f,2),Mathf.Log(h/1080f,2),.5f)),plate=h/720f,k=plate/Mathf.Max(.01f,hud);
            foreach(var e in new VisualElement[]{caption,notification,exploreHint})
            {
                var st=e.style;st.fontSize=28*k;st.paddingLeft=st.paddingRight=24*k;st.paddingTop=st.paddingBottom=8*k;st.maxWidth=880*k;
                st.borderTopLeftRadius=st.borderTopRightRadius=st.borderBottomLeftRadius=st.borderBottomRightRadius=8*k;
            }
            caption.style.bottom=24*k;
        }
        bool s_IsLanding()=>chapter.Stage==ChapterStage.Landing;
        string GoalFor(FieldStudy study)=>study==FieldStudy.Berries||study==FieldStudy.Meteorite?"v15.goal.1":study==FieldStudy.None?null:null;
        void UpdateFlight()
        {
            var j=chapter.journey;bool storm=chapter.Stage==ChapterStage.DustStorm;
            if(objectiveKey!=j.ObjectiveKey){objectiveKey=j.ObjectiveKey;ObjectiveChanged();}
            objectiveAge+=Time.deltaTime;objective.text=Localization.Get(objectiveKey);
            location.text=chapter.Stage==ChapterStage.HeatShieldExploration?"MERIDIANI PLANUM   ·   JAN 2005":chapter.Stage==ChapterStage.Exploration?"EAGLE CRATER   ·   2004":"PERSEVERANCE VALLEY   ·   JUN 2018";
            distance.text=$"{chapter.TargetDistance:0} m";
            objective.parent.style.opacity=objectiveAge>7?.72f:1;
            string key=j.FinalAct&&j.PanoramaCount>0?"controls.final":"controls.drive";
            if(chapter.Stage==ChapterStage.Exploration&&j.ReadyForScience&&chapter.TargetDistance<3.4f)key="controls.sample";
            else if(j.FinalAct&&j.NearUplink&&j.PanoramaCount>0)key="controls.send";
            else if(j.FinalAct&&j.NearbyStation>=0)key="controls.photo";
            if(j.Transmitting)key="photo.sending";
            if(chapter.investigation.OverridesTarget)key=chapter.investigation.Near?"v10.controls.observe":"v10.controls.approach";
            help.text=Localization.Get(key)+(j.Transmitting?$"  {j.Transmission*100:0}%":"");Show(energyBox,storm);
            energy.text=Localization.Get("telemetry.solar");energyFill.style.width=Length.Percent(chapter.Energy*100);energyFill.style.backgroundColor=new Color(.94f,.64f,.37f);
            var action=chapter.interactions?chapter.interactions.Current:default;
            bool valid=action.Valid&&!j.Transmitting&&!chapter.Paused;
            bool available=valid&&action.Available;
            Vector3 point=valid?action.Anchor.position+Vector3.up*.85f:chapter.ActiveTarget.position+Vector3.up*1.1f;
            var viewport=chapter.cameras.view.WorldToViewportPoint(point);
            Show(rockHint,available);
            float hintX=viewport.z>0?viewport.x:(viewport.x>.5f?.92f:.08f);
            float hintY=viewport.z>0?1-viewport.y:.5f;
            rockHint.style.left=Length.Percent(Mathf.Clamp(hintX*100,12,82));rockHint.style.top=Length.Percent(Mathf.Clamp(hintY*100,24,72));
            rockHint.Q<Label>("rock-interaction-label").text=valid?Localization.Get(action.LabelKey):"";
            bool holdAction=valid&&(action.Id=="berries"||action.Id=="shield"||action.Id=="meteorite"||action.Id=="sun-clear"||action.Id=="sun-dust");
            rockHint.Q<Label>("rock-interaction-sub").text=valid?Localization.Get(holdAction?"v15.hint.hold":"v15.hint.press"):"";
            rockHint.userData=valid?action.Id:null;
            bool markerVisible=!available&&!j.Transmitting&&!chapter.Paused;
            Show(marker,markerVisible);
            // On screen the marker shows a drawn "!"; at the screen edge it shows an arrow toward the destination.
            marker.EnableInClassList("marker-edge",viewport.z<0||viewport.x<.08f||viewport.x>.92f);
            marker.Q<Label>().text=viewport.z<0||viewport.x<.08f?"‹":"›";
            marker.style.left=Length.Percent(Mathf.Clamp(hintX*100,8,92));marker.style.top=Length.Percent(Mathf.Clamp(hintY*100,20,77));marker.style.opacity=.95f;
            var tutorial=chapter.tutorial;
            Show(tutorialPanel,tutorial&&tutorial.Visible);
            if(tutorial&&tutorial.Visible){tutorialTitle.text=Localization.Get(tutorial.TitleKey);tutorialBody.text=Localization.Get(tutorial.BodyKey);}
            objective.parent.EnableInClassList("tutorial-focus",tutorial&&tutorial.Visible&&tutorial.Step==2);

        }
        void Project(VisualElement e,Vector3 point)
        {var v=chapter.cameras.view.WorldToViewportPoint(point);e.style.left=Length.Percent(v.x*100);e.style.top=Length.Percent((1-v.y)*100);}
        void UpdateField()
        {
            var s=chapter.investigation;fieldTitle.text=Localization.Get(s.TitleKey);fieldBody.text=Localization.Get(s.BodyKey);
            // Hold-only observation: no reticle or framing meter, just the progress of the hold.
            Show(fieldReticle,false);Show(fieldCenter,false);Show(framingFill.parent,false);Show(fieldFill.parent,!s.Result);fieldFill.style.width=Length.Percent(s.Dwell*100);
            string goalKey=GoalFor(s.Study);fieldGoal.text=goalKey!=null?Localization.Get(goalKey):"";Show(fieldGoal,goalKey!=null&&!s.Capturing);
            fieldBody.style.width=Length.Percent(s.Result?88:48);
            fieldStatus.text=Localization.Get(s.Capturing?"v11.capture":s.Result?"v10.controls.return":"v11.guide.hold");
            bool compare=s.Result&&s.Study==FieldStudy.SunDust&&s.ClearSunFrame&&s.DustSunFrame;
            fieldA.image=compare?s.ClearSunFrame:s.ResultImage;fieldB.image=compare?s.DustSunFrame:s.Solar?null:s.Archive;
            Show(fieldPictures,s.Result&&s.ResultImage);Show(fieldB,fieldB.image);fieldA.style.width=Length.Percent(fieldB.image?50:100);
            comparisonA.text=Localization.Get(compare?"v10.sun.reference":s.Solar?"v11.sun.saved":"v11.image.game");comparisonB.text=Localization.Get(compare?"v10.sun.dust":"v11.image.archive");
            Show(archiveImage,false);Show(archiveCredit,s.Result&&!s.Solar&&s.Archive);archiveCredit.text=Localization.Get(s.Study==FieldStudy.Shield?"v10.archive.shield":"v10.archive.credit");
        }
        void UpdateScience()
        {
            var m=chapter.science;Show(scienceReadout.parent,m.Active&&(m.Phase==AbrasionPhase.Abrade||m.Phase==AbrasionPhase.Analyze||m.Phase==AbrasionPhase.Complete));string phase=m.Active&&!(m.Phase==AbrasionPhase.Identify&&!m.ToolDeployed)?m.Phase.ToString().ToLowerInvariant():"deploy";
            scienceTitle.text=Localization.Get(m.Phase==AbrasionPhase.Analyze?(m.Observations==0?"v11.rat.micro.title":"v11.rat.composition.title"):"rat."+phase+".title");scienceBody.text=Localization.Get(m.Phase==AbrasionPhase.Analyze?(m.Observations==0?"v11.rat.micro.body":"v11.rat.composition.body"):"rat."+phase+".body");
            scienceGoal.text=Localization.Get("v15.goal.1");
            Show(cursor,false);
            bool analyze=m.Phase==AbrasionPhase.Analyze;Show(featureA,false);Show(featureB,false);
            if(analyze){Project(featureA,m.surface.WorldPoint(m.surface.Features[0]));Project(featureB,m.surface.WorldPoint(m.surface.Features[1]));featureA.style.opacity=m.Observed[0]?.25f:1;featureB.style.opacity=m.Observed[1]?.25f:1;}
            Show(pressureFill.parent,false);pressureFill.style.width=Length.Percent(m.Pressure*100);pressureFill.style.backgroundColor=m.Pressure>=m.PressureLow&&m.Pressure<=m.PressureHigh?new Color(.55f,.82f,.72f):new Color(.97f,.59f,.3f);
            grindFill.style.width=Length.Percent((analyze?m.AnalysisProgress:m.Progress)*100);
            safeBand.style.left=Length.Percent(m.PressureLow*100);safeBand.style.width=Length.Percent((m.PressureHigh-m.PressureLow)*100);
            Show(thermal,false);thermalFill.style.width=Length.Percent(m.Temperature*100);
            thermal.Q<Label>("thermal-label").text=Localization.Get("rat.temperature");
            string pressureKey=m.Cooling?"rat.cooldown":m.Pressure<m.PressureLow?"rat.low":m.Pressure>m.PressureHigh?"rat.high":"rat.pressure";
            scienceReadout.text=analyze?Localization.Get(!m.ToolStowed?"v11.rat.retract":m.AnalysisScanning?"v11.rat.scanning":"v11.rat.request"):string.Format(Localization.Get(m.Working?"rat.hold.active":"rat.hold"),Mathf.CeilToInt(m.RemainingSeconds));
            // Leituras da cavidade, reveladas conforme cada instrumento termina (MI: textura; APXS: elementos; Mössbauer: minerais de ferro).
            bool done=m.Phase==AbrasionPhase.Complete;int read=done?2:m.Phase==AbrasionPhase.Analyze?m.Observations:-1;
            var findings=new System.Text.StringBuilder();
            if(read>=0)findings.Append(Localization.Get("rat.find.title"));
            if(read>=1)findings.Append('\n').Append(Localization.Get("rat.find.mi"));
            if(read>=2||read==1&&m.ScanProgress>=.45f)findings.Append('\n').Append(Localization.Get("rat.find.apxs"));
            if(read>=2||read==1&&m.ScanProgress>=.9f)findings.Append('\n').Append(Localization.Get("rat.find.mossbauer"));
            if(read>=2)findings.Append('\n').Append(Localization.Get("rat.find.spheres"));
            scienceFindings.text=findings.ToString();Show(scienceFindings,read>=0);
            if(done){scienceReadout.text=Localization.Get("rat.find.done");grindFill.style.width=Length.Percent(100);}
            // Barra da abrasão no canto inferior direito, longe da rocha.
            var readout=scienceReadout.parent;readout.style.position=Position.Absolute;readout.style.right=32;readout.style.bottom=96;readout.style.left=StyleKeyword.Auto;readout.style.top=StyleKeyword.Auto;readout.style.width=300;
        }
        void UpdateMemory()
        {
            var j=chapter.journey;var p=j.photography;
            if(!wasChoosing||galleryVersion!=p.CaptureVersion)
            {
                wasChoosing=true;galleryVersion=p.CaptureVersion;selected=-1;memoryChoices.Clear();
                for(int n=0;n<p.Frames.Count;n++){int index=n;var b=new Button(()=>j.ChooseMemory(index));b.AddToClassList("memory-thumb");var i=new Image{image=p.Frames[n],scaleMode=ScaleMode.ScaleToFit};b.Add(i);memoryChoices.Add(b);}
            }
            if(selected!=j.SelectedMemory){selected=j.SelectedMemory;chosenImage.image=p.SelectedFrame;for(int n=0;n<memoryChoices.childCount;n++)memoryChoices[n].EnableInClassList("selected",n==selected);}
            memoryTitle.text=Localization.Get("photo.choose");
        }
        void UpdateCinema()
        {
            var film=chapter.film;float local=film.BeatTime;float orbitStart=0,orbitEnd=0;bool orbital=chapter.Stage==ChapterStage.Intro&&film.CaptionWindow(out orbitStart,out orbitEnd,"v14.intro.hematite1","v14.intro.hematite2")&&chapter.Elapsed>=orbitStart&&chapter.Elapsed<orbitEnd;Show(orbitEvidence,orbital);Show(orbitCredit,orbital);orbitEvidence.image=chapter.investigation.orbitalMap;orbitEvidence.style.opacity=Mathf.Clamp01((chapter.Elapsed-orbitStart)/1.4f)*Mathf.Clamp01((orbitEnd-chapter.Elapsed)/1.4f)*.85f;orbitCredit.text="MERIDIANI PLANUM  ·  NASA/JPL-Caltech/ASU";
            heading.text=Localization.Get(film.TitleKey);story.text="";caption.text=GameSettings.Subtitles?Localization.Get(film.BodyKey):"";Show(caption,!string.IsNullOrWhiteSpace(caption.text));
            if(lastFilmTitle!=film.TitleKey){lastFilmTitle=film.TitleKey;var titleFont=filmSans;if(titleFont)heading.style.unityFontDefinition=FontDefinition.FromFont(titleFont);}
            bool memorialTitle=chapter.Stage==ChapterStage.Farewell&&film.TitleKey=="v3.opportunity";
            heading.style.fontSize=memorialTitle?64:film.TitleKey!=null&&film.TitleKey.StartsWith("v3.year")?56:30;
            heading.style.letterSpacing=memorialTitle?9:2;
            heading.parent.EnableInClassList("memorial-title",memorialTitle);
            story.text="";
            filmVeil.style.opacity=film.PictureBlack;
            skip.text=chapter.CanSkip&&!memorialTitle?Localization.Get("controls.skip"):"";
            bool memory=chapter.Stage==ChapterStage.Farewell&&chapter.FilmTime>=16&&chapter.FilmTime<23&&chapter.journey.photography.SelectedFrame;
            remembrance.image=chapter.journey.photography.SelectedFrame;Show(remembrance,memory);Show(heading.parent,!memory&&!string.IsNullOrEmpty(film.TitleKey));
            remembrance.style.opacity=memory?Mathf.Clamp01((chapter.FilmTime-16)/.65f)*Mathf.Clamp01((23-chapter.FilmTime)/.65f):0;
            heading.parent.style.opacity=Mathf.Clamp01(local/.65f)*(memorialTitle?1:Mathf.Clamp01((film.BeatLength-local)/.5f));
            caption.style.opacity=film.CaptionOpacity;caption.BringToFront();skip.BringToFront();
        }
        static void Quit(){PlayerPrefs.Save();Application.Quit();}
    }
}


