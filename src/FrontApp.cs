using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CJSwitcher
{
    /// <summary>Программы, в которых автозамена отключена (IDE и терминалы).</summary>
    internal static class FrontApp
    {
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "idea64", "idea", "webstorm64", "webstorm", "pycharm64", "pycharm", "phpstorm64",
            "goland64", "rider64", "clion64", "rubymine64", "datagrip64", "studio64",
            "code", "cursor", "devenv",
            "windowsterminal", "cmd", "powershell", "pwsh", "conhost", "mintty", "wezterm-gui", "alacritty"
        };

        public static bool IsExcluded()
        {
            try
            {
                IntPtr hwnd = Native.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;

                uint pid;
                Native.GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return false;

                using (Process process = Process.GetProcessById((int)pid))
                {
                    return Excluded.Contains(process.ProcessName);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
