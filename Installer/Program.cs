using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AutomatedClashRunner.Installer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Handle CLI arguments for silent mode
            if (args.Length > 0)
            {
                string arg = args[0].ToLowerInvariant();
                if (arg == "/install" || arg == "/silent" || arg == "/s")
                {
                    InstallerEngine.PerformInstall(null);
                    return;
                }
                if (arg == "/uninstall" || arg == "/u")
                {
                    InstallerEngine.PerformUninstall(null);
                    return;
                }
            }

            Application.Run(new InstallerForm());
        }
    }

    public class InstallerForm : Form
    {
        private Label lblHeader;
        private Label lblSubtitle;
        private CheckedListBox clbVersions;
        private Label lblDetected;
        private TextBox txtLog;
        private ProgressBar progressBar;
        private Button btnInstall;
        private Button btnUninstall;
        private Button btnClose;

        public InstallerForm()
        {
            InitializeComponent();
            LoadInstalledVersions();
        }

        private void InitializeComponent()
        {
            this.Text = "Rimo Tools Setup (Navisworks 2020-2026)";
            this.Size = new Size(580, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Header Panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.FromArgb(15, 23, 42) // Slate 900
            };

            lblHeader = new Label
            {
                Text = "⚡ RIMO TOOLS SETUP",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 16),
                AutoSize = true
            };

            lblSubtitle = new Label
            {
                Text = "Universal Multi-Version Add-in Installer for Autodesk Navisworks (2020 - 2026)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(22, 48),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblHeader);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // Detected Versions Label
            lblDetected = new Label
            {
                Text = "Detected Navisworks Installations:",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(20, 100),
                AutoSize = true
            };
            this.Controls.Add(lblDetected);

            // CheckedListBox
            clbVersions = new CheckedListBox
            {
                Location = new Point(20, 125),
                Size = new Size(525, 105),
                CheckOnClick = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(clbVersions);

            // Progress Bar
            progressBar = new ProgressBar
            {
                Location = new Point(20, 240),
                Size = new Size(525, 14),
                Style = ProgressBarStyle.Continuous
            };
            this.Controls.Add(progressBar);

            // Log Console
            txtLog = new TextBox
            {
                Location = new Point(20, 265),
                Size = new Size(525, 180),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 8.5F),
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(txtLog);

            // Buttons Panel
            btnInstall = new Button
            {
                Text = "Install / Update",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(16, 185, 129), // Emerald 500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(160, 40),
                Location = new Point(20, 460),
                Cursor = Cursors.Hand
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += async (s, e) => await DoInstallAsync();
            this.Controls.Add(btnInstall);

            btnUninstall = new Button
            {
                Text = "Uninstall",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(239, 68, 68), // Red 500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(130, 40),
                Location = new Point(190, 460),
                Cursor = Cursors.Hand
            };
            btnUninstall.FlatAppearance.BorderSize = 0;
            btnUninstall.Click += async (s, e) => await DoUninstallAsync();
            this.Controls.Add(btnUninstall);

            btnClose = new Button
            {
                Text = "Exit",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 40),
                Location = new Point(445, 460),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        private void LoadInstalledVersions()
        {
            clbVersions.Items.Clear();
            clbVersions.Items.Add("Global Bundle (ProgramData ApplicationPlugins)", true);

            var detected = InstallerEngine.GetInstalledNavisworksDirectories();
            foreach (var dir in detected)
            {
                string name = Path.GetFileName(dir);
                string engine = name.Contains("2024") || name.Contains("2025") || name.Contains("2026") ? "2024 Engine" : "2023 Engine";
                clbVersions.Items.Add($"{name} ({engine})", true);
            }

            Log($"Ready. Found {detected.Count} Navisworks installation(s).");
        }

        private void Log(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => Log(message)));
                return;
            }
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnInstall.Enabled = enabled;
            btnUninstall.Enabled = enabled;
            clbVersions.Enabled = enabled;
        }

        private async Task DoInstallAsync()
        {
            if (InstallerEngine.IsNavisworksRunning())
            {
                MessageBox.Show(
                    "Autodesk Navisworks is currently running.\n\nPlease close Navisworks completely before proceeding with installation.",
                    "Navisworks is Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SetControlsEnabled(false);
            progressBar.Value = 10;
            Log("=== Starting Installation ===");

            bool success = await Task.Run(() => InstallerEngine.PerformInstall(Log));

            progressBar.Value = 100;
            SetControlsEnabled(true);

            if (success)
            {
                Log("=== Installation Completed Successfully! ===");
                MessageBox.Show(
                    "Rimo Tools has been successfully installed!\n\nYou can now launch Autodesk Navisworks.",
                    "Installation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                Log("=== Installation Failed! ===");
                MessageBox.Show(
                    "Installation failed. Please review the log output.",
                    "Installation Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task DoUninstallAsync()
        {
            if (InstallerEngine.IsNavisworksRunning())
            {
                MessageBox.Show(
                    "Autodesk Navisworks is currently running.\n\nPlease close Navisworks completely before proceeding with uninstallation.",
                    "Navisworks is Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "Are you sure you want to completely uninstall Rimo Tools from all Navisworks versions?",
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetControlsEnabled(false);
            progressBar.Value = 10;
            Log("=== Starting Uninstallation ===");

            await Task.Run(() => InstallerEngine.PerformUninstall(Log));

            progressBar.Value = 100;
            SetControlsEnabled(true);

            Log("=== Uninstallation Completed! ===");
            MessageBox.Show(
                "Rimo Tools has been completely removed from all Navisworks versions.",
                "Uninstall Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    public static class InstallerEngine
    {
        public static bool IsNavisworksRunning()
        {
            return Process.GetProcessesByName("Roamer").Length > 0;
        }

        public static List<string> GetInstalledNavisworksDirectories()
        {
            var list = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string autodeskDir = Path.Combine(programFiles, "Autodesk");

            if (Directory.Exists(autodeskDir))
            {
                foreach (string dir in Directory.GetDirectories(autodeskDir, "Navisworks*"))
                {
                    list.Add(dir);
                }
            }
            return list;
        }

        public static bool PerformInstall(Action<string> logger)
        {
            Action<string> log = logger ?? ((m) => { });

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                Stream stream = null;
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("bundle.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        stream = assembly.GetManifestResourceStream(name);
                        break;
                    }
                }

                if (stream == null)
                {
                    log("ERROR: Embedded bundle.zip not found in installer binary.");
                    return false;
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "RimoInstall_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(tempDir);
                log("Extracting installation payload...");

                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string entryPath = Path.Combine(tempDir, entry.FullName);
                        string dirPath = Path.GetDirectoryName(entryPath);
                        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(entryPath, true);
                            try { File.Delete(entryPath + ":Zone.Identifier"); } catch { }
                        }
                    }
                }

                // 1. Deploy Global ProgramData ApplicationPlugins bundle
                try
                {
                    string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    string globalBundle = Path.Combine(progData, @"Autodesk\ApplicationPlugins\RimoNavisTools.bundle");
                    if (Directory.Exists(globalBundle)) Directory.Delete(globalBundle, true);
                    CopyDirectory(tempDir, globalBundle);
                    log("✓ Deployed Global ApplicationPlugins Bundle (ProgramData)");
                }
                catch (Exception ex)
                {
                    log($"⚠ Warning deploying global bundle: {ex.Message}");
                }

                // 2. Deploy User AppData ApplicationPlugins bundle
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string userBundle = Path.Combine(appData, @"Autodesk\ApplicationPlugins\RimoNavisTools.bundle");
                    if (Directory.Exists(userBundle)) Directory.Delete(userBundle, true);
                    CopyDirectory(tempDir, userBundle);
                    log("✓ Deployed User ApplicationPlugins Bundle (AppData)");
                }
                catch (Exception ex)
                {
                    log($"⚠ Warning deploying user bundle: {ex.Message}");
                }

                // 3. Deploy to Program Files Navisworks Plugins directories
                var installedDirs = GetInstalledNavisworksDirectories();
                foreach (string nwDir in installedDirs)
                {
                    string nwName = Path.GetFileName(nwDir);
                    bool is2024Plus = nwName.Contains("2024") || nwName.Contains("2025") || nwName.Contains("2026");
                    string engineFolder = is2024Plus ? "2024" : "2023";

                    try
                    {
                        // Clean legacy folders
                        string old1 = Path.Combine(nwDir, @"Plugins\AutomatedClashRunner");
                        if (Directory.Exists(old1)) Directory.Delete(old1, true);
                        string old2 = Path.Combine(nwDir, @"Plugins\RimoTools");
                        if (Directory.Exists(old2)) Directory.Delete(old2, true);

                        string targetPlugin = Path.Combine(nwDir, @"Plugins\RimoNavisTools");
                        if (Directory.Exists(targetPlugin)) Directory.Delete(targetPlugin, true);
                        Directory.CreateDirectory(targetPlugin);

                        // Copy engine binary
                        string sourceEngine = Path.Combine(tempDir, "Contents", engineFolder);
                        if (Directory.Exists(sourceEngine))
                        {
                            CopyDirectory(sourceEngine, targetPlugin);
                        }

                        // Copy shared Ribbon & Images
                        string sharedRibbon = Path.Combine(tempDir, "en-US");
                        if (Directory.Exists(sharedRibbon))
                        {
                            CopyDirectory(sharedRibbon, Path.Combine(targetPlugin, "en-US"));
                        }
                        string sharedImages = Path.Combine(tempDir, "Images");
                        if (Directory.Exists(sharedImages))
                        {
                            CopyDirectory(sharedImages, Path.Combine(targetPlugin, "Images"));
                        }

                        log($"✓ Installed to {nwName} (Using {engineFolder} Engine)");
                    }
                    catch (Exception ex)
                    {
                        log($"⚠ Error installing to {nwName}: {ex.Message}");
                    }
                }

                // Cleanup temp
                try { Directory.Delete(tempDir, true); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                log($"FATAL ERROR: {ex.Message}");
                return false;
            }
        }

        public static void PerformUninstall(Action<string> logger)
        {
            Action<string> log = logger ?? ((m) => { });

            // 1. Program Files
            var installedDirs = GetInstalledNavisworksDirectories();
            foreach (string nwDir in installedDirs)
            {
                string[] targets = { "RimoNavisTools", "RimoTools", "AutomatedClashRunner" };
                foreach (var t in targets)
                {
                    string p = Path.Combine(nwDir, "Plugins", t);
                    if (Directory.Exists(p))
                    {
                        try { Directory.Delete(p, true); log($"✓ Removed {Path.GetFileName(nwDir)}\\Plugins\\{t}"); } catch { }
                    }
                }
            }

            // 2. ProgramData
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] bundles = { "RimoNavisTools.bundle", "RimoTools.bundle", "AutomatedClashRunner.bundle" };
            foreach (var b in bundles)
            {
                string p = Path.Combine(progData, @"Autodesk\ApplicationPlugins", b);
                if (Directory.Exists(p))
                {
                    try { Directory.Delete(p, true); log($"✓ Removed ProgramData\\...\\{b}"); } catch { }
                }
            }

            // 3. AppData
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (var b in bundles)
            {
                string p = Path.Combine(appData, @"Autodesk\ApplicationPlugins", b);
                if (Directory.Exists(p))
                {
                    try { Directory.Delete(p, true); log($"✓ Removed AppData\\...\\{b}"); } catch { }
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
                try { File.Delete(destFile + ":Zone.Identifier"); } catch { }
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }
    }
}

