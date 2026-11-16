using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    public static class Localization
    {
        [Serializable] public class Entry { public string key, pt, en; }
        [Serializable] public class Table { public Entry[] entries; }
        static Dictionary<string, Entry> table;
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (table == null)
            {
                table = new Dictionary<string, Entry>();
                // "Localization" is the Opportunity table.
                foreach (string name in new[] { "Localization" })
                {
                    var asset = Resources.Load<TextAsset>(name);
                    if (asset != null) foreach (var e in JsonUtility.FromJson<Table>(asset.text).entries) table[e.key] = e;
                }
            }
            return table.TryGetValue(key, out var entry) ? (GameSettings.English ? entry.en : entry.pt) : key;
        }
        public static string Choose(string pt, string en) => GameSettings.English ? en : pt;
    }
}
