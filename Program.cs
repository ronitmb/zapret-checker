// Zapret ALT Checker — проверка пресетов zapret-discord-youtube (Flowseal)
// По очереди запускает каждый .bat-пресет и проверяет доступность YouTube/Discord.
// Компиляция: build.cmd

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZapretChecker
{
    public enum TestStatus { Pending, Running, Ok, Slow, Fail }

    public class TestResult
    {
        public string Name;
        public TestStatus Status;
        public int LatencyMs;
        public double SpeedKBps;
        public string Note;
        public string Body;
    }

    public class PresetInfo
    {
        public string Name;
        public string FullPath;   // null для эталона "без обхода"
        public bool IsBaseline;
    }

    public class PresetResult
    {
        public PresetInfo Preset;
        public List<TestResult> Tests = new List<TestResult>();
        public string Verdict = "";
        public bool NetworkBroken;
        public int OkCount;
        public int TotalCount;
        public int CritOk;
        public int SecOk;
        public double AvgLatency;
    }

    public class MainForm : Form
    {
        private TextBox txtDir;
        private Button btnBrowse, btnScan, btnRun, btnStop, btnLaunch;
        private CheckedListBox clbPresets;
        private ListView lv;
        private NumericUpDown numWarmup;
        private CheckBox chkBaseline;
        private CheckBox chkFast;
        private TextBox txtLog;
        private LinkLabel lnkRepo;

        private CancellationTokenSource cts;
        private List<PresetInfo> presets = new List<PresetInfo>();
        private List<PresetResult> lastResults = new List<PresetResult>();
        private bool running;

        public static bool ConsoleMode;

        public MainForm()
        {
            Font = new Font("Segoe UI", 9F);
            Text = "Zapret ALT Checker — проверка пресетов (YouTube / Discord)";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(920, 620);
            Size = new Size(1020, 700);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var lblDir = new Label { Text = "Папка zapret-discord-youtube:", AutoSize = true, Location = new Point(12, 16) };
            txtDir = new TextBox
            {
                Location = new Point(195, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtDir.Width = ClientSize.Width - 195 - 190;
            btnBrowse = new Button { Text = "Обзор…", Width = 80, Location = new Point(ClientSize.Width - 180, 10), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnScan = new Button { Text = "Найти пресеты", Width = 92, Location = new Point(ClientSize.Width - 94, 10), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowse.Click += BtnBrowse_Click;
            btnScan.Click += BtnScan_Click;
            Controls.Add(lblDir); Controls.Add(txtDir); Controls.Add(btnBrowse); Controls.Add(btnScan);

            var lblHint = new Label
            {
                Text = "По очереди запускает каждый пресет и проверяет YouTube/Discord. Нужны права администратора. Во время проверки ваш обход будет переключаться!",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Location = new Point(12, 42)
            };
            Controls.Add(lblHint);

            chkBaseline = new CheckBox { Text = "Сначала проверить БЕЗ обхода (эталон)", AutoSize = true, Location = new Point(12, 97), Checked = true };
            Controls.Add(chkBaseline);

            chkFast = new CheckBox { Text = "Быстрый режим", AutoSize = true, Location = new Point(300, 97) };
            var tip = new ToolTip();
            tip.SetToolTip(chkFast, "Короткие таймауты (3 сек) и прогрев до 4 сек.\nБыстрее, но заблокированные хосты с тихой потерей пакетов могут определиться неточно.");
            Controls.Add(chkFast);

            var lblWarm = new Label { Text = "Прогрев, сек:", AutoSize = true, Location = new Point(430, 98) };
            numWarmup = new NumericUpDown { Minimum = 3, Maximum = 30, Value = 6, Width = 45, Location = new Point(520, 94) };
            Controls.Add(lblWarm); Controls.Add(numWarmup);

            btnRun = new Button { Text = "▶  Проверить", Width = 120, Height = 28, Location = new Point(ClientSize.Width - 320, 64), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnStop = new Button { Text = "■  Стоп", Width = 80, Height = 28, Location = new Point(ClientSize.Width - 194, 64), Anchor = AnchorStyles.Top | AnchorStyles.Right, Enabled = false };
            btnLaunch = new Button { Text = "Запустить выбранный пресет", Width = 200, Height = 28, Location = new Point(12, 64), Enabled = false };
            btnRun.Click += BtnRun_Click;
            btnStop.Click += BtnStop_Click;
            btnLaunch.Click += BtnLaunch_Click;
            Controls.Add(btnRun); Controls.Add(btnStop); Controls.Add(btnLaunch);

            int listTop = 126;
            int listHeight = ClientSize.Height - 160 - listTop - 8;

            clbPresets = new CheckedListBox
            {
                Location = new Point(12, listTop),
                Size = new Size(280, listHeight),
                CheckOnClick = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
                HorizontalScrollbar = true,
                IntegralHeight = false
            };
            Controls.Add(clbPresets);

            lv = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                Location = new Point(300, listTop),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            lv.Size = new Size(ClientSize.Width - 312, listHeight);
            lv.Font = new Font("Segoe UI", 8.25F);
            lv.Columns.Add("Пресет", 150);
            lv.Columns.Add("Итог", 110);
            lv.Columns.Add("Контроль", 76);
            lv.Columns.Add("YT сайт", 80);
            lv.Columns.Add("YT CDN", 98);
            lv.Columns.Add("YT видео", 100);
            lv.Columns.Add("DC API", 78);
            lv.Columns.Add("DC GW", 76);
            lv.Columns.Add("DC CDN", 78);
            lv.Columns.Add("Cloudflare", 85);
            lv.Columns.Add("Twitch", 76);
            lv.Columns.Add("Steam", 76);
            lv.Columns.Add("Ср. пинг", 78);
            lv.SelectedIndexChanged += delegate { btnLaunch.Enabled = !running && lv.SelectedItems.Count > 0; };
            Controls.Add(lv);

            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5F),
                BackColor = Color.White,
                WordWrap = false,
                Location = new Point(12, ClientSize.Height - 160),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            txtLog.Size = new Size(ClientSize.Width - 24, 130);
            Controls.Add(txtLog);

            lnkRepo = new LinkLabel
            {
                Text = "Пресеты: github.com/Flowseal/zapret-discord-youtube",
                AutoSize = true,
                Location = new Point(12, ClientSize.Height - 26),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            lnkRepo.LinkClicked += delegate { try { Process.Start(new ProcessStartInfo("https://github.com/Flowseal/zapret-discord-youtube") { UseShellExecute = true }); } catch { } };
            Controls.Add(lnkRepo);

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LoadSettings();
            string dir = txtDir.Text.Trim();
            if (dir.Length > 0 && Directory.Exists(dir) && ScanPresets(dir).Count > 0)
            {
                ScanAndFill();
                Log("Загружен сохранённый путь: " + dir);
                return;
            }
            dir = AutoDetect();
            if (dir.Length > 0) { txtDir.Text = dir; ScanAndFill(); }
            else Log("Укажите папку zapret-discord-youtube и нажмите «Найти пресеты».");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (running) { if (cts != null) cts.Cancel(); KillZapret(); }
            SaveSettings();
        }

        // ---------- Сканирование ----------

        private string AutoDetect()
        {
            string[] cands = new string[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "zapret-discord-youtube"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "zapret-discord-youtube"),
                "D:\\zapret-discord-youtube",
                "C:\\zapret-discord-youtube",
                "D:\\zapret\\zapret-discord-youtube"
            };
            foreach (string c in cands)
                if (Directory.Exists(c) && ScanPresets(c).Count > 0) return c;
            try
            {
                foreach (string d in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "zapret*"))
                    if (ScanPresets(d).Count > 0) return d;
            }
            catch { }
            return "";
        }

        private static int Rank(string n)
        {
            n = n.ToLowerInvariant();
            if (n.StartsWith("general")) return 0;
            if (n.Contains("discord") && n.Contains("youtube")) return 1;
            if (n.Contains("youtube")) return 2;
            if (n.Contains("discord")) return 3;
            return 4;
        }

        private List<PresetInfo> ScanPresets(string dir)
        {
            var list = new List<PresetInfo>();
            if (dir == null || !Directory.Exists(dir)) return list;
            try
            {
                foreach (string bat in Directory.GetFiles(dir, "*.bat"))
                {
                    string nm = Path.GetFileName(bat);
                    if (nm.ToLowerInvariant() == "service.bat") continue;
                    try
                    {
                        string content = File.ReadAllText(bat);
                        if (content.IndexOf("winws", StringComparison.OrdinalIgnoreCase) >= 0)
                            list.Add(new PresetInfo { Name = nm, FullPath = bat });
                    }
                    catch { }
                }
            }
            catch { }
            list.Sort(delegate (PresetInfo a, PresetInfo b) { return Rank(a.Name).CompareTo(Rank(b.Name)); });
            return list;
        }

        private void ScanAndFill()
        {
            presets = ScanPresets(txtDir.Text.Trim());
            clbPresets.BeginUpdate();
            clbPresets.Items.Clear();
            foreach (PresetInfo p in presets) clbPresets.Items.Add(p.Name, true);
            clbPresets.EndUpdate();
            Log(presets.Count > 0
                ? "Найдено пресетов: " + presets.Count
                : "Пресеты (*.bat с winws) не найдены. Проверьте путь к папке.");
            SaveSettings();
        }

        private void BtnScan_Click(object sender, EventArgs e) { ScanAndFill(); }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog { Description = "Выберите папку zapret-discord-youtube" })
            {
                if (Directory.Exists(txtDir.Text)) dlg.SelectedPath = txtDir.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) { txtDir.Text = dlg.SelectedPath; ScanAndFill(); }
            }
        }

        // ---------- Запуск проверки ----------

        private void BtnRun_Click(object sender, EventArgs e)
        {
            if (!running) StartRun();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (cts != null) cts.Cancel();
        }

        private async void StartRun()
        {
            SaveSettings();
            var toRun = new List<PresetInfo>();
            if (chkBaseline.Checked)
                toRun.Add(new PresetInfo { Name = "БЕЗ ОБХОДА (эталон)", IsBaseline = true });
            foreach (object it in clbPresets.CheckedItems)
            {
                string nm = (string)it;
                foreach (PresetInfo p in presets)
                    if (p.Name == nm) { toRun.Add(p); break; }
            }
            if (toRun.Count == 0)
            {
                MessageBox.Show("Не выбран ни один пресет.", "Zapret Checker");
                return;
            }

            cts = new CancellationTokenSource();
            SetBusy(true);
            lv.Items.Clear();
            foreach (PresetInfo p in toRun) AddRow(p);
            txtLog.Clear();

            int warm = (int)numWarmup.Value;
            bool fast = chkFast.Checked;
            if (fast && warm > 4) warm = 4;
            lastResults.Clear();

            foreach (PresetInfo p in toRun)
            {
                if (cts.IsCancellationRequested) break;
                ListViewItem item = FindItem(p.Name);
                Ui(delegate { if (item != null) item.SubItems[1].Text = "проверка…"; });
                Log("");
                Log("======== " + p.Name + " ========");

                PresetResult res = new PresetResult();
                res.Preset = p;

                if (p.IsBaseline)
                {
                    res.Tests = await RunTestsAsync(res, item, cts.Token, fast);
                }
                else
                {
                    Log("Останавливаю текущий zapret…");
                    await Task.Run((Action)KillZapret);
                    Process proc = StartPreset(p.FullPath, Path.GetDirectoryName(p.FullPath));
                    if (proc == null)
                    {
                        Log("!! Не удалось запустить пресет: " + p.Name);
                        Ui(delegate { if (item != null) item.SubItems[1].Text = "✖ ошибка запуска"; });
                        res.Verdict = "✖ ошибка запуска";
                        lastResults.Add(res);
                        continue;
                    }
                    Log("Прогрев " + warm + " сек…");
                    if (!await Wait(warm * 1000, cts.Token)) { await Task.Run(() => KillTree(proc)); await Task.Run((Action)KillZapret); break; }
                    res.Tests = await RunTestsAsync(res, item, cts.Token, fast);
                    await Task.Run(() => KillTree(proc));
                    await Task.Run((Action)KillZapret);
                }

                FinalizeResult(res);
                lastResults.Add(res);
                UpdateRow(res, item);
                Log("Итог " + p.Name + ": " + res.Verdict);
                if (cts.IsCancellationRequested) break;
                if (!await Wait(500, cts.Token)) break;
            }

            await Task.Run((Action)KillZapret);
            PresetResult best = HighlightBest();
            Summarize();
            SetBusy(false);
            if (best != null && !cts.IsCancellationRequested)
            {
                DialogResult ans = MessageBox.Show(this,
                    "Лучший пресет: " + best.Preset.Name +
                    "\nКритичные тесты: " + best.CritOk + "/3 · доп.: " + best.SecOk + "/6 · ср. пинг: ~" + Math.Round(best.AvgLatency) + " мс" +
                    "\n\nЗапустить его сейчас?",
                    "Проверка завершена", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ans == DialogResult.Yes) { LaunchPreset(best.Preset); return; }
            }
            Log("Готово. Выберите пресет в таблице и нажмите «Запустить выбранный пресет».");
        }

        private void AddRow(PresetInfo p)
        {
            var it = new ListViewItem(p.Name);
            for (int i = 1; i < lv.Columns.Count; i++) it.SubItems.Add("—");
            it.Tag = p;
            if (p.IsBaseline) it.ForeColor = Color.Gray;
            lv.Items.Add(it);
        }

        private ListViewItem FindItem(string name)
        {
            foreach (ListViewItem it in lv.Items)
                if ((string)it.Text == name) return it;
            return null;
        }

        private void SetBusy(bool b)
        {
            running = b;
            btnRun.Enabled = !b;
            btnScan.Enabled = !b;
            btnBrowse.Enabled = !b;
            btnLaunch.Enabled = !b && lv.SelectedItems.Count > 0;
            btnStop.Enabled = b;
        }

        // ---------- Тесты ----------

        // Порядок тестов: 0 контроль, 1 YT сайт, 2 YT CDN, 3 YT видео, 4 DC API,
        // 5 DC GW, 6 DC CDN, 7 Cloudflare, 8 Twitch, 9 Steam
        private async Task<List<TestResult>> RunTestsAsync(PresetResult res, ListViewItem item, CancellationToken ct, bool fast)
        {
            int tShort = fast ? 3000 : 5000;
            int tLong = fast ? 5000 : 7000;
            TaskScheduler sched = TaskScheduler.FromCurrentSynchronizationContext();
            MarkRunning(item);

            Task<TestResult> control = Task.Run(() => HttpProbe("Контроль (ya.ru)", "https://ya.ru/robots.txt", tShort, false, new int[] { 200 }), ct);
            WatchUI(control, item, 2, sched);

            Task<TestResult> ytsite = Task.Run(() => HttpProbe("YouTube — сайт", "https://www.youtube.com/generate_204", tShort, false, new int[] { 204 }), ct);
            WatchUI(ytsite, item, 3, sched);

            Task<TestResult> ytcdn = Task.Run(() => YoutubeCdnTest(tLong), ct);
            WatchUI(ytcdn, item, 4, sched);

            Task<TestResult> ytvideo = Task.Run(() => YoutubeVideoTest(tShort), ct);
            WatchUI(ytvideo, item, 5, sched);

            Task<TestResult> dcapi = Task.Run(() => HttpProbe("Discord — API", "https://discord.com/api/v10/gateway", tShort, false, new int[] { 200 }), ct);
            WatchUI(dcapi, item, 6, sched);

            Task<TestResult> dcgw = Task.Run(() => HttpProbe("Discord — gateway", "https://gateway.discord.gg/?v=10&encoding=json", tShort, false, null), ct);
            WatchUI(dcgw, item, 7, sched);

            Task<TestResult> dccdn = Task.Run(() => HttpProbe("Discord — CDN", "https://cdn.discordapp.com/embed/avatars/0.png", tShort, false, new int[] { 200 }), ct);
            WatchUI(dccdn, item, 8, sched);

            Task<TestResult> cf = Task.Run(() => HttpProbe("Cloudflare", "https://www.cloudflare.com/cdn-cgi/trace", tShort, false, new int[] { 200 }), ct);
            WatchUI(cf, item, 9, sched);

            Task<TestResult> twitch = Task.Run(() => HttpProbe("Twitch", "https://www.twitch.tv/", tShort, false, null), ct);
            WatchUI(twitch, item, 10, sched);

            Task<TestResult> steam = Task.Run(() => HttpProbe("Steam", "https://store.steampowered.com/", tShort, false, null), ct);
            WatchUI(steam, item, 11, sched);

            try { await Task.WhenAll(control, ytsite, ytcdn, ytvideo, dcapi, dcgw, dccdn, cf, twitch, steam); }
            catch (OperationCanceledException) { }

            var list = new List<TestResult>();
            list.Add(Done(control));
            list.Add(Done(ytsite));
            list.Add(Done(ytcdn));
            list.Add(Done(ytvideo));
            list.Add(Done(dcapi));
            list.Add(Done(dcgw));
            list.Add(Done(dccdn));
            list.Add(Done(cf));
            list.Add(Done(twitch));
            list.Add(Done(steam));

            TestResult c = list[0];
            if (c == null || c.Status == TestStatus.Fail)
            {
                Log("!! Контрольная точка недоступна — сеть не работает при этом пресете.");
                res.NetworkBroken = true;
            }
            return list;
        }

        private static TestResult Done(Task<TestResult> t)
        {
            return t.Status == TaskStatus.RanToCompletion ? t.Result : null;
        }

        private void WatchUI(Task<TestResult> t, ListViewItem item, int col, TaskScheduler sched)
        {
            t.ContinueWith(delegate (Task<TestResult> x) { Show(item, col, Done(x)); }, sched);
        }

        private void MarkRunning(ListViewItem item)
        {
            Ui(delegate
            {
                if (item == null) return;
                for (int i = 2; i <= 11; i++)
                    if (item.SubItems.Count > i) item.SubItems[i].Text = "…";
            });
        }

        public static TestResult YoutubeCdnTest(int timeoutMs)
        {
            TestResult r = HttpProbe("YouTube — CDN (i.ytimg.com)", "https://i.ytimg.com/vi/dQw4w9WgXcQ/maxresdefault.jpg", timeoutMs, true, new int[] { 200 });
            if (r.Status == TestStatus.Fail && r.Note != null && r.Note.Contains("404"))
                r = HttpProbe("YouTube — CDN (i.ytimg.com)", "https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg", timeoutMs, true, new int[] { 200 });
            return r;
        }

        public static TestResult HttpProbe(string name, string url, int timeoutMs, bool speedTest, int[] goodCodes)
        {
            var r = new TestResult();
            r.Name = name;
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.DefaultConnectionLimit = 32;
            }
            catch { }
            var sw = Stopwatch.StartNew();
            long total = 0;
            int status = 0;
            string err = null;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZapretChecker/1.0";
                req.AllowAutoRedirect = true;
                req.KeepAlive = false;
                req.Proxy = null; // напрямую, без медленного автоопределения системного прокси
                long cap = speedTest ? 8L * 1024 * 1024 : 65536;
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    r.LatencyMs = (int)sw.ElapsedMilliseconds; // пинг = время до заголовков ответа (TTFB)
                    status = (int)resp.StatusCode;
                    using (Stream s = resp.GetResponseStream())
                    {
                        byte[] buf = new byte[16384];
                        int n;
                        StringBuilder sb = new StringBuilder();
                        while ((n = s.Read(buf, 0, buf.Length)) > 0)
                        {
                            total += n;
                            if (sb.Length < 4096)
                            {
                                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                                if (sb.Length > 4096) sb.Length = 4096;
                            }
                            if (total >= cap) break;
                        }
                        r.Body = sb.ToString();
                    }
                }
            }
            catch (WebException ex)
            {
                var er = ex.Response as HttpWebResponse;
                if (er != null)
                {
                    status = (int)er.StatusCode;
                    try { using (Stream s = er.GetResponseStream()) { if (s != null) r.Body = ReadStringCap(s, 4096); } } catch { }
                }
                else
                {
                    err = DescribeWebException(ex);
                }
            }
            catch (Exception e)
            {
                err = e.Message;
            }
            sw.Stop();

            if (r.LatencyMs == 0) r.LatencyMs = (int)sw.ElapsedMilliseconds; // ветка с ошибкой соединения
            r.SpeedKBps = sw.Elapsed.TotalSeconds > 0.05 ? (total / 1024.0) / sw.Elapsed.TotalSeconds : 0;

            if (err != null)
            {
                r.Status = TestStatus.Fail;
                r.Note = err;
            }
            else if (goodCodes != null && Array.IndexOf(goodCodes, status) < 0)
            {
                r.Status = TestStatus.Fail;
                r.Note = "HTTP " + status + " (неожиданный код)";
            }
            else
            {
                r.Status = TestStatus.Ok;
                r.Note = "HTTP " + status;
                if (speedTest && r.SpeedKBps < 20) r.Status = TestStatus.Slow;
                if (r.LatencyMs > 3000) r.Status = TestStatus.Slow;
            }
            return r;
        }

        private static string ReadStringCap(Stream s, int cap)
        {
            byte[] buf = new byte[8192];
            StringBuilder sb = new StringBuilder();
            int n;
            while ((n = s.Read(buf, 0, buf.Length)) > 0)
            {
                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                if (sb.Length >= cap) break;
            }
            if (sb.Length > cap) sb.Length = cap;
            return sb.ToString();
        }

        private static string DescribeWebException(WebException ex)
        {
            string s = ex.Status.ToString();
            if (ex.InnerException != null && ex.InnerException.Message != null)
                s += ": " + ex.InnerException.Message;
            return s;
        }

        public static TestResult YoutubeVideoTest(int timeoutMs)
        {
            var r = new TestResult();
            r.Name = "YouTube видео (googlevideo)";

            var map = HttpProbe("redirector", "https://redirector.googlevideo.com/report_mapping", timeoutMs, false, null);
            if (map.Status == TestStatus.Fail)
                map = HttpProbe("redirector", "http://redirector.googlevideo.com/report_mapping", timeoutMs, false, null);
            if (map.Status == TestStatus.Fail)
            {
                r.Status = TestStatus.Fail;
                r.Note = "redirector.googlevideo.com недоступен";
                r.LatencyMs = map.LatencyMs;
                return r;
            }

            string host = null;
            if (map.Body != null)
            {
                Match m = Regex.Match(map.Body, @"rr\d+---sn-[a-z0-9\-]+\.googlevideo\.com", RegexOptions.IgnoreCase);
                if (m.Success) host = m.Value;
            }
            string target = host != null
                ? "https://" + host + "/generate_204"
                : "https://redirector.googlevideo.com/generate_204";

            var probe = HttpProbe("video", target, timeoutMs, false, new int[] { 204, 200, 302 });
            r.LatencyMs = probe.LatencyMs;
            r.Status = probe.Status;
            r.Note = (host != null ? host : "без маппинга") + " · " + probe.Note;
            return r;
        }

        // ---------- Отображение ----------

        private void Show(ListViewItem item, int col, TestResult t)
        {
            if (ConsoleMode)
            {
                Console.WriteLine("  " + Pad(t.Name, 30) + " " + CellText(t) + (t.Status == TestStatus.Fail ? "  [" + t.Note + "]" : ""));
                return;
            }
            Ui(delegate
            {
                if (item != null && item.SubItems.Count > col) item.SubItems[col].Text = CellText(t);
                if (t.Status == TestStatus.Fail)
                    Log("  ✖ " + t.Name + " — " + t.Note);
                else if (t.Status == TestStatus.Slow)
                    Log("  ⚠ " + t.Name + " — " + t.Note + " (медленно)");
            });
        }

        private static string Pad(string s, int len)
        {
            if (s == null) return "";
            if (s.Length >= len) return s;
            return s + new string(' ', len - s.Length);
        }

        private static string CellText(TestResult t)
        {
            if (t == null) return "—";
            switch (t.Status)
            {
                case TestStatus.Ok:
                    return "✔ " + t.LatencyMs + "мс" + (t.SpeedKBps > 0.5 ? " · " + Math.Round(t.SpeedKBps) + " КБ/с" : "");
                case TestStatus.Slow:
                    return "⚠ " + t.LatencyMs + "мс" + (t.SpeedKBps > 0.5 ? " · " + Math.Round(t.SpeedKBps) + " КБ/с" : "");
                case TestStatus.Fail:
                    return "✖ " + ShortNote(t.Note);
                case TestStatus.Running:
                    return "…";
                default:
                    return "—";
            }
        }

        private static string ShortNote(string note)
        {
            if (string.IsNullOrEmpty(note)) return "";
            if (note.Length > 24) note = note.Substring(0, 24);
            return note;
        }

        private void FinalizeResult(PresetResult pr)
        {
            if (pr.NetworkBroken)
            {
                pr.Verdict = "⛔ сеть недоступна";
                return;
            }
            int[] crit = new int[] { 1, 3, 4 };                  // YT сайт, YT видео, DC API
            int[] sec = new int[] { 2, 5, 6, 7, 8, 9 };          // YT CDN, DC GW, DC CDN, Cloudflare, Twitch, Steam
            int critOk = 0, secOk = 0, latSum = 0, latN = 0;
            for (int i = 1; i < pr.Tests.Count; i++)
            {
                TestResult t = pr.Tests[i];
                if (t == null) continue;
                if (t.Status == TestStatus.Ok || t.Status == TestStatus.Slow)
                {
                    latSum += t.LatencyMs;
                    latN++;
                }
                if (t.Status == TestStatus.Ok)
                {
                    if (Array.IndexOf(crit, i) >= 0) critOk++;
                    if (Array.IndexOf(sec, i) >= 0) secOk++;
                }
            }
            pr.CritOk = critOk;
            pr.SecOk = secOk;
            pr.TotalCount = 9;
            pr.OkCount = critOk + secOk;
            pr.AvgLatency = latN > 0 ? (double)latSum / latN : 0;
            if (critOk == 3 && secOk >= 4) pr.Verdict = "✔ отлично";
            else if (critOk == 3) pr.Verdict = "✔ хорошо";
            else if (critOk == 2) pr.Verdict = "⚠ частично";
            else if (critOk == 1) pr.Verdict = "✖ почти нет";
            else pr.Verdict = "✖ не работает";
        }

        private void UpdateRow(PresetResult res, ListViewItem item)
        {
            Ui(delegate
            {
                if (item == null) return;
                item.SubItems[1].Text = res.Verdict;
                item.SubItems[12].Text = res.AvgLatency > 0 ? Math.Round(res.AvgLatency) + " мс" : "—";
                if (res.NetworkBroken) item.BackColor = Color.FromArgb(238, 238, 238);
                else if (res.CritOk == 3 && res.SecOk >= 4) item.BackColor = Color.FromArgb(190, 232, 190);
                else if (res.CritOk == 3) item.BackColor = Color.FromArgb(214, 240, 214);
                else if (res.CritOk == 2) item.BackColor = Color.FromArgb(255, 243, 214);
                else item.BackColor = Color.FromArgb(250, 222, 222);
            });
        }

        private PresetResult BestResult()
        {
            PresetResult best = null;
            foreach (PresetResult r in lastResults)
            {
                if (r.Preset.IsBaseline || r.NetworkBroken || r.TotalCount == 0) continue;
                if (best == null
                    || r.CritOk > best.CritOk
                    || (r.CritOk == best.CritOk && r.SecOk > best.SecOk)
                    || (r.CritOk == best.CritOk && r.SecOk == best.SecOk && r.AvgLatency > 0 && r.AvgLatency < best.AvgLatency))
                    best = r;
            }
            return best;
        }

        private PresetResult HighlightBest()
        {
            PresetResult best = BestResult();
            if (best == null) return null;
            Ui(delegate
            {
                ListViewItem item = FindItem(best.Preset.Name);
                if (item != null)
                {
                    item.BackColor = Color.FromArgb(140, 220, 140);
                    item.Font = new Font(lv.Font, FontStyle.Bold);
                }
            });
            Log(">>> Лучший пресет: " + best.Preset.Name
                + " (критичные " + best.CritOk + "/3, доп. " + best.SecOk + "/6, ср. пинг ~" + Math.Round(best.AvgLatency) + " мс)");
            return best;
        }

        private void Summarize()
        {
            var ordered = new List<PresetResult>(lastResults);
            ordered.Sort(delegate (PresetResult a, PresetResult b)
            {
                if (a.NetworkBroken != b.NetworkBroken) return a.NetworkBroken ? 1 : -1;
                if (a.CritOk != b.CritOk) return b.CritOk.CompareTo(a.CritOk);
                if (a.SecOk != b.SecOk) return b.SecOk.CompareTo(a.SecOk);
                return a.AvgLatency.CompareTo(b.AvgLatency);
            });
            Log("");
            Log("---- Рейтинг пресетов (лучшие сверху) ----");
            int place = 1;
            foreach (PresetResult r in ordered)
            {
                Log(Pad(place + ". " + r.Preset.Name, 32) + r.Verdict
                    + "  (крит. " + r.CritOk + "/3, доп. " + r.SecOk + "/6)"
                    + (r.AvgLatency > 0 ? "  ~" + Math.Round(r.AvgLatency) + " мс" : ""));
                place++;
            }
        }

        // ---------- Управление zapret ----------

        private static string RunCmd(string args, int timeoutMs)
        {
            try
            {
                var pi = new ProcessStartInfo("cmd.exe", "/c " + args);
                pi.CreateNoWindow = true;
                pi.UseShellExecute = false;
                pi.RedirectStandardOutput = true;
                pi.RedirectStandardError = true;
                using (Process p = Process.Start(pi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    return outp;
                }
            }
            catch { return ""; }
        }

        private static void KillZapret()
        {
            string[] names = new string[] { "winws.exe", "zapret.exe" };
            foreach (string n in names)
                RunCmd("taskkill /F /IM " + n + " >nul 2>&1", 4000);
            RunCmd("sc stop zapret >nul 2>&1", 5000);
        }

        private static Process StartPreset(string batPath, string workDir)
        {
            try
            {
                var pi = new ProcessStartInfo();
                pi.FileName = batPath;
                pi.WorkingDirectory = workDir;
                pi.UseShellExecute = true;
                pi.WindowStyle = ProcessWindowStyle.Hidden;
                return Process.Start(pi);
            }
            catch { return null; }
        }

        private static void KillTree(Process p)
        {
            if (p == null) return;
            try
            {
                if (!p.HasExited)
                    RunCmd("taskkill /T /F /PID " + p.Id + " >nul 2>&1", 5000);
            }
            catch { }
        }

        // ---------- Прочее ----------

        // Настройки (папка обхода, прогрев, режимы) хранятся в %APPDATA%\ZapretChecker
        private string SettingsPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZapretChecker");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, "settings.txt");
        }

        private void SaveSettings()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("folder=" + txtDir.Text.Trim());
                sb.AppendLine("warmup=" + (int)numWarmup.Value);
                sb.AppendLine("fast=" + (chkFast.Checked ? "1" : "0"));
                sb.AppendLine("baseline=" + (chkBaseline.Checked ? "1" : "0"));
                File.WriteAllText(SettingsPath(), sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                string p = SettingsPath();
                if (!File.Exists(p)) return;
                foreach (string line in File.ReadAllLines(p))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim();
                    string v = line.Substring(i + 1).Trim();
                    if (k == "folder")
                        txtDir.Text = v;
                    else if (k == "warmup")
                    {
                        int n;
                        if (int.TryParse(v, out n))
                        {
                            if (n < 3) n = 3;
                            if (n > 30) n = 30;
                            numWarmup.Value = n;
                        }
                    }
                    else if (k == "fast") chkFast.Checked = v == "1";
                    else if (k == "baseline") chkBaseline.Checked = v == "1";
                }
            }
            catch { }
        }

        private void Ui(Action a)
        {
            if (ConsoleMode) { a(); return; }
            try
            {
                if (InvokeRequired) BeginInvoke(a);
                else a();
            }
            catch { }
        }

        private void Log(string msg)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg;
            if (ConsoleMode) { Console.WriteLine(line); return; }
            Ui(delegate
            {
                txtLog.AppendText(line + Environment.NewLine);
            });
        }

        private static async Task<bool> Wait(int ms, CancellationToken ct)
        {
            try { await Task.Delay(ms, ct); return true; }
            catch (OperationCanceledException) { return false; }
        }

        public void AutoStartCheck()
        {
            if (presets.Count == 0) ScanAndFill();
            StartRun();
        }

        public void LaunchPresetByName(string name)
        {
            foreach (PresetInfo p in presets)
            {
                if (p.Name == name)
                {
                    KillZapret();
                    Process proc = StartPreset(p.FullPath, Path.GetDirectoryName(p.FullPath));
                    Log(proc != null ? "Запущен пресет: " + name : "!! Не удалось запустить: " + name);
                    return;
                }
            }
            Log("Пресет не найден: " + name);
        }

        private void BtnLaunch_Click(object sender, EventArgs e)
        {
            if (lv.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите пресет в таблице.", "Zapret Checker");
                return;
            }
            var p = lv.SelectedItems[0].Tag as PresetInfo;
            if (p == null || p.IsBaseline || p.FullPath == null) return;
            LaunchPreset(p);
        }

        private async void LaunchPreset(PresetInfo p)
        {
            if (p == null || p.IsBaseline || p.FullPath == null) return;
            btnLaunch.Enabled = false;
            Log("Останавливаю текущий zapret…");
            await Task.Run((Action)KillZapret);
            Process proc = StartPreset(p.FullPath, Path.GetDirectoryName(p.FullPath));
            if (proc != null) Log("Запущен пресет: " + p.Name + " (окно скрыто). Проверьте Discord/YouTube.");
            else Log("!! Не удалось запустить: " + p.Name);
            btnLaunch.Enabled = !running && lv.SelectedItems.Count > 0;
        }
    }

    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        private static void Main(string[] args)
        {
            bool consoleMode = args.Length > 0 && args[0] == "--test";
            if (consoleMode)
            {
                MainForm.ConsoleMode = true;
                try { AttachConsole(ATTACH_PARENT_PROCESS); } catch { }
                Console.WriteLine("Проверка БЕЗ обхода (эталон). Ожидайте ~30 сек…");
                List<TestResult> tests = RunBaselineTests();
                Console.WriteLine("");
                foreach (TestResult t in tests)
                    Console.WriteLine("  " + (t.Name.Length >= 34 ? t.Name : t.Name + new string(' ', 34 - t.Name.Length))
                        + " " + (t.Status == TestStatus.Ok ? "OK   " : t.Status == TestStatus.Slow ? "МЕДЛ " : "FAIL ")
                        + t.LatencyMs + "мс" + (t.SpeedKBps > 0.5 ? "  " + Math.Round(t.SpeedKBps) + " КБ/с" : "")
                        + (t.Status == TestStatus.Fail ? "  [" + t.Note + "]" : ""));
                Console.WriteLine("");
                Console.WriteLine("Тесты работают: FAIL/МЕДЛ здесь — состояние без обхода.");
                Console.WriteLine("Запустите ZapretChecker.exe (без аргументов) для проверки каждого пресета.");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm();
            if (args.Length > 0 && args[0] == "--run")
            {
                form.Load += delegate { form.AutoStartCheck(); };
            }
            else if (args.Length > 0 && args[0].StartsWith("--preset:"))
            {
                string pn = args[0].Substring(9);
                form.Load += delegate { form.LaunchPresetByName(pn); };
            }
            Application.Run(form);
        }

        private static TestResult DoneT(Task<TestResult> t)
        {
            return t.Status == TaskStatus.RanToCompletion ? t.Result : null;
        }

        private static List<TestResult> RunBaselineTests()
        {
            var list = new List<TestResult>();

            var t1 = MainForm.HttpProbe("Контроль (ya.ru)", "https://ya.ru/robots.txt", 5000, false, new int[] { 200 });
            list.Add(t1);
            if (t1.Status == TestStatus.Fail)
            {
                Console.WriteLine("!! Сеть недоступна — проверьте интернет.");
                return list;
            }

            Task<TestResult> ytsite = Task.Run(() => MainForm.HttpProbe("YouTube — сайт", "https://www.youtube.com/generate_204", 5000, false, new int[] { 204 }));
            Task<TestResult> ytcdn = Task.Run(() => MainForm.YoutubeCdnTest(7000));
            Task<TestResult> ytvideo = Task.Run(() => MainForm.YoutubeVideoTest(5000));
            Task<TestResult> dcapi = Task.Run(() => MainForm.HttpProbe("Discord — API", "https://discord.com/api/v10/gateway", 5000, false, new int[] { 200 }));
            Task<TestResult> dcgw = Task.Run(() => MainForm.HttpProbe("Discord — gateway", "https://gateway.discord.gg/?v=10&encoding=json", 5000, false, null));
            Task<TestResult> dccdn = Task.Run(() => MainForm.HttpProbe("Discord — CDN", "https://cdn.discordapp.com/embed/avatars/0.png", 5000, false, new int[] { 200 }));
            Task<TestResult> cf = Task.Run(() => MainForm.HttpProbe("Cloudflare", "https://www.cloudflare.com/cdn-cgi/trace", 5000, false, new int[] { 200 }));
            Task<TestResult> twitch = Task.Run(() => MainForm.HttpProbe("Twitch", "https://www.twitch.tv/", 5000, false, null));
            Task<TestResult> steam = Task.Run(() => MainForm.HttpProbe("Steam", "https://store.steampowered.com/", 5000, false, null));

            try { Task.WaitAll(new Task[] { ytsite, ytcdn, ytvideo, dcapi, dcgw, dccdn, cf, twitch, steam }); }
            catch (AggregateException) { }

            list.Add(DoneT(ytsite));
            list.Add(DoneT(ytcdn));
            list.Add(DoneT(ytvideo));
            list.Add(DoneT(dcapi));
            list.Add(DoneT(dcgw));
            list.Add(DoneT(dccdn));
            list.Add(DoneT(cf));
            list.Add(DoneT(twitch));
            list.Add(DoneT(steam));
            return list;
        }
    }
}
