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
    public class DistillerTabViewModel : ViewModelBase
    {
        private readonly IClashDistillerService _distiller;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;

        public ObservableCollection<ClashTestNode> AllTests { get; } = new ObservableCollection<ClashTestNode>();
        public ICollectionView TestsView { get; }

        private string _searchTextTests = string.Empty;
        public string SearchTextTests
        {
            get => _searchTextTests;
            set
            {
                if (SetProperty(ref _searchTextTests, value))
                {
                    TestsView.Refresh();
                }
            }
        }

        private double _groupingProximity = 10.0;
        public double GroupingProximity
        {
            get => _groupingProximity;
            set => SetProperty(ref _groupingProximity, value);
        }

        public string TestSelectionSummary =>
            $"{AllTests.Count(x => x.IsSelected)} of {AllTests.Count} tests selected";

        public bool HasNoTests => AllTests.Count == 0;

        public ICommand RefreshTestsCommand { get; }
        public ICommand SelectAllTestsCommand { get; }
        public ICommand DeselectAllTestsCommand { get; }
        public ICommand GroupByElementCommand { get; }
        public ICommand ExportViewpointsCommand { get; }
        public ICommand ReRunSelectedCommand { get; }

        public DistillerTabViewModel(
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
            GroupByElementCommand = new RelayCommand(_ => GroupSelectedTests());
            ExportViewpointsCommand = new RelayCommand(_ => ExportSelectedViewpoints());
            ReRunSelectedCommand = new RelayCommand(_ => ReRunSelectedTests());

            LoadTests();
        }

        private bool FilterTestItem(object obj)
        {
            if (!(obj is ClashTestNode node)) return false;
            if (string.IsNullOrWhiteSpace(SearchTextTests)) return true;

            string q = SearchTextTests.Trim();
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
                _logger.LogError("Error loading clash tests", ex);
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

        private void ReRunSelectedTests()
        {
            var selected = AllTests.Where(t => t.IsSelected).Select(t => t.OriginalTest).ToList();
            if (selected.Count == 0)
            {
                _dialogService.ShowWarning("Please select at least one test to re-run.", "No Tests Selected");
                return;
            }

            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                _distiller.ReRunTests(doc, selected);
                LoadTests();
                _dialogService.ShowInformation($"Re-ran {selected.Count} tests successfully.", "Success");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error re-running selected tests", ex);
                _dialogService.ShowError($"Failed to re-run tests: {ex.Message}");
            }
        }

        private void GroupSelectedTests()
        {
            var selected = AllTests.Where(t => t.IsSelected).Select(t => t.OriginalTest).ToList();
            if (selected.Count == 0)
            {
                _dialogService.ShowWarning("Please select at least one test to group.", "No Tests Selected");
                return;
            }

            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                int groupsCreated = _distiller.GroupByElement(doc, selected, GroupingProximity);
                LoadTests();
                _dialogService.ShowInformation($"Grouped clashes! Created {groupsCreated} new groups across {selected.Count} tests.", "Grouping Complete");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error grouping selected tests", ex);
                _dialogService.ShowError($"Failed to group clashes: {ex.Message}");
            }
        }

        private void ExportSelectedViewpoints()
        {
            var selected = AllTests.Where(t => t.IsSelected).Select(t => t.OriginalTest).ToList();
            if (selected.Count == 0)
            {
                _dialogService.ShowWarning("Please select at least one test to export viewpoints from.", "No Tests Selected");
                return;
            }

            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                int vpCreated = _distiller.ExportReviewedViewpoints(doc, selected);
                LoadTests();
                _dialogService.ShowInformation($"Exported {vpCreated} 'Reviewed' viewpoints into the Saved Viewpoints window.", "Export Complete");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error exporting viewpoints", ex);
                _dialogService.ShowError($"Failed to export viewpoints: {ex.Message}");
            }
        }
    }
}
