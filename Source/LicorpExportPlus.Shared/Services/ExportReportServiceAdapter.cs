using System.Collections.Generic;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services.Interfaces;

namespace LicorpExportPlus.Services;

public class ExportReportServiceAdapter : IExportReportService
{
    public string WriteCsvReport(IEnumerable<ExportQueueItem> items, string outputFolder)
    {
        return ExportReportService.WriteCsvReport(items, outputFolder);
    }

    public string WriteXlsxReport(IEnumerable<ExportQueueItem> items, string outputFolder)
    {
        return ExportReportService.WriteXlsxReport(items, outputFolder);
    }
}
