using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Saída TV 640×480 da cena isolada. SUR-007 pode ocultar DrawToGameView e consumir Texture.</summary>
    public sealed class SurveyorSiteViewport : MonoBehaviour
    {
        [SerializeField] Camera televisionCamera;
        [SerializeField] bool drawToGameView = true;
        RenderTexture texture;
        RenderTexture previousTarget;
        float previousAspect;
        public RenderTexture Texture => texture;
        public bool DrawToGameView { get => drawToGameView; set => drawToGameView = value; }

        public void Configure(Camera camera) => televisionCamera = camera;

        void OnEnable()
        {
            if (!Application.isPlaying || televisionCamera == null) return;
            previousTarget = televisionCamera.targetTexture;
            previousAspect = televisionCamera.aspect;
            texture = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGB32)
                { name = "Surveyor TV 640x480", useMipMap = false, antiAliasing = 1 };
            texture.Create();
            televisionCamera.targetTexture = texture;
            televisionCamera.aspect = 4f / 3f;
        }

        void OnGUI()
        {
            if (!drawToGameView || texture == null || Event.current.type != EventType.Repaint) return;
            float height = Mathf.Min(Screen.height, Screen.width * .75f), width = height * 4f / 3f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
            GUI.DrawTexture(new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height), texture, ScaleMode.StretchToFill);
        }

        void OnDisable()
        {
            if (texture == null) return;
            if (televisionCamera != null && televisionCamera.targetTexture == texture)
            {
                televisionCamera.targetTexture = previousTarget;
                televisionCamera.aspect = previousAspect;
            }
            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }
}
