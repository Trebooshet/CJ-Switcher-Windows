using System;

namespace CJSwitcher
{
    /// <summary>Чтение и переключение раскладки в активном окне.</summary>
    internal static class InputSource
    {
        private const int PrimaryEn = 0x09;
        private const int PrimaryRu = 0x19;

        public static KeyLayout? CurrentLayout()
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            uint pid;
            uint thread = Native.GetWindowThreadProcessId(hwnd, out pid);
            if (thread == 0) return null;

            return FromHkl(Native.GetKeyboardLayout(thread));
        }

        public static void Select(KeyLayout layout)
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            IntPtr hkl = FindInstalled(layout);
            if (hkl == IntPtr.Zero) return;

            Native.PostMessage(hwnd, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
        }

        private static int PrimaryLanguage(IntPtr hkl)
        {
            int langId = (int)((long)hkl & 0xFFFF);
            return langId & 0x3FF;
        }

        private static KeyLayout? FromHkl(IntPtr hkl)
        {
            int primary = PrimaryLanguage(hkl);
            if (primary == PrimaryRu) return KeyLayout.Ru;
            if (primary == PrimaryEn) return KeyLayout.En;
            return null;
        }

        private static IntPtr FindInstalled(KeyLayout layout)
        {
            int count = Native.GetKeyboardLayoutList(0, null);
            if (count <= 0) return IntPtr.Zero;

            var list = new IntPtr[count];
            Native.GetKeyboardLayoutList(count, list);

            int wanted = layout == KeyLayout.Ru ? PrimaryRu : PrimaryEn;
            foreach (IntPtr hkl in list)
            {
                if (PrimaryLanguage(hkl) == wanted) return hkl;
            }
            return IntPtr.Zero;
        }
    }
}
