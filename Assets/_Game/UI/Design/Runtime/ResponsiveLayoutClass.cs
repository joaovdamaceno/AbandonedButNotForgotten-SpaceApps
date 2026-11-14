using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// USS não tem media queries. Este componente aplica a classe <see cref="NarrowClass"/> à raiz do documento
    /// quando a largura LÓGICA do painel fica abaixo do limite (4:3 → 960 px lógicos com a escala por altura).
    /// Reage a GeometryChangedEvent: sem Update, sem alocação por quadro.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ResponsiveLayoutClass : MonoBehaviour
    {
        public const string NarrowClass = "sa-layout--narrow";
        public const float DefaultNarrowBelowLogicalWidth = 1100f;

        [SerializeField] UIDocument document;
        [SerializeField, Min(1f)] float narrowBelowLogicalWidth = DefaultNarrowBelowLogicalWidth;

        VisualElement root;

        public static bool IsNarrow(float logicalWidth, float narrowBelow) =>
            logicalWidth > 0f && logicalWidth < narrowBelow;

        void Reset() => document = GetComponent<UIDocument>();

        void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;

            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Apply(root.layout.width);
        }

        void OnDisable()
        {
            root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root = null;
        }

        void OnGeometryChanged(GeometryChangedEvent evt) => Apply(evt.newRect.width);

        void Apply(float logicalWidth)
        {
            if (root == null || float.IsNaN(logicalWidth) || logicalWidth <= 0f)
                return;

            root.EnableInClassList(NarrowClass, IsNarrow(logicalWidth, narrowBelowLogicalWidth));
        }
    }
}
