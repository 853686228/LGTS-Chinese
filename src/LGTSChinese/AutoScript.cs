using System;
using System.Collections;
using System.IO;
using BepInEx.Configuration;
using LGTS.Scenes;
using LGTS.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LGTSChinese
{
    /// <summary>
    /// Debug/QA helper: drives the title screen into the settings menu (verifies translated UI)
    /// and starts a new game (verifies translated dialogue), capturing screenshots with
    /// ScreenCapture. Enabled only through the [Debug] AutoScript config entry.
    /// </summary>
    public class AutoScript : MonoBehaviour
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<int> DialogSeconds;
        public static ConfigEntry<bool> OpenSettings;

        private static bool _spawned;
        private static int _shotIndex;

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Debug", "AutoScript", false,
                "自动脚本：标题界面→设置菜单截图→开新游戏截对话图（QA 用）。");
            DialogSeconds = config.Bind("Debug", "AutoScriptDialogSeconds", 90,
                "开新游戏后继续截图/按键的时长（秒）。");
            OpenSettings = config.Bind("Debug", "AutoScriptOpenSettings", true,
                "自动脚本中是否打开设置菜单截图（第二次跑对话时可关掉）。");
        }

        public static void SpawnIfTitleScreen(string sceneName)
        {
            if (_spawned || Enabled == null || !Enabled.Value)
                return;
            if (sceneName != "TitleScreen")
                return;
            _spawned = true;
            try
            {
                var go = new GameObject("LGTSChineseAutoScript");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<AutoScript>();
                Plugin.Trace("AutoScript spawned on TitleScreen");
            }
            catch (Exception e)
            {
                Plugin.Trace("AutoScript spawn failed: " + e);
            }
        }

        private void Start()
        {
            StartCoroutine(Run());
        }

        private void Shot(string tag)
        {
            try
            {
                var dir = Path.Combine(Plugin.PluginDir ?? ".", "shots");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "script_" + tag + "_" + (++_shotIndex).ToString("00") + ".png");
                ScreenCapture.CaptureScreenshot(path);
                Plugin.Trace("AutoScript shot: " + path);
            }
            catch (Exception e)
            {
                Plugin.Trace("AutoScript shot failed: " + e.Message);
            }
        }

        private static T FindOne<T>() where T : UnityEngine.Object
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<T>();
                if (all != null && all.Length > 0)
                    return all[0];
            }
            catch (Exception e)
            {
                Plugin.Trace("FindOne<" + typeof(T).Name + "> failed: " + e.Message);
            }
            return null;
        }

        private IEnumerator Run()
        {
            Plugin.Trace("AutoScript start");
            InputSim.Ensure();
            yield return new WaitForSeconds(6f);

            // 1) title screen: reveal logo / menu
            TitleScreenManager tsm = null;
            try
            {
                tsm = FindOne<TitleScreenManager>();
                Plugin.Trace("AutoScript TitleScreenManager=" + (tsm == null ? "NULL" : "OK"));
                if (tsm != null)
                    tsm.ShowLogo();
            }
            catch (Exception e)
            {
                Plugin.Trace("AutoScript show logo failed: " + e);
            }
            yield return new WaitForSeconds(2.5f);
            Shot("title");

            // 2) settings menu
            ConfigMenuHome menu = null;
            if (OpenSettings.Value)
            {
                try
                {
                    menu = FindOne<ConfigMenuHome>();
                    Plugin.Trace("AutoScript ConfigMenuHome=" + (menu == null ? "NULL" : "OK"));
                    if (menu != null)
                    {
                        menu.enabled = true;
                        menu.Open();
                    }
                }
                catch (Exception e)
                {
                    Plugin.Trace("AutoScript settings failed: " + e);
                }
                yield return new WaitForSeconds(2.0f);
                Shot("settings");
                yield return new WaitForSeconds(0.5f);
                try
                {
                    if (menu != null)
                    {
                        // Hide the whole menu instead of Close() (Close() pops a "save settings?"
                        // prompt that would block the following gameplay screenshots).
                        menu.gameObject.SetActive(false);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Trace("AutoScript hide settings failed: " + e);
                }
                yield return new WaitForSeconds(0.8f);
            }

            // 3) start a new game
            try
            {
                if (tsm != null)
                {
                    Plugin.Trace("AutoScript starting new game");
                    tsm.LoadInitialScene();
                }
            }
            catch (Exception e)
            {
                Plugin.Trace("AutoScript new game failed: " + e);
            }
            yield return new WaitForSeconds(2f);

            // 4) advance dialogue and take screenshots
            float total = 0f;
            float nextShot = 0f;
            while (total < DialogSeconds.Value)
            {
                if (total >= nextShot)
                {
                    Shot("play");
                    nextShot = total + 2.0f;
                }
                yield return InputSim.Tap(Key.Space);
                yield return new WaitForSeconds(0.7f);
                yield return InputSim.Tap(Key.Enter);
                yield return new WaitForSeconds(0.7f);
                total += 1.9f;
            }
            Shot("end");
            Plugin.Trace("AutoScript done");
        }
    }
}
