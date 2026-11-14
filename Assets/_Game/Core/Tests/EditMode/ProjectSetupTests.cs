using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>CORE-001: a cópia versionada corresponde à configuração documentada e reaplicar não a altera.</summary>
    public class ProjectSetupTests
    {
        [Test]
        public void ProjectMatchesCore001Spec()
        {
            var issues = ProjectSetupValidator.Collect();
            Assert.That(issues, Is.Empty, "Divergências:\n" + string.Join("\n", issues));
        }

        [Test]
        public void ApplyingTwiceKeepsPipelineGuidsAndConformance()
        {
            var pipelineGuid = AssetDatabase.AssetPathToGUID(ProjectSetupSpec.PipelineAssetPath);
            var rendererGuid = AssetDatabase.AssetPathToGUID(ProjectSetupSpec.RendererDataPath);

            ProjectSetup.Apply();
            ProjectSetup.Apply();

            Assert.That(AssetDatabase.AssetPathToGUID(ProjectSetupSpec.PipelineAssetPath), Is.EqualTo(pipelineGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(ProjectSetupSpec.RendererDataPath), Is.EqualTo(rendererGuid));
            Assert.That(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline), Is.EqualTo(ProjectSetupSpec.PipelineAssetPath));
            Assert.That(ProjectSetupValidator.Collect(), Is.Empty);
        }

        [Test]
        public void EveryEditorFolderCompilesIntoTheEditorAssembly()
        {
            Assert.That(ProjectSetupValidator.FindEditorFoldersWithoutAssembly(), Is.Empty,
                "Adicione um .asmref para " + ProjectSetupSpec.EditorAssemblyName + " nessas pastas.");
        }
    }
}
