using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace Abandoned.SpaceApps
{
    /// <summary>Only installed in the QA baseline scene, never in chapter prefabs.</summary>
    public sealed class WebBaselineProbe:MonoBehaviour
    {
        [Serializable]sealed class Sample{public string device,graphics,unity;public int width,height,frames;public float meanFps,p95FrameMs;public long allocatedBytes,reservedBytes;}
        readonly float[] samples=new float[256];int count;float elapsed;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void WebBaselineReport(string json);
#endif
        void Update()
        {
            float dt=Time.unscaledDeltaTime;if(dt<=0)return;samples[count++]=dt;elapsed+=dt;
            if(count<samples.Length)return;
            Array.Sort(samples);var s=new Sample{device=SystemInfo.deviceModel,graphics=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,width=Screen.width,height=Screen.height,frames=count,meanFps=count/elapsed,p95FrameMs=samples[(int)(count*.95f)]*1000,allocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),reservedBytes=Profiler.GetTotalReservedMemoryLong()};
            string json=JsonUtility.ToJson(s);Debug.Log("QA_WEB "+json);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebBaselineReport(json);
#endif
            count=0;elapsed=0;
        }
    }
}
