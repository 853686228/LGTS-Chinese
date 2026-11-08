using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LGTSChinese
{
    /// <summary>
    /// Translates serialized / hard-coded UI strings (settings labels, buttons, minigame HUD, credits...)
    /// by exact match against zh_static.tsv. Tracks originals so English mode can be restored.
    /// </summary>
    public static class StaticTranslator
    {
        private static readonly Dictionary<TMP_Text, string> TmpOriginals = new Dictionary<TMP_Text, string>();
        private static readonly Dictionary<UnityEngine.UI.Text, string> LegacyOriginals = new Dictionary<UnityEngine.UI.Text, string>();

        public static void Refresh(bool chinese)
        {
            if (!chinese)
            {
                Restore();
                return;
            }
            if (Plugin.Data == null)
                return;
            int changed = 0;

            TMP_Text[] tmps;
            try
            {
                tmps = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
            }
            catch (Exception)
            {
                tmps = UnityEngine.Object.FindObjectsOfType<TMP_Text>();
            }
            foreach (var t in tmps)
            {
                if (t == null)
                    continue;
                try
                {
                    string original;
                    if (!TmpOriginals.TryGetValue(t, out original))
                    {
                        original = t.text;
                        if (string.IsNullOrEmpty(original))
                            continue;
                        if (!HasTranslation(original))
                            continue;
                        TmpOriginals[t] = original;
                    }
                    t.text = Plugin.Data.TranslateStatic(original);
                    changed++;
                }
                catch (Exception)
                {
                }
            }

            UnityEngine.UI.Text[] legacies;
            try
            {
                legacies = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>(true);
            }
            catch (Exception)
            {
                legacies = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>();
            }
            foreach (var t in legacies)
            {
                if (t == null)
                    continue;
                try
                {
                    string original;
                    if (!LegacyOriginals.TryGetValue(t, out original))
                    {
                        original = t.text;
                        if (string.IsNullOrEmpty(original))
                            continue;
                        if (!HasTranslation(original))
                            continue;
                        LegacyOriginals[t] = original;
                    }
                    t.text = Plugin.Data.TranslateStatic(original);
                    changed++;
                }
                catch (Exception)
                {
                }
            }

            if (changed > 0)
                Plugin.Log.LogInfo("static UI translated: " + changed);
        }

        private static bool HasTranslation(string original)
        {
            return Plugin.Data != null && Plugin.Data.TranslateStatic(original) != original;
        }

        public static void Restore()
        {
            foreach (var kv in TmpOriginals)
            {
                try
                {
                    if (kv.Key != null)
                        kv.Key.text = kv.Value;
                }
                catch (Exception)
                {
                }
            }
            TmpOriginals.Clear();
            foreach (var kv in LegacyOriginals)
            {
                try
                {
                    if (kv.Key != null)
                        kv.Key.text = kv.Value;
                }
                catch (Exception)
                {
                }
            }
            LegacyOriginals.Clear();
        }
    }
}
