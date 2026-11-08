using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Logging;

namespace LGTSChinese
{
    /// <summary>Loads external translation tables shipped next to the plugin dll.</summary>
    public class LocData
    {
        private readonly Dictionary<string, string> _grid = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _static = new Dictionary<string, string>();

        public int GridCount => _grid.Count;
        public int StaticCount => _static.Count;

        public static string Unescape(string s)
        {
            if (s == null)
                return null;
            if (s.IndexOf('\\') < 0)
                return s;
            return s.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
        }

        public static LocData Load(string pluginDir, ManualLogSource log)
        {
            var d = new LocData();
            if (pluginDir == null)
                return d;
            d.LoadGrid(Path.Combine(pluginDir, "zh_gridly.tsv"), log);
            d.LoadStatic(Path.Combine(pluginDir, "zh_static.tsv"), log);
            return d;
        }

        private void LoadGrid(string path, ManualLogSource log)
        {
            if (!File.Exists(path))
            {
                log.LogWarning("missing " + path);
                return;
            }
            using (var reader = new StreamReader(path, new UTF8Encoding(false)))
            {
                string line;
                bool first = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (first)
                    {
                        first = false;
                        if (line.StartsWith("grid\t"))
                            continue;
                    }
                    if (line.Length == 0)
                        continue;
                    int t1 = line.IndexOf('\t');
                    if (t1 <= 0)
                        continue;
                    int t2 = line.IndexOf('\t', t1 + 1);
                    if (t2 < 0)
                        continue;
                    string grid = line.Substring(0, t1);
                    string id = line.Substring(t1 + 1, t2 - t1 - 1);
                    string zh = line.Substring(t2 + 1);
                    if (zh.Length == 0)
                        continue;
                    _grid[grid + "\u0001" + id] = Unescape(zh);
                }
            }
        }

        private void LoadStatic(string path, ManualLogSource log)
        {
            if (!File.Exists(path))
            {
                log.LogWarning("missing " + path);
                return;
            }
            using (var reader = new StreamReader(path, new UTF8Encoding(false)))
            {
                string line;
                bool first = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (first)
                    {
                        first = false;
                        if (line.StartsWith("en") || line.StartsWith("count"))
                        {
                            // header may be "count\ten" or "en\ten" etc
                            if (line.EndsWith("zh") || line.EndsWith("en"))
                                continue;
                        }
                    }
                    if (line.Length == 0)
                        continue;
                    int t1 = line.IndexOf('\t');
                    if (t1 <= 0)
                        continue;
                    string en = line.Substring(0, t1);
                    string zh = line.Substring(t1 + 1);
                    if (zh.Length == 0)
                        continue;
                    _static[Unescape(en)] = Unescape(zh);
                }
            }
        }

        public bool TryGetGrid(string grid, string recordId, out string zh)
        {
            return _grid.TryGetValue(grid + "\u0001" + recordId, out zh);
        }

        public string TranslateStatic(string text)
        {
            if (text == null)
                return null;
            string zh;
            return _static.TryGetValue(text, out zh) ? zh : text;
        }
    }
}
