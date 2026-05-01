using Autodesk.Revit.DB;
using LicorpExportPlus.Models;
using Licorp.Diagnostics;
using LicorpExportPlus.Helpers;
using System;

namespace LicorpExportPlus.Services
{
    public class PDFOptionsApplier
    {
        public static void ApplyViewOptionsToSheetNoTransaction(Document doc, ViewSheet sheet, ExportSettings options)
        {
            try
            {
                if (options.HideRefWorkPlanes)
                {
                    SetCategoryVisibilityNoTransaction(doc, sheet, BuiltInCategory.OST_CLines, true);
                }

                if (options.HideScopeBoxes)
                {
                    SetCategoryVisibilityNoTransaction(doc, sheet, BuiltInCategory.OST_VolumeOfInterest, true);
                }

                if (options.HideCropBoundaries)
                {
                    sheet.CropBoxVisible = false;
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error applying view options: {ex.Message}");
            }
        }

        public static void ApplyPrintManagerSettings(PrintManager pm, ExportSettings settings)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(settings.SelectedPdfPrinter))
                {
                    TrySelectPrinter(pm, settings.SelectedPdfPrinter);
                }

                var printSetup = pm.PrintSetup;
                var currentSetting = printSetup.CurrentPrintSetting;
                var printParams = currentSetting.PrintParameters;

                if (settings.Colors == PSColors.BlackAndWhite)
                {
                    printParams.ColorDepth = ColorDepthType.BlackLine;
                }
                else if (settings.Colors == PSColors.Grayscale)
                {
                    printParams.ColorDepth = ColorDepthType.GrayScale;
                }
                else if (settings.Colors == PSColors.Color)
                {
                    printParams.ColorDepth = ColorDepthType.Color;
                }

                if (settings.RasterQuality == PSRasterQuality.High)
                {
                    printParams.RasterQuality = RasterQualityType.High;
                }
                else if (settings.RasterQuality == PSRasterQuality.Medium)
                {
                    printParams.RasterQuality = RasterQualityType.Medium;
                }
                else if (settings.RasterQuality == PSRasterQuality.Low)
                {
                    printParams.RasterQuality = RasterQualityType.Low;
                }

                if (settings.Zoom == PSZoomType.FitToPage)
                {
                    printParams.ZoomType = ZoomType.FitToPage;
                }
                else if (settings.Zoom == PSZoomType.Zoom)
                {
                    printParams.ZoomType = ZoomType.Zoom;
                    printParams.Zoom = settings.ZoomPercentage;
                }

                if (settings.HiddenLineViews == PSHiddenLineViews.VectorProcessing)
                {
                    printParams.HiddenLineViews = HiddenLineViewsType.VectorProcessing;
                }
                else if (settings.HiddenLineViews == PSHiddenLineViews.RasterProcessing)
                {
                    printParams.HiddenLineViews = HiddenLineViewsType.RasterProcessing;
                }

                printParams.HideCropBoundaries = settings.HideCropBoundaries;
                printParams.HideScopeBoxes = settings.HideScopeBoxes;
                printParams.HideUnreferencedViewTags = settings.HideUnreferencedViewTags;
                TrySetPrintParameter(printParams, "ReplaceHalftoneWithThinLines", settings.ReplaceHalftone);
                TrySetPrintParameter(printParams, "MaskCoincidentLines", settings.RegionEdgesMask);
                TrySetPrintParameter(printParams, "ViewLinksInBlue", settings.ViewLinksInBlue);
                ApplyPaperPlacement(printParams, settings);
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error applying print manager settings: {ex.Message}");
            }
        }

#if REVIT2024_OR_GREATER
        public static void ApplyNativePdfExportOptions(PDFExportOptions options, ExportSettings settings)
        {
            if (options == null || settings == null) return;

            options.Combine = settings.CombineFiles;
            options.HideCropBoundaries = settings.HideCropBoundaries;
            options.HideScopeBoxes = settings.HideScopeBoxes;
            options.HideUnreferencedViewTags = settings.HideUnreferencedViewTags;
            options.ColorDepth = ToColorDepth(settings.Colors);
            options.RasterQuality = ToRasterQuality(settings.RasterQuality);

            TrySetNativeOption(options, "ReplaceHalftoneWithThinLines", settings.ReplaceHalftone);
            TrySetNativeOption(options, "RegionEdgesMaskCoincidentLines", settings.RegionEdgesMask);
            TrySetNativeOption(options, "MaskCoincidentLines", settings.RegionEdgesMask);
            TrySetNativeOption(options, "ViewLinksInBlue", settings.ViewLinksInBlue);
            TrySetNativeOption(options, "AlwaysUseVectorText", true);
            TrySetNativeOption(options, "KeepPaperSize", settings.KeepPaperSize);
            TrySetNativeOption(options, "KeepPaperSizeAndOrientation", settings.KeepPaperSize);

            TrySetNativeOption(options, "HiddenLineViews", settings.HiddenLineViews == PSHiddenLineViews.VectorProcessing ? 0 : 1);
            TrySetNativeOption(options, "ZoomType", settings.Zoom == PSZoomType.FitToPage ? 0 : 1);
            TrySetNativeOption(options, "ZoomPercentage", settings.ZoomPercentage);
            TrySetNativeOption(options, "Zoom", settings.ZoomPercentage);

            ApplyPaperPlacement(options, settings);
        }

        private static ColorDepthType ToColorDepth(PSColors colors)
        {
            switch (colors)
            {
                case PSColors.BlackAndWhite:
                    return ColorDepthType.BlackLine;
                case PSColors.Grayscale:
                    return ColorDepthType.GrayScale;
                default:
                    return ColorDepthType.Color;
            }
        }

        private static RasterQualityType ToRasterQuality(PSRasterQuality quality)
        {
            switch (quality)
            {
                case PSRasterQuality.Low:
                    return RasterQualityType.Low;
                case PSRasterQuality.Medium:
                    return RasterQualityType.Medium;
                case PSRasterQuality.Maximum:
                    return RasterQualityType.Presentation;
                default:
                    return RasterQualityType.High;
            }
        }
#endif

        private static void ApplyPaperPlacement(object target, ExportSettings settings)
        {
            if (target == null || settings == null) return;

            var centered = settings.PaperPlacement == PSPaperPlacement.Center;
            TrySetNativeOption(target, "PaperPlacement", centered ? 0 : 1);
            TrySetNativeOption(target, "PaperPlacementType", centered ? 0 : 1);
            TrySetNativeOption(target, "Centered", centered);
            TrySetNativeOption(target, "Center", centered);

            if (!centered)
            {
                TrySetNativeOption(target, "OriginOffsetX", settings.OffsetX);
                TrySetNativeOption(target, "OriginOffsetY", settings.OffsetY);
                TrySetNativeOption(target, "OffsetX", settings.OffsetX);
                TrySetNativeOption(target, "OffsetY", settings.OffsetY);
            }

            TrySetNativeOption(target, "PaperMargin", settings.PaperMargin == PSPaperMargin.NoMargin ? 0 : settings.PaperMargin == PSPaperMargin.PrinterLimit ? 1 : 2);
            TrySetNativeOption(target, "MarginType", settings.PaperMargin == PSPaperMargin.NoMargin ? 0 : settings.PaperMargin == PSPaperMargin.PrinterLimit ? 1 : 2);
        }

        private static void TrySelectPrinter(PrintManager pm, string printerName)
        {
            try
            {
                pm.SelectNewPrintDriver(printerName);
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"PDF printer '{printerName}' is not available: {ex.Message}");
            }
        }

        private static void TrySetPrintParameter(object target, string propertyName, object value)
        {
            TrySetNativeOption(target, propertyName, value);
        }

        private static void TrySetNativeOption(object target, string propertyName, object value)
        {
            if (!ReflectionHelper.TrySetProperty(target, propertyName, value))
            {
                LicorpTrace.Dbg($"PDF option not supported by this Revit API: {propertyName}");
            }
        }

        private static void SetCategoryVisibilityNoTransaction(Document doc, Autodesk.Revit.DB.View view, BuiltInCategory category, bool hide)
        {
            try
            {
                var catId = new ElementId(category);
                if (view.CanCategoryBeHidden(catId))
                {
                    view.SetCategoryHidden(catId, hide);
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Error($"Error setting category visibility: {ex.Message}");
            }
        }
    }
}
