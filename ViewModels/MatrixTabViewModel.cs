using System;
using System.Collections.Generic;
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
    public class MatrixTabViewModel : ViewModelBase
    {
        private readonly IModelDiscoveryService _modelDiscovery;
        private readonly ISearchSetService _searchSets;
        private readonly IClashExecutionService _clashExecution;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;

        public ObservableCollection<ModelSourceNode> AllModels { get; } = new ObservableCollection<ModelSourceNode>();
        public ICollectionView ModelsView { get; }

        public ObservableCollection<SearchSetNode> AllSearchSets { get; } = new ObservableCollection<SearchSetNode>();
        public ICollectionView SearchSetsView { get; }

        private string _searchTextModels = string.Empty;
        public string SearchTextModels
        {
            get => _searchTextModels;
            set
            {
                if (SetProperty(ref _searchTextModels, value))
                {
                    ModelsView.Refresh();
                }
            }
        }

        private string _searchTextSets = string.Empty;
        public string SearchTextSets
        {
            get => _searchTextSets;
            set
            {
                if (SetProperty(ref _searchTextSets, value))
                {
                    SearchSetsView.Refresh();
                }
            }
        }

        private ClashTestType _selectedClashType = ClashTestType.Clearance;
        public ClashTestType SelectedClashType
        {
            get => _selectedClashType;
            set => SetProperty(ref _selectedClashType, value);
        }

        public Array AvailableClashTypes => new[] { ClashTestType.Clearance, ClashTestType.Hard, ClashTestType.Duplicate };

        private double _tolerance = 0.0;
        public double Tolerance
        {
            get => _tolerance;
            set => SetProperty(ref _tolerance, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsRunEnabled));
                }
            }
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

        public string ModelSelectionSummary =>
            $"{AllModels.Count(x => x.IsSelected && x.IsSelectable)} of {AllModels.Count} selected";

        public string SetSelectionSummary =>
            $"{AllSearchSets.Count(x => x.IsSelected && !x.IsFolder)} of {AllSearchSets.Count(x => !x.IsFolder)} selected";

        public bool HasNoModels => AllModels.Count == 0;
        public bool HasNoSets => AllSearchSets.Count == 0;

        public int ExpectedTestCount
        {
            get
            {
                int m = AllModels.Count(x => x.IsSelected && x.IsSelectable);
                int s = AllSearchSets.Count(x => x.IsSelected && !x.IsFolder);
                return m * s;
            }
        }

        public bool IsRunEnabled => ExpectedTestCount > 0 && !IsBusy;

        public ICommand RefreshModelsCommand { get; }
        public ICommand RefreshSearchSetsCommand { get; }
        public ICommand SelectAllModelsCommand { get; }
        public ICommand DeselectAllModelsCommand { get; }
        public ICommand SelectAllSetsCommand { get; }
        public ICommand DeselectAllSetsCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand RunCommand { get; }

        public MatrixTabViewModel(
            IModelDiscoveryService modelDiscovery,
            ISearchSetService searchSets,
            IClashExecutionService clashExecution,
            IDialogService dialogService,
            ILoggerService logger)
        {
            _modelDiscovery = modelDiscovery;
            _searchSets = searchSets;
            _clashExecution = clashExecution;
            _dialogService = dialogService;
            _logger = logger;

            ModelsView = CollectionViewSource.GetDefaultView(AllModels);
            ModelsView.Filter = FilterModelItem;

            SearchSetsView = CollectionViewSource.GetDefaultView(AllSearchSets);
            SearchSetsView.Filter = FilterSearchSetItem;

            RefreshModelsCommand = new RelayCommand(_ => LoadModels());
            RefreshSearchSetsCommand = new RelayCommand(_ => LoadSearchSets());

            SelectAllModelsCommand = new RelayCommand(_ => SelectAllVisibleModels(true));
            DeselectAllModelsCommand = new RelayCommand(_ => SelectAllVisibleModels(false));

            SelectAllSetsCommand = new RelayCommand(_ => SelectAllVisibleSets(true));
            DeselectAllSetsCommand = new RelayCommand(_ => SelectAllVisibleSets(false));

            ClearAllCommand = new RelayCommand(_ => ClearAllSelections());
            RunCommand = new RelayCommand(_ => RunClashTests(), _ => IsRunEnabled);

            LoadModels();
            LoadSearchSets();
        }

        private bool FilterModelItem(object obj)
        {
            if (!(obj is ModelSourceNode node)) return false;
            if (string.IsNullOrWhiteSpace(SearchTextModels)) return true;

            string q = SearchTextModels.Trim();
            return (node.DisplayName != null && node.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (node.ParentContainerName != null && node.ParentContainerName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool FilterSearchSetItem(object obj)
        {
            if (!(obj is SearchSetNode node)) return false;
            if (string.IsNullOrWhiteSpace(SearchTextSets)) return true;

            string q = SearchTextSets.Trim();
            return node.FullPath != null && node.FullPath.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void LoadModels()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                var previousSelections = new HashSet<string>(
                    AllModels.Where(x => x.IsSelected).Select(x => x.DisplayName ?? string.Empty));

                // Unsubscribe existing
                foreach (var m in AllModels)
                {
                    m.PropertyChanged -= OnModelPropertyChanged;
                }

                AllModels.Clear();

                var discovered = _modelDiscovery.DiscoverModels(doc);
                foreach (var node in discovered)
                {
                    if (previousSelections.Contains(node.DisplayName ?? string.Empty))
                    {
                        node.IsSelected = true;
                    }
                    node.PropertyChanged += OnModelPropertyChanged;
                    AllModels.Add(node);
                }

                ModelsView.Refresh();
                UpdateSelectionState();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load models", ex);
                _dialogService.ShowError($"Error loading models: {ex.Message}");
            }
        }

        public void LoadSearchSets()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                var previousSelections = new HashSet<string>(
                    AllSearchSets.Where(x => x.IsSelected).Select(x => x.FullPath ?? string.Empty));

                // Unsubscribe existing
                foreach (var s in AllSearchSets)
                {
                    s.PropertyChanged -= OnSetPropertyChanged;
                }

                AllSearchSets.Clear();

                var sets = _searchSets.GetManualSearchSets(doc);
                foreach (var setNode in sets)
                {
                    if (previousSelections.Contains(setNode.FullPath ?? string.Empty))
                    {
                        setNode.IsSelected = true;
                    }
                    setNode.PropertyChanged += OnSetPropertyChanged;
                    AllSearchSets.Add(setNode);
                }

                SearchSetsView.Refresh();
                UpdateSelectionState();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load search sets", ex);
                _dialogService.ShowError($"Error loading search sets: {ex.Message}");
            }
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelSourceNode.IsSelected))
            {
                UpdateSelectionState();
            }
        }

        private void OnSetPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchSetNode.IsSelected))
            {
                UpdateSelectionState();
            }
        }

        private void UpdateSelectionState()
        {
            OnPropertyChanged(nameof(ExpectedTestCount));
            OnPropertyChanged(nameof(IsRunEnabled));
            OnPropertyChanged(nameof(ModelSelectionSummary));
            OnPropertyChanged(nameof(SetSelectionSummary));
            OnPropertyChanged(nameof(HasNoModels));
            OnPropertyChanged(nameof(HasNoSets));
            (RunCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SelectAllVisibleModels(bool isSelected)
        {
            foreach (var item in ModelsView)
            {
                if (item is ModelSourceNode node && node.IsSelectable)
                {
                    node.IsSelected = isSelected;
                }
            }
        }

        private void SelectAllVisibleSets(bool isSelected)
        {
            foreach (var item in SearchSetsView)
            {
                if (item is SearchSetNode node && !node.IsFolder)
                {
                    node.IsSelected = isSelected;
                }
            }
        }

        private void ClearAllSelections()
        {
            foreach (var m in AllModels) m.IsSelected = false;
            foreach (var s in AllSearchSets) s.IsSelected = false;
        }

        private void RunClashTests()
        {
            var selectedModels = AllModels.Where(x => x.IsSelected && x.IsSelectable).ToList();
            var selectedSets = AllSearchSets.Where(x => x.IsSelected && !x.IsFolder).ToList();

            if (selectedSets.Count == 0)
            {
                _dialogService.ShowWarning("Please select at least one Search Set from the right panel.", "No Sets Selected");
                return;
            }
            if (selectedModels.Count == 0)
            {
                _dialogService.ShowWarning("Please select at least one Model from the left panel.", "No Models Selected");
                return;
            }

            int count = selectedModels.Count * selectedSets.Count;
            bool confirm = _dialogService.ShowConfirmation(
                $"Generate and run {count} clash test combinations?\n\nModels: {selectedModels.Count}\nSearch Sets: {selectedSets.Count}\nClash Type: {SelectedClashType}\nTolerance: {Tolerance:F4} m",
                "Confirm Clash Matrix Execution");

            if (!confirm) return;

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            IsBusy = true;
            ProgressText = "Initializing...";
            ProgressBarValue = 0;
            ProgressBarMax = count;

            try
            {
                var result = _clashExecution.RunClashMatrix(
                    doc,
                    selectedSets,
                    selectedModels,
                    SelectedClashType,
                    Tolerance,
                    (status, current, total) =>
                    {
                        ProgressText = status;
                        ProgressBarValue = current;
                        ProgressBarMax = total;
                    });

                _dialogService.ShowSummary(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Fatal error in clash matrix execution", ex);
                _dialogService.ShowError($"Execution failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                ProgressText = string.Empty;
                ProgressBarValue = 0;
            }
        }
    }
}
