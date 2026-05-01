using System;
using LicorpExportPlus.Models;
using LicorpExportPlus.Utils;
using NUnit.Framework;

namespace LicorpExportPlus.Tests;

[TestFixture]
public class FileNameGeneratorTests
{
    [Test]
    public void SanitizeFileName_ReplacesInvalidCharactersAndCollapsesUnderscores()
    {
        var sanitized = FileNameGenerator.SanitizeFileName("A:101 / Test,,Name;;");

        Assert.That(sanitized, Is.EqualTo("A_101_Test_Name"));
    }

    [Test]
    public void SanitizeFileName_TrimsLeadingAndTrailingUnderscores()
    {
        var sanitized = FileNameGenerator.SanitizeFileName("__Sheet Name__");

        Assert.That(sanitized, Is.EqualTo("Sheet_Name"));
    }

    [Test]
    public void ResolveEnvironmentVariables_ReplacesProSheetsStyleDateAndDrawingTokens()
    {
        var resolved = FileNameGenerator.ResolveEnvironmentVariables(
            "%DrawingName%_%YYYY%-%mm%-%dd%_%HH%-%MM%-%SS%",
            "A101",
            new DateTime(2026, 4, 30, 8, 9, 10));

        Assert.That(resolved, Is.EqualTo("A101_2026-04-30_08-09-10"));
    }

    [Test]
    public void ResolveEnvironmentVariables_ReplacesShortDateTimeTokens()
    {
        var resolved = FileNameGenerator.ResolveEnvironmentVariables(
            "%DrawingName%_%Y%-%m%-%d%_%H%-%M%-%S%",
            "A101",
            new DateTime(2026, 4, 3, 8, 9, 10));

        Assert.That(resolved, Is.EqualTo("A101_2026-4-3_8-9-10"));
    }

    [Test]
    public void BuildNameFromParameters_UsesStaticTextPrefixSuffixAndSeparators()
    {
        var parameters = new[]
        {
            new SelectedParameterInfo { ParameterName = "Sheet Number", SampleValue = "A101", Separator = "-" },
            new SelectedParameterInfo { ParameterName = "Static Text", SampleValue = "FOR_ISSUE", IsStaticText = true, Separator = "-" },
            new SelectedParameterInfo { ParameterName = "Revision", SampleValue = "01", Prefix = "R", Separator = "" }
        };

        var name = FileNameGenerator.BuildNameFromParameters(
            parameters,
            parameter => parameter == "Sheet Number" ? "A101" : parameter == "Revision" ? "01" : "",
            sanitize: true);

        Assert.That(name, Is.EqualTo("A101-FOR_ISSUE-R01"));
    }
}
