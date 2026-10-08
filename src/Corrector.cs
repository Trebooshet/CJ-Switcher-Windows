using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CJSwitcher
{
    /// <summary>Автозамена по пробелу, Ctrl+Alt+D (добавить слово) и Ctrl+Alt+Z (отмена).</summary>
    internal sealed class Corrector
    {
        private readonly KeyboardHook hook;
        private readonly CustomWords customWords;
        private readonly Detector detector;
        private readonly Toast toast;

        private readonly List<KeyValuePair<KeyLayout, string>> history = new List<KeyValuePair<KeyLayout, string>>();
        private bool busy;

        public Corrector(KeyboardHook hook, CustomWords customWords, Detector detector, Toast toast)
        {
            this.hook = hook;
            this.customWords = customWords;
            this.detector = detector;
            this.toast = toast;
        }

        // ---------- Автозамена ----------

        /// <summary>
        /// Вызывается в хуке при нажатии пробела после слова. Возвращает true, если слово исправлено:
        /// тогда стёрто слово, напечатано исправление и поставлен пробел (одним пакетом),
        /// а настоящий пробел скрыт от приложения.
        /// </summary>
        public bool TryHandleWord(string keys)
        {
            try
            {
                if (busy) return false;

                KeyLayout? layout = InputSource.CurrentLayout();
                if (layout == null) return false;

                Fix fix = detector.Detect(keys, layout.Value);
                if (fix == null) return false;
                if (FrontApp.IsExcluded()) return false;

                Synth.TypeReplacement(keys.Length, fix.Text, true);
                // Переключение раскладки уходит в окно раньше следующих нажатий пользователя.
                InputSource.Select(fix.Layout);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Автозамена: " + ex);
                return false;
            }
        }

        // ---------- Ctrl+Alt+D ----------

        public async void ConvertAndAdd()
        {
            if (busy) return;
            busy = true;
            try
            {
                toast.ShowToast(ToastKind.Added);
                await WaitForModifiersRelease();

                hook.Paused = true;
                ClipboardSnapshot snapshot = ClipboardSnapshot.Take();
                try
                {
                    string selected = await CopySelection();
                    KeyLayout? source = Layouts.DetectSource(selected);
                    if (source == null)
                    {
                        toast.HideToast();
                        return;
                    }

                    KeyLayout target = source.Value.Opposite();
                    string converted = Layouts.ConvertFrom(selected, source.Value);
                    string word = converted.Trim().ToLowerInvariant();

                    if (!Regex.IsMatch(word, @"^\p{L}{2,}$"))
                    {
                        toast.HideToast();
                    }
                    else if (!customWords.Contains(word, target))
                    {
                        customWords.Add(word, target);
                        history.Add(new KeyValuePair<KeyLayout, string>(target, word));
                    }

                    // Выделенный текст заменяется печатью (без вставки из буфера).
                    Synth.TypeText(converted);
                    InputSource.Select(target);
                }
                finally
                {
                    snapshot.Restore();
                    await Task.Delay(100);
                    hook.Paused = false;
                }
            }
            catch (Exception ex)
            {
                Log.Write("Ctrl+Alt+D: " + ex);
            }
            finally
            {
                busy = false;
            }
        }

        // ---------- Ctrl+Alt+Z ----------

        public void UndoLastWord()
        {
            if (history.Count == 0) return;

            KeyValuePair<KeyLayout, string> last = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);

            customWords.Remove(last.Value, last.Key);
            toast.ShowToast(ToastKind.Removed);
        }

        // ---------- Вспомогательное ----------

        private static async Task WaitForModifiersRelease()
        {
            for (int i = 0; i < 20; i++)
            {
                bool held = (Native.GetAsyncKeyState(Native.VK_MENU) & 0x8000) != 0
                            || (Native.GetAsyncKeyState(Native.VK_CONTROL) & 0x8000) != 0;
                if (!held) return;
                await Task.Delay(20);
            }
        }

        private static async Task<string> CopySelection()
        {
            ClipboardUtil.Clear();
            uint before = Native.GetClipboardSequenceNumber();
            Synth.Chord(Native.VK_CONTROL, Native.VK_C);

            for (int i = 0; i < 25; i++)
            {
                await Task.Delay(20);
                if (Native.GetClipboardSequenceNumber() != before)
                {
                    string text = ClipboardUtil.GetText();
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            return "";
        }
    }
}
