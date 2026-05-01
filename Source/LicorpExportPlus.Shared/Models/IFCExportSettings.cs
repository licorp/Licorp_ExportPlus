using System.ComponentModel;

namespace LicorpExportPlus.Models
{
    /// <summary>
    /// Complete IFC Export Settings matching Revit's native IFC Export dialog
    /// </summary>
    public class IFCExportSettings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ========== Setup ==========
        private string _currentSetup = "<In-Session Setup>";
        public string CurrentSetup
        {
            get => _currentSetup;
            set { _currentSetup = value; OnPropertyChanged(nameof(CurrentSetup)); }
        }

        // ========== General ==========
        private string _ifcVersion = "IFC 2x3 Coordination View 2.0";
        public string IFCVersion
        {
            get => _ifcVersion;
            set { _ifcVersion = value; OnPropertyChanged(nameof(IFCVersion)); }
        }

        private string _fileType = "IFC";
        public string FileType
        {
            get => _fileType;
            set { _fileType = value; OnPropertyChanged(nameof(FileType)); }
        }

        private string _phaseToExport = "Default phase to export";
        public string PhaseToExport
        {
            get => _phaseToExport;
            set { _phaseToExport = value; OnPropertyChanged(nameof(PhaseToExport)); }
        }

        private string _spaceBoundaries = "None";
        public string SpaceBoundaries
        {
            get => _spaceBoundaries;
            set { _spaceBoundaries = value; OnPropertyChanged(nameof(SpaceBoundaries)); }
        }

        private string _projectOrigin = "Current Shared Coordinates";
        public string ProjectOrigin
        {
            get => _projectOrigin;
            set { _projectOrigin = value; OnPropertyChanged(nameof(ProjectOrigin)); }
        }

        private bool _splitWallsByLevel = false;
        public bool SplitWallsByLevel
        {
            get => _splitWallsByLevel;
            set { _splitWallsByLevel = value; OnPropertyChanged(nameof(SplitWallsByLevel)); }
        }

        private bool _includeSteelElements = true;
        public bool IncludeSteelElements
        {
            get => _includeSteelElements;
            set { _includeSteelElements = value; OnPropertyChanged(nameof(IncludeSteelElements)); }
        }

        // ========== Level of Details ==========
        private string _detailLevel = "Medium";
        public string DetailLevel
        {
            get => _detailLevel;
            set { _detailLevel = value; OnPropertyChanged(nameof(DetailLevel)); }
        }

        // ========== Property Sets ==========
        private bool _exportRevitPropertySets = false;
        public bool ExportRevitPropertySets
        {
            get => _exportRevitPropertySets;
            set { _exportRevitPropertySets = value; OnPropertyChanged(nameof(ExportRevitPropertySets)); }
        }

        private bool _exportIFCCommonPropertySets = true;
        public bool ExportIFCCommonPropertySets
        {
            get => _exportIFCCommonPropertySets;
            set { _exportIFCCommonPropertySets = value; OnPropertyChanged(nameof(ExportIFCCommonPropertySets)); }
        }

        private bool _exportBaseQuantities = false;
        public bool ExportBaseQuantities
        {
            get => _exportBaseQuantities;
            set { _exportBaseQuantities = value; OnPropertyChanged(nameof(ExportBaseQuantities)); }
        }

        private bool _exportSchedulesAsPropertySets = false;
        public bool ExportSchedulesAsPropertySets
        {
            get => _exportSchedulesAsPropertySets;
            set { _exportSchedulesAsPropertySets = value; OnPropertyChanged(nameof(ExportSchedulesAsPropertySets)); }
        }

        private bool _exportOnlySchedulesContainingIFC = false;
        public bool ExportOnlySchedulesContainingIFC
        {
            get => _exportOnlySchedulesContainingIFC;
            set { _exportOnlySchedulesContainingIFC = value; OnPropertyChanged(nameof(ExportOnlySchedulesContainingIFC)); }
        }

        private bool _exportUserDefinedPropertySets = false;
        public bool ExportUserDefinedPropertySets
        {
            get => _exportUserDefinedPropertySets;
            set { _exportUserDefinedPropertySets = value; OnPropertyChanged(nameof(ExportUserDefinedPropertySets)); }
        }

        private string _userDefinedPropertySetsPath = "";
        public string UserDefinedPropertySetsPath
        {
            get => _userDefinedPropertySetsPath;
            set { _userDefinedPropertySetsPath = value; OnPropertyChanged(nameof(UserDefinedPropertySetsPath)); }
        }

        private bool _exportParameterMappingTable = false;
        public bool ExportParameterMappingTable
        {
            get => _exportParameterMappingTable;
            set { _exportParameterMappingTable = value; OnPropertyChanged(nameof(ExportParameterMappingTable)); }
        }

        private string _parameterMappingTablePath = "";
        public string ParameterMappingTablePath
        {
            get => _parameterMappingTablePath;
            set { _parameterMappingTablePath = value; OnPropertyChanged(nameof(ParameterMappingTablePath)); }
        }

        // ========== Additional Contents ==========
        private bool _export2DPlanViewElements = false;
        public bool Export2DPlanViewElements
        {
            get => _export2DPlanViewElements;
            set { _export2DPlanViewElements = value; OnPropertyChanged(nameof(Export2DPlanViewElements)); }
        }

        private bool _exportLinkedFilesAsSeparateIFCs = false;
        public bool ExportLinkedFilesAsSeparateIFCs
        {
            get => _exportLinkedFilesAsSeparateIFCs;
            set { _exportLinkedFilesAsSeparateIFCs = value; OnPropertyChanged(nameof(ExportLinkedFilesAsSeparateIFCs)); }
        }

        private bool _exportOnlyElementsVisibleInView = true;
        public bool ExportOnlyElementsVisibleInView
        {
            get => _exportOnlyElementsVisibleInView;
            set { _exportOnlyElementsVisibleInView = value; OnPropertyChanged(nameof(ExportOnlyElementsVisibleInView)); }
        }

        private bool _exportRoomsIn3DViews = false;
        public bool ExportRoomsIn3DViews
        {
            get => _exportRoomsIn3DViews;
            set { _exportRoomsIn3DViews = value; OnPropertyChanged(nameof(ExportRoomsIn3DViews)); }
        }

        // ========== Advanced Options ==========
        private bool _exportPartsAsBuildingElements = false;
        public bool ExportPartsAsBuildingElements
        {
            get => _exportPartsAsBuildingElements;
            set { _exportPartsAsBuildingElements = value; OnPropertyChanged(nameof(ExportPartsAsBuildingElements)); }
        }

        private bool _allowMixedSolidModel = false;
        public bool AllowMixedSolidModel
        {
            get => _allowMixedSolidModel;
            set { _allowMixedSolidModel = value; OnPropertyChanged(nameof(AllowMixedSolidModel)); }
        }

        private bool _useActiveViewWhenCreatingGeometry = false;
        public bool UseActiveViewWhenCreatingGeometry
        {
            get => _useActiveViewWhenCreatingGeometry;
            set { _useActiveViewWhenCreatingGeometry = value; OnPropertyChanged(nameof(UseActiveViewWhenCreatingGeometry)); }
        }

        private bool _useFamilyAndTypeNameForReference = true;
        public bool UseFamilyAndTypeNameForReference
        {
            get => _useFamilyAndTypeNameForReference;
            set { _useFamilyAndTypeNameForReference = value; OnPropertyChanged(nameof(UseFamilyAndTypeNameForReference)); }
        }

        private bool _use2DRoomBoundariesForRoomVolume = false;
        public bool Use2DRoomBoundariesForRoomVolume
        {
            get => _use2DRoomBoundariesForRoomVolume;
            set { _use2DRoomBoundariesForRoomVolume = value; OnPropertyChanged(nameof(Use2DRoomBoundariesForRoomVolume)); }
        }

        private bool _includeIFCSiteElevation = false;
        public bool IncludeIFCSiteElevation
        {
            get => _includeIFCSiteElevation;
            set { _includeIFCSiteElevation = value; OnPropertyChanged(nameof(IncludeIFCSiteElevation)); }
        }

        private bool _storeIFCGUIDInElement = false;
        public bool StoreIFCGUIDInElement
        {
            get => _storeIFCGUIDInElement;
            set { _storeIFCGUIDInElement = value; OnPropertyChanged(nameof(StoreIFCGUIDInElement)); }
        }

        private bool _exportBoundingBox = false;
        public bool ExportBoundingBox
        {
            get => _exportBoundingBox;
            set { _exportBoundingBox = value; OnPropertyChanged(nameof(ExportBoundingBox)); }
        }

        private bool _keepTessellatedGeometry = false;
        public bool KeepTessellatedGeometry
        {
            get => _keepTessellatedGeometry;
            set { _keepTessellatedGeometry = value; OnPropertyChanged(nameof(KeepTessellatedGeometry)); }
        }

        private bool _useTypeNameOnlyForIFCType = false;
        public bool UseTypeNameOnlyForIFCType
        {
            get => _useTypeNameOnlyForIFCType;
            set { _useTypeNameOnlyForIFCType = value; OnPropertyChanged(nameof(UseTypeNameOnlyForIFCType)); }
        }

        private bool _useVisibleRevitNameAsIFCEntity = false;
        public bool UseVisibleRevitNameAsIFCEntity
        {
            get => _useVisibleRevitNameAsIFCEntity;
            set { _useVisibleRevitNameAsIFCEntity = value; OnPropertyChanged(nameof(UseVisibleRevitNameAsIFCEntity)); }
        }

        // ========== Output ==========
        private string _outputFolder = "";
        public string OutputFolder
        {
            get => _outputFolder;
            set { _outputFolder = value; OnPropertyChanged(nameof(OutputFolder)); }
        }

        // ========== Legacy properties for backward compatibility ==========
        [System.Obsolete("Use ExportBaseQuantities instead")]
        public bool ExportQuantities
        {
            get => ExportBaseQuantities;
            set => ExportBaseQuantities = value;
        }

        [System.Obsolete("Use SplitWallsByLevel instead")]
        public bool SplitWallsColumns
        {
            get => SplitWallsByLevel;
            set => SplitWallsByLevel = value;
        }

        [System.Obsolete("Use Export2DPlanViewElements instead")]
        public bool Export2DElements
        {
            get => Export2DPlanViewElements;
            set => Export2DPlanViewElements = value;
        }

        [System.Obsolete("Use ExportRevitPropertySets instead")]
        public bool ExportRevitProperties
        {
            get => ExportRevitPropertySets;
            set => ExportRevitPropertySets = value;
        }

        [System.Obsolete("Use ExportIFCCommonPropertySets instead")]
        public bool ExportIFCProperties
        {
            get => ExportIFCCommonPropertySets;
            set => ExportIFCCommonPropertySets = value;
        }

        [System.Obsolete("Use ExportOnlyElementsVisibleInView instead")]
        public bool ExportByView
        {
            get => ExportOnlyElementsVisibleInView;
            set => ExportOnlyElementsVisibleInView = value;
        }

        [System.Obsolete("Use ExportLinkedFilesAsSeparateIFCs instead")]
        public bool ExportLinkedFiles
        {
            get => ExportLinkedFilesAsSeparateIFCs;
            set => ExportLinkedFilesAsSeparateIFCs = value;
        }

        // ========== User Defined Property Sets (CRITICAL FOR CUSTOM IFC PROPERTIES) ==========
        private bool _exportUserDefinedPsets = false;
        /// <summary>
        /// Enable export of user-defined property sets from external file
        /// </summary>
        public bool ExportUserDefinedPsets
        {
            get => _exportUserDefinedPsets;
            set { _exportUserDefinedPsets = value; OnPropertyChanged(nameof(ExportUserDefinedPsets)); }
        }

        private string _exportUserDefinedPsetsFileName = string.Empty;
        /// <summary>
        /// Path to .txt file containing user-defined property sets mappings
        /// Example: D:\\OneDrive\\Desktop\\CRRTSD-IFCPropertySets - Template.txt
        /// </summary>
        public string ExportUserDefinedPsetsFileName
        {
            get => _exportUserDefinedPsetsFileName;
            set { _exportUserDefinedPsetsFileName = value; OnPropertyChanged(nameof(ExportUserDefinedPsetsFileName)); }
        }

        // ========== Parameter Mapping (APS FEATURE) ==========
        private bool _exportParameterMapping = false;
        /// <summary>
        /// Enable parameter mapping from external file (maps Revit parameters to IFC properties)
        /// </summary>
        public bool ExportParameterMapping
        {
            get => _exportParameterMapping;
            set { _exportParameterMapping = value; OnPropertyChanged(nameof(ExportParameterMapping)); }
        }

        private string _exportParameterMappingFileName = string.Empty;
        /// <summary>
        /// Path to parameter mapping file
        /// </summary>
        public string ExportParameterMappingFileName
        {
            get => _exportParameterMappingFileName;
            set { _exportParameterMappingFileName = value; OnPropertyChanged(nameof(ExportParameterMappingFileName)); }
        }

        // ========== Advanced Export Options (APS FEATURES) ==========
        private bool _visibleElementsOfCurrentView = false;
        /// <summary>
        /// Export only elements visible in current/active view
        /// </summary>
        public bool VisibleElementsOfCurrentView
        {
            get => _visibleElementsOfCurrentView;
            set { _visibleElementsOfCurrentView = value; OnPropertyChanged(nameof(VisibleElementsOfCurrentView)); }
        }

        private bool _useActiveViewGeometry = false;
        /// <summary>
        /// Use geometry from active view (affects element representation)
        /// </summary>
        public bool UseActiveViewGeometry
        {
            get => _useActiveViewGeometry;
            set { _useActiveViewGeometry = value; OnPropertyChanged(nameof(UseActiveViewGeometry)); }
        }

        private bool _storeIFCGUID = false;
        /// <summary>
        /// Store IFC GUID in Revit element (saves GUID back to model)
        /// Important: This commits changes to the Revit model!
        /// </summary>
        public bool StoreIFCGUID
        {
            get => _storeIFCGUID;
            set { _storeIFCGUID = value; OnPropertyChanged(nameof(StoreIFCGUID)); }
        }

        private string _activeViewId = string.Empty;
        /// <summary>
        /// ElementId of specific view to use for export (leave empty to use current active view)
        /// </summary>
        public string ActiveViewId
        {
            get => _activeViewId;
            set { _activeViewId = value; OnPropertyChanged(nameof(ActiveViewId)); }
        }

        public IFCExportSettings()
        {
        }
    }

    // Legacy enum for backward compatibility
    public enum IFCVersion
    {
        IFC2x2,
        IFC2x3,
        IFC4,
        IFC4RV
    }
}
