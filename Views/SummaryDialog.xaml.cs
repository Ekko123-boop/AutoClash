using System.Windows;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.ViewModels;

namespace AutomatedClashRunner.Views
{
    public partial class SummaryDialog : Window
    {
        public SummaryDialog(ExecutionResult result)
        {
            InitializeComponent();
            DataContext = new SummaryViewModel(result, () => Close());
        }
    }
}
