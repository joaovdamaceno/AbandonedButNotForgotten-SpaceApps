using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>
    /// Preview isolado da SUR-009 para capturas de QA (Editor e WebGL). Sem entrada de jogo: a pose vem da URL
    /// (<c>?dig=0.5</c> fração da escavação, <c>?retract=0.3</c> fração do recolhimento, <c>&amp;cam=low|tv|top|side</c>).
    /// Não faz parte dos prefabs do capítulo.
    /// </summary>
    public sealed class SurveyorScoopWebPreview : MonoBehaviour
    {
        [SerializeField] SurveyorScoop scoop;
        [SerializeField] GameObject trench, rig, site, visibleRig;
        [SerializeField] Camera previewCamera;
        [SerializeField] float defaultDig = 1f;
        public string Report { get; private set; } = "waiting";

        public void Configure(SurveyorScoop controller, GameObject trenchInstance, GameObject rigInstance, GameObject siteInstance, GameObject visibleRigInstance, Camera camera)
        { scoop = controller; trench = trenchInstance; rig = rigInstance; site = siteInstance; visibleRig = visibleRigInstance; previewCamera = camera; }

        static string Query(string key, string fallback)
        {
            string url = Application.absoluteURL ?? string.Empty;
            int q = url.IndexOf('?');
            if (q < 0) return fallback;
            foreach (var part in url.Substring(q + 1).Split('&'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && kv[0] == key) return kv[1];
            }
            return fallback;
        }

        void Start()
        {
            Apply();
            if (Query("auto", "0") == "1") StartCoroutine(CaptureAll());
        }

        /// <summary>QA WebGL: captura o canvas real a cada 10 % e envia por POST a <c>/save?name=</c> do servidor local de QA.</summary>
        IEnumerator CaptureAll()
        {
            string url = Application.absoluteURL ?? string.Empty;
            int q = url.IndexOf('?');
            string origin = q >= 0 ? url.Substring(0, q) : url;
            if (!origin.EndsWith("/")) origin += "/";
            foreach (var cam in new[] { "low", "tv" })
                for (var i = 0; i <= 10; i++)
                    foreach (var retract in new[] { false, true })
                    {
                        float f = i / 10f;
                        ApplyState(retract ? 1f : f, retract ? f : -1f, cam);
                        yield return null; yield return null;
                        yield return new WaitForEndOfFrame();
                        var texture = ScreenCapture.CaptureScreenshotAsTexture();
                        string name = (retract ? "retract" : "dig") + "-" + (i * 10).ToString("000") + "-" + cam + ".png";
                        using (var request = UnityWebRequest.Put(origin + "save?name=" + name, texture.EncodeToPNG()))
                        {
                            yield return request.SendWebRequest();
                            if (request.result != UnityWebRequest.Result.Success) Debug.LogError("SUR-009 save falhou: " + request.error);
                        }
                        Destroy(texture);
                        using (var log = UnityWebRequest.Put(origin + "save?name=" + name + ".txt", System.Text.Encoding.UTF8.GetBytes(Report))) yield return log.SendWebRequest();
                    }
            Report = "capture-done";
            Debug.Log("SUR-009 capture-done");
        }

        public void Apply()
        {
            float.TryParse(Query("dig", defaultDig.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dig);
            float retract = -1f;
            string retractText = Query("retract", "");
            if (retractText.Length > 0) float.TryParse(retractText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out retract);
            ApplyState(dig, retract, Query("cam", "low"));
        }

        public void ApplyState(float dig, float retract, string cam)
        {
            var ground = FindGround(trench.transform.Find(SurveyorScoop.PhotoTargetName).position);
            var native = new System.Collections.Generic.List<Renderer>();
            foreach (var r in visibleRig.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Team_ScoopLinkage" || r.name == "Team_ScoopTool") native.Add(r);
            scoop.ConfigurePreview(trench, rig, ground != null ? new[] { ground } : Array.Empty<Renderer>(), native.ToArray());
            string state;
            if (retract >= 0f)
            { scoop.PreviewRetract(retract); state = "retract=" + retract.ToString("0.00"); }
            else { scoop.PreviewDig(Mathf.Clamp01(dig) * SurveyorScoop.TotalDigSeconds); state = "dig=" + dig.ToString("0.00"); }
            PlaceCamera(cam);
            Report = state + " cam=" + cam + " depth=" + scoop.DepthMeters.ToString("0.000") + " clearance=" + scoop.TipClearanceMeters.ToString("0.0000") + " reach=" + scoop.ReachErrorMeters.ToString("0.0000") + " ground=" + (ground != null ? ground.name : "none");
            Debug.Log("SUR-009 preview " + Report);
        }

        Renderer FindGround(Vector3 point)
        {
            Renderer best = null;
            foreach (var r in site.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.transform.parent == null || r.transform.parent.name != "NearGround_40m") continue;
                var b = r.bounds;
                if (point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z) best = r;
            }
            return best;
        }

        void PlaceCamera(string mode)
        {
            var center = scoop.TrenchCenter.position;
            var root = scoop.TrenchRoot;
            Vector3 along = root.right, across = root.forward;
            Vector3 position; float fov = 30f;
            switch (mode)
            {
                case "tv":
                    var mount = visibleRig.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount");
                    position = mount != null ? mount.position : center + Vector3.up * 2f; fov = 18f; break;
                case "top": position = center + root.up * 2.2f - across * .3f; fov = 28f; break;
                case "side": position = center - across * 1.6f + root.up * .22f; fov = 32f; break;
                default: position = center - across * 1.5f - along * .9f + root.up * .28f; break; // ângulo baixo
            }
            var cam = previewCamera.transform;
            cam.position = position;
            cam.rotation = Quaternion.LookRotation(center + root.up * (mode == "tv" ? -.05f : -.04f) - position, root.up);
            previewCamera.fieldOfView = fov; previewCamera.nearClipPlane = .02f;
        }
    }
}
