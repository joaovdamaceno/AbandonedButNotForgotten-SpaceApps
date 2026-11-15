using System;
using Abandoned.SpaceApps.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorArchiveHost:MonoBehaviour,ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterCatalog catalog;[SerializeField] DossierScreen screen;
        [SerializeField] Camera menuCamera;
        public ChapterDirector Owner{get;private set;}public SessionClock Clock{get;private set;}public InputRouter Input{get;private set;}public InteractionService Interactions{get;private set;}
        public SettingsStore Settings{get;private set;}public DossierScreen Screen=>screen;
        void Start()=>Initialize();
        public void Configure(ChapterCatalog chapters,DossierScreen dossier,Camera camera){catalog=chapters;screen=dossier;menuCamera=camera;}
        public void Initialize()
        {
            if(Owner!=null)return;Owner=GetComponent<ChapterDirector>();Clock=GetComponent<SessionClock>();Input=GetComponent<InputRouter>();Interactions=GetComponent<InteractionService>();Settings=new SettingsStore();Settings.Load();
            Owner.Initialize(Clock,Input,this,new EvidenceStore());Input.Enable();Interactions.Initialize(Input,Clock);Owner.StateChanged+=State;screen.Initialize(Owner,Settings,Input,catalog);Owner.OpenDossier();
        }
        void State(AppState state){if(state==AppState.Dossier)Debug.Log("SURVEYOR_ARCHIVE_READY");bool menu=state==AppState.Dossier||state==AppState.Boot;menuCamera.enabled=menu;menuCamera.GetComponent<AudioListener>().enabled=menu;}
        void ChapterDirector.ISceneLoader.Load(string name,Action done,Action<string> failed)
        {
            // sceneLoaded runs before Start: inject the archive's Core before the standalone fallback can initialize.
            void Loaded(Scene scene,LoadSceneMode mode)
            {
                if(scene.name!=name)return;SceneManager.sceneLoaded-=Loaded;
                try
                {
                    SurveyorDocumentaryHost documentary=null;SurveyorChapterHost chapter=null;
                    foreach(var root in scene.GetRootGameObjects())
                    {
                        documentary=root.GetComponentInChildren<SurveyorDocumentaryHost>();if(documentary!=null)break;
                        chapter=root.GetComponentInChildren<SurveyorChapterHost>();
                    }
                    if(documentary!=null)documentary.Initialize(Owner,Clock,Input,Interactions,Settings);
                    else if(chapter!=null)chapter.Initialize(Owner,Clock,Input,Interactions,Settings);
                    else throw new InvalidOperationException("Surveyor chapter host missing.");
                    done();
                }
                catch(Exception e){SceneManager.UnloadSceneAsync(scene);failed(e.Message);}
            }
            SceneManager.sceneLoaded+=Loaded;
            try
            {
#if UNITY_EDITOR
                var operation=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Chapters/Surveyor/Scenes/"+name+".unity",new LoadSceneParameters(LoadSceneMode.Additive));
#else
                var operation=SceneManager.LoadSceneAsync(name,LoadSceneMode.Additive);
#endif
                if(operation==null){SceneManager.sceneLoaded-=Loaded;failed("Scene not found: "+name);}
            }
            catch(Exception e){SceneManager.sceneLoaded-=Loaded;failed(e.Message);}
        }
        void ChapterDirector.ISceneLoader.Unload(string name,Action done){var operation=SceneManager.UnloadSceneAsync(name);if(operation==null)done();else operation.completed+=_=>done();}
        void OnDisable(){if(Owner!=null)Owner.StateChanged-=State;}
    }
}
