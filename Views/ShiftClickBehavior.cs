using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Views
{
    /// <summary>
    /// WPF attached behavior that enables Shift+Click range selection on ListView controls.
    /// When the user clicks a row, it toggles IsSelected. When Shift+clicking, it toggles
    /// all items between the anchor and the clicked row to match the anchor's state.
    /// </summary>
    public static class ShiftClickBehavior
    {
        // Attached property: set to True on a ListView to enable shift-click
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ShiftClickBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        // Track the last-clicked index per ListView instance
        private static readonly Dictionary<ListView, int> _anchorIndex = new Dictionary<ListView, int>();

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
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListView lv)
            {
                lv.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                lv.Unloaded -= OnUnloaded;
                _anchorIndex.Remove(lv);
            }
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListView listView)) return;

            // Find the ListViewItem that was clicked
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;

            // If the click landed directly on a CheckBox, let WPF handle it normally
            // We only intercept row-level clicks
            if (IsCheckBoxClick(originalSource)) return;

            var listViewItem = FindAncestor<ListViewItem>(originalSource);
            if (listViewItem == null) return;

            var clickedItem = listViewItem.Content as ISelectableItem;
            if (clickedItem == null) return;

            int clickedIndex = listView.Items.IndexOf(clickedItem);
            if (clickedIndex < 0) return;

            bool isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (isShift && _anchorIndex.ContainsKey(listView))
            {
                int anchorIdx = _anchorIndex[listView];
                if (anchorIdx >= 0 && anchorIdx < listView.Items.Count)
                {
                    // Determine the target state: toggle the anchor's current state
                    var anchorItem = listView.Items[anchorIdx] as ISelectableItem;
                    bool targetState = anchorItem != null ? !anchorItem.IsSelected : true;

                    int start = Math.Min(anchorIdx, clickedIndex);
                    int end = Math.Max(anchorIdx, clickedIndex);

                    for (int i = start; i <= end; i++)
                    {
                        if (listView.Items[i] is ISelectableItem item)
                        {
                            item.IsSelected = targetState;
                        }
                    }

                    e.Handled = true;
                    return;
                }
            }

            // Normal click (no Shift): toggle item and set anchor
            clickedItem.IsSelected = !clickedItem.IsSelected;
            _anchorIndex[listView] = clickedIndex;
            e.Handled = true;
        }

        private static bool IsCheckBoxClick(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is System.Windows.Controls.CheckBox)
                    return true;
                if (current is ListViewItem)
                    return false;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T found) return found;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
