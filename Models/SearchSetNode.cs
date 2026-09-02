using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Navisworks.Api;

namespace AutomatedClashRunner.Models
{
    public class SearchSetNode : INotifyPropertyChanged, ISelectableItem
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
        public string FullPath { get; set; }
        public bool IsFolder { get; set; }
        public bool IsSet => !IsFolder;
        public SavedItem OriginalSavedItem { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
