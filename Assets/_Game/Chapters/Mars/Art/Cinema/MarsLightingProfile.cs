using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Exposição de uma região: Sol principal (a única luz com sombra), ambiente e fundo da câmera. Graus e cores lineares do Inspector.</summary>
    [Serializable]
    public struct MarsLightingProfile
    {
        public string regionId;
        [Min(0f)] public float sunIntensity;
        public Color sunColor;
        public Vector3 sunEulerDegrees;
        public Color ambientColor;
        public Color backgroundColor;
    }
}
