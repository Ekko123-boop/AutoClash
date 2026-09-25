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

        public static Window ActiveWindow { get; set; }

        private static Window GetOwnerWindow()
        {
            if (ActiveWindow != null && ActiveWindow.IsVisible)
            {
                return ActiveWindow;
            }

            try
            {
                if (Application.Current != null)
                {
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win.IsActive && win.IsVisible) return win;
                    }
                    if (Application.Current.MainWindow != null &&
                        Application.Current.MainWindow.IsVisible)
                    {
                        return Application.Current.MainWindow;
                    }
                }
            }
            catch { }

            return null;
        }

        public void ShowInformation(string message, string title = "Information")
        {
            var owner = GetOwnerWindow();
            if (owner != null)
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            var owner = GetOwnerWindow();
            if (owner != null)
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ShowError(string message, string title = "Error")
        {
            var owner = GetOwnerWindow();
            if (owner != null)
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool ShowConfirmation(string message, string title = "Confirm")
        {
            var owner = GetOwnerWindow();
            var result = owner != null
                ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
                : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public void ShowSummary(ExecutionResult result, Window owner = null)
        {
            var dialog = new SummaryDialog(result);
            var targetOwner = owner ?? GetOwnerWindow();
            if (targetOwner != null)
            {
                dialog.Owner = targetOwner;
            }
            dialog.ShowDialog();
        }
    }
}
