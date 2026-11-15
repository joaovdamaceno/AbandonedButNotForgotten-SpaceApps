using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    // Baked from the same Timeline curves. Explicit transform evaluation avoids player root-motion stripping.
    public sealed class CinematicPath : ScriptableObject
    {
        public float sampleRate = 30;
        public Vector3[] positions;
        public Quaternion[] rotations;
        public float[] fieldsOfView;
        public float[] cutTimes;
        public float Lens(double time)
        { if(fieldsOfView==null||fieldsOfView.Length==0)return 45;float frame=Mathf.Clamp((float)time*sampleRate,0,fieldsOfView.Length-1);int a=Mathf.FloorToInt(frame),b=Mathf.Min(a+1,fieldsOfView.Length-1);return Mathf.Lerp(fieldsOfView[a],fieldsOfView[b],frame-a); }
        public void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float frame = Mathf.Clamp((float)time * sampleRate, 0, positions.Length - 1);
            int a = Mathf.FloorToInt(frame), b = Mathf.Min(a + 1, positions.Length - 1);
            bool cut=false;if(cutTimes!=null)foreach(float t in cutTimes)if(a/sampleRate<t&&b/sampleRate>=t){cut=true;break;}
            position = Vector3.Lerp(positions[a], positions[b], cut?0:frame-a);
            rotation = Quaternion.Slerp(rotations[a], rotations[b], cut?0:frame-a);
        }
    }
}
