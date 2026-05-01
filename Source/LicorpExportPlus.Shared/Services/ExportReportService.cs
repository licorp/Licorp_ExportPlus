using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services
{
    public static class ExportReportService
    {
        public static string WriteCsvReport(IEnumerable<ExportQueueItem> items, string outputFolder)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Output folder is required.", nameof(outputFolder));

            Directory.CreateDirectory(outputFolder);

            var reportPath = Path.Combine(outputFolder, $"LicorpExportPlus_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var builder = new StringBuilder();
            builder.AppendLine("ViewSheetNumber,ViewSheetName,Format,Size,Orientation,Status,Progress,OutputPath,ErrorMessage,CompletedAt");

            foreach (var item in items)
            {
                builder.AppendLine(string.Join(",",
                    Csv(item.ViewSheetNumber),
                    Csv(item.ViewSheetName),
                    Csv(item.Format),
                    Csv(item.Size),
                    Csv(item.Orientation),
                    Csv(item.Status),
                    Csv(item.Progress.ToString("0.##", CultureInfo.InvariantCulture)),
                    Csv(item.OutputPath),
                    Csv(item.ErrorMessage),
                    Csv(item.CompletedAt)));
            }

            File.WriteAllText(reportPath, builder.ToString(), new UTF8Encoding(true));
            return reportPath;
        }

        public static string WriteXlsxReport(IEnumerable<ExportQueueItem> items, string outputFolder)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Output folder is required.", nameof(outputFolder));

            Directory.CreateDirectory(outputFolder);

            var reportPath = Path.Combine(outputFolder, $"LicorpExportPlus_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Export Report");
                var headers = new[]
                {
                    "ViewSheetNumber",
                    "ViewSheetName",
                    "Format",
                    "Size",
                    "Orientation",
                    "Status",
                    "Progress",
                    "OutputPath",
                    "ErrorMessage",
                    "CompletedAt"
                };

                for (int column = 0; column < headers.Length; column++)
                {
                    worksheet.Cell(1, column + 1).Value = headers[column];
                }

                var row = 2;
                foreach (var item in items)
                {
                    worksheet.Cell(row, 1).Value = item.ViewSheetNumber ?? string.Empty;
                    worksheet.Cell(row, 2).Value = item.ViewSheetName ?? string.Empty;
                    worksheet.Cell(row, 3).Value = item.Format ?? string.Empty;
                    worksheet.Cell(row, 4).Value = item.Size ?? string.Empty;
                    worksheet.Cell(row, 5).Value = item.Orientation ?? string.Empty;
                    worksheet.Cell(row, 6).Value = item.Status ?? string.Empty;
                    worksheet.Cell(row, 7).Value = item.Progress;
                    worksheet.Cell(row, 8).Value = item.OutputPath ?? string.Empty;
                    worksheet.Cell(row, 9).Value = item.ErrorMessage ?? string.Empty;
                    worksheet.Cell(row, 10).Value = item.CompletedAt ?? string.Empty;
                    row++;
                }

                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(reportPath);
            }

            return reportPath;
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
