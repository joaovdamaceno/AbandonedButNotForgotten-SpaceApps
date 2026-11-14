using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Orçamento das três fotos do jogador em Marte (Docs/Production/MARS_EVIDENCE_BUDGET.md): as atividades já integradas
    /// podem usar duas; a terceira vaga é reservada para a foto do vale (M26, memorial), da MAR-008.
    /// </summary>
    public class MarsEvidenceBudgetTests
    {
        const int ReservedForValley = 1;

        static EvidenceStore AllCurrentRecords()
        {
            var store = new EvidenceStore();
            var blue = AssetDatabase.LoadAssetAtPath<BlueberriesActivityData>("Assets/_Game/Chapters/Mars/Data/BlueberriesActivity.asset");
            var shield = AssetDatabase.LoadAssetAtPath<HeatShieldResultsData>("Assets/_Game/Chapters/Mars/Data/HeatShieldResults.asset");
            Assert.That(blue, Is.Not.Null);
            Assert.That(shield, Is.Not.Null);

            foreach (var record in new[]
            {
                blue.ImagingRecord(true), blue.ImagingRecord(false), blue.MineralRecord(),
                shield.ShieldPhoto(), shield.ShieldArchive(), shield.MeteoriteComposition(), shield.MeteoritePhoto(), shield.MeteoriteArchive(),
            })
            {
                Assert.That(store.Add(record), Is.True, record.EvidenceId);
            }

            return store;
        }

        [Test]
        public void CurrentActivitiesLeaveTheValleyPhotoItsReservedSlot()
        {
            var store = AllCurrentRecords();
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(2), "escudo e meteorito");
            Assert.That(EvidenceStore.MaxPlayerPhotos - store.PlayerPhotoCount, Is.GreaterThanOrEqualTo(ReservedForValley),
                "a vaga da foto do vale (M26, memorial) não pode ser gasta antes da MAR-008");
        }

        [Test]
        public void ValleyPhotoStillFitsAndAFourthPlayerPhotoWouldBeRefused()
        {
            var store = AllCurrentRecords();
            var valley = new EvidenceRecord("mars.ev.valley.photo", EvidenceKind.Photo, "MAR.VALLEY.photo", "MS26", EvidenceAuthorship.PlayerCapture);
            Assert.That(store.Add(valley), Is.True, "a terceira foto cabe");
            var fourth = new EvidenceRecord("mars.ev.extra.photo", EvidenceKind.Photo, "MAR.EXTRA.photo", "MS26", EvidenceAuthorship.PlayerCapture);
            Assert.Throws<System.InvalidOperationException>(() => store.Add(fourth), "a quarta lança: é por isso que o Sol não pode ser foto do jogador");
        }

        [Test]
        public void SunComparisonAsAScientificRecordDoesNotSpendAPlayerPhoto()
        {
            var store = AllCurrentRecords();
            store.Add(new EvidenceRecord("mars.ev.valley.photo", EvidenceKind.Photo, "MAR.VALLEY.photo", "MS26", EvidenceAuthorship.PlayerCapture));
            var sun = new EvidenceRecord("mars.ev.sun.comparison", EvidenceKind.Observation, "MAR.SUN.comparison", "MS27", EvidenceAuthorship.Reconstruction);
            Assert.That(sun.CanBePresentedAsPlayers, Is.False);
            Assert.That(store.Add(sun), Is.True);
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(EvidenceStore.MaxPlayerPhotos));
        }

        [Test]
        public void ArchiveAndMeasurementRecordsNeverCountAsPlayerPhotos()
        {
            var store = AllCurrentRecords();
            var counted = store.Records.Where(r => r.CanBePresentedAsPlayers).Select(r => r.EvidenceId).ToList();
            Assert.That(counted, Is.EquivalentTo(new[] { "mars.ev.shield.photo", "mars.ev.meteorite.photo" }));
        }
    }
}
