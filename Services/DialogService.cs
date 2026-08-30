using System;
using System.Windows;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services.Interfaces;
using AutomatedClashRunner.Views;

namespace AutomatedClashRunner.Services
{
    public class DialogService : IDialogService
    {
        public static DialogService Instance { get; } = new DialogService();

        public void ShowInformation(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool ShowConfirmation(string message, string title = "Confirm")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public void ShowSummary(ExecutionResult result, Window owner = null)
        {
            var dialog = new SummaryDialog(result);
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            dialog.ShowDialog();
        }
    }
}
