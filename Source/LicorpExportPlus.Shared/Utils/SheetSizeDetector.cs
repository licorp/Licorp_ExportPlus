using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Nice3point.Revit.Extensions;

namespace LicorpExportPlus.Utils
{
    public class SheetSizeDetector
    {
        // ⚡ CACHE: Store all TitleBlock data once for entire document
        private static Dictionary<ElementId, string> _titleBlockSizeCache = new Dictionary<ElementId, string>();
        private static Document _cachedDocument = null;

        /// <summary>
        /// ⚡ OPTIMIZATION: Batch load all TitleBlocks at once, then use cache for each sheet
        /// Call this ONCE before processing multiple sheets
        /// </summary>
        public static void PreloadTitleBlockSizes(Document doc)
        {
            if (doc == null || doc == _cachedDocument) return;

            _titleBlockSizeCache.Clear();
            _cachedDocument = doc;

            try
            {
                // ⚡ Load ALL TitleBlocks in document once (much faster than per-sheet)
                var allTitleBlocks = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType() // Nice3point extension
                    .Cast<FamilyInstance>()
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[SheetSizeDetector] ⚡ Preloaded {allTitleBlocks.Count} TitleBlocks");

                // Build cache: OwnerViewId -> Size
                foreach (var tb in allTitleBlocks)
                {
                    if (tb.OwnerViewId == null || tb.OwnerViewId == ElementId.InvalidElementId)
                        continue;

                    // Try instance parameters first (fastest)
                    string size = TryGetParameterValue(tb, new[] { "Sheet Size", "Paper Size", "Size" });

                    if (string.IsNullOrEmpty(size) && tb.Symbol != null)
                    {
                        // Try type parameters if instance failed
                        size = TryGetParameterValue(tb.Symbol, new[] { "Sheet Size", "Paper Size", "Size" });
                    }

                    if (!string.IsNullOrEmpty(size) && size != "Unknown")
                    {
                        _titleBlockSizeCache[tb.OwnerViewId] = size;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[SheetSizeDetector] ✅ Cached sizes for {_titleBlockSizeCache.Count} sheets");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SheetSizeDetector] ERROR preloading: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear cache when switching documents
        /// </summary>
        public static void ClearCache()
        {
            _titleBlockSizeCache.Clear();
            _cachedDocument = null;
        }

        public static string GetSheetSize(ViewSheet sheet)
        {
            if (sheet == null) return "A1";

            // ⚡ FAST PATH: Check cache first (if preloaded)
            if (_titleBlockSizeCache.TryGetValue(sheet.Id, out string cachedSize))
            {
                return cachedSize;
            }

            // FALLBACK: Try sheet parameters (fast, no collector needed)
            string size = GetSizeFromSheetParameters(sheet);
            if (!string.IsNullOrEmpty(size) && size != "Unknown")
                return size;

            // FALLBACK: Calculate from dimensions (slower)
            size = GetSizeFromDimensions(sheet);
            if (!string.IsNullOrEmpty(size) && size != "Unknown")
                return size;

            // FALLBACK: Guess from sheet number
            return GuessFromSheetNumber(sheet.SheetNumber);
        }

        private static string GetSizeFromSheetParameters(ViewSheet sheet)
        {
            try
            {
                return TryGetParameterValue(sheet, new[] {
                    "Sheet Size", "Paper Size", "Size", "Format"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting size from sheet parameters: {ex.Message}");
            }

            return "Unknown";
        }

        private static string TryGetParameterValue(Element element, string[] parameterNames)
        {
            foreach (string paramName in parameterNames)
            {
                var param = element.LookupParameter(paramName);
                if (param != null && param.HasValue)
                {
                    string value = param.AsString();
                    if (!string.IsNullOrEmpty(value) && value.Trim() != "")
                        return value.Trim();
                }
            }
            return null;
        }

        private static string GetSizeFromDimensions(ViewSheet sheet)
        {
            try
            {
                var outline = sheet.Outline;
                if (outline != null)
                {
                    double widthFeet = outline.Max.U - outline.Min.U;
                    double heightFeet = outline.Max.V - outline.Min.V;

// Revit 2025+ uses UnitTypeId, older versions use DisplayUnitType
#if REVIT2025_OR_GREATER
			double widthMM = UnitUtils.ConvertFromInternalUnits(widthFeet, UnitTypeId.Millimeters);
			double heightMM = UnitUtils.ConvertFromInternalUnits(heightFeet, UnitTypeId.Millimeters);
#else
			double widthMM = UnitUtils.ConvertFromInternalUnits(widthFeet, DisplayUnitType.DUT_MILLIMETERS);
			double heightMM = UnitUtils.ConvertFromInternalUnits(heightFeet, DisplayUnitType.DUT_MILLIMETERS);
#endif

                    return DeterminePaperSize(widthMM, heightMM);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating paper size: {ex.Message}");
            }

            return "Unknown";
        }

        private static string GuessFromSheetNumber(string sheetNumber)
        {
            if (string.IsNullOrEmpty(sheetNumber))
                return "A1";

            // Dự đoán dựa trên prefix của sheet number
            if (sheetNumber.StartsWith("A0")) return "A0";
            if (sheetNumber.StartsWith("A1")) return "A1";
            if (sheetNumber.StartsWith("A2")) return "A2";
            if (sheetNumber.StartsWith("A3")) return "A3";
            if (sheetNumber.StartsWith("A4")) return "A4";

            // Default cho architectural sheets
            if (sheetNumber.StartsWith("A")) return "A1";

            return "A1"; // Default fallback
        }

        private static string DeterminePaperSize(double widthMM, double heightMM)
        {
            // Đảm bảo width luôn là chiều dài (lớn hơn)
            if (widthMM < heightMM)
            {
                double temp = widthMM;
                widthMM = heightMM;
                heightMM = temp;
            }

            // Định nghĩa các kích thước chuẩn (tolerance ±15mm)
            var standardSizes = new Dictionary<string, (double width, double height)>
            {
                ["A0"] = (1189, 841),
                ["A1"] = (841, 594),
                ["A2"] = (594, 420),
                ["A3"] = (420, 297),
                ["A4"] = (297, 210),
                ["B0"] = (1414, 1000),
                ["B1"] = (1000, 707),
                ["B2"] = (707, 500),
                ["B3"] = (500, 353),
                ["B4"] = (353, 250),
                ["ANSI C"] = (610, 457),
                ["ANSI D"] = (914, 610),
                ["ANSI E"] = (1118, 864),
                ["Tabloid"] = (432, 279),
                ["Letter"] = (279, 216)
            };

            const double tolerance = 15.0; // 15mm tolerance

            foreach (var size in standardSizes)
            {
                var (w, h) = size.Value;

                if (Math.Abs(widthMM - w) <= tolerance && Math.Abs(heightMM - h) <= tolerance)
                {
                    return size.Key;
                }
            }

            // Nếu không khớp với size chuẩn, return custom size
            return $"{widthMM:F0}x{heightMM:F0}mm";
        }
    }
}
