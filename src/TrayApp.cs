using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CJSwitcher
{
    /// <summary>Значок в трее, меню и связывание всех частей.</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem statusItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem autoItem = new ToolStripMenuItem("Автозамена");
        private readonly ToolStripMenuItem loginItem = new ToolStripMenuItem("Запускать при входе в Windows");

        private readonly KeyboardHook hook;
        private readonly CustomWords customWords = new CustomWords();
        private readonly Detector detector;
        private readonly Toast toast = new Toast();
        private readonly Corrector corrector;

        public TrayApp()
        {
            hook = new KeyboardHook(SynchronizationContext.Current ?? new SynchronizationContext());
            detector = Detector.Load(customWords);
            corrector = new Corrector(hook, customWords, detector, toast);

            hook.AutoCorrect = Settings.AutoCorrect;
            hook.WordTyped += corrector.HandleWord;
            hook.HotkeyPressed += OnHotkey;

            statusItem.Enabled = false;

            autoItem.Click += delegate
            {
                hook.AutoCorrect = !hook.AutoCorrect;
                Settings.AutoCorrect = hook.AutoCorrect;
                RefreshMenu();
            };

            loginItem.Click += delegate
            {
                Settings.AutoStart = !Settings.AutoStart;
                RefreshMenu();
            };

            var licenses = new ToolStripMenuItem("Лицензии…");
            licenses.Click += delegate { OpenLicenses(); };

            var log = new ToolStripMenuItem("Журнал ошибок…");
            log.Click += delegate { OpenLog(); };

            var quit = new ToolStripMenuItem("Выйти");
            quit.Click += delegate { Quit(); };

            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(autoItem);
            menu.Items.Add(loginItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(licenses);
            menu.Items.Add(log);
            menu.Items.Add(quit);
            menu.Opening += delegate { RefreshMenu(); };

            tray = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "CJ-Switcher",
                ContextMenuStrip = menu,
                Visible = true
            };

            hook.Start();
            RefreshMenu();
        }

        private void OnHotkey(Hotkey hotkey)
        {
            if (hotkey == Hotkey.AddWord) corrector.ConvertAndAdd();
            else corrector.UndoLastWord();
        }

        private void RefreshMenu()
        {
            if (!hook.IsRunning) statusItem.Text = "Не удалось включить перехват клавиш";
            else if (!detector.IsReady) statusItem.Text = "Работает, но словари не найдены";
            else statusItem.Text = "Работает";

            autoItem.Checked = hook.AutoCorrect;
            loginItem.Checked = Settings.AutoStart;
        }

        private static Icon LoadIcon()
        {
            using (Stream stream = typeof(TrayApp).Assembly.GetManifestResourceStream("icon.ico"))
            {
                if (stream != null) return new Icon(stream, SystemInformation.SmallIconSize);
            }
            return SystemIcons.Application;
        }

        private static void OpenLicenses()
        {
            try
            {
                using (Stream stream = typeof(TrayApp).Assembly.GetManifestResourceStream("THIRD_PARTY_NOTICES.txt"))
                {
                    if (stream == null) return;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string path = Path.Combine(Path.GetTempPath(), "CJ-Switcher-THIRD_PARTY_NOTICES.txt");
                        File.WriteAllText(path, reader.ReadToEnd(), new UTF8Encoding(true));
                        Process.Start(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Лицензии: " + ex.Message);
            }
        }

        private static void OpenLog()
        {
            try
            {
                if (!File.Exists(Log.FilePath)) Log.Write("Журнал создан.");
                Process.Start(Log.FilePath);
            }
            catch (Exception ex)
            {
                Log.Write("Журнал: " + ex.Message);
            }
        }

        private void Quit()
        {
            tray.Visible = false;
            hook.Dispose();
            toast.Dispose();
            tray.Dispose();
            ExitThread();
        }
    }
}
