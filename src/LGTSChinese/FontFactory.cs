using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LGTSChinese
{
    /// <summary>Creates a runtime dynamic TMP font asset that can render simplified Chinese.</summary>
    public static class FontFactory
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceExW(string lpszFilename, uint fl, IntPtr pdv);

        private const uint FR_PRIVATE = 0x10;

        private static readonly string[] BundleFontFiles =
        {
            @"fonts\NotoSerifSC-Regular.otf",
            @"fonts\NotoSerifSC-VF.ttf",
            @"fonts\font.otf",
            @"fonts\font.ttf",
            "font.otf", "font.ttf", "LGTS_CN.otf", "LGTS_CN.ttf",
            "NotoSerifSC-Regular.otf", "NotoSerifSC-Regular.ttf", "NotoSerifSC-VF.ttf"
        };

        // Unity's public Font(string) constructor loads a font file from disk when the
        // argument contains a directory (Font.Internal_CreateFontFromPath).
        private static readonly string[] SystemFontCandidates =
        {
            @"C:\Windows\Fonts\NotoSerifSC-VF.ttf",
            @"C:\Windows\Fonts\NotoSerifSC-Regular.otf",
            @"C:\Windows\Fonts\NotoSerifCJK-Regular.ttc",
            @"C:\Windows\Fonts\simsun.ttc",
            @"C:\Windows\Fonts\Deng.ttf",
            @"C:\Windows\Fonts\simhei.ttf",
            @"C:\Windows\Fonts\msyh.ttc",
            @"C:\Windows\Fonts\NotoSansSC-VF.ttf",
        };

        private static readonly List<Font> KeepAliveFonts = new List<Font>();

        public static TMP_FontAsset Create(string pluginDir, ManualLogSource log)
        {
            var paths = new List<string>();
            try
            {
                if (pluginDir != null)
                {
                    foreach (var f in BundleFontFiles)
                    {
                        var p = Path.Combine(pluginDir, f);
                        if (File.Exists(p))
                            paths.Add(p);
                    }
                }
            }
            catch (Exception e)
            {
                log.LogWarning("bundle font scan failed: " + e.Message);
            }
            foreach (var p in SystemFontCandidates)
            {
                try
                {
                    if (File.Exists(p))
                        paths.Add(p);
                }
                catch (Exception)
                {
                }
            }

            foreach (var path in paths)
            {
                try
                {
                    // Registering privately is only needed for OS-family lookup, but harmless.
                    try { AddFontResourceExW(path, FR_PRIVATE, IntPtr.Zero); } catch (Exception) { }
                    Debug.Log("[LGTSZH] try font file: " + path);
                    var font = new Font(path);
                    if (font == null)
                    {
                        log.LogWarning("new Font(path) returned null: " + path);
                        continue;
                    }
                    var asset = TMP_FontAsset.CreateFontAsset(
                        font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                        AtlasPopulationMode.Dynamic, true);
                    if (asset == null)
                    {
                        log.LogWarning("CreateFontAsset returned null for " + path);
                        continue;
                    }
                    asset.name = "LGTS_CN_" + Path.GetFileNameWithoutExtension(path);
                    asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    asset.isMultiAtlasTexturesEnabled = true;
                    // Keep the source font alive with the asset
                    KeepAliveFonts.Add(font);
                    try
                    {
                        var field = HarmonyLib.AccessTools.Field(typeof(TMP_FontAsset), "m_SourceFontFile");
                        if (field != null)
                            field.SetValue(asset, font);
                    }
                    catch (Exception)
                    {
                    }
                    log.LogInfo("TMP font created from file: " + path);
                    Debug.Log("[LGTSZH] TMP font created from file: " + path);
                    return asset;
                }
                catch (Exception e)
                {
                    log.LogWarning("font file '" + path + "' failed: " + e.GetType().Name + " " + e.Message);
                }
            }

            // last resort: default TMP font (will not render Chinese, but keeps game alive)
            try
            {
                var def = TMP_Settings.defaultFontAsset;
                if (def != null)
                {
                    log.LogError("no CJK font file found, using default TMP font (Chinese will be missing)");
                    return def;
                }
            }
            catch (Exception)
            {
            }
            log.LogError("could not create any Chinese TMP font");
            return null;
        }
    }
}
