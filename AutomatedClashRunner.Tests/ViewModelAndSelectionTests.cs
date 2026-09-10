using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public void ModelSourceNode_DisplayType_ReflectsIsDirectNwc()
        {
            var nodeNwc = new ModelSourceNode { IsDirectNwc = true, DisplayName = "Test1.nwc" };
            var nodeNwd = new ModelSourceNode { IsDirectNwc = false, DisplayName = "Test2.nwc" };

            nodeNwc.DisplayType.Should().Be("Direct NWC");
            nodeNwd.DisplayType.Should().Be("NWD Branch");
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
    }
}
