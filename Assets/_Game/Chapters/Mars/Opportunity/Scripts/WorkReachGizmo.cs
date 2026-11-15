#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>
    /// Só no Editor: desenha na Scene view (fora do Play) se o ponto de abrasão da RAT está ao alcance do braço IDD quando o rover estaciona
    /// via <see cref="RoverController.WorkParking"/>. Verde = dentro do alcance seguro, amarelo = no limite, vermelho = fora.
    /// Ajuste o <c>workFrame</c> da rocha (ou <see cref="RoverController.WorkDistance"/>) até o ponto ficar verde.
    /// </summary>
    static class WorkReachGizmo
    {
        const float Reach = InstrumentArm.Reach, Safe = .74f;

        public static void Draw(RoverController rover, RockAbrasionMinigame science, Transform parking)
        {
            if (rover == null || science == null || science.surface == null || science.surface.workFrame == null || parking == null) return;
            InstrumentArm arm = rover.GetComponentInChildren<InstrumentArm>(true);
            if (arm == null || arm.Shoulder == null) return;

            Transform frame = science.surface.workFrame;
            Vector3 workPoint = frame.position;
            Vector3 park = rover.WorkParking(workPoint, parking.rotation);
            Vector3 shoulderLocal = rover.transform.InverseTransformPoint(arm.Shoulder.position);
            park.y = parking.position.y;
            Vector3 shoulder = Matrix4x4.TRS(park, parking.rotation, rover.transform.lossyScale).MultiplyPoint3x4(shoulderLocal);
            float distance = Vector3.Distance(shoulder, workPoint);
            Color color = distance <= Safe ? Color.green : distance <= Reach ? Color.yellow : Color.red;

            Gizmos.color = new Color(color.r, color.g, color.b, .35f);
            Gizmos.DrawWireSphere(shoulder, Reach);
            Gizmos.color = color;
            Gizmos.DrawLine(shoulder, workPoint);
            Gizmos.DrawWireSphere(workPoint, .04f);

            // Faixa de moagem (largura x profundidade da AbrasionSurface) em volta do workFrame.
            AbrasionSurface s = science.surface;
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(workPoint, frame.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(s.width, .005f, s.depth));
            Gizmos.matrix = Matrix4x4.TRS(park, parking.rotation, Vector3.one);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(0, .3f, 0), new Vector3(1.6f, .6f, 2.3f));
            Gizmos.matrix = old;

            Handles.Label(workPoint + Vector3.up * .25f, $"RAT: {distance:0.00} m do ombro | à frente {Vector3.Dot(workPoint - shoulder, parking.rotation * Vector3.forward):0.00} m | abaixo {shoulder.y - workPoint.y:0.00} m");
        }
    }
}
#endif
