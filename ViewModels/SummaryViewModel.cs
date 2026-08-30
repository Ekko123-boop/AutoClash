using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using AutomatedClashRunner.Models;
using Microsoft.Win32;

namespace AutomatedClashRunner.ViewModels
{
    public class SummaryViewModel : ViewModelBase
    {
        private readonly Action _closeAction;

        public ObservableCollection<SummaryLineItem> Items { get; } = new ObservableCollection<SummaryLineItem>();

        public string SummaryHeaderText { get; set; } = "Execution Summary";

        public ICommand CopyCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand CloseCommand { get; }

        public SummaryViewModel(ExecutionResult result, Action closeAction)
        {
            _closeAction = closeAction;

            CopyCommand = new RelayCommand(_ => CopyToClipboard());
            ExportCsvCommand = new RelayCommand(_ => ExportToCsv());
            CloseCommand = new RelayCommand(_ => _closeAction?.Invoke());

            LoadResult(result);
        }

        private void LoadResult(ExecutionResult result)
        {
            if (result == null) return;

            Items.Clear();

            if (result.HasWarningsOrFailures)
            {
                SummaryHeaderText = "Completed with warnings / errors";
            }
            else
            {
                SummaryHeaderText = "Completed Successfully";
            }

            // Generated Search Sets
            foreach (var set in result.GeneratedSets)
            {
                Items.Add(new SummaryLineItem { Category = "Search Sets", Message = set, Type = SummaryItemType.Success });
            }
            foreach (var set in result.SkippedSets)
            {
                Items.Add(new SummaryLineItem { Category = "Search Sets", Message = set, Type = SummaryItemType.Warning });
            }

            // Clash Tests
            foreach (var test in result.SuccessfulTests)
            {
                Items.Add(new SummaryLineItem { Category = "Clash Tests", Message = test, Type = SummaryItemType.Success });
            }
            foreach (var test in result.SkippedTests)
            {
                Items.Add(new SummaryLineItem { Category = "Clash Tests", Message = $"{test} (Skipped: Already exists)", Type = SummaryItemType.Warning });
            }
            foreach (var test in result.FailedTests)
            {
                Items.Add(new SummaryLineItem { Category = "Clash Tests", Message = test, Type = SummaryItemType.Error });
            }
        }

        private void CopyToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine(SummaryHeaderText);
            sb.AppendLine();
            foreach (var item in Items)
            {
                sb.AppendLine($"{item.Icon} [{item.Category}] {item.Message}");
            }
            Clipboard.SetText(sb.ToString());
        }

        private void ExportToCsv()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"Clash_Execution_Summary_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Category,Status,Message");
                    foreach (var item in Items)
                    {
                        sb.AppendLine($"\"{item.Category}\",\"{item.Type}\",\"{item.Message.Replace("\"", "\"\"")}\"");
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Summary exported successfully to CSV!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
