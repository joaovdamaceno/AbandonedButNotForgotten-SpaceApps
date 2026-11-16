using System.IO;
using System.Linq;
using Abandoned.SpaceApps.Opportunity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Prompt de segurar E do módulo Spirit and Opportunity: a barra mostra exatamente o progresso informado, a tecla E tem
    /// estados distintos (segurando, esperando, concluído), a confirmação de conclusão expira sozinha e o prompt vai para o
    /// lugar certo (alto à direita no lugar das dicas, ou canto inferior esquerdo). Também confere os textos novos.
    /// Não avalia o visual renderizado: aparência e sobreposição precisam ser vistas no Editor e no navegador.
    /// </summary>
    public class MarsHoldPromptTests
    {
        const string LocalizationPath = "Assets/_Game/Chapters/Mars/Opportunity/Resources/Localization.json";
        const float Frame = 0.05f;

        static float FillPercent(HoldPrompt prompt) => prompt.Root.Q(className: "hold-fill").style.width.value.value;

        static bool Has(HoldPrompt prompt, string state) => prompt.Root.ClassListContains("hold-prompt--" + state);

        static Abandoned.SpaceApps.Opportunity.Localization.Entry Entry(string key)
        {
            var table = JsonUtility.FromJson<Abandoned.SpaceApps.Opportunity.Localization.Table>(File.ReadAllText(LocalizationPath));
            return table.entries.FirstOrDefault(e => e.key == key);
        }

        [Test]
        public void StartsHiddenAndShowsOnlyWhileAnActionIsActive()
        {
            var prompt = new HoldPrompt();
            Assert.IsFalse(prompt.Visible);

            prompt.Apply(true, "Observar", 0f, false, false, Frame);
            Assert.IsTrue(prompt.Visible);

            prompt.Apply(false, null, 0f, false, false, Frame);
            Assert.IsFalse(prompt.Visible);
        }

        [Test]
        public void ProgressBarMatchesTheProgressItIsGiven()
        {
            var prompt = new HoldPrompt();
            foreach (var progress in new[] { 0f, 0.25f, 0.4f, 0.999f, 1f })
            {
                prompt.Apply(true, "Observar", progress, true, false, Frame);
                Assert.AreEqual(progress * 100f, FillPercent(prompt), 0.1f, "A barra precisa refletir o progresso real da ação.");
            }

            prompt.Apply(true, "Observar", 7f, true, false, Frame);
            Assert.AreEqual(100f, FillPercent(prompt), 0.1f, "Fora de 0–1 a barra é limitada.");
        }

        [Test]
        public void KeyStatesAreDistinct()
        {
            var prompt = new HoldPrompt();
            prompt.Apply(true, "Observar", 0.3f, true, false, Frame);
            Assert.IsTrue(Has(prompt, "holding"));
            Assert.IsFalse(Has(prompt, "idle") || Has(prompt, "waiting") || Has(prompt, "done"));

            prompt.Apply(true, "Observar", 0.3f, false, false, Frame);
            Assert.IsTrue(Has(prompt, "idle"));
            Assert.IsFalse(Has(prompt, "holding"));

            prompt.Apply(true, "Aguarde", 0f, false, true, Frame);
            Assert.IsTrue(Has(prompt, "waiting"));
        }

        [Test]
        public void CompletionIsShownThenExpiresEvenAfterTheActionEnds()
        {
            var prompt = new HoldPrompt();
            prompt.Apply(true, "Observar", 0.9f, true, false, Frame);
            prompt.Complete("Observação registrada");

            // A ação já terminou (tela fechou): a confirmação continua visível, cheia e em verde.
            prompt.Apply(false, null, 0f, false, false, Frame);
            Assert.IsTrue(prompt.Visible);
            Assert.IsTrue(prompt.ShowingDone);
            Assert.IsTrue(Has(prompt, "done"));
            Assert.AreEqual(100f, FillPercent(prompt), 0.1f);
            Assert.AreEqual("Observação registrada", prompt.Root.Q<Label>(className: "hold-text").text);

            prompt.Apply(false, null, 0f, false, false, HoldPrompt.DoneSeconds + Frame);
            Assert.IsFalse(prompt.ShowingDone);
            Assert.IsFalse(prompt.Visible);
        }

        [Test]
        public void ANewActionAdvancingReplacesThePreviousConfirmation()
        {
            var prompt = new HoldPrompt();
            prompt.Complete("Leitura registrada");
            prompt.Apply(true, "Examinar a cavidade", 0.5f, true, false, Frame);

            Assert.IsFalse(prompt.ShowingDone);
            Assert.IsFalse(Has(prompt, "done"));
            Assert.AreEqual(50f, FillPercent(prompt), 0.1f);
        }

        [Test]
        public void CancelHidesImmediately()
        {
            var prompt = new HoldPrompt();
            prompt.Complete("Foto registrada");
            prompt.Apply(false, null, 0f, false, false, Frame);
            Assert.IsTrue(prompt.Visible);

            prompt.Cancel();
            Assert.IsFalse(prompt.Visible);
            Assert.IsFalse(prompt.ShowingDone);
        }

        [Test]
        public void PlacementPutsThePromptInTheHintStackOrInTheCorner()
        {
            var root = new VisualElement();
            var before = new VisualElement();
            var stack = new VisualElement();
            var after = new VisualElement();
            root.Add(before);
            root.Add(stack);
            root.Add(after);
            stack.Add(new VisualElement());

            var prompt = new HoldPrompt();
            prompt.Place(HoldPrompt.Placement.Hint, stack);
            Assert.AreSame(stack, prompt.Root.parent, "Esferas de hematita: o prompt toma o lugar das dicas.");
            Assert.AreEqual(0, stack.IndexOf(prompt.Root));
            Assert.IsTrue(prompt.Root.ClassListContains("hold-prompt--hint"));
            Assert.IsFalse(prompt.Root.ClassListContains("hold-prompt--corner"));

            prompt.Place(HoldPrompt.Placement.Corner, stack);
            Assert.AreSame(root, prompt.Root.parent);
            Assert.AreEqual(root.IndexOf(stack) + 1, root.IndexOf(prompt.Root), "Canto inferior esquerdo, logo depois da pilha.");
            Assert.IsTrue(prompt.Root.ClassListContains("hold-prompt--corner"));
            Assert.IsFalse(prompt.Root.ClassListContains("hold-prompt--hint"));
        }

        [Test]
        public void KeyIsDrawnAsAKeyboardButtonWithTheLetterE()
        {
            var prompt = new HoldPrompt();
            var key = prompt.Root.Q<Label>(className: "hold-key");
            Assert.IsNotNull(key);
            Assert.AreEqual("E", key.text);
            Assert.IsNotNull(prompt.Root.Q(className: "hold-track"));
            Assert.IsNotNull(prompt.Root.Q(className: "hold-check"), "Existe o visto de conclusão.");
        }

        [Test]
        public void SecondSunComparisonSaysTheSunIsSlightlyObscuredByDust()
        {
            var entry = Entry("v10.result.sundust");
            Assert.IsNotNull(entry);
            StringAssert.StartsWith("The Sun is slightly obscured by dust.", entry.en);
            StringAssert.StartsWith("O Sol está levemente encoberto pela poeira.", entry.pt);
        }

        [Test]
        public void ConfirmationTextsExistInBothLanguages()
        {
            foreach (var key in new[] { "v18.hold.done", "v18.done.observation", "v18.done.photo", "v18.done.grind", "v18.done.reading" })
            {
                var entry = Entry(key);
                Assert.IsNotNull(entry, key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.pt), key + " (pt)");
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.en), key + " (en)");
            }
        }
    }
}
