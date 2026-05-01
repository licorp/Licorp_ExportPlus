using System;
using System.Collections.Generic;
using System.IO;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services;
using NUnit.Framework;

namespace LicorpExportPlus.Tests;

[TestFixture]
public class XmlProfileServiceTests
{
    [Test]
    public void SaveAndLoadProfile_RoundTripsCoreFields()
    {
        var profile = new ExportPlusXMLProfile
        {
            Name = "QA Profile",
            FilePath = @"C:\Temp\Exports",
            Version = "1.0.0",
            TemplateInfo = new TemplateInfo
            {
                IsPDFChecked = true,
                IsDWGChecked = true,
                IsSeparateFile = false,
                DWG_MergedViews = true,
                PaperSize = "A1"
            }
        };

        var tempFile = Path.Combine(Path.GetTempPath(), $"exportplus-profile-{Guid.NewGuid():N}.xml");

        try
        {
            XMLProfileService.SaveProfileToXML(profile, tempFile);
            var loaded = XMLProfileService.LoadProfileFromXML(tempFile);

            Assert.That(loaded, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(loaded!.Name, Is.EqualTo("QA Profile"));
                Assert.That(loaded.FilePath, Is.EqualTo(@"C:\Temp\Exports"));
                Assert.That(loaded.TemplateInfo.IsPDFChecked, Is.True);
                Assert.That(loaded.TemplateInfo.IsDWGChecked, Is.True);
                Assert.That(loaded.TemplateInfo.IsSeparateFile, Is.False);
                Assert.That(loaded.TemplateInfo.DWG_MergedViews, Is.True);
                Assert.That(loaded.TemplateInfo.PaperSize, Is.EqualTo("A1"));
            });
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public void ConvertXmlToProfile_MapsSelectedFormatsAndFlags()
    {
        var xmlProfile = new ExportPlusXMLProfile
        {
            Name = "Mapped",
            FilePath = @"D:\Exports",
            TemplateInfo = new TemplateInfo
            {
                IsSeparateFile = true,
                HideCropBoundaries = true,
                HideScopeBox = false,
                PaperSize = "A3",
                IsPDFChecked = true,
                IsNWCChecked = true,
                IsIMGChecked = true
            }
        };

        var profile = XMLProfileService.ConvertXMLToProfile(xmlProfile);

        Assert.Multiple(() =>
        {
            Assert.That(profile.ProfileName, Is.EqualTo("Mapped"));
            Assert.That(profile.OutputFolder, Is.EqualTo(@"D:\Exports"));
            Assert.That(profile.CreateSeparateFolders, Is.True);
            Assert.That(profile.HideCropRegions, Is.True);
            Assert.That(profile.HideScopeboxes, Is.False);
            Assert.That(profile.PaperSize, Is.EqualTo("A3"));
            Assert.That(profile.SelectedFormats, Is.EqualTo(new List<string> { "PDF", "JPG", "NWC" }));
        });
    }

    [Test]
    public void GetFormatSettings_ReturnsPdfSettings()
    {
        var xmlProfile = new ExportPlusXMLProfile
        {
            TemplateInfo = new TemplateInfo
            {
                IsVectorProcessing = false,
                RasterQuality = "Presentation",
                Color = "GrayScale",
                IsFitToPage = true,
                IsCenter = false,
                SelectedMarginType = "Custom"
            }
        };

        var settings = XMLProfileService.GetFormatSettings(xmlProfile, "PDF");

        Assert.Multiple(() =>
        {
            Assert.That(settings["VectorProcessing"], Is.EqualTo(false));
            Assert.That(settings["RasterQuality"], Is.EqualTo("Presentation"));
            Assert.That(settings["ColorMode"], Is.EqualTo("GrayScale"));
            Assert.That(settings["FitToPage"], Is.EqualTo(true));
            Assert.That(settings["IsCenter"], Is.EqualTo(false));
            Assert.That(settings["MarginType"], Is.EqualTo("Custom"));
        });
    }
}
