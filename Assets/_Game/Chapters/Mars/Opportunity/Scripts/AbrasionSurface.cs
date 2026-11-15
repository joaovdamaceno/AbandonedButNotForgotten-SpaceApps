using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class AbrasionSurface : MonoBehaviour
    {
        const int Resolution=160,GridSide=33,MaxCuttings=240;
        public float width=.94f,depth=.72f,maxCutDepth=.045f;
        public Transform workFrame;public Vector3[] contactPoints;public Material cuttingsMaterial;
        Texture2D mask;float[] wear;Color32[] pixels;Mesh mesh;Vector3[] original,vertices,normals;
        Vector2[] workUV;float[] cuts,upWeights;Material material;MeshCollider collision;float meshClock,grainBudget;
        Mesh debris;Vector3[] debrisVertices=new Vector3[MaxCuttings*4];int[] debrisTriangles=new int[MaxCuttings*12];int grains;System.Random random=new System.Random(2004);
        Vector2[] grainUV=new Vector2[MaxCuttings];float[] grainSize=new float[MaxCuttings],grainHeight=new float[MaxCuttings];
        public float Coverage{get;private set;}
        public float CircularProgress{get;private set;}
        public float LastRemoval{get;private set;}
        public int CuttingsCount=>grains;
        public readonly Vector2[] Features={new Vector2(.47f,.52f),new Vector2(.53f,.49f)};
        public void Initialize()
        {
            if(wear!=null)return;
            wear=new float[Resolution*Resolution];pixels=new Color32[wear.Length];
            mask=new Texture2D(Resolution,Resolution,TextureFormat.RGBA32,false,true){name="RAT progressive cut",wrapMode=TextureWrapMode.Clamp};
            material=GetComponent<MeshRenderer>().material;material.SetTexture("_WearMask",mask);
            material.SetMatrix("_WorldToWork",workFrame.worldToLocalMatrix);material.SetVector("_WorkSize",new Vector4(width,depth,1f/Resolution,0));
            mesh=GetComponent<MeshFilter>().mesh;mesh.MarkDynamic();original=mesh.vertices;vertices=mesh.vertices;normals=mesh.normals;
            workUV=new Vector2[vertices.Length];cuts=new float[vertices.Length];upWeights=new float[vertices.Length];collision=GetComponent<MeshCollider>();
            Matrix4x4 normalMatrix=transform.localToWorldMatrix.inverse.transpose;
            for(int i=0;i<original.Length;i++)
            {
                Vector3 p=workFrame.InverseTransformPoint(transform.TransformPoint(original[i]));
                workUV[i]=new Vector2(p.x/width+.5f,p.z/depth+.5f);
                float upward=Vector3.Dot(normalMatrix.MultiplyVector(normals[i]).normalized,workFrame.up);
                upWeights[i]=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.45f,upward));
                cuts[i]=Mathf.Min(0,SmoothHeight(workUV[i])-p.y)-maxCutDepth;
            }
            var settled=new GameObject("RAT • settled cuttings");settled.transform.SetParent(transform,false);
            debris=new Mesh{name="Accumulated abrasion cuttings"};debris.MarkDynamic();settled.AddComponent<MeshFilter>().sharedMesh=debris;
            settled.AddComponent<MeshRenderer>().sharedMaterial=cuttingsMaterial?cuttingsMaterial:GetComponent<MeshRenderer>().sharedMaterial;
            ResetSurface();
        }
        public void ResetSurface()
        {
            if(wear==null){Initialize();return;}
            System.Array.Clear(wear,0,wear.Length);CircularProgress=Coverage=LastRemoval=0;grains=0;grainBudget=0;random=new System.Random(2004);debris.Clear();
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(0,0,0,255);
            mask.SetPixels32(pixels);mask.Apply(false);System.Array.Copy(original,vertices,original.Length);
            mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();material.SetFloat("_Inspection",0);meshClock=0;CommitCollision();
        }
        float WearAt(Vector2 uv)
        {
            if(uv.x<0||uv.x>1||uv.y<0||uv.y>1)return 0;
            float fx=uv.x*(Resolution-1),fy=uv.y*(Resolution-1);int x=Mathf.Min(Resolution-2,(int)fx),y=Mathf.Min(Resolution-2,(int)fy);
            return Mathf.Lerp(Mathf.Lerp(wear[y*Resolution+x],wear[y*Resolution+x+1],fx-x),Mathf.Lerp(wear[(y+1)*Resolution+x],wear[(y+1)*Resolution+x+1],fx-x),fy-y);
        }
        static float EdgeFade(Vector2 uv)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.07f,Mathf.Min(uv.x,1-uv.x,uv.y,1-uv.y)));
        Vector3 OriginalPoint(Vector2 uv)
        {
            float fx=Mathf.Clamp01(uv.x)*(GridSide-1),fy=Mathf.Clamp01(uv.y)*(GridSide-1);
            int x=Mathf.Min(GridSide-2,(int)fx),y=Mathf.Min(GridSide-2,(int)fy);float a=fx-x,b=fy-y;
            return Vector3.Lerp(Vector3.Lerp(contactPoints[y*GridSide+x],contactPoints[y*GridSide+x+1],a),Vector3.Lerp(contactPoints[(y+1)*GridSide+x],contactPoints[(y+1)*GridSide+x+1],a),b);
        }
        float SmoothHeight(Vector2 uv)
        {
            float value=OriginalPoint(uv).y*4;
            value+=OriginalPoint(uv+Vector2.right*.055f).y+OriginalPoint(uv-Vector2.right*.055f).y;
            value+=OriginalPoint(uv+Vector2.up*.055f).y+OriginalPoint(uv-Vector2.up*.055f).y;return value/8;
        }
        public float Brush(Vector2 uv,float seconds,float pressure,float low=.38f,float high=.68f)
        {
            Initialize();LastRemoval=0;if(seconds<=0||pressure<low||pressure>high)return Coverage;
            float radius=.135f,rate=seconds*.30f,previous=Coverage;
            int minX=Mathf.Max(0,Mathf.FloorToInt((uv.x-radius)*Resolution)),maxX=Mathf.Min(Resolution-1,Mathf.CeilToInt((uv.x+radius)*Resolution));
            int minY=Mathf.Max(0,Mathf.FloorToInt((uv.y-radius)*Resolution)),maxY=Mathf.Min(Resolution-1,Mathf.CeilToInt((uv.y+radius)*Resolution));
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                Vector2 p=new Vector2(x/(float)Resolution,y/(float)Resolution);float d=Vector2.Distance(p,uv)/radius;int i=y*Resolution+x;
                // A flatter grinding face with a soft circular shoulder gives a readable shallow cut.
                float face=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,d));
                wear[i]=Mathf.Clamp01(wear[i]+rate*face*EdgeFade(p));pixels[i]=new Color32((byte)(wear[i]*255),0,0,255);
            }
            float total=0;foreach(float value in wear)total+=Mathf.SmoothStep(0,1,value);Coverage=total/wear.Length;
            LastRemoval=Mathf.Max(0,Coverage-previous);mask.SetPixels32(pixels);mask.Apply(false);meshClock+=seconds;
            if(meshClock>.065f){meshClock=0;ApplyCut();}
            grainBudget+=LastRemoval*1250;
            while(grainBudget>=1&&grains<MaxCuttings){grainBudget--;AddCutting(uv,radius);}
            if(grains>0)UpdateCuttings();return Coverage;
        }
        public void GrindCircular(float seconds,float pressure,float low,float high)
        {
            Initialize();LastRemoval=0;if(seconds<=0||pressure<low||pressure>high)return;
            float previous=Coverage;CircularProgress=Mathf.Clamp01(CircularProgress+seconds/32f);
            const float radius=.115f;
            float total=0;
            for(int y=0;y<Resolution;y++)for(int x=0;x<Resolution;x++)
            {
                float dx=(x/(float)(Resolution-1)-.5f)*width,dz=(y/(float)(Resolution-1)-.5f)*depth;
                float d=Mathf.Sqrt(dx*dx+dz*dz)/radius;
                float profile=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.83f,1,d));int i=y*Resolution+x;
                wear[i]=CircularProgress*profile;pixels[i]=new Color32((byte)(wear[i]*255),0,0,255);total+=Mathf.SmoothStep(0,1,wear[i]);
            }
            Coverage=total/wear.Length;LastRemoval=Mathf.Max(0,Coverage-previous);mask.SetPixels32(pixels);mask.Apply(false);meshClock+=seconds;
            if(meshClock>.065f||CircularProgress>=1){meshClock=0;ApplyCut();}
            grainBudget+=LastRemoval*4200;
            while(grainBudget>=1&&grains<MaxCuttings){grainBudget--;AddCutting(new Vector2(.5f,.5f),radius/width);grainSize[grains-1]*=.32f;grainSize[grains-1]*=.32f;}
            if(grains>0)UpdateCuttings();
        }
        void ApplyCut()
        {
            for(int i=0;i<vertices.Length;i++)
            {
                float amount=WearAt(workUV[i])*upWeights[i];
                vertices[i]=original[i]+transform.InverseTransformVector(workFrame.up*(amount*cuts[i]));
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        public void CommitCollision()
        {
            if(!mesh||!collision)return;
            collision.sharedMesh=null;collision.sharedMesh=mesh;
        }
        public Vector3 WorldPoint(Vector2 uv)
        {
            Initialize();Vector3 p=OriginalPoint(uv);float cut=Mathf.Min(0,SmoothHeight(uv)-p.y)-maxCutDepth;
            p.y+=WearAt(uv)*cut;return workFrame.TransformPoint(p)+workFrame.up*.008f;
        }
        void AddCutting(Vector2 center,float radius)
        {
            float angle=(float)random.NextDouble()*Mathf.PI*2,range=radius*(.88f+(float)random.NextDouble()*.32f);
            Vector2 uv=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*range;
            uv=new Vector2(Mathf.Clamp01(uv.x),Mathf.Clamp01(uv.y));
            grainUV[grains]=uv;grainSize[grains]=.0035f+(float)random.NextDouble()*.006f;grainHeight[grains]=.18f+(float)random.NextDouble()*.35f;int k=grains*4;
            int j=grains*12;int[] face={0,2,1,0,1,3,1,2,3,2,0,3};for(int i=0;i<12;i++)debrisTriangles[j+i]=k+face[i];grains++;
        }
        void UpdateCuttings()
        {
            // Existing residue follows later passes instead of hanging above the lowered surface.
            for(int i=0;i<grains;i++)
            {
                float size=grainSize[i];var uv=grainUV[i];int k=i*4;
                Vector3 Point(Vector2 p)=>transform.InverseTransformPoint(WorldPoint(p)-workFrame.up*.007f);
                debrisVertices[k]=Point(uv+new Vector2(-size/width,-size*.6f/depth));
                debrisVertices[k+1]=Point(uv+new Vector2(size/width,-size*.45f/depth));
                debrisVertices[k+2]=Point(uv+new Vector2(0,size/depth));
                debrisVertices[k+3]=Point(uv)+transform.InverseTransformVector(workFrame.up*(size*grainHeight[i]));
            }
            debris.Clear();debris.SetVertices(debrisVertices,0,grains*4);debris.SetTriangles(debrisTriangles,0,grains*12,0);debris.RecalculateNormals();debris.RecalculateBounds();
        }
        public void SetInspection(int index){if(material)material.SetFloat("_Inspection",index);}
        void OnDestroy(){if(mask)Destroy(mask);if(material)Destroy(material);if(mesh)Destroy(mesh);if(debris)Destroy(debris);}
    }
}
