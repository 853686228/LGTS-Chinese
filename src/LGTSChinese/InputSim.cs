using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LGTSChinese
{
    /// <summary>Simulates keyboard state events through the Unity Input System.</summary>
    public static class InputSim
    {
        private static Keyboard _keyboard;

        public static void Ensure()
        {
            if (_keyboard != null)
                return;
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
                Plugin.Trace("InputSim: add keyboard failed: " + e.Message);
            }
        }

        public static void SetKeys(params Key[] keys)
        {
            if (_keyboard == null)
                return;
            try
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            }
            catch (Exception e)
            {
                Plugin.Trace("InputSim: queue keys failed: " + e.Message);
            }
        }

        public static IEnumerator Tap(Key key)
        {
            SetKeys(key);
            yield return new WaitForSeconds(0.09f);
            SetKeys();
            yield return new WaitForSeconds(0.12f);
        }

        public static IEnumerator Hold(Key key, float seconds)
        {
            SetKeys(key);
            yield return new WaitForSeconds(seconds);
            SetKeys();
            yield return new WaitForSeconds(0.15f);
        }
    }
}
