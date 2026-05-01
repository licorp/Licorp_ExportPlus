using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace LicorpExportPlus.Models
{
    /// <summary>
    /// ExportPlus Profile compatible model for loading existing profiles
    /// </summary>
    public class ExportPlusProfile : INotifyPropertyChanged
    {
        private string _profileName;
        private string _outputFolder;
        private List<string> _selectedFormats;
        private bool _createSeparateFolders;
        private string _paperSize;
        private string _orientation;
        private bool _placeCenterDrawing;
        private bool _zoomTo100;
        private bool _hideCropRegions;
        private bool _hideScopeboxes;

        [JsonProperty("ProfileName")]
        public string ProfileName
        {
            get => _profileName;
            set
            {
                if (_profileName != value)
                {
                    _profileName = value;
                    OnPropertyChanged(nameof(ProfileName));
                }
            }
        }

        [JsonProperty("OutputFolder")]
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (_outputFolder != value)
                {
                    _outputFolder = value;
                    OnPropertyChanged(nameof(OutputFolder));
                }
            }
        }

        [JsonProperty("SelectedFormats")]
        public List<string> SelectedFormats
        {
            get => _selectedFormats ?? new List<string>();
            set
            {
                _selectedFormats = value;
                OnPropertyChanged(nameof(SelectedFormats));
            }
        }

        [JsonProperty("CreateSeparateFolders")]
        public bool CreateSeparateFolders
        {
            get => _createSeparateFolders;
            set
            {
                if (_createSeparateFolders != value)
                {
                    _createSeparateFolders = value;
                    OnPropertyChanged(nameof(CreateSeparateFolders));
                }
            }
        }

        [JsonProperty("PaperSize")]
        public string PaperSize
        {
            get => _paperSize;
            set
            {
                if (_paperSize != value)
                {
                    _paperSize = value;
                    OnPropertyChanged(nameof(PaperSize));
                }
            }
        }

        [JsonProperty("Orientation")]
        public string Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    OnPropertyChanged(nameof(Orientation));
                }
            }
        }

        [JsonProperty("PlaceCenterDrawing")]
        public bool PlaceCenterDrawing
        {
            get => _placeCenterDrawing;
            set
            {
                if (_placeCenterDrawing != value)
                {
                    _placeCenterDrawing = value;
                    OnPropertyChanged(nameof(PlaceCenterDrawing));
                }
            }
        }

        [JsonProperty("ZoomTo100")]
        public bool ZoomTo100
        {
            get => _zoomTo100;
            set
            {
                if (_zoomTo100 != value)
                {
                    _zoomTo100 = value;
                    OnPropertyChanged(nameof(ZoomTo100));
                }
            }
        }

        [JsonProperty("HideCropRegions")]
        public bool HideCropRegions
        {
            get => _hideCropRegions;
            set
            {
                if (_hideCropRegions != value)
                {
                    _hideCropRegions = value;
                    OnPropertyChanged(nameof(HideCropRegions));
                }
            }
        }

        [JsonProperty("HideScopeboxes")]
        public bool HideScopeboxes
        {
            get => _hideScopeboxes;
            set
            {
                if (_hideScopeboxes != value)
                {
                    _hideScopeboxes = value;
                    OnPropertyChanged(nameof(HideScopeboxes));
                }
            }
        }

        // Additional ExportPlus specific properties
        [JsonProperty("ExportSettings")]
        public Dictionary<string, object> ExportSettings { get; set; } = new Dictionary<string, object>();

        [JsonProperty("SheetFilters")]
        public Dictionary<string, object> SheetFilters { get; set; } = new Dictionary<string, object>();

        // DA4R-DwgExporter features
        private string _viewSheetSetName;
        private bool _exportAllViews;
        private bool _includeRenderingViews;

        [JsonProperty("ViewSheetSetName")]
        public string ViewSheetSetName
        {
            get => _viewSheetSetName;
            set
            {
                if (_viewSheetSetName != value)
                {
                    _viewSheetSetName = value;
                    OnPropertyChanged(nameof(ViewSheetSetName));
                }
            }
        }

        [JsonProperty("ExportAllViews")]
        public bool ExportAllViews
        {
            get => _exportAllViews;
            set
            {
                if (_exportAllViews != value)
                {
                    _exportAllViews = value;
                    OnPropertyChanged(nameof(ExportAllViews));
                }
            }
        }

        [JsonProperty("IncludeRenderingViews")]
        public bool IncludeRenderingViews
        {
            get => _includeRenderingViews;
            set
            {
                if (_includeRenderingViews != value)
                {
                    _includeRenderingViews = value;
                    OnPropertyChanged(nameof(IncludeRenderingViews));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ExportPlusProfile()
        {
            _profileName = "Default";
            _outputFolder = "";
            _selectedFormats = new List<string> { "DWG" }; // Only DWG by default
            _createSeparateFolders = false;
            _paperSize = "Auto";
            _orientation = "Auto";
            _placeCenterDrawing = true;
            _zoomTo100 = false;
            _hideCropRegions = true;
            _hideScopeboxes = true;
        }
    }
}
