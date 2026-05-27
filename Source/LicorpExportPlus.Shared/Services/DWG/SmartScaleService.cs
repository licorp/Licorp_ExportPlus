using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services.DWG;

/// <summary>
/// Smart Scale Service - Auto-detect viewport scale and update title block.
/// Learned from Licorp_Combi CAD SmartScaleService.
/// </summary>
public class SmartScaleService
{
    private readonly Document _document;
    private readonly Dictionary<ElementId, string> _originalValues = new();

    public SmartScaleService(Document document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public List<ViewportInfo> GetViewportsOnSheet(ViewSheet sheet)
    {
        var viewports = new List<ViewportInfo>();

        try
        {
            var viewportIds = sheet.GetAllViewports();
            if (viewportIds == null || viewportIds.Count == 0)
                return viewports;

            foreach (ElementId vpId in viewportIds)
            {
                var viewport = _document.GetElement(vpId) as Viewport;
                if (viewport == null) continue;

                var view = _document.GetElement(viewport.ViewId) as View;
                if (view == null) continue;

                var info = new ViewportInfo
                {
                    ElementId = vpId,
                    ViewId = viewport.ViewId,
                    ViewName = view.Name,
                    Scale = view.Scale,
                    ScaleText = $"1:{view.Scale}"
                };

                try
                {
                    var outline = viewport.GetBoxOutline();
                    if (outline != null)
                    {
                        info.Width = outline.MaximumPoint.X - outline.MinimumPoint.X;
                        info.Height = outline.MaximumPoint.Y - outline.MinimumPoint.Y;
                        info.Area = info.Width * info.Height;
                    }
                }
                catch { }

                viewports.Add(info);
            }
        }
        catch (Exception ex)
        {
            LicorpTrace.Warn($"[SmartScale] Error getting viewports: {ex.Message}");
        }

        return viewports;
    }

    public string FormatScale(int scale)
    {
        if (scale <= 0) return "As Indicated";
        return $"1:{scale}";
    }

    private ElementId FindTitleBlock(ViewSheet sheet)
    {
        try
        {
            var titleBlock = new FilteredElementCollector(_document, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilyInstance))
                .FirstOrDefault();
            return titleBlock?.Id;
        }
        catch (Exception ex)
        {
            LicorpTrace.Warn($"[SmartScale] Error finding title block: {ex.Message}");
        }

        return null;
    }

    public bool ApplySmartScale(ViewSheet sheet, Transaction trans)
    {
        try
        {
            var viewports = GetViewportsOnSheet(sheet);
            if (viewports.Count == 0) return false;

            // Find the largest viewport (primary view)
            var primaryViewport = viewports.OrderByDescending(v => v.Area).FirstOrDefault();
            if (primaryViewport == null) return false;

            var titleBlockId = FindTitleBlock(sheet);
            if (titleBlockId == null) return false;

            var titleBlock = _document.GetElement(titleBlockId);
            if (titleBlock == null) return false;

            // Store original value for restore
            var scaleParam = titleBlock.LookupParameter("Scale") 
                ?? titleBlock.LookupParameter("View Scale")
                ?? titleBlock.LookupParameter("Drawing Scale");

            if (scaleParam != null && !scaleParam.IsReadOnly)
            {
                _originalValues[titleBlockId] = scaleParam.AsString() ?? "";

                // Update with actual viewport scale
                var scaleText = FormatScale(primaryViewport.Scale);
                scaleParam.Set(scaleText);
                LicorpTrace.Info($"[SmartScale] Applied scale {scaleText} to {sheet.SheetNumber}");
                return true;
            }
        }
        catch (Exception ex)
        {
            LicorpTrace.Warn($"[SmartScale] Error applying smart scale: {ex.Message}");
        }

        return false;
    }

    public void RestoreOriginalScale(ViewSheet sheet, Transaction trans)
    {
        try
        {
            var titleBlockId = FindTitleBlock(sheet);
            if (titleBlockId == null) return;

            if (!_originalValues.TryGetValue(titleBlockId, out var originalValue))
                return;

            var titleBlock = _document.GetElement(titleBlockId);
            if (titleBlock == null) return;

            var scaleParam = titleBlock.LookupParameter("Scale") 
                ?? titleBlock.LookupParameter("View Scale")
                ?? titleBlock.LookupParameter("Drawing Scale");

            if (scaleParam != null && !scaleParam.IsReadOnly)
            {
                scaleParam.Set(originalValue);
                _originalValues.Remove(titleBlockId);
                LicorpTrace.Info($"[SmartScale] Restored original scale for {sheet.SheetNumber}");
            }
        }
        catch (Exception ex)
        {
            LicorpTrace.Warn($"[SmartScale] Error restoring scale: {ex.Message}");
        }
    }

    public void ClearState()
    {
        _originalValues.Clear();
    }
}

public class ViewportInfo
{
    public ElementId ElementId { get; set; }
    public ElementId ViewId { get; set; }
    public string ViewName { get; set; }
    public int Scale { get; set; }
    public string ScaleText { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Area { get; set; }
}
