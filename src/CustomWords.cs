\xEF\xBB\xBFusing System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CJSwitcher
{
    /// <summary>
    /// Свои слова пользователя. Хранятся в %APPDATA%\CJ-Switcher\custom-words.json
    /// (тот же формат, что в версиях для Mac и Electron).
    /// </summary>
    internal sealed class CustomWords
    {
        private readonly Dictionary<KeyLayout, HashSet<string>> sets = new Dictionary<KeyLayout, HashSet<string>>
        {
            { KeyLayout.En, new HashSet<string>() },
            { KeyLayout.Ru, new HashSet<string>() }
        };

        private readonly string path;

        public CustomWords() : this(null)
        {
        }

        /// <summary>path нужен только для самопроверки; обычно не передаётся.</summary>
        public CustomWords(string path)
        {
            bool standard = path == null;
            if (standard)
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CJ-Switcher");
                try { Directory.CreateDirectory(dir); } catch { }
                path = Path.Combine(dir, "custom-words.json");
            }
            this.path = path;

            Load(this.path);
            if (standard) ImportElectronWordsOnce();
        }

        public bool Contains(string word, KeyLayout layout)
        {
            return sets[layout].Contains(word);
        }

        public void Add(string word, KeyLayout layout)
        {
            sets[layout].Add(word);
            Save();
        }

        public void Remove(string word, KeyLayout layout)
        {
            sets[layout].Remove(word);
            Save();
        }

        private static List<string> ParseArray(string json, string key)
        {
            var result = new List<string>();
            Match array = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            if (!array.Success) return result;

            foreach (Match item in Regex.Matches(array.Groups[1].Value, "\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                result.Add(Regex.Unescape(item.Groups[1].Value));
            }
            return result;
        }

        private void Load(string file)
        {
            try
            {
                if (!File.Exists(file)) return;
                string json = File.ReadAllText(file, Encoding.UTF8);
                foreach (string word in ParseArray(json, "en")) sets[KeyLayout.En].Add(word);
                foreach (string word in ParseArray(json, "ru")) sets[KeyLayout.Ru].Add(word);
            }
            catch (Exception ex)
            {
                Log.Write("Чтение своих слов: " + ex.Message);
            }
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void AppendArray(StringBuilder sb, string key, IEnumerable<string> words, bool last)
        {
            var sorted = new List<string>(words);
            sorted.Sort(StringComparer.Ordinal);

            sb.Append("  \"").Append(key).Append("\": [");
            for (int i = 0; i < sorted.Count; i++)
            {
                sb.Append(i == 0 ? "\n    " : ",\n    ");
                sb.Append('"').Append(Escape(sorted[i])).Append('"');
            }
            sb.Append(sorted.Count > 0 ? "\n  ]" : "]");
            sb.Append(last ? "\n" : ",\n");
        }

        private void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\n");
                AppendArray(sb, "en", sets[KeyLayout.En], false);
                AppendArray(sb, "ru", sets[KeyLayout.Ru], true);
                sb.Append("}\n");

                string temp = path + ".tmp";
                File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(false));
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
            catch (Exception ex)
            {
                Log.Write("Сохранение своих слов: " + ex.Message);
            }
        }

        /// <summary>Один раз подтягивает слова, накопленные в версии на Electron.</summary>
        private void ImportElectronWordsOnce()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string marker = Path.Combine(Path.GetDirectoryName(path), ".imported-electron");
                if (File.Exists(marker)) return;
                File.WriteAllText(marker, "1");

                bool changed = false;
                foreach (string name in new[] { "layout-switcher", "cj-switcher" })
                {
                    string legacy = Path.Combine(appData, name, "custom-words.json");
                    if (!File.Exists(legacy)) continue;

                    string json = File.ReadAllText(legacy, Encoding.UTF8);
                    foreach (string word in ParseArray(json, "en")) changed |= sets[KeyLayout.En].Add(word);
                    foreach (string word in ParseArray(json, "ru")) changed |= sets[KeyLayout.Ru].Add(word);
                }
                if (changed) Save();
            }
            catch (Exception ex)
            {
                Log.Write("Импорт слов из Electron: " + ex.Message);
            }
        }
    }
}
