// AdBanner.cs — маленький зацикленный рекламный баннер (видео) в правом верхнем углу.
// Видео banner.mp4 встроено в ZapretChecker.exe ресурсом и при первом запуске
// распаковывается в %LOCALAPPDATA%\ZapretChecker. Кнопки поверх видео:
// 🔊/🔇 — включить/заглушить звук (запоминается), ✕ — скрыть баннер до следующего запуска.
// Если видео недоступно или кодеков нет — баннер просто не показывается, на работу не влияет.

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace ZapretChecker
{
    internal class AdBannerBox : Panel
    {
        private const string ResourceName = "banner.mp4";

        private ElementHost host;
        private System.Windows.Controls.MediaElement media;
        private Button btnMute, btnClose;
        private ToolTip tips;
        private Timer watchdog;
        private double lastPos = -1;
        private int stuckTicks;
        private bool muted;

        // Вызывается при переключении звука (true = заглушен), чтобы Main-форма сохранила настройку
        public event Action<bool> MuteChanged;

        public bool IsMuted
        {
            get { return muted; }
            set
            {
                muted = value;
                if (media != null) media.IsMuted = value;
                UpdateMuteButton();
            }
        }

        public AdBannerBox()
        {
            BackColor = Color.Black;
            Size = new Size(100, 50);

            string file = ExtractVideo();
            if (file == null) { Visible = false; return; }

            try
            {
                host = new ElementHost { Dock = DockStyle.Fill };
                media = new System.Windows.Controls.MediaElement
                {
                    // Manual с обеих сторон: стартуем и глушим только сами,
                    // иначе WPF на Unloaded может остановить воспроизведение.
                    LoadedBehavior = System.Windows.Controls.MediaState.Manual,
                    UnloadedBehavior = System.Windows.Controls.MediaState.Manual,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    IsMuted = muted,
                    Volume = 1.0
                };
                media.MediaOpened += delegate { try { media.Play(); } catch { } };
                media.MediaEnded += delegate
                {
                    try { media.Position = TimeSpan.Zero; media.Play(); } catch { }
                };
                media.MediaFailed += delegate
                {
                    try { StopWatchdog(); Visible = false; } catch { }
                };
                media.Source = new Uri(file);
                host.Child = media;
                Controls.Add(host);
            }
            catch { Visible = false; return; }

            // Сторож: если проигрывание не началось или встало (события WPF иногда
            // теряются при хостинге в WinForms) — доталкиваем Play() сами.
            watchdog = new Timer { Interval = 700 };
            watchdog.Tick += WatchdogTick;
            watchdog.Start();

            tips = new ToolTip();
            btnMute = new Button
            {
                Size = new Size(24, 18),
                Location = new Point(Width - 26, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Symbol", 8F),
                TabStop = false
            };
            btnMute.FlatAppearance.BorderSize = 0;
            btnMute.Click += delegate
            {
                IsMuted = !IsMuted;
                if (MuteChanged != null) MuteChanged(muted);
            };
            Controls.Add(btnMute);
            tips.SetToolTip(btnMute, "Заглушить / включить звук");

            btnClose = new Button
            {
                Text = "✕",
                Size = new Size(18, 18),
                Location = new Point(Width - 46, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Symbol", 8F),
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += delegate { StopWatchdog(); Visible = false; };
            Controls.Add(btnClose);
            tips.SetToolTip(btnClose, "Скрыть баннер (до следующего запуска)");

            btnMute.BringToFront();
            btnClose.BringToFront();
            UpdateMuteButton();
        }

        private void WatchdogTick(object sender, EventArgs e)
        {
            try
            {
                if (media == null || !Visible || Disposing || IsDisposed) { StopWatchdog(); return; }
                double pos = media.Position.TotalSeconds;
                if (pos != lastPos) { lastPos = pos; stuckTicks = 0; return; }
                stuckTicks++;
                if (stuckTicks < 3) return;   // ~2 секунды без движения — считаем, что встало
                stuckTicks = 0;
                lastPos = -1;
                if (media.Position.TotalSeconds < 0.05)
                {
                    try { media.Position = TimeSpan.Zero; } catch { }
                }
                try { media.Play(); } catch { }
            }
            catch { }
        }

        private void StopWatchdog()
        {
            try { if (watchdog != null) watchdog.Stop(); } catch { }
        }

        private void UpdateMuteButton()
        {
            if (btnMute == null) return;
            btnMute.Text = muted ? "🔇" : "🔊";
            if (tips != null) tips.SetToolTip(btnMute, muted ? "Включить звук" : "Заглушить звук");
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            try { if (media != null) media.Close(); } catch { }
            base.OnHandleDestroyed(e);
        }

        // Распаковывает встроенное видео; возвращает путь к файлу или null
        private static string ExtractVideo()
        {
            try
            {
                byte[] data;
                using (Stream s = typeof(AdBannerBox).Assembly.GetManifestResourceStream(ResourceName))
                {
                    if (s == null) return null;
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        data = ms.ToArray();
                    }
                }
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ZapretChecker");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, ResourceName);
                if (!File.Exists(path) || new FileInfo(path).Length != data.Length)
                    File.WriteAllBytes(path, data);
                return File.Exists(path) ? path : null;
            }
            catch { return null; }
        }
    }
}
