using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
