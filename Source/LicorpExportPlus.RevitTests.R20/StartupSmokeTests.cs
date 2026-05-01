#nullable enable
using System.Linq;
using Autodesk.Revit.UI;
using NUnit.Framework;

namespace LicorpExportPlus.RevitTests.R20;

[TestFixture]
public class StartupSmokeTests
{
    private ExportPlusApplication? _application;
    private UIControlledApplication? _controlledApplication;

    [OneTimeSetUp]
    public void Setup(UIControlledApplication controlledApplication)
    {
        _controlledApplication = controlledApplication;
        _application = new ExportPlusApplication();
    }

    [Test]
    public void Startup_CreatesLicorpExportPanel()
    {
        Assert.That(_application, Is.Not.Null);
        Assert.That(_controlledApplication, Is.Not.Null);

        var startupResult = _application!.OnStartup(_controlledApplication!);
        var panels = _controlledApplication!.GetRibbonPanels("Licorp");
        var exportPanel = panels.FirstOrDefault(panel => panel.Name == "Export");

        Assert.Multiple(() =>
        {
            Assert.That(startupResult, Is.EqualTo(Result.Succeeded));
            Assert.That(exportPanel, Is.Not.Null);
            Assert.That(exportPanel!.GetItems().Any(item => item.Name == "ExportPlus"), Is.True);
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        if (_application != null && _controlledApplication != null)
        {
            _application.OnShutdown(_controlledApplication);
        }
    }
}
