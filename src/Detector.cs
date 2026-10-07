\xEF\xBB\xBFusing System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CJSwitcher
{
    internal sealed class Fix
    {
        public string Text;
        public KeyLayout Layout;
    }

    /// <summary>Определяет, что слово набрано не в той раскладке (тот же алгоритм, что в версии для Mac).</summary>
    internal sealed class Detector
    {
        private const int MaxTail = 2;
        private const double MinFrequencyRatio = 10;
        private const double CustomWordFrequency = 1000000;

        private static readonly HashSet<char> TailKeys = new HashSet<char>("[]{};:'\",<>./?`~!@#$^&)");

        private static readonly Dictionary<KeyLayout, HashSet<string>> SingleLetterWords =
            new Dictionary<KeyLayout, HashSet<string>>
            {
                { KeyLayout.En, new HashSet<string> { "a", "i" } },
                { KeyLayout.Ru, new HashSet<string> { "а", "в", "и", "к", "о", "с", "у", "я" } }
            };

        private readonly Dictionary<KeyLayout, Dictionary<string, double>> dictionaries;
        private readonly CustomWords customWords;

        public Detector(Dictionary<string, double> en, Dictionary<string, double> ru, CustomWords customWords)
        {
            dictionaries = new Dictionary<KeyLayout, Dictionary<string, double>>
            {
                { KeyLayout.En, en },
                { KeyLayout.Ru, ru }
            };
            this.customWords = customWords;
        }

        /// <summary>Словари берутся из ресурсов самой программы.</summary>
        public static Detector Load(CustomWords customWords)
        {
            return new Detector(ReadList("Dictionaries.en.txt"), ReadList("Dictionaries.ru.txt"), customWords);
        }

        public bool IsReady
        {
            get { return dictionaries[KeyLayout.En].Count > 0 && dictionaries[KeyLayout.Ru].Count > 0; }
        }

        private static Dictionary<string, double> ReadList(string resource)
        {
            var list = new Dictionary<string, double>(150000);
            using (Stream stream = typeof(Detector).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                {
                    Log.Write("Нет словаря в ресурсах: " + resource);
                    return list;
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split(' ');
                        if (parts[0].Length == 0) continue;

                        double count;
                        if (parts.Length < 2 ||
                            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out count))
                        {
                            count = double.NaN;
                        }
                        list[parts[0].ToLowerInvariant()] = count;
                    }
                }
            }
            return list;
        }

        /// <summary>keys: набранные клавиши в символах английской раскладки; layout: текущая раскладка.</summary>
        public Fix Detect(string keys, KeyLayout layout)
        {
            KeyLayout target = layout.Opposite();

            for (int tailLength = 0; tailLength <= MaxTail && tailLength < keys.Length; tailLength++)
            {
                string baseKeys = keys.Substring(0, keys.Length - tailLength);
                string tailKeys = keys.Substring(keys.Length - tailLength);
                if (!tailKeys.All(c => TailKeys.Contains(c))) break;

                double typedFrequency = FrequencyOf(Screen(baseKeys, layout), layout);
                string fixedText = Screen(baseKeys, target);
                double fixedFrequency = FrequencyOf(fixedText, target);

                if (fixedFrequency > typedFrequency * MinFrequencyRatio)
                {
                    return new Fix { Text = fixedText + Screen(tailKeys, target), Layout = target };
                }
                if (typedFrequency > 0) return null;
            }
            return null;
        }

        /// <summary>Что пользователь видит на экране, если набрал клавиши keys в раскладке layout.</summary>
        private static string Screen(string keys, KeyLayout layout)
        {
            return layout == KeyLayout.Ru ? Layouts.ToRu(keys) : keys;
        }

        private double FrequencyOf(string word, KeyLayout layout)
        {
            string lower = word.ToLowerInvariant();
            if (customWords.Contains(lower, layout)) return CustomWordFrequency;
            if (lower.Length == 1)
            {
                return SingleLetterWords[layout].Contains(lower) ? double.PositiveInfinity : 0;
            }

            double value;
            return dictionaries[layout].TryGetValue(lower, out value) ? value : 0;
        }
    }
}
