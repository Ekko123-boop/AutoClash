using System;
using System.Windows.Interop;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Api;
using AutomatedClashRunner.Views;
using AutomatedClashRunner.ViewModels;

namespace AutomatedClashRunner
{
    [Plugin("AutomatedClashRunner", "ACR", DisplayName = "Automated Clash Runner", ToolTip = "Automated Model Clash Runner")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class App : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                if (Autodesk.Navisworks.Api.Application.IsAutomated)
                    return 0;

                var window = new MainWindow();
                var helper = new WindowInteropHelper(window);
                helper.Owner = Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle;

                window.DataContext = new MainViewModel(window);
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Fatal Error: {ex.Message}\n{ex.StackTrace}");
            }
            return 0;
        }
    }
}
