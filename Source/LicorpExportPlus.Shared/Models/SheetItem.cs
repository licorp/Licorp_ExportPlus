using System;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace LicorpExportPlus.Models
{
    public class SheetItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _sheetNumber;
        private string _sheetName;
        private string _revision;
        private string _size;
        private string _customFileName;
        private ElementId _id;
        private bool _isFullyLoaded;
        private bool _hasNoView;
        private string _warningMessage;

        // ⚡ NEW: Extended parameters (loaded by default)
        private string _drawnBy;
        private string _checkedBy;
        private string _approvedBy;
        private string _issueDate;
        private string _designOption;
        private string _phase;

        // Store reference to sheet for lazy loading
        public ViewSheet RevitSheet { get; set; }

        /// <summary>
        /// Indicates whether full details have been loaded
        /// </summary>
        public bool IsFullyLoaded
        {
            get => _isFullyLoaded;
            set
            {
                if (_isFullyLoaded != value)
                {
                    _isFullyLoaded = value;
                    OnPropertyChanged(nameof(IsFullyLoaded));
                }
            }
        }

        public ElementId Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

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

        public string SheetNumber
        {
            get => _sheetNumber;
            set
            {
                if (_sheetNumber != value)
                {
                    _sheetNumber = value;
                    OnPropertyChanged(nameof(SheetNumber));
                }
            }
        }

        public string SheetName
        {
            get => _sheetName;
            set
            {
                if (_sheetName != value)
                {
                    _sheetName = value;
                    OnPropertyChanged(nameof(SheetName));
                }
            }
        }

        public string Revision
        {
            get => _revision;
            set
            {
                if (_revision != value)
                {
                    _revision = value;
                    OnPropertyChanged(nameof(Revision));
                }
            }
        }

        public string Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    OnPropertyChanged(nameof(Size));
                }
            }
        }

        public string CustomFileName
        {
            get => _customFileName;
            set
            {
                if (_customFileName != value)
                {
                    _customFileName = value;
                    OnPropertyChanged(nameof(CustomFileName));
                }
            }
        }

        // ⚡ NEW: Extended parameters
        public string DrawnBy
        {
            get => _drawnBy;
            set
            {
                if (_drawnBy != value)
                {
                    _drawnBy = value;
                    OnPropertyChanged(nameof(DrawnBy));
                }
            }
        }

        public string CheckedBy
        {
            get => _checkedBy;
            set
            {
                if (_checkedBy != value)
                {
                    _checkedBy = value;
                    OnPropertyChanged(nameof(CheckedBy));
                }
            }
        }

        public string ApprovedBy
        {
            get => _approvedBy;
            set
            {
                if (_approvedBy != value)
                {
                    _approvedBy = value;
                    OnPropertyChanged(nameof(ApprovedBy));
                }
            }
        }

        public string IssueDate
        {
            get => _issueDate;
            set
            {
                if (_issueDate != value)
                {
                    _issueDate = value;
                    OnPropertyChanged(nameof(IssueDate));
                }
            }
        }

        public string DesignOption
        {
            get => _designOption;
            set
            {
                if (_designOption != value)
                {
                    _designOption = value;
                    OnPropertyChanged(nameof(DesignOption));
                }
            }
        }

        public string Phase
        {
            get => _phase;
            set
            {
                if (_phase != value)
                {
                    _phase = value;
                    OnPropertyChanged(nameof(Phase));
                }
            }
        }

        /// <summary>
        /// Indicates if sheet has no view placed on it
        /// </summary>
        public bool HasNoView
        {
            get => _hasNoView;
            set
            {
                if (_hasNoView != value)
                {
                    _hasNoView = value;
                    OnPropertyChanged(nameof(HasNoView));
                }
            }
        }

        /// <summary>
        /// Warning message for sheets with no view
        /// </summary>
        public string WarningMessage
        {
            get => _warningMessage;
            set
            {
                if (_warningMessage != value)
                {
                    _warningMessage = value;
                    OnPropertyChanged(nameof(WarningMessage));
                }
            }
        }

        // Legacy properties for backward compatibility
        public string Number
        {
            get => SheetNumber;
            set => SheetNumber = value;
        }

        public string Name
        {
            get => SheetName;
            set => SheetName = value;
        }

        public string CustomDrawingNumber
        {
            get => CustomFileName;
            set => CustomFileName = value;
        }

        // Alias for Size property (for DataGrid binding compatibility)
        public string PaperSize
        {
            get => Size;
            set => Size = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Load full details when sheet is selected (currently sheets already load all info, but keeping for consistency)
        /// </summary>
        public void LoadFullDetails()
        {
            if (IsFullyLoaded || RevitSheet == null) return;

            try
            {
                // Currently sheets load all basic info at startup
                // This method is here for future expansion if needed
                // For now, just mark as fully loaded
                IsFullyLoaded = true;
            }
            catch (Exception)
            {
                // If loading fails, ignore
            }
        }
    }
}
