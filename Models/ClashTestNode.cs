using System;
using System.ComponentModel;
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
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string DisplayName => OriginalTest.DisplayName;
        public string Status => OriginalTest.Status.ToString();

        public ClashTestNode(ClashTest test)
        {
            OriginalTest = test ?? throw new ArgumentNullException(nameof(test));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
