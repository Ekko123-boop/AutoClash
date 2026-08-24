using Autodesk.Navisworks.Api;
using AutomatedClashRunner.ViewModels;

namespace AutomatedClashRunner.Models
{
    public class SearchSetNode : ViewModelBase
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string DisplayName { get; set; }
        public string FullPath { get; set; }
        public bool IsFolder { get; set; }
        public bool IsSet => !IsFolder;
        public SavedItem OriginalSavedItem { get; set; }
    }
}
