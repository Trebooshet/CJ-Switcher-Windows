using System;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CJSwitcher
{
    /// <summary>Работа с буфером обмена с повторами (буфер бывает занят другой программой).</summary>
    internal static class ClipboardUtil
    {
        private static T Retry<T>(Func<T> action)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return action();
                }
                catch (ExternalException)
                {
                    if (attempt >= 9) throw;
                    Thread.Sleep(15);
                }
            }
        }

        public static void Clear()
        {
            try
            {
                Retry<bool>(delegate { Clipboard.Clear(); return true; });
            }
            catch (Exception ex)
            {
                Log.Write("Буфер обмена (очистка): " + ex.Message);
            }
        }

        public static string GetText()
        {
            try
            {
                return Retry<string>(delegate { return Clipboard.ContainsText() ? Clipboard.GetText() : ""; });
            }
            catch (Exception ex)
            {
                Log.Write("Буфер обмена (чтение): " + ex.Message);
                return "";
            }
        }

        /// <summary>
        /// Кладёт текст в буфер так, чтобы он не попадал в историю буфера Windows (Win+V)
        /// и не синхронизировался с облаком.
        /// </summary>
        public static void SetText(string text)
        {
            try
            {
                Retry<bool>(delegate
                {
                    var data = new DataObject();
                    data.SetText(text, TextDataFormat.UnicodeText);
                    data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
                    data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));
                    Clipboard.SetDataObject(data, true);
                    return true;
                });
            }
            catch (Exception ex)
            {
                Log.Write("Буфер обмена (запись, запасной способ): " + ex.Message);
                try
                {
                    Retry<bool>(delegate { Clipboard.SetText(text); return true; });
                }
                catch (Exception ex2)
                {
                    Log.Write("Буфер обмена (запись): " + ex2.Message);
                }
            }
        }
    }

    /// <summary>Снимок буфера обмена (текст, картинка или список файлов), чтобы вернуть его после вставки.</summary>
    internal sealed class ClipboardSnapshot
    {
        private string text;
        private Image image;
        private string[] files;

        public static ClipboardSnapshot Take()
        {
            var snapshot = new ClipboardSnapshot();
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    StringCollection list = Clipboard.GetFileDropList();
                    snapshot.files = new string[list.Count];
                    list.CopyTo(snapshot.files, 0);
                }
                else if (Clipboard.ContainsImage())
                {
                    snapshot.image = Clipboard.GetImage();
                }
                else if (Clipboard.ContainsText())
                {
                    snapshot.text = Clipboard.GetText();
                }
            }
            catch (Exception ex)
            {
                Log.Write("Снимок буфера: " + ex.Message);
            }
            return snapshot;
        }

        public void Restore()
        {
            try
            {
                if (files != null)
                {
                    var list = new StringCollection();
                    list.AddRange(files);
                    Clipboard.SetFileDropList(list);
                }
                else if (image != null)
                {
                    Clipboard.SetImage(image);
                }
                else if (!string.IsNullOrEmpty(text))
                {
                    ClipboardUtil.SetText(text);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            catch (Exception ex)
            {
                Log.Write("Возврат буфера: " + ex.Message);
            }
        }
    }
}
