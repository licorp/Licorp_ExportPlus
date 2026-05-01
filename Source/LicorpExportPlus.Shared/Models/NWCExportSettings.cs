using System.ComponentModel;

namespace LicorpExportPlus.Models
{
    /// <summary>
    /// Settings for Navisworks NWC export - matches Revit's Navisworks Options Editor
    /// </summary>
    public class NWCExportSettings : INotifyPropertyChanged
    {
        // File Readers > Revit section options (in order as shown in Revit)
        private bool _convertConstructionParts = false;
        private bool _convertElementIds = true;
        private string _convertElementParameters = "All"; // None, Elements, All
        private bool _convertElementProperties = false;
        private bool _convertLights = false;
        private bool _convertLinkedCADFormats = true;
        private bool _convertLinkedFiles = false;
        private bool _convertRoomAsAttribute = true;
        private bool _convertURLs = true;
        private string _coordinates = "Shared"; // Shared, Project Internal
        private bool _divideFileIntoLevels = true;
        private bool _embedTextures = true;
        private string _exportScope = "Current view"; // Current view, Model
        private bool _exportRoomGeometry = true;
        private double _facetingFactor = 1.0;
        private bool _separateCustomProperties = true;
        private bool _strictSectioning = false;
        private bool _tryAndFindMissingMaterials = true;
        private bool _typePropertiesOnElements = false;

        public bool ConvertConstructionParts
        {
            get => _convertConstructionParts;
            set { _convertConstructionParts = value; OnPropertyChanged(nameof(ConvertConstructionParts)); }
        }

        public bool ConvertElementIds
        {
            get => _convertElementIds;
            set { _convertElementIds = value; OnPropertyChanged(nameof(ConvertElementIds)); }
        }

        public string ConvertElementParameters
        {
            get => _convertElementParameters;
            set { _convertElementParameters = value; OnPropertyChanged(nameof(ConvertElementParameters)); }
        }

        public bool ConvertElementProperties
        {
            get => _convertElementProperties;
            set { _convertElementProperties = value; OnPropertyChanged(nameof(ConvertElementProperties)); }
        }

        public bool ConvertLights
        {
            get => _convertLights;
            set { _convertLights = value; OnPropertyChanged(nameof(ConvertLights)); }
        }

        public bool ConvertLinkedCADFormats
        {
            get => _convertLinkedCADFormats;
            set { _convertLinkedCADFormats = value; OnPropertyChanged(nameof(ConvertLinkedCADFormats)); }
        }

        public bool ConvertLinkedFiles
        {
            get => _convertLinkedFiles;
            set { _convertLinkedFiles = value; OnPropertyChanged(nameof(ConvertLinkedFiles)); }
        }

        public bool ConvertRoomAsAttribute
        {
            get => _convertRoomAsAttribute;
            set { _convertRoomAsAttribute = value; OnPropertyChanged(nameof(ConvertRoomAsAttribute)); }
        }

        public bool ConvertURLs
        {
            get => _convertURLs;
            set { _convertURLs = value; OnPropertyChanged(nameof(ConvertURLs)); }
        }

        public string Coordinates
        {
            get => _coordinates;
            set { _coordinates = value; OnPropertyChanged(nameof(Coordinates)); }
        }

        public bool DivideFileIntoLevels
        {
            get => _divideFileIntoLevels;
            set { _divideFileIntoLevels = value; OnPropertyChanged(nameof(DivideFileIntoLevels)); }
        }

        public bool EmbedTextures
        {
            get => _embedTextures;
            set { _embedTextures = value; OnPropertyChanged(nameof(EmbedTextures)); }
        }

        public string ExportScope
        {
            get => _exportScope;
            set { _exportScope = value; OnPropertyChanged(nameof(ExportScope)); }
        }

        public bool ExportRoomGeometry
        {
            get => _exportRoomGeometry;
            set { _exportRoomGeometry = value; OnPropertyChanged(nameof(ExportRoomGeometry)); }
        }

        public double FacetingFactor
        {
            get => _facetingFactor;
            set { _facetingFactor = value; OnPropertyChanged(nameof(FacetingFactor)); }
        }

        public bool SeparateCustomProperties
        {
            get => _separateCustomProperties;
            set { _separateCustomProperties = value; OnPropertyChanged(nameof(SeparateCustomProperties)); }
        }

        public bool StrictSectioning
        {
            get => _strictSectioning;
            set { _strictSectioning = value; OnPropertyChanged(nameof(StrictSectioning)); }
        }

        public bool TryAndFindMissingMaterials
        {
            get => _tryAndFindMissingMaterials;
            set { _tryAndFindMissingMaterials = value; OnPropertyChanged(nameof(TryAndFindMissingMaterials)); }
        }

        public bool TypePropertiesOnElements
        {
            get => _typePropertiesOnElements;
            set { _typePropertiesOnElements = value; OnPropertyChanged(nameof(TypePropertiesOnElements)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Create a deep copy of settings
        /// </summary>
        public NWCExportSettings Clone()
        {
            return new NWCExportSettings
            {
                ConvertConstructionParts = this.ConvertConstructionParts,
                ConvertElementIds = this.ConvertElementIds,
                ConvertElementParameters = this.ConvertElementParameters,
                ConvertElementProperties = this.ConvertElementProperties,
                ConvertLights = this.ConvertLights,
                ConvertLinkedCADFormats = this.ConvertLinkedCADFormats,
                ConvertLinkedFiles = this.ConvertLinkedFiles,
                ConvertRoomAsAttribute = this.ConvertRoomAsAttribute,
                ConvertURLs = this.ConvertURLs,
                Coordinates = this.Coordinates,
                DivideFileIntoLevels = this.DivideFileIntoLevels,
                EmbedTextures = this.EmbedTextures,
                ExportScope = this.ExportScope,
                ExportRoomGeometry = this.ExportRoomGeometry,
                FacetingFactor = this.FacetingFactor,
                SeparateCustomProperties = this.SeparateCustomProperties,
                StrictSectioning = this.StrictSectioning,
                TryAndFindMissingMaterials = this.TryAndFindMissingMaterials,
                TypePropertiesOnElements = this.TypePropertiesOnElements
            };
        }
    }
}
