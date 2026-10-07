using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CJSwitcher
{
    internal enum Hotkey
    {
        AddWord,
        Undo
    }

    /// <summary>
    /// Следит за клавиатурой (низкоуровневый хук), собирает слово и ловит Ctrl+Alt+D / Ctrl+Alt+Z.
    /// Клавиши определяются по физическим скан-кодам, поэтому не зависят от текущей раскладки.
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        /// <summary>Метка на событиях, которые генерирует само приложение.</summary>
        public static readonly UIntPtr Marker = new UIntPtr(0x434A5357u);

        private const int ScanD = 0x20;
        private const int ScanZ = 0x2C;

        // Физическая клавиша (скан-код) -> символы английской раскладки (без Shift, с Shift).
        private static readonly Dictionary<int, string[]> KeyChars = new Dictionary<int, string[]>
        {
            { 0x10, new[] { "q", "Q" } }, { 0x11, new[] { "w", "W" } }, { 0x12, new[] { "e", "E" } },
            { 0x13, new[] { "r", "R" } }, { 0x14, new[] { "t", "T" } }, { 0x15, new[] { "y", "Y" } },
            { 0x16, new[] { "u", "U" } }, { 0x17, new[] { "i", "I" } }, { 0x18, new[] { "o", "O" } },
            { 0x19, new[] { "p", "P" } }, { 0x1A, new[] { "[", "{" } }, { 0x1B, new[] { "]", "}" } },
            { 0x1E, new[] { "a", "A" } }, { 0x1F, new[] { "s", "S" } }, { 0x20, new[] { "d", "D" } },
            { 0x21, new[] { "f", "F" } }, { 0x22, new[] { "g", "G" } }, { 0x23, new[] { "h", "H" } },
            { 0x24, new[] { "j", "J" } }, { 0x25, new[] { "k", "K" } }, { 0x26, new[] { "l", "L" } },
            { 0x27, new[] { ";", ":" } }, { 0x28, new[] { "'", "\"" } }, { 0x29, new[] { "`", "~" } },
            { 0x2C, new[] { "z", "Z" } }, { 0x2D, new[] { "x", "X" } }, { 0x2E, new[] { "c", "C" } },
            { 0x2F, new[] { "v", "V" } }, { 0x30, new[] { "b", "B" } }, { 0x31, new[] { "n", "N" } },
            { 0x32, new[] { "m", "M" } }, { 0x33, new[] { ",", "<" } }, { 0x34, new[] { ".", ">" } },
            { 0x35, new[] { "/", "?" } }
        };

        // Цифры с Shift: 1 2 3 4 6 7 0 (как в версии для Mac).
        private static readonly Dictionary<int, string> ShiftedDigits = new Dictionary<int, string>
        {
            { 0x02, "!" }, { 0x03, "@" }, { 0x04, "#" }, { 0x05, "$" },
            { 0x07, "^" }, { 0x08, "&" }, { 0x0B, ")" }
        };

        private static readonly HashSet<int> ResetKeys = new HashSet<int>
        {
            Native.VK_RETURN, Native.VK_TAB, Native.VK_ESCAPE,
            Native.VK_LEFT, Native.VK_RIGHT, Native.VK_UP, Native.VK_DOWN
        };

        private readonly SynchronizationContext sync;
        private readonly StringBuilder buffer = new StringBuilder();
        private Native.HookProc keyboardProc;
        private Native.HookProc mouseProc;
        private IntPtr keyboardHook = IntPtr.Zero;
        private IntPtr mouseHook = IntPtr.Zero;
        private int heldHotkeyScan;

        public bool AutoCorrect = true;
        public bool Paused;

        public event Action<string> WordTyped;
        public event Action<Hotkey> HotkeyPressed;

        public KeyboardHook(SynchronizationContext sync)
        {
            this.sync = sync;
        }

        public bool IsRunning
        {
            get { return keyboardHook != IntPtr.Zero; }
        }

        public bool Start()
        {
            if (keyboardHook != IntPtr.Zero) return true;

            keyboardProc = KeyboardProc;
            mouseProc = MouseProc;

            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
            {
                IntPtr moduleHandle = Native.GetModuleHandle(module.ModuleName);
                keyboardHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, keyboardProc, moduleHandle, 0);
                mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, moduleHandle, 0);
            }

            if (keyboardHook == IntPtr.Zero)
            {
                Log.Write("Не удалось установить хук клавиатуры, код " + Marshal.GetLastWin32Error());
            }
            return keyboardHook != IntPtr.Zero;
        }

        public void Dispose()
        {
            if (keyboardHook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }
            if (mouseHook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
        }

        private static bool IsDown(int vk)
        {
            return (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    var info = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                    if (info.dwExtraInfo != Marker)
                    {
                        int msg = wParam.ToInt32();
                        bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                        bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

                        if (down && OnKeyDown(info)) return (IntPtr)1;
                        if (up && OnKeyUp(info)) return (IntPtr)1;
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("Хук клавиатуры: " + ex);
                }
            }
            return Native.CallNextHookEx(keyboardHook, nCode, wParam, lParam);
        }

        private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == Native.WM_LBUTTONDOWN || msg == Native.WM_RBUTTONDOWN || msg == Native.WM_MBUTTONDOWN)
                {
                    buffer.Clear();
                }
            }
            return Native.CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        private bool OnKeyUp(Native.KBDLLHOOKSTRUCT info)
        {
            int scan = (int)info.scanCode;
            if (heldHotkeyScan != 0 && scan == heldHotkeyScan)
            {
                heldHotkeyScan = 0;
                return true; // нажатие мы «съели», отпускание тоже
            }
            return false;
        }

        /// <summary>Возвращает true, если нажатие нужно скрыть от приложения.</summary>
        private bool OnKeyDown(Native.KBDLLHOOKSTRUCT info)
        {
            int vk = (int)info.vkCode;
            int scan = (int)info.scanCode;
            bool extended = (info.flags & 1) != 0;

            bool ctrl = IsDown(Native.VK_CONTROL);
            bool alt = IsDown(Native.VK_MENU);
            bool shift = IsDown(Native.VK_SHIFT);
            bool win = IsDown(Native.VK_LWIN) || IsDown(Native.VK_RWIN);

            if (heldHotkeyScan != 0 && !(ctrl && alt)) heldHotkeyScan = 0;

            // Ctrl+Alt+D и Ctrl+Alt+Z работают всегда.
            if (ctrl && alt && !shift && !win && !extended && (scan == ScanD || scan == ScanZ))
            {
                if (heldHotkeyScan != scan)
                {
                    heldHotkeyScan = scan;
                    Hotkey hotkey = scan == ScanD ? Hotkey.AddWord : Hotkey.Undo;
                    sync.Post(delegate { RaiseHotkey(hotkey); }, null);
                }
                return true;
            }

            if (!AutoCorrect || Paused)
            {
                buffer.Clear();
                return false;
            }

            if (ctrl || alt || win)
            {
                buffer.Clear();
                return false;
            }

            // Сам Shift и CapsLock слово не сбрасывают (нужно для знаков вроде «?» в конце слова).
            if (vk == Native.VK_SHIFT || vk == 0xA0 || vk == 0xA1 || vk == 0x14)
            {
                return false;
            }

            if (vk == Native.VK_SPACE)
            {
                if (buffer.Length > 0)
                {
                    string word = buffer.ToString();
                    sync.Post(delegate { RaiseWord(word); }, null);
                }
                buffer.Clear();
            }
            else if (vk == Native.VK_BACK)
            {
                if (buffer.Length > 0) buffer.Length--;
            }
            else if (ResetKeys.Contains(vk))
            {
                buffer.Clear();
            }
            else
            {
                string symbol;
                string[] pair;
                if (!extended && shift && ShiftedDigits.TryGetValue(scan, out symbol))
                {
                    buffer.Append(symbol);
                }
                else if (!extended && KeyChars.TryGetValue(scan, out pair))
                {
                    buffer.Append(shift ? pair[1] : pair[0]);
                }
                else
                {
                    buffer.Clear();
                }
            }
            return false;
        }

        private void RaiseWord(string word)
        {
            Action<string> handler = WordTyped;
            if (handler != null) handler(word);
        }

        private void RaiseHotkey(Hotkey hotkey)
        {
            Action<Hotkey> handler = HotkeyPressed;
            if (handler != null) handler(hotkey);
        }
    }
}
