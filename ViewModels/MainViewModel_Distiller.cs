using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services;
using Application = Autodesk.Navisworks.Api.Application;

namespace AutomatedClashRunner.ViewModels
{
    public partial class MainViewModel
    {
        // ============================================
        // CLASH DISTILLER / MANAGE TESTS TAB
        // ============================================
        
        private ObservableCollection<ClashTestNode> _allTests = new ObservableCollection<ClashTestNode>();
        public ObservableCollection<ClashTestNode> FilteredTests { get; } = new ObservableCollection<ClashTestNode>();

        private string _searchTextTests;
        public string SearchTextTests
        {
            get => _searchTextTests;
            set
            {
                if (_searchTextTests != value)
                {
                    _searchTextTests = value;
                    OnPropertyChanged(nameof(SearchTextTests));
                    FilterTests();
                }
            }
        }

        public ICommand RefreshTestsCommand { get; }
        public ICommand GroupByElementCommand { get; }
        public ICommand ExportViewpointsCommand { get; }
        public ICommand ReRunSelectedCommand { get; }

        private void InitializeDistillerTab()
        {
            RefreshTestsCommand = new RelayCommand(_ => LoadTests());
            GroupByElementCommand = new RelayCommand(_ => GroupSelectedTests());
            ExportViewpointsCommand = new RelayCommand(_ => ExportSelectedViewpoints());
            ReRunSelectedCommand = new RelayCommand(_ => ReRunSelectedTests());
            
            LoadTests();
        }

        private void LoadTests()
        {
            _allTests.Clear();
            var doc = Application.ActiveDocument;
            var clashData = doc.GetClash().TestsData;
            
            if (clashData != null && clashData.Tests != null)
            {
                foreach (SavedItem item in clashData.Tests)
                {
                    if (item is ClashTest test)
                    {
                        var node = new ClashTestNode(test);
                        _allTests.Add(node);
                    }
                }
            }
            FilterTests();
        }

        private void FilterTests()
        {
            FilteredTests.Clear();
            var q = string.IsNullOrWhiteSpace(SearchTextTests) ? string.Empty : SearchTextTests.ToLower();
            foreach (var t in _allTests)
            {
                if (string.IsNullOrEmpty(q) || (t.DisplayName != null && t.DisplayName.ToLower().Contains(q)))
                {
                    FilteredTests.Add(t);
                }
            }
        }

        private void ReRunSelectedTests()
        {
            var selected = _allTests.Where(t => t.IsSelected).Select(t => t.OriginalTest).ToList();
            if (selected.Count == 0) return;
            
            ClashDistillerService.ReRunTests(selected);
            LoadTests();
            MessageBox.Show($""Re-ran {selected.Count} tests successfully."", ""Success"", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GroupSelectedTests()
        {
            var selected = _allTests.Where(t => t.IsSelected).Select(t => t.OriginalTest).ToList();
            if (selected.Count == 0) return;
            
            int groupsCreated = ClashDistillerService.GroupByElement(selected);
            LoadTests();
            MessageBox.Show($""Grouped clashes! Created {groupsCreated} new groups across {selected.Count} tests."", ""Success"", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportSelectedViewpoints()
        {
            var selected = _allTests.Where(t => t.IsSelected).Select(t => t.OriginalTest).ToList();
            if (selected.Count == 0) return;
            
            int vpCreated = ClashDistillerService.ExportReviewedViewpoints(selected);
            LoadTests();
            MessageBox.Show($""Exported {vpCreated} 'Reviewed' viewpoints into the Saved Viewpoints window."", ""Success"", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
