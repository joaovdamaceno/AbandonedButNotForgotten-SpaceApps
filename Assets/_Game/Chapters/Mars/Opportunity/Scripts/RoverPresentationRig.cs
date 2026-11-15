using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    public sealed class RoverPresentationRig : MonoBehaviour
    {
        public Transform leftPanel,rightPanel,mastFold;
        public void Fold(float amount)
        {
            amount=Mathf.Clamp01(amount);
            if(leftPanel)leftPanel.localRotation=Quaternion.Euler(0,0,-76*amount);
            if(rightPanel)rightPanel.localRotation=Quaternion.Euler(0,0,76*amount);
            if(mastFold)mastFold.localRotation=Quaternion.Euler(-88*amount,0,0);
        }
    }
}
