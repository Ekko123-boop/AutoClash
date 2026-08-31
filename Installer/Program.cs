using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AutomatedClashRunner.Installer
{
    class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        static void Main(string[] args)
        {
            try
            {
                // Check if Navisworks is currently running
                while (true)
                {
                    var roamerProcs = System.Diagnostics.Process.GetProcessesByName("Roamer");
                    if (roamerProcs.Length == 0) break;

                    int res = MessageBox(IntPtr.Zero,
                        "Autodesk Navisworks is currently running.\n\nPlease close Navisworks completely, then click 'OK' to continue the installation.",
                        "Navisworks is Running",
                        0x01 | 0x30); // MB_OKCANCEL | MB_ICONWARNING

                    if (res == 2) // IDCANCEL
                    {
                        return;
                    }
                    System.Threading.Thread.Sleep(500);
                }

                var assembly = Assembly.GetExecutingAssembly();
                Stream stream = assembly.GetManifestResourceStream("bundle.zip") 
                             ?? assembly.GetManifestResourceStream("AutomatedClashRunner.Installer.bundle.zip");

                if (stream == null)
                {
                    foreach (var name in assembly.GetManifestResourceNames())
                    {
                        if (name.EndsWith("bundle.zip", StringComparison.OrdinalIgnoreCase))
                        {
                            stream = assembly.GetManifestResourceStream(name);
                            break;
                        }
                    }
                }

                if (stream == null)
                {
                    MessageBox(IntPtr.Zero, "Installation archive corrupted or missing.", "Error", 0x10);
                    return;
                }

                // Temporary extraction
                string tempDir = Path.Combine(Path.GetTempPath(), "ACR_Install_" + Guid.NewGuid().ToString().Substring(0, 8));
                Directory.CreateDirectory(tempDir);
                
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

                string sourceDll = Path.Combine(tempDir, @"Contents\AutomatedClashRunner\AutomatedClashRunner.dll");
                if (!File.Exists(sourceDll)) sourceDll = Path.Combine(tempDir, @"Contents\AutomatedClashRunner.dll");

                int installedCount = 0;
                List<string> paths = new List<string>();

                // Clean legacy AutomatedClashRunner folders if present
                try
                {
                    string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    string oldGlobalBundle = Path.Combine(progData, @"Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle");
                    if (Directory.Exists(oldGlobalBundle)) Directory.Delete(oldGlobalBundle, true);
                }
                catch { }

                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string oldUserBundle = Path.Combine(appData, @"Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle");
                    if (Directory.Exists(oldUserBundle)) Directory.Delete(oldUserBundle, true);
                }
                catch { }

                // 1. Deploy to Global ApplicationPlugins (ProgramData)
                try
                {
                    string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    string globalBundle = Path.Combine(progData, @"Autodesk\ApplicationPlugins\RimoTools.bundle");
                    if (Directory.Exists(globalBundle)) Directory.Delete(globalBundle, true);
                    CopyDirectory(tempDir, globalBundle);
                    paths.Add("Global ApplicationPlugins (RimoTools.bundle)");
                    installedCount++;
                }
                catch { }

                // 2. Deploy to User ApplicationPlugins (AppData) - Fallback
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string userBundle = Path.Combine(appData, @"Autodesk\ApplicationPlugins\RimoTools.bundle");
                    if (Directory.Exists(userBundle)) Directory.Delete(userBundle, true);
                    CopyDirectory(tempDir, userBundle);
                    paths.Add("User ApplicationPlugins (RimoTools.bundle)");
                    installedCount++;
                }
                catch { }

                // 3. Deploy directly to Navisworks Plugins directories (Program Files)
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string autodeskDir = Path.Combine(programFiles, "Autodesk");
                
                if (Directory.Exists(autodeskDir))
                {
                    foreach (string nwDir in Directory.GetDirectories(autodeskDir, "Navisworks*"))
                    {
                        try
                        {
                            // Clean legacy folder
                            string oldPluginsDir = Path.Combine(nwDir, @"Plugins\AutomatedClashRunner");
                            if (Directory.Exists(oldPluginsDir)) Directory.Delete(oldPluginsDir, true);

                            string pluginsDir = Path.Combine(nwDir, @"Plugins\RimoTools");
                            if (Directory.Exists(pluginsDir)) Directory.Delete(pluginsDir, true);
                            Directory.CreateDirectory(pluginsDir);
                            
                            string sourceContents = Path.Combine(tempDir, @"Contents");
                            if (Directory.Exists(sourceContents))
                            {
                                CopyDirectory(sourceContents, pluginsDir);
                            }

                            paths.Add(Path.GetFileName(nwDir));
                            installedCount++;
                        }
                        catch { }
                    }
                }

                // Cleanup
                try { Directory.Delete(tempDir, true); } catch { }

                if (installedCount > 0)
                {
                    string msg = "Rimo tools was successfully installed!\n\nDeployed to:\n- " + string.Join("\n- ", paths);
                    MessageBox(IntPtr.Zero, msg, "Installation Complete", 0x40);
                }
                else
                {
                    MessageBox(IntPtr.Zero, "Failed to install. Please run as Administrator and ensure Navisworks is closed.", "Installation Failed", 0x10);
                }
            }
            catch (Exception ex)
            {
                MessageBox(IntPtr.Zero, $"Critical Error during installation:\n{ex.Message}", "Fatal Error", 0x10);
            }
        }

        static void CopyDirectory(string sourceDir, string destDir)
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
