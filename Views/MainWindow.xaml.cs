using System.Windows;

namespace AutomatedClashRunner.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ShiftClickBehavior.SetIsEnabled(ListViewSelectionAModels, true);
            ShiftClickBehavior.SetIsEnabled(ListViewSelectionASets, true);
            ShiftClickBehavior.SetIsEnabled(ListViewSelectionBModels, true);
            ShiftClickBehavior.SetIsEnabled(ListViewSelectionBSets, true);
            ShiftClickBehavior.SetIsEnabled(ListViewDistillerTests, true);
            ShiftClickBehavior.SetIsEnabled(ListViewViewpointsTests, true);

            Loaded += (s, e) => Services.DialogService.ActiveWindow = this;
            Closed += (s, e) => { if (Services.DialogService.ActiveWindow == this) Services.DialogService.ActiveWindow = null; };
        }
    }
}
