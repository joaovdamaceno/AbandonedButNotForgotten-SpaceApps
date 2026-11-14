using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>CORE-003: o roteador entrega a entrada por contexto e nunca vaza comandos entre estados.</summary>
    public class InputRouterTests
    {
        GameObject host;
        InputRouter router;
        List<string> log;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("router");
            router = host.AddComponent<InputRouter>();
            log = new List<string>();
            router.InteractDown += () => log.Add("down");
            router.InteractUp += () => log.Add("up");
            router.PauseDown += () => log.Add("pause");
            router.HelpDown += () => log.Add("help");
            router.SubmitDown += () => log.Add("submit");
            router.NavigateDown += d => log.Add($"nav:{d.x},{d.y}");
            router.InputCleared += () => log.Add("cleared");
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void DefaultContextIsPanelSoGameplayInputIsOffUntilAcquired()
        {
            Assert.That(router.Context, Is.EqualTo(InputContext.Panel));
            router.FeedMove(Vector2.right);
            Assert.That(router.Move, Is.EqualTo(Vector2.zero));

            using (router.Acquire(InputContext.Gameplay, "test"))
            {
                router.FeedMove(Vector2.right);
                Assert.That(router.Move, Is.EqualTo(Vector2.right));
            }

            Assert.That(router.Context, Is.EqualTo(InputContext.Panel));
            Assert.That(router.Move, Is.EqualTo(Vector2.zero), "sair do contexto zera o movimento");
        }

        [Test]
        public void EDownAndUpAreDeliveredOnceAndHeldDoesNotRepeat()
        {
            router.FeedInteract(true);
            router.FeedInteract(true);
            router.FeedInteract(true);
            Assert.That(router.InteractHeld, Is.True);
            router.FeedInteract(false);
            router.FeedInteract(false);

            Assert.That(log, Is.EqualTo(new[] { "down", "up" }));
            Assert.That(router.InteractHeld, Is.False);
        }

        [Test]
        public void EHeldAcrossAContextChangeNeverStartsAnythingInTheNewContext()
        {
            using (router.Acquire(InputContext.Gameplay, "game"))
            {
                router.FeedInteract(true);
                Assert.That(log, Is.EqualTo(new[] { "down" }));

                // Abre um painel/cinematic com E ainda pressionado.
                var cinematic = router.Acquire(InputContext.Cinematic, "cutscene");
                Assert.That(log, Is.EqualTo(new[] { "down", "up" }), "E deixa de valer ao trocar de contexto");

                cinematic.Dispose();
                Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay));
                Assert.That(router.InteractHeld, Is.False, "E mantido na saída da cinematic não retoma nada");

                router.FeedInteract(false);
                Assert.That(log, Is.EqualTo(new[] { "down", "up" }), "soltar depois da supressão não gera evento");

                router.FeedInteract(true);
                Assert.That(log, Is.EqualTo(new[] { "down", "up", "down" }), "novo pressionamento volta a valer");
            }
        }

        [Test]
        public void EPressedDuringACinematicIsIgnored()
        {
            using (router.Acquire(InputContext.Cinematic, "cutscene"))
            {
                router.FeedInteract(true);
                router.FeedInteract(false);
            }

            Assert.That(log, Is.Empty);
        }

        [Test]
        public void LeasesStackAndDisposeSafelyOutOfOrderAndTwice()
        {
            var game = router.Acquire(InputContext.Gameplay, "game");
            var pause = router.Acquire(InputContext.Paused, "pause");
            Assert.That(router.Context, Is.EqualTo(InputContext.Paused));
            Assert.That(router.DescribeLeases(), Is.EqualTo(new[] { "Gameplay:game", "Paused:pause" }));

            game.Dispose();
            Assert.That(router.Context, Is.EqualTo(InputContext.Paused), "o último pedido continua valendo");
            pause.Dispose();
            pause.Dispose();
            Assert.That(router.Context, Is.EqualTo(InputContext.Panel));
            Assert.That(router.DescribeLeases(), Is.Empty);
        }

        [Test]
        public void AcquireRequiresAnOwnerLabel() =>
            Assert.Throws<ArgumentException>(() => router.Acquire(InputContext.Gameplay, " "));

        [Test]
        public void EachActionIsDeliveredOnlyToTheContextsThatAcceptIt()
        {
            void PressAll()
            {
                router.FeedPause();
                router.FeedHelp();
                router.FeedSubmit();
                router.FeedNavigate(Vector2.up);
            }

            using (router.Acquire(InputContext.Gameplay, "g"))
            {
                PressAll();
                Assert.That(log, Is.EqualTo(new[] { "pause", "help" }));
                log.Clear();
            }

            using (router.Acquire(InputContext.Cinematic, "c"))
            {
                PressAll();
                Assert.That(log, Is.EqualTo(new[] { "pause", "submit" }));
                log.Clear();
            }

            using (router.Acquire(InputContext.Paused, "p"))
            {
                PressAll();
                Assert.That(log, Is.EqualTo(new[] { "pause", "submit", "nav:0,1" }));
                log.Clear();
            }

            PressAll();
            Assert.That(log, Is.EqualTo(new[] { "pause", "submit", "nav:0,1" }), "Panel: navegação e confirmação, sem ajuda");
        }

        [Test]
        public void NavigationIgnoresNeutralAndRoundsDiagonals()
        {
            router.FeedNavigate(Vector2.zero);
            router.FeedNavigate(new Vector2(0.1f, -0.2f));
            router.FeedNavigate(new Vector2(0.707f, 0.707f));
            router.FeedNavigate(Vector2.left);
            Assert.That(log, Is.EqualTo(new[] { "nav:1,1", "nav:-1,0" }));
        }

        [Test]
        public void FocusLossReleasesEverythingAndLeavesNoPhantomKey()
        {
            using (router.Acquire(InputContext.Gameplay, "g"))
            {
                router.FeedMove(Vector2.up);
                router.FeedInteract(true);
                log.Clear();

                router.NotifyFocusLost();

                Assert.That(log, Is.EqualTo(new[] { "up", "cleared" }));
                Assert.That(router.Move, Is.EqualTo(Vector2.zero));
                Assert.That(router.InteractHeld, Is.False);
                Assert.That(router.IsInteractPhysicallyHeld, Is.False);

                router.FeedInteract(false);
                Assert.That(log, Is.EqualTo(new[] { "up", "cleared" }), "o soltar que chega depois não duplica o evento");
                router.FeedInteract(true);
                Assert.That(log[log.Count - 1], Is.EqualTo("down"));
            }
        }

        [Test]
        public void LookIsPerFrameAndOnlyInGameplay()
        {
            router.FeedLook(Vector2.one);
            Assert.That(router.Look, Is.EqualTo(Vector2.zero));

            using (router.Acquire(InputContext.Gameplay, "g"))
            {
                router.FeedLook(new Vector2(1f, 2f));
                router.FeedLook(new Vector2(1f, 0f));
                Assert.That(router.Look, Is.EqualTo(new Vector2(2f, 2f)));
                router.EndFrame();
                Assert.That(router.Look, Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void DisablingReleasesTheLogicalKey()
        {
            router.Enable();
            using (router.Acquire(InputContext.Gameplay, "g"))
            {
                router.FeedInteract(true);
                log.Clear();
                router.Disable();
                Assert.That(log, Is.EqualTo(new[] { "up", "cleared" }));
                router.Disable();
                Assert.That(log.Count, Is.EqualTo(2), "desabilitar duas vezes é inofensivo");
            }
        }
    }
}
