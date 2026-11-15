using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>
    /// Articulações do rover MER da equipe (<c>MER_Rover.fbx</c>, ver Art/Rover/RIG.md) que o capítulo move em conjunto:
    /// o diferencial do rocker-bogie, que segue o terreno, e a pose guardada dentro do módulo de pouso.
    /// Não tem relógio nem entrada: <see cref="RoverController"/> informa a suspensão e as cenas de pouso chamam <see cref="Fold"/>.
    /// </summary>
    public sealed class RoverPresentationRig : MonoBehaviour
    {
        public const float StowedRockerDegrees = 32f, StowedBogieDegrees = -48f, StowedMastDegrees = 75f, StowedDishDegrees = 70f;
        [SerializeField] Transform rockerLeft, rockerRight, bogieLeft, bogieRight, mast, mastHead, dish;
        float fold, rocker, bogieLeftAngle, bogieRightAngle;

        /// <summary>Azimute do mastro (<c>PMA_Mast</c>): eixo Y, + olha para a direita.</summary>
        public Transform Mast => mast;
        public float FoldAmount => fold;

        /// <summary>1 = rover guardado no módulo (rodas recolhidas, mastro e antena dobrados); 0 = em operação.</summary>
        public void Fold(float amount)
        {
            fold = Mathf.Clamp01(amount);
            Apply();
        }

        /// <summary>
        /// Ângulos de suspensão em graus, + = frente para baixo. O diferencial é mecânico: o rocker direito sempre gira o oposto do esquerdo.
        /// </summary>
        public void Suspend(float rockerDegrees, float bogieLeftDegrees, float bogieRightDegrees)
        {
            rocker = rockerDegrees;
            bogieLeftAngle = bogieLeftDegrees;
            bogieRightAngle = bogieRightDegrees;
            Apply();
        }

        void Apply()
        {
            float r = Mathf.Lerp(rocker, StowedRockerDegrees, fold);
            rockerLeft.localRotation = Quaternion.Euler(r, 0, 0);
            rockerRight.localRotation = Quaternion.Euler(-r, 0, 0);
            bogieLeft.localRotation = Quaternion.Euler(Mathf.Lerp(bogieLeftAngle, StowedBogieDegrees, fold), 0, 0);
            bogieRight.localRotation = Quaternion.Euler(Mathf.Lerp(bogieRightAngle, StowedBogieDegrees, fold), 0, 0);
            mastHead.localRotation = Quaternion.Euler(StowedMastDegrees * fold, 0, 0);
            dish.localRotation = Quaternion.Euler(StowedDishDegrees * fold, 0, 0);
        }
    }
}
