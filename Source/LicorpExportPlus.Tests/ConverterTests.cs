using System.Globalization;
using System.Windows.Data;
using LicorpExportPlus.Converters;
using LicorpExportPlus.Views;
using NUnit.Framework;

namespace LicorpExportPlus.Tests;

[TestFixture]
public class ConverterTests
{
    [Test]
    public void OneWayConverters_ConvertBack_ReturnBindingDoNothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SelectedCountConverter().ConvertBack("ignored", typeof(int), null, CultureInfo.InvariantCulture), Is.EqualTo(Binding.DoNothing));
            Assert.That(new FormatListConverter().ConvertBack("ignored", typeof(object), null, CultureInfo.InvariantCulture), Is.EqualTo(Binding.DoNothing));
            Assert.That(StringIsEmptyConverter.Instance.ConvertBack(false, typeof(string), null, CultureInfo.InvariantCulture), Is.EqualTo(Binding.DoNothing));
        });
    }
}
