namespace LicorpExportPlus.Models
{
    public class DWGExportSettings
    {
        public string SetupName { get; set; } = "Default";
        public string OutputFolder { get; set; }
        public bool BindImagesAsOLE { get; set; } = false;
        public bool ExportViewsAsXrefs { get; set; } = false;
        public bool MergeViews { get; set; } = false;
        public string LayerSettings { get; set; } = "AIA";
        public string LineWeights { get; set; } = "ByLayer";
        public string Colors { get; set; } = "ByLayer";
        public string Units { get; set; } = "Millimeter";
        public bool ExportRoomBoundaries { get; set; } = true;
        public bool ExportAreaBoundaries { get; set; } = true;
        public string FileVersion { get; set; } = "2018"; // AutoCAD version

        // Advanced settings from DA4R-DwgExporter
        public bool UseSharedCoords { get; set; } = false;
        public bool HideScopeBox { get; set; } = true;
        public bool HideReferencePlane { get; set; } = true;
        public bool HideUnreferenceViewTags { get; set; } = true;
        public bool PreserveCoincidentLines { get; set; } = false;
        public bool ExportViewsOnSheetSeparately { get; set; } = false;
        public bool CompactDwgFiles { get; set; } = true;
        public bool UseHatchBackgroundColor { get; set; } = false;
        public string HatchBackgroundColor { get; set; } = "#FFFFFF";
        public bool MarkNonplotLayers { get; set; } = false;
        public string NonplotSuffix { get; set; } = "-NONPLOT";

        public DWGExportSettings()
        {
        }

        public DWGExportSettings Clone()
        {
            return new DWGExportSettings
            {
                SetupName = this.SetupName,
                OutputFolder = this.OutputFolder,
                BindImagesAsOLE = this.BindImagesAsOLE,
                ExportViewsAsXrefs = this.ExportViewsAsXrefs,
                MergeViews = this.MergeViews,
                LayerSettings = this.LayerSettings,
                LineWeights = this.LineWeights,
                Colors = this.Colors,
                Units = this.Units,
                ExportRoomBoundaries = this.ExportRoomBoundaries,
                ExportAreaBoundaries = this.ExportAreaBoundaries,
                FileVersion = this.FileVersion,
                UseSharedCoords = this.UseSharedCoords,
                HideScopeBox = this.HideScopeBox,
                HideReferencePlane = this.HideReferencePlane,
                HideUnreferenceViewTags = this.HideUnreferenceViewTags,
                PreserveCoincidentLines = this.PreserveCoincidentLines,
                ExportViewsOnSheetSeparately = this.ExportViewsOnSheetSeparately,
                CompactDwgFiles = this.CompactDwgFiles,
                UseHatchBackgroundColor = this.UseHatchBackgroundColor,
                HatchBackgroundColor = this.HatchBackgroundColor,
                MarkNonplotLayers = this.MarkNonplotLayers,
                NonplotSuffix = this.NonplotSuffix
            };
        }
    }
}
