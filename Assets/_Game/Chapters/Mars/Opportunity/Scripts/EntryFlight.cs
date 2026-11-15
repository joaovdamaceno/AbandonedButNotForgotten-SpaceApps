using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    // Shared by the animated payload and the baked camera: one continuous trajectory.
    public static class EntryFlight
    {
        public static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        public static float CableDrop(float t)=>Mathf.Lerp(1.3f,11,Ease(8,14,t));
        public static Vector3 Upper(float t)
        {float u=Mathf.Min(t,18)/18;return new Vector3(-50+32*u,160-102*u,-55+35*u)+(t>18?new Vector3((t-18)*4,(t-18)*9,t-18):Vector3.zero);}
        public static float Inflation(float t)=>Mathf.Lerp(.035f,1,Ease(12.2f,15.2f,t));
        public static float Deflation(float t)=>Mathf.InverseLerp(38,46,t);
        public static float Opening(float t)=>Mathf.InverseLerp(46,53,t);
        public static float Deployed(float t)=>Ease(54,60,t);
        public static int Impact(float t)=>t<28.4f?-1:t<32.6f?0:t<35.8f?1:2;
        public static Vector3 Payload(float t,float clearance)
        {
            if(t<18)return Upper(t)-Vector3.up*CableDrop(t);
            if(t<28.4f){float u=(t-18)/10.4f;return new Vector3(-18*(1-u),clearance+(47-clearance)*Mathf.Pow(1-u,1.7f),-20*(1-u));}
            if(t>=38)return Vector3.up*clearance*(1-AirbagLanderRig.PressureLoss(Deflation(t)));
            float a,b,h;Vector3 p,q;
            if(t<32.6f){a=28.4f;b=32.6f;h=3.1f;p=Vector3.zero;q=new Vector3(2.7f,0,1.9f);}
            else if(t<35.8f){a=32.6f;b=35.8f;h=1.15f;p=new Vector3(2.7f,0,1.9f);q=new Vector3(1,0,1.1f);}
            else{a=35.8f;b=38;h=.3f;p=new Vector3(1,0,1.1f);q=Vector3.zero;}
            float v=(t-a)/(b-a);return Vector3.Lerp(p,q,v)+Vector3.up*(clearance+4*v*(1-v)*h);
        }
        public static Quaternion Rotation(float t)
        {
            if(t<18||t>=38)return Quaternion.Euler(0,35,0);
            if(t<28.4f){float u=(t-18)/10.4f;return Quaternion.Euler(Mathf.Sin(u*Mathf.PI)*2,35+Mathf.Sin(u*Mathf.PI)*2,-Mathf.Sin(u*Mathf.PI)*2);}
            int i=Impact(t);float a=i==0?28.4f:i==1?32.6f:35.8f,b=i==0?32.6f:i==1?35.8f:38;
            float v=(t-a)/(b-a);return Quaternion.Euler(Mathf.Sin(v*Mathf.PI*2)*18/(i+1),35+Mathf.Sin(v*Mathf.PI)*20/(i+1),Mathf.Sin(v*Mathf.PI)*13/(i+1));
        }
    }
}
