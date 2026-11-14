using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Objetivo principal do HUD (UI-003): placa com verbo e objeto, âncora no mundo quando o alvo está no quadro e seta
    /// de borda quando está fora, para ninguém precisar procurar um pixel. Mostra UM alvo por vez: o candidato
    /// disponível do <see cref="IInteractionService"/> (é nele que o E vai agir) ou, se não houver, o objetivo.
    /// A distância vem do candidato quando ele é o alvo do objetivo (a mesma que decide o E); senão é medida da
    /// referência até a âncora. As regras de projeção estão em <see cref="EdgeCueMath"/>.
    /// </summary>
    public sealed class ObjectiveIndicator
    {
        const float PositionTolerance = 0.0005f;
        const float AngleTolerance = 0.25f;

        static readonly string[] SideClasses = { null, "sa-edge--left", "sa-edge--right", "sa-edge--top", "sa-edge--bottom" };

        readonly VisualElement accentRoot;
        readonly VisualElement plate;
        readonly Label stamp;
        readonly Label label;
        readonly Label meta;
        readonly VisualElement marker;
        readonly Label markerLabel;
        readonly VisualElement edge;
        readonly VisualElement edgeArrow;
        readonly Label edgeLabel;

        HudObjective objective;
        bool hasObjective;
        string accentOnRoot;

        bool plateShown;
        bool plateKnown;
        bool markerShown;
        bool markerKnown;
        bool edgeShown;
        bool edgeKnown;
        string renderedStamp;
        string renderedLabel;
        string renderedMeta;
        string renderedMarkerLabel;
        string renderedEdgeLabel;
        EdgeSide renderedSide;
        float markerX = -1f;
        float markerY = -1f;
        float edgeX = -1f;
        float edgeY = -1f;
        float edgeAngle = -1f;

        // Cache dos textos: só recalcula quando objetivo, idioma, tabela ou distância (já arredondada) mudam.
        ObjectiveView cachedView = ObjectiveView.Hidden;
        string cachedLanguage;
        int cachedTextCount = -1;
        int cachedDistanceKey = -2;
        bool cachedValid;
        string cachedCueText = string.Empty;
        string cachedCueName;
        int cachedCueKey = -2;
        string cachedCueLanguage;

        public ObjectiveIndicator(VisualElement root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            // Q() só procura descendentes: quando o chamador já entrega o próprio hud-root (como o MissionHUD), ele é a raiz de acento.
            accentRoot = root.name == "hud-root" ? root : Require<VisualElement>(root, "hud-root");
            plate = Require<VisualElement>(root, "objective");
            stamp = Require<Label>(root, "objective-stamp");
            label = Require<Label>(root, "objective-label");
            meta = Require<Label>(root, "objective-meta");
            marker = Require<VisualElement>(root, "anchor-marker");
            markerLabel = Require<Label>(root, "anchor-label");
            edge = Require<VisualElement>(root, "edge-cue");
            edgeArrow = Require<VisualElement>(root, "edge-arrow");
            edgeLabel = Require<Label>(root, "edge-label");
            SetPlate(false);
            SetMarker(false);
            SetEdge(false);
        }

        public bool HasObjective => hasObjective;
        public string ObjectiveId => hasObjective ? objective.Id : null;

        /// <summary>A placa do objetivo está desenhada.</summary>
        public bool IsPlateShown => plateShown;

        /// <summary>A âncora no mundo está desenhada (alvo dentro do retângulo interno).</summary>
        public bool IsMarkerShown => markerShown;

        /// <summary>A seta de borda está desenhada (alvo fora do quadro).</summary>
        public bool IsEdgeShown => edgeShown;

        public OrientationCue LastCue { get; private set; } = OrientationCue.Invalid;

        public void SetObjective(in HudObjective value)
        {
            var errors = value.Validate();
            if (errors.Count > 0)
                throw new ArgumentException("Objetivo inválido:" + ContractValidation.Describe(errors));

            objective = value;
            hasObjective = true;
            cachedValid = false;
            if (!string.IsNullOrEmpty(value.AccentClass) && value.AccentClass != accentOnRoot)
            {
                if (accentOnRoot != null)
                    accentRoot.RemoveFromClassList(accentOnRoot);

                accentRoot.AddToClassList(value.AccentClass);
                accentOnRoot = value.AccentClass;
            }
        }

        public void Clear()
        {
            objective = default;
            hasObjective = false;
            renderedStamp = null;
            renderedLabel = null;
            renderedMeta = null;
            cachedValid = false;
            cachedView = ObjectiveView.Hidden;
            cachedCueKey = -2;
            LastCue = OrientationCue.Invalid;
        }

        /// <summary>
        /// Atualiza texto, âncora e seta. Chamar depois de a câmera se mover no quadro (LateUpdate). Só escreve na interface
        /// quando algo visível muda.
        /// </summary>
        public void Tick(Camera camera, Transform reference, bool candidateActive, in InteractionCandidate candidate,
            HudLayers layers, DossierTextCatalog texts, string language)
        {
            var objectiveShown = layers.Objective && hasObjective;
            SetPlate(objectiveShown);

            var activeIsObjective = candidateActive && hasObjective && !string.IsNullOrEmpty(objective.CandidateId) && candidate.Id == objective.CandidateId;
            var objectiveDistance = float.NaN;
            if (activeIsObjective)
                objectiveDistance = candidate.DistanceMeters;
            else if (hasObjective && reference != null && objective.Anchor != null)
                objectiveDistance = Vector3.Distance(reference.position, objective.Anchor.position);

            var distanceKey = HudFormat.DistanceKey(objectiveDistance);
            var textCount = texts != null ? texts.Count : 0;
            if (!cachedValid || distanceKey != cachedDistanceKey || language != cachedLanguage || textCount != cachedTextCount)
            {
                cachedView = hasObjective
                    ? ObjectiveViewBuilder.Build(objective.ToData(), HudFormat.Distance(objectiveDistance, language), texts, language)
                    : ObjectiveView.Hidden;
                cachedDistanceKey = distanceKey;
                cachedLanguage = language;
                cachedTextCount = textCount;
                cachedValid = true;
            }

            var view = cachedView;
            if (objectiveShown)
                RenderPlate(view);

            if (!layers.Orientation || camera == null)
            {
                SetMarker(false);
                SetEdge(false);
                LastCue = OrientationCue.Invalid;
                return;
            }

            // Um alvo só: onde o E vai agir (candidato disponível com âncora) ou, se não houver, o objetivo.
            Transform anchor = null;
            var targetName = string.Empty;
            var targetDistance = float.NaN;
            if (candidateActive && candidate.Available && candidate.Anchor != null)
            {
                anchor = candidate.Anchor;
                targetDistance = candidate.DistanceMeters;
                if (activeIsObjective)
                    targetName = view.TargetName;
            }
            else if (hasObjective && objective.Anchor != null)
            {
                anchor = objective.Anchor;
                targetDistance = objectiveDistance;
                targetName = view.TargetName;
            }

            if (anchor == null)
            {
                SetMarker(false);
                SetEdge(false);
                LastCue = OrientationCue.Invalid;
                return;
            }

            var viewport = camera.WorldToViewportPoint(anchor.position);
            var cue = EdgeCueMath.Compute(viewport.x, viewport.y, viewport.z);
            LastCue = cue;
            var cueKey = HudFormat.DistanceKey(targetDistance);
            if (cueKey != cachedCueKey || targetName != cachedCueName || language != cachedCueLanguage)
            {
                cachedCueText = HudFormat.Compose(targetName, HudFormat.Distance(targetDistance, language));
                cachedCueKey = cueKey;
                cachedCueName = targetName;
                cachedCueLanguage = language;
            }

            var cueText = cachedCueText;
            if (!cue.Valid)
            {
                SetMarker(false);
                SetEdge(false);
            }
            else if (cue.OnScreen)
            {
                SetEdge(false);
                SetMarker(true);
                RenderMarker(cue, cueText);
            }
            else
            {
                SetMarker(false);
                SetEdge(true);
                RenderEdge(cue, cueText);
            }
        }

        void RenderPlate(in ObjectiveView view)
        {
            if (view.Stamp != renderedStamp)
            {
                stamp.text = view.Stamp;
                stamp.style.display = view.Stamp.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                renderedStamp = view.Stamp;
            }

            if (view.Label != renderedLabel)
            {
                label.text = view.Label;
                renderedLabel = view.Label;
            }

            if (view.Meta != renderedMeta)
            {
                meta.text = view.Meta;
                meta.style.display = view.Meta.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                renderedMeta = view.Meta;
            }
        }

        void RenderMarker(in OrientationCue cue, string text)
        {
            if (Math.Abs(cue.X - markerX) > PositionTolerance || Math.Abs(cue.Y - markerY) > PositionTolerance)
            {
                marker.style.left = new Length(cue.X * 100f, LengthUnit.Percent);
                marker.style.top = new Length(cue.Y * 100f, LengthUnit.Percent);
                markerX = cue.X;
                markerY = cue.Y;
            }

            if (text != renderedMarkerLabel)
            {
                markerLabel.text = text;
                markerLabel.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                renderedMarkerLabel = text;
            }
        }

        void RenderEdge(in OrientationCue cue, string text)
        {
            if (cue.Side != renderedSide)
            {
                var previous = SideClasses[(int)renderedSide];
                if (previous != null)
                    edge.RemoveFromClassList(previous);

                var next = SideClasses[(int)cue.Side];
                if (next != null)
                    edge.AddToClassList(next);

                renderedSide = cue.Side;
            }

            if (Math.Abs(cue.X - edgeX) > PositionTolerance || Math.Abs(cue.Y - edgeY) > PositionTolerance)
            {
                edge.style.left = new Length(cue.X * 100f, LengthUnit.Percent);
                edge.style.top = new Length(cue.Y * 100f, LengthUnit.Percent);
                edgeX = cue.X;
                edgeY = cue.Y;
            }

            if (Math.Abs(cue.AngleDegrees - edgeAngle) > AngleTolerance)
            {
                // A seta do sistema visual aponta para a direita com 45°; somar a direção do alvo.
                edgeArrow.style.rotate = new Rotate(new Angle(45f + cue.AngleDegrees, AngleUnit.Degree));
                edgeAngle = cue.AngleDegrees;
            }

            if (text != renderedEdgeLabel)
            {
                edgeLabel.text = text;
                edgeLabel.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                renderedEdgeLabel = text;
            }
        }

        void SetPlate(bool visible)
        {
            if (plateKnown && plateShown == visible)
                return;

            plateKnown = true;
            plateShown = visible;
            plate.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SetMarker(bool visible)
        {
            if (markerKnown && markerShown == visible)
                return;

            markerKnown = true;
            markerShown = visible;
            marker.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SetEdge(bool visible)
        {
            if (edgeKnown && edgeShown == visible)
                return;

            edgeKnown = true;
            edgeShown = visible;
            edge.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException($"ObjectiveIndicator: o UXML não tem o elemento '{name}' ({typeof(T).Name}).");

            return element;
        }
    }
}
