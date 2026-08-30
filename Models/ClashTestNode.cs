using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Navisworks.Api.Clash;

namespace AutomatedClashRunner.Models
{
    public class ClashTestNode : INotifyPropertyChanged
    {
        public ClashTest OriginalTest { get; }

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

        public string DisplayName => OriginalTest?.DisplayName ?? string.Empty;
        public string Status => OriginalTest?.Status.ToString() ?? "Unknown";

        public ClashTestNode(ClashTest test)
        {
            OriginalTest = test ?? throw new ArgumentNullException(nameof(test));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
