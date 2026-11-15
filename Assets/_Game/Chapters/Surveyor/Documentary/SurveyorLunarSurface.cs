using UnityEngine;
namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Same height function as the authored site, available to ground-safe documentary shots at runtime.</summary>
    public static class SurveyorLunarSurface
    {
        public static float Height(float x, float z)
        {
            float radius = new Vector2(x + 65f, z).magnitude, r = Mathf.Min(radius / 105f, 1f);
            float h = 15f * (r * r - 65f * 65f / (105f * 105f));
            if (radius > 105f) h -= Mathf.Min((radius - 105f) * .025f, 2f);
            h += .16f * (Mathf.PerlinNoise(x * .065f + 20f, z * .065f + 30f) - .5f);
            h += .035f * (Mathf.PerlinNoise(x * .73f + 80f, z * .73f + 60f) - .5f);
            return h + Crater(x,z,-7,5,2.7f,.48f) + Crater(x,z,10,-7,1.8f,.27f) + Crater(x,z,-17,-9,4.7f,.8f) + Crater(x,z,-37,23,8,1.8f);
        }
        static float Crater(float x,float z,float cx,float cz,float radius,float depth)
        { float r = new Vector2(x-cx,z-cz).magnitude/radius; return r>1.4f?0f:-depth*Mathf.Pow(Mathf.Max(0,1-r*r),2)+depth*.22f*Mathf.Exp(-Mathf.Pow((r-1.03f)*6,2)); }
    }
}
