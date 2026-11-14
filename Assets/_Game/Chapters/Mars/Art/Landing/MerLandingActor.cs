using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Pose de um ator do pouso no quadro do palco (metros e rotação).</summary>
    public struct MerLandingActor
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public bool Visible;
    }
}
