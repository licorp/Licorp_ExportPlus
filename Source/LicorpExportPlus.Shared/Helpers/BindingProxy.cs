using System.Windows;

namespace LicorpExportPlus.Helpers;

/// <summary>
/// Solves DataContext inheritance issues in nested controls (e.g., DataGrid columns).
/// Usage in XAML:
/// <![CDATA[
/// <DataGrid.Resources>
///     <helpers:BindingProxy x:Key="Proxy" Data="{Binding}" />
/// </DataGrid.Resources>
/// <DataGridCheckBoxColumn Binding="{Binding IsSelected}" 
///                         Visibility="{Binding Data.ShowCheckboxes, Source={StaticResource Proxy}}" />
/// ]]>
/// </summary>
public class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(
            nameof(Data),
            typeof(object),
            typeof(BindingProxy),
            new PropertyMetadata(null));

    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
