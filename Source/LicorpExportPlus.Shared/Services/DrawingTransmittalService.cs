using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services
{
    public class DrawingTransmittalService
    {
        public void CreateTransmittal(List<ViewSheet> sheets, TransmittalSettings settings)
        {
            try
            {
                var transmittal = new DrawingTransmittal
                {
                    Id = Guid.NewGuid().ToString(),
                    ProjectName = settings.ProjectName,
                    ProjectNumber = settings.ProjectNumber,
                    TransmittalNumber = settings.TransmittalNumber,
                    DateIssued = DateTime.Now,
                    IssuedBy = settings.IssuedBy,
                    IssuedTo = settings.IssuedTo,
                    Purpose = settings.Purpose,
                    Sheets = sheets.Select(s => new TransmittalSheet
                    {
                        SheetNumber = s.SheetNumber,
                        SheetName = s.Name,
                        Revision = GetSheetRevision(s),
                        DateIssued = DateTime.Now
                    }).ToList()
                };

                GenerateTransmittalDocument(transmittal, settings.OutputPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi tạo transmittal: {ex.Message}", ex);
            }
        }

        private string GetSheetRevision(ViewSheet sheet)
        {
            try
            {
                var revisions = sheet.GetAllRevisionIds();
                if (revisions.Any())
                {
                    var latestRevisionId = revisions.Last();
                    var revision = sheet.Document.GetElement(latestRevisionId) as Revision;
                    return revision?.SequenceNumber.ToString() ?? "0";
                }
                return "0";
            }
            catch
            {
                return "0";
            }
        }

        private void GenerateTransmittalDocument(DrawingTransmittal transmittal, string outputPath)
        {
            var content = GenerateTransmittalContent(transmittal);
            var fileName = $"Transmittal_{transmittal.TransmittalNumber}_{DateTime.Now:yyyyMMdd}.txt";
            var filePath = Path.Combine(outputPath, fileName);

            File.WriteAllText(filePath, content);
        }

        private string GenerateTransmittalContent(DrawingTransmittal transmittal)
        {
            var content = $@"
DRAWING TRANSMITTAL
==================

Project: {transmittal.ProjectName}
Project Number: {transmittal.ProjectNumber}
Transmittal Number: {transmittal.TransmittalNumber}
Date: {transmittal.DateIssued:dd/MM/yyyy}
From: {transmittal.IssuedBy}
To: {transmittal.IssuedTo}
Purpose: {transmittal.Purpose}

DRAWING LIST:
=============
";

            foreach (var sheet in transmittal.Sheets)
            {
                content += $"{sheet.SheetNumber} - {sheet.SheetName} (Rev. {sheet.Revision})\n";
            }

            content += $"\nTotal Drawings: {transmittal.Sheets.Count}\n";
            content += $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n";

            return content;
        }
    }

    public class TransmittalSettings
    {
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string TransmittalNumber { get; set; }
        public string IssuedBy { get; set; }
        public string IssuedTo { get; set; }
        public string Purpose { get; set; }
        public string OutputPath { get; set; }
    }
}
