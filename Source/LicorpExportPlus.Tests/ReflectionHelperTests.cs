using LicorpExportPlus.Helpers;
using NUnit.Framework;

namespace LicorpExportPlus.Tests;

[TestFixture]
public class ReflectionHelperTests
{
    private enum TestMode
    {
        First,
        Second
    }

    private sealed class Target
    {
        public TestMode Mode { get; set; }
        public int Count { get; set; }
    }

    [Test]
    public void TrySetProperty_ConvertsStringToEnum()
    {
        var target = new Target();

        var result = ReflectionHelper.TrySetProperty(target, nameof(Target.Mode), "Second");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(target.Mode, Is.EqualTo(TestMode.Second));
        });
    }

    [Test]
    public void TrySetProperty_ConvertsNumericValue()
    {
        var target = new Target();

        var result = ReflectionHelper.TrySetProperty(target, nameof(Target.Count), "25");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(target.Count, Is.EqualTo(25));
        });
    }
}
