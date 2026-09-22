using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Common;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services;
using AutomatedClashRunner.Services.Interfaces;
using AutomatedClashRunner.Utils;

namespace AutomatedClashRunner.ViewModels
{
    public class MatrixTabViewModel : ViewModelBase
    {
        private readonly IModelDiscoveryService _modelDiscovery;
        private readonly ISearchSetService _searchSets;
        private readonly IClashExecutionService _clashExecution;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;
        private readonly INamingService _naming;

        // Selection A collections
        public ObservableCollection<ModelSourceNode> ModelsA { get; } = new ObservableCollection<ModelSourceNode>();
        public ICollectionView ModelsViewA { get; }

        public ObservableCollection<SearchSetNode> SetsA { get; } = new ObservableCollection<SearchSetNode>();
        public ICollectionView SetsViewA { get; }

        // Selection B collections
        public ObservableCollection<ModelSourceNode> ModelsB { get; } = new ObservableCollection<ModelSourceNode>();
        public ICollectionView ModelsViewB { get; }

        public ObservableCollection<SearchSetNode> SetsB { get; } = new ObservableCollection<SearchSetNode>();
        public ICollectionView SetsViewB { get; }

        private int _activeTabIndexA;
        public int ActiveTabIndexA
        {
            get => _activeTabIndexA;
            set
            {
                if (SetProperty(ref _activeTabIndexA, value))
                {
                    if (value == 1 && SetsA.Count == 0)
                    {
                        LoadSearchSets();
                    }
                    UpdateSelectionState();
                }
            }
        }

        private int _activeTabIndexB;
        public int ActiveTabIndexB
        {
            get => _activeTabIndexB;
            set
            {
                if (SetProperty(ref _activeTabIndexB, value))
                {
                    if (value == 1 && SetsB.Count == 0)
                    {
                        LoadSearchSets();
                    }
                    UpdateSelectionState();
                }
            }
        }

        private string _searchTextModelsA = string.Empty;
        public string SearchTextModelsA
        {
            get => _searchTextModelsA;
            set
            {
                if (SetProperty(ref _searchTextModelsA, value))
                {
                    ModelsViewA.Refresh();
                }
            }
        }

        private bool _showNwcA = true;
        public bool ShowNwcA
        {
            get => _showNwcA;
            set
            {
                if (SetProperty(ref _showNwcA, value))
                {
                    if (!value)
                    {
                        foreach (var m in ModelsA.Where(x => !x.IsNwd))
                            m.IsSelected = false;
                    }
                    OnPropertyChanged(nameof(FilterTypeTextA));
                    ModelsViewA.Refresh();
                    UpdateSelectionState();
                }
            }
        }

        private bool _showNwdA = true;
        public bool ShowNwdA
        {
            get => _showNwdA;
            set
            {
                if (SetProperty(ref _showNwdA, value))
                {
                    if (!value)
                    {
                        foreach (var m in ModelsA.Where(x => x.IsNwd))
                            m.IsSelected = false;
                    }
                    OnPropertyChanged(nameof(FilterTypeTextA));
                    ModelsViewA.Refresh();
                    UpdateSelectionState();
                }
            }
        }

        public string FilterTypeTextA
        {
            get
            {
                if (ShowNwcA && ShowNwdA) return "Type: All ▾";
                if (ShowNwcA) return "Type: NWC ▾";
                if (ShowNwdA) return "Type: NWD ▾";
                return "Type: None ▾";
            }
        }

        private string _searchTextSetsA = string.Empty;
        public string SearchTextSetsA
        {
            get => _searchTextSetsA;
            set
            {
                if (SetProperty(ref _searchTextSetsA, value))
                {
                    SetsViewA.Refresh();
                }
            }
        }

        private string _searchTextModelsB = string.Empty;
        public string SearchTextModelsB
        {
            get => _searchTextModelsB;
            set
            {
                if (SetProperty(ref _searchTextModelsB, value))
                {
                    ModelsViewB.Refresh();
                }
            }
        }

        private bool _showNwcB = true;
        public bool ShowNwcB
        {
            get => _showNwcB;
            set
            {
                if (SetProperty(ref _showNwcB, value))
                {
                    if (!value)
                    {
                        foreach (var m in ModelsB.Where(x => !x.IsNwd))
                            m.IsSelected = false;
                    }
                    OnPropertyChanged(nameof(FilterTypeTextB));
                    ModelsViewB.Refresh();
                    UpdateSelectionState();
                }
            }
        }

        private bool _showNwdB = true;
        public bool ShowNwdB
        {
            get => _showNwdB;
            set
            {
                if (SetProperty(ref _showNwdB, value))
                {
                    if (!value)
                    {
                        foreach (var m in ModelsB.Where(x => x.IsNwd))
                            m.IsSelected = false;
                    }
                    OnPropertyChanged(nameof(FilterTypeTextB));
                    ModelsViewB.Refresh();
                    UpdateSelectionState();
                }
            }
        }

        public string FilterTypeTextB
        {
            get
            {
                if (ShowNwcB && ShowNwdB) return "Type: All ▾";
                if (ShowNwcB) return "Type: NWC ▾";
                if (ShowNwdB) return "Type: NWD ▾";
                return "Type: None ▾";
            }
        }

        private string _searchTextSetsB = string.Empty;
        public string SearchTextSetsB
        {
            get => _searchTextSetsB;
            set
            {
                if (SetProperty(ref _searchTextSetsB, value))
                {
                    SetsViewB.Refresh();
                }
            }
        }

        public string[] AvailableDelimiters => new[] { "x", "v", "vs" };

        private string _selectedDelimiter = "x";
        public string SelectedDelimiter
        {
            get => _selectedDelimiter;
            set => SetProperty(ref _selectedDelimiter, value);
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

        public int SelectedModelsCountA => ModelsA.Count(x => x.IsSelected && x.IsSelectable);
        public int SelectedSetsCountA => SetsA.Count(x => x.IsSelected && !x.IsFolder);

        public int SelectedModelsCountB => ModelsB.Count(x => x.IsSelected && x.IsSelectable);
        public int SelectedSetsCountB => SetsB.Count(x => x.IsSelected && !x.IsFolder);

        public int SelectedCountA => ActiveTabIndexA == 0 ? SelectedModelsCountA : SelectedSetsCountA;
        public int SelectedCountB => ActiveTabIndexB == 0 ? SelectedModelsCountB : SelectedSetsCountB;

        public string ModelSelectionSummaryA =>
            $"{SelectedModelsCountA} of {ModelsA.Count} selected";

        public string SetSelectionSummaryA =>
            $"{SelectedSetsCountA} of {SetsA.Count(x => !x.IsFolder)} selected";

        public string ModelSelectionSummaryB =>
            $"{SelectedModelsCountB} of {ModelsB.Count} selected";

        public string SetSelectionSummaryB =>
            $"{SelectedSetsCountB} of {SetsB.Count(x => !x.IsFolder)} selected";

        public string TabHeaderModelsA => SelectedModelsCountA > 0 
            ? $"Models ({SelectedModelsCountA}/{ModelsA.Count})" 
            : $"Models ({ModelsA.Count})";

        public string TabHeaderSetsA => SelectedSetsCountA > 0 
            ? $"Sets ({SelectedSetsCountA}/{SetsA.Count(x => !x.IsFolder)})" 
            : $"Sets ({SetsA.Count(x => !x.IsFolder)})";

        public string TabHeaderModelsB => SelectedModelsCountB > 0 
            ? $"Models ({SelectedModelsCountB}/{ModelsB.Count})" 
            : $"Models ({ModelsB.Count})";

        public string TabHeaderSetsB => SelectedSetsCountB > 0 
            ? $"Sets ({SelectedSetsCountB}/{SetsB.Count(x => !x.IsFolder)})" 
            : $"Sets ({SetsB.Count(x => !x.IsFolder)})";

        public bool HasNoModelsA => ModelsA.Count == 0;
        public bool HasNoSetsA => SetsA.Count == 0;
        public bool HasNoModelsB => ModelsB.Count == 0;
        public bool HasNoSetsB => SetsB.Count == 0;

        // Sorting state
        private ListSortDirection? _sortModelNameA;
        private ListSortDirection? _sortModelTypeA;
        private ListSortDirection? _sortModelNameB;
        private ListSortDirection? _sortModelTypeB;
        private ListSortDirection? _sortSetsPathA;
        private ListSortDirection? _sortSetsPathB;

        public string SortNameTextA => _sortModelNameA == ListSortDirection.Ascending ? "Name ▲" : (_sortModelNameA == ListSortDirection.Descending ? "Name ▼" : "Name ⇅");
        public string SortTypeTextA => _sortModelTypeA == ListSortDirection.Ascending ? "Type ▲" : (_sortModelTypeA == ListSortDirection.Descending ? "Type ▼" : "Type ⇅");

        public string SortNameTextB => _sortModelNameB == ListSortDirection.Ascending ? "Name ▲" : (_sortModelNameB == ListSortDirection.Descending ? "Name ▼" : "Name ⇅");
        public string SortTypeTextB => _sortModelTypeB == ListSortDirection.Ascending ? "Type ▲" : (_sortModelTypeB == ListSortDirection.Descending ? "Type ▼" : "Type ⇅");

        public string SortSetsTextA => _sortSetsPathA == ListSortDirection.Ascending ? "Path ▲" : (_sortSetsPathA == ListSortDirection.Descending ? "Path ▼" : "Path ⇅");
        public string SortSetsTextB => _sortSetsPathB == ListSortDirection.Ascending ? "Path ▲" : (_sortSetsPathB == ListSortDirection.Descending ? "Path ▼" : "Path ⇅");

        public int ExpectedTestCount => SelectedCountA * SelectedCountB;

        public bool IsRunEnabled => ExpectedTestCount > 0 && !IsBusy;

        public ICommand RefreshAllCommand { get; }
        public ICommand RefreshModelsCommand { get; }
        public ICommand RefreshSearchSetsCommand { get; }

        public ICommand SelectAllACommand { get; }
        public ICommand DeselectAllACommand { get; }
        public ICommand ClearACommand { get; }

        public ICommand SelectAllBCommand { get; }
        public ICommand DeselectAllBCommand { get; }
        public ICommand ClearBCommand { get; }

        public ICommand ToggleSortNameACommand { get; }
        public ICommand ToggleSortTypeACommand { get; }
        public ICommand ToggleSortNameBCommand { get; }
        public ICommand ToggleSortTypeBCommand { get; }
        public ICommand ToggleSortSetsACommand { get; }
        public ICommand ToggleSortSetsBCommand { get; }

        public ICommand ClearAllCommand { get; }
        public ICommand RunCommand { get; }

        public MatrixTabViewModel(
            IModelDiscoveryService modelDiscovery,
            ISearchSetService searchSets,
            IClashExecutionService clashExecution,
            IDialogService dialogService,
            ILoggerService logger,
            INamingService naming = null)
        {
            _modelDiscovery = modelDiscovery;
            _searchSets = searchSets;
            _clashExecution = clashExecution;
            _dialogService = dialogService;
            _logger = logger;
            _naming = naming ?? NamingService.Instance;

            // Selection A views
            ModelsViewA = CollectionViewSource.GetDefaultView(ModelsA);
            ModelsViewA.Filter = FilterModelItemA;

            SetsViewA = CollectionViewSource.GetDefaultView(SetsA);
            SetsViewA.Filter = FilterSearchSetItemA;

            // Selection B views
            ModelsViewB = CollectionViewSource.GetDefaultView(ModelsB);
            ModelsViewB.Filter = FilterModelItemB;

            SetsViewB = CollectionViewSource.GetDefaultView(SetsB);
            SetsViewB.Filter = FilterSearchSetItemB;

            RefreshAllCommand = new RelayCommand(_ => LoadAll());
            RefreshModelsCommand = new RelayCommand(_ => LoadModels());
            RefreshSearchSetsCommand = new RelayCommand(_ => LoadSearchSets());

            SelectAllACommand = new RelayCommand(_ => SelectAllVisibleA(true));
            DeselectAllACommand = new RelayCommand(_ => SelectAllVisibleA(false));
            ClearACommand = new RelayCommand(_ => ClearSelectionA());

            SelectAllBCommand = new RelayCommand(_ => SelectAllVisibleB(true));
            DeselectAllBCommand = new RelayCommand(_ => SelectAllVisibleB(false));
            ClearBCommand = new RelayCommand(_ => ClearSelectionB());

            ToggleSortNameACommand = new RelayCommand(_ => ToggleSortNameA());
            ToggleSortTypeACommand = new RelayCommand(_ => ToggleSortTypeA());
            ToggleSortNameBCommand = new RelayCommand(_ => ToggleSortNameB());
            ToggleSortTypeBCommand = new RelayCommand(_ => ToggleSortTypeB());
            ToggleSortSetsACommand = new RelayCommand(_ => ToggleSortSetsA());
            ToggleSortSetsBCommand = new RelayCommand(_ => ToggleSortSetsB());

            ClearAllCommand = new RelayCommand(_ => ClearAllSelections());
            RunCommand = new RelayCommand(_ => RunClashTests(), _ => IsRunEnabled);

            LoadAll();
        }

        private bool FilterModelItemA(object obj)
        {
            if (!(obj is ModelSourceNode node)) return false;

            if (!ShowNwcA && !node.IsNwd) return false;
            if (!ShowNwdA && node.IsNwd) return false;

            if (string.IsNullOrWhiteSpace(SearchTextModelsA)) return true;

            string q = SearchTextModelsA.Trim();
            return (node.DisplayName != null && node.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (node.ParentContainerName != null && node.ParentContainerName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool FilterSearchSetItemA(object obj)
        {
            if (!(obj is SearchSetNode node)) return false;
            if (string.IsNullOrWhiteSpace(SearchTextSetsA)) return true;

            string q = SearchTextSetsA.Trim();
            return node.FullPath != null && node.FullPath.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool FilterModelItemB(object obj)
        {
            if (!(obj is ModelSourceNode node)) return false;

            if (!ShowNwcB && !node.IsNwd) return false;
            if (!ShowNwdB && node.IsNwd) return false;

            if (string.IsNullOrWhiteSpace(SearchTextModelsB)) return true;

            string q = SearchTextModelsB.Trim();
            return (node.DisplayName != null && node.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (node.ParentContainerName != null && node.ParentContainerName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool FilterSearchSetItemB(object obj)
        {
            if (!(obj is SearchSetNode node)) return false;
            if (string.IsNullOrWhiteSpace(SearchTextSetsB)) return true;

            string q = SearchTextSetsB.Trim();
            return node.FullPath != null && node.FullPath.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void LoadAll()
        {
            LoadModels();
            LoadSearchSets();
        }

        public void LoadModels()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                var prevA = new HashSet<string>(
                    ModelsA.Where(x => x.IsSelected).Select(x => x.DisplayName ?? string.Empty));
                var prevB = new HashSet<string>(
                    ModelsB.Where(x => x.IsSelected).Select(x => x.DisplayName ?? string.Empty));

                foreach (var m in ModelsA) m.PropertyChanged -= OnModelPropertyChanged;
                foreach (var m in ModelsB) m.PropertyChanged -= OnModelPropertyChanged;

                ModelsA.Clear();
                ModelsB.Clear();

                var discovered = _modelDiscovery.DiscoverModels(doc);
                foreach (var node in discovered)
                {
                    // Selection A node
                    if (prevA.Contains(node.DisplayName ?? string.Empty))
                    {
                        node.IsSelected = true;
                    }
                    node.PropertyChanged += OnModelPropertyChanged;
                    ModelsA.Add(node);

                    // Selection B node (cloned for independent selection)
                    var nodeB = node.Clone();
                    if (prevB.Contains(nodeB.DisplayName ?? string.Empty))
                    {
                        nodeB.IsSelected = true;
                    }
                    nodeB.PropertyChanged += OnModelPropertyChanged;
                    ModelsB.Add(nodeB);
                }

                ModelsViewA.Refresh();
                ModelsViewB.Refresh();
                ApplyModelSortA();
                ApplyModelSortB();
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
                var prevA = new HashSet<string>(
                    SetsA.Where(x => x.IsSelected).Select(x => x.FullPath ?? string.Empty));
                var prevB = new HashSet<string>(
                    SetsB.Where(x => x.IsSelected).Select(x => x.FullPath ?? string.Empty));

                foreach (var s in SetsA) s.PropertyChanged -= OnSetPropertyChanged;
                foreach (var s in SetsB) s.PropertyChanged -= OnSetPropertyChanged;

                SetsA.Clear();
                SetsB.Clear();

                var sets = _searchSets.GetManualSearchSets(doc);
                foreach (var node in sets)
                {
                    // Selection A node
                    if (prevA.Contains(node.FullPath ?? string.Empty))
                    {
                        node.IsSelected = true;
                    }
                    node.PropertyChanged += OnSetPropertyChanged;
                    SetsA.Add(node);

                    // Selection B node (cloned for independent selection)
                    var setB = node.Clone();
                    if (prevB.Contains(setB.FullPath ?? string.Empty))
                    {
                        setB.IsSelected = true;
                    }
                    setB.PropertyChanged += OnSetPropertyChanged;
                    SetsB.Add(setB);
                }

                SetsViewA.Refresh();
                SetsViewB.Refresh();
                ApplySetsSortA();
                ApplySetsSortB();
                UpdateSelectionState();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load search sets", ex);
                _dialogService.ShowError($"Error loading search sets: {ex.Message}");
            }
        }

        private void ToggleSortNameA()
        {
            _sortModelTypeA = null;
            if (_sortModelNameA == null || _sortModelNameA == ListSortDirection.Descending)
                _sortModelNameA = ListSortDirection.Ascending;
            else
                _sortModelNameA = ListSortDirection.Descending;
            ApplyModelSortA();
        }

        private void ToggleSortTypeA()
        {
            _sortModelNameA = null;
            if (_sortModelTypeA == null || _sortModelTypeA == ListSortDirection.Descending)
                _sortModelTypeA = ListSortDirection.Ascending;
            else
                _sortModelTypeA = ListSortDirection.Descending;
            ApplyModelSortA();
        }

        private void ApplyModelSortA()
        {
            using (ModelsViewA.DeferRefresh())
            {
                ModelsViewA.SortDescriptions.Clear();
                if (_sortModelTypeA.HasValue)
                {
                    ModelsViewA.SortDescriptions.Add(new SortDescription(nameof(ModelSourceNode.ModelType), _sortModelTypeA.Value));
                    ModelsViewA.SortDescriptions.Add(new SortDescription(nameof(ModelSourceNode.DisplayName), ListSortDirection.Ascending));
                }
                else if (_sortModelNameA.HasValue)
                {
                    ModelsViewA.SortDescriptions.Add(new SortDescription(nameof(ModelSourceNode.DisplayName), _sortModelNameA.Value));
                }
            }
            OnPropertyChanged(nameof(SortNameTextA));
            OnPropertyChanged(nameof(SortTypeTextA));
        }

        private void ToggleSortNameB()
        {
            _sortModelTypeB = null;
            if (_sortModelNameB == null || _sortModelNameB == ListSortDirection.Descending)
                _sortModelNameB = ListSortDirection.Ascending;
            else
                _sortModelNameB = ListSortDirection.Descending;
            ApplyModelSortB();
        }

        private void ToggleSortTypeB()
        {
            _sortModelNameB = null;
            if (_sortModelTypeB == null || _sortModelTypeB == ListSortDirection.Descending)
                _sortModelTypeB = ListSortDirection.Ascending;
            else
                _sortModelTypeB = ListSortDirection.Descending;
            ApplyModelSortB();
        }

        private void ApplyModelSortB()
        {
            using (ModelsViewB.DeferRefresh())
            {
                ModelsViewB.SortDescriptions.Clear();
                if (_sortModelTypeB.HasValue)
                {
                    ModelsViewB.SortDescriptions.Add(new SortDescription(nameof(ModelSourceNode.ModelType), _sortModelTypeB.Value));
                    ModelsViewB.SortDescriptions.Add(new SortDescription(nameof(ModelSourceNode.DisplayName), ListSortDirection.Ascending));
                }
                else if (_sortModelNameB.HasValue)
                {
                    ModelsViewB.SortDescriptions.Add(new SortDescription(nameof(ModelSourceNode.DisplayName), _sortModelNameB.Value));
                }
            }
            OnPropertyChanged(nameof(SortNameTextB));
            OnPropertyChanged(nameof(SortTypeTextB));
        }

        private void ToggleSortSetsA()
        {
            if (_sortSetsPathA == null || _sortSetsPathA == ListSortDirection.Descending)
                _sortSetsPathA = ListSortDirection.Ascending;
            else
                _sortSetsPathA = ListSortDirection.Descending;
            ApplySetsSortA();
        }

        private void ApplySetsSortA()
        {
            using (SetsViewA.DeferRefresh())
            {
                SetsViewA.SortDescriptions.Clear();
                if (_sortSetsPathA.HasValue)
                {
                    SetsViewA.SortDescriptions.Add(new SortDescription(nameof(SearchSetNode.FullPath), _sortSetsPathA.Value));
                }
            }
            OnPropertyChanged(nameof(SortSetsTextA));
        }

        private void ToggleSortSetsB()
        {
            if (_sortSetsPathB == null || _sortSetsPathB == ListSortDirection.Descending)
                _sortSetsPathB = ListSortDirection.Ascending;
            else
                _sortSetsPathB = ListSortDirection.Descending;
            ApplySetsSortB();
        }

        private void ApplySetsSortB()
        {
            using (SetsViewB.DeferRefresh())
            {
                SetsViewB.SortDescriptions.Clear();
                if (_sortSetsPathB.HasValue)
                {
                    SetsViewB.SortDescriptions.Add(new SortDescription(nameof(SearchSetNode.FullPath), _sortSetsPathB.Value));
                }
            }
            OnPropertyChanged(nameof(SortSetsTextB));
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
            OnPropertyChanged(nameof(SelectedModelsCountA));
            OnPropertyChanged(nameof(SelectedSetsCountA));
            OnPropertyChanged(nameof(SelectedModelsCountB));
            OnPropertyChanged(nameof(SelectedSetsCountB));
            OnPropertyChanged(nameof(SelectedCountA));
            OnPropertyChanged(nameof(SelectedCountB));
            OnPropertyChanged(nameof(ExpectedTestCount));
            OnPropertyChanged(nameof(IsRunEnabled));
            OnPropertyChanged(nameof(ModelSelectionSummaryA));
            OnPropertyChanged(nameof(SetSelectionSummaryA));
            OnPropertyChanged(nameof(ModelSelectionSummaryB));
            OnPropertyChanged(nameof(SetSelectionSummaryB));
            OnPropertyChanged(nameof(TabHeaderModelsA));
            OnPropertyChanged(nameof(TabHeaderSetsA));
            OnPropertyChanged(nameof(TabHeaderModelsB));
            OnPropertyChanged(nameof(TabHeaderSetsB));
            OnPropertyChanged(nameof(HasNoModelsA));
            OnPropertyChanged(nameof(HasNoSetsA));
            OnPropertyChanged(nameof(HasNoModelsB));
            OnPropertyChanged(nameof(HasNoSetsB));
            (RunCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SelectAllVisibleA(bool isSelected)
        {
            if (ActiveTabIndexA == 0)
            {
                foreach (var item in ModelsViewA)
                {
                    if (item is ModelSourceNode node && node.IsSelectable)
                    {
                        node.IsSelected = isSelected;
                    }
                }
            }
            else
            {
                foreach (var item in SetsViewA)
                {
                    if (item is SearchSetNode node && !node.IsFolder)
                    {
                        node.IsSelected = isSelected;
                    }
                }
            }
        }

        private void SelectAllVisibleB(bool isSelected)
        {
            if (ActiveTabIndexB == 0)
            {
                foreach (var item in ModelsViewB)
                {
                    if (item is ModelSourceNode node && node.IsSelectable)
                    {
                        node.IsSelected = isSelected;
                    }
                }
            }
            else
            {
                foreach (var item in SetsViewB)
                {
                    if (item is SearchSetNode node && !node.IsFolder)
                    {
                        node.IsSelected = isSelected;
                    }
                }
            }
        }

        private void ClearSelectionA()
        {
            foreach (var m in ModelsA) m.IsSelected = false;
            foreach (var s in SetsA) s.IsSelected = false;
        }

        private void ClearSelectionB()
        {
            foreach (var m in ModelsB) m.IsSelected = false;
            foreach (var s in SetsB) s.IsSelected = false;
        }

        private void ClearAllSelections()
        {
            ClearSelectionA();
            ClearSelectionB();
        }

        private void RunClashTests()
        {
            List<ISelectableItem> itemsA;
            string typeAName;
            if (ActiveTabIndexA == 0)
            {
                itemsA = ModelsA.Where(x => x.IsSelected && x.IsSelectable && ((ShowNwcA && !x.IsNwd) || (ShowNwdA && x.IsNwd))).Cast<ISelectableItem>().ToList();
                typeAName = "Model(s)";
            }
            else
            {
                itemsA = SetsA.Where(x => x.IsSelected && !x.IsFolder).Cast<ISelectableItem>().ToList();
                typeAName = "Set(s)";
            }

            List<ISelectableItem> itemsB;
            string typeBName;
            if (ActiveTabIndexB == 0)
            {
                itemsB = ModelsB.Where(x => x.IsSelected && x.IsSelectable && ((ShowNwcB && !x.IsNwd) || (ShowNwdB && x.IsNwd))).Cast<ISelectableItem>().ToList();
                typeBName = "Model(s)";
            }
            else
            {
                itemsB = SetsB.Where(x => x.IsSelected && !x.IsFolder).Cast<ISelectableItem>().ToList();
                typeBName = "Set(s)";
            }

            if (itemsA.Count == 0)
            {
                _dialogService.ShowWarning($"Please select at least one item from Selection A ({typeAName}).", "No Selection A");
                return;
            }
            if (itemsB.Count == 0)
            {
                _dialogService.ShowWarning($"Please select at least one item from Selection B ({typeBName}).", "No Selection B");
                return;
            }

            int count = itemsA.Count * itemsB.Count;
            bool confirm = _dialogService.ShowConfirmation(
                $"Generate and run {count} clash test combinations?\n\n" +
                $"Selection A: {itemsA.Count} {typeAName}\n" +
                $"Selection B: {itemsB.Count} {typeBName}\n" +
                $"Clash Type: {SelectedClashType}\n" +
                $"Tolerance: {Tolerance:F4} m\n" +
                $"Delimiter: '{SelectedDelimiter}'\n\n" +
                $"Naming formula: Selection A {SelectedDelimiter} Selection B",
                "Confirm Clash Matrix Execution");

            if (!confirm) return;

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null || doc.IsClear)
            {
                _dialogService.ShowError("Active document is not available or is empty.");
                return;
            }

            IsBusy = true;
            ProgressText = "Initializing Clash Matrix...";
            ProgressBarValue = 0;
            ProgressBarMax = count;

            try
            {
                var result = _clashExecution.RunGenericClashMatrix(
                    doc,
                    itemsA,
                    itemsB,
                    SelectedClashType,
                    Tolerance,
                    SelectedDelimiter,
                    (status, current, total) =>
                    {
                        ProgressText = status;
                        ProgressBarValue = current;
                        ProgressBarMax = total;
                        DoEvents();
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

        private static void DoEvents() => DispatcherUtils.DoEvents();
    }
}

