using System.Collections.Generic;
using Autodesk.Revit.DB;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services.Interfaces;

namespace LicorpExportPlus.Services;

public class DrawingTransmittalServiceAdapter : IDrawingTransmittalService
{
    private readonly DrawingTransmittalService _service = new();

    public void CreateTransmittal(List<ViewSheet> sheets, TransmittalSettings settings)
    {
        _service.CreateTransmittal(sheets, settings);
    }
}
