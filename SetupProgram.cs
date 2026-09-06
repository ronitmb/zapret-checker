// Setup.exe — классический мастер установки Zapret ALT Checker
// Страницы: приветствие → папка → ярлыки/компоненты → установка → готово.
// Компилируется build.cmd вместе с Program.cs.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZapretSetup
{
    public class SetupForm : Form
    {
        private const string AppLnkName = "Zapret ALT Checker";
        private const string TaskName = "ZapretCheckerAdmin";

        private Panel pageWelcome, pageFolder, pageOptions, pageInstall, pageFinish;
        private TextBox txtPath, txtLog;
        private ComboBox cmbCategory;
        private CheckBox chkDesk, chkStart, chkTask, chkRun;
        private Label lblFinishPath;
        private ProgressBar progress;
        private Button btnBack, btnNext, btnCancel;
        private int page;
        private string exeDst;

        public SetupForm()
        {
            Font = new Font("Segoe UI", 9F);
            Text = "Установка Zapret ALT Checker";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(580, 420);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            pageWelcome = BuildWelcome();
            pageFolder = BuildFolder();
            pageOptions = BuildOptions();
            pageInstall = BuildInstall();
            pageFinish = BuildFinish();
            Controls.AddRange(new Control[] { pageWelcome, pageFolder, pageOptions, pageInstall, pageFinish });

            var strip = new Panel { Location = new Point(0, 372), Size = new Size(580, 48), BackColor = SystemColors.Control };
            btnCancel = new Button { Text = "Отмена", Width = 80, Location = new Point(12, 9) };
            btnCancel.Click += delegate
            {
                if (page == 3 || page == 4 ||
                    MessageBox.Show("Прервать установку?", "Установка",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Close();
            };
            btnBack = new Button { Text = "< Назад", Width = 90, Location = new Point(388, 9) };
            btnBack.Click += delegate { if (page >= 1 && page <= 2) ShowPage(page - 1); };
            btnNext = new Button { Text = "Далее >", Width = 90, Location = new Point(486, 9) };
            btnNext.Click += BtnNext_Click;
            strip.Controls.Add(btnCancel);
            strip.Controls.Add(btnBack);
            strip.Controls.Add(btnNext);
            Controls.Add(strip);

            ShowPage(0);
        }

        private Panel NewPage()
        {
            return new Panel { Location = new Point(0, 0), Size = new Size(580, 372), BackColor = Color.White };
        }

        private Label Header(string text, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, y)
            };
        }

        private Panel BuildWelcome()
        {
            var p = NewPage();
            var t = new Label
            {
                Text = "Zapret ALT Checker",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 28)
            };
            var s = new Label
            {
                Text = "Мастер установки",
                Font = new Font("Segoe UI", 11F),
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(24, 64)
            };
            var body = new Label
            {
                Text = "Программа проверяет, какие пресеты zapret-discord-youtube работают у вас:\n" +
                       "по очереди запускает каждый пресет и тестирует YouTube, Discord,\n" +
                       "Cloudflare, Twitch и Steam, а в конце показывает лучший по пингу.\n\n" +
                       "Требования: Windows 10/11, права администратора (запрашиваются автоматически).\n\n" +
                       "Нажмите «Далее», чтобы продолжить.",
                AutoSize = true,
                Location = new Point(24, 112)
            };
            p.Controls.Add(t);
            p.Controls.Add(s);
            p.Controls.Add(body);
            return p;
        }

        private Panel BuildFolder()
        {
            var p = NewPage();
            p.Controls.Add(Header("Куда установить программу?", 24));

            var l1 = new Label { Text = "Папка:", AutoSize = true, Location = new Point(24, 76) };
            txtPath = new TextBox { Location = new Point(80, 73), Width = 378, Text = "C:\\Program Files\\ZapretChecker" };
            var b = new Button { Text = "Обзор…", Width = 84, Location = new Point(466, 71) };
            b.Click += delegate
            {
                using (var d = new FolderBrowserDialog { Description = "Выберите папку для установки" })
                {
                    if (Directory.Exists(txtPath.Text)) d.SelectedPath = txtPath.Text;
                    if (d.ShowDialog(this) == DialogResult.OK) txtPath.Text = d.SelectedPath;
                }
            };
            p.Controls.Add(l1);
            p.Controls.Add(txtPath);
            p.Controls.Add(b);

            var note = new Label
            {
                Text = "Программа будет установлена для всех пользователей этого компьютера.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Location = new Point(24, 112)
            };
            p.Controls.Add(note);
            return p;
        }

        private Panel BuildOptions()
        {
            var p = NewPage();
            p.Controls.Add(Header("Ярлыки и компоненты", 24));

            chkStart = new CheckBox { Text = "Ярлык в меню «Пуск»", AutoSize = true, Location = new Point(24, 72), Checked = true };
            p.Controls.Add(chkStart);

            var l2 = new Label { Text = "Категория (подпапка) в меню «Пуск»:", AutoSize = true, Location = new Point(44, 102) };
            cmbCategory = new ComboBox { Location = new Point(260, 98), Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
            cmbCategory.Items.Add("Zapret");
            cmbCategory.Items.Add("Сеть");
            cmbCategory.Items.Add("Утилиты");
            cmbCategory.Text = "Zapret";
            p.Controls.Add(l2);
            p.Controls.Add(cmbCategory);

            chkDesk = new CheckBox { Text = "Ярлык на рабочий стол", AutoSize = true, Location = new Point(24, 136), Checked = true };
            p.Controls.Add(chkDesk);

            chkTask = new CheckBox
            {
                Text = "Запуск без окна UAC (задача планировщика)",
                AutoSize = true,
                Location = new Point(24, 168),
                Checked = true
            };
            p.Controls.Add(chkTask);

            var note = new Label
            {
                Text = "Ярлык создаётся как новый файл: существующие значки на рабочем столе\nостанутся на своих местах, ничего не сдвинется.\n\n" +
                       "Если убрать галочку UAC, программу можно запускать только обычным способом\n(со стандартным запросом прав администратора при каждом запуске).",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Location = new Point(24, 204)
            };
            p.Controls.Add(note);
            return p;
        }

        private Panel BuildInstall()
        {
            var p = NewPage();
            p.Controls.Add(Header("Установка…", 24));
            progress = new ProgressBar { Location = new Point(24, 66), Size = new Size(532, 22) };
            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5F),
                BackColor = Color.White,
                Location = new Point(24, 100),
                Size = new Size(532, 246)
            };
            p.Controls.Add(progress);
            p.Controls.Add(txtLog);
            return p;
        }

        private Panel BuildFinish()
        {
            var p = NewPage();
            var t = new Label
            {
                Text = "Установка завершена!",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 140, 76),
                AutoSize = true,
                Location = new Point(24, 32)
            };
            lblFinishPath = new Label { AutoSize = true, Location = new Point(24, 74) };
            chkRun = new CheckBox { Text = "Запустить Zapret ALT Checker", AutoSize = true, Location = new Point(24, 108), Checked = true };
            var note = new Label
            {
                Text = "Запуск без окна UAC: файл run-no-uac.cmd в папке программы\n" +
                       "(или закрепите его ярлык на панели задач).\n\n" +
                       "Удаление: файл uninstall.cmd в папке программы — уберёт ярлыки и задачу.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Location = new Point(24, 148)
            };
            p.Controls.Add(t);
            p.Controls.Add(lblFinishPath);
            p.Controls.Add(chkRun);
            p.Controls.Add(note);
            return p;
        }

        private void ShowPage(int i)
        {
            page = i;
            pageWelcome.Visible = i == 0;
            pageFolder.Visible = i == 1;
            pageOptions.Visible = i == 2;
            pageInstall.Visible = i == 3;
            pageFinish.Visible = i == 4;
            btnBack.Enabled = i == 1 || i == 2;
            btnNext.Enabled = i != 3;
            if (i == 2) btnNext.Text = "Установить";
            else if (i == 4) btnNext.Text = "Готово";
            else btnNext.Text = "Далее >";
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (page == 2) { StartInstall(); return; }
            if (page == 4) { Finish(); return; }
            if (page < 2) ShowPage(page + 1);
        }

        private void Finish()
        {
            bool run = chkRun.Checked;
            string dst = exeDst;
            Close();
            if (run && dst != null && File.Exists(dst))
            {
                try { Process.Start(new ProcessStartInfo(dst) { UseShellExecute = true }); } catch { }
            }
        }

        private void Log(string msg)
        {
            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
        }

        private async void StartInstall()
        {
            ShowPage(3);
            btnNext.Enabled = false;
            btnBack.Enabled = false;
            btnCancel.Text = "Отмена";
            txtLog.Clear();
            progress.Value = 0;
            progress.Maximum = 7;

            string targetDir = txtPath.Text.Trim().TrimEnd('\\');
            string category = string.IsNullOrEmpty(cmbCategory.Text.Trim()) ? "Zapret" : cmbCategory.Text.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) category = category.Replace(c, '_');

            string srcExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ZapretChecker.exe");
            exeDst = Path.GetFullPath(Path.Combine(targetDir, "ZapretChecker.exe"));
            string commonPrograms = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            string catDir = Path.Combine(commonPrograms, category);
            string startLnk = Path.Combine(catDir, AppLnkName + ".lnk");
            string deskLnk = Path.Combine(commonDesktop, AppLnkName + ".lnk");
            string desc = "Проверка пресетов zapret: YouTube / Discord";
            bool ok = true;

            try
            {
                Log("Создание папки: " + targetDir);
                await Task.Run(() => Directory.CreateDirectory(targetDir));
                progress.PerformStep();

                // Программа берётся из файла рядом с Setup.exe, а если его нет —
                // из копии, встроенной прямо в Setup.exe (скачивать больше ничего не нужно).
                bool exeOk = false;
                if (File.Exists(srcExe))
                {
                    Log("Копирование ZapretChecker.exe (файл рядом с Setup.exe)…");
                    await Task.Run(() => File.Copy(srcExe, exeDst, true));
                    exeOk = File.Exists(exeDst);
                }
                else
                {
                    Log("Файл программы не найден рядом — беру встроенную копию из Setup.exe…");
                    byte[] emb = await Task.Run(() => EmbeddedFile("ZapretChecker.exe"));
                    if (emb != null)
                    {
                        await Task.Run(() => File.WriteAllBytes(exeDst, emb));
                        exeOk = true;
                    }
                }
                if (exeOk) Log("   ZapretChecker.exe установлен.");
                else { Log("!! ZapretChecker.exe не найден ни рядом с Setup.exe, ни внутри — установка прервана."); ok = false; }
                progress.PerformStep();

                if (exeOk)
                {
                    if (chkStart.Checked)
                    {
                        Log("Ярлык в меню «Пуск»: " + startLnk);
                        if (!await Task.Run(() => CreateShortcut(startLnk, exeDst, targetDir, desc)))
                        {
                            Log("   !! не удалось создать");
                            ok = false;
                        }
                    }
                    progress.PerformStep();

                    if (chkDesk.Checked)
                    {
                        Log("Ярлык на рабочий стол: " + deskLnk);
                        if (!await Task.Run(() => CreateShortcut(deskLnk, exeDst, targetDir, desc)))
                        {
                            Log("   !! не удалось создать");
                            ok = false;
                        }
                    }
                    progress.PerformStep();

                    if (chkTask.Checked)
                    {
                        Log("Создание задачи «" + TaskName + "» (запуск без UAC)…");
                        string err = await Task.Run(() => CreateNoUacTask(exeDst));
                        if (err == null) Log("   готово — запускайте через run-no-uac.cmd");
                        else { Log("   !! " + err); ok = false; }
                    }
                    progress.PerformStep();

                    Log("Создание run-no-uac.cmd и uninstall.cmd…");
                    await Task.Run(delegate { WriteCmdFiles(targetDir, deskLnk, startLnk, catDir); });
                    progress.PerformStep();
                }

                Log(ok ? "Установка завершена успешно!" : "Установка не завершена — см. сообщения выше.");
            }
            catch (Exception ex)
            {
                Log("!! Ошибка установки: " + ex.Message);
                if (ex.InnerException != null) Log("   " + ex.InnerException.Message);
                ok = false;
            }
            progress.Value = progress.Maximum;

            lblFinishPath.Text = ok
                ? "Установлено в папку: " + targetDir
                : "Установка не выполнена — см. журнал на предыдущем шаге.";
            lblFinishPath.ForeColor = ok ? Color.Black : Color.Firebrick;
            chkRun.Enabled = ok;
            if (!ok) chkRun.Checked = false;
            ShowPage(4);
        }

        // Файл, встроенный в Setup.exe при сборке (/resource:…), либо null
        private static byte[] EmbeddedFile(string name)
        {
            try
            {
                using (Stream s = typeof(SetupForm).Assembly.GetManifestResourceStream(name))
                {
                    if (s == null) return null;
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        // Ярлык создаётся обычной записью .lnk-файла, без вызовов обновления/перестроения
        // рабочего стола — существующие значки не сдвигаются.
        private static bool CreateShortcut(string lnkPath, string exePath, string workDir, string description)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lnkPath));
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object lnk = shellType.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { exePath });
                lt.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { workDir });
                lt.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { description });
                lt.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, lnk, new object[0]);
                return File.Exists(lnkPath);
            }
            catch { return false; }
        }

        private static string CreateNoUacTask(string exePath)
        {
            try
            {
                string ps = Path.Combine(Path.GetTempPath(), "zapretchecker-task.ps1");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("$action = New-ScheduledTaskAction -Execute '" + exePath.Replace("'", "''") + "'");
                sb.AppendLine("$principal = New-ScheduledTaskPrincipal -UserId \"$env:USERDOMAIN\\$env:USERNAME\" -LogonType Interactive -RunLevel Highest");
                sb.AppendLine("$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)");
                sb.AppendLine("Register-ScheduledTask -TaskName '" + TaskName + "' -Action $action -Principal $principal -Settings $settings -Force | Out-Null");
                sb.AppendLine("Write-Output OK");
                WriteTextFile(ps, sb.ToString());

                var pi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + ps + "\"");
                pi.UseShellExecute = false;
                pi.CreateNoWindow = true;
                pi.RedirectStandardOutput = true;
                pi.RedirectStandardError = true;
                using (Process p = Process.Start(pi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit(30000);
                    if (o != null && o.Contains("OK")) return null;
                    return string.IsNullOrEmpty(err) ? "powershell завершился с ошибкой" : err.Trim();
                }
            }
            catch (Exception ex) { return ex.Message; }
        }

        private static void WriteCmdFiles(string targetDir, string deskLnk, string startLnk, string catDir)
        {
            WriteTextFile(Path.Combine(targetDir, "run-no-uac.cmd"),
                "@echo off\r\n" +
                "rem Запускает Zapret ALT Checker с правами администратора БЕЗ окна UAC.\r\n" +
                "schtasks /Run /TN " + TaskName + "\r\n" +
                "if errorlevel 1 (\r\n" +
                "  echo Задача не найдена. Переустановите программу через Setup.exe.\r\n" +
                "  pause\r\n" +
                ")\r\n");

            WriteTextFile(Path.Combine(targetDir, "uninstall.cmd"),
                "@echo off\r\n" +
                "rem Удаляет ярлыки и задачу. Папку программы удалите вручную.\r\n" +
                "chcp 65001 >nul\r\n" +
                "schtasks /Delete /TN " + TaskName + " /F >nul 2>&1\r\n" +
                "del \"" + deskLnk + "\" >nul 2>&1\r\n" +
                "del \"" + startLnk + "\" >nul 2>&1\r\n" +
                "rmdir \"" + catDir + "\" >nul 2>&1\r\n" +
                "echo Готово: ярлыки и задача удалены. Папку программы (и этот файл) можно удалить вручную.\r\n" +
                "pause\r\n");
        }

        private static void WriteTextFile(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}
