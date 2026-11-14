using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Estado completo do pouso num instante, no quadro do palco. Função pura de (tempo, local): sem estado oculto.</summary>
    public struct MerLandingPose
    {
        public MerLandingActor Capsule;
        public MerLandingActor HeatShield;
        public MerLandingActor Lander;
        public MerLandingActor Parachute;
        public bool TetherVisible;
        public Vector3 TetherTop;
        public Vector3 TetherBottom;

        /// <summary>0–1.</summary>
        public float Fireball;
        public float Retro;
        public float ChuteInflation;
        public float AirbagInflation;
        public float AirbagDeflation;
        public float PetalOpen;
        public float RoverUnfold;
    }
}
