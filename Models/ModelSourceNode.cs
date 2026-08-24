using Autodesk.Navisworks.Api;
using AutomatedClashRunner.ViewModels;

namespace AutomatedClashRunner.Models
{
    public class ModelSourceNode : ViewModelBase
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string DisplayName { get; set; }
        public string SourceFilePath { get; set; }
        public bool IsDirectNwc { get; set; }
        public string ParentContainerName { get; set; }
        public ModelItem OriginalModelItem { get; set; }
        public bool IsSelectable { get; set; }
        public string WarningMessage { get; set; }
        public string DisplayType => IsDirectNwc ? "Direct NWC" : "NWD Branch";
    }
}
