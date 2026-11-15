using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    // Editorial clock shared by actors, impacts and the baked lens. Each interval has a visual purpose.
    public static class CompactLandingClock
    {
        static readonly float[] cut={0,2.6f,4.5f,7.5f,10.5f,13.5f,16,18};
        static readonly float[] source={0,8,18,28.4f,38,46,53,60};
        public static float SourceTime(float time)
        {
            time=Mathf.Clamp(time,0,18);
            for(int i=1;i<cut.Length;i++)if(time<=cut[i])return Mathf.Lerp(source[i-1],source[i],Mathf.InverseLerp(cut[i-1],cut[i],time));
            return 60;
        }
    }
}
