\xEF\xBB\xBFusing System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CJSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Contains("--selftest"))
            {
                return SelfTest.Run(args.Contains("--quiet"));
            }

            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\CJ-Switcher-SingleInstance", out createdNew))
            {
                if (!createdNew) return 0;

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    Log.Write("Необработанная ошибка: " + e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Log.Write("Критическая ошибка: " + e.ExceptionObject);
                };

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                Application.Run(new TrayApp());
            }
            return 0;
        }
    }
}
