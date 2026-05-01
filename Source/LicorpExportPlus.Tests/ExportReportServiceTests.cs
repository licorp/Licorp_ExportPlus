using System.IO;
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
}
