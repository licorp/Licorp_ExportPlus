using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace LicorpExportPlus.Utils
{
    /// <summary>
    /// ⚡ ASYNC MANAGER for loading sheets/views without blocking Revit UI
    /// Based on RevitScheduleEditor incremental loading pattern
    /// </summary>
    public class AsyncSheetViewManager : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private readonly SheetBatchLoader _batchLoader;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;

        // Configuration
        private const int INITIAL_BATCH_SIZE = 50;
        private const int BACKGROUND_BATCH_SIZE = 100;

        // Data
        private List<ElementId> _allElementIds;
        private int _totalElements;
        private int _loadedElements;
        private bool _isLoading;
        private string _loadingStatus;
        private double _loadingProgress;

        // Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<SheetBatchEventArgs> SheetBatchLoaded;
        public event EventHandler<ViewBatchEventArgs> ViewBatchLoaded;
        public event EventHandler LoadingCompleted;

        // Properties for binding
        public int TotalElements
        {
            get => _totalElements;
            private set
            {
                _totalElements = value;
                OnPropertyChanged(nameof(TotalElements));
            }
        }

        public int LoadedElements
        {
            get => _loadedElements;
            private set
            {
                _loadedElements = value;
                OnPropertyChanged(nameof(LoadedElements));
                LoadingProgress = TotalElements > 0 ? (double)LoadedElements / TotalElements * 100.0 : 0;
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public string LoadingStatus
        {
            get => _loadingStatus;
            private set
            {
                _loadingStatus = value;
                OnPropertyChanged(nameof(LoadingStatus));
            }
        }

        public double LoadingProgress
        {
            get => _loadingProgress;
            private set
            {
                _loadingProgress = value;
                OnPropertyChanged(nameof(LoadingProgress));
            }
        }

        public AsyncSheetViewManager(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _dispatcher = Dispatcher.CurrentDispatcher;

            // Create ExternalEvent handler
            _batchLoader = new SheetBatchLoader();
            _batchLoader.BatchLoadCompleted += OnBatchLoadCompleted;
            _externalEvent = ExternalEvent.Create(_batchLoader);

            Debug.WriteLine("[AsyncSheetViewManager] Initialized");
        }

        /// <summary>
        /// ⚡ Load sheets asynchronously with incremental batches
        /// </summary>
        public void LoadSheetsAsync()
        {
            if (IsLoading)
            {
                Debug.WriteLine("[AsyncSheetViewManager] Already loading, ignoring request");
                return;
            }

            Debug.WriteLine("[AsyncSheetViewManager] === Starting async sheet load ===");

            try
            {
                IsLoading = true;
                _allElementIds?.Clear();
                TotalElements = 0;
                LoadedElements = 0;
                LoadingStatus = "Getting sheet list...";

                // Step 1: Get all ElementIds (fast)
                var request = new BatchLoadRequest
                {
                    RequestType = BatchLoadRequestType.GetAllSheetIds
                };

                _batchLoader.QueueRequest(request);
                _externalEvent.Raise();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AsyncSheetViewManager] ERROR: {ex.Message}");
                IsLoading = false;
                LoadingStatus = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// ⚡ Load views asynchronously with incremental batches
        /// </summary>
        public void LoadViewsAsync()
        {
            if (IsLoading)
            {
                Debug.WriteLine("[AsyncSheetViewManager] Already loading, ignoring request");
                return;
            }

            Debug.WriteLine("[AsyncSheetViewManager] === Starting async view load ===");

            try
            {
                IsLoading = true;
                _allElementIds?.Clear();
                TotalElements = 0;
                LoadedElements = 0;
                LoadingStatus = "Getting view list...";

                // Step 1: Get all ElementIds (fast)
                var request = new BatchLoadRequest
                {
                    RequestType = BatchLoadRequestType.GetAllViewIds
                };

                _batchLoader.QueueRequest(request);
                _externalEvent.Raise();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AsyncSheetViewManager] ERROR: {ex.Message}");
                IsLoading = false;
                LoadingStatus = $"Error: {ex.Message}";
            }
        }

        private void OnBatchLoadCompleted(object sender, BatchLoadCompletedEventArgs e)
        {
            // Ensure UI updates on UI thread
            _dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var result = e.Result;
                    Debug.WriteLine($"[AsyncSheetViewManager] Batch completed: Type={result.RequestType}, Time={result.ProcessingTime.TotalMilliseconds}ms");

                    switch (result.RequestType)
                    {
                        case BatchLoadRequestType.GetAllSheetIds:
                            HandleSheetIdsLoaded(result);
                            break;

                        case BatchLoadRequestType.LoadSheetBatch:
                            HandleSheetBatchLoaded(result);
                            break;

                        case BatchLoadRequestType.GetAllViewIds:
                            HandleViewIdsLoaded(result);
                            break;

                        case BatchLoadRequestType.LoadViewBatch:
                            HandleViewBatchLoaded(result);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AsyncSheetViewManager] ERROR in callback: {ex.Message}");
                    IsLoading = false;
                    LoadingStatus = $"Error: {ex.Message}";
                }
            }));
        }

        private void HandleSheetIdsLoaded(BatchLoadResult result)
        {
            _allElementIds = result.ElementIds;
            TotalElements = _allElementIds.Count;

            Debug.WriteLine($"[AsyncSheetViewManager] Sheet IDs loaded: {TotalElements} sheets");
            LoadingStatus = $"Found {TotalElements} sheets. Loading data...";

            if (TotalElements == 0)
            {
                IsLoading = false;
                LoadingStatus = "No sheets found";
                LoadingCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Load first batch immediately
            LoadNextSheetBatch(0, INITIAL_BATCH_SIZE);
        }

        private void HandleSheetBatchLoaded(BatchLoadResult result)
        {
            var sheetDataList = result.SheetDataList;
            LoadedElements += sheetDataList.Count;

            Debug.WriteLine($"[AsyncSheetViewManager] Sheet batch {result.BatchIndex + 1} loaded: {sheetDataList.Count} sheets");
            LoadingStatus = $"Loading sheet {LoadedElements} of {TotalElements}...";

            // Notify subscribers
            SheetBatchLoaded?.Invoke(this, new SheetBatchEventArgs(sheetDataList, LoadedElements, TotalElements));

            // Check if done
            if (LoadedElements >= TotalElements)
            {
                Debug.WriteLine("[AsyncSheetViewManager] All sheets loaded");
                IsLoading = false;
                LoadingStatus = $"Completed! Loaded {LoadedElements} sheets";
                LoadingCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Load next batch
                int nextBatchStart = LoadedElements;
                LoadNextSheetBatch(nextBatchStart, BACKGROUND_BATCH_SIZE);
            }
        }

        private void HandleViewIdsLoaded(BatchLoadResult result)
        {
            _allElementIds = result.ElementIds;
            TotalElements = _allElementIds.Count;

            Debug.WriteLine($"[AsyncSheetViewManager] View IDs loaded: {TotalElements} views");
            LoadingStatus = $"Found {TotalElements} views. Loading data...";

            if (TotalElements == 0)
            {
                IsLoading = false;
                LoadingStatus = "No views found";
                LoadingCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Load first batch immediately
            LoadNextViewBatch(0, INITIAL_BATCH_SIZE);
        }

        private void HandleViewBatchLoaded(BatchLoadResult result)
        {
            var viewDataList = result.ViewDataList;
            LoadedElements += viewDataList.Count;

            Debug.WriteLine($"[AsyncSheetViewManager] View batch {result.BatchIndex + 1} loaded: {viewDataList.Count} views");
            LoadingStatus = $"Loading view {LoadedElements} of {TotalElements}...";

            // Notify subscribers
            ViewBatchLoaded?.Invoke(this, new ViewBatchEventArgs(viewDataList, LoadedElements, TotalElements));

            // Check if done
            if (LoadedElements >= TotalElements)
            {
                Debug.WriteLine("[AsyncSheetViewManager] All views loaded");
                IsLoading = false;
                LoadingStatus = $"Completed! Loaded {LoadedElements} views";
                LoadingCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Load next batch
                int nextBatchStart = LoadedElements;
                LoadNextViewBatch(nextBatchStart, BACKGROUND_BATCH_SIZE);
            }
        }

        private void LoadNextSheetBatch(int startIndex, int batchSize)
        {
            if (startIndex >= _allElementIds.Count) return;

            int actualBatchSize = Math.Min(batchSize, _allElementIds.Count - startIndex);
            var batchElementIds = _allElementIds.Skip(startIndex).Take(actualBatchSize).ToList();
            int batchIndex = startIndex / batchSize;

            Debug.WriteLine($"[AsyncSheetViewManager] Loading sheet batch {batchIndex + 1}: elements {startIndex + 1}-{startIndex + actualBatchSize}");

            var request = new BatchLoadRequest
            {
                RequestType = BatchLoadRequestType.LoadSheetBatch,
                ElementIds = batchElementIds,
                BatchIndex = batchIndex
            };

            _batchLoader.QueueRequest(request);
            _externalEvent.Raise();
        }

        private void LoadNextViewBatch(int startIndex, int batchSize)
        {
            if (startIndex >= _allElementIds.Count) return;

            int actualBatchSize = Math.Min(batchSize, _allElementIds.Count - startIndex);
            var batchElementIds = _allElementIds.Skip(startIndex).Take(actualBatchSize).ToList();
            int batchIndex = startIndex / batchSize;

            Debug.WriteLine($"[AsyncSheetViewManager] Loading view batch {batchIndex + 1}: elements {startIndex + 1}-{startIndex + actualBatchSize}");

            var request = new BatchLoadRequest
            {
                RequestType = BatchLoadRequestType.LoadViewBatch,
                ElementIds = batchElementIds,
                BatchIndex = batchIndex
            };

            _batchLoader.QueueRequest(request);
            _externalEvent.Raise();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ==================== EVENT ARGS ====================

    public class SheetBatchEventArgs : EventArgs
    {
        public List<SheetData> SheetDataList { get; }
        public int LoadedCount { get; }
        public int TotalCount { get; }

        public SheetBatchEventArgs(List<SheetData> sheetDataList, int loadedCount, int totalCount)
        {
            SheetDataList = sheetDataList;
            LoadedCount = loadedCount;
            TotalCount = totalCount;
        }
    }

    public class ViewBatchEventArgs : EventArgs
    {
        public List<ViewData> ViewDataList { get; }
        public int LoadedCount { get; }
        public int TotalCount { get; }

        public ViewBatchEventArgs(List<ViewData> viewDataList, int loadedCount, int totalCount)
        {
            ViewDataList = viewDataList;
            LoadedCount = loadedCount;
            TotalCount = totalCount;
        }
    }
}
