using System;
using System.Windows.Interop;
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
                            "Automated Clash Runner - License Notice");
                    }
                    else
                    {
                        DialogService.Instance.ShowWarning(
                            licenseResult.Message ?? "Please connect to the internet to authorize Automated Clash Runner.",
                            "Automated Clash Runner");
                    }
                    return 0;
                }
                // =====================================================

                if (Autodesk.Navisworks.Api.Application.IsAutomated)
                    return 0;

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null || doc.IsClear)
                {
                    DialogService.Instance.ShowWarning(
                        "Please open a Navisworks document (.nwf or .nwd) before running the Automated Clash Runner.",
                        "No Active Document");
                    return 0;
                }

                var window = new MainWindow();
                
                // Set Win32 parent handle so the modal behaves properly over Navisworks
                if (Autodesk.Navisworks.Api.Application.Gui?.MainWindow?.Handle != IntPtr.Zero)
                {
                    var helper = new WindowInteropHelper(window);
                    helper.Owner = Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle;
                }

                window.DataContext = new MainViewModel(() => window.Close());
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

            return 0;
        }
    }
}
