using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    // Lightweight two-link visual rig; the technical view intentionally compresses deployment.
    public sealed class InstrumentArm : MonoBehaviour
    {
        public Transform shoulder, elbow, wrist, upperLink, lowerLink;
        public LineRenderer cable;
        public Collider specimen;
        void LateUpdate()=>RefreshPose();
        public void RefreshPose()
        {
            if (!shoulder || !wrist) return;
            Vector3 a = shoulder.position, b = wrist.position;
            Vector3 joint = Vector3.Lerp(a, b, .48f) + transform.up * .22f - transform.right * .19f;
            if(specimen)
            {
                Bounds bounds=specimen.bounds;
                float dx=Mathf.Max(bounds.min.x-b.x,0,b.x-bounds.max.x),dz=Mathf.Max(bounds.min.z-b.z,0,b.z-bounds.max.z);
                float clearance=1-Mathf.SmoothStep(0,1,Mathf.Sqrt(dx*dx+dz*dz)/.65f);
                joint.y=Mathf.Lerp(joint.y,Mathf.Max(joint.y,bounds.max.y+.32f),clearance);
            }
            elbow.position = joint;
            Connect(upperLink, a, joint); Connect(lowerLink, joint, b);
            if(cable){for(int i=0;i<cable.positionCount;i++){float u=i/(float)(cable.positionCount-1);Vector3 p=u<.5f?Vector3.Lerp(a,joint,u*2):Vector3.Lerp(joint,b,(u-.5f)*2);p+=transform.right*.045f+transform.up*Mathf.Sin(u*Mathf.PI)*.06f;cable.SetPosition(i,p);}}
        }
        static void Connect(Transform link, Vector3 a, Vector3 b)
        {
            link.position = (a + b) * .5f;
            link.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            link.localScale = new Vector3(.035f, Vector3.Distance(a, b) * .5f, .035f);
        }
    }
}
