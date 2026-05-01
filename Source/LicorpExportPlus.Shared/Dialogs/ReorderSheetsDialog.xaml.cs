using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LicorpExportPlus.Models;
using LicorpExportPlus.Utils;
using WpfPoint = System.Windows.Point;

namespace LicorpExportPlus.Dialogs
{
    public partial class ReorderSheetsDialog : Window
    {
        private ObservableCollection<ReorderItem> _items;
        private List<ReorderItem> _originalItems;
        private WpfPoint _startPoint;
        private bool _isDragging = false;

        public List<string> ReorderedIds { get; private set; }

        public ReorderSheetsDialog(List<object> items, bool isSheetsMode)
        {
            InitializeComponent();
            
            _items = new ObservableCollection<ReorderItem>();
            
            int index = 1;
            foreach (var item in items)
            {
                if (isSheetsMode && item is SheetItem sheet)
                {
                    _items.Add(new ReorderItem
                    {
                        Index = index++,
                        Id = sheet.Id.GetIdValue().ToString(),
                        Number = sheet.SheetNumber,
                        Name = sheet.SheetName,
                        DisplayText = $"{sheet.SheetNumber} - {sheet.SheetName}"
                    });
                }
                else if (!isSheetsMode && item is ViewItem view)
                {
                    _items.Add(new ReorderItem
                    {
                        Index = index++,
                        Id = view.ViewId,
                        Number = string.IsNullOrWhiteSpace(view.ViewNumber) ? view.ViewType : view.ViewNumber,
                        Name = view.ViewName,
                        DisplayText = $"{view.ViewName}"
                    });
                }
            }
            
            _originalItems = _items.Select(CloneReorderItem).ToList();
            ItemsDataGrid.ItemsSource = _items;
        }

        private void ItemsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void ItemsGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                WpfPoint mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var dataGrid = sender as DataGrid;
                    var item = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
                    
                    if (item != null && dataGrid != null)
                    {
                        var dataItem = dataGrid.ItemContainerGenerator.ItemFromContainer(item);
                        if (dataItem != null)
                        {
                            _isDragging = true;
                            DragDrop.DoDragDrop(item, dataItem, DragDropEffects.Move);
                            _isDragging = false;
                        }
                    }
                }
            }
        }

        private void ItemsGrid_DragOver(object sender, DragEventArgs e)
        {
            // Allow drop
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void ItemsGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ReorderItem)))
            {
                var droppedItem = e.Data.GetData(typeof(ReorderItem)) as ReorderItem;
                var target = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
                
                if (target != null && droppedItem != null)
                {
                    var targetItem = ItemsDataGrid.ItemContainerGenerator.ItemFromContainer(target) as ReorderItem;
                    
                    if (targetItem != null && droppedItem != targetItem)
                    {
                        int oldIndex = _items.IndexOf(droppedItem);
                        int newIndex = _items.IndexOf(targetItem);
                        
                        _items.Move(oldIndex, newIndex);
                        UpdateIndices();
                    }
                }
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var selected = ItemsDataGrid.SelectedItem as ReorderItem;
            if (selected != null)
            {
                int index = _items.IndexOf(selected);
                if (index > 0)
                {
                    _items.Move(index, index - 1);
                    UpdateIndices();
                    ItemsDataGrid.SelectedIndex = index - 1;
                }
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var selected = ItemsDataGrid.SelectedItem as ReorderItem;
            if (selected != null)
            {
                int index = _items.IndexOf(selected);
                if (index < _items.Count - 1)
                {
                    _items.Move(index, index + 1);
                    UpdateIndices();
                    ItemsDataGrid.SelectedIndex = index + 1;
                }
            }
        }

        private void MoveTop_Click(object sender, RoutedEventArgs e)
        {
            var selected = ItemsDataGrid.SelectedItem as ReorderItem;
            if (selected == null) return;

            int index = _items.IndexOf(selected);
            if (index > 0)
            {
                _items.Move(index, 0);
                UpdateIndices();
                ItemsDataGrid.SelectedIndex = 0;
            }
        }

        private void MoveBottom_Click(object sender, RoutedEventArgs e)
        {
            var selected = ItemsDataGrid.SelectedItem as ReorderItem;
            if (selected == null) return;

            int index = _items.IndexOf(selected);
            if (index >= 0 && index < _items.Count - 1)
            {
                _items.Move(index, _items.Count - 1);
                UpdateIndices();
                ItemsDataGrid.SelectedIndex = _items.Count - 1;
            }
        }

        private void ResetOrder_Click(object sender, RoutedEventArgs e)
        {
            _items.Clear();
            foreach (var item in _originalItems.Select(CloneReorderItem))
            {
                _items.Add(item);
            }
            UpdateIndices();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            ReorderedIds = _items.Select(item => item.Id).ToList();
            DialogResult = true;
            Close();
        }

        private void UpdateIndices()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Index = i + 1;
            }
        }

        private static ReorderItem CloneReorderItem(ReorderItem item)
        {
            return new ReorderItem
            {
                Index = item.Index,
                Id = item.Id,
                Number = item.Number,
                Name = item.Name,
                DisplayText = item.DisplayText
            };
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }

    /// <summary>
    /// Item for reorder list
    /// </summary>
    public class ReorderItem : System.ComponentModel.INotifyPropertyChanged
    {
        private int _index;
        
        public int Index
        {
            get { return _index; }
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }
        
        public string Id { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public string DisplayText { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
