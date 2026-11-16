#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Abandoned.SpaceApps.Tests
{
    public sealed class SupplementalNarrationTests
    {
        [UnityTest]
        public IEnumerator FourRecordedSupplementsStartAndFinishAtNaturalSpeed()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            var probe = new GameObject("Supplemental voice playback check");
            var player = probe.AddComponent<NarrationPlayer>();
            player.Initialize(null);
            player.SetFocus(true);
            var source = probe.GetComponent<AudioSource>();
            source.ignoreListenerPause = true; // Probe the files, independent of menu audio unlock.
            try
            {
                foreach (var chapter in new[] { "Pioneer10", "Mars" })
                {
                    var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<NarrationCatalog>(
                        "Assets/_Game/Chapters/" + chapter + "/Narration/EnglishNarration.asset");
                    Assert.That(catalog, Is.Not.Null);
                    player.Configure(catalog);
                    var keys = chapter == "Pioneer10" ? new[] { "PIO.05.01b", "PIO.05.01c" }
                        : new[] { "v13.years.work", "v15.storm.dark" };
                    foreach (var key in keys)
                    {
                        Assert.That(player.TryPlay(key), Is.True, key);
                        var deadline = Time.realtimeSinceStartup + catalog.Duration(key) + 10f;
                        bool observedPlayback = false;
                        while (player.IsBusy && Time.realtimeSinceStartup < deadline)
                        {
                            player.Tick(Time.unscaledDeltaTime);
                            observedPlayback |= source.isPlaying;
                            yield return null;
                        }
                        Assert.That(observedPlayback, Is.True, key + " actual AudioSource start");
                        Assert.That(player.IsBusy, Is.False, key + " natural playback completion");
                        Assert.That(source.pitch, Is.EqualTo(1f));
                    }
                }
            }
            finally { Object.Destroy(probe); }
        }
    }
}
#endif
