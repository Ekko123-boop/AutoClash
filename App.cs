using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using AutomatedClashRunner.Services;
using AutomatedClashRunner.ViewModels;
using AutomatedClashRunner.Views;

namespace AutomatedClashRunner
{
    [Plugin("AutomatedClashRunner", "ACR", DisplayName = "Automated Clash Runner", ToolTip = "Automated Model Clash Runner & Distiller")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class App : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            LaunchApp(0);
            return 0;
        }

        public static void LaunchApp(int targetTab = 0)
        {
            try
            {
                // ===== PRIMARY LICENSE & REMOTE KILL-SWITCH GATE =====
                var licenseResult = LicenseService.Validate();
                if (!licenseResult.IsAllowed)
                {
                    if (licenseResult.IsRevoked)
                    {
                        DialogService.Instance.ShowWarning(
                            licenseResult.Message ?? "Your access license for Automated Clash Runner has been disabled by the administrator.",
                            "Rimo Tools - License Notice");
                    }
                    return;
                }
                // =====================================================

                if (Autodesk.Navisworks.Api.Application.IsAutomated)
                    return;

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null || doc.IsClear)
                {
                    DialogService.Instance.ShowWarning(
                        "Please open a Navisworks document (.nwf or .nwd) before running the tool.",
                        "No Active Document");
                    return;
                }

                var window = new MainWindow();
                if (Autodesk.Navisworks.Api.Application.Gui?.MainWindow?.Handle != IntPtr.Zero)
                {
                    var helper = new WindowInteropHelper(window);
                    helper.Owner = Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle;
                }

                window.DataContext = new MainViewModel(() => window.Close(), initialTabIndex: targetTab);
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                var sb = new System.Text.StringBuilder();
                var curr = ex;
                while (curr != null)
                {
                    sb.AppendLine($"[{curr.GetType().Name}] {curr.Message}");
                    sb.AppendLine(curr.StackTrace);
                    curr = curr.InnerException;
                }
                LoggerService.LogErrorStatic($"Unhandled exception in plugin execution:\n{sb}");
                DialogService.Instance.ShowError($"Fatal error: {ex.Message}\n\nDetails:\n{ex.InnerException?.Message}");
            }
        }
    }

    [Plugin("RimoRibbonWatcher", "RIMO", DisplayName = "Rimo Ribbon Watcher", ToolTip = "Initializes the dedicated Rimo Ribbon tab")]
    public class RimoRibbonWatcher : EventWatcherPlugin
    {
        private System.Windows.Forms.Timer _timer;
        private int _attempts;

        public override void OnLoaded()
        {
            _attempts = 0;
            _timer = new System.Windows.Forms.Timer { Interval = 300 };
            _timer.Tick += (s, e) =>
            {
                _attempts++;
                bool success = DynamicRibbonService.TryInitializeRibbon(App.LaunchApp);
                if (success || _attempts > 40) // Stop polling after success or 12 seconds
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }
            };
            _timer.Start();
        }

        public override void OnUnloading()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }
    }
}
