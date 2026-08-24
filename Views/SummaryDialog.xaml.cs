using System.Windows;
using AutomatedClashRunner.Models;
using System.Text;

namespace AutomatedClashRunner.Views
{
    public partial class SummaryDialog : Window
    {
        public SummaryDialog(ExecutionResult result)
        {
            InitializeComponent();
            var sb = new StringBuilder();
            sb.AppendLine("Completed with warnings/info");
            sb.AppendLine();
            sb.AppendLine("Generated Search Sets:");
            foreach (var set in result.GeneratedSets) sb.AppendLine($"✓ {set}");
            foreach (var set in result.SkippedSets) sb.AppendLine($"⚠ {set}");
            sb.AppendLine();
            sb.AppendLine("Clash Tests:");
            foreach (var test in result.SuccessfulTests) sb.AppendLine($"✓ {test}");
            foreach (var test in result.SkippedTests) sb.AppendLine($"⚠ {test} (Skipped: Already exists)");
            foreach (var test in result.FailedTests) sb.AppendLine($"✗ {test}");
            
            txtSummary.Text = sb.ToString();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtSummary.Text);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
