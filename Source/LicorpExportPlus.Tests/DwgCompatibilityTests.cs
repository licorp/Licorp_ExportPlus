using LicorpExportPlus.Helpers;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services;
using NUnit.Framework;

namespace LicorpExportPlus.Tests;

[TestFixture]
public class DwgCompatibilityTests
{
    [Test]
    public void ReflectionHelper_CanSetAndReadMergedViewsOnDwgOptions()
    {
        var options = new DwgOptionsStub();

        var set = ReflectionHelper.TrySetProperty(options, "MergedViews", true);
        var read = ReflectionHelper.TryGetProperty<bool>(options, "MergedViews", out var mergedViews);

        Assert.Multiple(() =>
        {
            Assert.That(set, Is.True);
            Assert.That(read, Is.True);
            Assert.That(mergedViews, Is.True);
        });
    }

    [Test]
    public void DwgExportSettings_ClonePreservesCompactMode()
    {
        var settings = new DWGExportSettings
        {
            CompactDwgFiles = false,
            UseSharedCoords = true,
            FileVersion = "2013"
        };

        var clone = settings.Clone();

        Assert.Multiple(() =>
        {
            Assert.That(clone.CompactDwgFiles, Is.False);
            Assert.That(clone.UseSharedCoords, Is.True);
            Assert.That(clone.FileVersion, Is.EqualTo("2013"));
        });
    }

    [Test]
    public void NwcGuard_OnlyTreats3DViewsAsExportable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(NWCExportService.Is3DViewItem(new ViewItem { ViewType = "3D" }), Is.True);
            Assert.That(NWCExportService.Is3DViewItem(new ViewItem { ViewType = "ThreeD" }), Is.True);
            Assert.That(NWCExportService.Is3DViewItem(new ViewItem { ViewType = "Floor Plan" }), Is.False);
            Assert.That(NWCExportService.Is3DViewItem(new ViewItem { ViewType = null }), Is.False);
        });
    }

    private class DwgOptionsStub
    {
        public bool MergedViews { get; set; }
    }
}
