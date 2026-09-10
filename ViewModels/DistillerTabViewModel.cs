using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Common;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services.Interfaces;
using AutomatedClashRunner.Utils;

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

        private double _groupingProximity = AppConstants.DefaultGroupingProximityFt;
        public double GroupingProximity
        {
            get => _groupingProximity;
            set => SetProperty(ref _groupingProximity, value);
        }

        public string TestSelectionSummary =>
            $"{AllTests.Count(x => x.IsSelected)} of {AllTests.Count} tests selected";

        public bool HasNoTests => AllTests.Count == 0;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _progressText = string.Empty;
        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        private int _progressBarValue;
        public int ProgressBarValue
        {
            get => _progressBarValue;
            set => SetProperty(ref _progressBarValue, value);
        }

        private int _progressBarMax = 100;
        public int ProgressBarMax
        {
            get => _progressBarMax;
            set => SetProperty(ref _progressBarMax, value);
        }

        public ICommand RefreshTestsCommand { get; }
        public ICommand SelectAllTestsCommand { get; }
        public ICommand DeselectAllTestsCommand { get; }
        public ICommand DistillSelectedCommand { get; }
        public ICommand DistillAllCommand { get; }
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
            DistillSelectedCommand = new RelayCommand(_ => DistillTests(selectedOnly: true));
            DistillAllCommand = new RelayCommand(_ => DistillTests(selectedOnly: false));
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

            IsBusy = true;
            ProgressText = "Re-running selected clash tests...";
            ProgressBarValue = 0;
            ProgressBarMax = selected.Count;

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
            finally
            {
                IsBusy = false;
                ProgressText = string.Empty;
                ProgressBarValue = 0;
            }
        }

        private void DistillTests(bool selectedOnly)
        {
            var targetTests = selectedOnly
                ? AllTests.Where(t => t.IsSelected).Select(t => t.OriginalTest).ToList()
                : AllTests.Select(t => t.OriginalTest).ToList();

            if (targetTests.Count == 0)
            {
                _dialogService.ShowWarning(
                    selectedOnly ? "Please select at least one test to distill." : "No clash tests available.",
                    "No Tests Available");
                return;
            }

            IsBusy = true;
            ProgressText = "Preparing clash distillation...";
            ProgressBarValue = 0;
            ProgressBarMax = 100;

            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                int groupsCreated = _distiller.GroupByElement(
                    doc,
                    targetTests,
                    GroupingProximity,
                    (status, current, total) =>
                    {
                        ProgressText = status;
                        ProgressBarValue = current;
                        ProgressBarMax = total > 0 ? total : 100;
                        DoEvents();
                    });

                LoadTests();
                _dialogService.ShowInformation($"Clash Distillation complete! Created {groupsCreated} new groups across {targetTests.Count} tests.", "Distill Complete");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error distilling clash tests", ex);
                _dialogService.ShowError($"Failed to distill clashes: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                ProgressText = string.Empty;
                ProgressBarValue = 0;
            }
        }

        private static void DoEvents() => DispatcherUtils.DoEvents();
    }
}
