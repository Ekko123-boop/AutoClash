using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Windows;
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
            _timer = new System.Windows.Forms.Timer { Interval = 400 };
            _timer.Tick += (s, e) =>
            {
                _attempts++;
                var ribbon = ComponentManager.Ribbon;
                if (ribbon != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                    InitializeRimoRibbon(ribbon);
                }
                else if (_attempts > 30) // Stop polling after 12 seconds
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }
            };
            _timer.Start();
        }

        private void InitializeRimoRibbon(RibbonControl ribbon)
        {
            try
            {
                var tab = ribbon.Tabs.FirstOrDefault(t => t.Id == "ID_RIMO_TAB" || t.Title == "Rimo");
                if (tab == null)
                {
                    tab = new RibbonTab { Title = "Rimo", Id = "ID_RIMO_TAB" };
                    ribbon.Tabs.Add(tab);
                }

                var panel = tab.Panels.FirstOrDefault(p => p.Source?.Title == "Clash Automation");
                if (panel == null)
                {
                    var source = new RibbonPanelSource { Title = "Clash Automation" };
                    panel = new RibbonPanel { Source = source };
                    tab.Panels.Add(panel);

                    string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                    // Button 1: Clash Matrix
                    var btnMatrix = new RibbonButton
                    {
                        Text = "Clash\nMatrix",
                        ShowText = true,
                        Id = "ID_RIMO_CMD_MATRIX",
                        ToolTip = "Launch Clash Matrix generator and Tools Test runner",
                        Size = RibbonItemSize.Large,
                        Orientation = System.Windows.Controls.Orientation.Vertical,
                        LargeImage = LoadBitmap(Path.Combine(asmDir, "Images", "icon_matrix_32.png")),
                        Image = LoadBitmap(Path.Combine(asmDir, "Images", "icon_matrix_16.png")),
                        CommandHandler = new RelayRibbonCommand(() => App.LaunchApp(0))
                    };
                    source.Items.Add(btnMatrix);

                    // Button 2: Distill Clashes
                    var btnDistill = new RibbonButton
                    {
                        Text = "Distill\nClashes",
                        ShowText = true,
                        Id = "ID_RIMO_CMD_DISTILL",
                        ToolTip = "Spatial element grouping & clash cluster distillation",
                        Size = RibbonItemSize.Large,
                        Orientation = System.Windows.Controls.Orientation.Vertical,
                        LargeImage = LoadBitmap(Path.Combine(asmDir, "Images", "icon_distill_32.png")),
                        Image = LoadBitmap(Path.Combine(asmDir, "Images", "icon_distill_16.png")),
                        CommandHandler = new RelayRibbonCommand(() => App.LaunchApp(1))
                    };
                    source.Items.Add(btnDistill);

                    // Button 3: Create Viewpoints
                    var btnViewpoints = new RibbonButton
                    {
                        Text = "Create\nViewpoints",
                        ShowText = true,
                        Id = "ID_RIMO_CMD_VIEWPOINTS",
                        ToolTip = "Generate filtered saved viewpoints for clash results",
                        Size = RibbonItemSize.Large,
                        Orientation = System.Windows.Controls.Orientation.Vertical,
                        LargeImage = LoadBitmap(Path.Combine(asmDir, "Images", "icon_viewpoints_32.png")),
                        Image = LoadBitmap(Path.Combine(asmDir, "Images", "icon_viewpoints_16.png")),
                        CommandHandler = new RelayRibbonCommand(() => App.LaunchApp(2))
                    };
                    source.Items.Add(btnViewpoints);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogErrorStatic($"Failed to initialize Rimo Ribbon: {ex}");
            }
        }

        private BitmapImage LoadBitmap(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { }
            return null;
        }

        public override void OnUnloading()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
    }

    public class RelayRibbonCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayRibbonCommand(Action action) { _action = action; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _action?.Invoke();
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
