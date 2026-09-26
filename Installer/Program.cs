using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
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
            this.Text = "Cypher Generic Clash Setup (Navisworks 2020-2026)";
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
                Text = "⚡ CYPHER GENERIC CLASH SETUP",
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
            clbVersions.Items.Add("Autodesk ApplicationPlugins Bundle (2020-2026 All Engines)", true);

            var detected = InstallerEngine.GetInstalledNavisworksDirectories();
            foreach (var dir in detected)
            {
                string name = Path.GetFileName(dir);
                string engine = name.Contains("2026") ? "2026 Engine (.NET 8)" :
                                name.Contains("2025") ? "2025 Engine (.NET 8)" :
                                name.Contains("2024") ? "2024 Engine (.NET 4.8)" : "2023 Engine (.NET 4.8)";
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

            var selectedTargets = new List<string>();
            foreach (var item in clbVersions.CheckedItems)
            {
                selectedTargets.Add(item.ToString());
            }

            bool success = await Task.Run(() => InstallerEngine.PerformInstall(Log, selectedTargets));

            progressBar.Value = 100;
            SetControlsEnabled(true);

            if (success)
            {
                Log("=== Installation Completed Successfully! ===");
                MessageBox.Show(
                    "Cypher Generic Clash has been successfully installed!\n\nSupported Engines:\n• Navisworks 2020 - 2023 (.NET 4.8 Engine)\n• Navisworks 2024 (.NET 4.8 Engine)\n• Navisworks 2025 (.NET 8 Engine)\n• Navisworks 2026 (.NET 8 Engine)\n\nYou can now launch Autodesk Navisworks.",
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
                "Are you sure you want to completely uninstall Cypher Generic Clash from all Navisworks versions?",
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
                "Cypher Generic Clash has been completely removed from all Navisworks versions.",
                "Uninstall Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    public static class InstallerEngine
    {
        public static bool IsNavisworksRunning()
        {
            try
            {
                return Process.GetProcesses().Any(p =>
                {
                    try
                    {
                        string name = p.ProcessName;
                        return name.IndexOf("roamer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("navisworks", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    catch { return false; }
                });
            }
            catch
            {
                return false;
            }
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

        public static bool PerformInstall(Action<string> logger, List<string> selectedTargets = null)
        {
            Action<string> log = logger ?? ((m) => { });

            try
            {
                int successCount = 0;
                int failureCount = 0;

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

                string tempDir = Path.Combine(Path.GetTempPath(), "CypherInstall_" + Guid.NewGuid().ToString("N").Substring(0, 8));
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

                bool deployGlobal = selectedTargets == null || selectedTargets.Count == 0 ||
                    selectedTargets.Any(x => x.IndexOf("Bundle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             x.IndexOf("ApplicationPlugins", StringComparison.OrdinalIgnoreCase) >= 0);

                if (deployGlobal)
                {
                    // 1. Clean any legacy standalone plugins from Program Files across all versions
                    var allNw = GetInstalledNavisworksDirectories();
                    foreach (string nwDir in allNw)
                    {
                        foreach (var leg in new[] { "CypherNavisTools", "CypherTools", "RimoNavisTools", "RimoTools", "AutomatedClashRunner" })
                        {
                            string old = Path.Combine(nwDir, "Plugins", leg);
                            if (Directory.Exists(old))
                            {
                                SafeDeleteDirectory(old);
                                log($"✓ Cleaned standalone duplicate: {Path.GetFileName(nwDir)}\\Plugins\\{leg}");
                            }
                        }
                    }

                    // 1b. Clean any legacy standalone user plugins from AppData across all versions
                    string userAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string userAutodesk = Path.Combine(userAppData, "Autodesk");
                    if (Directory.Exists(userAutodesk))
                    {
                        foreach (var nwDir in Directory.GetDirectories(userAutodesk, "Navisworks Manage*"))
                        {
                            foreach (var leg in new[] { "CypherNavisTools", "CypherTools", "RimoNavisTools", "RimoTools", "AutomatedClashRunner" })
                            {
                                string old = Path.Combine(nwDir, "Plugins", leg);
                                if (Directory.Exists(old))
                                {
                                    SafeDeleteDirectory(old);
                                    log($"✓ Cleaned AppData duplicate: {Path.GetFileName(nwDir)}\\Plugins\\{leg}");
                                }
                            }
                        }
                    }

                    // 1c. Clean legacy bundle names from both ProgramData and AppData
                    string commonProgData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    var legacyBundleNames = new[] { "CypherNavisTools.bundle", "CypherTools.bundle", "RimoNavisTools.bundle", "RimoTools.bundle", "AutomatedClashRunner.bundle", "CypherNavisTools_backup.bundle" };
                    foreach (var legacyBundle in legacyBundleNames)
                    {
                        string pdOld = Path.Combine(commonProgData, @"Autodesk\ApplicationPlugins", legacyBundle);
                        if (Directory.Exists(pdOld))
                        {
                            SafeDeleteDirectory(pdOld);
                            log($"✓ Purged legacy ProgramData bundle: {legacyBundle}");
                        }
                        string adOld = Path.Combine(userAppData, @"Autodesk\ApplicationPlugins", legacyBundle);
                        if (Directory.Exists(adOld))
                        {
                            SafeDeleteDirectory(adOld);
                            log($"✓ Purged legacy AppData bundle: {legacyBundle}");
                        }
                    }

                    // 2. Deploy Multi-Version Bundle
                    // Authoritative Target #1: Machine-Wide ProgramData (for all users and all Navisworks versions 2020-2026)
                    // Fallback Target #2: User AppData (if ProgramData is inaccessible)
                    // Rule: Single authoritative bundle location. Delete the secondary bundle to prevent duplicate loading collisions.
                    string progDataPlugins = Path.Combine(commonProgData, @"Autodesk\ApplicationPlugins");
                    string progDataBundle = Path.Combine(progDataPlugins, "CypherGenericClash.bundle");
                    string userBundle = Path.Combine(userAppData, @"Autodesk\ApplicationPlugins\CypherGenericClash.bundle");

                    bool deployedToProgramData = false;
                    try
                    {
                        if (!Directory.Exists(progDataPlugins)) Directory.CreateDirectory(progDataPlugins);
                        SafeDeleteDirectory(progDataBundle);
                        CopyDirectory(tempDir, progDataBundle);
                        deployedToProgramData = true;
                        log("✓ Deployed All-Users ApplicationPlugins Bundle (ProgramData)");
                        log($"  Bundle: {progDataBundle}");
                        successCount++;

                        // Clean user AppData bundle to guarantee no dual-bundle collision
                        if (Directory.Exists(userBundle))
                        {
                            SafeDeleteDirectory(userBundle);
                            log("✓ Cleaned user AppData bundle to prevent duplicate ribbon collisions.");
                        }
                    }
                    catch (Exception pdEx)
                    {
                        log($"⚠ Could not deploy to ProgramData ({pdEx.Message}). Falling back to User AppData...");
                    }

                    if (!deployedToProgramData)
                    {
                        try
                        {
                            string appDataPlugins = Path.Combine(userAppData, @"Autodesk\ApplicationPlugins");
                            if (!Directory.Exists(appDataPlugins)) Directory.CreateDirectory(appDataPlugins);
                            SafeDeleteDirectory(userBundle);
                            CopyDirectory(tempDir, userBundle);
                            log("✓ Deployed User ApplicationPlugins Bundle (AppData)");
                            log($"  Bundle: {userBundle}");
                            successCount++;
                        }
                        catch (Exception appEx)
                        {
                            log($"❌ Error deploying bundle to AppData: {appEx.Message}");
                            failureCount++;
                        }
                    }
                }
                else
                {
                    // Deploy to specific targeted Program Files Navisworks Plugins directories only
                    var installedDirs = GetInstalledNavisworksDirectories();
                    foreach (string nwDir in installedDirs)
                    {
                        string nwName = Path.GetFileName(nwDir);
                        bool isTargeted = selectedTargets.Any(x => x.IndexOf(nwName, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!isTargeted)
                        {
                            log($"Skipping {nwName} (unselected in installer)");
                            continue;
                        }

                        string engineFolder;
                        if (nwName.Contains("2026")) engineFolder = "2026";
                        else if (nwName.Contains("2025")) engineFolder = "2025";
                        else if (nwName.Contains("2024")) engineFolder = "2024";
                        else engineFolder = "2023";

                        try
                        {
                            // Clean legacy folders
                            string[] legacy = { "AutomatedClashRunner", "RimoTools", "RimoNavisTools", "CypherTools" };
                            foreach (var leg in legacy)
                            {
                                string old = Path.Combine(nwDir, "Plugins", leg);
                                if (Directory.Exists(old)) SafeDeleteDirectory(old);
                            }

                            string targetPlugin = Path.Combine(nwDir, @"Plugins\CypherNavisTools");
                            if (Directory.Exists(targetPlugin)) SafeDeleteDirectory(targetPlugin);
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
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            log($"⚠ Error installing to {nwName}: {ex.Message}");
                            failureCount++;
                        }
                    }
                }

                // Cleanup temp
                try { SafeDeleteDirectory(tempDir); } catch { }
                return successCount > 0;
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
                string[] targets = { "CypherNavisTools", "CypherTools", "RimoNavisTools", "RimoTools", "AutomatedClashRunner" };
                foreach (var t in targets)
                {
                    string p = Path.Combine(nwDir, "Plugins", t);
                    if (Directory.Exists(p))
                    {
                        SafeDeleteDirectory(p);
                        log($"✓ Removed {Path.GetFileName(nwDir)}\\Plugins\\{t}");
                    }
                }
            }

            // 2. ProgramData
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] bundles = { "CypherGenericClash.bundle", "CypherNavisTools.bundle", "CypherTools.bundle", "RimoNavisTools.bundle", "RimoTools.bundle", "AutomatedClashRunner.bundle" };
            foreach (var b in bundles)
            {
                string p = Path.Combine(progData, @"Autodesk\ApplicationPlugins", b);
                if (Directory.Exists(p))
                {
                    SafeDeleteDirectory(p);
                    log($"✓ Removed ProgramData\\...\\{b}");
                }
            }

            // 3. AppData
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (var b in bundles)
            {
                string p = Path.Combine(appData, @"Autodesk\ApplicationPlugins", b);
                if (Directory.Exists(p))
                {
                    SafeDeleteDirectory(p);
                    log($"✓ Removed AppData\\...\\{b}");
                }
            }

            // Clean AppData user plugin folders
            string autodeskAppData = Path.Combine(appData, "Autodesk");
            if (Directory.Exists(autodeskAppData))
            {
                foreach (var nwDir in Directory.GetDirectories(autodeskAppData, "Navisworks Manage*"))
                {
                    foreach (var t in new[] { "CypherNavisTools", "CypherTools", "RimoNavisTools", "RimoTools", "AutomatedClashRunner" })
                    {
                        string p = Path.Combine(nwDir, "Plugins", t);
                        if (Directory.Exists(p))
                        {
                            SafeDeleteDirectory(p);
                            log($"✓ Removed AppData\\Autodesk\\{Path.GetFileName(nwDir)}\\Plugins\\{t}");
                        }
                    }
                }
            }
        }

        public static void SafeDeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            try
            {
                var dir = new DirectoryInfo(path);
                foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    try { file.Attributes = FileAttributes.Normal; } catch { }
                }
                foreach (var subDir in dir.GetDirectories("*", SearchOption.AllDirectories))
                {
                    try { subDir.Attributes = FileAttributes.Normal; } catch { }
                }
                dir.Attributes = FileAttributes.Normal;
                dir.Delete(true);
            }
            catch
            {
                try { Directory.Delete(path, true); } catch { }
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                try
                {
                    if (File.Exists(destFile))
                    {
                        File.SetAttributes(destFile, FileAttributes.Normal);
                    }
                    File.Copy(file, destFile, true);
                    try { File.Delete(destFile + ":Zone.Identifier"); } catch { }
                }
                catch (Exception copyEx)
                {
                    throw new IOException($"Could not overwrite '{destFile}': {copyEx.Message}", copyEx);
                }
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }
    }
}

