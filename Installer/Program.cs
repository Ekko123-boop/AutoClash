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
            Console.WriteLine("    Automated Clash Runner & Distiller");
            Console.WriteLine("    Installer for Autodesk Navisworks (2022-2026)");
            Console.WriteLine("==================================================");
            Console.WriteLine();
            
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
                    Console.WriteLine("[INFO] Previous version found. Updating bundle...");
                    try
                    {
                        Directory.Delete(bundleDir, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARN] Could not clean previous folder: {ex.Message}");
                    }
                }

                Console.WriteLine("[INFO] Extracting add-in bundle files...");

                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("bundle.zip") ?? assembly.GetManifestResourceStream("AutomatedClashRunner.Installer.bundle.zip"))
                {
                    if (stream == null)
                    {
                        // Fallback search in manifest resource names
                        foreach (var name in assembly.GetManifestResourceNames())
                        {
                            if (name.EndsWith("bundle.zip", StringComparison.OrdinalIgnoreCase))
                            {
                                using (var foundStream = assembly.GetManifestResourceStream(name))
                                {
                                    using (var archive = new ZipArchive(foundStream, ZipArchiveMode.Read))
                                    {
                                        archive.ExtractToDirectory(bundleDir);
                                    }
                                }
                                Console.WriteLine("[SUCCESS] Automated Clash Runner installed successfully!");
                                Console.WriteLine($"[INFO] Deployed to: {bundleDir}");
                                Console.WriteLine();
                                Console.WriteLine("You can now launch Navisworks (2022 to 2026).");
                                Console.WriteLine("Press any key to finish...");
                                Console.ReadKey();
                                return;
                            }
                        }

                        Console.WriteLine("[ERROR] Could not find the embedded bundle.zip in installer resources.");
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                        return;
                    }

                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        archive.ExtractToDirectory(bundleDir);
                    }
                }

                Console.WriteLine("[SUCCESS] Automated Clash Runner installed successfully!");
                Console.WriteLine($"[INFO] Deployed to: {bundleDir}");
                Console.WriteLine();
                Console.WriteLine("You can now launch Navisworks (2022 to 2026).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Installation failed: {ex.Message}");
                if (ex.Message.Contains("access") || ex.Message.Contains("process"))
                {
                    Console.WriteLine("[HINT] Please close Autodesk Navisworks and retry.");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
