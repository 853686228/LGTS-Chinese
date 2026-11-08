using System;
using System.Collections;
using UnityEngine;

namespace LGTSChinese
{
    /// <summary>Per-scene refresh runner: translates serialized UI texts and attaches the
    /// Chinese fallback font once the scene is fully loaded (works even if the BepInEx
    /// plugin instance is not available as a coroutine host).</summary>
    public class SceneRefresher : MonoBehaviour
    {
        public static void Spawn()
        {
            try
            {
                var go = new GameObject("LGTSChineseRefresher");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<SceneRefresher>();
            }
            catch (Exception e)
            {
                Plugin.Trace("SceneRefresher spawn failed: " + e.Message);
            }
        }

        private IEnumerator Start()
        {
            // several passes: HUD / popup prefabs are often instantiated a moment later
            for (int pass = 0; pass < 4; pass++)
            {
                yield return null;
                try
                {
                    if (Plugin.CfgEnableStaticUI != null && Plugin.CfgEnableStaticUI.Value)
                        StaticTranslator.Refresh(Plugin.ChineseMode);
                    if (Plugin.CfgEnableFont != null && Plugin.CfgEnableFont.Value)
                        Plugin.EnsureFontsAvailable();
                    Plugin.Trace("SceneRefresher pass " + pass + " (chinese=" + Plugin.ChineseMode + ")");
                }
                catch (Exception e)
                {
                    Plugin.Trace("SceneRefresher failed: " + e);
                }
                yield return new WaitForSeconds(1.0f);
            }
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
