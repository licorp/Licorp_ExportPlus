using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using LicorpExportPlus.Utils;

namespace LicorpExportPlus.Services
{
    public class XMLExportService
    {
        public void ExportToXML(List<ViewSheet> sheets, string outputPath)
        {
            try
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("ExportPlusExport",
                        new XAttribute("ExportDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                        new XElement("Sheets",
                            sheets.Select(sheet => CreateSheetElement(sheet))
                        )
                    )
                );

                doc.Save(outputPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi xuất XML: {ex.Message}", ex);
            }
        }

        private XElement CreateSheetElement(ViewSheet sheet)
        {
return new XElement("Sheet",
new XAttribute("Id",
                    sheet.Id.GetIdValue()
),
                new XElement("Name", sheet.Name ?? ""),
                new XElement("Number", sheet.SheetNumber ?? ""),
                new XElement("Properties",
                    GetSheetProperties(sheet).Select(prop =>
                        new XElement("Property",
                            new XAttribute("Name", prop.Key),
                            new XElement("Value", prop.Value ?? "")
                        )
                    )
                )
            );
        }

        private Dictionary<string, string> GetSheetProperties(ViewSheet sheet)
        {
            var properties = new Dictionary<string, string>();

            try
            {
                properties["Title"] = GetParameterValue(sheet, BuiltInParameter.SHEET_NAME);
                properties["Number"] = GetParameterValue(sheet, BuiltInParameter.SHEET_NUMBER);
                properties["DrawnBy"] = GetParameterValue(sheet, BuiltInParameter.SHEET_DRAWN_BY);
                properties["CheckedBy"] = GetParameterValue(sheet, BuiltInParameter.SHEET_CHECKED_BY);
                properties["ApprovedBy"] = GetParameterValue(sheet, BuiltInParameter.SHEET_APPROVED_BY);

                var titleBlock = GetTitleBlock(sheet);
                if (titleBlock != null)
                {
                    properties["Width"] = GetParameterDisplayValue(titleBlock, BuiltInParameter.SHEET_WIDTH);
                    properties["Height"] = GetParameterDisplayValue(titleBlock, BuiltInParameter.SHEET_HEIGHT);
                }
            }
            catch (Exception ex)
            {
                properties["Error"] = ex.Message;
            }

            return properties;
        }

        private string GetParameterValue(Element element, BuiltInParameter paramId)
        {
            var param = element.get_Parameter(paramId);
            return param?.AsString() ?? "";
        }

        private string GetParameterDisplayValue(Element element, BuiltInParameter paramId)
        {
            var param = element.get_Parameter(paramId);
            return param?.AsValueString() ?? "";
        }

        private Element GetTitleBlock(ViewSheet sheet)
        {
            var collector = new FilteredElementCollector(sheet.Document, sheet.Id);
            return collector.OfCategory(BuiltInCategory.OST_TitleBlocks).FirstElement();
        }
    }
}
