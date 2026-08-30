using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Navisworks.Api;
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

        public int ActiveNewCount { get; private set; }
        public int ReviewedCount { get; private set; }
        public int ApprovedCount { get; private set; }
        public int ResolvedCount { get; private set; }
        public int TotalCount { get; private set; }

        public ClashTestNode(ClashTest test)
        {
            OriginalTest = test ?? throw new ArgumentNullException(nameof(test));
            CalculateCounts();
        }

        public void RefreshCounts()
        {
            CalculateCounts();
            OnPropertyChanged(nameof(ActiveNewCount));
            OnPropertyChanged(nameof(ReviewedCount));
            OnPropertyChanged(nameof(ApprovedCount));
            OnPropertyChanged(nameof(ResolvedCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(Status));
        }

        private void CalculateCounts()
        {
            if (OriginalTest?.Children == null) return;

            int activeNew = 0;
            int reviewed = 0;
            int approved = 0;
            int resolved = 0;
            int total = 0;

            foreach (SavedItem child in OriginalTest.Children)
            {
                if (child is ClashResult res)
                {
                    total++;
                    switch (res.Status)
                    {
                        case ClashResultStatus.New:
                        case ClashResultStatus.Active:
                            activeNew++;
                            break;
                        case ClashResultStatus.Reviewed:
                            reviewed++;
                            break;
                        case ClashResultStatus.Approved:
                            approved++;
                            break;
                        case ClashResultStatus.Resolved:
                            resolved++;
                            break;
                    }
                }
                else if (child is ClashResultGroup group)
                {
                    int groupCount = group.Children.Count;
                    int countToAdd = groupCount > 0 ? groupCount : 1;
                    total += countToAdd;

                    switch (group.Status)
                    {
                        case ClashResultStatus.New:
                        case ClashResultStatus.Active:
                            activeNew += countToAdd;
                            break;
                        case ClashResultStatus.Reviewed:
                            reviewed += countToAdd;
                            break;
                        case ClashResultStatus.Approved:
                            approved += countToAdd;
                            break;
                        case ClashResultStatus.Resolved:
                            resolved += countToAdd;
                            break;
                        default:
                            activeNew += countToAdd;
                            break;
                    }
                }
            }

            ActiveNewCount = activeNew;
            ReviewedCount = reviewed;
            ApprovedCount = approved;
            ResolvedCount = resolved;
            TotalCount = total;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
