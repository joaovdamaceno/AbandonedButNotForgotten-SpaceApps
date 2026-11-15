using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Abandoned.SpaceApps.Tests
{
    public sealed class PrimaryInstrumentDeviceTests : InputTestFixture
    {
        [Test] public void ActualMouseBindingIsScopedAndDoesNotCancelKeyboardContact()
        {
            var mouse = InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();
            var go=new GameObject("actual primary input");var router=go.AddComponent<InputRouter>();router.Enable();int presses=0;router.PrimaryDown+=()=>presses++;
            Press(mouse.leftButton);Assert.That(router.PrimaryHeld,Is.False);
            using(var play=router.Acquire(InputContext.Gameplay,"instrument"))
            {
                Assert.That(router.PrimaryHeld,Is.False);Release(mouse.leftButton);Press(mouse.leftButton);Assert.That(router.PrimaryHeld,Is.True);Assert.That(presses,Is.EqualTo(1));
                Press(keyboard.eKey);Release(mouse.leftButton);Assert.That(router.InteractHeld,Is.True);Assert.That(router.PrimaryHeld,Is.False);
                router.NotifyFocusLost();Assert.That(router.InteractHeld,Is.False);Assert.That(router.PrimaryHeld,Is.False);
            }
            Object.DestroyImmediate(go);
        }
    }
}
