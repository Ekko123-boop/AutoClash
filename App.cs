using System;
using System.Windows.Interop;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using AutomatedClashRunner.Services;
using AutomatedClashRunner.ViewModels;
using AutomatedClashRunner.Views;

namespace AutomatedClashRunner
{
    [Plugin("RimoRibbonCommands", "RIMO", DisplayName = "Rimo Tools", ToolTip = "Rimo Clash Automation Tools")]
    [RibbonLayout("RimoRibbon.xaml")]
    [RibbonTab("ID_RIMO_TAB", DisplayName = "Rimo")]
    [Command("ID_RIMO_CMD_MATRIX", DisplayName = "Clash Matrix", Icon = "Images/icon_matrix_16.png", LargeIcon = "Images/icon_matrix_32.png", ToolTip = "Launch Clash Matrix generator and Tools Test runner")]
    [Command("ID_RIMO_CMD_DISTILL", DisplayName = "Distill Clashes", Icon = "Images/icon_distill_16.png", LargeIcon = "Images/icon_distill_32.png", ToolTip = "Spatial element grouping & clash cluster distillation")]
    [Command("ID_RIMO_CMD_VIEWPOINTS", DisplayName = "Create Viewpoints", Icon = "Images/icon_viewpoints_16.png", LargeIcon = "Images/icon_viewpoints_32.png", ToolTip = "Generate filtered saved viewpoints for clash results")]
    public class App : CommandHandlerPlugin
    {
        public override int ExecuteCommand(string name, params string[] parameters)
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
                    return 0;
                }
                // =====================================================

                if (Autodesk.Navisworks.Api.Application.IsAutomated)
                    return 0;

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null || doc.IsClear)
                {
                    DialogService.Instance.ShowWarning(
                        "Please open a Navisworks document (.nwf or .nwd) before running the tool.",
                        "No Active Document");
                    return 0;
                }

                int targetTab = 0;
                if (string.Equals(name, "ID_RIMO_CMD_DISTILL", StringComparison.OrdinalIgnoreCase))
                {
                    targetTab = 1;
                }
                else if (string.Equals(name, "ID_RIMO_CMD_VIEWPOINTS", StringComparison.OrdinalIgnoreCase))
                {
                    targetTab = 2;
                }

                var window = new MainWindow();
                
                // Set Win32 parent handle so the modal behaves properly over Navisworks
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

            return 0;
        }

        public override CommandState CanExecuteCommand(string name)
        {
            return new CommandState
            {
                IsVisible = true,
                IsEnabled = !Autodesk.Navisworks.Api.Application.IsAutomated,
                IsChecked = false
            };
        }
    }

    [Plugin("AutomatedClashRunner", "ACR", DisplayName = "Automated Clash Runner", ToolTip = "Automated Model Clash Runner & Distiller")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class AddinLegacyEntry : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var app = new App();
            return app.ExecuteCommand("ID_RIMO_CMD_MATRIX", parameters);
        }
    }
}
