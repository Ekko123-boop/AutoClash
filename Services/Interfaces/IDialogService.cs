using System;
using System.Windows;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Services.Interfaces
{
    public interface IDialogService
    {
        void ShowInformation(string message, string title = "Information");
        void ShowWarning(string message, string title = "Warning");
        void ShowError(string message, string title = "Error");
        bool ShowConfirmation(string message, string title = "Confirm");
        void ShowSummary(ExecutionResult result, Window owner = null);
    }
}
