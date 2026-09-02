using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Views
{
    /// <summary>
    /// WPF attached behavior that enables Shift+Click range selection on ListView controls.
    /// Supports clicking anywhere on a row or directly on a checkbox.
    /// When Shift is held, all items between the last anchor and clicked row are set to the target state.
    /// </summary>
    public static class ShiftClickBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ShiftClickBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static readonly Dictionary<ListView, int> _anchorIndex = new Dictionary<ListView, int>();
        private static readonly Dictionary<ListView, bool> _anchorState = new Dictionary<ListView, bool>();

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ListView listView)) return;

            if ((bool)e.NewValue)
            {
                listView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                listView.Unloaded += OnUnloaded;
            }
            else
            {
                listView.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                listView.Unloaded -= OnUnloaded;
                _anchorIndex.Remove(listView);
                _anchorState.Remove(listView);
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListView lv)
            {
                lv.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                lv.Unloaded -= OnUnloaded;
                _anchorIndex.Remove(lv);
                _anchorState.Remove(lv);
            }
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListView listView)) return;

            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;

            // Find the ListViewItem containing the click
            var listViewItem = FindAncestor<ListViewItem>(originalSource);
            if (listViewItem == null) return;

            var clickedItem = listViewItem.Content as ISelectableItem;
            if (clickedItem == null) return;

            // Check if item is selectable
            if (clickedItem is ModelSourceNode modelNode && !modelNode.IsSelectable) return;
            if (clickedItem is SearchSetNode setNode && setNode.IsFolder) return;

            int clickedIndex = listView.Items.IndexOf(clickedItem);
            if (clickedIndex < 0) return;

            bool isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (isShift && _anchorIndex.ContainsKey(listView))
            {
                int anchorIdx = _anchorIndex[listView];
                if (anchorIdx >= 0 && anchorIdx < listView.Items.Count)
                {
                    // Target state is the state of the anchor (e.g. true if anchor was selected)
                    bool targetState = _anchorState.ContainsKey(listView) 
                        ? _anchorState[listView] 
                        : (listView.Items[anchorIdx] as ISelectableItem)?.IsSelected ?? true;

                    int start = Math.Min(anchorIdx, clickedIndex);
                    int end = Math.Max(anchorIdx, clickedIndex);

                    for (int i = start; i <= end; i++)
                    {
                        var item = listView.Items[i] as ISelectableItem;
                        if (item == null) continue;

                        if (item is ModelSourceNode m && !m.IsSelectable) continue;
                        if (item is SearchSetNode s && s.IsFolder) continue;

                        item.IsSelected = targetState;
                    }

                    e.Handled = true;
                    return;
                }
            }

            // Normal click (or first click): toggle selection and set as anchor
            bool newState = !clickedItem.IsSelected;
            clickedItem.IsSelected = newState;
            _anchorIndex[listView] = clickedIndex;
            _anchorState[listView] = newState;

            e.Handled = true;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T found) return found;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
