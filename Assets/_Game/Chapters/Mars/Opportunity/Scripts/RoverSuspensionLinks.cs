using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    // Small articulated lower knuckles bridge the static NASA rocker geometry
    // to the terrain-following wheel carriers. They do not change vehicle physics.
    public sealed class RoverSuspensionLinks : MonoBehaviour
    {
        public RoverController rover;public Transform[] links,knuckles;public Vector3[] mounts;
        void LateUpdate()=>RefreshPose();
        public void RefreshPose()
        {
            if(!rover||links==null)return;
            for(int i=0;i<links.Length;i++)
            {
                var axle=rover.wheels[i].parent;float side=axle.localPosition.x<0?-1:1;
                Vector3 a=rover.visual.TransformPoint(mounts[i]),b=axle.TransformPoint(new Vector3(-side*.064f,0,0));
                links[i].SetPositionAndRotation((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,b-a));
                links[i].localScale=new Vector3(.016f,Vector3.Distance(a,b)*.5f,.016f);
                knuckles[i].position=b;
            }
        }
    }
}
