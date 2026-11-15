using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    [DefaultExecutionOrder(15)]
    public sealed class StormPresentation : MonoBehaviour
    {
        public ChapterDirector chapter;
        public ParticleSystem nearGrains,nearVeils,groundDrift;
        public float Gust{get;private set;}
        public float CameraMotion{get;private set;}
        bool active;Vector3 previousCamera;
        void Update()
        {
            if(chapter.Paused)return;
            var env=chapter.environment;float storm=env.Storm;
            bool enabled=env.OnSurface&&!env.Memorial&&storm>.10f;
            Vector3 camera=chapter.cameras.view.transform.position;
            if(!enabled)
            {
                if(active)foreach(var p in new[]{nearGrains,nearVeils,groundDrift})p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                active=false;Gust=CameraMotion=0;env.Gust=0;return;
            }
            if(!active||Vector3.Distance(camera,previousCamera)>25)
                foreach(var p in new[]{nearGrains,nearVeils,groundDrift}){p.Clear();p.Play();}
            active=true;previousCamera=camera;
            float time=chapter.Elapsed;
            float broad=Mathf.PerlinNoise(time*.24f+4,19),fine=Mathf.PerlinNoise(time*.71f+8,5);
            float irregular=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.74f,broad*.7f+fine*.3f));
            float hit=chapter.Stage==ChapterStage.TimeSkip?Mathf.Exp(-Mathf.Pow((time-chapter.film.YearsStormStart-2.8f)/1.5f,2)):chapter.Stage==ChapterStage.CommunicationLost?Mathf.Exp(-Mathf.Pow((time-5)/1.8f,2)):0;
            Gust=Mathf.Clamp01(irregular*.85f+hit*.7f);env.Gust=Gust;
            float intensity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.88f,storm));
            CameraMotion=intensity*(.055f+Gust*.65f);
            if(chapter.Stage==ChapterStage.CommunicationLost&&time>8)CameraMotion*=Mathf.InverseLerp(12,8,time);
            nearGrains.transform.position=camera+chapter.cameras.view.transform.forward*4+Vector3.up*.1f;
            nearVeils.transform.position=camera+chapter.cameras.view.transform.forward*7;
            groundDrift.transform.position=chapter.rover.transform.position+Vector3.up*.22f;
            Rate(nearGrains,intensity*(30+Gust*90));Rate(nearVeils,intensity*(1.8f+Gust*4));Rate(groundDrift,intensity*(22+Gust*42));
            foreach(var p in new[]{nearGrains,nearVeils,groundDrift})
            {var velocity=p.velocityOverLifetime;velocity.x=-4-Gust*8;velocity.z=1.5f+Gust*2;}
        }
        static void Rate(ParticleSystem p,float value){var emission=p.emission;emission.rateOverTime=value;}
    }
}
