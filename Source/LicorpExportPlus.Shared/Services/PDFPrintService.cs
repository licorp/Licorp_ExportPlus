using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services
{
    public class PDFPrintService
    {
        private readonly Document _document;

        public PDFPrintService(Document document)
        {
            _document = document;
        }

        public bool ExportSheetsWithPrintManager(List<SheetItem> sheetItems, string outputFolder, ExportSettings settings, Action<int, int, string, bool> progressCallback = null)
        {
            try
            {

                Directory.CreateDirectory(outputFolder);

                int successCount = 0;
                int failCount = 0;
                int total = sheetItems.Count;

                for (int i = 0; i < total; i++)
                {
                    var sheetItem = sheetItems[i];

                    try
                    {
                        progressCallback?.Invoke(i + 1, total, sheetItem.Number, false);

                        ViewSheet sheet = _document.GetElement(sheetItem.Id) as ViewSheet;
                        if (sheet == null)
                        {
                            failCount++;
                            continue;
                        }


                        string customFileName = sheetItem.CustomFileName;
                        if (string.IsNullOrEmpty(customFileName))
                        {
                            customFileName = $"{sheet.SheetNumber}_{sheet.Name}";
                        }

                        bool exportSuccess = ExportSingleSheetWithPrintManager(sheet, outputFolder, customFileName, settings);

                        if (exportSuccess)
                        {
                            successCount++;
                            progressCallback?.Invoke(i + 1, total, sheetItem.Number, true);
                        }
                        else
                        {
                            failCount++;
                            progressCallback?.Invoke(i + 1, total, sheetItem.Number, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        LicorpTrace.Error($"Legacy PDF export failed for {sheetItem.Number}", ex);
                        progressCallback?.Invoke(i + 1, total, sheetItem.Number, true);
                    }
                }

                return successCount > 0;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error("Legacy PDF export failed", ex);
                return false;
            }
        }

        private bool ExportSingleSheetWithPrintManager(ViewSheet sheet, string outputFolder, string fileName, ExportSettings settings)
        {
            PrintManager pm = _document.PrintManager;

            try
            {

                try
                {
                    using (Transaction trans = new Transaction(_document, "Configure Print ViewSet"))
                    {
                        trans.Start();

                        ViewSheetSetting vss = pm.ViewSheetSetting;

                        vss.SaveAs($"_TempPrintSet_{sheet.Id}");

                        ViewSet viewSet = vss.CurrentViewSheetSet.Views;
                        viewSet.Clear();
                        viewSet.Insert(sheet);

                        trans.Commit();
                    }

                }
                catch (Exception ex)
                {
                    pm.PrintRange = PrintRange.Current;
                    pm.PrintToFile = true;
                    pm.CombinedFile = true;
                    goto skip_viewset;
                }

                pm.PrintRange = PrintRange.Select;
                pm.PrintToFile = true;
                pm.CombinedFile = false;

                skip_viewset:


                try
                {
                    using (Transaction trans = new Transaction(_document, "Apply View Options"))
                    {
                        trans.Start();
                        PDFOptionsApplier.ApplyViewOptionsToSheetNoTransaction(_document, sheet, settings);
                        trans.Commit();
                    }
                }
                catch (Exception ex)
                {
                }

                try
                {
                    PDFOptionsApplier.ApplyPrintManagerSettings(pm, settings);
                }
                catch (Exception ex)
                {
                }

                try
                {
                    pm.Apply();
                }
                catch (Exception ex)
                {
                    return false;
                }

                string outputPath = Path.Combine(outputFolder, fileName + ".pdf");

                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch (Exception ex)
                    {
                    }
                }

                pm.PrintToFileName = outputPath;

                bool submitResult = pm.SubmitPrint();

                System.Threading.Thread.Sleep(1000);

                if (File.Exists(outputPath))
                {
                    FileInfo fi = new FileInfo(outputPath);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void WriteDebugLog(string message)
        {
            return;
        }
    }
}
