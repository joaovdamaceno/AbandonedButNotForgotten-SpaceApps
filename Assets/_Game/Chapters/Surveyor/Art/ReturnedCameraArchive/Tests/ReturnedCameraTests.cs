using System.Collections;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace Abandoned.SpaceApps.Chapters.Surveyor.ChapterTests
{
    public sealed class ReturnedCameraTests
    {
        static string previousLanguage;
        static void English(SettingsData data)=>data.languageCode="en";
        static void Portuguese(SettingsData data)=>data.languageCode="pt";
        static void Restore(SettingsData data)=>data.languageCode=previousLanguage;
        [UnityTest]public IEnumerator OptionalCardUsesExistingNasaPhotographAndNeverStartsChapter()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene);yield return new EnterPlayMode();
            var archive=Object.FindAnyObjectByType<SurveyorArchiveHost>();archive.Initialize();var view=archive.GetComponent<SurveyorReturnedCameraView>();view.Initialize();for(int i=0;i<3;i++)yield return null;
            previousLanguage=archive.Settings.Current.languageCode;archive.Settings.Apply(Portuguese);
            Assert.That(view.Data.Validate(),Is.Empty);Assert.That(UnityEditor.AssetDatabase.GetAssetPath(view.Data.Photograph),Is.EqualTo(SurveyorVisitBuilder.PhotoPath));Assert.That(archive.Clock.ActiveSeconds,Is.Zero);
            var root=archive.Screen.GetComponent<UIDocument>().rootVisualElement;var folder=root.Q(className:"sa-chapter--surveyor");var details=folder.Q<Button>("folder-details");details.Focus();using(var submit=NavigationSubmitEvent.GetPooled())details.SendEvent(submit);yield return null;view.Refresh();Assert.That(view.Foldout,Is.Not.Null);Assert.That(view.Foldout.value,Is.False,"Card opened automatically");view.Foldout.value=true;yield return null;root.Q<ScrollView>("panel-body").ScrollTo(view.Foldout);yield return SurveyorChapterTests.Capture("card-pt","QA/SUR-017");
            archive.Settings.Apply(English);yield return null;view.Refresh();Assert.That(view.Foldout.value,Is.False);Assert.That(view.Foldout.text,Does.Contain("Recovered hardware"));view.Foldout.value=true;yield return null;root.Q<ScrollView>("panel-body").ScrollTo(view.Foldout);yield return SurveyorChapterTests.Capture("card-en","QA/SUR-017");archive.Settings.Apply(Restore);
            Assert.That(archive.Owner.State,Is.EqualTo(AppState.Dossier));Assert.That(archive.Clock.ActiveSeconds,Is.Zero);Assert.That(archive.Owner.Evidence.Records.Count,Is.Zero);yield return new ExitPlayMode();
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying){var archive=Object.FindAnyObjectByType<SurveyorArchiveHost>();if(archive!=null&&previousLanguage!=null)archive.Settings.Apply(Restore);yield return new ExitPlayMode();}}
    }
}
