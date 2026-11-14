using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>PIO-003 · Prévia dos anchors no Inspector: compõe o palco e alinha a Scene view à pose escolhida.</summary>
    [CustomEditor(typeof(PioneerPresentation))]
    public sealed class PioneerPresentationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var presentation = (PioneerPresentation)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prévia dos anchors (PIO-003)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("A prévia altera regiões, luzes e rotação na cena aberta. Reconstruir pelo builder (CORE-006) restaura o estado salvo.", MessageType.None);
            foreach (var anchor in presentation.Anchors)
            {
                if (anchor == null)
                    continue;

                var label = (anchor.IsStateAnchor ? "● " : "   ") + anchor.anchorId;
                if (GUILayout.Button(new GUIContent(label, anchor.function)))
                {
                    presentation.ShowAnchor(anchor.anchorId);
                    var view = SceneView.lastActiveSceneView;
                    if (view != null && anchor.pose != null)
                    {
                        view.cameraSettings.fieldOfView = anchor.fieldOfViewDegrees;
                        view.AlignViewToObject(anchor.pose);
                    }
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Verificar palco"))
            {
                var errors = presentation.ValidateSetup();
                errors.AddRange(PioneerStageChecks.Run(presentation).Errors);
                if (errors.Count == 0)
                    Debug.Log("PIO-003 · palco conforme.");
                else
                    Debug.LogError("PIO-003 · palco com problemas:\n- " + string.Join("\n- ", errors));
            }
        }
    }
}
