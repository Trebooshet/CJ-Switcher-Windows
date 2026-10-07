using System;
using System.Runtime.InteropServices;

namespace CJSwitcher
{
    /// <summary>Отправка синтетических нажатий клавиш.</summary>
    internal static class Synth
    {
        public static void Tap(int vk)
        {
            Send(Down(vk), Up(vk));
        }

        public static void Chord(int modifier, int vk)
        {
            Send(Down(modifier), Down(vk), Up(vk), Up(modifier));
        }

        private static Native.INPUT Down(int vk)
        {
            return Make(vk, 0);
        }

        private static Native.INPUT Up(int vk)
        {
            return Make(vk, Native.KEYEVENTF_KEYUP);
        }

        private static Native.INPUT Make(int vk, uint flags)
        {
            var input = new Native.INPUT();
            input.type = Native.INPUT_KEYBOARD;
            input.U.ki.wVk = (ushort)vk;
            input.U.ki.wScan = (ushort)Native.MapVirtualKey((uint)vk, 0);
            input.U.ki.dwFlags = flags;
            input.U.ki.time = 0;
            input.U.ki.dwExtraInfo = KeyboardHook.Marker;
            return input;
        }

        private static void Send(params Native.INPUT[] inputs)
        {
            uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
            if (sent != inputs.Length)
            {
                Log.Write("SendInput отправил " + sent + " из " + inputs.Length
                    + " (возможно, активно окно с повышенными правами)");
            }
        }
    }
}
