using UnityEngine;
using UnityEngine.Rendering;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Atmosfera do mapa importado da cratera Eagle: enquanto o mapa está ativo, liga o
    /// céu de Marte, a luz ambiente em três tons e as variáveis globais que o shader do terreno lê (densidade e cor da atmosfera, direção
    /// do Sol). Ao desligar, devolve o que encontrou. Sem alocação por quadro; a direção do Sol é lida da luz da cena a cada Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsClassicAtmosphere : MonoBehaviour
    {
        static readonly int AtmosphericDensity = Shader.PropertyToID("_AtmosphericDensity");
        static readonly int AtmosphericColor = Shader.PropertyToID("_AtmosphericColor");
        static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
        static readonly int DustLevel = Shader.PropertyToID("_DustLevel");

        [SerializeField] Material sky;
        [SerializeField, Min(0f)] float atmosphericDensity = 0.0018f;
        [SerializeField] Color atmosphericColor = new Color(0.43f, 0.33f, 0.25f);
        [SerializeField] Color ambientSky = new Color(0.38f, 0.43f, 0.5f);
        [SerializeField] Color ambientEquator = new Color(0.42f, 0.32f, 0.24f);
        [SerializeField] Color ambientGround = new Color(0.13f, 0.1f, 0.08f);

        Light sun;
        Material savedSkybox;
        AmbientMode savedAmbientMode;
        Color savedSky, savedEquator, savedGround;
        bool applied;

        public Material Sky => sky;

        void OnEnable()
        {
            Save();
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fog = false;
            Shader.SetGlobalFloat(AtmosphericDensity, atmosphericDensity);
            Shader.SetGlobalColor(AtmosphericColor, atmosphericColor);
            Shader.SetGlobalFloat(DustLevel, 0f);
            sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (light.type == LightType.Directional)
                    {
                        sun = light;
                        break;
                    }
                }
            }

            Aim();
        }

        void Update() => Aim();

        void Aim()
        {
            if (sun != null)
                Shader.SetGlobalVector(SunDirection, -sun.transform.forward);
        }

        void OnDisable()
        {
            if (!applied)
                return;

            applied = false;
            RenderSettings.skybox = savedSkybox;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientSkyColor = savedSky;
            RenderSettings.ambientEquatorColor = savedEquator;
            RenderSettings.ambientGroundColor = savedGround;
        }

        void Save()
        {
            if (applied)
                return;

            applied = true;
            savedSkybox = RenderSettings.skybox;
            savedAmbientMode = RenderSettings.ambientMode;
            savedSky = RenderSettings.ambientSkyColor;
            savedEquator = RenderSettings.ambientEquatorColor;
            savedGround = RenderSettings.ambientGroundColor;
        }
    }
}
