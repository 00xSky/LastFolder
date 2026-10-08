using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using LastFolder.Native;
using LastFolder.Services;

namespace LastFolder.Helpers
{
    /// <summary>System tray (next to the clock) icon and menu.</summary>
    internal sealed class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly Icon _iconImage;
        private readonly ToolStripMenuItem _openItem;
        private readonly ToolStripMenuItem _autoStartItem;
        private readonly ToolStripMenuItem _languageItem;
        private readonly ToolStripMenuItem _englishItem;
        private readonly ToolStripMenuItem _turkishItem;
        private readonly ToolStripMenuItem _exitItem;

        public TrayIcon(Action onOpen, Action onExit)
        {
            var menu = new ContextMenuStrip();
            _openItem = new ToolStripMenuItem(string.Empty, null, (_, _) => onOpen());
            menu.Items.Add(_openItem);

            var autoStart = new ToolStripMenuItem() { CheckOnClick = true };
            try { autoStart.Checked = AutoStart.IsEnabled; } catch { /* registry could not be read */ }
            autoStart.CheckedChanged += (_, _) =>
            {
                try { AutoStart.Set(autoStart.Checked); } catch { autoStart.Checked = !autoStart.Checked; }
            };
            _autoStartItem = autoStart;
            menu.Items.Add(autoStart);

            _englishItem = new ToolStripMenuItem(Strings.EnglishName, null, (_, _) => Strings.SetLanguage(AppLanguage.English));
            _turkishItem = new ToolStripMenuItem(Strings.TurkishName, null, (_, _) => Strings.SetLanguage(AppLanguage.Turkish));
            _languageItem = new ToolStripMenuItem();
            _languageItem.DropDownItems.Add(_englishItem);
            _languageItem.DropDownItems.Add(_turkishItem);
            menu.Items.Add(_languageItem);

            menu.Items.Add(new ToolStripSeparator());
            _exitItem = new ToolStripMenuItem(string.Empty, null, (_, _) => onExit());
            menu.Items.Add(_exitItem);

            _iconImage = CreateIcon();
            _icon = new NotifyIcon
            {
                Icon = _iconImage,
                ContextMenuStrip = menu,
                Visible = true
            };
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left) onOpen();
            };

            ApplyLanguage();
            Strings.LanguageChanged += ApplyLanguage;
        }

        private void ApplyLanguage()
        {
            _openItem.Text = Strings.TrayOpen;
            _autoStartItem.Text = Strings.TrayAutoStart;
            _languageItem.Text = Strings.TrayLanguage;
            _englishItem.Checked = Strings.Language == AppLanguage.English;
            _turkishItem.Checked = Strings.Language == AppLanguage.Turkish;
            _exitItem.Text = Strings.TrayExit;
            _icon.Text = Strings.TrayTooltip;
        }

        public void ShowBalloon(string title, string text) =>
            _icon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);

        public void Dispose()
        {
            Strings.LanguageChanged -= ApplyLanguage;
            _icon.Visible = false;
            _icon.Dispose();
            _iconImage.Dispose();
        }

        /// <summary>White folder outline inside a blue-gradient rounded square.</summary>
        private static Icon CreateIcon()
        {
            const int size = 32;
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using var bg = RoundedRect(new RectangleF(1, 1, 30, 30), 8);
                using var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(size, size),
                    Color.FromArgb(255, 88, 150, 250), Color.FromArgb(255, 36, 70, 160));
                g.FillPath(brush, bg);

                using var folder = new GraphicsPath();
                folder.AddLines(new[]
                {
                    new PointF(8, 10.5f), new PointF(13, 10.5f), new PointF(15, 12.5f),
                    new PointF(24, 12.5f), new PointF(24, 22), new PointF(8, 22)
                });
                folder.CloseFigure();
                using var pen = new Pen(Color.White, 2.2f) { LineJoin = LineJoin.Round };
                g.DrawPath(pen, folder);
            }

            IntPtr handle = bmp.GetHicon();
            try
            {
                using var temp = Icon.FromHandle(handle);
                return (Icon)temp.Clone();
            }
            finally
            {
                NativeMethods.DestroyIcon(handle);
            }
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
