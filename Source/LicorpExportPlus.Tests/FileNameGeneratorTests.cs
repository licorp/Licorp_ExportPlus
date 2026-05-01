using System;
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
}
