using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nice3point.Revit.Extensions;

namespace LicorpExportPlus.Utils
{
    /// <summary>
    /// ⚡ HIGH-PERFORMANCE BATCH LOADER for sheets/views
    /// Based on RevitScheduleEditor pattern: IExternalEventHandler + Incremental Loading
    /// </summary>
    public class SheetBatchLoader : IExternalEventHandler
    {
        private Queue<BatchLoadRequest> _requestQueue = new Queue<BatchLoadRequest>();
        private readonly object _lockObject = new object();

        public event EventHandler<BatchLoadCompletedEventArgs> BatchLoadCompleted;

        // Configuration
        private const int INITIAL_BATCH_SIZE = 50; // Show first 50 immediately
        private const int BACKGROUND_BATCH_SIZE = 100; // Then load 100 at a time

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;

                lock (_lockObject)
                {
                    while (_requestQueue.Count > 0)
                    {
                        var request = _requestQueue.Dequeue();

                        switch (request.RequestType)
                        {
                            case BatchLoadRequestType.GetAllSheetIds:
                                ProcessGetAllSheetIds(doc, request);
                                break;

                            case BatchLoadRequestType.LoadSheetBatch:
                                ProcessLoadSheetBatch(doc, request);
                                break;

                            case BatchLoadRequestType.GetAllViewIds:
                                ProcessGetAllViewIds(doc, request);
                                break;

                            case BatchLoadRequestType.LoadViewBatch:
                                ProcessLoadViewBatch(doc, request);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"[SheetBatchLoader] ERROR: {ex.Message}");
            }
        }

        private void ProcessGetAllSheetIds(Document doc, BatchLoadRequest request)
        {
            var sw = Stopwatch.StartNew();

            // ⚡ FAST: Only get ElementIds first
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .WhereElementIsNotElementType()
                .ToElementIds()
                .ToList();

            sw.Stop();
            Licorp.Diagnostics.LicorpTrace.Dbg($"[SheetBatchLoader] Got {collector.Count} sheet IDs in {sw.ElapsedMilliseconds}ms");

            // Preload TitleBlock sizes ONCE for all sheets
            SheetSizeDetector.PreloadTitleBlockSizes(doc);

            var result = new BatchLoadResult
            {
                RequestType = request.RequestType,
                ElementIds = collector,
                ProcessingTime = sw.Elapsed
            };

            BatchLoadCompleted?.Invoke(this, new BatchLoadCompletedEventArgs(result));
        }

        private void ProcessLoadSheetBatch(Document doc, BatchLoadRequest request)
        {
            var sw = Stopwatch.StartNew();
            var sheetDataList = new List<SheetData>();

            foreach (var elementId in request.ElementIds)
            {
                try
                {
                    var sheet = doc.GetElement(elementId) as ViewSheet;
                    if (sheet == null || sheet.IsTemplate) continue;

                    // Extract all data from Revit API in main thread
                    var data = new SheetData
                    {
                        ElementId = sheet.Id,
                        SheetNumber = sheet.SheetNumber ?? "NO_NUMBER",
                        SheetName = sheet.Name ?? "NO_NAME",
                        Revision = GetRevision(sheet),
                        SheetSize = SheetSizeDetector.GetSheetSize(sheet) // Uses preloaded cache
                    };

                    sheetDataList.Add(data);
                }
                catch (Exception ex)
                {
                    Licorp.Diagnostics.LicorpTrace.Dbg($"[SheetBatchLoader] Error loading sheet {elementId}: {ex.Message}");
                }
            }

            sw.Stop();
            Licorp.Diagnostics.LicorpTrace.Dbg($"[SheetBatchLoader] Loaded batch {request.BatchIndex}: {sheetDataList.Count} sheets in {sw.ElapsedMilliseconds}ms");

            var result = new BatchLoadResult
            {
                RequestType = request.RequestType,
                BatchIndex = request.BatchIndex,
                SheetDataList = sheetDataList,
                ProcessingTime = sw.Elapsed
            };

            BatchLoadCompleted?.Invoke(this, new BatchLoadCompletedEventArgs(result));
        }

        private void ProcessGetAllViewIds(Document doc, BatchLoadRequest request)
        {
            var sw = Stopwatch.StartNew();

var collector = new FilteredElementCollector(doc)
			.OfClass(typeof(Autodesk.Revit.DB.View))
			.WhereElementIsNotElementType()
                .ToElementIds()
                .ToList();

            sw.Stop();
            Licorp.Diagnostics.LicorpTrace.Dbg($"[SheetBatchLoader] Got {collector.Count} view IDs in {sw.ElapsedMilliseconds}ms");

            var result = new BatchLoadResult
            {
                RequestType = request.RequestType,
                ElementIds = collector,
                ProcessingTime = sw.Elapsed
            };

            BatchLoadCompleted?.Invoke(this, new BatchLoadCompletedEventArgs(result));
        }

        private void ProcessLoadViewBatch(Document doc, BatchLoadRequest request)
        {
            var sw = Stopwatch.StartNew();
            var viewDataList = new List<ViewData>();

            foreach (var elementId in request.ElementIds)
            {
                try
                {
                    var view = doc.GetElement(elementId) as Autodesk.Revit.DB.View;
                    if (view == null || view.IsTemplate || view.ViewType == ViewType.DrawingSheet) continue;

                    // Filter valid view types
                    if (!IsValidViewType(view.ViewType)) continue;

                    var data = new ViewData
                    {
                        ElementId = view.Id,
                        ViewName = view.Name ?? "NO_name",
                        ViewType = view.ViewType.ToString(),
                        ViewScale = GetViewScale(view)
                    };

                    viewDataList.Add(data);
                }
                catch (Exception ex)
                {
                    Licorp.Diagnostics.LicorpTrace.Dbg($"[SheetBatchLoader] Error loading view {elementId}: {ex.Message}");
                }
            }

            sw.Stop();
            Licorp.Diagnostics.LicorpTrace.Dbg($"[SheetBatchLoader] Loaded batch {request.BatchIndex}: {viewDataList.Count} views in {sw.ElapsedMilliseconds}ms");

            var result = new BatchLoadResult
            {
                RequestType = request.RequestType,
                BatchIndex = request.BatchIndex,
                ViewDataList = viewDataList,
                ProcessingTime = sw.Elapsed
            };

            BatchLoadCompleted?.Invoke(this, new BatchLoadCompletedEventArgs(result));
        }

        private bool IsValidViewType(ViewType viewType)
        {
            return viewType == ViewType.FloorPlan ||
                   viewType == ViewType.CeilingPlan ||
                   viewType == ViewType.Elevation ||
                   viewType == ViewType.ThreeD ||
                   viewType == ViewType.Section ||
                   viewType == ViewType.Detail ||
                   viewType == ViewType.DraftingView ||
                   viewType == ViewType.EngineeringPlan;
        }

        private string GetRevision(ViewSheet sheet)
        {
            try
            {
                Parameter revParam = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                return revParam?.AsString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string GetViewScale(Autodesk.Revit.DB.View view)
        {
            try
            {
                return $"1:{view.Scale}";
            }
            catch
            {
                return "NTS";
            }
        }

        public void QueueRequest(BatchLoadRequest request)
        {
            lock (_lockObject)
            {
                _requestQueue.Enqueue(request);
            }
        }

        public string GetName()
        {
            return "ExportPlus_SheetBatchLoader";
        }
    }

    // ==================== DATA CLASSES ====================

    public enum BatchLoadRequestType
    {
        GetAllSheetIds,
        LoadSheetBatch,
        GetAllViewIds,
        LoadViewBatch
    }

    public class BatchLoadRequest
    {
        public BatchLoadRequestType RequestType { get; set; }
        public List<ElementId> ElementIds { get; set; }
        public int BatchIndex { get; set; }
    }

    public class BatchLoadResult
    {
        public BatchLoadRequestType RequestType { get; set; }
        public List<ElementId> ElementIds { get; set; }
        public List<SheetData> SheetDataList { get; set; }
        public List<ViewData> ViewDataList { get; set; }
        public int BatchIndex { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class BatchLoadCompletedEventArgs : EventArgs
    {
        public BatchLoadResult Result { get; }

        public BatchLoadCompletedEventArgs(BatchLoadResult result)
        {
            Result = result;
        }
    }

    public class SheetData
    {
        public ElementId ElementId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public string Revision { get; set; }
        public string SheetSize { get; set; }
    }

    public class ViewData
    {
        public ElementId ElementId { get; set; }
        public string ViewName { get; set; }
        public string ViewType { get; set; }
        public string ViewScale { get; set; }
    }
}
