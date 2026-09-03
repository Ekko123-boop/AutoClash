using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Interop;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using AutomatedClashRunner.Services;
using AutomatedClashRunner.ViewModels;
using AutomatedClashRunner.Views;

namespace AutomatedClashRunner
{
    // =========================================================================
    // 1. Dedicated "Cypher" Ribbon Tab via Official Navisworks CommandHandlerPlugin
    // =========================================================================
    [Plugin("CypherNavisRibbon", "CYPH", DisplayName = "Cypher Tools")]
    [RibbonLayout("CypherRibbon.xaml")]
    [RibbonTab("Cypher", DisplayName = "Cypher")]
    [Command("ID_CYPHER_CMD_MATRIX",
             DisplayName = "Clash Matrix",
             Icon = "Images\\icon_matrix_16.png",
             LargeIcon = "Images\\icon_matrix_32.png",
             ToolTip = "Launch Clash Matrix generator and Tools Test runner")]
    [Command("ID_CYPHER_CMD_DISTILL",
             DisplayName = "Distill Clashes",
             Icon = "Images\\icon_distill_16.png",
             LargeIcon = "Images\\icon_distill_32.png",
             ToolTip = "Spatial element grouping & clash cluster distillation")]
    [Command("ID_CYPHER_CMD_VIEWPOINTS",
             DisplayName = "Create Viewpoints",
             Icon = "Images\\icon_viewpoints_16.png",
             LargeIcon = "Images\\icon_viewpoints_32.png",
             ToolTip = "Generate filtered saved viewpoints for clash results")]
    public class CypherRibbonCommandHandler : CommandHandlerPlugin
    {
        public override int ExecuteCommand(string name, params string[] parameters)
        {
            switch (name)
            {
                case "ID_CYPHER_CMD_MATRIX":
                    App.LaunchApp(0);
                    break;
                case "ID_CYPHER_CMD_DISTILL":
                    App.LaunchApp(1);
                    break;
                case "ID_CYPHER_CMD_VIEWPOINTS":
                    App.LaunchApp(2);
                    break;
                default:
                    App.LaunchApp(0);
                    break;
            }
            return 0;
        }

        public override CommandState CanExecuteCommand(string commandId)
        {
            var state = new CommandState();
            state.IsVisible = true;
            state.IsEnabled = true;
            state.IsChecked = true;
            return state;
        }

        public override bool CanExecuteRibbonTab(string name)
        {
            return true;
        }
    }

    // =========================================================================
    // 2. Tool Add-ins Tab Fallback Plugin
    // =========================================================================
    [Plugin("CypherNavisAddin", "CYPH", DisplayName = "Cypher Tools", ToolTip = "Automated Model Clash Runner & Distiller")]
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
                // ===== PRIMARY REMOTE DEACTIVATION GATE =====
                var licenseResult = LicenseService.Validate();
                if (!licenseResult.IsAllowed && licenseResult.IsRevoked)
                {
                    string msg = !string.IsNullOrWhiteSpace(licenseResult.Message)
                        ? licenseResult.Message
                        : "Cypher Tools is temporarily unavailable. Please contact administrator.";
                    DialogService.Instance.ShowWarning(msg, "Cypher Tools");
                    return;
                }
                // =============================================

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
}
