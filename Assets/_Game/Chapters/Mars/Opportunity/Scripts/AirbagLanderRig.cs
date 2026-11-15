using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    public sealed class AirbagLanderRig : MonoBehaviour
    {
        public float apothem=1.35f,deckHeight=.22f,closedRise=2.5f,closedAngle=-28,openAngle=95,landingClearance=1;
        public Transform[] cushions;public SkinnedMeshRenderer[] cloth;public Vector3[] inflated,withdrawn;
        public static float PressureLoss(float deflation)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.66f,deflation));
        public void Pose(float deflation,float opening)
        {
            float pressure=PressureLoss(deflation);
            float gather=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.66f,1,deflation));
            for(int i=0;i<cushions.Length;i++)
            {
                cushions[i].localPosition=inflated[i];
                cloth[i].SetBlendShapeWeight(0,pressure*100);
                cloth[i].SetBlendShapeWeight(1,gather*100);
            }
            for(int i=0;i<petals.Length;i++)petals[i].localRotation=Quaternion.Euler(Mathf.Lerp(closedAngle,openAngle,Mathf.SmoothStep(0,1,opening)),i*120,0);
        }
        public Transform[] petals;
    }
}
