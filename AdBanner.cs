// AdBanner.cs — маленький зацикленный рекламный баннер (видео) в правом верхнем углу.
// Видео banner.mp4 встроено в ZapretChecker.exe ресурсом и при первом запуске
// распаковывается в %LOCALAPPDATA%\ZapretChecker. Кнопки поверх видео:
// 🔊/🔇 — включить/заглушить звук (запоминается), ✕ — скрыть баннер до следующего запуска.
// Кнопки прозрачные: тёмная подложка появляется только при наведении на них мыши.
// Если видео недоступно или кодеков нет — баннер просто не показывается, на работу не влияет.

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using WpfControls = System.Windows.Controls;
using WpfMedia = System.Windows.Media;

namespace ZapretChecker
{
    internal class AdBannerBox : Panel
    {
        private const string ResourceName = "banner.mp4";

        private ElementHost host;
        private WpfControls.MediaElement media;
        private WpfControls.Border btnMute, btnClose;
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
                media = new WpfControls.MediaElement
                {
                    // Manual с обеих сторон: стартуем и глушим только сами,
                    // иначе WPF на Unloaded может остановить воспроизведение.
                    LoadedBehavior = WpfControls.MediaState.Manual,
                    UnloadedBehavior = WpfControls.MediaState.Manual,
                    Stretch = WpfMedia.Stretch.Uniform,
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

                // Кнопки в том же WPF-дереве, что и видео — только так подложка
                // может быть честно полупрозрачной поверх кадра.
                WpfMedia.Brush hoverBg = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(115, 15, 15, 17));
                if (hoverBg.CanFreeze) hoverBg.Freeze();

                btnMute = MakeOverlayButton(new System.Windows.Thickness(0, 2, 2, 0), 24);
                btnMute.MouseLeftButtonUp += delegate
                {
                    IsMuted = !IsMuted;
                    if (MuteChanged != null) MuteChanged(muted);
                };
                btnMute.ToolTip = "Заглушить звук";

                btnClose = MakeOverlayButton(new System.Windows.Thickness(0, 2, 28, 0), 20);
                btnClose.MouseLeftButtonUp += delegate { try { StopWatchdog(); Visible = false; } catch { } };
                btnClose.ToolTip = "Скрыть баннер (до следующего запуска)";

                var grid = new WpfControls.Grid();
                grid.Children.Add(media);
                grid.Children.Add(btnMute);
                grid.Children.Add(btnClose);

                host.Child = grid;
                Controls.Add(host);
            }
            catch { Visible = false; return; }

            // Сторож: если проигрывание не началось или встало (события WPF иногда
            // теряются при хостинге в WinForms) — доталкиваем Play() сами.
            watchdog = new Timer { Interval = 700 };
            watchdog.Tick += WatchdogTick;
            watchdog.Start();

            UpdateMuteButton();
        }

        // Прозрачная кнопка-плашка с глифом: подложка видна только при наведении
        private static WpfControls.Border MakeOverlayButton(System.Windows.Thickness margin, int width)
        {
            var text = new WpfControls.TextBlock
            {
                FontFamily = new WpfMedia.FontFamily("Segoe UI Symbol"),
                FontSize = 10,
                Foreground = WpfMedia.Brushes.White,
                TextAlignment = System.Windows.TextAlignment.Center
            };
            var border = new WpfControls.Border
            {
                Child = text,
                Width = width,
                Height = 17,
                Margin = margin,
                CornerRadius = new System.Windows.CornerRadius(3),
                Background = WpfMedia.Brushes.Transparent,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            border.MouseEnter += delegate { try { border.Background = OverlayBg; } catch { } };
            border.MouseLeave += delegate { try { border.Background = WpfMedia.Brushes.Transparent; } catch { } };
            return border;
        }

        private static WpfMedia.Brush OverlayBg
        {
            get
            {
                WpfMedia.Brush b = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(115, 15, 15, 17));
                if (b.CanFreeze) b.Freeze();
                return b;
            }
        }

        private void UpdateMuteButton()
        {
            if (btnMute == null) return;
            ((WpfControls.TextBlock)btnMute.Child).Text = muted ? "🔇" : "🔊";
            btnMute.ToolTip = muted ? "Включить звук" : "Заглушить звук";
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
