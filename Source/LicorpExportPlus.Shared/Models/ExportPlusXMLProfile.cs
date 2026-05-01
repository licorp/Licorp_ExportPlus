using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.ComponentModel;

namespace LicorpExportPlus.Models
{
    [XmlRoot("Profiles")]
    public class ExportPlusProfileList
    {
        [XmlArray("List")]
        [XmlArrayItem("Profile")]
        public List<ExportPlusXMLProfile> Profiles { get; set; } = new List<ExportPlusXMLProfile>();
    }

    public class ExportPlusXMLProfile
    {
        public string Name { get; set; }
        public bool IsCurrent { get; set; }
        public string FilePath { get; set; }
        public string Version { get; set; }

        [XmlElement("TemplateInfo")]
        public TemplateInfo TemplateInfo { get; set; } = new TemplateInfo();
    }

    public class TemplateInfo
    {
        // Format Settings
        [XmlElement("DWF")]
        public DWFSettings DWF { get; set; } = new DWFSettings();

        [XmlElement("NWC")]
        public NWCSettings NWC { get; set; } = new NWCSettings();

        [XmlElement("IFC")]
        public IFCSettings IFC { get; set; } = new IFCSettings();

        [XmlElement("IMG")]
        public IMGSettings IMG { get; set; } = new IMGSettings();

        // Export Format Flags
        public bool IsPDFChecked { get; set; }
        public bool IsDWGChecked { get; set; }
        public bool IsDGNChecked { get; set; }
        public bool IsIFCChecked { get; set; }
        public bool IsIMGChecked { get; set; }
        public bool IsNWCChecked { get; set; }
        public bool IsDWFChecked { get; set; }

        // Selection Settings
        [XmlElement("SelectionSheets")]
        public SelectionSettings SelectionSheets { get; set; } = new SelectionSettings();

        [XmlElement("SelectionViews")]
        public SelectionSettings SelectionViews { get; set; } = new SelectionSettings();

        [XmlElement("Selection")]
        public SelectionSettings Selection { get; set; } = new SelectionSettings();

        // Custom File Name Parameters
        [XmlElement("SelectSheetParameters")]
        public SelectSheetParameters SelectSheetParameters { get; set; } = new SelectSheetParameters();

        [XmlElement("SelectViewParameters")]
        public SelectViewParameters SelectViewParameters { get; set; } = new SelectViewParameters();

        // Common Export Settings (from root level of TemplateInfo)
        public bool Create_SplitFolder { get; set; }
        public bool MaskCoincidentLines { get; set; } = true;
        public bool DWG_MergedViews { get; set; } = true;

        [XmlArray("Formats")]
        [XmlArrayItem("Format")]
        public List<string> Formats { get; set; } = new List<string>();

        public bool JumpToSection { get; set; }

        // Paper Placement Settings
        public bool IsCenter { get; set; } = true;
        public string SelectedMarginType { get; set; } = "No Margin";
        public bool IsFitToPage { get; set; }
        public bool IsPortrait { get; set; }
        public bool IsVectorProcessing { get; set; } = true;
        public string RasterQuality { get; set; } = "High";
        public string Color { get; set; } = "Color";

        // View Options
        public bool ViewLink { get; set; }
        public bool HidePlanes { get; set; } = true;
        public bool HideScopeBox { get; set; } = true;
        public bool HideUnreferencedTags { get; set; } = true;
        public bool HideCropBoundaries { get; set; } = true;
        public bool ReplaceHalftone { get; set; }

        // File Settings
        public bool IsSeparateFile { get; set; } = true;
        public bool IsFileNameSet { get; set; }
        public string FilePath { get; set; }

        // DWG/DGN Settings
        public string DWGSettingName { get; set; }
        public string DGNSettingName { get; set; }

        public string PaperSize { get; set; } = "Default";
    }

    public class DWFSettings
    {
        public bool IsDwfx { get; set; }
        public string PaperSize { get; set; } = "Default";

        [XmlElement("opt_ImageFormat")]
        public string OptImageFormat { get; set; } = "Lossless";

        [XmlElement("opt_ImageQuality")]
        public string OptImageQuality { get; set; } = "Default";

        [XmlElement("opt_CropBoxVisible")]
        public bool OptCropBoxVisible { get; set; }

        [XmlElement("opt_ExportingAreas")]
        public bool OptExportingAreas { get; set; }

        [XmlElement("opt_ExportTextures")]
        public bool OptExportTextures { get; set; } = true;

        [XmlElement("opt_ExpportObjectData")]
        public bool OptExpportObjectData { get; set; } = true;

        public bool IsCenter { get; set; }
        public string SelectedMarginType { get; set; } = "No Margin";
        public bool IsFitToPage { get; set; } = true;
        public bool IsPortrait { get; set; }
        public bool IsVectorProcessing { get; set; }
        public string RasterQuality { get; set; } = "Low";
        public string Color { get; set; } = "Color";
        public bool ViewLink { get; set; }
        public bool HidePlanes { get; set; } = true;
        public bool HideScopeBox { get; set; } = true;
        public bool HideUnreferencedTags { get; set; } = true;
        public bool HideCropBoundaries { get; set; } = true;
        public bool ReplaceHalftone { get; set; }
        public bool IsSeparateFile { get; set; } = true;
        public string FilePath { get; set; }
    }

    public class NWCSettings
    {
        // Standard options
        public bool ConvertConstructionParts { get; set; } = false;
        public bool ConvertElementIds { get; set; } = true;
        public string ConvertElementParameters { get; set; } = "All"; // None, Elements, All
        public bool ConvertElementProperties { get; set; } = false;
        public bool ConvertLinkedFiles { get; set; } = false;
        public bool ConvertRoomAsAttribute { get; set; } = true;
        public bool ConvertURLs { get; set; } = true;
        public string Coordinates { get; set; } = "Shared"; // Shared, Project Internal
        public bool DivideFileIntoLevels { get; set; } = true;
        public bool EmbedTextures { get; set; } = true;
        public string ExportScope { get; set; } = "Current view"; // Current view, Model
        public bool ExportRoomGeometry { get; set; } = true;
        public bool SeparateCustomProperties { get; set; } = true;
        public bool StrictSectioning { get; set; } = false;
        public bool TryAndFindMissingMaterials { get; set; } = true;
        public bool TypePropertiesOnElements { get; set; } = false;

        // Revit 2020+ options
        public bool ConvertLinkedCADFormats { get; set; } = true;
        public bool ConvertLights { get; set; } = false;
        public double FacetingFactor { get; set; } = 1.0;

        // Legacy property names for backward compatibility
        [XmlElement("ExportElementIds")]
        public bool ExportElementIds
        {
            get => ConvertElementIds;
            set => ConvertElementIds = value;
        }

        [XmlElement("ExportLinks")]
        public bool ExportLinks
        {
            get => ConvertLinkedFiles;
            set => ConvertLinkedFiles = value;
        }

        [XmlElement("ExportParts")]
        public bool ExportParts
        {
            get => ConvertConstructionParts;
            set => ConvertConstructionParts = value;
        }

        [XmlElement("ExportRoomAsAttribute")]
        public bool ExportRoomAsAttribute
        {
            get => ConvertRoomAsAttribute;
            set => ConvertRoomAsAttribute = value;
        }

        [XmlElement("ExportUrls")]
        public bool ExportUrls
        {
            get => ConvertURLs;
            set => ConvertURLs = value;
        }

        [XmlElement("FindMissingMaterials")]
        public bool FindMissingMaterials
        {
            get => TryAndFindMissingMaterials;
            set => TryAndFindMissingMaterials = value;
        }

        [XmlElement("Parameters")]
        public string Parameters
        {
            get => ConvertElementParameters;
            set => ConvertElementParameters = value;
        }
    }

    public class IFCSettings
    {
        public string FileVersion { get; set; } = "IFC2x3CV2";
        public string IFCFileType { get; set; } = "Ifc";

        [XmlElement("CurrentPhase")]
        public PhaseInfo CurrentPhase { get; set; } = new PhaseInfo();

        public string SpaceBoundaries { get; set; } = "None";
        public string SitePlacement { get; set; } = "Current Shared Coordinates";
        public bool WallAndColumnSplitting { get; set; }
        public bool IncludeSteelElements { get; set; } = true;
        public bool Export2DElements { get; set; }
        public bool ExportLinkedFiles { get; set; }
        public bool ExportRoomsInView { get; set; }
        public bool ExportInternalRevitPropertySets { get; set; }
        public bool ExportIFCCommonPropertySets { get; set; } = true;
        public bool ExportBaseQuantities { get; set; }
        public bool ExportSchedulesAsPsets { get; set; }
        public bool ExportSpecificSchedules { get; set; }
        public bool ExportUserDefinedPsets { get; set; }
        public string ExportUserDefinedPsetsFileName { get; set; }
        public bool ExportUserDefinedParameterMapping { get; set; }
        public string ExportUserDefinedParameterMappingFileName { get; set; }
        public string TessellationLevelOfDetail { get; set; } = "Low";
        public bool ExportPartsAsBuildingElements { get; set; }
        public bool ExportSolidModelRep { get; set; }
        public bool UseFamilyAndTypeNameForReference { get; set; }
        public bool UseActiveViewCreatingGeometry { get; set; }
        public bool Use2DRoomBoundaryForVolume { get; set; }
        public bool IncludeSiteElevation { get; set; }
        public bool StoreIFCGUID { get; set; }
        public bool ExportBoundingBox { get; set; }
        public bool UseOnlyTriangulation { get; set; }
        public bool VisibleElementsOfCurrentView { get; set; } = true;
        public double TessellationFactor { get; set; } = -1;
        public bool UseTypeNameOnlyForIfcType { get; set; }
        public bool UseVisibleRevitNameAsEntityName { get; set; }
    }

    public class PhaseInfo
    {
        [XmlElement("id")]
        public string Id { get; set; } = "-1";

        [XmlElement("Text")]
        public string Text { get; set; } = "Default phase to export";
    }

    public class IMGSettings
    {
        public bool IsCombineHTML { get; set; }
        public string objCombineFilename { get; set; }
        public string FitDirection { get; set; } = "Horizontal";
        public string HLRandWFViewsFileType { get; set; } = "PNG";
        public string ImageResolution { get; set; } = "DPI_72";
        public string PixelSize { get; set; } = "2048";
        public string ShadowViewsFileType { get; set; } = "PNG";
        public string Zoom { get; set; } = "50";
        public string ZoomType { get; set; } = "FitToPage";
    }

    public class SelectionSettings
    {
        public string SelectionType { get; set; } = "Sheet";
        public bool IsLableCheked { get; set; }
        public bool IsFieldSeparatorChecked { get; set; } = true;
        public string FieldSeparator { get; set; } = "-";

        [XmlArray("ViewIds")]
        [XmlArrayItem("ViewId")]
        public List<string> ViewIds { get; set; } = new List<string>();

        [XmlArray("SelectedParams")]
        [XmlArrayItem("Param")]
        public List<string> SelectedParams { get; set; } = new List<string>();

        [XmlElement("SelectedParams_Virtual")]
        public SelectedParamsVirtual SelectedParamsVirtual { get; set; } = new SelectedParamsVirtual();
    }

    public class SelectedParamsVirtual
    {
        [XmlElement("SelectionParameter")]
        public List<SelectionParameter> SelectionParameters { get; set; } = new List<SelectionParameter>();
    }

    public class SelectionParameter
    {
        [XmlAttribute("xml:space")]
        public string DisplayName { get; set; }

        public string GUID { get; set; }
        public string BuiltinType { get; set; } = "INVALID";
        public string Type { get; set; } = "Revit";
        public int AutoNumberOffset { get; set; }
        public bool IsSelected { get; set; }
    }

    public class CustomFileNameParameters
    {
        [XmlElement("CombineParameters")]
        public CombineParameters CombineParameters { get; set; } = new CombineParameters();
    }

    public class CombineParameters
    {
        [XmlElement("ParameterModel")]
        public List<ParameterModel> ParameterModels { get; set; } = new List<ParameterModel>();
    }

    public class SelectSheetParameters
    {
        public string CombineParameterName { get; set; }

        [XmlArray("CombineParameters")]
        [XmlArrayItem("ParameterModel")]
        public List<ParameterModel> CombineParameters { get; set; } = new List<ParameterModel>();
    }

    public class SelectViewParameters
    {
        public string CombineParameterName { get; set; }

        [XmlArray("CombineParameters")]
        [XmlArrayItem("ParameterModel")]
        public List<ParameterModel> CombineParameters { get; set; } = new List<ParameterModel>();
    }

    public class ParameterModel
    {
        [XmlAttribute("xml:space_x003D_preserve")]
        public string XmlSpaceAttribute { get; set; }

        public string ParameterName { get; set; }
        public string StorageType { get; set; }
        public string ParameterId { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public bool IsProjectParameter { get; set; }
        public bool IsCustomParameter { get; set; }
    }

    public class SheetFileNameInfo : INotifyPropertyChanged
    {
        public string SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public string Revision { get; set; }
        public string Size { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        private string _customFileName;
        public string CustomFileName
        {
            get => _customFileName;
            set
            {
                _customFileName = value;
                OnPropertyChanged(nameof(CustomFileName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
