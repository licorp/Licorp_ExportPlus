using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.ComponentModel;

namespace LicorpExportPlus.Models
{
    /// <summary>
    /// Model for View/Sheet Set information
    /// </summary>
    public class ViewSheetSetInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; }
        public List<ElementId> ViewIds { get; set; }
        public List<ElementId> SheetIds { get; set; }
        public bool IsBuiltIn { get; set; }
        public int TotalCount => (ViewIds?.Count ?? 0) + (SheetIds?.Count ?? 0);

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public ViewSheetSetInfo()
        {
            Name = string.Empty;
            ViewIds = new List<ElementId>();
            SheetIds = new List<ElementId>();
            IsBuiltIn = false;
            IsSelected = false;
        }

        public ViewSheetSetInfo(string name)
        {
            Name = name;
            ViewIds = new List<ElementId>();
            SheetIds = new List<ElementId>();
            IsBuiltIn = false;
            IsSelected = false;
        }

        /// <summary>
        /// Display name for ComboBox (includes count)
        /// </summary>
        public string DisplayName => $"{Name} ({TotalCount})";

        public override string ToString()
        {
            return DisplayName;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
