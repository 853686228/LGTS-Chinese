using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Gridly;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LGTSChinese
{
    [BepInPlugin(GUID, "Little Goody Two Shoes 简体中文汉化", "0.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.syukuu.lgts.zhcn";

        public static Plugin Instance;
        public static ManualLogSource Log;
        public static string PluginDir;
        public static LocData Data;
        public static bool ChineseMode = true;
        public static TMP_FontAsset ChineseFont;
        public static ConfigEntry<bool> CfgEnableChinese;
        public static ConfigEntry<bool> CfgEnableFont;
        public static ConfigEntry<bool> CfgEnableStaticUI;
        public static ConfigEntry<bool> CfgEnableGrid;
        public static ConfigEntry<string> CfgPatchGroups;
        public static ConfigEntry<bool> CfgDumpFonts;
        public static ConfigEntry<bool> CfgTraceToFile;

        private static bool _projectApplied;
        private static bool _fontApplied;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);

            CfgEnableChinese = Config.Bind("General", "EnableChinese", true,
                "启动时使用简体中文。false = 保持原版英文。");
            CfgEnableFont = Config.Bind("General", "EnableFont", true,
                "启用中文字体替换（排查崩溃时可关闭）。");
            CfgEnableStaticUI = Config.Bind("General", "EnableStaticUI", true,
                "翻译硬编码 UI 文本（排查崩溃时可关闭）。");
            CfgEnableGrid = Config.Bind("General", "EnableGrid", true,
                "向 Gridly 文本表注入中文列（排查崩溃时可关闭）。");
            CfgPatchGroups = Config.Bind("Debug", "PatchGroups", "all",
                "调试用：启用的 Harmony 补丁组，逗号分隔（proj,trans,alias,menu,lang,font,text）或 all/none。");
            CfgDumpFonts = Config.Bind("Debug", "DumpFonts", false,
                "调试用：把系统字体列表和动态字体创建结果写入 trace.log。");
            CfgTraceToFile = Config.Bind("Debug", "TraceToFile", false,
                "把插件调试信息追加写入插件目录的 trace.log（排查问题用）。");
            AutoTest.Bind(Config);
            AutoScript.Bind(Config);
            ChineseMode = CfgEnableChinese.Value;
            Trace("plugin awake; AutoTest=" + (AutoTest.Enabled == null ? "NULL" : AutoTest.Enabled.Value.ToString()) + " dir=" + PluginDir);

            Data = LocData.Load(PluginDir, Log);
            Log.LogInfo("translation grid entries=" + Data.GridCount + " static entries=" + Data.StaticCount);

            var harmony = new Harmony(GUID);
            ApplyPatchGroups(harmony);

            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log("[LGTSZH] plugin awake, fonts=" + CfgEnableFont.Value + " static=" + CfgEnableStaticUI.Value + " grid=" + CfgEnableGrid.Value);
            Log.LogInfo("LGTSChinese loaded. ChineseMode=" + ChineseMode);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Plugin.Trace("sceneLoaded: " + scene.name + " instance=" + (Instance == null ? "NULL" : "OK"));
            if (CfgEnableStaticUI.Value || CfgEnableFont.Value)
                SceneRefresher.Spawn();
            AutoTest.Spawn();
            AutoScript.SpawnIfTitleScreen(scene.name);
        }

        /// <summary>Called from Project.Init postfix after Resources.Load&lt;Project&gt;("Project").</summary>
        public static void ApplyProject(Project project)
        {
            if (!CfgEnableGrid.Value)
            {
                Debug.Log("[LGTSZH] grid injection disabled");
                return;
            }
            if (_projectApplied || project == null)
                return;
            _projectApplied = true;
            Trace("ApplyProject start");
            try
            {
                int grids = 0, records = 0, translated = 0;
                Trace("ApplyProject: grids=" + (project.grids == null ? -1 : project.grids.Count));
                foreach (Gridly.Internal.Grid grid in project.grids)
                {
                    grids++;
                    if (grid.records == null)
                        continue;
                    foreach (Gridly.Internal.Record record in grid.records)
                    {
                        if (record.columns == null)
                            record.columns = new List<Gridly.Internal.Column>();
                        string zh;
                        if (!Data.TryGetGrid(grid.nameGrid, record.recordID, out zh))
                        {
                            zh = null;
                            foreach (var col in record.columns)
                            {
                                if (col.columnID == "enUS")
                                {
                                    zh = col.text;
                                    break;
                                }
                            }
                            if (zh == null)
                                zh = "";
                        }
                        else
                        {
                            translated++;
                        }
                        bool has = false;
                        foreach (var col in record.columns)
                        {
                            if (col.columnID == "zhCN") { has = true; break; }
                        }
                        if (!has)
                            record.columns.Add(new Gridly.Internal.Column("zhCN", zh));
                        records++;
                    }
                }
                Log.LogInfo("Project localized: grids=" + grids + " records=" + records + " translated=" + translated);
                Trace("ApplyProject: records=" + records + " translated=" + translated);

                if (ChineseMode)
                {
                    Trace("ApplyProject: add lang support");
                    bool hasSupport = project.langSupports != null &&
                        project.langSupports.Exists(x => x != null && x.languagesSuport == Languages.zhCN);
                    if (!hasSupport)
                    {
                        var ls = new LangSupport
                        {
                            languagesSuport = Languages.zhCN,
                            name = "zhCN",
                            screenshotColumnId = "zhCN"
                        };
                        if (project.langSupports == null)
                            project.langSupports = new List<LangSupport>();
                        project.langSupports.Insert(0, ls);
                    }
                    // NOTE: do NOT call SetChosenLanguageCode here (it refreshes all Translator
                    // components and can run before the Chinese font is ready). The language
                    // getter already falls back to langSupports[0], which is zhCN now.
                    try
                    {
                        var field = HarmonyLib.AccessTools.Field(typeof(Project), "chosenLangCodeName");
                        if (field != null)
                            field.SetValue(project, "zhCN");
                    }
                    catch (Exception e)
                    {
                        Log.LogWarning("set chosen lang field failed: " + e.Message);
                    }
                    Trace("ApplyProject: language forced zhCN");
                }
                Trace("ApplyProject done");
            }
            catch (Exception e)
            {
                Log.LogError("ApplyProject failed: " + e);
            }
        }

        private static void DumpOsFonts()
        {
            try
            {
                var names = Font.GetOSInstalledFontNames();
                var sb = new StringBuilder();
                sb.Append("OS fonts (").Append(names == null ? -1 : names.Length).Append("): ");
                if (names != null)
                {
                    int n = Math.Min(names.Length, 80);
                    for (int i = 0; i < n; i++)
                    {
                        if (i > 0) sb.Append(" | ");
                        sb.Append(names[i]);
                    }
                }
                Trace(sb.ToString());
                if (names != null)
                {
                    int tested = 0;
                    foreach (var nm in names)
                    {
                        if (tested >= 8) break;
                        tested++;
                        try
                        {
                            var f = Font.CreateDynamicFontFromOSFont(nm, 32);
                            Trace("CreateDynamicFontFromOSFont('" + nm + "') = " + (f == null ? "NULL" : f.name));
                        }
                        catch (Exception e)
                        {
                            Trace("CreateDynamicFontFromOSFont('" + nm + "') EX " + e.GetType().Name + ": " + e.Message);
                        }
                    }
                    foreach (var cand in new[] { "Noto Serif SC", "SimSun", "Microsoft YaHei", "NSimSun", "KaiTi", "FangSong" })
                    {
                        try
                        {
                            var f = Font.CreateDynamicFontFromOSFont(cand, 32);
                            Trace("candidate '" + cand + "' = " + (f == null ? "NULL" : f.name));
                        }
                        catch (Exception e)
                        {
                            Trace("candidate '" + cand + "' EX " + e.GetType().Name + ": " + e.Message);
                        }
                    }
                    // deeper TMP test
                    try
                    {
                        var f = Font.CreateDynamicFontFromOSFont("Noto Serif SC", 90);
                        Trace("tmp-test font = " + (f == null ? "NULL" : f.name));
                        try
                        {
                            var err = UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(f, 90);
                            Trace("FontEngine.LoadFontFace(font,90) = " + err);
                        }
                        catch (Exception e)
                        {
                            Trace("FontEngine.LoadFontFace EX " + e.GetType().Name + ": " + e.Message);
                        }
                        try
                        {
                            var a1 = TMP_FontAsset.CreateFontAsset(f);
                            Trace("CreateFontAsset(font) = " + (a1 == null ? "NULL" : a1.name));
                        }
                        catch (Exception e)
                        {
                            Trace("CreateFontAsset(font) EX " + e.GetType().Name + ": " + e.Message);
                        }
                        try
                        {
                            var a2 = TMP_FontAsset.CreateFontAsset(f, 32, 5,
                                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                                AtlasPopulationMode.Dynamic, true);
                            Trace("CreateFontAsset(font,32,...) = " + (a2 == null ? "NULL" : a2.name));
                        }
                        catch (Exception e)
                        {
                            Trace("CreateFontAsset(font,32,...) EX " + e.GetType().Name + ": " + e.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        Trace("tmp-test EX " + e.GetType().Name + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Trace("DumpOsFonts failed: " + e);
            }
        }

        public static TMP_FontAsset GetChineseFont()
        {
            if (!CfgEnableFont.Value)
                return null;
            if (ChineseFont == null)
            {
                Debug.Log("[LGTSZH] creating chinese font...");
                if (CfgDumpFonts.Value)
                    DumpOsFonts();
                try
                {
                    ChineseFont = FontFactory.Create(PluginDir, Log);
                }
                catch (Exception e)
                {
                    Log.LogError("font create failed: " + e);
                }
                if (ChineseFont != null)
                {
                    Log.LogInfo("Chinese font asset created: " + ChineseFont.name);
                    Debug.Log("[LGTSZH] chinese font created: " + ChineseFont.name);
                }
            }
            return ChineseFont;
        }

        /// <summary>Attach the Chinese font as a fallback to every TMP font asset in the game.</summary>
        public static void EnsureFontsAvailable()
        {
            if (_fontApplied)
                return;
            var cn = GetChineseFont();
            if (cn == null)
                return;
            _fontApplied = true;
            TMP_FontAsset[] all;
            try
            {
                all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            }
            catch (Exception e)
            {
                Log.LogWarning("FindObjectsOfTypeAll<TMP_FontAsset> failed: " + e.Message);
                return;
            }
            int n = 0;
            foreach (var fa in all)
            {
                if (fa == null || fa == cn)
                    continue;
                try
                {
                    var list = fa.fallbackFontAssetTable;
                    if (list == null)
                    {
                        list = new List<TMP_FontAsset>();
                        fa.fallbackFontAssetTable = list;
                    }
                    if (!list.Contains(cn))
                    {
                        list.Add(cn);
                        n++;
                    }
                }
                catch (Exception e)
                {
                    Log.LogWarning("fallback add failed on " + fa.name + ": " + e.Message);
                }
            }
            Log.LogInfo("fallback font attached to " + n + " font assets (total " + all.Length + ")");
        }

        public static void OnLanguageChanged(int index)
        {
            bool zh = index != 0;
            ChineseMode = zh;
            CfgEnableChinese.Value = zh;
            try { Instance.Config.Save(); } catch { }
            try
            {
                LGTS.LGTS_Utils.LocalizationMediator.SetLanguage(zh ? Languages.zhCN : Languages.enUS);
            }
            catch (Exception e)
            {
                Log.LogWarning("LocalizationMediator.SetLanguage failed (still setting grid language): " + e.Message);
                try
                {
                    var p = Project.singleton;
                    if (p != null)
                        p.SetChosenLanguageCode(zh ? Languages.zhCN : Languages.enUS);
                }
                catch (Exception e2)
                {
                    Log.LogError("language switch failed: " + e2);
                }
            }
            StaticTranslator.Refresh(zh);
        }

        // ---------- helpers used by patches ----------

        private static void ApplyPatchGroups(Harmony harmony)
        {
            string groups = (CfgPatchGroups.Value ?? "all").Trim().ToLowerInvariant();
            if (groups.Length == 0 || groups == "all")
                groups = "proj,trans,alias,menu,lang,font,text";
            Debug.Log("[LGTSZH] patch groups = " + groups);
            if (groups == "none")
            {
                Log.LogInfo("no harmony patch groups enabled");
                return;
            }
            var set = new HashSet<string>();
            foreach (var part in groups.Split(','))
            {
                var g = part.Trim();
                if (g.Length > 0)
                    set.Add(g);
            }
            foreach (var g in set)
            {
                try
                {
                    switch (g)
                    {
                        case "proj":
                            harmony.PatchAll(typeof(ProjectInitPatch));
                            break;
                        case "projpre":
                            harmony.PatchAll(typeof(ProjectInitPreOnlyPatch));
                            break;
                        case "load":
                            Trace("early type resolve: " + typeof(Gridly.Project).FullName);
                            break;
                        case "trans":
                            harmony.PatchAll(typeof(GetTranslationPatch));
                            harmony.PatchAll(typeof(GetTranslationTabPatch));
                            PatchRefTranslation(harmony);
                            break;
                        case "alias":
                            harmony.PatchAll(typeof(EntityAliasPatch));
                            break;
                        case "menu":
                            harmony.PatchAll(typeof(ConfigMenuStartPatch));
                            break;
                        case "lang":
                            harmony.PatchAll(typeof(SetLanguagePatch));
                            break;
                        case "font":
                            harmony.PatchAll(typeof(FontLocalizationPatch));
                            break;
                        case "text":
                            harmony.PatchAll(typeof(TmpTextSetPatch));
                            harmony.PatchAll(typeof(LegacyTextSetPatch));
                            break;
                        default:
                            Log.LogWarning("unknown patch group: " + g);
                            continue;
                    }
                    Log.LogInfo("patched group: " + g);
                }
                catch (Exception e)
                {
                    Log.LogError("patch group " + g + " failed: " + e);
                }
            }
        }

        private static void PatchRefTranslation(Harmony harmony)
        {
            // ref-string overload cannot be targeted through the HarmonyPatch attribute
            try
            {
                var refMethod = typeof(LGTS.LGTS_Utils.LocalizationMediator).GetMethod(
                    "GetTranslation", new Type[] { typeof(string).MakeByRefType() });
                if (refMethod == null)
                {
                    Log.LogWarning("GetTranslation(ref string) not found");
                    return;
                }
                harmony.Patch(refMethod,
                    new HarmonyMethod(typeof(ManualPatches).GetMethod("RefTranslationPrefix")));
                Log.LogInfo("patched GetTranslation(ref string)");
            }
            catch (Exception e)
            {
                Log.LogError("manual patch failed: " + e);
            }
        }

        public static string Translate(string text)
        {
            if (!ChineseMode || string.IsNullOrEmpty(text))
                return text;
            return Data.TranslateStatic(text);
        }

        /// <summary>Persistent crash tracer (Unity log may lose buffered lines on a native crash).</summary>
        public static void Trace(string message)
        {
            if (CfgTraceToFile != null && CfgTraceToFile.Value && PluginDir != null)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(PluginDir, "trace.log"),
                        DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine,
                        new UTF8Encoding(false));
                }
                catch (Exception)
                {
                }
            }
            Debug.Log("[LGTSZH] " + message);
        }

    }
}
