using System.IO;
using ClosedXML.Excel;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services;
using NUnit.Framework;

namespace LicorpExportPlus.Tests;

[TestFixture]
public class ExportReportServiceTests
{
    [Test]
    public void WriteCsvReport_WritesStatusPathAndErrorColumns()
    {
        var outputFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "report-test");
        var item = new ExportQueueItem
        {
            ViewSheetNumber = "A101",
            ViewSheetName = "Floor Plan",
            Format = "PDF",
            Size = "A1",
            Orientation = "Landscape",
            Status = "Failed",
            Progress = 0,
            OutputPath = @"C:\Exports",
            ErrorMessage = "Printer unavailable"
        };

        var reportPath = ExportReportService.WriteCsvReport(new[] { item }, outputFolder);
        var csv = File.ReadAllText(reportPath);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(reportPath), Is.True);
            Assert.That(csv, Does.Contain("ViewSheetNumber,ViewSheetName,Format,Size,Orientation,Status,Progress,OutputPath,ErrorMessage,CompletedAt"));
            Assert.That(csv, Does.Contain("\"A101\""));
            Assert.That(csv, Does.Contain("\"Printer unavailable\""));
        });
    }

    [Test]
    public void WriteXlsxReport_WritesStatusPathAndErrorColumns()
    {
        var outputFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "report-xlsx-test");
        var item = new ExportQueueItem
        {
            ViewSheetNumber = "A102",
            ViewSheetName = "Ceiling Plan",
            Format = "DWG",
            Size = "A1",
            Orientation = "Landscape",
            Status = "Completed",
            Progress = 100,
            OutputPath = @"C:\Exports\A102.dwg",
            ErrorMessage = "",
            CompletedAt = "2026-05-02 10:00:00"
        };

        var reportPath = ExportReportService.WriteXlsxReport(new[] { item }, outputFolder);

        using var workbook = new XLWorkbook(reportPath);
        var worksheet = workbook.Worksheet("Export Report");

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(reportPath), Is.True);
            Assert.That(worksheet.Cell(1, 1).GetString(), Is.EqualTo("ViewSheetNumber"));
            Assert.That(worksheet.Cell(2, 1).GetString(), Is.EqualTo("A102"));
            Assert.That(worksheet.Cell(2, 3).GetString(), Is.EqualTo("DWG"));
            Assert.That(worksheet.Cell(2, 8).GetString(), Is.EqualTo(@"C:\Exports\A102.dwg"));
        });
    }
}
