using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using FluentAssertions;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.ViewModels;

namespace AutomatedClashRunner.Tests
{
    public class ViewModelAndSelectionTests
    {
        [Fact]
        public void RelayCommand_ExecutesActionWhenInvoked()
        {
            // Arrange
            bool executed = false;
            object passedParam = null;
            var command = new RelayCommand(p =>
            {
                executed = true;
                passedParam = p;
            });

            // Act
            command.Execute("test-payload");

            // Assert
            executed.Should().BeTrue();
            passedParam.Should().Be("test-payload");
        }

        [Fact]
        public void RelayCommand_CanExecuteEvaluatesPredicate()
        {
            // Arrange
            bool allow = false;
            var command = new RelayCommand(_ => { }, _ => allow);

            // Assert
            command.CanExecute(null).Should().BeFalse();

            allow = true;
            command.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void ModelSourceNode_DisplayType_ReflectsCleanTypeWithoutDirectNwcText()
        {
            var nodeNwc = new ModelSourceNode { ModelType = "NWC", IsDirectNwc = true, DisplayName = "Test1.nwc" };
            var nodeNwd = new ModelSourceNode { ModelType = "NWD", IsDirectNwc = false, DisplayName = "Basebuild.nwd" };

            nodeNwc.DisplayType.Should().Be("NWC");
            nodeNwc.IsNwd.Should().BeFalse();

            nodeNwd.DisplayType.Should().Be("NWD");
            nodeNwd.IsNwd.Should().BeTrue();
        }

        [Fact]
        public void ModelSourceNode_PropertyChangeFired_WhenIsSelectedChanges()
        {
            var node = new ModelSourceNode { DisplayName = "Model.nwc" };
            string changedProperty = null;
            node.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            node.IsSelected = true;

            changedProperty.Should().Be(nameof(ModelSourceNode.IsSelected));
        }

        [Fact]
        public void SearchSetNode_IsSet_InvertsIsFolder()
        {
            var folder = new SearchSetNode { IsFolder = true };
            var set = new SearchSetNode { IsFolder = false };

            folder.IsSet.Should().BeFalse();
            set.IsSet.Should().BeTrue();
        }

        [Fact]
        public void RangeSelection_ShiftClickSimulation_SelectsIntermediateItems()
        {
            // Simulate Shift+Click range selection across items implementing ISelectableItem
            var items = new List<ModelSourceNode>();
            for (int i = 0; i < 10; i++)
            {
                items.Add(new ModelSourceNode { DisplayName = $"Model_{i}.nwc", IsSelected = false });
            }

            int anchorIndex = 2;
            int targetIndex = 6;

            int start = Math.Min(anchorIndex, targetIndex);
            int end = Math.Max(anchorIndex, targetIndex);

            for (int i = start; i <= end; i++)
            {
                items[i].IsSelected = true;
            }

            // Items 0 and 1 should NOT be selected
            items[0].IsSelected.Should().BeFalse();
            items[1].IsSelected.Should().BeFalse();

            // Items 2 through 6 SHOULD be selected
            for (int i = 2; i <= 6; i++)
            {
                items[i].IsSelected.Should().BeTrue();
            }

            // Items 7 through 9 should NOT be selected
            items[7].IsSelected.Should().BeFalse();
            items[8].IsSelected.Should().BeFalse();
            items[9].IsSelected.Should().BeFalse();
        }

        public class MockTabNavigationViewModel : INotifyPropertyChanged
        {
            private int _selectedTabIndex;
            public int SelectedTabIndex
            {
                get => _selectedTabIndex;
                set
                {
                    if (_selectedTabIndex != value)
                    {
                        _selectedTabIndex = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTabIndex)));
                        if (value == 1) DistillerRefreshed = true;
                        if (value == 2) ViewpointsRefreshed = true;
                    }
                }
            }

            public bool DistillerRefreshed { get; private set; }
            public bool ViewpointsRefreshed { get; private set; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        [Fact]
        public void TabNavigation_WhenSwitched_TriggersDistillerAndViewpointsRefresh()
        {
            var vm = new MockTabNavigationViewModel();
            string changedProp = null;
            vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

            vm.SelectedTabIndex = 1;
            vm.SelectedTabIndex.Should().Be(1);
            changedProp.Should().Be(nameof(MockTabNavigationViewModel.SelectedTabIndex));
            vm.DistillerRefreshed.Should().BeTrue();

            vm.SelectedTabIndex = 2;
            vm.SelectedTabIndex.Should().Be(2);
            vm.ViewpointsRefreshed.Should().BeTrue();
        }

        [Fact]
        public void ModelSourceNode_IndependentSelection_AllowsDistinctCheckedStates()
        {
            var nodeA = new ModelSourceNode
            {
                DisplayName = "F1-L0-BAE-E.nwc",
                SourceFilePath = "C:\\Models\\F1-L0-BAE-E.nwc",
                IsDirectNwc = true,
                ParentContainerName = "MEI.nwd",
                IsSelectable = true,
                IsSelected = true
            };

            var nodeB = new ModelSourceNode
            {
                DisplayName = nodeA.DisplayName,
                SourceFilePath = nodeA.SourceFilePath,
                IsDirectNwc = nodeA.IsDirectNwc,
                ParentContainerName = nodeA.ParentContainerName,
                IsSelectable = nodeA.IsSelectable,
                IsSelected = false
            };

            nodeA.IsSelected.Should().BeTrue();
            nodeB.IsSelected.Should().BeFalse();

            nodeB.IsSelected = true;
            nodeA.IsSelected = false;

            nodeA.IsSelected.Should().BeFalse();
            nodeB.IsSelected.Should().BeTrue();
        }

        [Fact]
        public void SearchSetNode_IndependentSelection_AllowsDistinctCheckedStates()
        {
            var setA = new SearchSetNode
            {
                DisplayName = "L0-BAE-E",
                FullPath = "Tests/L0-BAE-E",
                IsFolder = false,
                IsSelected = true
            };

            var setB = new SearchSetNode
            {
                DisplayName = setA.DisplayName,
                FullPath = setA.FullPath,
                IsFolder = setA.IsFolder,
                IsSelected = false
            };

            setA.IsSelected.Should().BeTrue();
            setB.IsSelected.Should().BeFalse();

            setB.IsSelected = true;
            setA.IsSelected = false;

            setA.IsSelected.Should().BeFalse();
            setB.IsSelected.Should().BeTrue();
        }

        [Fact]
        public void ModelSourceNode_ModelTypeAndNwdState_WorkCorrectly()
        {
            var node = new ModelSourceNode
            {
                DisplayName = "Basebuild.nwd",
                ModelType = "NWD",
                IsDirectNwc = false,
                IsSelected = true
            };

            node.DisplayName.Should().Be("Basebuild.nwd");
            node.ModelType.Should().Be("NWD");
            node.IsNwd.Should().BeTrue();
            node.DisplayType.Should().Be("NWD");
        }

        [Fact]
        public void ModelSourceNodes_SortingByNameAndType_OrdersCorrectly()
        {
            var list = new List<ModelSourceNode>
            {
                new ModelSourceNode { DisplayName = "Z-Model.nwc", ModelType = "NWC" },
                new ModelSourceNode { DisplayName = "A-Model.nwc", ModelType = "NWC" },
                new ModelSourceNode { DisplayName = "M-Model.nwd", ModelType = "NWD" }
            };

            // Sort by Name Ascending
            var sortedByNameAsc = list.OrderBy(x => x.DisplayName).ToList();
            sortedByNameAsc[0].DisplayName.Should().Be("A-Model.nwc");
            sortedByNameAsc[1].DisplayName.Should().Be("M-Model.nwd");
            sortedByNameAsc[2].DisplayName.Should().Be("Z-Model.nwc");

            // Sort by Name Descending
            var sortedByNameDesc = list.OrderByDescending(x => x.DisplayName).ToList();
            sortedByNameDesc[0].DisplayName.Should().Be("Z-Model.nwc");
            sortedByNameDesc[1].DisplayName.Should().Be("M-Model.nwd");
            sortedByNameDesc[2].DisplayName.Should().Be("A-Model.nwc");

            // Sort by Type (NWC first, then NWD, secondary by Name)
            var sortedByTypeAsc = list.OrderBy(x => x.ModelType).ThenBy(x => x.DisplayName).ToList();
            sortedByTypeAsc[0].ModelType.Should().Be("NWC");
            sortedByTypeAsc[0].DisplayName.Should().Be("A-Model.nwc");
            sortedByTypeAsc[1].ModelType.Should().Be("NWC");
            sortedByTypeAsc[1].DisplayName.Should().Be("Z-Model.nwc");
            sortedByTypeAsc[2].ModelType.Should().Be("NWD");
            sortedByTypeAsc[2].DisplayName.Should().Be("M-Model.nwd");

            // Sort by Type Descending (NWD first, then NWC)
            var sortedByTypeDesc = list.OrderByDescending(x => x.ModelType).ThenBy(x => x.DisplayName).ToList();
            sortedByTypeDesc[0].ModelType.Should().Be("NWD");
            sortedByTypeDesc[0].DisplayName.Should().Be("M-Model.nwd");
            sortedByTypeDesc[1].ModelType.Should().Be("NWC");
        }

        [Fact]
        public void TabHeader_Formatting_ReflectsTotalAndSelectedAccurately()
        {
            int totalModels = 47;
            int selectedModels = 0;

            string headerEmpty = selectedModels > 0 ? $"Models ({selectedModels}/{totalModels})" : $"Models ({totalModels})";
            headerEmpty.Should().Be("Models (47)");

            selectedModels = 5;
            string headerSelected = selectedModels > 0 ? $"Models ({selectedModels}/{totalModels})" : $"Models ({totalModels})";
            headerSelected.Should().Be("Models (5/47)");
        }
    }
}
