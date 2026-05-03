using System.Collections.Generic;
using LicorpExportPlus.Models;
using NUnit.Framework;

namespace LicorpExportPlus.Tests;

[TestFixture]
public class ExportSettingsTests
{
    [Test]
    public void DgnAndDwf_AreNotReturnedAsSelectableFormatsUntilServicesExist()
    {
        var settings = new ExportSettings
        {
            IsDgnSelected = true,
            IsDwfSelected = true,
            IsPdfSelected = true
        };

        var formats = settings.GetSelectedFormatsList();

        Assert.That(formats, Does.Contain("PDF"));
        Assert.That(formats, Does.Not.Contain("DGN"));
        Assert.That(formats, Does.Not.Contain("DWF"));
        Assert.Multiple(() =>
        {
            Assert.That(settings.IsDgnSelected, Is.False);
            Assert.That(settings.IsDwfSelected, Is.False);
        });
    }

    [Test]
    public void SelectedFormats_SetterKeepsBindingsSafeWhenKeysAreMissing()
    {
        var settings = new ExportSettings
        {
            SelectedFormats = new Dictionary<string, bool>
            {
                ["PDF"] = true,
                ["DGN"] = true,
                ["DWF"] = true
            }
        };

        settings.IsDwgSelected = true;
        settings.IsImgSelected = true;

        var formats = settings.GetSelectedFormatsList();

        Assert.That(formats, Is.EquivalentTo(new[] { "PDF", "DWG", "IMG" }));
    }

    [Test]
    public void ZoomPercentage_IsClampedToSupportedRange()
    {
        var settings = new ExportSettings();

        settings.ZoomPercentage = -10;
        Assert.That(settings.ZoomPercentage, Is.EqualTo(1));

        settings.ZoomPercentage = 5000;
        Assert.That(settings.ZoomPercentage, Is.EqualTo(1000));
    }
}
