using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Abandoned.SpaceApps.Chapters.Surveyor.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor.Tests
{
    public sealed class SurveyorScienceTests
    {
        SurveyorScience science;
        string json;

        [SetUp]
        public void SetUp()
        {
            json = File.ReadAllText(SurveyorScienceBuilder.ManifestPath);
            science = ScriptableObject.CreateInstance<SurveyorScience>();
            JsonUtility.FromJsonOverwrite(json, science);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(science);

        [Test]
        public void CatalogLoadsAsTypedUnityAssetAndMatchesManifest()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SurveyorScience>(SurveyorScienceBuilder.AssetPath);
            Assert.That(asset, Is.Not.Null, "Execute o builder SUR-001 antes dos testes.");
            Assert.That(asset.Validate(), Is.Empty);
            Assert.That(JsonUtility.ToJson(asset), Is.EqualTo(JsonUtility.ToJson(science)));
            Assert.That(asset.CrossReviewStatus, Is.EqualTo("pending-R7"), "Não simular revisão humana.");
        }

        [Test]
        public void EveryScriptCueHasPublishableEvidence()
        {
            var script = File.ReadAllText("Docs/Chapters/03_SURVEYOR_3.md");
            var ids = Regex.Matches(script, @"(?m)^\| (SUR-S\d{2}) \|").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            Assert.That(ids.Length, Is.EqualTo(31));
            Assert.That(science.CueLinks.Select(c => c.cueId), Is.EquivalentTo(ids));
            Assert.That(science.Validate(), Is.Empty);
            foreach (var cue in science.CueLinks)
                foreach (var id in cue.factIds)
                {
                    Assert.That(science.TryGetFact(id, out var fact), Is.True, cue.cueId);
                    Assert.That(fact.approvedForDisplay, Is.True, cue.cueId);
                }
        }

        [Test]
        public void RebuildingPreservesGuidAndSerializedContent()
        {
            var guid = AssetDatabase.AssetPathToGUID(SurveyorScienceBuilder.AssetPath);
            var before = File.ReadAllBytes(SurveyorScienceBuilder.AssetPath);
            SurveyorScienceBuilder.Build();
            Assert.That(AssetDatabase.AssetPathToGUID(SurveyorScienceBuilder.AssetPath), Is.EqualTo(guid));
            Assert.That(File.ReadAllBytes(SurveyorScienceBuilder.AssetPath), Is.EqualTo(before));
        }

        [TestCase("SUR.C.TestCounts")]
        [TestCase("SUR.C.LandingTime")]
        [TestCase("SUR.C.Coordinates")]
        [TestCase("SUR.C.Pressure")]
        [TestCase("SUR.C.ApolloDates")]
        public void UnresolvedClaimsCannotBeEnabledForDisplay(string id)
        {
            Assert.That(science.TryGetFact(id, out var fact), Is.True);
            Assert.That(fact.approvedForDisplay, Is.False);
            fact.approvedForDisplay = true;
            Assert.That(science.Validate().Any(e => e.Contains("Conflito não pode ser publicado")), Is.True);
        }

        [Test]
        public void CueCannotAccidentallyBindAnUnresolvedCounter()
        {
            science.CueLinks[14].factIds = new[] { "SUR.C.TestCounts" };
            Assert.That(science.Validate().Any(e => e.Contains("Cue usa fato bloqueado")), Is.True);
        }

        [Test]
        public void ImaginedVisitCannotLoseItsQualification()
        {
            science.CueLinks[25].factIds = new[] { "SUR.F.ApolloVisit" };
            Assert.That(science.Validate().Any(e => e.Contains("reconstrução imaginada")), Is.True);
        }

        [Test]
        public void MissingSourceAndUnreachableMuseumCannotSupportReleasedFact()
        {
            var citation = science.Facts[0].citations[0];
            citation.sourceId = "S3-unknown";
            Assert.That(science.Validate().Any(e => e.Contains("Fonte desconhecida")), Is.True);
            citation.sourceId = "S3-12";
            Assert.That(science.Validate().Any(e => e.Contains("fonte indisponível")), Is.True);
        }

        [Test]
        public void DuplicateCueDoesNotMaskMissingCue()
        {
            science.CueLinks[30].cueId = "SUR-S01";
            var errors = science.Validate();
            Assert.That(errors.Any(e => e.Contains("Cue duplicado")), Is.True);
            Assert.That(errors.Any(e => e.Contains("Cue ausente: SUR-S31")), Is.True);
        }

        [Test]
        public void MissingCitationIsReportedWithoutNullReference()
        {
            science.Facts[0].citations = new SurveyorScience.Citation[] { null };
            Assert.That(science.Validate().Any(e => e.Contains("Citação sem localização")), Is.True);
        }

        [Test]
        public void VisualInventoryHasNoFictitiousImports()
        {
            Assert.That(SurveyorScienceBuilder.ValidateReferences(json, science), Is.Empty);
            var imported = json.Replace("\"status\": \"reference-only\"", "\"status\": \"imported\"");
            Assert.That(SurveyorScienceBuilder.ValidateReferences(imported, science).Any(e => e.Contains("não declara arquivos")), Is.True);
            var outside = json.Replace("Assets/_Game/Chapters/Surveyor/Art/References/", "Assets/_Game/Shared/");
            Assert.That(SurveyorScienceBuilder.ValidateReferences(outside, science).Any(e => e.Contains("fora do capítulo")), Is.True);
        }

        [Test]
        public void ComponentCannotLoseItsReferenceOrPretendToBeBuilt()
        {
            var built = json.Replace("\"status\": \"not-built\"", "\"status\": \"built\"");
            Assert.That(SurveyorScienceBuilder.ValidateReferences(built, science).Any(e => e.Contains("Estado/destino de componente inválido")), Is.True);
            // Campo visual removido do inventário de referências, mas o componente ainda o utiliza.
            var orphan = json.Replace("\"id\": \"SUR.V.ScoopRig\"", "\"id\": \"SUR.V.Other\"");
            Assert.That(SurveyorScienceBuilder.ValidateReferences(orphan, science).Any(e => e.Contains("Referência desconhecida no componente")), Is.True);
        }

        [Test]
        public void EmptyCatalogAndVisualManifestFailClearly()
        {
            var empty = ScriptableObject.CreateInstance<SurveyorScience>();
            try
            {
                Assert.That(empty.Validate(), Is.Not.Empty);
                Assert.That(SurveyorScienceBuilder.ValidateReferences("{}", empty), Is.Not.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(empty); }
        }
    }
}
