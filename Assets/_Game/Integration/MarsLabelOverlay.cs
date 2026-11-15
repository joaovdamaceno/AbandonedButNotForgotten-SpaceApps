using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Integration
{
    /// <summary>
    /// Desenha os letreiros por chave dos palcos de Marte (abertura, pouso, Spirit, memorial). Os diretores só dizem quais chaves estão visíveis e onde
    /// ancorar; o texto vem do <see cref="LocalizationService"/>. Só reconstrói quando o conjunto visível muda.
    /// </summary>
    public sealed class MarsLabelOverlay : IDisposable
    {
        sealed class Entry
        {
            public VisualElement Label;
            public MarsLandingLabelAnchor Anchor;
            /// <summary>Ponto do globo ao qual o letreiro fica preso (Meridiani, Gusev); nulo para letreiros de tela.</summary>
            public Transform Marker;
        }

        readonly VisualElement layer = new VisualElement { name = "mars-labels", pickingMode = PickingMode.Ignore };
        readonly Func<string, string> text;
        readonly List<Entry> entries = new List<Entry>();
        readonly List<(Action detach, int owner)> subscriptions = new List<(Action, int)>();
        readonly Dictionary<int, List<MarsLandingLabel>> visibleByOwner = new Dictionary<int, List<MarsLandingLabel>>();
        MarsOriginDirector origin;
        readonly Camera camera;
        readonly bool styled;
        readonly Action<string> titleChanged;
        string title;

        /// <param name="styledHost">Contêiner com as folhas de estilo do jogo (camada de legenda). Com ele, os nomes presos aos marcadores usam a
        /// placa e a fonte de carimbo do sistema visual, e os letreiros de tela viram o carimbo da legenda (<paramref name="titleChanged"/>),
        /// como nos outros capítulos. Sem ele, o desenho antigo vale como reserva.</param>
        public MarsLabelOverlay(VisualElement host, Func<string, string> textResolver, MarsChapterBindings bindings,
            VisualElement styledHost = null, Action<string> titleChanged = null)
        {
            text = textResolver;
            camera = bindings.SceneCamera;
            styled = styledHost != null;
            this.titleChanged = titleChanged;
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0f;
            if (styled)
                styledHost.Add(layer);
            else
                host.panel.visualTree.Add(layer);
            origin = UnityEngine.Object.FindFirstObjectByType<MarsOriginDirector>(FindObjectsInactive.Include);
            Watch(0, origin != null, h => origin.LabelsChanged += h, h => origin.LabelsChanged -= h);
            Watch(1, bindings.Landing != null, h => bindings.Landing.LabelsChanged += h, h => bindings.Landing.LabelsChanged -= h);
            Watch(2, bindings.Spirit != null, h => bindings.Spirit.LabelsChanged += h, h => bindings.Spirit.LabelsChanged -= h);
            Watch(3, bindings.Memorial != null, h => bindings.Memorial.LabelsChanged += h, h => bindings.Memorial.LabelsChanged -= h);
            layer.schedule.Execute(Track).Every(0);
        }

        /// <summary>Letreiros do globo acompanham o marcador: centrados acima dele e escondidos quando ele está do outro lado do planeta.</summary>
        void Track()
        {
            foreach (var entry in entries)
            {
                if (entry.Marker == null)
                    continue;

                var marker = entry.Marker;
                var visible = camera != null && marker.gameObject.activeInHierarchy;
                var viewport = Vector3.zero;
                if (visible)
                {
                    viewport = camera.WorldToViewportPoint(marker.position);
                    visible = viewport.z > 0f && Vector3.Dot(marker.up, camera.transform.position - marker.position) > 0f;
                }

                var s = entry.Label.style;
                s.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible)
                    continue;

                var rect = camera.rect;
                s.left = Length.Percent((rect.x + viewport.x * rect.width) * 100f);
                s.top = Length.Percent((1f - (rect.y + viewport.y * rect.height)) * 100f);
                s.translate = new Translate(Length.Percent(-50f), new Length(-150f, LengthUnit.Percent));
            }
        }

        void Watch(int owner, bool present, Action<Action<IReadOnlyList<MarsLandingLabel>>> attach, Action<Action<IReadOnlyList<MarsLandingLabel>>> detach)
        {
            if (!present)
                return;

            Action<IReadOnlyList<MarsLandingLabel>> handler = labels => Rebuild(owner, labels);
            attach(handler);
            subscriptions.Add((() => detach(handler), owner));
        }

        void Rebuild(int owner, IReadOnlyList<MarsLandingLabel> labels)
        {
            visibleByOwner[owner] = new List<MarsLandingLabel>(labels);
            foreach (var entry in entries)
                entry.Label.RemoveFromHierarchy();
            entries.Clear();
            string newTitle = null;
            foreach (var pair in visibleByOwner)
            {
                foreach (var label in pair.Value)
                {
                    var marker = origin != null ? origin.MarkerOf(label.TextKey) : null;
                    if (styled && titleChanged != null && marker == null)
                    {
                        // Letreiro de tela: vira o carimbo da legenda (mesmo formato de data/lugar das outras sondas).
                        newTitle ??= label.TextKey;
                        continue;
                    }

                    VisualElement element;
                    if (styled)
                    {
                        element = new VisualElement { pickingMode = PickingMode.Ignore };
                        element.AddToClassList("sa-plate");
                        var name = new Label(text(label.TextKey)) { pickingMode = PickingMode.Ignore };
                        name.AddToClassList("sa-stamp");
                        name.AddToClassList("sa-stamp--hud");
                        name.style.whiteSpace = WhiteSpace.NoWrap;
                        element.Add(name);
                    }
                    else
                    {
                        var plain = new Label(text(label.TextKey)) { pickingMode = PickingMode.Ignore };
                        plain.style.color = new Color(0.96f, 0.94f, 0.9f);
                        plain.style.fontSize = 26f;
                        plain.style.unityFontStyleAndWeight = FontStyle.Bold;
                        plain.style.whiteSpace = WhiteSpace.NoWrap;
                        element = plain;
                    }

                    element.style.position = Position.Absolute;
                    var entry = new Entry { Label = element, Anchor = label.Anchor, Marker = marker != null ? marker.transform.parent : null };
                    Place(entry);
                    if (entry.Marker != null)
                        element.style.display = DisplayStyle.None;
                    layer.Add(element);
                    entries.Add(entry);
                }
            }

            if (newTitle != title)
            {
                title = newTitle;
                titleChanged?.Invoke(title);
            }
        }

        static void Place(Entry entry)
        {
            var s = entry.Label.style;
            s.left = StyleKeyword.Null;
            s.right = StyleKeyword.Null;
            s.top = StyleKeyword.Null;
            s.bottom = StyleKeyword.Null;
            switch (entry.Anchor)
            {
                case MarsLandingLabelAnchor.LeftHalf:
                case MarsLandingLabelAnchor.UpperHalf:
                    s.left = Length.Percent(4f);
                    s.top = Length.Percent(entry.Anchor == MarsLandingLabelAnchor.UpperHalf ? 12f : 62f);
                    break;
                case MarsLandingLabelAnchor.RightHalf:
                case MarsLandingLabelAnchor.LowerHalf:
                    s.left = Length.Percent(entry.Anchor == MarsLandingLabelAnchor.RightHalf ? 54f : 4f);
                    s.top = Length.Percent(entry.Anchor == MarsLandingLabelAnchor.RightHalf ? 62f : 62f);
                    break;
                case MarsLandingLabelAnchor.Separator:
                    s.left = Length.Percent(4f);
                    s.top = Length.Percent(5f);
                    break;
                default:
                    s.left = Length.Percent(6.5f);
                    s.top = Length.Percent(6f);
                    break;
            }
        }

        public void Dispose()
        {
            foreach (var (detach, _) in subscriptions)
                detach();
            subscriptions.Clear();
            layer.RemoveFromHierarchy();
        }
    }
}
