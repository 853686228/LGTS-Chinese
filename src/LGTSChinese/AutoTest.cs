using System;
using System.Collections;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LGTSChinese
{
    /// <summary>
    /// Automated in-game verification: drives the game with simulated keyboard events and
    /// captures framebuffer screenshots with ScreenCapture (works without window focus).
    /// Attached to a hidden DontDestroyOnLoad GameObject by <see cref="Plugin"/>.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<int> Seconds;
        public static ConfigEntry<int> ShotInterval;
        public static ConfigEntry<bool> PressKeys;

        private static AutoTest _instance;
        private static bool _spawned;
        private static int _shotIndex;
        private Keyboard _keyboard;

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Debug", "AutoTest", false, "自动测试：模拟按键并定期截图。");
            Seconds = config.Bind("Debug", "AutoTestSeconds", 120, "自动测试总时长（秒）。");
            ShotInterval = config.Bind("Debug", "AutoTestShotInterval", 4, "自动测试截图间隔（秒）。");
            PressKeys = config.Bind("Debug", "AutoTestPressKeys", true, "自动测试中模拟按键推进剧情。");
        }

        public static void Spawn()
        {
            if (_spawned)
                return;
            Plugin.Trace("AutoTest.Spawn enter, instance=" + (_instance == null ? "NULL" : "exists"));
            if (Enabled == null || !Enabled.Value)
                return;
            _spawned = true;
            try
            {
                var go = new GameObject("LGTSChineseAutoTest");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<AutoTest>();
                Plugin.Trace("AutoTest.Spawn created go=" + go + " component=" + (_instance == null ? "NULL" : "OK"));
            }
            catch (Exception e)
            {
                Plugin.Trace("AutoTest spawn failed: " + e);
                _spawned = false;
            }
        }

        private void Awake()
        {
            Plugin.Trace("AutoTest.Awake");
        }

        private void Start()
        {
            Plugin.Trace("AutoTest component start");
            StartCoroutine(Run());
        }

        private void EnsureKeyboard()
        {
            if (Keyboard.current != null)
            {
                _keyboard = Keyboard.current;
                return;
            }
            try
            {
                _keyboard = InputSystem.AddDevice<Keyboard>();
            }
            catch (Exception e)
            {
                Plugin.Trace("AutoTest: add keyboard failed: " + e.Message);
            }
        }

        private void SetKeys(params Key[] keys)
        {
            if (_keyboard == null)
                return;
            try
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            }
            catch (Exception e)
            {
                Plugin.Trace("AutoTest: queue keys failed: " + e.Message);
            }
        }

        private IEnumerator Tap(Key key)
        {
            SetKeys(key);
            yield return new WaitForSeconds(0.09f);
            SetKeys();
            yield return new WaitForSeconds(0.12f);
        }

        private IEnumerator Hold(Key key, float seconds)
        {
            SetKeys(key);
            yield return new WaitForSeconds(seconds);
            SetKeys();
            yield return new WaitForSeconds(0.15f);
        }

        private void Shot()
        {
            try
            {
                var dir = Path.Combine(Plugin.PluginDir ?? ".", "shots");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "auto_" + (++_shotIndex).ToString("000") + ".png");
                ScreenCapture.CaptureScreenshot(path);
                Plugin.Trace("AutoTest shot: " + path);
            }
            catch (Exception e)
            {
                Plugin.Trace("AutoTest shot failed: " + e.Message);
            }
        }

        private IEnumerator Run()
        {
            Plugin.Trace("AutoTest start (seconds=" + Seconds.Value + ")");
            yield return new WaitForSeconds(3f);
            EnsureKeyboard();
            Plugin.Trace("AutoTest keyboard=" + (_keyboard != null ? _keyboard.name : "NULL"));

            // skip the opening video with a held ESC
            yield return new WaitForSeconds(2f);
            yield return Hold(Key.Escape, 1.4f);
            yield return new WaitForSeconds(1.5f);
            Shot();

            float total = 0f;
            float nextShot = 0f;
            while (total < Seconds.Value)
            {
                if (total >= nextShot)
                {
                    Shot();
                    nextShot = total + Math.Max(1, ShotInterval.Value);
                }
                if (PressKeys.Value)
                {
                    yield return Tap(Key.Space);
                    yield return new WaitForSeconds(0.5f);
                    yield return Tap(Key.Enter);
                    yield return new WaitForSeconds(0.5f);
                    if (((int)total) % 9 < 3)
                    {
                        yield return Tap(Key.DownArrow);
                        yield return new WaitForSeconds(0.2f);
                        yield return Tap(Key.Enter);
                        yield return new WaitForSeconds(0.4f);
                    }
                }
                total += 1.8f;
                yield return new WaitForSeconds(1.0f);
            }
            Shot();
            Plugin.Trace("AutoTest done, shots=" + _shotIndex);
        }
    }
}
