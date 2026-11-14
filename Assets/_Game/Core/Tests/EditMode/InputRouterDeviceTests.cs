using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>CORE-003: as ações reais do Input System (teclado virtual) chegam aos eventos do roteador.</summary>
    public class InputRouterDeviceTests : InputTestFixture
    {
        GameObject host;
        InputRouter router;
        Keyboard keyboard;
        List<string> log;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            host = new GameObject("router-device");
            router = host.AddComponent<InputRouter>();
            log = new List<string>();
            router.InteractDown += () => log.Add("down");
            router.InteractUp += () => log.Add("up");
            router.PauseDown += () => log.Add("pause");
            router.SubmitDown += () => log.Add("submit");
            router.HelpDown += () => log.Add("help");
            router.NavigateDown += d => log.Add($"nav:{d.x},{d.y}");
            router.Enable();
        }

        public override void TearDown()
        {
            router.Disable();
            Object.DestroyImmediate(host);
            base.TearDown();
        }

        [Test]
        public void ETravelsFromTheKeyboardAsDownAndUpOnce()
        {
            using (router.Acquire(InputContext.Gameplay, "g"))
            {
                Press(keyboard.eKey);
                Assert.That(router.InteractHeld, Is.True);
                Release(keyboard.eKey);
            }

            Assert.That(log, Is.EqualTo(new[] { "down", "up" }));
        }

        [Test]
        public void WasdAndArrowsDriveTheNormalizedMoveVectorOnlyInGameplay()
        {
            Press(keyboard.wKey);
            Assert.That(router.Move, Is.EqualTo(Vector2.zero), "em Panel o movimento não é entregue");
            Release(keyboard.wKey);

            using (router.Acquire(InputContext.Gameplay, "g"))
            {
                Press(keyboard.wKey);
                Press(keyboard.dKey);
                Assert.That(router.Move.x, Is.EqualTo(0.7071f).Within(0.01f));
                Assert.That(router.Move.y, Is.EqualTo(0.7071f).Within(0.01f));
                Release(keyboard.wKey);
                Release(keyboard.dKey);
                Assert.That(router.Move, Is.EqualTo(Vector2.zero));

                Press(keyboard.leftArrowKey);
                Assert.That(router.Move, Is.EqualTo(Vector2.left));
                Release(keyboard.leftArrowKey);
            }
        }

        [Test]
        public void PauseKeysAndPanelNavigationAreDelivered()
        {
            Press(keyboard.pKey);
            Release(keyboard.pKey);
            Press(keyboard.escapeKey);
            Release(keyboard.escapeKey);
            Press(keyboard.downArrowKey);
            Release(keyboard.downArrowKey);
            Press(keyboard.enterKey);
            Release(keyboard.enterKey);

            Assert.That(log, Is.EqualTo(new[] { "pause", "pause", "nav:0,-1", "submit" }));
        }

        [Test]
        public void HelpOnlyWorksInGameplay()
        {
            Press(keyboard.hKey);
            Release(keyboard.hKey);
            Assert.That(log, Is.Empty);

            using (router.Acquire(InputContext.Gameplay, "g"))
            {
                Press(keyboard.hKey);
                Release(keyboard.hKey);
            }

            Assert.That(log, Is.EqualTo(new[] { "help" }));
        }

        [Test]
        public void ERealKeyHeldThroughAContextChangeIsRearmedByRelease()
        {
            var game = router.Acquire(InputContext.Gameplay, "g");
            Press(keyboard.eKey);
            var panel = router.Acquire(InputContext.Cinematic, "cutscene");
            panel.Dispose();

            Assert.That(router.InteractHeld, Is.False);
            Release(keyboard.eKey);
            Press(keyboard.eKey);
            Release(keyboard.eKey);
            game.Dispose();

            Assert.That(log, Is.EqualTo(new[] { "down", "up", "down", "up" }));
        }
    }
}
