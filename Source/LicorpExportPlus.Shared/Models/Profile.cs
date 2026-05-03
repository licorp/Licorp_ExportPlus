using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LicorpExportPlus.Models
{
    /// <summary>
    /// Profile model for saving and loading ExportPlus configurations
    /// </summary>
    public class Profile : INotifyPropertyChanged
    {
        private string _name;
        private DateTime _lastModified;

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public DateTime LastModified
        {
            get => _lastModified;
            set { _lastModified = value; OnPropertyChanged(); }
        }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string Description { get; set; }

        // XML Profile data (for imported profiles)
        public string XmlFilePath { get; set; }

        // Profile Settings
        public ProfileSettings Settings { get; set; } = new ProfileSettings();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// All settings stored in a profile
    /// </summary>
    public class ProfileSettings
    {
        // Selection Settings
        public List<string> SelectedSheetNumbers { get; set; } = new List<string>();
        public string FilterBy { get; set; } = "All Sheets";
        public bool FilterByVSEnabled { get; set; } = false;

        // Format Settings - PDF
        public bool PDFEnabled { get; set; } = false; // Changed default to false
        public string PDFPrinterName { get; set; } = "PDF24";
        public bool PaperPlacementCenter { get; set; } = true;
        public string MarginType { get; set; } = "No Margin";
        public double OffsetX { get; set; } = 0;
        public double OffsetY { get; set; } = 0;
        public bool FitToPage { get; set; } = false;
        public int ZoomPercent { get; set; } = 100;
        public bool VectorProcessing { get; set; } = true;
        public string RasterQuality { get; set; } = "High";
        public string ColorMode { get; set; } = "Color";

        // PDF Advanced Settings (from XML import)
        public bool PDFVectorProcessing { get; set; } = true;
        public string PDFRasterQuality { get; set; } = "High";
        public string PDFColorMode { get; set; } = "Color";
        public bool PDFFitToPage { get; set; } = false;
        public bool PDFIsCenter { get; set; } = true;
        public string PDFMarginType { get; set; } = "No Margin";

        // PDF Options
        public bool ViewLinksInBlue { get; set; } = false;
        public bool HideRefWorkPlanes { get; set; } = true;
        public bool HideUnreferencedViewTags { get; set; } = true;
        public bool HideScopeBoxes { get; set; } = true;
        public bool HideCropBoundaries { get; set; } = true;
        public bool ReplaceHalftone { get; set; } = false;
        public bool RegionEdgesMask { get; set; } = true;
        public bool CreateSeparateFiles { get; set; } = true;
        public bool KeepPaperSizeOrientation { get; set; } = false;
        public bool SkipEmptySheets { get; set; } = false; // Skip sheets without any viewports

        // Format Settings - Other formats
        public bool DWGEnabled { get; set; } = true;
        public bool DGNEnabled { get; set; } = false;
        public bool DWFEnabled { get; set; } = false;
        public bool NWCEnabled { get; set; } = false;
        public bool IFCEnabled { get; set; } = false;
        public bool IMGEnabled { get; set; } = false;
        public bool CompactDwgFiles { get; set; } = true;

        // DWF Settings (from XML import)
        public string DWFImageFormat { get; set; } = "Lossless";
        public string DWFImageQuality { get; set; } = "Default";
        public bool DWFExportTextures { get; set; } = true;

        // NWC Settings (from XML import)
        public bool NWCConvertElementProperties { get; set; } = true;
        public string NWCCoordinates { get; set; } = "Shared";
        public bool NWCDivideFileIntoLevels { get; set; } = true;
        public bool NWCExportElementIds { get; set; } = true;
        public bool NWCExportParts { get; set; } = true;
        public double NWCFacetingFactor { get; set; } = 1.0;

        // IFC Settings (from XML import)
        public string IFCFileVersion { get; set; } = "IFC2x3CV2";
        public string IFCSpaceBoundaries { get; set; } = "None";
        public string IFCSitePlacement { get; set; } = "Current Shared Coordinates";
        public bool IFCExportBaseQuantities { get; set; } = false;
        public bool IFCExportIFCCommonPropertySets { get; set; } = true;
        public string IFCTessellationLevelOfDetail { get; set; } = "Low";

        // IMG Settings (from XML import)
        public string IMGImageResolution { get; set; } = "DPI_72";
        public string IMGFileType { get; set; } = "PNG";
        public string IMGZoomType { get; set; } = "FitToPage";
        public string IMGPixelSize { get; set; } = "2048";

        // Custom File Name Settings
        public List<string> CustomFileNameParameters { get; set; } = new List<string>();
        public string CustomFileNamePreview { get; set; } = "";

        // Custom File Name Configuration (detailed) - Separate for Sheets and Views
        public string CustomFileNameConfigJson { get; set; } = ""; // Deprecated - kept for backward compatibility
        public string CustomFileNameConfigJson_Sheets { get; set; } = ""; // Serialized SelectedParameterInfo list for Sheets
        public string CustomFileNameConfigJson_Views { get; set; } = ""; // Serialized SelectedParameterInfo list for Views

        // Create Settings
        public string OutputFolder { get; set; } = @"D:\\OneDrive\\Desktop\\";
        public bool SaveAllInSameFolder { get; set; } = false;
        public string ReportType { get; set; } = "Don't Save Report";
        public bool SchedulingEnabled { get; set; } = false;
        public DateTime ScheduleStartDate { get; set; } = DateTime.Now;
        public string ScheduleTime { get; set; } = "10:21 AM";
        public string RepeatType { get; set; } = "Does not repeat";

        // PDF Combine Settings
        public bool CombineMultipleSheets { get; set; } = false;
        public string CombineCustomFileName { get; set; } = ""; // Custom file name for combined PDF (legacy - kept for compatibility)
        // ❌ REMOVED: CombineFileNameParameters - no longer persisted, user sets fresh each time
        public List<string> SheetViewOrder { get; set; } = new List<string>(); // Ordered list of sheet/view IDs

        // Selected Items (to restore selection on next load)
        public List<string> SelectedSheetIds { get; set; } = new List<string>();
        public List<string> SelectedViewIds { get; set; } = new List<string>();
        public List<string> SelectedViewSheetSets { get; set; } = new List<string>(); // Selected View/Sheet Sets
    }
}
