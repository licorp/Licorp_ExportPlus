using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.ViewModels;

public partial class FormatsViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsPdfSelected { get; set; }
    [ObservableProperty] public partial bool IsDwgSelected { get; set; } = true;
    [ObservableProperty] public partial bool IsIfcSelected { get; set; }
    [ObservableProperty] public partial bool IsNwcSelected { get; set; }
    [ObservableProperty] public partial bool IsImgSelected { get; set; }
    [ObservableProperty] public partial bool IsDxfSelected { get; set; }
    [ObservableProperty] public partial bool IsXmlSelected { get; set; }

    [ObservableProperty] public partial string PdfColorMode { get; set; } = "Color";
    [ObservableProperty] public partial string PdfRasterQuality { get; set; } = "High";
    [ObservableProperty] public partial bool PdfCombineFiles { get; set; }
    [ObservableProperty] public partial bool PdfSkipEmptySheets { get; set; }
    [ObservableProperty] public partial bool PdfHideCropBoundaries { get; set; } = true;
    [ObservableProperty] public partial bool PdfHideScopeBoxes { get; set; } = true;
    [ObservableProperty] public partial bool PdfHideRefWorkPlanes { get; set; } = true;
    [ObservableProperty] public partial bool PdfHideUnreferencedViewTags { get; set; } = true;

    [ObservableProperty] public partial string DwgExportSetupName { get; set; } = "Default Setup";
    [ObservableProperty] public partial string DwgVersion { get; set; } = "2018";
    [ObservableProperty] public partial bool DwgCompactFiles { get; set; } = true;
    [ObservableProperty] public partial bool DwgExportViewsOnSheets { get; set; }
    [ObservableProperty] public partial bool DwgUseSharedCoordinates { get; set; } = true;

    [ObservableProperty] public partial string IfcVersion { get; set; } = "IFC 2x3 Coordination View 2.0";
    [ObservableProperty] public partial string IfcSpaceBoundaries { get; set; } = "None";
    [ObservableProperty] public partial bool IfcExportBaseQuantities { get; set; }
    [ObservableProperty] public partial bool IfcSplitWallsByLevel { get; set; } = true;

    [ObservableProperty] public partial string NwcCoordinates { get; set; } = "Shared";
    [ObservableProperty] public partial bool NwcExportRoomGeometry { get; set; }
    [ObservableProperty] public partial bool NwcDivideFileIntoLevels { get; set; }

    [ObservableProperty] public partial string ImageFormat { get; set; } = "PNG";
    [ObservableProperty] public partial int ImageResolution { get; set; } = 300;

    [ObservableProperty] public partial int ActiveFormatTab { get; set; }

    public bool HasSelectedFormat => IsPdfSelected || IsDwgSelected || IsIfcSelected ||
                                      IsNwcSelected || IsImgSelected || IsDxfSelected || IsXmlSelected;

    public List<string> GetSelectedFormats()
    {
        var formats = new List<string>();
        if (IsPdfSelected) formats.Add("PDF");
        if (IsDwgSelected) formats.Add("DWG");
        if (IsDxfSelected) formats.Add("DXF");
        if (IsIfcSelected) formats.Add("IFC");
        if (IsNwcSelected) formats.Add("NWC");
        if (IsImgSelected) formats.Add("IMG");
        if (IsXmlSelected) formats.Add("XML");
        return formats;
    }

    public ExportSettings GetExportSettings()
    {
        return new ExportSettings
        {
            Colors = PdfColorMode switch
            {
                "Grayscale" => PSColors.Grayscale,
                "BlackAndWhite" => PSColors.BlackAndWhite,
                _ => PSColors.Color
            },
            RasterQuality = PdfRasterQuality switch
            {
                "Low" => PSRasterQuality.Low,
                "Medium" => PSRasterQuality.Medium,
                "Maximum" => PSRasterQuality.Maximum,
                _ => PSRasterQuality.High
            },
            CombineFiles = PdfCombineFiles,
            SkipEmptySheets = PdfSkipEmptySheets,
            HideCropBoundaries = PdfHideCropBoundaries,
            HideScopeBoxes = PdfHideScopeBoxes,
            HideRefWorkPlanes = PdfHideRefWorkPlanes,
            HideUnreferencedViewTags = PdfHideUnreferencedViewTags,
            DWGExportSetupName = DwgExportSetupName,
            DWGVersion = DwgVersion,
            CompactDwgFiles = DwgCompactFiles,
            ExportViewsOnSheets = DwgExportViewsOnSheets,
            UseSharedCoordinates = DwgUseSharedCoordinates
        };
    }

    [RelayCommand]
    private void SelectAllFormats()
    {
        IsPdfSelected = true;
        IsDwgSelected = true;
        IsIfcSelected = true;
        IsNwcSelected = true;
        IsImgSelected = true;
    }

    [RelayCommand]
    private void ClearAllFormats()
    {
        IsPdfSelected = false;
        IsDwgSelected = false;
        IsIfcSelected = false;
        IsNwcSelected = false;
        IsImgSelected = false;
        IsDxfSelected = false;
        IsXmlSelected = false;
    }
}
