using System.Collections;
using System.Linq;
using Abandoned.SpaceApps.Chapters.Surveyor.EditorTools;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Abandoned.SpaceApps.Chapters.Surveyor.SiteTests
{
    public sealed class SurveyorSiteTests
    {
        GameObject root;
        [SetUp]
        public void OpenSite()
        {
            if (!Application.isPlaying) EditorSceneManager.OpenScene(SurveyorSiteBuilder.SceneAssetPath);
            root=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name==new SurveyorSiteBuilder().RootName);
        }

        [Test]
        public void SceneHasGroundedHardwareVisibleTargetsAndNoMissingReferences()
        {
            Assert.That(SpaceAppsSceneBuilder.ValidateHierarchy(root),Is.Empty);
            Assert.That(SurveyorSiteAudit.Validate(root),Is.Empty);
            Assert.That(root.GetComponentsInChildren<Camera>().Length,Is.EqualTo(1));
        }

        [Test]
        public void AllConsumerAnchorsRemainLocalToTheVisibleRig()
        {
            var rig=root.transform.Find("SurveyorVisibleRig");
            var bindings=root.GetComponent<SurveyorSiteBindings>();
            foreach(var id in SurveyorSiteAudit.TargetNames)
            {
                Assert.That(bindings.TryGetTarget("SUR."+id,out var t),Is.True,id);
                Assert.That(t.IsChildOf(rig),Is.True,id);
            }
            Assert.That(bindings.TryGetTarget("SUR.Missing",out _),Is.False);
            Assert.That(bindings.Scoop.GetComponentsInChildren<MeshFilter>().Sum(x=>SurveyorSiteAudit.TriangleCount(x.sharedMesh)),Is.GreaterThan(0));
            Assert.That(SurveyorTeamModel.Available,Is.True,"O modelo da equipe faz parte da entrega SUR-002.");
            Assert.That(rig.GetComponentsInChildren<MeshRenderer>().Count(x=>x.name.StartsWith("Team_")),Is.EqualTo(6));
        }

        [Test]
        public void AuditRejectsAFloatingLegAndABuriedCamera()
        {
            var foot=root.transform.Find("SurveyorVisibleRig/Footpads/Footpad_1");
            foot.position+=Vector3.up*.4f;
            Assert.That(SurveyorSiteAudit.Validate(root).Any(x=>x.Contains("Pata sem apoio")),Is.True);
            var camera=root.GetComponent<SurveyorSiteBindings>().CameraDirector.TargetCamera;
            camera.transform.position=Vector3.down*3f;
            Assert.That(SurveyorSiteAudit.Validate(root).Any(x=>x.Contains("Câmera sob o solo")),Is.True);
            // Não salvar a cena adulterada pelo teste.
        }

        [Test]
        public void AuditRejectsFloatingRockAndObstructedTarget()
        {
            var rock=root.transform.Find("LunarSite/EmbeddedRockField/Rock_000");
            rock.position+=Vector3.up*3f;
            Assert.That(SurveyorSiteAudit.Validate(root).Any(x=>x.Contains("Rocha flutuante")),Is.True);
            var camera=root.GetComponent<SurveyorSiteBindings>().CameraDirector.TargetCamera;
            root.GetComponent<SurveyorSiteBindings>().TryGetTarget("SUR.LandingMarks",out var target);
            var blocker=new GameObject("TestBlocker"); blocker.transform.SetParent(root.transform);
            blocker.transform.position=Vector3.Lerp(camera.transform.position,target.position,.5f);
            blocker.AddComponent<BoxCollider>().size=Vector3.one;
            Assert.That(SurveyorSiteAudit.Validate(root).Any(x=>x.Contains("Enquadramento obstruído")),Is.True);
        }

        [Test]
        public void RealGroundColliderMatchesReconstructionAtFeetAndWorkArea()
        {
            var ground=root.transform.Find("LunarSite/NearGround_40m").GetComponentsInChildren<MeshCollider>();
            foreach(var point in new[]{new Vector2(0,0),new Vector2(.65f,1.62f),new Vector2(1.73f,1),new Vector2(-1.73f,1),new Vector2(0,-2)})
            {
                var ray=new Ray(new Vector3(point.x,20,point.y),Vector3.down);
                bool found=false;
                foreach(var collider in ground)
                    if(collider.Raycast(ray,out var hit,30))
                    {
                        found=true;
                        Assert.That(hit.point.y,Is.EqualTo(SurveyorSiteGeometry.Height(point.x,point.y)).Within(.02f));
                    }
                Assert.That(found,Is.True,"Buraco no terreno "+point);
            }
        }

        [Test]
        public void RebuildPreservesOwnedGuidsFileIdsAndAnotherOwnersRoot()
        {
            string sceneGuid=AssetDatabase.AssetPathToGUID(SurveyorSiteBuilder.SceneAssetPath);
            string prefabGuid=AssetDatabase.AssetPathToGUID(SurveyorSiteBuilder.ArtPath+"/SurveyorVisibleRig.prefab");
            var beforeId=GlobalObjectId.GetGlobalObjectIdSlow(root.transform.Find("SurveyorVisibleRig").gameObject);
            Assert.That(beforeId.targetObjectId,Is.Not.EqualTo(0));
            var other=new GameObject("[Other Owner] Preserve Me"); other.transform.position=new Vector3(100,100,100);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var result=SpaceAppsSceneBuilder.Run(new SurveyorSiteBuilder());
            Assert.That(result.Succeeded,Is.True,string.Join("\n",result.Errors));
            Assert.That(AssetDatabase.AssetPathToGUID(SurveyorSiteBuilder.SceneAssetPath),Is.EqualTo(sceneGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(SurveyorSiteBuilder.ArtPath+"/SurveyorVisibleRig.prefab"),Is.EqualTo(prefabGuid));
            var rebuilt=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name==new SurveyorSiteBuilder().RootName);
            var afterId=GlobalObjectId.GetGlobalObjectIdSlow(rebuilt.transform.Find("SurveyorVisibleRig").gameObject);
            Assert.That(afterId,Is.EqualTo(beforeId));
            var preserved=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="[Other Owner] Preserve Me");
            Assert.That(preserved.transform.position,Is.EqualTo(new Vector3(100,100,100)));
            Object.DestroyImmediate(preserved);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        [UnityTest]
        public IEnumerator TelevisionOwnsOneBufferAndReleasesItWhenDisabled()
        {
            yield return new EnterPlayMode();
            var viewport=Object.FindFirstObjectByType<SurveyorSiteViewport>();
            Assert.That(viewport.Texture,Is.Not.Null);
            Assert.That(viewport.Texture.width,Is.EqualTo(640));
            Assert.That(viewport.Texture.height,Is.EqualTo(480));
            Assert.That(viewport.Texture.IsCreated(),Is.True);
            viewport.DrawToGameView=false;
            Assert.That(viewport.Texture,Is.Not.Null,"Ocultar a apresentação preserva o buffer para SUR-007.");
            var camera=viewport.GetComponent<SurveyorSiteBindings>().CameraDirector.TargetCamera;
            Assert.That(camera.targetTexture,Is.EqualTo(viewport.Texture));
            viewport.enabled=false;
            yield return null;
            Assert.That(viewport.Texture,Is.Null);
            Assert.That(camera.targetTexture,Is.Null);
            viewport.enabled=true;
            yield return null;
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(x=>x.name=="Surveyor TV 640x480"),Is.EqualTo(1));
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator DiscardTestSceneChanges()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.OpenScene(SurveyorSiteBuilder.SceneAssetPath);
        }
    }
}
