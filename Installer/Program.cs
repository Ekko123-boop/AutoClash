using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace AutomatedClashRunner.Installer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("    Automated Clash Runner - Installer");
            Console.WriteLine("    Supports: Autodesk Navisworks 2022-2025");
            Console.WriteLine("==================================================");
            
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string pluginsDir = Path.Combine(appData, @"Autodesk\ApplicationPlugins");
                string bundleDir = Path.Combine(pluginsDir, "AutomatedClashRunner.bundle");

                if (!Directory.Exists(pluginsDir))
                {
                    Directory.CreateDirectory(pluginsDir);
                }

                if (Directory.Exists(bundleDir))
                {
                    Console.WriteLine("[INFO] Previous version found. Removing...");
                    Directory.Delete(bundleDir, true);
                }

                Console.WriteLine("[INFO] Extracting add-in files...");

                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("Installer.bundle.zip"))
                {
                    if (stream == null)
                    {
                        Console.WriteLine("[ERROR] Could not find the embedded bundle.zip.");
                        Console.ReadLine();
                        return;
                    }

                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        archive.ExtractToDirectory(bundleDir);
                    }
                }

                Console.WriteLine("[SUCCESS] Automated Clash Runner installed successfully!");
                Console.WriteLine($"[INFO] Path: {bundleDir}");
                Console.WriteLine("You can now launch Navisworks (2022 to 2025).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Installation failed: {ex.Message}");
                if (ex.Message.Contains("access"))
                {
                    Console.WriteLine("Please ensure Navisworks is completely closed before installing.");
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
