using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.SoilTests
{
    public sealed class SoilFlowTests:InputTestFixture
    {
        SurveyorSoilPreview host;Keyboard keyboard;
        public override void Setup()
        {
            base.Setup();keyboard=InputSystem.AddDevice<Keyboard>();EditorSceneManager.OpenScene(SurveyorSoilBuilder.Scene);
            host=Object.FindAnyObjectByType<SurveyorSoilPreview>();host.Initialize();host.Service.Mode=HoldMode.Hold;
            host.Flow.CaptureOverride=()=>new Texture2D(16,12);host.Begin();
        }
        public override void TearDown(){host.Owner.ReturnToDossier();Object.DestroyImmediate(host.gameObject);base.TearDown();}
        void Advance(float seconds){while(seconds>.00001f){float dt=Mathf.Min(.05f,seconds);host.Clock.Tick(dt);host.Service.Tick(host.Clock.ActiveDeltaSeconds);host.Scoop.Tick(host.Clock.ActiveDeltaSeconds);host.Flow.Tick(host.Clock.ActiveDeltaSeconds);seconds-=dt;}host.Clock.Tick(0);}
        void Dig(){Advance(2.5f);Press(keyboard.eKey);Advance(17);Assert.That(host.Scoop.Phase,Is.EqualTo(ScoopPhase.Complete));Assert.That(host.Flow.Phase,Is.EqualTo(SoilPhase.Press));}
        void PressTest(){Dig();Release(keyboard.eKey);Press(keyboard.eKey);Advance(7.4f);Assert.That(host.Flow.Phase,Is.EqualTo(SoilPhase.Impact));}
        [Test]public void OneHeldKeyCannotDigPressAndDrop()
        {Dig();Advance(12);Assert.That(host.Flow.Preparation,Is.Zero);Assert.That(host.Flow.Phase,Is.EqualTo(SoilPhase.Press));Release(keyboard.eKey);Press(keyboard.eKey);Advance(7.4f);Advance(5);Assert.That(host.Flow.Phase,Is.EqualTo(SoilPhase.Impact));Assert.That(host.Flow.Preparation,Is.Zero);Release(keyboard.eKey);Press(keyboard.eKey);Advance(2.1f);Release(keyboard.eKey);Advance(4);Assert.That(host.Flow.Phase,Is.EqualTo(SoilPhase.Comparison));Assert.That(host.Flow.HasBothCaptures,Is.True);Advance(20.2f);Assert.That(host.Owner.CurrentStepId,Is.EqualTo("Eclipse"));Assert.That(host.Owner.Evidence.Records.Count,Is.EqualTo(1));}
        [Test]public void ReleaseAndPausePreserveThePressWork()
        {Dig();Release(keyboard.eKey);Press(keyboard.eKey);Advance(2);Release(keyboard.eKey);float p=host.Flow.Preparation;Advance(3);Assert.That(host.Flow.Preparation,Is.EqualTo(p));host.Owner.Pause("test");Advance(3);Assert.That(host.Flow.Preparation,Is.EqualTo(p));host.Owner.Resume("test");Press(keyboard.eKey);Advance(2.2f);Assert.That(host.Flow.Phase,Is.EqualTo(SoilPhase.PressRetreat));}
        [Test]public void RestartReleasesPhotosAndRestoresExcavation()
        {PressTest();var before=host.Flow.Before;Release(keyboard.eKey);Press(keyboard.eKey);Advance(2.2f);host.Restart();Assert.That(before==null,Is.True);Assert.That(host.Flow.Phase,Is.EqualTo(SoilPhase.Digging));Assert.That(host.Scoop.DigFraction,Is.Zero);Assert.That(host.Owner.Evidence.Records.Count,Is.Zero);Assert.That(host.Flow.Before,Is.Not.Null);}
        [Test]public void ReturningDuringContactClearsInputAndDoesNotCreateLateResults()
        {Dig();Release(keyboard.eKey);Press(keyboard.eKey);Advance(3.7f);var before=host.Flow.Before;host.Owner.ReturnToDossier();Advance(40);Assert.That(host.Service.HoldingId,Is.Null);Assert.That(before==null,Is.True);Assert.That(host.Flow.Running,Is.False);Assert.That(host.Owner.Evidence.Records.Count,Is.Zero);}
        [Test]public void TheExistingArmOnlyAcceptsTestPosesAfterSafeRetraction()
        {Assert.That(host.Scoop.PositionForSoilTest(Vector3.zero),Is.False);Dig();Assert.That(host.Scoop.PositionForSoilTest(new Vector3(.08f,-1,.38f)),Is.True);Assert.That(host.Scoop.TipClearanceMeters,Is.GreaterThanOrEqualTo(0));Assert.That(host.Scoop.ReachErrorMeters,Is.LessThan(.01f));}
    }
}
