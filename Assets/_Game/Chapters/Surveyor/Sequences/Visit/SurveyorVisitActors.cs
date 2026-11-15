using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Reconstructed actors, evaluated from the film clock; no independent animation/particle timers.</summary>
    public sealed class SurveyorVisitActors : MonoBehaviour
    {
        [SerializeField] Transform lander, walkers, leftBoot, rightBoot, glove, handFrame;
        [SerializeField] Transform[] grains;
        [SerializeField] Vector3 landingPosition, walkStart, walkEnd;
        [SerializeField] Vector3 gloveStart, gloveEnd;
        public Transform Lander=>lander;
        public Transform Glove=>glove;
        public Vector3 LandingPosition=>landingPosition;
        public void Configure(Transform lm,Transform people,Transform left,Transform right,Transform hand,Transform[] dust,
            Vector3 landing,Vector3 start,Vector3 end,Vector3 handStart,Vector3 handEnd)
        {lander=lm;walkers=people;leftBoot=left;rightBoot=right;glove=hand;grains=dust;landingPosition=landing;walkStart=start;walkEnd=end;gloveStart=handStart;gloveEnd=handEnd;}
        public void SetHandFrame(Transform frame)=>handFrame=frame;
        public void Evaluate(float seconds,bool reducedMotion)
        {
            bool world=seconds>=5 && seconds<31;
            lander.gameObject.SetActive(world);walkers.gameObject.SetActive(seconds>=19 && seconds<31);glove.gameObject.SetActive(seconds>=26 && seconds<31);
            float descent=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,10,seconds));
            lander.position=landingPosition+Vector3.up*(1-descent)*24;
            float walk=Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,25,seconds));walkers.position=Vector3.Lerp(walkStart,walkEnd,walk);
            float stride=seconds<25?Mathf.Sin(Mathf.Clamp(seconds-20,0,5)*Mathf.PI*1.6f):0;
            leftBoot.localPosition=new Vector3(-.16f,Mathf.Max(0,stride)*.09f,.05f+stride*.12f);
            rightBoot.localPosition=new Vector3(.16f,Mathf.Max(0,-stride)*.09f,-.08f-stride*.12f);
            float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(26,30.5f,seconds));
            var basis=handFrame!=null?handFrame:transform;
            glove.SetPositionAndRotation(basis.TransformPoint(Vector3.Lerp(gloveStart,gloveEnd,reach)),
                basis.rotation*Quaternion.Euler(15,-25,Mathf.Lerp(40,reducedMotion?25:20,reach)));
            for(int i=0;i<grains.Length;i++)
            {
                float age=seconds-(10+(i%12)*.075f);float velocity=.35f+(i%3)*.07f;
                float height=velocity*age-.81f*age*age;bool flying=world && age>0 && height>0;
                grains[i].gameObject.SetActive(flying);
                if(flying)
                {
                    float angle=i*2.399963f;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    grains[i].position=landingPosition+direction*(2.5f+age*(5+i%4))+Vector3.up*(.025f+height);
                }
            }
        }
        public void Hide()
        {if(lander!=null)lander.gameObject.SetActive(false);if(walkers!=null)walkers.gameObject.SetActive(false);if(glove!=null)glove.gameObject.SetActive(false);if(grains!=null)foreach(var grain in grains)grain.gameObject.SetActive(false);}
    }
}
