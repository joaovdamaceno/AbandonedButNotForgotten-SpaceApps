using UnityEngine;
using Draft=Abandoned.SpaceApps.Chapters.Surveyor.EditorTools.SurveyorSiteGeometry.Draft;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>Own low-cost silhouettes/partial props; proportions guided by the mission photograph.</summary>
    public static class SurveyorVisitGeometry
    {
        public static void LanderGold(Mesh mesh)
        {
            var d=new Draft();d.Tube(new Vector3(0,1.1f,0),new Vector3(0,2.6f,0),2.2f,.5f,8,2);
            for(int i=0;i<4;i++)
            {
                float a=(45+90*i)*Mathf.Deg2Rad;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var foot=direction*4.45f+Vector3.up*.12f;
                d.Tube(direction*1.6f+Vector3.up*2.4f,foot,.1f,.5f,8);
                d.Tube(direction*.65f+Vector3.up*1.1f,foot+Vector3.up*.4f,.065f,.5f,8);
                d.Tube(foot-Vector3.up*.10f,foot+Vector3.up*.04f,.43f,.5f,12);
            }
            d.Tube(new Vector3(0,1.5f,0),new Vector3(0,.55f,0),.38f,.5f,12,.65f);d.Write(mesh);
        }
        public static void LanderCabin(Mesh mesh)
        {
            var d=new Draft();d.Tube(new Vector3(0,2.6f,0),new Vector3(0,5.4f,0),1.6f,.5f,8,1.45f);
            d.Box(new Vector3(0,3.8f,1.45f),new Vector3(2.3f,2.0f,.65f),.5f,Quaternion.Euler(12,0,0));
            d.Tube(new Vector3(.7f,5.3f,0),new Vector3(.7f,6.5f,0),.035f,.5f,6);
            d.Tube(new Vector3(.7f,6.45f,0),new Vector3(.7f,6.55f,0),.4f,.5f,10);
            for(int i=0;i<8;i++)d.Box(new Vector3(0,.65f+i*.27f,2.45f),new Vector3(.75f,.05f,.15f),.5f);
            d.Write(mesh);
        }
        public static void LanderWindows(Mesh mesh)
        {
            var d=new Draft();d.Triangle(new Vector3(-1.05f,4.1f,1.85f),new Vector3(-.2f,4.1f,1.85f),new Vector3(-.5f,4.9f,1.68f),.5f);
            d.Triangle(new Vector3(.2f,4.1f,1.85f),new Vector3(1.05f,4.1f,1.85f),new Vector3(.5f,4.9f,1.68f),.5f);d.Write(mesh);
        }
        public static void Boot(Mesh mesh)
        {
            var d=new Draft();d.Box(new Vector3(0,.04f,.035f),new Vector3(.15f,.07f,.33f),.5f);
            d.Tube(new Vector3(0,.115f,-.04f),new Vector3(0,.115f,.13f),.085f,.5f,12,.065f);
            // Continue above the technical camera's field instead of showing a severed shin cap.
            d.Tube(new Vector3(0,.12f,-.06f),new Vector3(0,1.65f,-.06f),.085f,.5f,12,.13f);
            for(int i=0;i<5;i++)d.Tube(new Vector3(0,.21f+i*.045f,-.06f),new Vector3(0,.22f+i*.045f,-.06f),.098f,.5f,12);
            d.Write(mesh);
        }
        public static void Glove(Mesh mesh)
        {
            var d=new Draft();d.Tube(new Vector3(0,0,-.24f),new Vector3(0,0,-.02f),.074f,.5f,12,.065f);
            d.Tube(new Vector3(0,0,-.06f),new Vector3(0,0,-.015f),.085f,.5f,16);
            d.Box(new Vector3(0,0,.035f),new Vector3(.14f,.07f,.12f),.5f);
            for(int i=0;i<4;i++)
            {
                float x=-.051f+i*.034f,len=i==3?.065f:i==1?.105f:.09f;
                var knuckle=new Vector3(x,0,.08f);var bend=knuckle+new Vector3(0,-.008f,len*.55f);var tip=bend+new Vector3(0,-.028f,len*.45f);
                d.Tube(knuckle,bend,.021f,.5f,8,.018f);d.Tube(bend,tip,.019f,.5f,8,.012f);
            }
            d.Tube(new Vector3(-.062f,0,.015f),new Vector3(-.115f,-.015f,.075f),.026f,.5f,8,.018f);d.Write(mesh);
        }
        public static void ShadowBody(Mesh mesh)
        {
            var d=new Draft();d.Tube(new Vector3(-.16f,.35f,0),new Vector3(-.13f,.95f,0),.11f,.5f,8);
            d.Tube(new Vector3(.16f,.35f,0),new Vector3(.13f,.95f,0),.11f,.5f,8);
            d.Box(new Vector3(0,1.25f,0),new Vector3(.55f,.65f,.36f),.5f);
            d.Tube(new Vector3(0,1.52f,0),new Vector3(0,1.84f,0),.19f,.5f,10,.16f);d.Write(mesh);
        }
        public static void Grain(Mesh mesh)
        {var d=new Draft();d.Triangle(new Vector3(-.24f,0,-.1f),new Vector3(.24f,.04f,-.1f),new Vector3(.1f,0,.22f),.5f);d.Write(mesh);}
    }
}
