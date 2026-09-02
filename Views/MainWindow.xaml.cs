using System.Windows;

namespace AutomatedClashRunner.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ShiftClickBehavior.SetIsEnabled(ListViewModels, true);
            ShiftClickBehavior.SetIsEnabled(ListViewSearchSets, true);
            ShiftClickBehavior.SetIsEnabled(ListViewDistillerTests, true);
            ShiftClickBehavior.SetIsEnabled(ListViewViewpointsTests, true);
        }
    }
}
