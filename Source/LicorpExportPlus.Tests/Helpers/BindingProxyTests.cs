using System.Windows;
using LicorpExportPlus.Helpers;
using NUnit.Framework;

namespace LicorpExportPlus.Tests.Helpers;

[TestFixture]
public class BindingProxyTests
{
    [Test]
    public void BindingProxy_CreateInstance_ReturnsNewInstance()
    {
        var proxy = new BindingProxy();
        Assert.That(proxy, Is.Not.Null);
    }

    [Test]
    public void BindingProxy_DataProperty_CanBeSet()
    {
        var proxy = new BindingProxy();
        var testData = "Test Value";
        
        proxy.Data = testData;
        
        Assert.That(proxy.Data, Is.EqualTo(testData));
    }

    [Test]
    public void BindingProxy_DataProperty_CanBeNull()
    {
        var proxy = new BindingProxy();
        proxy.Data = null;
        Assert.That(proxy.Data, Is.Null);
    }

    [Test]
    public void BindingProxy_CreateInstanceCore_ReturnsNewBindingProxy()
    {
        var proxy = new BindingProxy();
        var newInstance = proxy.CreateInstanceCore();
        
        Assert.That(newInstance, Is.InstanceOf<BindingProxy>());
        Assert.That(newInstance, Is.Not.SameAs(proxy));
    }
}
