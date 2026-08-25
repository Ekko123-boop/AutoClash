using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services;
using AutomatedClashRunner.Views;

namespace AutomatedClashRunner.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly Window _window;
        private string _searchTextModels;
        private string _searchTextSets;

        public ObservableCollection<ModelSourceNode> AllModels { get; set; } = new ObservableCollection<ModelSourceNode>();
        public ObservableCollection<ModelSourceNode> FilteredModels { get; set; } = new ObservableCollection<ModelSourceNode>();

        public ObservableCollection<SearchSetNode> AllSearchSets { get; set; } = new ObservableCollection<SearchSetNode>();
        public ObservableCollection<SearchSetNode> FilteredSearchSets { get; set; } = new ObservableCollection<SearchSetNode>();

        public string SearchTextModels
        {
            get => _searchTextModels;
            set
            {
                _searchTextModels = value;
                OnPropertyChanged();
                FilterModels();
            }
        }

        public string SearchTextSets
        {
            get => _searchTextSets;
            set
            {
                _searchTextSets = value;
                OnPropertyChanged();
                FilterSearchSets();
            }
        }

        public int ExpectedTestCount
        {
            get
            {
                int m = AllModels.Count(x => x.IsSelected && x.IsSelectable);
                int s = AllSearchSets.Count(x => x.IsSelected && !x.IsFolder);
                return m * s;
            }
        }

        public bool IsRunEnabled => ExpectedTestCount > 0;

        public RelayCommand RefreshModelsCommand { get; }
        public RelayCommand RefreshSearchSetsCommand { get; }
        public RelayCommand ClearAllCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand RunCommand { get; }

        public MainViewModel(Window window)
        {
            _window = window;
            RefreshModelsCommand = new RelayCommand(_ => LoadModels());
            RefreshSearchSetsCommand = new RelayCommand(_ => LoadSearchSets());
            ClearAllCommand = new RelayCommand(_ => ClearSelection());
            CancelCommand = new RelayCommand(_ => _window.Close());
            RunCommand = new RelayCommand(_ => RunClashTests(), _ => IsRunEnabled);

            LoadModels();
            LoadSearchSets();
        }

        private void LoadModels()
        {
            var selectedPaths = AllModels.Where(x => x.IsSelected).Select(x => x.SourceFilePath ?? x.DisplayName).ToList();
            AllModels.Clear();
            foreach (var m in ModelDiscoveryService.DiscoverModels())
            {
                if (selectedPaths.Contains(m.SourceFilePath ?? m.DisplayName))
                    m.IsSelected = true;
                
                m.PropertyChanged += (s, e) => 
                {
                    if (e.PropertyName == nameof(ModelSourceNode.IsSelected))
                    {
                        OnPropertyChanged(nameof(ExpectedTestCount));
                        OnPropertyChanged(nameof(IsRunEnabled));
                    }
                };
                AllModels.Add(m);
            }
            FilterModels();
        }

        private void LoadSearchSets()
        {
            var selectedPaths = AllSearchSets.Where(x => x.IsSelected).Select(x => x.FullPath).ToList();
            AllSearchSets.Clear();
            foreach (var s in SearchSetService.GetManualSearchSets())
            {
                if (selectedPaths.Contains(s.FullPath))
                    s.IsSelected = true;
                
                s.PropertyChanged += (sender, e) => 
                {
                    if (e.PropertyName == nameof(SearchSetNode.IsSelected))
                    {
                        OnPropertyChanged(nameof(ExpectedTestCount));
                        OnPropertyChanged(nameof(IsRunEnabled));
                    }
                };
                AllSearchSets.Add(s);
            }
            FilterSearchSets();
        }

        private void FilterModels()
        {
            FilteredModels.Clear();
            var q = string.IsNullOrWhiteSpace(SearchTextModels) ? string.Empty : SearchTextModels.ToLower();
            foreach (var m in AllModels)
            {
                if (string.IsNullOrEmpty(q) || 
                    (m.DisplayName != null && m.DisplayName.ToLower().Contains(q)) || 
                    (m.SourceFilePath != null && m.SourceFilePath.ToLower().Contains(q)) ||
                    (m.ParentContainerName != null && m.ParentContainerName.ToLower().Contains(q)))
                {
                    FilteredModels.Add(m);
                }
            }
        }

        private void FilterSearchSets()
        {
            FilteredSearchSets.Clear();
            var q = string.IsNullOrWhiteSpace(SearchTextSets) ? string.Empty : SearchTextSets.ToLower();
            foreach (var s in AllSearchSets)
            {
                if (string.IsNullOrEmpty(q) || (s.FullPath != null && s.FullPath.ToLower().Contains(q)))
                {
                    FilteredSearchSets.Add(s);
                }
            }
        }

        private void ClearSelection()
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
                MessageBox.Show("Please select at least one Manual Search Set from the right panel to clash the models against.", "No Sets Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (selectedModels.Count == 0)
            {
                MessageBox.Show("Please select at least one Model from the left panel.", "No Models Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show($"Are you sure you want to run {selectedModels.Count * selectedSets.Count} combinations?\nModels: {selectedModels.Count}\nSets: {selectedSets.Count}", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            var result = ClashExecutionService.RunClashMatrix(selectedSets, selectedModels);
            
            var summaryDialog = new SummaryDialog(result);
            summaryDialog.Owner = _window;
            summaryDialog.ShowDialog();
        }
    }
}
