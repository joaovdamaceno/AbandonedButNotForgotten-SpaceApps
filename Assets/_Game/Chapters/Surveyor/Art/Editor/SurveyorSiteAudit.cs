using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EditorTools
{
    public static class SurveyorSiteAudit
    {
        public static readonly string[] TargetNames = { "LandingMarks", "CraterSlope", "Footpad", "Trench", "Bearing", "Impact", "Eclipse", "Visit" };

        public static List<string> Validate(GameObject root)
        {
            var errors = new List<string>();
            if (root == null) { errors.Add("Raiz SUR-002 ausente."); return errors; }
            var bindings = root.GetComponent<SurveyorSiteBindings>();
            if (bindings == null || bindings.CameraDirector == null || bindings.CameraMount == null)
            { errors.Add("Bindings/câmera da Surveyor incompletos."); return errors; }
            foreach (var name in TargetNames)
                if (!bindings.TryGetTarget("SUR."+name,out var anchor) || !anchor.IsChildOf(root.transform)) errors.Add("Âncora local ausente: "+name);
            var camera = bindings.CameraDirector.TargetCamera;
            if (camera == null) { errors.Add("Câmera TV ausente."); return errors; }
            if (root.GetComponentsInChildren<Camera>(true).Length != 1) errors.Add("Deve existir apenas uma câmera.");
            if (root.GetComponentsInChildren<AudioListener>(true).Count(x=>x.enabled) != 1) errors.Add("Deve existir um AudioListener.");
            var lights = root.GetComponentsInChildren<Light>(true);
            if (lights.Length != 1 || lights[0].type != LightType.Directional) errors.Add("Cena deve usar uma luz direcional.");
            if (camera.transform.position.y < SurveyorSiteGeometry.Height(camera.transform.position.x,camera.transform.position.z)+.1f) errors.Add("Câmera sob o solo.");
            if (RenderSettings.fog || root.GetComponentsInChildren<ParticleSystem>(true).Length != 0) errors.Add("Sem atmosfera/vento/poeira na Lua.");
            var pads = root.transform.Find("SurveyorVisibleRig/Footpads");
            if (pads == null || pads.childCount != 3) errors.Add("Três apoios são obrigatórios.");
            else foreach (Transform pad in pads)
            {
                var contact = pad.Find("Contact");
                if (contact == null) { errors.Add("Contato da pata ausente."); continue; }
                float gap = contact.position.y-SurveyorSiteGeometry.Height(contact.position.x,contact.position.z);
                if (Mathf.Abs(gap) > .025f) errors.Add("Pata sem apoio: "+pad.name+", desnível "+gap);
                foreach (var offset in new[] { new Vector3(.2f,-.03f,0),new Vector3(-.2f,-.03f,0),new Vector3(0,-.03f,.2f),new Vector3(0,-.03f,-.2f) })
                {
                    var p = pad.TransformPoint(offset);
                    if (p.y-SurveyorSiteGeometry.Height(p.x,p.z) > .03f) errors.Add("Borda de pata flutuante: "+pad.name);
                }
            }
            var rockRoot = root.transform.Find("LunarSite/EmbeddedRockField");
            if (rockRoot == null) errors.Add("Campo de rochas ausente.");
            else foreach (var filter in rockRoot.GetComponentsInChildren<MeshFilter>())
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (var v in filter.sharedMesh.vertices)
                {
                    var p = filter.transform.TransformPoint(v);
                    float gap = p.y-SurveyorSiteGeometry.Height(p.x,p.z);
                    min = Mathf.Min(min,gap); max = Mathf.Max(max,gap);
                }
                if (min > .005f || max < 0f) errors.Add("Rocha flutuante/enterrada: "+filter.name);
            }
            if (bindings.Scoop == null || bindings.MirrorAzimuth == null || bindings.MirrorElevation == null) errors.Add("Pivôs móveis ausentes.");
            else foreach (var renderer in bindings.Scoop.GetComponentsInChildren<Renderer>())
                if (renderer.bounds.Contains(camera.transform.position)) errors.Add("Pá intercepta câmera.");
            Physics.SyncTransforms();
            var siteColliders = root.GetComponentsInChildren<Collider>();
            foreach (var name in new[] { "LandingMarks", "CraterSlope", "Footpad" })
            {
                if (!bindings.TryGetTarget("SUR."+name,out var anchor)) continue;
                Vector3 delta=anchor.position-camera.transform.position;
                var ray=new Ray(camera.transform.position,delta.normalized);
                foreach (var collider in siteColliders)
                    if (collider.Raycast(ray,out var hit,delta.magnitude-.12f))
                    { errors.Add("Enquadramento obstruído: "+name+" por "+collider.name); break; }
            }
            long triangles=root.GetComponentsInChildren<MeshFilter>().Sum(x=>TriangleCount(x.sharedMesh));
            if (triangles>250000) errors.Add("Geometria acima do orçamento P0: "+triangles);
            foreach (var material in root.GetComponentsInChildren<Renderer>().SelectMany(x=>x.sharedMaterials).Distinct())
                if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader)) errors.Add("Material/shader inválido.");
            return errors;
        }

        public static void WriteReport()
        {
            var root=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x=>x.name==new SurveyorSiteBuilder().RootName);
            var errors=Validate(root);
            if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
            var filters=root.GetComponentsInChildren<MeshFilter>();
            var materials=root.GetComponentsInChildren<Renderer>().SelectMany(x=>x.sharedMaterials).Distinct().ToArray();
            var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(n=>m.GetTexture(n))).Where(x=>x!=null).Distinct().ToArray();
            Directory.CreateDirectory("QA/SUR-002");
            File.WriteAllText("QA/SUR-002/scene-audit.txt",$"SUR-002 PASS\nUnity: {Application.unityVersion}\nScene: {SurveyorSiteBuilder.SceneAssetPath}\nTriangles (all scene geometry): {filters.Sum(x=>TriangleCount(x.sharedMesh))}\nUnique meshes: {filters.Select(x=>x.sharedMesh).Distinct().Count()}\nMaterials: {materials.Length}\nTexture assets: {textures.Length}\nRenderers: {root.GetComponentsInChildren<Renderer>().Length}\nLights: 1 directional\nTV render target: 640x480 (runtime, owned/released by viewport)\nAnchors: {string.Join(", ",TargetNames)}\nTeam model: {SurveyorTeamModel.Available}\nMeasured Web FPS: NOT TESTED\n");
        }

        public static long TriangleCount(Mesh mesh) => mesh == null ? 0 : Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i)/3);

        [MenuItem("Abandoned Space Apps/Surveyor/SUR-002/Enquadramentos/Marcas do pouso")]
        public static void PreviewMarks() => Preview("LandingMarks",25.3f);
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-002/Enquadramentos/Encosta")]
        public static void PreviewSlope() => Preview("CraterSlope",25.3f);
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-002/Enquadramentos/Pata")]
        public static void PreviewFootpad() => Preview("Footpad",25.3f);
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-002/Enquadramentos/Pata em detalhe")]
        public static void PreviewDetail() => Preview("Footpad",6.43f);

        static void Preview(string name,float fov)
        {
            var root=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x=>x.name==new SurveyorSiteBuilder().RootName);
            if(root==null) { Debug.LogWarning("Abra Surveyor.unity para pré-visualizar o enquadramento."); return; }
            var bindings=root.GetComponent<SurveyorSiteBindings>();
            bindings.TryGetTarget("SUR."+name,out var target);
            var camera=bindings.CameraDirector.TargetCamera;
            camera.transform.position=bindings.CameraMount.position;
            SurveyorSiteBuilder.Frame(camera,target,fov);
            SceneView.RepaintAll();
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                EditorSceneManager.OpenScene(SurveyorSiteBuilder.SceneAssetPath);
                var root=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(x=>x.name==new SurveyorSiteBuilder().RootName);
                var bindings=root.GetComponent<SurveyorSiteBindings>();
                var camera=bindings.CameraDirector.TargetCamera;
                Directory.CreateDirectory("QA/SUR-002/Captures");
                foreach(var name in new[]{"LandingMarks","CraterSlope","Footpad","Trench"})
                {
                    bindings.TryGetTarget("SUR."+name,out var target);
                    SurveyorSiteBuilder.Frame(camera,target,25.3f);
                    Capture(camera,"QA/SUR-002/Captures/"+name+"-wide.png");
                }
                bindings.TryGetTarget("SUR.Footpad",out var detail);
                SurveyorSiteBuilder.Frame(camera,detail,6.43f);
                Capture(camera,"QA/SUR-002/Captures/Footpad-detail.png");
                // Vista externa somente para inspeção técnica: não é a câmera jogável da missão.
                camera.transform.position=SurveyorSiteGeometry.Ground(5.5f,6.5f,2.1f);
                var targetPoint=root.transform.Find("SurveyorVisibleRig/Body").position+Vector3.up*1.25f;
                camera.transform.LookAt(targetPoint); camera.fieldOfView=43f;
                Capture(camera,"QA/SUR-002/Captures/Rig-inspection.png");
                camera.transform.position=SurveyorSiteGeometry.Ground(3.3f,3.9f,.18f);
                camera.transform.LookAt(root.transform.Find("SurveyorVisibleRig/Footpads/Footpad_1").position); camera.fieldOfView=40f;
                Capture(camera,"QA/SUR-002/Captures/Ground-support.png");
                WriteReport();
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(960,720,24,RenderTextureFormat.ARGB32);
            var previous=camera.targetTexture; var active=RenderTexture.active;
            var image=new Texture2D(960,720,TextureFormat.RGB24,false);
            try
            {
                target.Create(); camera.targetTexture=target; camera.aspect=4f/3f;
                // Aquecer o primeiro frame do SRP antes de ler sombras/constantes da captura em batch.
                for(var frame=0;frame<3;frame++)
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,960,720),0,0); image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previous; RenderTexture.active=active;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
