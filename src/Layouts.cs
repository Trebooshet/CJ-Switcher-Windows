using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CJSwitcher
{
    internal enum KeyLayout
    {
        En,
        Ru
    }

    internal static class KeyLayoutExtensions
    {
        public static KeyLayout Opposite(this KeyLayout layout)
        {
            return layout == KeyLayout.En ? KeyLayout.Ru : KeyLayout.En;
        }

        public static string Code(this KeyLayout layout)
        {
            return layout == KeyLayout.En ? "en" : "ru";
        }
    }

    /// <summary>Таблицы соответствия раскладок (как в версии для Mac).</summary>
    internal static class Layouts
    {
        private const string EnChars =
            "qwertyuiop[]asdfghjkl;'zxcvbnm,./`"
            + "QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?~"
            + "@#$^&";

        private const string RuChars =
            "йцукенгшщзхъфывапролджэячсмитьбю.ё"
            + "ЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,Ё"
            + "\"№;:?";

        private static readonly Dictionary<char, char> EnToRu = MakeMap(EnChars, RuChars);
        private static readonly Dictionary<char, char> RuToEn = MakeMap(RuChars, EnChars);

        private static Dictionary<char, char> MakeMap(string from, string to)
        {
            var map = new Dictionary<char, char>();
            for (int i = 0; i < from.Length && i < to.Length; i++)
            {
                map[from[i]] = to[i];
            }
            return map;
        }

        private static string Convert(string text, Dictionary<char, char> map)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                char mapped;
                sb.Append(map.TryGetValue(c, out mapped) ? mapped : c);
            }
            return sb.ToString();
        }

        public static string ToRu(string text)
        {
            return Convert(text, EnToRu);
        }

        public static string ToEn(string text)
        {
            return Convert(text, RuToEn);
        }

        /// <summary>Переводит текст, набранный в раскладке source, в противоположную.</summary>
        public static string ConvertFrom(string text, KeyLayout source)
        {
            return source == KeyLayout.En ? ToRu(text) : ToEn(text);
        }

        /// <summary>В какой раскладке написан текст; null, если смесь или нет букв.</summary>
        public static KeyLayout? DetectSource(string text)
        {
            const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            bool hasCyrillic = Regex.IsMatch(text, "[а-яё]", options);
            bool hasLatin = Regex.IsMatch(text, "[a-z]", options);
            if (hasCyrillic == hasLatin) return null;
            return hasCyrillic ? KeyLayout.Ru : KeyLayout.En;
        }
    }
}
