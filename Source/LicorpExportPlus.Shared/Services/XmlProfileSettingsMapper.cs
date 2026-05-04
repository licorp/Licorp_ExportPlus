using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services
{
    public static class XmlProfileSettingsMapper
    {
        public static void ApplyTemplateToSettings(TemplateInfo template, ProfileSettings settings)
        {
            if (template == null || settings == null)
            {
                return;
            }

            settings.PDFEnabled = template.IsPDFChecked;
            settings.DWGEnabled = template.IsDWGChecked;
            settings.DGNEnabled = false;
            settings.IFCEnabled = template.IsIFCChecked;
            settings.IMGEnabled = template.IsIMGChecked;
            settings.CompactDwgFiles = template.DWG_MergedViews;

            settings.HideCropBoundaries = template.HideCropBoundaries;
            settings.HideScopeBoxes = template.HideScopeBox;

            settings.SaveAllInSameFolder = !template.IsSeparateFile;
            if (!string.IsNullOrWhiteSpace(template.FilePath))
            {
                settings.OutputFolder = template.FilePath;
            }

            settings.PDFVectorProcessing = template.IsVectorProcessing;
            settings.PDFRasterQuality = template.RasterQuality;
            settings.PDFColorMode = template.Color;
            settings.PDFFitToPage = template.IsFitToPage;
            settings.PDFIsCenter = template.IsCenter;
            settings.PDFMarginType = template.SelectedMarginType;

            if (template.DWF != null)
            {
                settings.DWFImageFormat = template.DWF.OptImageFormat;
                settings.DWFImageQuality = template.DWF.OptImageQuality;
                settings.DWFExportTextures = template.DWF.OptExportTextures;
            }

            if (template.NWC != null)
            {
                settings.NWCConvertElementProperties = template.NWC.ConvertElementProperties;
                settings.NWCCoordinates = template.NWC.Coordinates;
                settings.NWCDivideFileIntoLevels = template.NWC.DivideFileIntoLevels;
                settings.NWCExportElementIds = template.NWC.ExportElementIds;
                settings.NWCExportParts = template.NWC.ExportParts;
                settings.NWCFacetingFactor = template.NWC.FacetingFactor;
            }

            if (template.IFC != null)
            {
                settings.IFCFileVersion = template.IFC.FileVersion;
                settings.IFCSpaceBoundaries = template.IFC.SpaceBoundaries;
                settings.IFCSitePlacement = template.IFC.SitePlacement;
                settings.IFCExportBaseQuantities = template.IFC.ExportBaseQuantities;
                settings.IFCExportIFCCommonPropertySets = template.IFC.ExportIFCCommonPropertySets;
                settings.IFCTessellationLevelOfDetail = template.IFC.TessellationLevelOfDetail;
            }

            if (template.IMG != null)
            {
                settings.IMGImageResolution = template.IMG.ImageResolution;
                settings.IMGFileType = template.IMG.HLRandWFViewsFileType;
                settings.IMGZoomType = template.IMG.ZoomType;
                settings.IMGPixelSize = template.IMG.PixelSize;
            }
        }
    }
}
