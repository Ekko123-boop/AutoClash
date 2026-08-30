using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Navisworks.Api;

namespace AutomatedClashRunner.Models
{
    public class ModelSourceNode : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DisplayName { get; set; }
        public string SourceFilePath { get; set; }
        public bool IsDirectNwc { get; set; }
        public string ParentContainerName { get; set; }
        public ModelItem OriginalModelItem { get; set; }
        public bool IsSelectable { get; set; } = true;
        public string WarningMessage { get; set; }
        public string DisplayType => IsDirectNwc ? "Direct NWC" : "NWD Branch";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
