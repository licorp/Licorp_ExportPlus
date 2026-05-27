using System.Collections.Generic;
using LicorpExportPlus.Models;
using LicorpExportPlus.ViewModels;
using NUnit.Framework;

namespace LicorpExportPlus.Tests.ViewModels;

[TestFixture]
public class FormatsViewModelTests
{
    private FormatsViewModel _viewModel;

    [SetUp]
    public void Setup()
    {
        _viewModel = new FormatsViewModel();
    }

    [Test]
    public void GetSelectedFormats_DefaultState_ReturnsDwgOnly()
    {
        var formats = _viewModel.GetSelectedFormats();
        Assert.That(formats, Is.EquivalentTo(new[] { "DWG" }));
    }

    [Test]
    public void GetSelectedFormats_AllSelected_ReturnsAllFormats()
    {
        _viewModel.IsPdfSelected = true;
        _viewModel.IsDwgSelected = true;
        _viewModel.IsIfcSelected = true;
        _viewModel.IsNwcSelected = true;
        _viewModel.IsImgSelected = true;

        var formats = _viewModel.GetSelectedFormats();
        Assert.That(formats, Is.EquivalentTo(new[] { "PDF", "DWG", "IFC", "NWC", "IMG" }));
    }

    [Test]
    public void GetSelectedFormats_NoneSelected_ReturnsEmpty()
    {
        _viewModel.IsDwgSelected = false;

        var formats = _viewModel.GetSelectedFormats();
        Assert.That(formats, Is.Empty);
    }

    [Test]
    public void HasSelectedFormat_DefaultState_ReturnsTrue()
    {
        Assert.That(_viewModel.HasSelectedFormat, Is.True);
    }

    [Test]
    public void HasSelectedFormat_NoneSelected_ReturnsFalse()
    {
        _viewModel.IsDwgSelected = false;
        Assert.That(_viewModel.HasSelectedFormat, Is.False);
    }

    [Test]
    public void SelectAllFormats_SetsAllFormatsSelected()
    {
        _viewModel.ClearAllFormatsCommand.Execute(null);
        _viewModel.SelectAllFormatsCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.IsPdfSelected, Is.True);
            Assert.That(_viewModel.IsDwgSelected, Is.True);
            Assert.That(_viewModel.IsIfcSelected, Is.True);
            Assert.That(_viewModel.IsNwcSelected, Is.True);
            Assert.That(_viewModel.IsImgSelected, Is.True);
        });
    }

    [Test]
    public void ClearAllFormats_ClearsAllFormats()
    {
        _viewModel.SelectAllFormatsCommand.Execute(null);
        _viewModel.ClearAllFormatsCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.IsPdfSelected, Is.False);
            Assert.That(_viewModel.IsDwgSelected, Is.False);
            Assert.That(_viewModel.IsIfcSelected, Is.False);
            Assert.That(_viewModel.IsNwcSelected, Is.False);
            Assert.That(_viewModel.IsImgSelected, Is.False);
        });
    }

    [Test]
    public void GetExportSettings_DefaultPdfSettings_ReturnsCorrectValues()
    {
        var settings = _viewModel.GetExportSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.Colors, Is.EqualTo(PSColors.Color));
            Assert.That(settings.RasterQuality, Is.EqualTo(PSRasterQuality.High));
            Assert.That(settings.CombineFiles, Is.False);
            Assert.That(settings.SkipEmptySheets, Is.False);
            Assert.That(settings.HideCropBoundaries, Is.True);
            Assert.That(settings.HideScopeBoxes, Is.True);
        });
    }

    [Test]
    public void GetExportSettings_GrayscalePdf_ReturnsGrayscale()
    {
        _viewModel.PdfColorMode = "Grayscale";
        var settings = _viewModel.GetExportSettings();
        Assert.That(settings.Colors, Is.EqualTo(PSColors.Grayscale));
    }

    [Test]
    public void GetExportSettings_DwgSettings_ReturnsCorrectValues()
    {
        _viewModel.DwgVersion = "2013";
        _viewModel.DwgCompactFiles = false;

        var settings = _viewModel.GetExportSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.DWGVersion, Is.EqualTo("2013"));
            Assert.That(settings.CompactDwgFiles, Is.False);
        });
    }
}
