using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Стирает backspaces символов, печатает text и (по желанию) ставит пробел, всё ОДНИМ пакетом:
        /// Windows не вклинивает в такой пакет чужие нажатия, поэтому порядок не нарушается,
        /// даже если пользователь уже печатает следующее слово.
        /// Текст вводится как Unicode-символы, без буфера обмена.
        /// </summary>
        public static void TypeReplacement(int backspaces, string text, bool trailingSpace)
        {
            var list = new List<Native.INPUT>();
            for (int i = 0; i < backspaces; i++)
            {
                list.Add(Down(Native.VK_BACK));
                list.Add(Up(Native.VK_BACK));
            }
            foreach (char c in text)
            {
                list.Add(UnicodeKey(c, false));
                list.Add(UnicodeKey(c, true));
            }
            if (trailingSpace)
            {
                list.Add(Down(Native.VK_SPACE));
                list.Add(Up(Native.VK_SPACE));
            }
            Send(list.ToArray());
        }

        /// <summary>Печатает text как Unicode-символы (заменяет выделенный текст).</summary>
        public static void TypeText(string text)
        {
            TypeReplacement(0, text, false);
        }

        private static Native.INPUT UnicodeKey(char c, bool up)
        {
            var input = new Native.INPUT();
            input.type = Native.INPUT_KEYBOARD;
            input.U.ki.wVk = 0;
            input.U.ki.wScan = c;
            input.U.ki.dwFlags = Native.KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0u);
            input.U.ki.time = 0;
            input.U.ki.dwExtraInfo = KeyboardHook.Marker;
            return input;
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
