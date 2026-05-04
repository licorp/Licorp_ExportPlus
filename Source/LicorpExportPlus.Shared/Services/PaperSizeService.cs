using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services
{
    public class PaperSizeService
    {
        public string DetectPaperSize(ViewSheet sheet)
        {
            try
            {
                var titleBlock = GetTitleBlock(sheet);
                if (titleBlock != null)
                {
                    var width = titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH)?.AsDouble() ?? 0;
                    var height = titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT)?.AsDouble() ?? 0;

                    return ClassifyPaperSize(width, height);
                }
                return "Unknown";
            }
            catch (Exception)
            {
                return "Error";
            }
        }

        private Element GetTitleBlock(ViewSheet sheet)
        {
            var collector = new FilteredElementCollector(sheet.Document, sheet.Id);
            return collector.OfCategory(BuiltInCategory.OST_TitleBlocks).FirstElement();
        }

        private string ClassifyPaperSize(double width, double height)
        {
            var widthMm = width * 304.8; // Convert from feet to mm
            var heightMm = height * 304.8;

            if (IsCloseToSize(widthMm, heightMm, 297, 420) || IsCloseToSize(widthMm, heightMm, 420, 297))
                return "A3";
            if (IsCloseToSize(widthMm, heightMm, 210, 297) || IsCloseToSize(widthMm, heightMm, 297, 210))
                return "A4";
            if (IsCloseToSize(widthMm, heightMm, 594, 841) || IsCloseToSize(widthMm, heightMm, 841, 594))
                return "A1";
            if (IsCloseToSize(widthMm, heightMm, 420, 594) || IsCloseToSize(widthMm, heightMm, 594, 420))
                return "A2";

            return "Custom";
        }

        private bool IsCloseToSize(double w1, double h1, double w2, double h2)
        {
            const double tolerance = 10.0; // 10mm tolerance
            return Math.Abs(w1 - w2) < tolerance && Math.Abs(h1 - h2) < tolerance;
        }

        public List<string> GetAvailablePaperSizes()
        {
            return new List<string> { "A0", "A1", "A2", "A3", "A4", "Custom" };
        }

        public CustomPaperSize GetDefaultPaperSize()
        {
            return CustomPaperSize.GetDefaultPaperSize();
        }

        public List<CustomPaperSize> GetStandardPaperSizes()
        {
            return new List<CustomPaperSize>
            {
                new CustomPaperSize { Name = "A4", Width = 210, Height = 297 },
                new CustomPaperSize { Name = "A3", Width = 297, Height = 420 },
                new CustomPaperSize { Name = "A2", Width = 420, Height = 594 },
                new CustomPaperSize { Name = "A1", Width = 594, Height = 841 },
                new CustomPaperSize { Name = "A0", Width = 841, Height = 1189 }
            };
        }
    }
}
