using System.ComponentModel;

namespace LicorpExportPlus.Models
{
    /// <summary>
    /// Model for items in the Export Queue (Create tab)
    /// </summary>
    public class ExportQueueItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _viewSheetNumber;
        private string _viewSheetName;
        private string _format;
        private string _size;
        private string _orientation;
        private double _progress;
        private string _status;
        private string _outputPath;
        private string _errorMessage;
        private string _completedAt;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public string ViewSheetNumber
        {
            get => _viewSheetNumber;
            set
            {
                _viewSheetNumber = value;
                OnPropertyChanged(nameof(ViewSheetNumber));
            }
        }

        public string ViewSheetName
        {
            get => _viewSheetName;
            set
            {
                _viewSheetName = value;
                OnPropertyChanged(nameof(ViewSheetName));
            }
        }

        public string Format
        {
            get => _format;
            set
            {
                _format = value;
                OnPropertyChanged(nameof(Format));
            }
        }

        public string Size
        {
            get => _size;
            set
            {
                _size = value;
                OnPropertyChanged(nameof(Size));
            }
        }

        public string Orientation
        {
            get => _orientation;
            set
            {
                _orientation = value;
                OnPropertyChanged(nameof(Orientation));
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                _outputPath = value;
                OnPropertyChanged(nameof(OutputPath));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public string CompletedAt
        {
            get => _completedAt;
            set
            {
                _completedAt = value;
                OnPropertyChanged(nameof(CompletedAt));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
