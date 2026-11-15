using NUnit.Framework;
using UnityEngine;
namespace Abandoned.SpaceApps.Tests
{
    public sealed class PrimaryInstrumentInputTests
    {
        [Test] public void MouseIsGameplayOnlyAndMustBeReleasedAcrossContextAndFocusChanges()
        {
            var go = new GameObject("primary input test"); var router = go.AddComponent<InputRouter>(); int down = 0, up = 0;
            router.PrimaryDown += () => down++; router.PrimaryUp += () => up++;
            router.FeedPrimary(true); Assert.That(down, Is.Zero);
            var play = router.Acquire(InputContext.Gameplay, "instrument"); Assert.That(router.PrimaryHeld, Is.False);
            router.FeedPrimary(false); router.FeedPrimary(true); Assert.That(down, Is.EqualTo(1)); Assert.That(router.PrimaryHeld, Is.True);
            var pause = router.Acquire(InputContext.Paused, "pause"); Assert.That(up, Is.EqualTo(1)); pause.Dispose(); Assert.That(router.PrimaryHeld, Is.False);
            router.FeedPrimary(false); router.FeedPrimary(true); Assert.That(down, Is.EqualTo(2)); router.NotifyFocusLost(); Assert.That(up, Is.EqualTo(2));
            router.FeedPrimary(true); Assert.That(down, Is.EqualTo(2)); router.FeedPrimary(false); router.FeedPrimary(true); Assert.That(down, Is.EqualTo(3));
            play.Dispose(); Object.DestroyImmediate(go);
        }
        [Test] public void PointerPositionIsImmediateEvenInMenusAndEHasNoMouseSideEffects()
        {
            var go = new GameObject("pointer test"); var router = go.AddComponent<InputRouter>();
            router.FeedPointer(new Vector2(100, 200)); router.FeedInteract(true);
            Assert.That(router.PointerPosition, Is.EqualTo(new Vector2(100, 200))); Assert.That(router.PrimaryHeld, Is.False); Assert.That(router.InteractHeld, Is.True);
            Object.DestroyImmediate(go);
        }
    }
}
