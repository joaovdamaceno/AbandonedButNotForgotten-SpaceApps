using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    // Gameplay effort and thermal protection, not historical MER telemetry.
    public sealed class AbrasionLoad
    {
        public float Pressure{get;private set;}=.5f;
        public float Temperature{get;private set;}
        public bool Recovering{get;private set;}
        public bool Cutting{get;private set;}
        public bool Operating{get;private set;}
        public float Low{get;private set;}=.38f;
        public float High{get;private set;}=.68f;
        float time;
        public void Reset(){Pressure=.5f;Temperature=time=0;Recovering=Cutting=Operating=false;}
        public void Tick(float dt,float adjustment,bool held,float resistance,bool easy)
        {
            Low=easy?.24f:.38f;High=easy?.83f:.68f;
            // Substeps keep keyboard response and cooling independent of frame rate.
            for(float remaining=Mathf.Max(0,dt);remaining>0;)
            {
                float step=Mathf.Min(.025f,remaining);remaining-=step;time+=step;
                float drift=(.035f+.025f*resistance+.012f*Mathf.Sin(time*.83f))*(easy?.35f:1);
                if(Recovering){if(!held||adjustment<0)Pressure=Mathf.MoveTowards(Pressure,.45f,step*(.12f+Mathf.Max(0,-adjustment)*.34f));}
                else if(held)Pressure=Mathf.Clamp01(Pressure+step*(Mathf.Clamp(adjustment,-1,1)*.34f+drift));
                else Pressure=Mathf.MoveTowards(Pressure,.46f,step*.085f);
                if(easy&&!Recovering&&Mathf.Abs(adjustment)<.01f)Pressure=Mathf.Lerp(Pressure,.52f,step*1.7f);
                bool excessive=held&&!Recovering&&Pressure>High;
                if(excessive)Temperature=Mathf.Clamp01(Temperature+step*(.16f+.58f*Mathf.InverseLerp(High,1,Pressure)));
                else Temperature=Mathf.Max(0,Temperature-step*(held&&!Recovering?.07f:.31f));
                if(Temperature>=.999f)Recovering=true;
                if(Recovering&&Temperature<=.28f&&(!held||Pressure<=High))Recovering=false;
            }
            Operating=held&&!Recovering;
            Cutting=Operating&&Pressure>=Low&&Pressure<=High;
        }
    }
}
