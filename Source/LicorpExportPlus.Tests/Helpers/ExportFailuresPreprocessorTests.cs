using LicorpExportPlus.Helpers;
using NUnit.Framework;

namespace LicorpExportPlus.Tests.Helpers;

[TestFixture]
public class ExportFailuresPreprocessorTests
{
    [Test]
    public void Constructor_InitializesEmptyLists()
    {
        var preprocessor = new ExportFailuresPreprocessor();
        
        Assert.Multiple(() =>
        {
            Assert.That(preprocessor.FailureMessages, Is.Empty);
            Assert.That(preprocessor.WarningMessages, Is.Empty);
            Assert.That(preprocessor.HasFailures, Is.False);
            Assert.That(preprocessor.HasWarnings, Is.False);
        });
    }

    [Test]
    public void Clear_EmptyLists_DoesNotThrow()
    {
        var preprocessor = new ExportFailuresPreprocessor();
        Assert.DoesNotThrow(() => preprocessor.Clear());
    }

    [Test]
    public void GetSummary_NoIssues_ReturnsNoIssues()
    {
        var preprocessor = new ExportFailuresPreprocessor();
        var summary = preprocessor.GetSummary();
        Assert.That(summary, Is.EqualTo("No issues"));
    }

    [Test]
    public void FailureMessages_CanAddMessages()
    {
        var preprocessor = new ExportFailuresPreprocessor();
        preprocessor.FailureMessages.Add("Test error");
        
        Assert.Multiple(() =>
        {
            Assert.That(preprocessor.FailureMessages, Has.Count.EqualTo(1));
            Assert.That(preprocessor.HasFailures, Is.True);
        });
    }

    [Test]
    public void WarningMessages_CanAddMessages()
    {
        var preprocessor = new ExportFailuresPreprocessor();
        preprocessor.WarningMessages.Add("Test warning");
        
        Assert.Multiple(() =>
        {
            Assert.That(preprocessor.WarningMessages, Has.Count.EqualTo(1));
            Assert.That(preprocessor.HasWarnings, Is.True);
        });
    }

    [Test]
    public void GetSummary_WithFailures_ReturnsErrorCount()
    {
        var preprocessor = new ExportFailuresPreprocessor();
        preprocessor.FailureMessages.Add("Error 1");
        preprocessor.FailureMessages.Add("Error 2");
        
        var summary = preprocessor.GetSummary();
        Assert.That(summary, Does.Contain("2 error(s)"));
    }

    [Test]
    public void GetSummary_WithWarnings_ReturnsWarningCount()
    {
        var preprocessor = new ExportFailuresPreprocessor();
        preprocessor.WarningMessages.Add("Warning 1");
        
        var summary = preprocessor.GetSummary();
        Assert.That(summary, Does.Contain("1 warning(s)"));
    }

    [Test]
    public void GetSummary_WithBoth_ReturnsBothCounts()
    {
        var preprocessor = new ExportFailuresPreprocessor();
        preprocessor.FailureMessages.Add("Error 1");
        preprocessor.WarningMessages.Add("Warning 1");
        preprocessor.WarningMessages.Add("Warning 2");
        
        var summary = preprocessor.GetSummary();
        Assert.Multiple(() =>
        {
            Assert.That(summary, Does.Contain("1 error(s)"));
            Assert.That(summary, Does.Contain("2 warning(s)"));
        });
    }
}
