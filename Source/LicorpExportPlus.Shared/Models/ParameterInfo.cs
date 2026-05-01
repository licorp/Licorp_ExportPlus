using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LicorpExportPlus.Models
{
    /// <summary>
    /// Represents a Revit parameter available for custom file naming
    /// </summary>
    public class ParameterInfo : INotifyPropertyChanged
    {
        private string _name;
        private string _type;
        private string _category;
        private bool _isBuiltIn;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public bool IsBuiltIn
        {
            get => _isBuiltIn;
            set { _isBuiltIn = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a selected parameter with its configuration (prefix, suffix, separator)
    /// Used in the custom file name template
    /// </summary>
    public class SelectedParameterInfo : INotifyPropertyChanged
    {
        private string _parameterName;
        private string _prefix;
        private string _suffix;
        private string _separator;
        private string _sampleValue;

        public string ParameterName
        {
            get => _parameterName;
            set { _parameterName = value; OnPropertyChanged(); }
        }

        public string Prefix
        {
            get => _prefix;
            set { _prefix = value; OnPropertyChanged(); UpdatePreview(); }
        }

        public string Suffix
        {
            get => _suffix;
            set { _suffix = value; OnPropertyChanged(); UpdatePreview(); }
        }

        public string Separator
        {
            get => _separator;
            set { _separator = value; OnPropertyChanged(); UpdatePreview(); }
        }

        public string SampleValue
        {
            get => _sampleValue;
            set { _sampleValue = value; OnPropertyChanged(); UpdatePreview(); }
        }

        // Event to notify when preview needs updating
        public event Action PreviewChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdatePreview()
        {
            PreviewChanged?.Invoke();
        }
    }
}
