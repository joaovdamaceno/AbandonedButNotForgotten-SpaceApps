using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EditorTools
{
    /// <summary>SUR-002: cena/prefabs próprios através do contrato CORE-006. Não altera builder global ou Build Settings.</summary>
    public sealed class SurveyorSiteBuilder : SceneBuilderModule
    {
        public const string SceneAssetPath = "Assets/_Game/Chapters/Surveyor/Scenes/Surveyor.unity";
        public const string ArtPath = "Assets/_Game/Chapters/Surveyor/Art";
        public const string GeneratedPath = ArtPath + "/SiteGenerated";
        public override string Id => "surveyor.site";
        public override string Owner => "R6 · SUR-002";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => "[R6 SUR-002] Lunar Site";
        public override string AssetFolder => ArtPath;
        const float White = .125f, Metal = .375f, Dark = .625f, Solar = .875f;

        public override void Build(SceneBuildContext context)
        {
            var groundTexture = Texture("Regolith", 1024, false, RegolithPixel);
            var normalTexture = Texture("RegolithNormal", 512, true, NormalPixel);
            var atlas = Texture("HardwareAtlas", 1024, false, HardwarePixel);
            var ground = context.Material(GeneratedPath + "/Regolith.mat", "Universal Render Pipeline/Lit");
            SetMaterial(ground, Color.white, 0f, .05f, groundTexture);
            ground.SetTexture("_BumpMap", normalTexture); ground.SetFloat("_BumpScale", .32f); ground.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(ground);
            var painted = context.Material(GeneratedPath + "/PaintedHardware.mat", "Universal Render Pipeline/Lit");
            SetMaterial(painted, Color.white, 0f, .32f, atlas);
            var metal = context.Material(GeneratedPath + "/MetalHardware.mat", "Universal Render Pipeline/Lit");
            SetMaterial(metal, Color.white, .7f, .44f, atlas);
            var rock = context.Material(GeneratedPath + "/LunarRock.mat", "Universal Render Pipeline/Lit");
            SetMaterial(rock, new Color(.72f, .73f, .75f), 0f, .08f, groundTexture);
            rock.SetTexture("_BumpMap", normalTexture); rock.SetFloat("_BumpScale", .45f); rock.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(rock);

            var site = context.Prefab(ArtPath + "/LunarSite.prefab", root => BuildSite(context, root, ground, rock));
            var rig = context.Prefab(ArtPath + "/SurveyorVisibleRig.prefab", root => BuildRig(context, root, painted, metal));
            var siteInstance = context.PrefabInstance(context.Root.transform, site, "LunarSite");
            var rigInstance = context.PrefabInstance(context.Root.transform, rig, "SurveyorVisibleRig");
            siteInstance.transform.localPosition = Vector3.zero;
            rigInstance.transform.localPosition = Vector3.zero;
            siteInstance.transform.localRotation = rigInstance.transform.localRotation = Quaternion.identity;

            var cameraObject = context.Child(context.Root.transform, "Surveyor TV Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.nearClipPlane = .025f; camera.farClipPlane = 360f; camera.aspect = 4f / 3f;
            camera.allowHDR = false; camera.allowMSAA = false;
            var cameraData = context.Component<UniversalAdditionalCameraData>(cameraObject);
            cameraData.renderPostProcessing = false; cameraData.renderShadows = true;
            var director = context.Component<CameraDirector>(context.Root);
            director.Initialize(camera);
            var mount = rigInstance.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount");
            camera.transform.position = mount.position;
            var anchors = rigInstance.transform.Find("Anchors");
            var bindings = context.Component<SurveyorSiteBindings>(context.Root);
            bindings.Configure(director, mount, rigInstance.transform.Find("Body/TV_Mirror_Azimuth"),
                rigInstance.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation"),
                rigInstance.transform.Find("Body/Scoop_Arm_1"), rigInstance.transform.Find("Body/Scoop_Arm_1/Scoop_Arm_2/Scoop"),
                Enumerable.Range(0, anchors.childCount).Select(i => new SurveyorSiteBindings.Target
                    { id = "SUR." + anchors.GetChild(i).name, anchor = anchors.GetChild(i) }).ToArray());
            Frame(camera, anchors.Find("LandingMarks"), 25.3f);
            context.Component<SurveyorSiteViewport>(context.Root).Configure(camera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rigInstance.transform);

            var sunObject = context.Child(context.Root.transform, "Sun · single shadow light");
            sunObject.transform.rotation = Quaternion.Euler(23f, 145f, 0f);
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional; sun.color = new Color(1f, .965f, .91f); sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .93f; sun.shadowBias = .025f; sun.shadowNormalBias = .25f;
            RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.075f, .088f, .11f);
            RenderSettings.reflectionIntensity = .2f;
            EditorUtility.SetDirty(bindings); EditorUtility.SetDirty(director);
        }

        static void SetMaterial(Material material, Color color, float metallic, float smoothness, Texture2D texture)
        {
            material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true; EditorUtility.SetDirty(material);
        }

        static void BuildSite(SceneBuildContext context, GameObject root, Material ground, Material rock)
        {
            var near = Child(root.transform, "NearGround_40m");
            for (var z = 0; z < 4; z++) for (var x = 0; x < 4; x++)
            {
                var mesh = MeshAsset(context, $"Near_{x}_{z}", m => SurveyorSiteGeometry.Terrain(m, -20f + 10f*x, -20f + 10f*z, 10f, 24));
                Surface(Child(near, $"Chunk_{x}_{z}"), mesh, ground, true);
            }
            var far = Child(root.transform, "CraterContext_210m");
            for (var z = 0; z < 7; z++) for (var x = 0; x < 7; x++)
            {
                float px = -180f + 40f*x, pz = -140f + 40f*z;
                if (px == -20f && pz == -20f) continue;
                var mesh = MeshAsset(context, $"Far_{x}_{z}", m => SurveyorSiteGeometry.Terrain(m, px, pz, 40f, 12));
                Surface(Child(far, $"Chunk_{x}_{z}"), mesh, ground, true);
            }
            var variants = new Mesh[12];
            for (var i = 0; i < variants.Length; i++)
            {
                int seed = 723 + i;
                variants[i] = MeshAsset(context, "Rock_"+i, m => SurveyorSiteGeometry.Rock(m, seed));
            }
            var rocks = Child(root.transform, "EmbeddedRockField");
            var random = new System.Random(3021967);
            for (var i = 0; i < 260; i++)
            {
                float x = ((float)random.NextDouble() - .5f) * 39f, z = ((float)random.NextDouble() - .5f) * 39f;
                if (new Vector2(x,z).magnitude < 4.5f) { x += x < 0 ? -5f : 5f; z += 3f; }
                float size = Mathf.Lerp(.055f, 1.5f, Mathf.Pow((float)random.NextDouble(), 3.8f));
                var t = Child(rocks, "Rock_"+i.ToString("000"));
                t.localRotation = Quaternion.FromToRotation(Vector3.up, SurveyorSiteGeometry.Normal(x,z)) * Quaternion.Euler(0f,(float)random.NextDouble()*360f,0f);
                t.localScale = new Vector3(size, size * Mathf.Lerp(.65f, 1.1f, (float)random.NextDouble()), size * .9f);
                t.localPosition = SurveyorSiteGeometry.Ground(x,z);
                var mesh = variants[i % variants.Length];
                float min = float.PositiveInfinity;
                foreach (var vertex in mesh.vertices)
                {
                    var p = t.TransformPoint(vertex);
                    min = Mathf.Min(min, p.y - SurveyorSiteGeometry.Height(p.x,p.z));
                }
                t.localPosition += Vector3.up * (-min - size * .12f);
                Surface(t, mesh, rock, size > .4f);
                t.GetComponent<MeshRenderer>().shadowCastingMode = size > .2f ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            for (var i=0;i<100;i++)
            {
                float x=-165f+(float)random.NextDouble()*240f,z=-115f+(float)random.NextDouble()*230f;
                if(Mathf.Abs(x)<24f && Mathf.Abs(z)<24f) x-=45f;
                float size=.45f+(float)random.NextDouble()*2.1f;
                var t=Child(rocks,"FarRock_"+i.ToString("000"));
                t.localRotation=Quaternion.FromToRotation(Vector3.up,SurveyorSiteGeometry.Normal(x,z))*Quaternion.Euler(0,(float)random.NextDouble()*360f,0);
                t.localScale=new Vector3(size,size*.7f,size*.9f);
                t.localPosition=SurveyorSiteGeometry.Ground(x,z);
                var mesh=variants[(i+3)%variants.Length];
                float min=float.PositiveInfinity;
                foreach(var v in mesh.vertices)
                {
                    var p=t.TransformPoint(v);
                    min=Mathf.Min(min,p.y-SurveyorSiteGeometry.Height(p.x,p.z));
                }
                t.localPosition+=Vector3.up*(-min-size*.12f);
                Surface(t,mesh,rock,false);
                t.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            // Marcas largas e reconhecíveis; sem procurar pixels invisíveis nas tarefas de observação.
            var marks = Child(root.transform, "TouchdownMarks");
            var draft = new SurveyorSiteGeometry.Draft();
            for (var n = 0; n < 3; n++)
            {
                var center = SurveyorSiteGeometry.LandingMarkCenter(n);
                float cx=center.x,cz=center.y;
                for (var a = 0; a < 32; a++)
                {
                    float angle = a * Mathf.PI/16f, next = (a+1)*Mathf.PI/16f;
                    float outer=.265f+.018f*Mathf.Sin(a*2.7f+n), outerNext=.265f+.018f*Mathf.Sin((a+1)*2.7f+n);
                    draft.Quad(SurveyorSiteGeometry.Ground(cx+Mathf.Cos(angle)*.20f,cz+Mathf.Sin(angle)*.20f,.007f),
                        SurveyorSiteGeometry.Ground(cx+Mathf.Cos(next)*.20f,cz+Mathf.Sin(next)*.20f,.007f),
                        SurveyorSiteGeometry.Ground(cx+Mathf.Cos(next)*outerNext,cz+Mathf.Sin(next)*outerNext,.013f),
                        SurveyorSiteGeometry.Ground(cx+Mathf.Cos(angle)*outer,cz+Mathf.Sin(angle)*outer,.013f), Metal);
                }
            }
            var imprint = MeshAsset(context,"ImprintRims",draft.Write);
            Surface(marks, imprint, rock, false);
        }

        static void BuildRig(SceneBuildContext context, GameObject root, Material painted, Material metal)
        {
            if (SurveyorTeamModel.Available) { SurveyorTeamModel.Build(context,root); return; }
            var body = Child(root.transform, "Body");
            body.localPosition = SurveyorSiteGeometry.Ground(0,0);
            body.localRotation = Quaternion.FromToRotation(Vector3.up, SurveyorSiteGeometry.Normal(0,0));
            var frame = new SurveyorSiteGeometry.Draft();
            var skin = new SurveyorSiteGeometry.Draft();
            var pads = Child(root.transform,"Footpads");
            for (var i = 0; i < 3; i++)
            {
                float angle = (30f + 120f*i)*Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var corner = outward * .7f + Vector3.up * .78f;
                var other = new Vector3(Mathf.Cos(angle + 2f*Mathf.PI/3f),0,Mathf.Sin(angle + 2f*Mathf.PI/3f))*.7f + Vector3.up*.78f;
                frame.Tube(corner,other,.025f,White,12);
                frame.Tube(corner,new Vector3(0,1.65f,0),.018f,White);
                var foot = SurveyorSiteGeometry.Ground(outward.x*2.0f,outward.z*2.0f,.02f);
                var localFoot = body.InverseTransformPoint(foot);
                frame.Tube(corner,localFoot,.032f,White,12);
                frame.Tube(corner + Vector3.up*.24f,Vector3.Lerp(corner,localFoot,.72f),.047f,Metal,12);
                frame.Tube(other,localFoot,.014f,Metal);
                frame.Tube(corner,localFoot + Vector3.Cross(outward,Vector3.up)*.18f,.018f,White);
                var pad = Child(pads,"Footpad_"+(i+1));
                pad.localPosition = foot; pad.localRotation = Quaternion.FromToRotation(Vector3.up,SurveyorSiteGeometry.Normal(foot.x,foot.z));
                var d = new SurveyorSiteGeometry.Draft();
                d.Tube(new Vector3(0,-.03f,0),new Vector3(0,.07f,0),.235f,Metal,32,.21f);
                d.Tube(new Vector3(0,.07f,0),new Vector3(0,.11f,0),.06f,Metal,16);
                for (var b = 0; b < 8; b++)
                {
                    float a = b*Mathf.PI/4f;
                    d.Tube(new Vector3(Mathf.Cos(a)*.16f,.07f,Mathf.Sin(a)*.16f),new Vector3(Mathf.Cos(a)*.16f,.082f,Mathf.Sin(a)*.16f),.008f,Dark,6);
                }
                Surface(pad,MeshAsset(context,"FootpadMesh",d.Write),metal,true);
                var contact = Child(pad,"Contact"); contact.localPosition = new Vector3(0,-.02f,0);
                skin.Box(outward*.58f+Vector3.up*.94f,new Vector3(.46f,.35f,.36f),White,Quaternion.Euler(0,-30f-120f*i,0));
                frame.Tube(outward*.4f+Vector3.up*.60f,outward*.4f+Vector3.up*.32f,.075f,Metal,14,.11f);
            }
            // Treliça, linhas de alimentação e prateleira térmica, com bordas e fixadores combinados.
            frame.Tube(new Vector3(0,.72f,0),new Vector3(0,2.8f,0),.033f,Metal,16);
            frame.Tube(new Vector3(0,1.5f,0),new Vector3(0,1.58f,0),.06f,White,16);
            for (var i = 0; i < 3; i++)
                frame.Tube(new Vector3(.05f*i-.05f,.8f,.025f),new Vector3(.05f*i-.05f,2.63f,.025f),.006f,Dark,6);
            skin.Box(new Vector3(0,.82f,0),new Vector3(.66f,.055f,.59f),White);
            skin.Box(new Vector3(-.38f,.55f,.05f),new Vector3(.27f,.28f,.30f),Dark);
            // Painel solar e antena plana em orientações distintas, como no diagrama pós-pouso.
            var solarRotation = Quaternion.Euler(0,0,-25f);
            skin.Box(new Vector3(-.43f,2.91f,0),new Vector3(.98f,.035f,.68f),Solar,solarRotation);
            frame.Tube(new Vector3(0,2.7f,0),new Vector3(-.42f,2.91f,0),.025f,Metal);
            skin.Box(new Vector3(.38f,2.69f,-.02f),new Vector3(.72f,.055f,.71f),White,Quaternion.Euler(0,0,55f));
            for (var i = 0; i < 7; i++)
                frame.Tube(new Vector3(.18f+i*.035f,2.43f,-.36f),new Vector3(.59f+i*.035f,3.02f,-.36f),.003f,Metal,4);
            for (var i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1 : 1;
                frame.Tube(new Vector3(s*.61f,.87f,-.15f),new Vector3(s*1.45f,1.70f,-.4f),.009f,Metal,8);
                skin.Box(new Vector3(s*1.45f,1.70f,-.4f),new Vector3(.10f,.13f,.02f),White);
            }
            // Câmera cilíndrica, anéis, visor e pivôs preservados para SUR-007/008.
            skin.Tube(new Vector3(.56f,1.0f,.18f),new Vector3(.56f,1.49f,.18f),.092f,White,24);
            for (var i = 0; i < 5; i++) frame.Tube(new Vector3(.56f,1.03f+i*.09f,.18f),new Vector3(.56f,1.043f+i*.09f,.18f),.102f,Metal,24);
            var azimuth = Child(body,"TV_Mirror_Azimuth"); azimuth.localPosition = new Vector3(.56f,1.51f,.18f);
            var elevation = Child(azimuth,"TV_Mirror_Elevation"); elevation.localPosition = Vector3.zero;
            var mirror = new SurveyorSiteGeometry.Draft();
            mirror.Tube(new Vector3(0,-.015f,0),new Vector3(0,.025f,0),.105f,White,24);
            mirror.Tube(new Vector3(0,.025f,0),new Vector3(0,.07f,0),.105f,White,24,.087f);
            mirror.Tube(new Vector3(0,.07f,0),new Vector3(0,.10f,0),.087f,White,24,.04f);
            mirror.Tube(new Vector3(0,.1f,0),new Vector3(0,.11f,0),.04f,White,24,.006f);
            mirror.Box(new Vector3(0,.06f,.10f),new Vector3(.16f,.11f,.014f),Metal,Quaternion.Euler(-15,0,0));
            Surface(Child(elevation,"MirrorHead"),MeshAsset(context,"MirrorHead",mirror.Write),metal,false,true);
            var mount = Child(elevation,"CameraMount"); mount.localPosition = new Vector3(0,.06f,.145f);
            var arm1 = Child(body,"Scoop_Arm_1"); arm1.localPosition = new Vector3(.42f,.67f,.45f);
            var arm2 = Child(arm1,"Scoop_Arm_2"); arm2.localPosition = new Vector3(.0f,-.18f,.42f);
            var upperArm = new SurveyorSiteGeometry.Draft();
            var lowerArm = new SurveyorSiteGeometry.Draft();
            for (var i = 0; i < 2; i++)
            {
                float x = i == 0 ? -.04f : .04f;
                upperArm.Tube(new Vector3(x,0,0),new Vector3(x,-.18f,.42f),.012f,Metal,8);
                lowerArm.Tube(new Vector3(x,0,0),new Vector3(x,-.12f,.38f),.012f,Metal,8);
            }
            upperArm.Tube(Vector3.zero,new Vector3(0,-.18f,.42f),.009f,Dark,6);
            Surface(Child(arm1,"UpperLinkage"),MeshAsset(context,"UpperLinkage",upperArm.Write),metal,false,true);
            Surface(Child(arm2,"LowerLinkage"),MeshAsset(context,"LowerLinkage",lowerArm.Write),metal,false,true);
            var scoop = Child(arm2,"Scoop"); scoop.localPosition = new Vector3(0,-.12f,.38f);
            var tool = new SurveyorSiteGeometry.Draft();
            tool.Box(new Vector3(0,-.02f,0),new Vector3(.052f,.006f,.12f),Metal);
            tool.Box(new Vector3(-.026f,0,0),new Vector3(.006f,.04f,.12f),Metal);
            tool.Box(new Vector3(.026f,0,0),new Vector3(.006f,.04f,.12f),Metal);
            tool.Box(new Vector3(0,0,-.06f),new Vector3(.052f,.04f,.006f),Metal);
            Surface(Child(scoop,"ScoopShell"),MeshAsset(context,"ScoopShell",tool.Write),metal,false,true);
            Surface(Child(body,"StaticFrame"),MeshAsset(context,"StaticFrame",frame.Write),metal,false);
            Surface(Child(body,"ThermalAndPanels"),MeshAsset(context,"ThermalAndPanels",skin.Write),painted,false);

            var anchors = Child(root.transform,"Anchors");
            Anchor(anchors,"LandingMarks",SurveyorSiteGeometry.Ground(-2.2f,4f,.02f));
            Anchor(anchors,"CraterSlope",SurveyorSiteGeometry.Ground(-30f,10f,.9f));
            Anchor(anchors,"Footpad",pads.Find("Footpad_1").position + pads.Find("Footpad_1").up * .1f);
            Anchor(anchors,"Trench",SurveyorSiteGeometry.Ground(.65f,1.62f,.01f));
            Anchor(anchors,"Bearing",SurveyorSiteGeometry.Ground(.98f,1.34f,.01f));
            Anchor(anchors,"Impact",SurveyorSiteGeometry.Ground(1.25f,1.09f,.01f));
            Anchor(anchors,"Eclipse",new Vector3(-40f,65f,45f));
            Anchor(anchors,"Visit",SurveyorSiteGeometry.Ground(-170f,60f));
        }

        static void Anchor(Transform parent, string name, Vector3 position) => Child(parent,name).localPosition = position;

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name); go.transform.SetParent(parent,false); return go.transform;
        }

        static Mesh MeshAsset(SceneBuildContext context, string name, Action<Mesh> configure)
        {
            var mesh = context.Asset(GeneratedPath + "/" + name + ".asset", () => new Mesh { name = name });
            configure(mesh); EditorUtility.SetDirty(mesh); return mesh;
        }

        static void Surface(Transform t, Mesh mesh, Material material, bool collider, bool moving = false)
        {
            var filter = t.GetComponent<MeshFilter>();
            if (filter == null) filter = t.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = t.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = t.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;
            if (collider)
            {
                var c = t.GetComponent<MeshCollider>();
                if (c == null) c = t.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = mesh;
            }
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, moving ? 0 : StaticEditorFlags.BatchingStatic);
        }

        static Texture2D Texture(string name, int size, bool normal, Func<int,int,int,Color> pixel)
        {
            Directory.CreateDirectory(GeneratedPath);
            string path = GeneratedPath + "/"+name+".png";
            var texture = new Texture2D(size,size,TextureFormat.RGB24,false,normal);
            var colors = new Color[size*size];
            for (var y=0;y<size;y++) for(var x=0;x<size;x++) colors[y*size+x]=pixel(x,y,size);
            texture.SetPixels(colors); texture.Apply(); var png = texture.EncodeToPNG(); Object.DestroyImmediate(texture);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png)) File.WriteAllBytes(path,png);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.maxTextureSize != size || importer.alphaSource != TextureImporterAlphaSource.None || importer.isReadable)
            {
                importer.textureType = type; importer.maxTextureSize = size; importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true; importer.isReadable = false; importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Grain(int x, int y)
        {
            unchecked
            {
                uint h=(uint)x*374761393u+(uint)y*668265263u;
                h=(h^(h>>13))*1274126177u; h^=h>>16;
                return (h>>8)/16777215f;
            }
        }
        static Color RegolithPixel(int x,int y,int size)
        {
            float u=(float)x/size,v=(float)y/size;
            float macro = .06f*Mathf.PerlinNoise(u*5.2f+7,v*5.2f+13)+.025f*Mathf.PerlinNoise(u*35f,v*35f);
            float grain = (Grain(x,y)-.5f)*.028f;
            float value=.36f+macro+grain;
            // Marcas de sapata no próprio albedo, sem decal transparente ou outro material.
            float px=u*40f-20f,pz=v*40f-20f;
            for(var i=0;i<3;i++)
            {
                float distance=(new Vector2(px,pz)-SurveyorSiteGeometry.LandingMarkCenter(i)).magnitude;
                value*=Mathf.Lerp(.68f,1f,Mathf.SmoothStep(0,1,(distance-.17f)/.08f));
            }
            return new Color(value,value*.985f,value*.95f);
        }
        static Color NormalPixel(int x,int y,int size)
        {
            var n=new Vector3((Grain(x+1,y)-Grain(x-1,y))*.22f,(Grain(x,y+1)-Grain(x,y-1))*.22f,1).normalized;
            return new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
        }
        static Color HardwarePixel(int x,int y,int size)
        {
            int band=x*4/size;
            float noise=(Grain(x,y)-.5f)*.025f;
            if(band==0) return new Color(.78f+noise,.79f+noise,.77f+noise);
            if(band==1) return new Color(.62f+noise,.65f+noise,.67f+noise);
            if(band==2) return new Color(.035f+noise,.042f+noise,.048f+noise);
            bool line=x%24<2 || y%48<2;
            return line ? new Color(.42f,.48f,.51f) : new Color(.035f+noise,.075f+noise,.13f+noise);
        }

        public static void Frame(Camera camera, Transform anchor, float fieldOfView)
        {
            camera.transform.rotation = Quaternion.LookRotation(anchor.position-camera.transform.position,Vector3.up);
            camera.fieldOfView = fieldOfView;
        }

        public override void Validate(SceneBuildContext context, List<string> errors) => errors.AddRange(SurveyorSiteAudit.Validate(context.Root));

        [MenuItem("Abandoned Space Apps/Surveyor/SUR-002/Reconstruir cena lunar")]
        public static void BuildFromMenu()
        {
            var result=SpaceAppsSceneBuilder.Run(new SurveyorSiteBuilder());
            if(!result.Succeeded) throw new InvalidOperationException(result.ToString());
            Debug.Log(result.ToString());
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildFromMenu();
                SurveyorSiteAudit.WriteReport();
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
