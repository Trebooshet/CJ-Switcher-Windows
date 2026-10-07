using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CJSwitcher
{
    internal enum ToastKind
    {
        Added,
        Removed
    }

    /// <summary>Небольшое всплывающее уведомление внизу справа. Не забирает фокус и пропускает клики.</summary>
    internal sealed class Toast : Form
    {
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private readonly Label label;
        private readonly System.Windows.Forms.Timer holdTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer fadeTimer = new System.Windows.Forms.Timer();

        public Toast()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.FromArgb(38, 38, 42);
            Size = new Size(230, 58);
            Opacity = 0;

            label = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            Controls.Add(label);

            using (var path = RoundedRectangle(new Rectangle(0, 0, Width, Height), 14))
            {
                Region = new Region(path);
            }

            holdTimer.Interval = 1600;
            holdTimer.Tick += delegate
            {
                holdTimer.Stop();
                fadeTimer.Start();
            };

            fadeTimer.Interval = 20;
            fadeTimer.Tick += delegate
            {
                Opacity = Math.Max(0, Opacity - 0.1);
                if (Opacity <= 0.01)
                {
                    fadeTimer.Stop();
                    Hide();
                }
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_TOPMOST;
                return cp;
            }
        }

        public void ShowToast(ToastKind kind)
        {
            label.Text = kind == ToastKind.Added ? "Слово добавлено" : "Слово удалено";

            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Right - Width - 16, area.Top + (int)(area.Height * 0.8) - Height / 2);

            holdTimer.Stop();
            fadeTimer.Stop();
            Opacity = 0.95;
            if (!Visible) Show();
            holdTimer.Start();
        }

        public void HideToast()
        {
            holdTimer.Stop();
            fadeTimer.Stop();
            if (Visible) Hide();
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                holdTimer.Dispose();
                fadeTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
