\xEF\xBB\xBFusing System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CJSwitcher
{
    /// <summary>
    /// Проверка детектора на тех же примерах, что в detector.test.ts.
    /// Запуск: CJ-Switcher.exe --selftest  (добавьте --quiet, чтобы не показывать окно).
    /// Результат всегда записывается в %TEMP%\cj-switcher-selftest.txt
    /// </summary>
    internal static class SelfTest
    {
        public static int Run(bool quiet)
        {
            int passed = 0;
            int total = 0;
            var failures = new List<string>();

            Action<string, bool> check = delegate(string name, bool ok)
            {
                total++;
                if (ok) passed++;
                else failures.Add("НЕ ПРОШЁЛ: " + name);
            };

            try
            {
                string wordsFile = Path.Combine(Path.GetTempPath(), "cj-selftest-words.json");
                if (File.Exists(wordsFile)) File.Delete(wordsFile);

                var custom = new CustomWords(wordsFile);
                Detector detector = Detector.Load(custom);
                check("словари загружены", detector.IsReady);

                Action<string, KeyLayout, string, KeyLayout?> expect =
                    delegate(string keys, KeyLayout layout, string text, KeyLayout? target)
                    {
                        Fix fix = detector.Detect(keys, layout);
                        bool ok = text == null
                            ? fix == null
                            : fix != null && fix.Text == text && (target == null || fix.Layout == target.Value);
                        check(keys + " (" + layout.Code() + ") -> " + (text ?? "ничего"), ok);
                    };

                // исправляет неверную раскладку
                expect("ghbdtn", KeyLayout.En, "привет", KeyLayout.Ru);
                expect("Ghbdtn", KeyLayout.En, "Привет", null);
                expect("hello", KeyLayout.Ru, "hello", KeyLayout.En);

                // не трогает правильные слова
                expect("hello", KeyLayout.En, null, null);
                expect("ghbdtn", KeyLayout.Ru, null, null);
                expect("hello,", KeyLayout.En, null, null);

                // знаки в конце слова
                expect("ghbdtn?", KeyLayout.En, "привет,", null);
                expect("ghbdtn/", KeyLayout.En, "привет.", null);
                expect("hello,", KeyLayout.Ru, "hello,", KeyLayout.En);
                expect("ghbdtn&", KeyLayout.En, "привет?", null);
                expect("ghbdtn!", KeyLayout.En, "привет!", null);
                expect("hello?", KeyLayout.Ru, "hello?", KeyLayout.En);
                expect("ghbdtn&", KeyLayout.Ru, null, null);

                // короткие слова
                expect("d", KeyLayout.En, "в", null);
                expect("z", KeyLayout.En, "я", null);
                expect("lf", KeyLayout.En, "да", null);
                expect("lf?", KeyLayout.En, "да,", null);
                expect("yt", KeyLayout.En, "не", null);
                expect("i", KeyLayout.En, null, null);
                expect("a", KeyLayout.En, null, null);

                // свои слова
                expect("zorblax", KeyLayout.Ru, null, null);
                custom.Add("zorblax", KeyLayout.En);
                expect("zorblax", KeyLayout.Ru, "zorblax", KeyLayout.En);
                custom.Remove("zorblax", KeyLayout.En);
                expect("zorblax", KeyLayout.Ru, null, null);

                // таблицы раскладок
                check("ToRu(ghbdtn)", Layouts.ToRu("ghbdtn") == "привет");
                check("ToEn(привет)", Layouts.ToEn("привет") == "ghbdtn");

                if (File.Exists(wordsFile)) File.Delete(wordsFile);
            }
            catch (Exception ex)
            {
                failures.Add("ОШИБКА: " + ex);
            }

            var report = new StringBuilder();
            report.AppendLine("Пройдено " + passed + " из " + total);
            foreach (string line in failures) report.AppendLine(line);

            string text2 = report.ToString();
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "cj-switcher-selftest.txt"), text2, new UTF8Encoding(true));
            }
            catch
            {
            }

            if (!quiet) MessageBox.Show(text2, "CJ-Switcher: самопроверка");
            return failures.Count == 0 && passed == total ? 0 : 1;
        }
    }
}
