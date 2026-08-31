using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace AutomatedClashRunner.Services
{
    public static class DynamicRibbonService
    {
        public static bool TryInitializeRibbon(Action<int> launchCallback)
        {
            try
            {
                var adWindows = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.Equals("AdWindows", StringComparison.OrdinalIgnoreCase));
                if (adWindows == null) return false;

                var compMgrType = adWindows.GetType("Autodesk.Windows.ComponentManager");
                if (compMgrType == null) return false;

                var ribbonProp = compMgrType.GetProperty("Ribbon", BindingFlags.Public | BindingFlags.Static);
                if (ribbonProp == null) return false;

                object ribbon = ribbonProp.GetValue(null, null);
                if (ribbon == null) return false; // Ribbon not yet initialized in Navisworks

                var ribbonTabType = adWindows.GetType("Autodesk.Windows.RibbonTab");
                var ribbonPanelType = adWindows.GetType("Autodesk.Windows.RibbonPanel");
                var ribbonPanelSourceType = adWindows.GetType("Autodesk.Windows.RibbonPanelSource");
                var ribbonButtonType = adWindows.GetType("Autodesk.Windows.RibbonButton");
                var ribbonItemSizeType = adWindows.GetType("Autodesk.Windows.RibbonItemSize");

                if (ribbonTabType == null || ribbonPanelType == null || ribbonPanelSourceType == null || ribbonButtonType == null)
                    return false;

                var tabsProp = ribbon.GetType().GetProperty("Tabs");
                var tabsList = (IList)tabsProp.GetValue(ribbon, null);

                // Check if Rimo tab already exists
                object rimoTab = null;
                foreach (var t in tabsList)
                {
                    var title = (string)t.GetType().GetProperty("Title")?.GetValue(t, null);
                    var id = (string)t.GetType().GetProperty("Id")?.GetValue(t, null);
                    if (string.Equals(title, "Rimo", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(id, "ID_RIMO_TAB", StringComparison.OrdinalIgnoreCase))
                    {
                        rimoTab = t;
                        break;
                    }
                }

                if (rimoTab == null)
                {
                    rimoTab = Activator.CreateInstance(ribbonTabType);
                    ribbonTabType.GetProperty("Title")?.SetValue(rimoTab, "Rimo", null);
                    ribbonTabType.GetProperty("Id")?.SetValue(rimoTab, "ID_RIMO_TAB", null);
                    tabsList.Add(rimoTab);
                }

                // Check Panels
                var panelsProp = rimoTab.GetType().GetProperty("Panels");
                var panelsList = (IList)panelsProp.GetValue(rimoTab, null);

                object rimoPanel = null;
                foreach (var p in panelsList)
                {
                    var src = p.GetType().GetProperty("Source")?.GetValue(p, null);
                    var title = (string)src?.GetType().GetProperty("Title")?.GetValue(src, null);
                    if (string.Equals(title, "Clash Automation", StringComparison.OrdinalIgnoreCase))
                    {
                        rimoPanel = p;
                        break;
                    }
                }

                if (rimoPanel == null)
                {
                    var panelSource = Activator.CreateInstance(ribbonPanelSourceType);
                    ribbonPanelSourceType.GetProperty("Title")?.SetValue(panelSource, "Clash Automation", null);

                    rimoPanel = Activator.CreateInstance(ribbonPanelType);
                    ribbonPanelType.GetProperty("Source")?.SetValue(rimoPanel, panelSource, null);
                    panelsList.Add(rimoPanel);

                    var itemsProp = panelSource.GetType().GetProperty("Items");
                    var itemsList = (IList)itemsProp.GetValue(panelSource, null);

                    string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                    // Button 1: Clash Matrix (Target Tab 0)
                    object btnMatrix = CreateRibbonButton(ribbonButtonType, ribbonItemSizeType,
                        "Clash\nMatrix", "ID_RIMO_CMD_MATRIX", "Launch Clash Matrix generator and Tools Test runner",
                        Path.Combine(asmDir, "Images", "icon_matrix_32.png"),
                        Path.Combine(asmDir, "Images", "icon_matrix_16.png"),
                        () => launchCallback(0));
                    itemsList.Add(btnMatrix);

                    // Button 2: Distill Clashes (Target Tab 1)
                    object btnDistill = CreateRibbonButton(ribbonButtonType, ribbonItemSizeType,
                        "Distill\nClashes", "ID_RIMO_CMD_DISTILL", "Spatial element grouping & clash cluster distillation",
                        Path.Combine(asmDir, "Images", "icon_distill_32.png"),
                        Path.Combine(asmDir, "Images", "icon_distill_16.png"),
                        () => launchCallback(1));
                    itemsList.Add(btnDistill);

                    // Button 3: Create Viewpoints (Target Tab 2)
                    object btnViewpoints = CreateRibbonButton(ribbonButtonType, ribbonItemSizeType,
                        "Create\nViewpoints", "ID_RIMO_CMD_VIEWPOINTS", "Generate filtered saved viewpoints for clash results",
                        Path.Combine(asmDir, "Images", "icon_viewpoints_32.png"),
                        Path.Combine(asmDir, "Images", "icon_viewpoints_16.png"),
                        () => launchCallback(2));
                    itemsList.Add(btnViewpoints);
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogErrorStatic($"Dynamic Ribbon Initialization failed: {ex}");
                return false;
            }
        }

        private static object CreateRibbonButton(
            Type buttonType,
            Type sizeEnumType,
            string text,
            string id,
            string toolTip,
            string largeImgPath,
            string smallImgPath,
            Action executeAction)
        {
            var btn = Activator.CreateInstance(buttonType);
            buttonType.GetProperty("Text")?.SetValue(btn, text, null);
            buttonType.GetProperty("ShowText")?.SetValue(btn, true, null);
            buttonType.GetProperty("Id")?.SetValue(btn, id, null);
            buttonType.GetProperty("ToolTip")?.SetValue(btn, toolTip, null);
            buttonType.GetProperty("Orientation")?.SetValue(btn, System.Windows.Controls.Orientation.Vertical, null);

            if (sizeEnumType != null)
            {
                try
                {
                    object largeVal = Enum.Parse(sizeEnumType, "Large");
                    buttonType.GetProperty("Size")?.SetValue(btn, largeVal, null);
                }
                catch { }
            }

            if (File.Exists(largeImgPath))
            {
                buttonType.GetProperty("LargeImage")?.SetValue(btn, LoadBitmap(largeImgPath), null);
            }
            if (File.Exists(smallImgPath))
            {
                buttonType.GetProperty("Image")?.SetValue(btn, LoadBitmap(smallImgPath), null);
            }

            buttonType.GetProperty("CommandHandler")?.SetValue(btn, new DynamicRibbonCommand(executeAction), null);

            return btn;
        }

        private static BitmapImage LoadBitmap(string path)
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
    }

    public class DynamicRibbonCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public DynamicRibbonCommand(Action action) { _action = action; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _action?.Invoke();
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
