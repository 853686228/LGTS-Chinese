using System;
using System.Collections.Generic;
using Gridly;
using HarmonyLib;
using LGTS.LGTS_Utils;
using LGTS.Managers;
using LGTS.UI;
using TMPro;
using UnityEngine;

namespace LGTSChinese
{
    // ---------------------------------------------------------------------
    // 1) Localization data injection
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(Gridly.Project), "Init")]
    internal static class ProjectInitPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                // IMPORTANT: do NOT use Project.singleton here. Init() is called from inside
                // the singleton getter, and while Resources.Load is not ready yet the static
                // field stays null -> calling the getter again recurses until the native
                // stack overflows (observed as a ntdll access violation crash).
                var field = HarmonyLib.AccessTools.Field(typeof(Gridly.Project), "_singleton");
                var project = field != null ? field.GetValue(null) as Gridly.Project : null;
                if (project != null)
                    Plugin.ApplyProject(project);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("ProjectInitPatch: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(Gridly.Project), "Init")]
    internal static class ProjectInitPreOnlyPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Plugin.Trace("Project.Init prefix reached");
        }
    }

    // ---------------------------------------------------------------------
    // 2) Font selection
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(FontLocalization), "GetFont")]
    internal static class FontLocalizationPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(FontLocalization __instance, Gridly.Languages lang,
            FontLocalization.FontType fontType, ref TMP_FontAsset __result)
        {
            if (!Plugin.ChineseMode)
                return true;
            var font = Plugin.GetChineseFont();
            if (font != null)
            {
                __result = font;
                return false;
            }
            // Chinese mode without a usable Chinese font: fall back to an existing config
            // (otherwise the original code would NRE for zhCN and can take the game down)
            try
            {
                var list = __instance != null ? __instance.fontLocalizationList : null;
                if (list != null && list.Count > 0)
                {
                    var cfg = list.Find(x => x.language == lang);
                    if (cfg.language != lang || cfg.Dialogue == null)
                        cfg = list[0];
                    switch (fontType)
                    {
                        case FontLocalization.FontType.Dialogue:
                            __result = cfg.Dialogue;
                            return false;
                        case FontLocalization.FontType.Minigames:
                            __result = cfg.Minigames;
                            return false;
                        case FontLocalization.FontType.Titles:
                            __result = cfg.Titles;
                            return false;
                        default:
                            __result = cfg.UIFont;
                            return false;
                    }
                }
            }
            catch (Exception)
            {
            }
            return true;
        }
    }

    // ---------------------------------------------------------------------
    // 3) Static / hard-coded text translation
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(TMP_Text), "set_text")]
    internal static class TmpTextSetPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string value)
        {
            try
            {
                value = Plugin.Translate(value);
            }
            catch (Exception)
            {
            }
        }
    }

    [HarmonyPatch(typeof(UnityEngine.UI.Text), "set_text")]
    internal static class LegacyTextSetPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string value)
        {
            try
            {
                value = Plugin.Translate(value);
            }
            catch (Exception)
            {
            }
        }
    }

    // ---------------------------------------------------------------------
    // 4) Literal (non-key) dialogue texts and item strings go through the
    //    LocalizationMediator; translate exact matches there.
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(LocalizationMediator), "GetTranslation", new Type[] { typeof(string) })]
    internal static class GetTranslationPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref string __result)
        {
            try
            {
                __result = Plugin.Translate(__result);
            }
            catch (Exception)
            {
            }
        }
    }

    [HarmonyPatch(typeof(LocalizationMediator), "GetTranslation", new Type[] { typeof(string), typeof(string) })]
    internal static class GetTranslationTabPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref string __result)
        {
            try
            {
                __result = Plugin.Translate(__result);
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Manually patched (ref parameter cannot be expressed in the HarmonyPatch attribute).</summary>
    internal static class ManualPatches
    {
        public static void RefTranslationPrefix(ref string term)
        {
            try
            {
                if (Plugin.ChineseMode && term != null)
                    term = Plugin.Translate(term);
            }
            catch (Exception)
            {
            }
        }
    }

    // ---------------------------------------------------------------------
    // 5) Character aliases are hard-coded in EntityDataManager.LoadNewGameData
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(LGTS.Domain.DataManagers.EntityDataManager), "GetEntityAlias", new Type[] { typeof(string) })]
    internal static class EntityAliasPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref string __result)
        {
            try
            {
                __result = Plugin.Translate(__result);
            }
            catch (Exception)
            {
            }
        }
    }

    // ---------------------------------------------------------------------
    // 6) Language selector: original has a single "LANG_English" option.
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(ConfigMenuHome), "Start")]
    internal static class ConfigMenuStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ConfigMenuHome __instance)
        {
            try
            {
                var options = __instance.TextLanguage;
                if (options == null)
                    return;
                options.AddOption("简体中文");
                options.LockOptions(Plugin.ChineseMode ? 1 : 0);
                Plugin.Log.LogInfo("language option added");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("ConfigMenuStartPatch: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(SettingsManager), "SetLanguage", new Type[] { typeof(int) })]
    internal static class SetLanguagePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(SettingsManager __instance, int index)
        {
            try
            {
                __instance.LanguageIdx = index;
                Plugin.OnLanguageChanged(index);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SetLanguagePatch: " + e);
            }
            return false;
        }
    }
}
