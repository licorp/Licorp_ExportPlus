using System;
using System.Collections.Generic;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Utils
{
    internal static class ExportQueueBuilder
    {
        public static IEnumerable<ExportQueueItem> BuildSheetItems(
            IEnumerable<SheetItem> sheets,
            IEnumerable<string> selectedFormats,
            Func<SheetItem, string> getSheetSize,
            Func<SheetItem, string> getSheetOrientation,
            Func<string, string, string> buildOutputPath)
        {
            if (sheets == null || selectedFormats == null)
            {
                yield break;
            }

            foreach (var sheet in sheets)
            {
                if (sheet == null || !sheet.IsSelected)
                {
                    continue;
                }

                foreach (var format in selectedFormats)
                {
                    var formatUpper = ExportFormatSupport.Normalize(format);
                    if (ExportFormatSupport.IsUnsupported(formatUpper))
                    {
                        LicorpTrace.Warn($"{formatUpper} export is not supported in this build and was skipped for sheet queue.");
                        continue;
                    }

                    if (formatUpper == "NWC" || formatUpper == "IFC")
                    {
                        continue;
                    }

                    var displayName = string.IsNullOrWhiteSpace(sheet.CustomFileName)
                        ? sheet.SheetName
                        : sheet.CustomFileName;

                    var size = "-";
                    var orientation = "-";
                    var outputPath = string.Empty;
                    try
                    {
                        size = getSheetSize?.Invoke(sheet) ?? "-";
                    }
                    catch (Exception ex)
                    {
                        LicorpTrace.Warn($"Could not resolve sheet size for '{sheet.SheetNumber}': {ex.Message}");
                    }

                    try
                    {
                        orientation = getSheetOrientation?.Invoke(sheet) ?? "-";
                    }
                    catch (Exception ex)
                    {
                        LicorpTrace.Warn($"Could not resolve sheet orientation for '{sheet.SheetNumber}': {ex.Message}");
                    }

                    try
                    {
                        outputPath = buildOutputPath?.Invoke(displayName, formatUpper) ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        LicorpTrace.Warn($"Could not resolve output path for '{sheet.SheetNumber}' as {formatUpper}: {ex.Message}");
                    }

                    yield return new ExportQueueItem
                    {
                        IsSelected = true,
                        ViewSheetNumber = sheet.SheetNumber,
                        ViewSheetName = displayName,
                        Format = formatUpper,
                        Size = size,
                        Orientation = orientation,
                        OutputPath = outputPath,
                        Progress = 0,
                        Status = "Pending"
                    };
                }
            }
        }

        public static IEnumerable<ExportQueueItem> BuildViewItems(
            IEnumerable<ViewItem> views,
            IEnumerable<string> selectedFormats,
            Func<string, string, string> buildOutputPath)
        {
            if (views == null || selectedFormats == null)
            {
                yield break;
            }

            foreach (var view in views)
            {
                if (view == null || !view.IsSelected)
                {
                    continue;
                }

                var is3DView = view.ViewType != null &&
                    (view.ViewType.Contains("ThreeD") || view.ViewType.Contains("3D"));

                foreach (var format in selectedFormats)
                {
                    var formatUpper = ExportFormatSupport.Normalize(format);
                    if (ExportFormatSupport.IsUnsupported(formatUpper))
                    {
                        LicorpTrace.Warn($"{formatUpper} export is not supported in this build and was skipped for view queue.");
                        continue;
                    }

                    if (is3DView && (formatUpper == "PDF" || formatUpper == "DWG" || formatUpper == "DWF" || formatUpper == "IMG"))
                    {
                        continue;
                    }

                    if (!is3DView && (formatUpper == "NWC" || formatUpper == "IFC"))
                    {
                        continue;
                    }

                    var displayName = string.IsNullOrWhiteSpace(view.CustomFileName)
                        ? view.ViewName
                        : view.CustomFileName;

                    var outputPath = string.Empty;
                    try
                    {
                        outputPath = buildOutputPath?.Invoke(displayName, formatUpper) ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        LicorpTrace.Warn($"Could not resolve output path for view '{view.ViewName}' as {formatUpper}: {ex.Message}");
                    }

                    yield return new ExportQueueItem
                    {
                        IsSelected = true,
                        ViewSheetNumber = view.ViewType,
                        ViewSheetName = displayName,
                        Format = formatUpper,
                        Size = "-",
                        Orientation = "-",
                        OutputPath = outputPath,
                        Progress = 0,
                        Status = "Pending"
                    };
                }
            }
        }
    }
}
