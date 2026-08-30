using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.ViewModels
{
    public class ViewpointsTabViewModel : ViewModelBase
    {
        private readonly IClashDistillerService _distiller;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;

        public ObservableCollection<ClashTestNode> AllTests { get; } = new ObservableCollection<ClashTestNode>();
        public ICollectionView TestsView { get; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    TestsView.Refresh();
                }
            }
        }

        private bool _includeNew;
        public bool IncludeNew
        {
            get => _includeNew;
            set => SetProperty(ref _includeNew, value);
        }

        private bool _includeActive;
        public bool IncludeActive
        {
            get => _includeActive;
            set => SetProperty(ref _includeActive, value);
        }

        private bool _includeReviewed = true;
        public bool IncludeReviewed
        {
            get => _includeReviewed;
            set => SetProperty(ref _includeReviewed, value);
        }

        private bool _includeApproved;
        public bool IncludeApproved
        {
            get => _includeApproved;
            set => SetProperty(ref _includeApproved, value);
        }

        private bool _includeResolved;
        public bool IncludeResolved
        {
            get => _includeResolved;
            set => SetProperty(ref _includeResolved, value);
        }

        private bool _placeInTimestampedFolder;
        public bool PlaceInTimestampedFolder
        {
            get => _placeInTimestampedFolder;
            set => SetProperty(ref _placeInTimestampedFolder, value);
        }

        public string TestSelectionSummary =>
            $"{AllTests.Count(x => x.IsSelected)} of {AllTests.Count} selected";

        public bool HasNoTests => AllTests.Count == 0;

        public ICommand RefreshTestsCommand { get; }
        public ICommand SelectAllTestsCommand { get; }
        public ICommand DeselectAllTestsCommand { get; }
        public ICommand CreateViewpointsSelectedCommand { get; }
        public ICommand CreateViewpointsAllCommand { get; }

        public ViewpointsTabViewModel(
            IClashDistillerService distiller,
            IDialogService dialogService,
            ILoggerService logger)
        {
            _distiller = distiller;
            _dialogService = dialogService;
            _logger = logger;

            TestsView = CollectionViewSource.GetDefaultView(AllTests);
            TestsView.Filter = FilterTestItem;

            RefreshTestsCommand = new RelayCommand(_ => LoadTests());
            SelectAllTestsCommand = new RelayCommand(_ => SelectAllVisibleTests(true));
            DeselectAllTestsCommand = new RelayCommand(_ => SelectAllVisibleTests(false));

            CreateViewpointsSelectedCommand = new RelayCommand(_ => ExportViewpoints(selectedOnly: true));
            CreateViewpointsAllCommand = new RelayCommand(_ => ExportViewpoints(selectedOnly: false));

            LoadTests();
        }

        private bool FilterTestItem(object obj)
        {
            if (!(obj is ClashTestNode node)) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            string q = SearchText.Trim();
            return node.DisplayName != null && node.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void LoadTests()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                var previousSelections = new System.Collections.Generic.HashSet<string>(
                    AllTests.Where(x => x.IsSelected).Select(x => x.DisplayName));

                foreach (var t in AllTests)
                {
                    t.PropertyChanged -= OnTestPropertyChanged;
                }

                AllTests.Clear();

                if (doc != null && !doc.IsClear)
                {
                    var documentClash = doc.GetClash();
                    if (documentClash?.TestsData?.Tests != null)
                    {
                        foreach (SavedItem item in documentClash.TestsData.Tests)
                        {
                            if (item is ClashTest test)
                            {
                                var node = new ClashTestNode(test);
                                if (previousSelections.Contains(node.DisplayName))
                                {
                                    node.IsSelected = true;
                                }
                                node.PropertyChanged += OnTestPropertyChanged;
                                AllTests.Add(node);
                            }
                        }
                    }
                }

                TestsView.Refresh();
                UpdateSelectionState();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error loading tests for Viewpoints tab", ex);
                _dialogService.ShowError($"Failed to load clash tests: {ex.Message}");
            }
        }

        private void OnTestPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClashTestNode.IsSelected))
            {
                UpdateSelectionState();
            }
        }

        private void UpdateSelectionState()
        {
            OnPropertyChanged(nameof(TestSelectionSummary));
            OnPropertyChanged(nameof(HasNoTests));
        }

        private void SelectAllVisibleTests(bool isSelected)
        {
            foreach (var item in TestsView)
            {
                if (item is ClashTestNode node)
                {
                    node.IsSelected = isSelected;
                }
            }
        }

        private void ExportViewpoints(bool selectedOnly)
        {
            var targetTests = selectedOnly
                ? AllTests.Where(t => t.IsSelected).Select(t => t.OriginalTest).ToList()
                : AllTests.Select(t => t.OriginalTest).ToList();

            if (targetTests.Count == 0)
            {
                _dialogService.ShowWarning(
                    selectedOnly ? "Please select at least one clash test." : "No clash tests available.",
                    "No Tests Available");
                return;
            }

            if (!IncludeNew && !IncludeActive && !IncludeReviewed && !IncludeApproved && !IncludeResolved)
            {
                _dialogService.ShowWarning("Please enable at least one status (e.g., Reviewed) under 'Include statuses'.", "No Status Filter");
                return;
            }

            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                int count = _distiller.ExportViewpoints(
                    doc,
                    targetTests,
                    IncludeNew,
                    IncludeActive,
                    IncludeReviewed,
                    IncludeApproved,
                    IncludeResolved,
                    PlaceInTimestampedFolder);

                LoadTests();

                _dialogService.ShowInformation(
                    $"Generated {count} viewpoints across {targetTests.Count} clash tests into the Saved Viewpoints window.",
                    "Viewpoints Created");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating viewpoints", ex);
                _dialogService.ShowError($"Failed to generate viewpoints: {ex.Message}");
            }
        }
    }
}
