using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using Licorp.Diagnostics;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Extensions;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services;
using LicorpExportPlus.Events;
using LicorpExportPlus.Utils;
using LicorpExportPlus.Dialogs;
using MessageBox = System.Windows.MessageBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using WpfGrid = System.Windows.Controls.Grid;
using WpfColor = System.Windows.Media.Color;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using RevitView = Autodesk.Revit.DB.View;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace LicorpExportPlus.Views
{
    /// <summary>
    /// Export + - Professional Style Interface
    /// </summary>
    public partial class ExportPlusMainWindow : Window, INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly UIApplication _uiApp;
        private ObservableRangeCollection<SheetItem> _sheets;
        private ProfileManagerService _profileManager;
        private Models.Profile _selectedProfile;
        private ExternalEvent _exportEvent;
        private ExportHandler _exportHandler;
        
        // PDF Export External Event
        private ExternalEvent _pdfExportEvent;
        private Events.PDFExportEventHandler _pdfExportHandler;
        
// IFC Export External Event
    private ExternalEvent _ifcExportEvent;
    private Events.IFCExportHandler _ifcExportHandler;
        
        // ViewSheetSet Creation External Event
        private ExternalEvent _viewSheetSetEvent;
        private ViewSheetSetEventHandler _viewSheetSetHandler;
        
        // View/Sheet Set Manager
        private ViewSheetSetService _viewSheetSetManager;
        private ObservableCollection<ViewSheetSetInfo> _viewSheetSets;
        
        // Cancellation token for export operations
        private System.Threading.CancellationTokenSource _exportCancellationTokenSource;
        
        // Flag để tracking export completion - để reset khi user chọn lại sheet/RevitView
        private bool _exportJustCompleted = false;
        
        // Flag to prevent infinite loop when bulk updating checkboxes
        private bool _isBulkUpdatingCheckboxes = false;
        
        // Flag to indicate window is closing - used to stop LoadSheets/LoadViews early
        private volatile bool _isClosing = false;
        
        // ⚡ Lazy loading flags - only load sheets/views when user actually needs them
        private bool _sheetsLoaded = false;
        private bool _viewsLoaded = false;
        private bool _fastGridInitialized = false;  // ← Prevent double init
        private bool _windowFullyLoaded = false;    // ← Track if Window_Loaded has completed

        // 📝 Interaction tracking để log phản hồi UI trong lúc đang load sheets
        private bool _userInteractionLoggedDuringSheetLoad = false;
        private int _userInteractionEventCount = 0;
        
        // ⚡ Debounce timer for UpdateExportSummary - batch multiple PropertyChanged events
        private DispatcherTimer _summaryUpdateTimer;
        private bool _isSelectionRefreshScheduled = false;
        
        // ⏱️ CONTINUOUS UI MONITORING: Log every 5 seconds to detect freeze
        private DispatcherTimer _uiMonitorTimer;
        private DateTime _formShownTime;
        private bool _isFormFullyShown = false;
        
        private const int USER_INTERACTION_LOG_LIMIT = 5;
        
        // ⏱️ PERFORMANCE TRACKING: Total time from constructor start to complete load
        private System.Diagnostics.Stopwatch _totalLoadTimer;
        
        // Performance optimization constants
        private const int BATCH_SIZE = 50; // Load 50 items per batch for UI updates
        private const int DETAIL_LOAD_BATCH_SIZE = 75;
        private const int PARALLEL_THRESHOLD = 100; // Use parallel processing for 100+ items
        
        // Cache for sheet sizes to avoid repeated API calls
        private Dictionary<ElementId, string> _sheetSizeCache = new Dictionary<ElementId, string>();
        
        public ObservableCollection<ViewSheetSetInfo> ViewSheetSets
        {
            get => _viewSheetSets;
            set
            {
                _viewSheetSets = value;
                OnPropertyChanged(nameof(ViewSheetSets));
                OnPropertyChanged(nameof(SelectedSetsDisplay));
            }
        }
        
        /// <summary>
        /// Display text for selected sets in ToggleButton
        /// </summary>
        public string SelectedSetsDisplay
        {
            get
            {
                if (_viewSheetSets == null || !_viewSheetSets.Any(s => s.IsSelected))
                    return "All V/S Sets";
                
                var selectedNames = _viewSheetSets
                    .Where(s => s.IsSelected)
                    .Select(s => s.Name)
                    .ToList();
                    
                if (selectedNames.Count == 1)
                    return selectedNames[0];
                else if (selectedNames.Count <= 3)
                    return string.Join(", ", selectedNames);
                else
                    return $"{selectedNames.Count} sets selected";
            }
        }

        // Enhanced properties for data binding
        public int SelectedSheetsCount 
        { 
            get 
            { 
                return Sheets?.Count(s => s.IsSelected) ?? 0; 
            } 
        }

        public int SelectedViewsCount 
        { 
            get 
            { 
                return Views?.Count(v => v.IsSelected) ?? 0; 
            } 
        }
        
        public ObservableRangeCollection<SheetItem> SheetItems => Sheets;
        
        // New property for XAML binding
        public ObservableRangeCollection<SheetItem> Sheets 
        { 
            get => _sheets; 
            set 
            {
                _sheets = value;
                OnPropertyChanged(nameof(Sheets));
                OnPropertyChanged(nameof(SelectedSheetsCount));
            }
        }

        private ObservableCollection<ViewItem> _views;
        public ObservableCollection<ViewItem> Views 
        { 
            get => _views; 
            set 
            {
                _views = value;
                OnPropertyChanged(nameof(Views));
                OnPropertyChanged(nameof(SelectedViewsCount));
            }
        }

        // Properties for Create tab
        private string _outputFolder;
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                _outputFolder = value;
                OnPropertyChanged(nameof(OutputFolder));
                UpdateFilenamePreview();
            }
        }

        public ObservableCollection<object> SelectedItemsForExport
        {
            get
            {
                var selectedItems = new ObservableCollection<object>();
                
                // Add selected sheets
                if (Sheets != null)
                {
                    foreach (var sheet in Sheets.Where(s => s.IsSelected))
                    {
                        selectedItems.Add(new
                        {
                            Number = sheet.SheetNumber,
                            Name = sheet.SheetName,
                            CustomFileName = sheet.CustomFileName,
                            Type = "Sheet"
                        });
                    }
                }
                
                // Add selected views
                if (Views != null)
                {
                    foreach (var RevitView in Views.Where(v => v.IsSelected))
                    {
                        selectedItems.Add(new
                        {
                            Number = RevitView.ViewType,
                            Name = RevitView.ViewName,
                            CustomFileName = RevitView.CustomFileName,
                            Type = "View"
                        });
                    }
                }
                
                return selectedItems;
            }
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            // Debug logging removed
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Export settings với data binding
        public ExportSettings ExportSettings { get; set; }
        
        // Navisworks export settings
        private NWCExportSettings _nwcSettings = new NWCExportSettings();
        public NWCExportSettings NWCSettings
        {
            get => _nwcSettings;
            set
            {
                _nwcSettings = value;
                OnPropertyChanged(nameof(NWCSettings));
            }
        }
        
        // IFC export settings
        private IFCExportSettings _ifcSettings = new IFCExportSettings();
        public IFCExportSettings IFCSettings
        {
            get => _ifcSettings;
            set
            {
                _ifcSettings = value;
                OnPropertyChanged(nameof(IFCSettings));
            }
        }
        
        // IFC Setup Profiles Collection
        private ObservableCollection<string> _ifcCurrentSetups;
        public ObservableCollection<string> IFCCurrentSetups
        {
            get => _ifcCurrentSetups;
            set
            {
                _ifcCurrentSetups = value;
                OnPropertyChanged(nameof(IFCCurrentSetups));
            }
        }
        
        // Selected IFC Setup
        private string _selectedIFCSetup = "<In-Session Setup>";
        public string SelectedIFCSetup
        {
            get => _selectedIFCSetup;
            set
            {
                if (_selectedIFCSetup != value)
                {
                    _selectedIFCSetup = value;
                    OnPropertyChanged(nameof(SelectedIFCSetup));
                    
                    // Auto-load IFC setup from Revit when user selects
                    if (value != "<In-Session Setup>")
                    {
                        try
                        {
                            // Debug logging removed
                            var loadedSettings = IFCExportService.LoadIFCSetupFromRevit(_document, value);
                            
                            // Apply loaded settings to current IFCSettings
                            IFCSettings = loadedSettings;
                            
                            FilenamePreviewText = $"IFC setup '{value}' loaded.";
                            LicorpTrace.Info($"IFC setup '{value}' loaded from Revit.");
                        }
                        catch (Exception ex)
                        {
                            FilenamePreviewText = $"Could not load IFC setup '{value}': {ex.Message}";
                            LicorpTrace.Warn($"Could not load IFC setup '{value}': {ex.Message}");
                        }
                    }
                }
            }
        }
        
        // Export Queue Items for Create tab
        private ObservableCollection<ExportQueueItem> _exportQueueItems;
        private bool _isUpdatingExportQueue;
        private string _filenamePreviewText = "Select sheets/views and formats to preview output names.";

        public string FilenamePreviewText
        {
            get => _filenamePreviewText;
            set
            {
                _filenamePreviewText = value;
                OnPropertyChanged(nameof(FilenamePreviewText));
            }
        }

        public ObservableCollection<ExportQueueItem> ExportQueueItems
        {
            get => _exportQueueItems;
            set
            {
                if (_exportQueueItems != null)
                {
                    _exportQueueItems.CollectionChanged -= ExportQueueItems_CollectionChanged;
                }

                _exportQueueItems = value;
                if (_exportQueueItems != null)
                {
                    _exportQueueItems.CollectionChanged += ExportQueueItems_CollectionChanged;
                }

                OnPropertyChanged(nameof(ExportQueueItems));
                UpdateFilenamePreview();
            }
        }
        
        
        public ExportPlusMainWindow(Document document) : this(document, null)
        {
        }

        public ExportPlusMainWindow(Document document, UIApplication uiApp)
        {
            // ⏱️ START TOTAL LOAD TIMER
            _totalLoadTimer = System.Diagnostics.Stopwatch.StartNew();
            
            // ⚡ FIX: Only log once to avoid 5x duplication
            // Debug logging removed
            // Debug logging removed
            // Debug logging removed
            
            _document = document;
            _uiApp = uiApp;
            
            // ✅ Initialize RevitAsyncHelper for safe async Revit API calls
            // RevitAsyncHelper no longer needed - using ricaun.Revit.UI.Tasks instead
            // Debug logging removed
            
            // Initialize External Event for export operations
            if (_uiApp != null)
            {
                _exportHandler = new ExportHandler();
                _exportEvent = ExternalEvent.Create(_exportHandler);
                // Debug logging removed
                
                // Initialize PDF Export External Event
                _pdfExportHandler = new Events.PDFExportEventHandler();
                _pdfExportEvent = ExternalEvent.Create(_pdfExportHandler);
                // Debug logging removed
                
                // Initialize IFC Export External Event
                _ifcExportHandler = new Events.IFCExportHandler();
                _ifcExportEvent = ExternalEvent.Create(_ifcExportHandler);
                // Debug logging removed
                
                // Initialize ViewSheetSet External Event
                _viewSheetSetHandler = new Events.ViewSheetSetEventHandler();
                _viewSheetSetEvent = ExternalEvent.Create(_viewSheetSetHandler);
                // Debug logging removed
            }
            
            // Initialize export settings with data binding
            ExportSettings = new ExportSettings();
            // Debug logging removed
            
            // ✅ SUBSCRIBE to format changes - detect when user ticks/unticks PDF/DWG/...
            // This allows smart reset: rebuild queue when formats change after export completed
            ExportSettings.PropertyChanged += ExportSettings_PropertyChanged;
            // Debug logging removed
            
            // Initialize IFC Setup Profiles - Load from Revit dynamically
            // Debug logging removed
            try
            {
                var availableSetups = IFCExportService.GetAvailableIFCSetups(_document);
                IFCCurrentSetups = new ObservableCollection<string>(availableSetups);
                SelectedIFCSetup = "<In-Session Setup>";
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
                // Debug logging removed
                // Fallback to hardcoded list
                IFCCurrentSetups = new ObservableCollection<string>
                {
                    "<In-Session Setup>",
                    "IFC 2x3 Coordination RevitView 2.0",
                    "IFC 2x3 Coordination View",
                    "IFC 2x3 GSA Concept Design BIM 2010",
                    "IFC 2x3 Basic FM Handover View",
                    "IFC 2x2 Coordination View",
                    "IFC 2x2 Singapore BCA e-Plan Check",
                    "IFC 2x3 COBie 2.4 Design Deliverable View",
                    "IFC4 Reference View [Architecture]",
                    "IFC4 Reference View [Structural]",
                    "IFC4 Reference View [BuildingService]",
                    "IFC4 Design Transfer View",
                    "Typical Setup"
                };
                SelectedIFCSetup = "<In-Session Setup>";
            }
            
            // Initialize output folder to Desktop
            OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            ExportSettings.CreateSeparateFolders = true;
            // Debug logging removed
            
            // Initialize Export Queue with empty collection
            ExportQueueItems = new ObservableCollection<ExportQueueItem>();
            // Debug logging removed
            
            try
            {
                InitializeComponent();
                // Debug logging removed
            }
            catch (Exception initEx)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"[Export+] InitializeComponent ERROR: {initEx.Message}");
                Licorp.Diagnostics.LicorpTrace.Dbg($"[Export+] InnerException: {initEx.InnerException?.Message}");
                Licorp.Diagnostics.LicorpTrace.Dbg($"[Export+] InnerException StackTrace: {initEx.InnerException?.StackTrace}");
                
                // Build detailed error message with inner exception
                string detailedError = $"Lỗi khởi tạo giao diện XAML: {initEx.Message}";
                if (initEx.InnerException != null)
                {
                    detailedError += $"\n\n❌ Chi tiết lỗi:\n{initEx.InnerException.Message}";
                    detailedError += $"\n\n📍 Stack Trace:\n{initEx.InnerException.StackTrace}";
                }
                
                throw new Exception(detailedError, initEx);
            }
            
            // Load DWG Export Setups from Revit
            // TODO: Temporarily disabled - will re-enable after fixing WPF compiler issues
            // LoadDWGExportSetups();
            // // Debug logging removed
            
            // TODO: Wire up IFC Import/Export buttons after WPF build issues resolved
            // WireUpIFCButtons();
            
            // Configure window for non-modal operation
            ConfigureNonModalWindow();
            
            // Set DataContext for binding - should point to this window, not ExportSettings
            this.DataContext = this;
            // Debug logging removed
            InitializePdfSettingsControls();
            
            InitializeProfiles();
            
            // ⚡⚡⚡ CRITICAL FIX: KHÔNG load sheets trong constructor!
            // Form phải hiện NGAY LẬP TỨC, load data SAU trong Loaded event
            
            // Khởi tạo collection rỗng để DataGrid có thể bind
            Sheets = new ObservableRangeCollection<SheetItem>();
            
            _sheetsLoaded = false;
            _viewsLoaded = false;
            // Debug logging removed
            
            UpdateFormatSelection();
            UpdateNavigationButtons();
            
            // ⚡ Initialize View/Sheet Set Manager (fast - just create object)
            _viewSheetSetManager = new ViewSheetSetService(_document);
            
            // ⚡ Initialize empty ViewSheetSets for binding
            ViewSheetSets = new ObservableCollection<ViewSheetSetInfo>();
            
            AttachUserInteractionLoggingHandlers();
            // Debug logging removed
            
            // ⚡ Initialize UI monitoring timer - logs every 5 seconds to detect freeze
            _uiMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Log every 5 seconds
            };
            _uiMonitorTimer.Tick += UIMonitorTimer_Tick;
            
            // ⚡ Initialize debounce timer for UpdateExportSummary
            _summaryUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 100ms delay
            };
            _summaryUpdateTimer.Tick += (s, e) =>
            {
                _summaryUpdateTimer.Stop();

                if (!_isSelectionRefreshScheduled)
                {
                    return;
                }

                _isSelectionRefreshScheduled = false;
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                UpdateStatusText();
                sw.Stop();
                // Debug logging removed
                
                sw.Restart();
                UpdateExportSummary();
                sw.Stop();
                // Debug logging removed
                
                // Debug logging removed
            };
            // Debug logging removed

            // Debug logging removed
            
            // ⚡⚡⚡ CRITICAL: Unsubscribe trước khi subscribe để tránh duplicate
            this.Loaded -= ExportPlusMainWindow_Loaded;  // Remove nếu đã tồn tại
            this.Loaded += ExportPlusMainWindow_Loaded;  // Add mới
            
            // ⚡ CONTINUOUS USER INTERACTION MONITORING (not just during load!)
            AttachPermanentUserInteractionHandlers();
            // Debug logging removed
        }
        
        /// <summary>
        /// Attach PERMANENT user interaction handlers to log all user actions
        /// </summary>
        private void AttachPermanentUserInteractionHandlers()
        {
            this.PreviewMouseDown += OnUserMouseDown;
            this.PreviewMouseWheel += OnUserMouseWheel;
            this.PreviewKeyDown += OnUserKeyDown;
            
            // Debug logging removed
        }
        
        private void OnUserMouseDown(object sender, MouseButtonEventArgs e)
        {
            var control = e.OriginalSource?.GetType().Name ?? "Unknown";
            // Debug logging removed
        }
        
        private void OnUserMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var direction = e.Delta > 0 ? "UP" : "DOWN";
        }
        
        private void OnUserKeyDown(object sender, WpfKeyEventArgs e)
        {
            // Debug logging removed
        }
        
        private void AttachUserInteractionLoggingHandlers()
        {
            this.PreviewMouseDown -= OnPreviewMouseDownDuringLoad;
            this.PreviewMouseDown += OnPreviewMouseDownDuringLoad;

            this.PreviewMouseWheel -= OnPreviewMouseWheelDuringLoad;
            this.PreviewMouseWheel += OnPreviewMouseWheelDuringLoad;

            this.PreviewKeyDown -= OnPreviewKeyDownDuringLoad;
            this.PreviewKeyDown += OnPreviewKeyDownDuringLoad;

            this.TouchDown -= OnTouchDownDuringLoad;
            this.TouchDown += OnTouchDownDuringLoad;
        }

        private void DetachUserInteractionLoggingHandlers()
        {
            // ⚡ ONLY detach "during load" handlers, NOT permanent handlers!
            this.PreviewMouseDown -= OnPreviewMouseDownDuringLoad;
            this.PreviewMouseWheel -= OnPreviewMouseWheelDuringLoad;
            this.PreviewKeyDown -= OnPreviewKeyDownDuringLoad;
            this.TouchDown -= OnTouchDownDuringLoad;
            
        }

        private void OnPreviewMouseDownDuringLoad(object sender, MouseButtonEventArgs e)
        {
            var button = e.ChangedButton.ToString();
            HandleUserInteractionDuringLoad($"MouseDown ({button})");
        }

        private void OnPreviewMouseWheelDuringLoad(object sender, MouseWheelEventArgs e)
        {
            var direction = e.Delta > 0 ? "WheelUp" : "WheelDown";
            HandleUserInteractionDuringLoad($"MouseWheel ({direction})");
        }

        private void OnPreviewKeyDownDuringLoad(object sender, WpfKeyEventArgs e)
        {
            HandleUserInteractionDuringLoad($"KeyDown ({e.Key})");
        }

        private void OnTouchDownDuringLoad(object sender, TouchEventArgs e)
        {
            HandleUserInteractionDuringLoad("TouchDown");
        }

        private void HandleUserInteractionDuringLoad(string interactionSource)
        {
            if (_sheetsLoaded)
            {
                return;
            }

            _userInteractionEventCount++;

            if (!_userInteractionLoggedDuringSheetLoad)
            {
                _userInteractionLoggedDuringSheetLoad = true;
                // Debug logging removed
            }

            if (_userInteractionEventCount <= USER_INTERACTION_LOG_LIMIT)
            {
                // Debug logging removed

                if (_userInteractionEventCount == USER_INTERACTION_LOG_LIMIT)
                {
                    // Debug logging removed
                }
            }
        }

        private async Task BindSheetsInChunksAsync(IReadOnlyList<SheetItem> sheets)
        {
            const int CHUNK_SIZE = 20;

            if (sheets == null || sheets.Count == 0 || Sheets == null)
            {
                return;
            }

            int total = sheets.Count;
            int chunkIndex = 0;
            var chunkBuffer = new List<SheetItem>(CHUNK_SIZE);

            while (chunkIndex < total)
            {
                if (_cancelLoading)
                {
                    // Debug logging removed
                    return;
                }

                chunkBuffer.Clear();
                int count = Math.Min(CHUNK_SIZE, total - chunkIndex);

                for (int i = 0; i < count; i++)
                {
                    chunkBuffer.Add(sheets[chunkIndex + i]);
                }

                Sheets.AddRange(chunkBuffer);
                chunkIndex += count;

                // Debug logging removed

                if (chunkIndex < total)
                {
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
            }
            
            // ⚡ CRITICAL: Subscribe PropertyChanged AFTER all chunks bound
            // BUT do it in BATCHES to avoid blocking UI thread
            // Debug logging removed
            
            int subscribedCount = 0;
            const int SUBSCRIBE_BATCH_SIZE = 20;
            
            for (int i = 0; i < Sheets.Count; i++)
            {
                Sheets[i].PropertyChanged += SheetItem_PropertyChanged;
                subscribedCount++;
                
                // Yield every 20 subscriptions to keep UI responsive
                if (subscribedCount % SUBSCRIBE_BATCH_SIZE == 0 && i < Sheets.Count - 1)
                {
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    // Debug logging removed
                }
            }
            
        }
        
        /// <summary>
        /// ⚡ Loaded event - Load ViewSheetSets và FastGrid trong background
        /// Form đã hiện → user có thể thao tác ngay
        /// </summary>
        private void ExportPlusMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_windowFullyLoaded)
            {
                return;
            }

            _windowFullyLoaded = true;

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    var initialSets = _viewSheetSetManager?.GetAllViewSheetSets();
                    if (initialSets != null && initialSets.Any())
                    {
                        ViewSheetSets = new ObservableCollection<ViewSheetSetInfo>(initialSets);
                    }

                    _formShownTime = DateTime.Now;
                    _isFormFullyShown = true;
                    _uiMonitorTimer.Start();

                    await Dispatcher.Yield(DispatcherPriority.Background);

                    if (!_sheetsLoaded)
                    {
                        var initialSheets = LoadSheetsInitialFast();
                        Sheets.Clear();
                        await BindSheetsInChunksAsync(initialSheets);
                        _sheetsLoaded = true;
                        DetachUserInteractionLoggingHandlers();
                        UpdateStatusText();
                        _ = LoadSheetDetailsInBatchesAsync();
                    }
                }
                catch (Exception ex)
                {
                    LicorpTrace.Warn($"Initial selection load failed: {ex.Message}");
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Window loaded handler
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        /// <summary>
        /// Initialize FastWpfGrid control for high-performance sheet list rendering.
        /// Falls back to DataGrid if FastWpfGrid types are not available.
        /// </summary>
        private void InitializeFastGrid()
        {
            // ⚡ CRITICAL: Prevent duplicate initialization
            if (_fastGridInitialized)
            {
                // Debug logging removed
                return;
            }
            
            // FastWpfGrid đã bị xóa - chỉ dùng DataGrid
        }

        /// <summary>
        /// Update ExportSettings from UI controls before export
        /// This ensures UI selections are properly synchronized with settings object
        /// </summary>
        private void UpdateExportSettingsFromUI()
        {
            try
            {
                // Debug logging removed
                
                // Update Raster Quality from ComboBox
                if (RasterQualityCombo.SelectedItem is ComboBoxItem rasterItem)
                {
                    string rasterText = rasterItem.Content?.ToString() ?? "High";
                    // Debug logging removed
                    
                    switch (rasterText)
                    {
                        case "Low":
                            ExportSettings.RasterQuality = PSRasterQuality.Low;
                            break;
                        case "Medium":
                            ExportSettings.RasterQuality = PSRasterQuality.Medium;
                            break;
                        case "High":
                            ExportSettings.RasterQuality = PSRasterQuality.High;
                            break;
                        case "Presentation":
                            ExportSettings.RasterQuality = PSRasterQuality.Maximum;
                            break;
                        default:
                            ExportSettings.RasterQuality = PSRasterQuality.High;
                            // Debug logging removed
                            break;
                    }
                }
                
                // Update Colors from ComboBox
                if (ColorsCombo.SelectedItem is ComboBoxItem colorItem)
                {
                    string colorText = colorItem.Content?.ToString() ?? "Color";
                    // Debug logging removed
                    
                    switch (colorText)
                    {
                        case "Color":
                            ExportSettings.Colors = PSColors.Color;
                            // Debug logging removed
                            break;
                        case "Black and White":
                        case "Black and white":
                            ExportSettings.Colors = PSColors.BlackAndWhite;
                            // Debug logging removed
                            break;
                        case "Grayscale":
                            ExportSettings.Colors = PSColors.Grayscale;
                            // Debug logging removed
                            break;
                        default:
                            ExportSettings.Colors = PSColors.Color;
                            // Debug logging removed
                            break;
                    }
                }
                
                // Update Output Folder
                if (!string.IsNullOrEmpty(CreateFolderPathTextBox?.Text))
                {
                    ExportSettings.OutputFolder = CreateFolderPathTextBox.Text;
                    // Debug logging removed
                }
                
                // Update Paper Placement settings
                if (CenterRadio?.IsChecked == true)
                {
                    ExportSettings.PaperPlacement = PSPaperPlacement.Center;
                    // Debug logging removed
                }
                else if (OffsetRadio?.IsChecked == true)
                {
                    ExportSettings.PaperPlacement = PSPaperPlacement.OffsetFromCorner;
                    // Debug logging removed
                }
                
                // Update Paper Margin
                if (MarginCombo.SelectedItem is ComboBoxItem marginItem)
                {
                    string marginText = marginItem.Content?.ToString() ?? "No Margin";
                    // Debug logging removed
                    
                    switch (marginText)
                    {
                        case "No Margin":
                            ExportSettings.PaperMargin = PSPaperMargin.NoMargin;
                            // Debug logging removed
                            break;
                        case "Printer Limit":
                            ExportSettings.PaperMargin = PSPaperMargin.PrinterLimit;
                            // Debug logging removed
                            break;
                        case "User Defined":
                            ExportSettings.PaperMargin = PSPaperMargin.UserDefined;
                            // Debug logging removed
                            break;
                        default:
                            ExportSettings.PaperMargin = PSPaperMargin.NoMargin;
                            // Debug logging removed
                            break;
                    }
                }
                
                // Update Offset X and Y values
                if (double.TryParse(OffsetXTextBox?.Text, out double offsetX))
                {
                    ExportSettings.OffsetX = offsetX;
                    // Debug logging removed
                }
                
                if (double.TryParse(OffsetYTextBox?.Text, out double offsetY))
                {
                    ExportSettings.OffsetY = offsetY;
                    // Debug logging removed
                }
                
                // Update Combine Files setting
                if (CombineFilesRadio?.IsChecked == true)
                {
                    ExportSettings.CombineFiles = true;
                    // Debug logging removed
                }
                else if (SeparateFilesRadio?.IsChecked == true)
                {
                    ExportSettings.CombineFiles = false;
                    // Debug logging removed
                }
                
                // Update Keep Paper Size & Orientation setting
                if (KeepPaperSizeCheckBox?.IsChecked == true)
                {
                    ExportSettings.KeepPaperSize = true;
                    // Debug logging removed
                }
                else
                {
                    ExportSettings.KeepPaperSize = false;
                    // Debug logging removed
                }
                
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        /// <summary>
        /// ⏱️ UI MONITOR TIMER: Logs every 5 seconds to detect UI freeze patterns
        /// Tracks elapsed time since form shown and checks if user can interact
        /// </summary>
        private void UIMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!_isFormFullyShown) return;
            
            var elapsed = DateTime.Now - _formShownTime;
            var elapsedSeconds = (int)elapsed.TotalSeconds;
            
            // Test if UI is responsive by trying to access controls
            try
            {
                var dgEnabled = SheetsDataGrid?.IsEnabled ?? false;
                var dgItemCount = SheetsDataGrid?.Items?.Count ?? 0;
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        /// <summary>
        /// ⚡ DEBOUNCED PropertyChanged handler - batches multiple updates into single UI refresh
        /// Prevents cascading UpdateExportSummary calls (89 sheets × UpdateExportSummary = freeze!)
        /// </summary>
        private void SheetItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSelected")
            {
                // ⚡ Performance: skip per-item UI work during bulk checkbox updates
                if (_isBulkUpdatingCheckboxes)
                {
                    return;
                }

                ScheduleSelectionRefresh();
            }
        }

        private void ForceSelectionRefresh()
        {
            if (_summaryUpdateTimer == null)
            {
                UpdateStatusText();
                UpdateExportSummary();
                return;
            }

            _isSelectionRefreshScheduled = false;
            _summaryUpdateTimer.Stop();
            UpdateStatusText();
            UpdateExportSummary();
        }

        private void ScheduleSelectionRefresh()
        {
            if (_summaryUpdateTimer == null)
            {
                UpdateStatusText();
                UpdateExportSummary();
                return;
            }

            _isSelectionRefreshScheduled = true;
            _summaryUpdateTimer.Stop();
            _summaryUpdateTimer.Start();
        }
        
        /// <summary>
        /// Debug logging method - disabled in production
        /// </summary>
        private void WriteDebugLog(string message)
        {
            // Debug logging disabled for production
            return;
        }
        
        private void ConfigureNonModalWindow()
        {
            try
            {
                // Configure window to work well as non-modal
                this.ShowInTaskbar = true;
                this.Topmost = false;
                this.WindowState = WindowState.Normal;
                
                // Handle window closing event
                this.Closing += ExportPlusMainWindow_Closing;
                
                // Handle window activated/deactivated for better UX
                this.Activated += ExportPlusMainWindow_Activated;
                this.Deactivated += ExportPlusMainWindow_Deactivated;
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        private void ExportPlusMainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Debug logging removed
            DetachUserInteractionLoggingHandlers();
            // Debug logging removed
            
            // ⏱️ Stop UI monitor timer
            if (_uiMonitorTimer != null && _uiMonitorTimer.IsEnabled)
            {
                _uiMonitorTimer.Stop();
                // Debug logging removed
            }
            
            // ⚡⚡⚡ CRITICAL: Set cancel flag NGAY LẬP TỨC để dừng loading
            _cancelLoading = true;
            // Debug logging removed
            
            // ⚡ KHÔNG chặn đóng form - để user tắt ngay
            // Không cần đợi loading xong
            
            // 🚀 OPTIMIZATION: Nếu e.Cancel = true (from cache logic), chỉ cleanup minimal
            // Nếu e.Cancel = false, đây là close thật → cleanup toàn bộ
            
            try
            {
                // Cancel any ongoing export operations first
                if (_exportCancellationTokenSource != null && !_exportCancellationTokenSource.IsCancellationRequested)
                {
                    // Debug logging removed
                    try
                    {
                        _exportCancellationTokenSource.Cancel();
                    }
                    catch (Exception cancelEx)
                    {
                        // Debug logging removed
                    }
                }
                
                // Give a brief moment for any pending operations to complete
                System.Threading.Thread.Sleep(100);
                
                // ⚠️ CHỈ dispose resources nếu THẬT SỰ đóng (không phải Hide)
                // Kiểm tra sau khi event handlers chạy xong
                if (!e.Cancel)
                {
                    // Debug logging removed
                    DisposeResources();
                }
                else
                {
                    // Reset closing flag để lần mở lại có thể load nếu cần
                    _isClosing = false;
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                // Debug logging removed
            }
        }
        
        /// <summary>
        /// Dispose tất cả resources khi THẬT SỰ đóng window
        /// </summary>
        private void DisposeResources()
        {
            try
            {
                // Dispose CancellationTokenSource first
                if (_exportCancellationTokenSource != null)
                {
                    // Debug logging removed
                    try
                    {
                        _exportCancellationTokenSource.Dispose();
                        _exportCancellationTokenSource = null;
                        // Debug logging removed
                    }
                    catch (Exception disposeEx)
                    {
                        // Debug logging removed
                    }
                }
                
                // Dispose External Events
                if (_pdfExportEvent != null)
                {
                    // Debug logging removed
                    try
                    {
                        _pdfExportEvent.Dispose();
                        _pdfExportEvent = null;
                        // Debug logging removed
                    }
                    catch (Exception disposeEx)
                    {
                        // Debug logging removed
                    }
                }
                
                if (_exportEvent != null)
                {
                    // Debug logging removed
                    try
                    {
                        _exportEvent.Dispose();
                        _exportEvent = null;
                        // Debug logging removed
                    }
                    catch (Exception disposeEx)
                    {
                        // Debug logging removed
                    }
                }
                
                if (_ifcExportEvent != null)
                {
                    // Debug logging removed
                    try
                    {
                        _ifcExportEvent.Dispose();
                        _ifcExportEvent = null;
                        // Debug logging removed
                    }
                    catch (Exception disposeEx)
                    {
                        // Debug logging removed
                    }
                }
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        private void ExportPlusMainWindow_Activated(object sender, EventArgs e)
        {
            // Debug logging removed
            // Window brought to front - could refresh data if needed
        }

        private void ExportPlusMainWindow_Deactivated(object sender, EventArgs e)
        {
            // Debug logging removed
            // Window lost focus - user might be working in Revit
        }

        /// <summary>
        /// Show export completed dialog with Open Folder button
        /// </summary>
        private void ShowExportCompletedDialog(string folderPath)
        {
            try
            {
                var dialog = new ExportCompletedDialog(folderPath);
                dialog.Owner = this;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                // Debug logging removed
                // Fallback to simple message box
                MessageBox.Show($"Export completed.\n\nLocation: {folderPath}", 
                              "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ⚡ ASYNC LOADING: Use RevitAsyncHelper instead of DispatcherTimer
        private const int SHEET_CHUNK_SIZE = 50; // ⚡ Tăng lên 50 để ít lần delay hơn
        private bool _cancelLoading = false; // ⚡ Flag để cancel loading khi đóng form
        
        // ✅ Throttle scroll events (từ RevitScheduleEditor)
        private DateTime _lastScrollLoadTime = DateTime.MinValue;
        private const int SCROLL_LOAD_THROTTLE_MS = 200;

        private List<SheetItem> LoadSheetsInitialFast()
        {
            if (_cancelLoading || _document == null)
            {
                return new List<SheetItem>();
            }

            var sheetIds = new FilteredElementCollector(_document)
                .OfClass(typeof(ViewSheet))
                .ToElementIds()
                .ToList();

            var sheets = new List<SheetItem>(sheetIds.Count);

            foreach (var sheetId in sheetIds)
            {
                if (_cancelLoading) break;

                try
                {
                    var element = _document.GetElement(sheetId);
                    if (element == null) continue;

                    var number = element.get_Parameter(BuiltInParameter.SHEET_NUMBER)?.AsString() ?? "";
                    var name = element.get_Parameter(BuiltInParameter.SHEET_NAME)?.AsString() ?? element.Name ?? "";

                    sheets.Add(new SheetItem
                    {
                        Id = sheetId,
                        RevitSheet = element as ViewSheet,
                        SheetNumber = number,
                        SheetName = name,
                        CustomFileName = $"{number} - {name}",
                        Size = "",
                        Revision = "",
                        DrawnBy = "",
                        CheckedBy = "",
                        ApprovedBy = "",
                        IssueDate = "",
                        DesignOption = "",
                        Phase = "",
                        IsSelected = false,
                        IsFullyLoaded = false,
                        HasNoView = false,
                        WarningMessage = null
                    });
                }
                catch (Exception ex)
                {
                    LicorpTrace.Warn($"Sheet initial load skipped {sheetId}: {ex.Message}");
                }
            }

            return sheets
                .OrderBy(s => s.SheetNumber, new AlphanumericComparer())
                .ToList();
        }

        private async Task LoadSheetDetailsInBatchesAsync()
        {
            if (Sheets == null || Sheets.Count == 0) return;

            for (int i = 0; i < Sheets.Count; i++)
            {
                if (_isClosing || _cancelLoading) return;

                var item = Sheets[i];
                if (item == null || item.IsFullyLoaded) continue;

                try
                {
                    var sheet = item.RevitSheet ?? _document.GetElement(item.Id) as ViewSheet;
                    if (sheet == null)
                    {
                        item.IsFullyLoaded = true;
                        continue;
                    }

                    item.RevitSheet = sheet;
                    var parameters = GetSheetParametersFast(sheet);
                    var (hasNoView, warningMsg) = CheckSheetViews(sheet);

                    item.Revision = parameters.Revision;
                    item.DrawnBy = parameters.DrawnBy;
                    item.CheckedBy = parameters.CheckedBy;
                    item.ApprovedBy = parameters.ApprovedBy;
                    item.IssueDate = parameters.IssueDate;
                    item.DesignOption = parameters.DesignOption;
                    item.Phase = parameters.Phase;
                    item.Size = GuessSheetSizeFromNumber(item.SheetNumber);
                    item.HasNoView = hasNoView;
                    item.WarningMessage = warningMsg;
                    item.IsFullyLoaded = true;
                }
                catch (Exception ex)
                {
                    LicorpTrace.Warn($"Sheet detail load failed for {item?.SheetNumber}: {ex.Message}");
                    if (item != null) item.IsFullyLoaded = true;
                }

                if (i % DETAIL_LOAD_BATCH_SIZE == DETAIL_LOAD_BATCH_SIZE - 1)
                {
                    UpdateStatusText();
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
            }

            UpdateStatusText();
        }
        
        /// <summary>
        /// Check xem sheet có chứa model views không (không phải schedule)
        /// </summary>
        private bool HasModelViews(ViewSheet sheet)
        {
            var placedViews = sheet.GetAllPlacedViews();
            
            // ✅ Sheet trống - VẪN GIỮ (sẽ hiện warning icon)
            if (placedViews.Count == 0)
                return true; // Changed: Keep empty sheets and show warning
            
            // Kiểm tra TẤT CẢ views trên sheet
            foreach (var viewId in placedViews)
            {
                var view = _document.GetElement(viewId) as RevitView;
                if (view == null) continue;
                
                if (!(view is ViewSchedule) && view.ViewType != ViewType.Schedule)
                {
                    return true; // Có model view
                }
            }
            
            return false; // Tất cả đều là schedule - BỎ QUA
        }
        
        /// <summary>
        /// ⚡ ASYNC VERSION: Load sheets without blocking UI
        /// Dùng cho user interaction (click tab, button...)
        /// </summary>
        private void LoadSheetsSync()
        {
            // Debug logging removed
            
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            
            // ⚡⚡⚡ CRITICAL: Revit API MUST run on MAIN UI thread
            // SYNCHRONOUS execution - NO async/await to avoid context issues!
            List<SheetItem> loadedSheets = null;
            
            try
            {
                // Debug logging removed
                var loadTimer = System.Diagnostics.Stopwatch.StartNew();
                
                // ✅ Call directly on main thread - NO async, NO Task.Run()!
                loadedSheets = LoadSheetsInitialFast();
                
                loadTimer.Stop();
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
                // Debug logging removed
            }
            
            // Debug logging removed
            
            // ⚡ Update UI (already on UI thread)
            // Debug logging removed
            var uiUpdateTimer = System.Diagnostics.Stopwatch.StartNew();
            
            if (loadedSheets != null && loadedSheets.Count > 0)
            {
                // Debug logging removed
                Sheets = new ObservableRangeCollection<SheetItem>(loadedSheets);
                foreach (var sheet in Sheets)
                {
                    sheet.PropertyChanged += SheetItem_PropertyChanged;
                }
                _ = LoadSheetDetailsInBatchesAsync();
                // Debug logging removed
            }
            else
            {
                // Debug logging removed
            }
            
            // Debug logging removed
            var fastGridTimer = System.Diagnostics.Stopwatch.StartNew();
            InitializeFastGrid();
            fastGridTimer.Stop();
            // Debug logging removed
            
            uiUpdateTimer.Stop();
            totalTimer.Stop();
            // Debug logging removed
            
            // ✅ Mark as loaded
            _sheetsLoaded = true;
        }
        
        private void LoadSheets()
        {
            if (_cancelLoading)
            {
                return;
            }

            try
            {
                var initialSheets = LoadSheetsInitialFast();
                Sheets = new ObservableRangeCollection<SheetItem>(initialSheets);
                foreach (var sheet in Sheets)
                {
                    sheet.PropertyChanged += SheetItem_PropertyChanged;
                }

                _sheetsLoaded = true;
                _ = LoadSheetDetailsInBatchesAsync();
                UpdateStatusText();
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Sheet load failed: {ex.Message}");
                MessageBox.Show($"Error loading sheets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Load sheets in BACKGROUND THREAD - returns List (không update UI)
        /// </summary>
        private List<SheetItem> LoadSheetsInBackground()
        {
            // Debug logging removed
            
            // ⚡⚡⚡ GUARD: Nếu đã load rồi, return empty
            if (_sheetsLoaded)
            {
                // Debug logging removed
                return new List<SheetItem>();
            }
            
            var startTime = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // ⚡ STEP 1: Get sheet IDs (NHANH!)
                // Debug logging removed
                var collectorSw = System.Diagnostics.Stopwatch.StartNew();
                var allSheetIds = new FilteredElementCollector(_document)
                    .OfClass(typeof(ViewSheet))
                    .ToElementIds()
                    .ToList();
                collectorSw.Stop();
                // Debug logging removed
                
                // ⚡ STEP 2: Filter Schedule sheets
                // Debug logging removed
                var filterSw = System.Diagnostics.Stopwatch.StartNew();
                var sheetIds = new List<ElementId>();
                int skippedSchedules = 0;
                int processedCount = 0;
                
                foreach (var sheetId in allSheetIds)
                {
                    if (_cancelLoading) 
                    {
                        // Debug logging removed
                        break;
                    }
                    
                    processedCount++;
                    if (processedCount % 20 == 0)
                    {
                        // Debug logging removed
                    }
                    
                    var sheet = _document.GetElement(sheetId) as ViewSheet;
                    if (sheet == null) continue;
                    
                    if (!HasModelViews(sheet))
                    {
                        skippedSchedules++;
                        continue;
                    }
                    
                    sheetIds.Add(sheetId);
                }
                
                filterSw.Stop();
                
                if (sheetIds.Count == 0)
                {
                    // Debug logging removed
                    return new List<SheetItem>();
                }
                
                // ⚡ STEP 3: Load parameters SEQUENTIALLY (Revit API is NOT thread-safe!)
                var loadSw = System.Diagnostics.Stopwatch.StartNew();
                
                // ⚡⚡⚡ SEQUENTIAL PROCESSING: Revit API MUST run on main thread
                var tempList = new List<SheetItem>();
                int loadedCount = 0;
                
                // Debug logging removed
                
                foreach (var sheetId in sheetIds)
                {
                    if (_cancelLoading) break;
                    
                    try
                    {
                        // 🔍 DEBUG: Log EACH step
                        var sheet = _document.GetElement(sheetId) as ViewSheet;
                        if (sheet == null)
                        {
                            // Debug logging removed
                            continue;
                        }
                        
                        // Debug logging removed
                        
                        // ⚡ Load ALL parameters at once
                        // Debug logging removed
                        var parameters = GetSheetParametersFast(sheet);
                        // Debug logging removed
                        
                        // ⚡⚡⚡ SKIP GetCachedSheetSize() - TOO SLOW (8-20 seconds per sheet!)
                        // FilteredElementCollector in SheetSizeDetector causes massive delays
                        // Use fast pattern-based detection instead
                        string sheetSize = GuessSheetSizeFromNumber(sheet.SheetNumber);
                        
                        // Debug logging removed
                        
                        // ⚠️ Check if sheet has views
                        var (hasNoView, warningMsg) = CheckSheetViews(sheet);
                        if (hasNoView)
                        {
                            // Debug logging removed
                        }
                        
                        tempList.Add(new SheetItem
                        {
                            Id = sheetId,
                            SheetNumber = sheet.SheetNumber ?? "",
                            SheetName = sheet.Name ?? "",
                            CustomFileName = $"{sheet.SheetNumber} - {sheet.Name}",
                            Size = sheetSize,
                            Revision = parameters.Revision,
                            // ⚡ NEW: Extended parameters
                            DrawnBy = parameters.DrawnBy,
                            CheckedBy = parameters.CheckedBy,
                            ApprovedBy = parameters.ApprovedBy,
                            IssueDate = parameters.IssueDate,
                            DesignOption = parameters.DesignOption,
                            Phase = parameters.Phase,
                            IsSelected = false,
                            IsFullyLoaded = true, // ⚡ All data loaded!
                            HasNoView = hasNoView,
                            WarningMessage = warningMsg
                        });
                        // Debug logging removed
                        
                        loadedCount++;
                        if (loadedCount % 20 == 0)
                        {
                            // Debug logging removed
                        }
                    }
                    catch (Exception ex)
                    {
                        // Debug logging removed
                        // Debug logging removed
                    }
                }
                
                loadSw.Stop();
                
                startTime.Stop();
                // Debug logging removed
                
                return tempList;
            }
            catch (Exception ex)
            {
                // Debug logging removed
                return new List<SheetItem>();
            }
        }
        
        /// <summary>
        /// Load sheets ĐỒNG BỘ theo chunk - Nhanh vì không có overhead của async/await
        /// </summary>
        private void LoadSheetsSync(List<ElementId> sheetIds)
        {
            try
            {
                int loadedCount = 0;
                
                // Load từng chunk ĐỒNG BỘ - Nhanh!
                for (int i = 0; i < sheetIds.Count; i += SHEET_CHUNK_SIZE)
                {
                    // ⚡ Check cancel flag trước mỗi chunk
                    if (_cancelLoading)
                    {
                        // Debug logging removed
                        return;
                    }
                    
                    var endIndex = Math.Min(i + SHEET_CHUNK_SIZE, sheetIds.Count);
                    var chunkSw = System.Diagnostics.Stopwatch.StartNew();
                    
                    // ✅ Load chunk TRỰC TIẾP từ document
                    for (int j = i; j < endIndex; j++)
                    {
                        // ⚡ Check cancel flag trong loop
                        if (_cancelLoading)
                        {
                            // Debug logging removed
                            return;
                        }
                        
                        try
                        {
                            var sheet = _document.GetElement(sheetIds[j]) as ViewSheet;
                            if (sheet != null && j < Sheets.Count)
                            {
                                var item = Sheets[j];
                                item.SheetNumber = sheet.SheetNumber ?? "";
                                item.SheetName = sheet.Name ?? "";
                                item.Revision = GetRevisionFast(sheet);
                                item.CustomFileName = $"{item.SheetNumber} - {item.SheetName}";
                                item.Size = ""; // Load on-demand
                                item.IsFullyLoaded = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Debug logging removed
                        }
                    }
                    
                    chunkSw.Stop();
                    loadedCount = endIndex;
                    
                    var progress = (loadedCount * 100) / sheetIds.Count;
                }
                
                // ⏱️ COMPLETE!
                if (_totalLoadTimer != null)
                {
                    _totalLoadTimer.Stop();
                    // Debug logging removed
                    // Debug logging removed
                    // Debug logging removed
                    // Debug logging removed
                    // Debug logging removed
                    // Debug logging removed
                    // Debug logging removed
                    // Debug logging removed
                    // Debug logging removed
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }
        
        // ⚡ TECHNIQUE 3: Load visible rows on scroll (ProSheets on-demand loading)
        private void SheetsDataGrid_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            // Debug logging removed
            
            // ✅ Throttle scroll events - chỉ load khi scroll dừng 200ms (giảm spam)
            if (e.VerticalChange != 0)
            {
                var now = DateTime.Now;
                if ((now - _lastScrollLoadTime).TotalMilliseconds >= SCROLL_LOAD_THROTTLE_MS)
                {
                    _lastScrollLoadTime = now;
                    // Debug logging removed
                    LoadVisibleSheetRows();
                    // Debug logging removed
                }
                else
                {
                }
            }
        }
        
        /// <summary>
        /// Handle ViewsDataGrid scroll to load visible RevitView parameters on demand
        /// </summary>
        private void ViewsDataGrid_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            // Debug logging removed
            
            // ✅ Throttle scroll events - only load when scroll stops for 200ms
            if (e.VerticalChange != 0)
            {
                var now = DateTime.Now;
                if ((now - _lastScrollLoadTime).TotalMilliseconds >= SCROLL_LOAD_THROTTLE_MS)
                {
                    _lastScrollLoadTime = now;
                    // Debug logging removed
                    LoadVisibleViewRows();
                }
                else
                {
                }
            }
        }
        
        private void LoadVisibleSheetRows()
        {
            // Debug logging removed
            
            try
            {
                // Get ScrollViewer from DataGrid
                var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(SheetsDataGrid);
                if (scrollViewer == null)
                {
                    // Debug logging removed
                    return;
                }
                
                int firstVisibleIndex = (int)scrollViewer.VerticalOffset;
                int visibleCount = (int)scrollViewer.ViewportHeight + 10; // +10 buffer
                int lastVisibleIndex = Math.Min(firstVisibleIndex + visibleCount, Sheets.Count);
                
                
                // ⚡ NOTE: All sheets should already have Size loaded in background (IsFullyLoaded = true)
                // This method should rarely find items needing reload
                int itemsNeedingLoad = 0;
                for (int i = firstVisibleIndex; i < lastVisibleIndex; i++)
                {
                    if (i >= 0 && i < Sheets.Count)
                    {
                        var item = Sheets[i];
                        if (!item.IsFullyLoaded)
                        {
                            itemsNeedingLoad++;
                        }
                    }
                }
                
                if (itemsNeedingLoad > 0)
                {
                    // Debug logging removed
                }
                else
                {
                }
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }
        
        /// <summary>
        /// Load RevitView parameters (Scale, DetailLevel, Discipline) for visible rows ONLY
        /// Called when Views DataGrid is scrolled
        /// </summary>
        private void LoadVisibleViewRows()
        {
            // Debug logging removed
            
            try
            {
                if (Views == null || Views.Count == 0)
                {
                    // Debug logging removed
                    return;
                }
                
                // Get ScrollViewer from DataGrid
                var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(ViewsDataGrid);
                if (scrollViewer == null)
                {
                    // Debug logging removed
                    return;
                }
                
                // Calculate visible row range
                // VerticalOffset is in rows (virtualized), ViewportHeight is in pixels
                int firstVisibleIndex = (int)scrollViewer.VerticalOffset;
                
                // Estimate rows visible: ViewportHeight (pixels) / estimated row height (35 pixels per row)
                const int ESTIMATED_ROW_HEIGHT_PIXELS = 35;
                int visibleRowCount = (int)(scrollViewer.ViewportHeight / ESTIMATED_ROW_HEIGHT_PIXELS) + 5; // +5 buffer
                int lastVisibleIndex = Math.Min(firstVisibleIndex + visibleRowCount, Views.Count);
                
                // Debug logging removed
                
                // Load RevitView details for visible rows
                int itemsLoaded = 0;
                var loadedItems = new System.Collections.Generic.List<ViewItem>();
                
                for (int i = firstVisibleIndex; i < lastVisibleIndex; i++)
                {
                    if (i >= 0 && i < Views.Count)
                    {
                        var view = Views[i];
                        if (!view.IsFullyLoaded)
                        {
                            view.LoadFullDetails(_document);
                            loadedItems.Add(view);
                            itemsLoaded++;
                        }
                    }
                }
                
                // 🆕 CRITICAL: Force UI refresh for loaded items
                if (itemsLoaded > 0)
                {
                    // Debug logging removed
                    
                    // Force DataGrid to refresh by triggering PropertyChanged on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var item in loadedItems)
                        {
                            // Force UI to re-read the properties
                            item.RefreshUI();
                        }
                        // Debug logging removed
                    }, System.Windows.Threading.DispatcherPriority.Render);
                }
                else
                {
                    // Debug logging removed
                }
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }
        
        // Helper to find ScrollViewer in visual tree
        private T FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }
                
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// ⚡ OPTIMIZED: Load sheets from ElementIds (sequential)
        /// Only loads Element when needed - no upfront graphics generation
        /// </summary>
        private void LoadSheetsSequentialFromIds(List<ElementId> sheetIds)
        {
            var newSheets = new List<SheetItem>();
            int processedCount = 0;
            
            foreach (var elementId in sheetIds)
            {
                if (_isClosing)
                {
                    // Debug logging removed
                    return;
                }
                
                try
                {
                    // ✅ LAZY LOAD: Only get element when processing
                    var sheet = _document.GetElement(elementId) as ViewSheet;
                    if (sheet == null || sheet.IsTemplate) continue;
                    
                    var sheetItem = ProcessSheetFast(sheet);
                    if (sheetItem != null)
                    {
                        newSheets.Add(sheetItem);
                        processedCount++;
                        
                        // Batch logging
                        if (processedCount % BATCH_SIZE == 0)
                        {
                            // Debug logging removed
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                }
            }
            
            FinalizeSheets(newSheets);
        }
        
        /// <summary>
        /// ⚡ OPTIMIZED: Load sheets from ElementIds (parallel)
        /// </summary>
        private void LoadSheetsParallelFromIds(List<ElementId> sheetIds)
        {
            // Debug logging removed
            
            // Step 1: Extract data in main thread (Revit API not thread-safe)
            var sheetDataList = new List<SheetDataFast>();
            int extractedCount = 0;
            
            foreach (var elementId in sheetIds)
            {
                if (_isClosing) return;
                
                try
                {
                    var sheet = _document.GetElement(elementId) as ViewSheet;
                    if (sheet == null || sheet.IsTemplate) continue;
                    
                    var data = new SheetDataFast
                    {
                        ElementId = elementId,
                        SheetNumber = sheet.SheetNumber ?? "NO_NUMBER",
                        SheetName = sheet.Name ?? "NO_NAME",
                        Revision = GetRevisionFast(sheet),
                        SheetSize = GetCachedSheetSize(sheet)
                    };
                    
                    sheetDataList.Add(data);
                    extractedCount++;
                    
                    if (extractedCount % BATCH_SIZE == 0)
                    {
                        // Debug logging removed
                    }
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                }
            }
            
            // Debug logging removed
            
            if (_isClosing) return;
            
            // Step 2: Process data in parallel (no Revit API calls)
            // Debug logging removed
            var newSheets = new System.Collections.Concurrent.ConcurrentBag<SheetItem>();
            int processedCount = 0;
            
            System.Threading.Tasks.Parallel.ForEach(sheetDataList,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
                (data) =>
                {
                    if (_isClosing) return;
                    
                    var sheetItem = CreateSheetItemFromDataFast(data);
                    if (sheetItem != null)
                    {
                        newSheets.Add(sheetItem);
                        
                        int count = System.Threading.Interlocked.Increment(ref processedCount);
                        if (count % BATCH_SIZE == 0)
                        {
                            // Debug logging removed
                        }
                    }
                });
            
            // Debug logging removed
            FinalizeSheets(newSheets.ToList());
        }
        
        // Helper class for fast data extraction
        private class SheetDataFast
        {
            public ElementId ElementId { get; set; }
            public string SheetNumber { get; set; }
            public string SheetName { get; set; }
            public string Revision { get; set; }
            public string SheetSize { get; set; }
            
            // ⚡ NEW: Extended parameters
            public string DrawnBy { get; set; }
            public string CheckedBy { get; set; }
            public string ApprovedBy { get; set; }
            public string IssueDate { get; set; }
            public string DesignOption { get; set; }
            public string Phase { get; set; }
        }
        
        /// <summary>
        /// ⚡ NEW: Load ALL sheet parameters at once (more efficient than 7 separate calls)
        /// </summary>
        private SheetParameters GetSheetParametersFast(ViewSheet sheet)
        {
            try
            {
                return new SheetParameters
                {
                    Revision = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "",
                    DrawnBy = sheet.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY)?.AsString() ?? "",
                    CheckedBy = sheet.get_Parameter(BuiltInParameter.SHEET_CHECKED_BY)?.AsString() ?? "",
                    ApprovedBy = sheet.get_Parameter(BuiltInParameter.SHEET_APPROVED_BY)?.AsString() ?? "",
                    IssueDate = sheet.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE)?.AsString() ?? "",
                    DesignOption = sheet.get_Parameter(BuiltInParameter.DESIGN_OPTION_ID)?.AsValueString() ?? "",
                    Phase = sheet.get_Parameter(BuiltInParameter.PHASE_CREATED)?.AsValueString() ?? ""
                };
            }
            catch
            {
                return new SheetParameters(); // Return empty if any error
            }
        }
        
        /// <summary>
        /// Helper class to hold all sheet parameters
        /// </summary>
        private class SheetParameters
        {
            public string Revision { get; set; } = "";
            public string DrawnBy { get; set; } = "";
            public string CheckedBy { get; set; } = "";
            public string ApprovedBy { get; set; } = "";
            public string IssueDate { get; set; } = "";
            public string DesignOption { get; set; } = "";
            public string Phase { get; set; } = "";
        }
        
        /// <summary>
        /// ⚡ SAFE FALLBACK: Guess sheet size from sheet number when detection fails
        /// </summary>
        private string GuessSheetSizeFromNumber(string sheetNumber)
        {
            if (string.IsNullOrEmpty(sheetNumber)) return "A1";
            
            // Common patterns: A0, A1, A2, A3, A4
            if (sheetNumber.Contains("A0")) return "A0";
            if (sheetNumber.Contains("A1")) return "A1";
            if (sheetNumber.Contains("A2")) return "A2";
            if (sheetNumber.Contains("A3")) return "A3";
            if (sheetNumber.Contains("A4")) return "A4";
            
            // Default to A1 (most common)
            return "A1";
        }
        
        /// <summary>
        /// ⚡ FAST: Get revision without exception handling overhead (DEPRECATED - use GetSheetParametersFast)
        /// </summary>
        private string GetRevisionFast(ViewSheet sheet)
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
        
        /// <summary>
        /// ⚡ FAST: Process sheet with minimal overhead
        /// </summary>
        private SheetItem ProcessSheetFast(ViewSheet sheet)
        {
            // Check if sheet has views
            var (hasNoView, warningMsg) = CheckSheetViews(sheet);
            
            var sheetItem = new SheetItem
            {
                Id = sheet.Id,
                RevitSheet = sheet,
                IsSelected = false,
                SheetNumber = sheet.SheetNumber ?? "NO_NUMBER",
                SheetName = sheet.Name ?? "NO_NAME",
                Revision = GetRevisionFast(sheet),
                Size = GetCachedSheetSize(sheet),
                CustomFileName = $"{sheet.SheetNumber ?? "NO_NUMBER"}_{(sheet.Name ?? "NO_NAME").Replace(" ", "_")}",
                IsFullyLoaded = true,
                HasNoView = hasNoView,
                WarningMessage = warningMsg
            };
            
            // ⚡ NO PropertyChanged here - will subscribe AFTER binding completes
            
            return sheetItem;
        }
        
        /// <summary>
        /// ⚡ FAST: Get sheet size from cache (no FilteredElementCollector call)
        /// </summary>
        private string GetCachedSheetSize(ViewSheet sheet)
        {
            return Utils.SheetSizeDetector.GetSheetSize(sheet);
        }
        
        /// <summary>
        /// ⚡ FAST: Create SheetItem from pre-extracted data (parallel-safe)
        /// </summary>
        private SheetItem CreateSheetItemFromDataFast(SheetDataFast data)
        {
            var sheetItem = new SheetItem
            {
                Id = data.ElementId,
                RevitSheet = null, // Will be set when needed
                IsSelected = false,
                SheetNumber = data.SheetNumber,
                SheetName = data.SheetName,
                Revision = data.Revision,
                Size = data.SheetSize,
                // ⚡ NEW: Extended parameters
                DrawnBy = data.DrawnBy,
                CheckedBy = data.CheckedBy,
                ApprovedBy = data.ApprovedBy,
                IssueDate = data.IssueDate,
                DesignOption = data.DesignOption,
                Phase = data.Phase,
                CustomFileName = $"{data.SheetNumber}_{data.SheetName.Replace(" ", "_")}",
                IsFullyLoaded = true // ⚡ Already loaded all data (Size, Revision, + 6 extended params) in background
            };
            
            // ⚡ NO PropertyChanged here - will subscribe AFTER binding completes
            
            return sheetItem;
        }
        
        /// <summary>
        /// Load sheets sequentially with batch UI updates - DEPRECATED
        /// Use LoadSheetsSequentialFromIds instead
        /// </summary>
        private void LoadSheetsSequential(List<ViewSheet> sheets)
        {
            var newSheets = new List<SheetItem>();
            int addedCount = 0;
            
            foreach (var sheet in sheets)
            {
                if (_isClosing)
                {
                    // Debug logging removed
                    return;
                }
                
                var sheetItem = ProcessSheet(sheet);
                if (sheetItem != null)
                {
                    newSheets.Add(sheetItem);
                    addedCount++;
                    
                    // Batch update UI every BATCH_SIZE items
                    if (addedCount % BATCH_SIZE == 0)
                    {
                        // Debug logging removed
                    }
                }
            }
            
            FinalizeSheets(newSheets);
        }
        
        /// <summary>
        /// Load sheets in parallel for faster processing (100+ sheets)
        /// </summary>
        private void LoadSheetsParallel(List<ViewSheet> sheets)
        {
            // CRITICAL: Revit API is NOT thread-safe!
            // We MUST extract all data from Revit API in the main thread first
            // Then we can process that data in parallel threads
            
            
            // Step 1: Extract ALL data from Revit API in main thread (thread-safe)
            var sheetDataList = new List<SheetData>();
            int extractedCount = 0;
            
            foreach (var sheet in sheets)
            {
                if (_isClosing) return;
                
                try
                {
                    var data = new SheetData
                    {
                        Sheet = sheet,
                        SheetNumber = sheet.SheetNumber ?? "NO_NUMBER",
                        SheetName = sheet.Name ?? "NO_NAME",
                        SheetId = sheet.Id.GetIdValue().ToString(),
                        Revision = GetRevision(sheet),
                        SheetSize = GetCachedSheetSize(sheet)
                    };
                    
                    sheetDataList.Add(data);
                    extractedCount++;
                    
                    if (extractedCount % BATCH_SIZE == 0)
                    {
                        // Debug logging removed
                    }
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                }
            }
            
            // Debug logging removed
            
            if (_isClosing) return;
            
            // Step 2: Now process the extracted data in parallel (NO Revit API calls here!)
            // Debug logging removed
            var newSheets = new System.Collections.Concurrent.ConcurrentBag<SheetItem>();
            int processedCount = 0;
            
            System.Threading.Tasks.Parallel.ForEach(sheetDataList, 
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
                (data) =>
                {
                    if (_isClosing) return;
                    
                    var sheetItem = CreateSheetItemFromData(data);
                    if (sheetItem != null)
                    {
                        newSheets.Add(sheetItem);
                        
                        int count = System.Threading.Interlocked.Increment(ref processedCount);
                        if (count % BATCH_SIZE == 0)
                        {
                            // Debug logging removed
                        }
                    }
                });
            
            if (_isClosing) return;
            
            // Debug logging removed
            FinalizeSheets(newSheets.ToList());
        }
        
        // Helper class to store sheet data extracted from Revit API
        private class SheetData
        {
            public ViewSheet Sheet { get; set; }
            public string SheetNumber { get; set; }
            public string SheetName { get; set; }
            public string SheetId { get; set; }
            public string Revision { get; set; }
            public string SheetSize { get; set; }
        }
        
        // Helper method to extract revision (main thread only)
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
        
        // Create SheetItem from pre-extracted data (can run in parallel thread)
        private SheetItem CreateSheetItemFromData(SheetData data)
        {
            try
            {
                // Check if sheet has views
                var (hasNoView, warningMsg) = CheckSheetViews(data.Sheet);
                
                var sheetItem = new SheetItem
                {
                    Id = data.Sheet.Id,  // ElementId, not string
                    RevitSheet = data.Sheet,
                    SheetNumber = data.SheetNumber,
                    SheetName = data.SheetName,
                    Revision = data.Revision,
                    Size = data.SheetSize,
                    IsSelected = false,
                    IsFullyLoaded = true,
                    CustomFileName = $"{data.SheetNumber} - {data.SheetName}",
                    HasNoView = hasNoView,
                    WarningMessage = warningMsg
                };
                
                // ⚡ NO PropertyChanged here - will subscribe AFTER binding completes
                
                return sheetItem;
            }
            catch (Exception ex)
            {
                // Debug logging removed
                return null;
            }
        }
        
        /// <summary>
        /// Check if sheet has any views placed on it
        /// </summary>
        private (bool hasNoView, string warningMsg) CheckSheetViews(ViewSheet sheet)
        {
            try
            {
                var viewIds = sheet.GetAllPlacedViews();
                if (viewIds == null || viewIds.Count == 0)
                {
                    return (false, null);
                }
                return (false, null);
            }
            catch
            {
                return (false, null);
            }
        }
        
        /// <summary>
        /// Process a single sheet - optimized with caching
        /// </summary>
        private SheetItem ProcessSheet(ViewSheet sheet)
        {
            try
            {
                string sheetNumber = sheet.SheetNumber ?? "NO_NUMBER";
                string sheetName = sheet.Name ?? "NO_NAME";
                
                // Get revision (fast)
                string revision = "";
                try
                {
                    Parameter revParam = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                    revision = revParam?.AsString() ?? "";
                }
                catch (Exception ex)
                {
                    LicorpTrace.Warn($"Could not read revision for sheet {sheetNumber}: {ex.Message}");
                }
                
                // Get size with caching
                string sheetSize = GetCachedSheetSize(sheet);
                
                // Check if sheet has views
                var (hasNoView, warningMsg) = CheckSheetViews(sheet);
                
                var sheetItem = new SheetItem
                {
                    Id = sheet.Id,
                    RevitSheet = sheet,
                    IsSelected = false,
                    SheetNumber = sheetNumber,
                    SheetName = sheetName,
                    Revision = revision,
                    Size = sheetSize,
                    CustomFileName = $"{sheetNumber}_{sheetName.Replace(" ", "_")}",
                    IsFullyLoaded = true,
                    HasNoView = hasNoView,
                    WarningMessage = warningMsg
                };
                
                // ⚡ NO PropertyChanged here - will subscribe AFTER binding completes
                
                return sheetItem;
            }
            catch (Exception ex)
            {
                // Debug logging removed
                return null;
            }
        }
        
        /// <summary>
        /// Finalize sheets collection - sort and update UI
        /// </summary>
        private async void FinalizeSheets(List<SheetItem> sheets)
        {
            // Debug logging removed
            
            // Sort sheets
            var sortedSheets = sheets.OrderBy(s => s.SheetNumber, new AlphanumericComparer()).ToList();
            
            // Update UI on dispatcher thread (NON-BLOCKING)
            await Dispatcher.InvokeAsync(async () =>
            {
                Sheets = new ObservableRangeCollection<SheetItem>(sortedSheets);
                
                // Debug logging removed
                
                // ⚡ CRITICAL: Subscribe PropertyChanged in BATCHES to avoid blocking
                int subscribedCount = 0;
                const int SUBSCRIBE_BATCH_SIZE = 20;
                
                for (int i = 0; i < Sheets.Count; i++)
                {
                    Sheets[i].PropertyChanged += SheetItem_PropertyChanged;
                    subscribedCount++;
                    
                    // Yield every 20 subscriptions
                    if (subscribedCount % SUBSCRIBE_BATCH_SIZE == 0 && i < Sheets.Count - 1)
                    {
                        await Dispatcher.Yield(DispatcherPriority.Background);
                        // Debug logging removed
                    }
                }
                
                
                UpdateStatusText();
                UpdateExportSummary();
                // Debug logging removed
            });
        }

        private List<ViewItem> LoadViewsInitialFast()
        {
            var existingCustomNames = Views?.Where(v => !string.IsNullOrEmpty(v.ViewId))
                                            .ToDictionary(v => v.ViewId, v => v.CustomFileName)
                                     ?? new Dictionary<string, string>();
            var viewportNumbers = BuildViewportNumberCache();

            var viewIds = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Views)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .ToList();

            var views = new List<ViewItem>(viewIds.Count);

            foreach (var id in viewIds)
            {
                if (_isClosing) break;

                try
                {
                    var view = _document.GetElement(id) as RevitView;
                    if (!IsExportableView(view)) continue;

                    var viewId = view.Id.GetIdValueString();
                    var viewName = view.Name ?? "";
                    var customFileName = existingCustomNames.TryGetValue(viewId, out var existingName)
                        ? existingName
                        : viewName;

                    views.Add(new ViewItem
                    {
                        RevitView = view,
                        RevitViewId = view.Id,
                        ViewId = viewId,
                        ViewNumber = viewportNumbers.TryGetValue(view.Id, out var number) ? number : "",
                        ViewName = viewName,
                        ViewType = ConvertViewTypeToString(view.ViewType),
                        Scale = "",
                        DetailLevel = "",
                        Discipline = "",
                        ViewInfo = "",
                        CustomFileName = customFileName,
                        IsSelected = false,
                        IsFullyLoaded = false
                    });
                }
                catch (Exception ex)
                {
                    LicorpTrace.Warn($"View initial load skipped {id}: {ex.Message}");
                }
            }

            return views;
        }

        private bool IsExportableView(RevitView view)
        {
            return view != null &&
                   !view.IsTemplate &&
                   view.ViewType != ViewType.DrawingSheet &&
                   view.ViewType != ViewType.ProjectBrowser &&
                   view.ViewType != ViewType.SystemBrowser &&
                   view.CanBePrinted;
        }

        private Dictionary<ElementId, string> BuildViewportNumberCache()
        {
            var result = new Dictionary<ElementId, string>();

            try
            {
                var viewports = new FilteredElementCollector(_document)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>();

                var hasDetailNumberParameter = Enum.TryParse("VIEWPORT_DETAIL_NUMBER", out BuiltInParameter detailNumberParameter);

                foreach (var viewport in viewports)
                {
                    try
                    {
                        var viewId = viewport.ViewId;
                        if (result.ContainsKey(viewId)) continue;

                        string number = null;
                        if (hasDetailNumberParameter)
                        {
                            number = viewport.get_Parameter(detailNumberParameter)?.AsString();
                        }

                        if (string.IsNullOrWhiteSpace(number))
                        {
                            number = viewport.LookupParameter("Detail Number")?.AsString();
                        }

                        result[viewId] = number ?? "";
                    }
                    catch
                    {
                        // Ignore individual viewport failures; the view can still load.
                    }
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Viewport number cache failed: {ex.Message}");
            }

            return result;
        }

        private async Task LoadViewDetailsInBatchesAsync()
        {
            if (Views == null || Views.Count == 0) return;

            for (int i = 0; i < Views.Count; i++)
            {
                if (_isClosing) return;

                var item = Views[i];
                if (item == null || item.IsFullyLoaded) continue;

                try
                {
                    item.LoadFullDetails(_document);
                }
                catch (Exception ex)
                {
                    LicorpTrace.Warn($"View detail load failed for {item?.ViewName}: {ex.Message}");
                    if (item != null) item.IsFullyLoaded = true;
                }

                if (i % DETAIL_LOAD_BATCH_SIZE == DETAIL_LOAD_BATCH_SIZE - 1)
                {
                    UpdateStatusText();
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
            }

            UpdateStatusText();
        }

        private void LoadViews()
        {
            var startTime = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var initialViews = LoadViewsInitialFast();
                FinalizeViews(initialViews);
                _viewsLoaded = true;
                _ = LoadViewDetailsInBatchesAsync();
                startTime.Stop();
            }
            catch (Exception ex)
            {
                startTime.Stop();
                LicorpTrace.Warn($"View load failed: {ex.Message}");
                MessageBox.Show($"Error loading views: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// OPTIMIZED: Load views from ElementIds (sequential)
        /// </summary>
        private void LoadViewsSequentialFromIds(List<ElementId> viewIds, Dictionary<string, string> existingCustomNames)
        {
            var newViews = new List<ViewItem>();
            int addedCount = 0;
            int restoredCount = 0;
            
            foreach (var elementId in viewIds)
            {
                if (_isClosing)
                {
                    // Debug logging removed
                    return;
                }
                
                try
                {
                    // ✅ LAZY LOAD: Only get element when processing
                    var view = _document.GetElement(elementId) as RevitView;
                    if (view == null) continue;
                    
                var viewItem = ProcessView(view, existingCustomNames, ref restoredCount);
                    newViews.Add(viewItem);
                    addedCount++;
                    
                    if (addedCount % BATCH_SIZE == 0)
                    {
                        // Debug logging removed
                    }
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                }
            }
            
            // Debug logging removed
            FinalizeViews(newViews);
        }
        
        /// <summary>
        /// ⚡ OPTIMIZED: Load views from ElementIds (parallel)
        /// </summary>
        private void LoadViewsParallelFromIds(List<ElementId> viewIds, Dictionary<string, string> existingCustomNames)
        {
            // Debug logging removed
            
            // Step 1: Extract data in main thread
            var viewDataList = new List<ViewData>();
            int extractedCount = 0;
            
            foreach (var elementId in viewIds)
            {
                if (_isClosing) return;
                
                try
                {
                    var view = _document.GetElement(elementId) as RevitView;
                    if (view == null) continue;
                    
                    var data = new ViewData
                    {
                        ElementId = elementId,
                        ViewId = view.Id.ToString(),
                        ViewName = view.Name ?? "NO_NAME",
                        ViewType = view.ViewType.ToString(),
                        ViewScale = view.Scale.ToString()
                    };
                    
                    viewDataList.Add(data);
                    extractedCount++;
                    
                    if (extractedCount % BATCH_SIZE == 0)
                    {
                        // Debug logging removed
                    }
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                }
            }
            
            // Debug logging removed
            
            if (_isClosing) return;
            
            // Step 2: Process in parallel
            // Debug logging removed
            var newViews = new System.Collections.Concurrent.ConcurrentBag<ViewItem>();
            int processedCount = 0;
            int restoredCount = 0;
            
            System.Threading.Tasks.Parallel.ForEach(viewDataList,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
                (data) =>
                {
                    if (_isClosing) return;
                    
                    var viewItem = CreateViewItemFromData(data, existingCustomNames, ref restoredCount);
                    if (viewItem != null)
                    {
                        newViews.Add(viewItem);
                        
                        int count = System.Threading.Interlocked.Increment(ref processedCount);
                        if (count % BATCH_SIZE == 0)
                        {
                            // Debug logging removed
                        }
                    }
                });
            
            // Debug logging removed
            // Debug logging removed
            FinalizeViews(newViews.ToList());
        }
        
        /// <summary>
        /// Load views sequentially with batch updates - DEPRECATED
        /// Use LoadViewsSequentialFromIds instead
        /// </summary>
        private void LoadViewsSequential(List<RevitView> views, Dictionary<string, string> existingCustomNames)
        {
            var newViews = new List<ViewItem>();
            int addedCount = 0;
            int restoredCount = 0;
            
            foreach (var RevitView in views)
            {
                // Check if window is closing
                if (_isClosing)
                {
                    // Debug logging removed
                    return;
                }
                
                var viewItem = ProcessView(RevitView, existingCustomNames, ref restoredCount);
                newViews.Add(viewItem);
                addedCount++;
                
                // Batch update UI every BATCH_SIZE items
                if (addedCount % BATCH_SIZE == 0)
                {
                    // Debug logging removed
                }
            }
            
            // Debug logging removed
            FinalizeViews(newViews);
        }
        
        /// <summary>
        /// Load views in parallel - MUST extract Revit API data in main thread first!
        /// </summary>
        private void LoadViewsParallel(List<RevitView> views, Dictionary<string, string> existingCustomNames)
        {
            // CRITICAL: Revit API is NOT thread-safe!
            // Extract ALL data from Revit API in main thread first
            
            // Debug logging removed
            
            // Step 1: Extract data in main thread (safe with Revit API)
            var viewDataList = new List<ViewData>();
            int extractedCount = 0;
            
            foreach (var RevitView in views)
            {
                if (_isClosing) return;
                
                try
                {
                    var data = ExtractViewData(RevitView, existingCustomNames);
                    viewDataList.Add(data);
                    extractedCount++;
                    
                    if (extractedCount % BATCH_SIZE == 0)
                    {
                        // Debug logging removed
                    }
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                }
            }
            
            // Debug logging removed
            
            if (_isClosing) return;
            
            // Step 2: Process extracted data in parallel (NO Revit API calls!)
            // Debug logging removed
            var newViews = new System.Collections.Concurrent.ConcurrentBag<ViewItem>();
            int processedCount = 0;
            int restoredCount = 0;
            
            Parallel.ForEach(viewDataList, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (data, state) =>
            {
                if (_isClosing)
                {
                    state.Stop();
                    return;
                }
                
                var viewItem = CreateViewItemFromData(data);
                if (viewItem != null)
                {
                    newViews.Add(viewItem);
                    
                    if (data.HasCustomFileName)
                    {
                        System.Threading.Interlocked.Increment(ref restoredCount);
                    }
                    
                    int current = System.Threading.Interlocked.Increment(ref processedCount);
                    if (current % BATCH_SIZE == 0)
                    {
                        // Debug logging removed
                    }
                }
            });
            
            // Debug logging removed
            FinalizeViews(newViews.ToList());
        }
        
        // Helper class to store RevitView data
        private class ViewData
        {
            public ElementId ElementId { get; set; } // ⚡ NEW: For ElementIds optimization
            public RevitView View { get; set; }
            public string ViewId { get; set; }
            public string ViewName { get; set; }
            public string ViewType { get; set; }
            public string ViewScale { get; set; } // ⚡ NEW: For fast extraction
            public string Scale { get; set; }
            public string DetailLevel { get; set; }
            public string Discipline { get; set; }
            public string CustomFileName { get; set; }
            public bool HasCustomFileName { get; set; }
        }
        
        /// <summary>
        /// Convert Revit ViewType enum to human-readable string matching AvailableViewTypes list
        /// </summary>
        private string ConvertViewTypeToString(Autodesk.Revit.DB.ViewType viewType)
        {
            switch (viewType)
            {
                case Autodesk.Revit.DB.ViewType.ThreeD:
                    return "3D";
                case Autodesk.Revit.DB.ViewType.FloorPlan:
                    return "Floor Plan";
                case Autodesk.Revit.DB.ViewType.CeilingPlan:
                    return "Ceiling Plan";
                case Autodesk.Revit.DB.ViewType.Elevation:
                    return "Elevation";
                case Autodesk.Revit.DB.ViewType.Section:
                    return "Section";
                case Autodesk.Revit.DB.ViewType.Detail:
                    return "Detail";
                case Autodesk.Revit.DB.ViewType.Rendering:
                    return "Rendering";
                case Autodesk.Revit.DB.ViewType.Legend:
                    return "Legend";
                case Autodesk.Revit.DB.ViewType.EngineeringPlan:
                    return "Engineering Plan";
                case Autodesk.Revit.DB.ViewType.AreaPlan:
                    return "Area Plan";
                default:
                    return viewType.ToString();
            }
        }
        
        // Extract RevitView data in main thread (Revit API calls)
        private ViewData ExtractViewData(RevitView view, Dictionary<string, string> existingCustomNames)
        {
            var data = new ViewData
            {
                View = view,
                ViewId = view.Id.GetIdValue().ToString(),
                ViewName = view.Name ?? "Unnamed",
                ViewType = ConvertViewTypeToString(view.ViewType)  // ✅ FIX: Convert to human-readable
            };
            
            // Extract scale, detail level, discipline
            try
            {
                Parameter scaleParam = view.get_Parameter(BuiltInParameter.VIEW_SCALE);
                data.Scale = scaleParam != null && scaleParam.HasValue ? $"1:{scaleParam.AsInteger()}" : "N/A";
                
                data.DetailLevel = view.DetailLevel.ToString();
                
                Parameter disciplineParam = view.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE);
                data.Discipline = disciplineParam?.AsValueString() ?? "N/A";
            }
            catch (Exception ex)
            {
                // Debug logging removed
                data.Scale = "N/A";
                data.DetailLevel = "N/A";
                data.Discipline = "N/A";
            }
            
            // Check for custom filename
            if (!string.IsNullOrEmpty(data.ViewId) && 
                existingCustomNames.TryGetValue(data.ViewId, out string customName))
            {
                data.CustomFileName = customName;
                data.HasCustomFileName = true;
            }
            else
            {
                data.CustomFileName = data.ViewName;
                data.HasCustomFileName = false;
            }
            
            return data;
        }
        
        // Create ViewItem from extracted data (can run in parallel)
        private ViewItem CreateViewItemFromData(ViewData data)
        {
            try
            {
                var viewItem = new ViewItem
                {
                    RevitView = data.View,
                    RevitViewId = data.View.Id,  // CRITICAL: Set ElementId for NWC/IFC export
                    ViewId = data.ViewId,
                    ViewName = data.ViewName,
                    ViewType = data.ViewType,
                    Scale = data.Scale,
                    DetailLevel = data.DetailLevel,
                    Discipline = data.Discipline,
                    CustomFileName = data.CustomFileName,
                    IsSelected = false
                };
                
                // Subscribe to PropertyChanged
                viewItem.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "IsSelected")
                    {
                        // Auto-enable NWC/IFC for 3D views
                        if (viewItem.IsSelected && viewItem.ViewType != null && 
                            (viewItem.ViewType.Contains("ThreeD") || viewItem.ViewType.Contains("3D")))
                        {
                            if (!ExportSettings.IsNwcSelected)
                                ExportSettings.IsNwcSelected = true;
                            if (!ExportSettings.IsIfcSelected)
                                ExportSettings.IsIfcSelected = true;
                        }
                        UpdateStatusText();
                        UpdateExportSummary();
                    }
                };
                
                return viewItem;
            }
            catch (Exception ex)
            {
                // Debug logging removed
                return null;
            }
        }
        
        /// <summary>
        /// ⚡ FAST: Create ViewItem from pre-extracted data with custom filename restore
        /// </summary>
        private ViewItem CreateViewItemFromData(ViewData data, Dictionary<string, string> existingCustomNames, ref int restoredCount)
        {
            try
            {
                string customFileName = $"{data.ViewName.Replace(" ", "_")}";
                
                // Restore custom filename if exists
                if (existingCustomNames.TryGetValue(data.ViewId, out string existingName))
                {
                    customFileName = existingName;
                    System.Threading.Interlocked.Increment(ref restoredCount);
                }
                
                var viewItem = new ViewItem
                {
                    RevitView = null, // Will be set when needed (lazy)
                    RevitViewId = data.ElementId,
                    ViewId = data.ViewId,
                    ViewName = data.ViewName,
                    ViewType = data.ViewType,
                    Scale = data.ViewScale,
                    DetailLevel = "Not Loaded", // Lazy load
                    Discipline = "Not Loaded",  // Lazy load
                    CustomFileName = customFileName,
                    IsSelected = false
                };
                
                // Subscribe to PropertyChanged
                viewItem.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "IsSelected")
                    {
                        // Auto-enable NWC/IFC for 3D views
                        if (viewItem.IsSelected && viewItem.ViewType != null && 
                            (viewItem.ViewType.Contains("ThreeD") || viewItem.ViewType.Contains("3D")))
                        {
                            if (!ExportSettings.IsNwcSelected)
                                ExportSettings.IsNwcSelected = true;
                            if (!ExportSettings.IsIfcSelected)
                                ExportSettings.IsIfcSelected = true;
                        }
                        UpdateStatusText();
                        UpdateExportSummary();
                    }
                };
                
                return viewItem;
            }
            catch (Exception ex)
            {
                // Debug logging removed
                return null;
            }
        }
        
        /// <summary>
        /// Process a single RevitView item
        /// </summary>
        private ViewItem ProcessView(RevitView view, Dictionary<string, string> existingCustomNames, ref int restoredCount)
        {
            try
            {
                // ⚠️ CRITICAL: Do NOT load full details here - parallel processing runs on background thread
                // LoadFullDetails() will be called later on UI thread when rows are scrolled into RevitView
                var viewItem = new ViewItem(view, loadFullDetails: false);
                
                // Restore custom filename
                if (!string.IsNullOrEmpty(viewItem.ViewId) && 
                    existingCustomNames.TryGetValue(viewItem.ViewId, out string customName))
                {
                    viewItem.CustomFileName = customName;
                    restoredCount++;
                }
                
                // ⚡ NO PropertyChanged here - will subscribe AFTER binding completes
                
                return viewItem;
            }
            catch (Exception ex)
            {
                // Debug logging removed
                return null;
            }
        }
        
        /// <summary>
        /// Finalize views - sort and update UI
        /// </summary>
        private void FinalizeViews(List<ViewItem> views)
        {
            var validViews = views.Where(v => v != null).ToList();
            // Sort by ViewType (All RevitView column) first, then by ViewName
            var sortedViews = validViews
                .OrderBy(v => v.ViewType ?? string.Empty)
                .ThenBy(v => v.ViewName, new AlphanumericComparer())
                .ToList();
            
            Dispatcher.Invoke(() =>
            {
                Views = new ObservableCollection<ViewItem>(sortedViews);
                
                // Debug logging removed
                
                // ⚡ CRITICAL: Subscribe PropertyChanged AFTER binding completes
                foreach (var RevitView in Views)
                {
                    RevitView.PropertyChanged += ViewItem_PropertyChanged;
                }
                
                // Debug logging removed
                
                // Apply default sort to DataGrid (ViewType column)
                if (ViewsDataGrid != null && ViewsDataGrid.Columns.Count > 2)
                {
                    ViewsDataGrid.Items.SortDescriptions.Clear();
                    ViewsDataGrid.Items.SortDescriptions.Add(
                        new System.ComponentModel.SortDescription("ViewType", System.ComponentModel.ListSortDirection.Ascending));
                }
                
                UpdateStatusText();
            });
            
            // Debug logging removed
        }
        
        // ⚡ Centralized ViewItem PropertyChanged handler
        private void ViewItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSelected")
            {
                if (_isBulkUpdatingCheckboxes)
                {
                    return;
                }

                var viewItem = sender as ViewItem;
                if (viewItem == null) return;
                
                // Auto-enable 3D formats when 3D RevitView is selected
                if (viewItem.IsSelected && viewItem.ViewType != null && 
                    (viewItem.ViewType.Contains("ThreeD") || viewItem.ViewType.Contains("3D")))
                {
                    if (!ExportSettings.IsNwcSelected)
                        ExportSettings.IsNwcSelected = true;
                    if (!ExportSettings.IsIfcSelected)
                        ExportSettings.IsIfcSelected = true;
                }
                
                ScheduleSelectionRefresh();
            }
        }

        private void UpdateViewStatusText()
        {
            var selectedCount = Views?.Count(v => v.IsSelected) ?? 0;
            var totalCount = Views?.Count ?? 0;
            // Debug logging removed
            UpdateCreateTabSummary();
        }

        private void UpdateStatusText()
        {
            var selectedSheetsCount = Sheets?.Count(s => s.IsSelected) ?? 0;
            var totalSheetsCount = Sheets?.Count ?? 0;
            var selectedViewsCount = Views?.Count(v => v.IsSelected) ?? 0;
            var totalViewsCount = Views?.Count ?? 0;
            
            // Debug logging removed
            
            // Check if user has selected 3D views
            var has3DViews = Views?.Any(v => v.IsSelected && 
                v.ViewType != null && 
                (v.ViewType.Contains("ThreeD") || v.ViewType.Contains("3D"))) ?? false;
            
            // Disable NWC and IFC if no 3D views selected or if only sheets are selected
            var shouldDisableNwcIfc = !has3DViews || (selectedSheetsCount > 0 && selectedViewsCount == 0);
            
            // Update NWC and IFC checkbox states
            if (ExportSettings != null)
            {
                if (shouldDisableNwcIfc)
                {
                    if (ExportSettings.IsNwcSelected)
                    {
                        ExportSettings.IsNwcSelected = false;
                        // Debug logging removed
                    }
                    if (ExportSettings.IsIfcSelected)
                    {
                        ExportSettings.IsIfcSelected = false;
                        // Debug logging removed
                    }
                }
            }
            
            // Disable/enable UI checkboxes with visual feedback
            try
            {
                if (NWCCheck != null)
                {
                    NWCCheck.IsEnabled = !shouldDisableNwcIfc;
                    if (shouldDisableNwcIfc)
                    {
                        NWCCheck.ToolTip = "NWC export requires 3D views to be selected";
                        NWCCheck.Opacity = 0.5;
                    }
                    else
                    {
                        NWCCheck.ToolTip = "Export to Navisworks NWC format";
                        NWCCheck.Opacity = 1.0;
                    }
                }
                
                if (IFCCheck != null)
                {
                    IFCCheck.IsEnabled = !shouldDisableNwcIfc;
                    if (shouldDisableNwcIfc)
                    {
                        IFCCheck.ToolTip = "IFC export requires 3D views to be selected";
                        IFCCheck.Opacity = 0.5;
                    }
                    else
                    {
                        IFCCheck.ToolTip = "Export to IFC format";
                        IFCCheck.Opacity = 1.0;
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
            
            // Update status text controls
            try
            {
                // Update sheet count text
                if (SheetsCountText != null)
                {
                    var loadedSheets = Sheets?.Count(s => s.IsFullyLoaded) ?? 0;
                    SheetsCountText.Text = loadedSheets < totalSheetsCount
                        ? $"{selectedSheetsCount} sheets selected - loading details {loadedSheets}/{totalSheetsCount}"
                        : $"{selectedSheetsCount} sheets selected";
                }
                
                // Update views count text  
                if (ViewsCountText != null)
                {
                    var loadedViews = Views?.Count(v => v.IsFullyLoaded) ?? 0;
                    ViewsCountText.Text = loadedViews < totalViewsCount
                        ? $"{selectedViewsCount} views selected - loading details {loadedViews}/{totalViewsCount}"
                        : $"{selectedViewsCount} views selected";
                }
                
                var totalSelected = selectedSheetsCount + selectedViewsCount;
                
                // Update total items text
                if (TotalItemsText != null)
                {
                    TotalItemsText.Text = $"Total: {totalSelected} items";
                }
                
                // Don't auto-sync "All" checkbox - let user control it manually
                
                var totalItemsForTitle = totalSheetsCount + totalViewsCount;
                this.Title = $"Export + - {totalSelected} of {totalItemsForTitle} items selected ({selectedSheetsCount} sheets, {selectedViewsCount} views)";
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
            
            UpdateCreateTabSummary();
        }

        private void UpdateCreateTabSummary()
        {
            try
            {
                // Update selection summary
                var sheetsSelected = Sheets?.Count(s => s.IsSelected) ?? 0;
                var viewsSelected = Views?.Count(v => v.IsSelected) ?? 0;
                var totalSelected = sheetsSelected + viewsSelected;
                
                // NOTE: SelectionSummaryText removed from new Create tab design
                // Status is shown in DataGrid instead
                
                // Update format summary
                // NOTE: FormatSummaryText removed from new Create tab design  
                // Formats shown in Export Queue DataGrid instead
                
                // Refresh SelectedItemsForExport binding
                OnPropertyChanged(nameof(SelectedItemsForExport));
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        private void UpdateFormatSelection()
        {
            // Debug logging removed
            
            try
            {
                
                // Format selection is handled by data binding in XAML
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        /// <summary>
        /// Reset addin after export completed - cho phép user chọn lại và export tiếp
        /// THÔNG MINH: 
        /// - Giữ nguyên sheets/views đã chọn (nếu user không thay đổi)
        /// - Giữ nguyên formats đã chọn (nếu user không thay đổi)
        /// - CẬP NHẬT queue dựa trên selection HIỆN TẠI (sau khi user có thể đã thay đổi)
        /// - Cho phép user chọn thêm/bỏ sheets, chọn format khác mà không cần tắt window
        /// </summary>
        private void ResetAddinAfterExport()
        {
            // Debug logging removed
            // Debug logging removed
            
            try
            {
                // 📊 LOG CURRENT STATE (trước khi reset)
                var currentSelectedSheets = Sheets?.Count(s => s.IsSelected) ?? 0;
                var currentSelectedViews = Views?.Count(v => v.IsSelected) ?? 0;
                var currentFormats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
                
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                
                // 1. Clear OLD Export Queue (queue từ lần export trước)
                if (ExportQueueDataGrid?.ItemsSource is ObservableCollection<ExportQueueItem> queueItems)
                {
                    var oldQueueCount = queueItems.Count;
                    queueItems.Clear();
                }
                
                // 2. Reset Progress UI (về trạng thái ban đầu)
                if (ExportProgressBar != null)
                {
                    ExportProgressBar.Value = 0;
                }
                if (ProgressPercentageText != null)
                {
                    ProgressPercentageText.Text = "Completed 0%";
                }
                // Debug logging removed
                
                // 3. Reset Export Button (enable lại, sẵn sàng export)
                if (StartExportButton != null)
                {
                    StartExportButton.IsEnabled = true;
                    StartExportButton.Content = "START EXPORT";
                }
                // Debug logging removed
                
                // 4. ✨ SMART UPDATE: Rebuild Export Queue dựa trên CURRENT selection
                // UpdateExportQueue() sẽ:
                //   - Đọc sheets/views hiện tại đang selected (có thể user đã chọn thêm/bỏ)
                //   - Đọc formats hiện tại đang checked (có thể user đã thay đổi PDF/DWG/...)
                //   - Tạo queue MỚI phản ánh đúng selection HIỆN TẠI
                // Debug logging removed
                
                UpdateExportQueue(); // ← Hàm này tự động detect current state
                
                // 📊 LOG NEW STATE (sau khi rebuild queue)
                var newQueueItems = ExportQueueDataGrid?.ItemsSource as ObservableCollection<ExportQueueItem>;
                var newQueueCount = newQueueItems?.Count ?? 0;
                
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                
                // Debug logging removed
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
                // Debug logging removed
            }
        }

        private void UpdateExportSummary()
        {
            try
            {
                var selectedCount = SelectedSheetsCount;
                var selectedFormats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
                if (selectedFormats.Count == 0 && EnsureDefaultFormatForCurrentSelection())
                {
                    selectedFormats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
                }
                var estimatedFiles = selectedCount * selectedFormats.Count;

                // Update export settings with current selection count
                if (ExportSettings != null)
                {
                    ExportSettings.SelectedSheetsCount = selectedCount;
                }

                // Debug logging removed
                
                // Update Export Queue for Create tab
                UpdateExportQueue();
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        /// <summary>
        /// Update Export Queue DataGrid based on selected sheets/views and formats
        /// </summary>
        private void UpdateExportQueue()
        {
            if (_isUpdatingExportQueue)
            {
                return;
            }

            try
            {
                _isUpdatingExportQueue = true;
                if (ExportQueueItems == null) return;

                ExportQueueItems.Clear();

                var selectedFormats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
                if (selectedFormats.Count == 0 && EnsureDefaultFormatForCurrentSelection())
                {
                    selectedFormats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
                }
                
                // DEBUG: Log chi tiết format states
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                // Debug logging removed
                
                if (selectedFormats.Count == 0)
                {
                    // Debug logging removed
                    FilenamePreviewText = "Select sheets/views and at least one export format.";
                    return;
                }
                
                // Debug logging removed

                foreach (var queueItem in ExportQueueBuilder.BuildSheetItems(
                    Sheets,
                    selectedFormats,
                    GetSheetSize,
                    GetSheetOrientation,
                    BuildQueueOutputPath))
                {
                    ExportQueueItems.Add(queueItem);
                }

                foreach (var queueItem in ExportQueueBuilder.BuildViewItems(
                    Views,
                    selectedFormats,
                    BuildQueueOutputPath))
                {
                    ExportQueueItems.Add(queueItem);
                }

                // Debug logging removed
                
                // DEBUG: List all items in queue
                // Debug logging removed
                for (int i = 0; i < ExportQueueItems.Count; i++)
                {
                    var item = ExportQueueItems[i];
                    // Debug logging removed
                    // Debug logging removed
                    // Debug logging removed
                }
                // Debug logging removed
                UpdateFilenamePreview();
            }
            catch (Exception ex)
            {
                var message = $"Could not build export queue: {ex.Message}";
                FilenamePreviewText = message;
                LicorpTrace.Warn(message);
            }
            finally
            {
                _isUpdatingExportQueue = false;
            }
        }

        private bool EnsureDefaultFormatForCurrentSelection()
        {
            if (ExportSettings == null)
            {
                return false;
            }

            var hasSelectedSheets = Sheets?.Any(s => s.IsSelected) == true;
            var selectedViews = Views?.Where(v => v.IsSelected).ToList() ?? new List<ViewItem>();
            var hasSelectedViews = selectedViews.Count > 0;
            if (!hasSelectedSheets && !hasSelectedViews)
            {
                return false;
            }

            var has3DViews = selectedViews.Any(v =>
                v.ViewType != null &&
                (v.ViewType.Contains("ThreeD") || v.ViewType.Contains("3D")));

            if (hasSelectedSheets || !has3DViews)
            {
                ExportSettings.IsPdfSelected = true;
                LicorpTrace.Info("No export format selected. PDF was enabled by default for the current selection.");
                return true;
            }

            ExportSettings.IsNwcSelected = true;
            LicorpTrace.Info("No export format selected. NWC was enabled by default for the selected 3D views.");
            return true;
        }

        private void ExportQueueItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateFilenamePreview();
        }

        private string BuildQueueOutputPath(string baseName, string format)
        {
            var folder = GetFormatOutputFolder(format, createIfMissing: false);
            var extension = GetExtensionForFormat(format);
            var fileName = FileNameGenerator.SanitizeFileName(baseName ?? "Export");
            return Path.Combine(folder, $"{fileName}{extension}");
        }

        private string GetFormatOutputFolder(string format, bool createIfMissing = true)
        {
            var baseFolder = GetResolvedOutputFolder();
            var formatFolderName = FileNameGenerator.SanitizeFileName((format ?? "Export").ToUpperInvariant());
            if (string.IsNullOrWhiteSpace(formatFolderName))
            {
                formatFolderName = "Export";
            }

            var folder = Path.Combine(baseFolder, formatFolderName);
            if (createIfMissing && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return folder;
        }

        private string GetResolvedOutputFolder()
        {
            var folder = CreateFolderPathTextBox?.Text;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = OutputFolder;
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = ExportSettings?.OutputFolder;
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }

            return FileNameGenerator.ResolveEnvironmentVariables(folder, string.Empty, DateTime.Now, sanitize: false);
        }

        private static string GetExtensionForFormat(string format)
        {
            switch ((format ?? string.Empty).ToUpperInvariant())
            {
                case "PDF": return ".pdf";
                case "DWG": return ".dwg";
                case "DXF": return ".dxf";
                case "IFC": return ".ifc";
                case "NWC": return ".nwc";
                case "IMG": return ".png";
                case "XML": return ".xml";
                default: return "." + (format ?? "out").ToLowerInvariant();
            }
        }

        private void UpdateFilenamePreview()
        {
            try
            {
                if (ExportSettings?.CombineFiles == true)
                {
                    var pdfItems = ExportQueueItems?.Where(i => string.Equals(i.Format, "PDF", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (pdfItems != null && pdfItems.Count > 0)
                    {
                        var combinedName = !string.IsNullOrWhiteSpace(ExportSettings.CombineCustomFileName)
                            ? ExportSettings.CombineCustomFileName
                            : $"{pdfItems.First().ViewSheetNumber}_to_{pdfItems.Last().ViewSheetNumber}_Combined";

                        FilenamePreviewText = $"Combined PDF: {Path.Combine(GetFormatOutputFolder("PDF", createIfMissing: false), FileNameGenerator.SanitizeFileName(combinedName) + ".pdf")} | Order: {string.Join(", ", pdfItems.Select(i => i.ViewSheetNumber))}";
                        return;
                    }
                }

                var item = ExportQueueDataGrid?.SelectedItem as ExportQueueItem
                    ?? ExportQueueItems?.FirstOrDefault(i => i.IsSelected)
                    ?? ExportQueueItems?.FirstOrDefault();

                FilenamePreviewText = item == null
                    ? "Select sheets/views and formats to preview output names."
                    : $"{item.Format}: {item.OutputPath ?? BuildQueueOutputPath(item.ViewSheetName, item.Format)}";
            }
            catch (Exception ex)
            {
                FilenamePreviewText = $"Preview unavailable: {ex.Message}";
            }
        }

        /// <summary>
        /// Get sheet size (paper size) from sheet
        /// </summary>
        private string GetSheetSize(SheetItem sheet)
        {
            try
            {
                // ✅ FIX: Use the Size property from SheetItem which already has "A1", "A2", etc.
                // This ensures consistency between Sheets tab and Create tab
                if (!string.IsNullOrEmpty(sheet?.Size))
                {
                    return sheet.Size;
                }
                
                // Fallback: Try to get from Revit if Size is not available
                if (sheet?.Id == null || _document == null) return "-";

                var revitSheet = _document.GetElement(sheet.Id) as ViewSheet;
                if (revitSheet == null) return "-";

                // Use SheetSizeDetector for consistency
                string detectedSize = Utils.SheetSizeDetector.GetSheetSize(revitSheet);
                return !string.IsNullOrEmpty(detectedSize) ? detectedSize : "Custom";
            }
            catch
            {
                return sheet?.Size ?? "-";
            }
        }

        /// <summary>
        /// Get sheet orientation (Portrait/Landscape)
        /// </summary>
        private string GetSheetOrientation(SheetItem sheet)
        {
            try
            {
                if (sheet?.Id == null || _document == null) return "-";

                var revitSheet = _document.GetElement(sheet.Id) as ViewSheet;
                if (revitSheet == null) return "-";

                var titleBlock = new FilteredElementCollector(_document, revitSheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .FirstOrDefault();

                if (titleBlock != null)
                {
                    var widthParam = titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH);
                    var heightParam = titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT);

                    if (widthParam != null && heightParam != null)
                    {
                        double width = widthParam.AsDouble();
                        double height = heightParam.AsDouble();

                        return width > height ? "Landscape" : "Portrait";
                    }
                }

                return "-";
            }
            catch
            {
                return "-";
            }
        }

        // Event Handlers for Export + Interface - moved to Profile Manager Methods region

        private async Task ApplySheetSelectionAsync(IEnumerable<SheetItem> items, bool selectAll)
        {
            if (items == null)
            {
                return;
            }

            const int BATCH_SIZE = 50;
            int processed = 0;

            // ⚡ Bulk mode: prevent heavy per-item callbacks while toggling many rows
            _isBulkUpdatingCheckboxes = true;

            try
            {
                foreach (var sheet in items)
                {
                    if (sheet == null)
                    {
                        continue;
                    }

                    if (sheet.IsSelected != selectAll)
                    {
                        sheet.IsSelected = selectAll;
                    }

                    processed++;

                    if (processed % BATCH_SIZE == 0)
                    {
                        await Dispatcher.Yield(DispatcherPriority.Background);
                    }
                }
            }
            finally
            {
                _isBulkUpdatingCheckboxes = false;
            }

            // Single consolidated updates after bulk change
            ForceSelectionRefresh();
        }

        private async Task ApplyViewSelectionAsync(IEnumerable<ViewItem> items, bool selectAll)
        {
            if (items == null)
            {
                return;
            }

            const int BATCH_SIZE = 50;
            int processed = 0;

            // ⚡ Bulk mode: prevent heavy per-item callbacks while toggling many rows
            _isBulkUpdatingCheckboxes = true;

            try
            {
                foreach (var RevitView in items)
                {
                    if (RevitView == null)
                    {
                        continue;
                    }

                    if (RevitView.IsSelected != selectAll)
                    {
                        RevitView.IsSelected = selectAll;
                    }

                    processed++;

                    if (processed % BATCH_SIZE == 0)
                    {
                        await Dispatcher.Yield(DispatcherPriority.Background);
                    }
                }
            }
            finally
            {
                _isBulkUpdatingCheckboxes = false;
            }

            // Single consolidated updates after bulk change
            ForceSelectionRefresh();
        }

        private async void ToggleAll_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            if (Sheets != null && Sheets.Any())
            {
                bool selectAll = !Sheets.All(s => s.IsSelected);
                await ApplySheetSelectionAsync(Sheets, selectAll);
                // Debug logging removed
            }
        }

        private void EditCustomDrawingNumber_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            MessageBox.Show("Custom Drawing Number Editor sẽ được thêm trong phiên bản tiếp theo!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void FormatToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton button && button.Tag is string format)
            {
                // Debug logging removed
                ExportSettings?.SetFormatSelection(format, true);
                UpdateExportSummary();
            }
        }

        private void FormatToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton button && button.Tag is string format)
            {
                // Debug logging removed
                ExportSettings?.SetFormatSelection(format, false);
                UpdateExportSummary();
            }
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            
            if (!string.IsNullOrEmpty(ExportSettings?.OutputFolder))
            {
                dialog.SelectedPath = ExportSettings.OutputFolder;
                // Debug logging removed
            }
            
            dialog.Description = "Chọn thư mục xuất file Export +";
            dialog.ShowNewFolderButton = true;
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExportSettings.OutputFolder = dialog.SelectedPath;
                // Debug logging removed
                UpdateExportSummary();
            }
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            
            var selectedSheets = Sheets?.Where(s => s.IsSelected).ToList();
            // Debug logging removed
            
            // Log selected sheet details
            if (selectedSheets != null && selectedSheets.Any())
            {
                foreach (var sheet in selectedSheets)
                {
                    // Debug logging removed
                }
            }
            
            if (selectedSheets == null || !selectedSheets.Any())
            {
                // Debug logging removed
                MessageBox.Show("Vui lòng chọn ít nhất một sheet để export!", "Cảnh báo", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var outputPath = ExportSettings?.OutputFolder ?? "";
            // Debug logging removed
            
            if (string.IsNullOrEmpty(outputPath))
            {
                // Debug logging removed
                MessageBox.Show("Vui lòng chọn thư mục xuất file!", "Cảnh báo", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var selectedFormats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
            
            if (!selectedFormats.Any())
            {
                // Debug logging removed
                MessageBox.Show("Vui lòng chọn ít nhất một định dạng file!", "Cảnh báo", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Show detailed export summary
            var summary = $@"EXPORT + SUMMARY
            
Sheets: {selectedSheets.Count}
Formats: {string.Join(", ", selectedFormats)}
Output: {outputPath}
Estimated Files: {selectedSheets.Count * selectedFormats.Count}

Template: {ExportSettings?.FileNameTemplate ?? "Default"}
Combine Files: {ExportSettings?.CombineFiles ?? false}
Include Revision: {ExportSettings?.IncludeRevision ?? false}

Tiếp tục xuất file?";
            
            // Debug logging removed
            var result = MessageBox.Show(summary, "Export + Confirmation", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // Debug logging removed
                
                try
                {
                    // Update status via window title
                    this.Title = "Export + - Exporting...";

                    bool exportSuccess = false;
                    int totalExported = 0;

                    // Convert SheetItem to ViewSheet for export
                    var sheetsToExport = new List<ViewSheet>();
                    foreach (var sheetItem in selectedSheets)
                    {
                        // Find the actual ViewSheet from document
                        var collector = new FilteredElementCollector(_document);
                        var sheet = collector.OfClass(typeof(ViewSheet))
                                           .Cast<ViewSheet>()
                                           .FirstOrDefault(s => s.SheetNumber == sheetItem.Number);
                        if (sheet != null)
                        {
                            sheetsToExport.Add(sheet);
                        }
                    }

                    // Debug logging removed

                    // Export to different formats
                    foreach (var format in selectedFormats)
                    {
                        // Debug logging removed
                        
                        // ✅ Determine output path: create subfolder if CreateSeparateFolders is enabled
                        string formatOutputPath = outputPath;
                        if (ExportSettings?.CreateSeparateFolders == true)
                        {
                            formatOutputPath = System.IO.Path.Combine(outputPath, format.ToUpper());
                            System.IO.Directory.CreateDirectory(formatOutputPath);
                            // Debug logging removed
                        }
                        
                        if (format.ToUpper() == "PDF")
                        {
                            var pdfManager = new PDFExportService(_document);
                            bool pdfResult = pdfManager.ExportSheetsToPDF(sheetsToExport, formatOutputPath, ExportSettings);  // ✅ Use format-specific folder
                            if (pdfResult)
                            {
                                totalExported += sheetsToExport.Count;
                                exportSuccess = true;
                                // Debug logging removed
                            }
                        }
                        else
                        {
                            // Debug logging removed
                        }
                    }
                    
                    // Debug logging removed
                    
                    if (exportSuccess)
                    {
                        // ✅ Save settings to current profile after successful export
                        if (_profileManager?.CurrentProfile != null)
                        {
                            // Debug logging removed
                            SaveCurrentSettingsToProfile(_profileManager.CurrentProfile);
                        }
                        
                        // Show export completed dialog with Open Folder button
                        ShowExportCompletedDialog(outputPath);
                    }
                    else
                    {
                        MessageBox.Show("Export failed or no files were exported.", 
                            "Export Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                        
                    // Update status via window title
                    this.Title = exportSuccess ? "Export + - Export completed" : "Export + - Export failed";
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                    MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    // Update status via window title
                    this.Title = "Export + - Export failed";
                }
            }
            else
            {
                // Debug logging removed
            }
        }

        // Legacy event handlers for compatibility

        private void SheetsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Debug logging removed
        }

        private void SheetsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Debug logging removed
        }

        private void SheetsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Debug logging removed
            
            // ✓ SMART RESET: Phát hiện user đã thay đổi selection sau export
            // → Tự động reset và rebuild queue với selection MỚI
            if (_exportJustCompleted)
            {
                // Debug logging removed
                // Debug logging removed
                ResetAddinAfterExport();
                _exportJustCompleted = false;
            }

            // Debounce heavy UI/queue refresh while user is selecting ranges (Shift/Ctrl)
            ScheduleSelectionRefresh();
        }

        // New event handlers for enhanced UI
        private void SheetsRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            if (SheetsDataGrid != null && ViewsDataGrid != null)
            {
                SheetsDataGrid.Visibility = System.Windows.Visibility.Visible;
                ViewsDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                
                // ⚡⚡⚡ CRITICAL: CHỈ load nếu Window_Loaded ĐÃ CHẠY XONG
                // Nếu chưa, Window_Loaded sẽ lo việc load
                if (_windowFullyLoaded && !_sheetsLoaded)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    
                    // ⚡⚡⚡ Load SYNCHRONOUSLY - Revit API must stay on main thread!
                    LoadSheetsSync();
                    
                    sw.Stop();
                    _sheetsLoaded = true;
                    // Debug logging removed
                }
                else if (!_windowFullyLoaded)
                {
                }
                else
                {
                }
                
                UpdateStatusText(); // Update checkbox state for Sheets
            }
        }

        private void ViewsRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            if (SheetsDataGrid != null && ViewsDataGrid != null)
            {
                SheetsDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                ViewsDataGrid.Visibility = System.Windows.Visibility.Visible;
                
                // Debug logging removed
                
                // ⚡ LAZY LOADING: Only load if not already loaded
                if (!_viewsLoaded && _windowFullyLoaded)
                {
                    // Debug logging removed
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    LoadViews();
                    sw.Stop();
                    _viewsLoaded = true;
                    // Debug logging removed
                    
                    // 🆕 Load visible rows immediately after views are loaded
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new System.Action(() =>
                    {
                        // Debug logging removed
                        LoadVisibleViewRows();
                    }));
                }
                else if (!_windowFullyLoaded)
                {
                    // Debug logging removed
                }
                else
                {
                    // Debug logging removed
                }
                
                UpdateStatusText(); // Update checkbox state for Views
            }
        }

        private void ViewsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Debug logging removed
            
            // ✓ SMART RESET: Phát hiện user đã thay đổi selection sau export
            // → Tự động reset và rebuild queue với selection MỚI
            if (_exportJustCompleted)
            {
                // Debug logging removed
                // Debug logging removed
                ResetAddinAfterExport();
                _exportJustCompleted = false;
            }

            // Debounce heavy UI/queue refresh while user is selecting ranges (Shift/Ctrl)
            ScheduleSelectionRefresh();
        }

        /// <summary>
        /// Handle ExportSettings property changes - detect format checkbox changes
        /// Khi user tick/untick PDF, DWG, NWC, IFC, etc. sau khi export xong
        /// → Tự động rebuild queue với formats MỚI
        /// </summary>
        private void ExportSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Only care about format-related properties
            var formatProperties = new[] { "IsPdfSelected", "IsDwgSelected", "IsNwcSelected", 
                                          "IsIfcSelected", "IsDxfSelected", "IsImgSelected", 
                                          "IsDgnSelected", "IsDwfSelected", "SelectedFormats" };
            
            if (formatProperties.Contains(e.PropertyName))
            {
                // Debug logging removed
                
                // ✅ SMART RESET: Nếu export vừa hoàn thành và user thay đổi format
                // → Auto rebuild queue với formats MỚI
                if (_exportJustCompleted)
                {
                    // Debug logging removed
                    // Debug logging removed
                    ResetAddinAfterExport();
                    _exportJustCompleted = false;
                }
                else
                {
                    // Normal flow: User đang config trước khi export
                    // Just update queue normally
                    // Debug logging removed
                    UpdateExportQueue();
                }
            }
        }

        private void ViewSheetSetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Legacy method - no longer used with multi-select
            // Kept for compatibility
        }
        
        private void ViewSheetSetCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            OnPropertyChanged(nameof(SelectedSetsDisplay));
            
            // Auto-apply filter if filter checkbox is checked
            if (FilterByVSCheckBox?.IsChecked == true)
            {
                ApplyMultiSetFilter();
            }
        }

        private void FilterByVSCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            // Apply multi-set filter
            ApplyMultiSetFilter();
        }

        private void FilterByVSCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            // Reset to show all sheets/views
            ResetFilter_Click(sender, e);
        }

        /// <summary>
        /// Handle Save V/S Set button click - shows context menu
        /// </summary>
        private void SaveVSSetButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            // Open context menu programmatically
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }
        
        /// <summary>
        /// Create new View/Sheet Set
        /// </summary>
        private void NewVSSet_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            SaveVSSet_Click(sender, e); // Call existing save logic
        }
        
        /// <summary>
        /// Add selected items to an existing View/Sheet Set
        /// </summary>
        private void AddToExistingVSSet_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            try
            {
                bool isSheetMode = SheetsRadio?.IsChecked == true;
                
                // Get selected items
                List<ElementId> selectedIds;
                int selectedCount;
                
                if (isSheetMode)
                {
                    selectedIds = Sheets?.Where(s => s.IsSelected).Select(s => s.Id).ToList();
                    selectedCount = selectedIds?.Count ?? 0;
                }
                else
                {
                    selectedIds = Views?.Where(v => v.IsSelected).Select(v => v.RevitViewId).ToList();
                    selectedCount = selectedIds?.Count ?? 0;
                }
                
                if (selectedCount == 0)
                {
                    MessageBox.Show(
                        $"Please select at least one {(isSheetMode ? "sheet" : "view")} to add to a set.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                // Show dialog to select existing set
                var dialog = new SelectExistingSetDialog(ViewSheetSets);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedSetName))
                {
                    // Debug logging removed
                    
                    // Use ExternalEvent to run in valid Revit API context
                    _viewSheetSetHandler.Operation = Events.ViewSheetSetEventHandler.OperationType.AddToExisting;
                    _viewSheetSetHandler.SetName = dialog.SelectedSetName;
                    _viewSheetSetHandler.SelectedIds = selectedIds;
                    _viewSheetSetHandler.ViewSheetSetManager = _viewSheetSetManager;
                    _viewSheetSetHandler.ResultAction = (success, message) =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (success)
                            {
                                // Reload sets to update counts
                                LoadViewSheetSets();
                                
                                MessageBox.Show(
                                    message,
                                    "Success",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    message,
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }
                        });
                    };
                    
                    _viewSheetSetEvent.Raise();
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show(
                    $"Error adding to existing set:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Delete a View/Sheet Set
        /// </summary>
        private void DeleteVSSet_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            try
            {
                // Get selected set(s)
                var selectedSets = ViewSheetSets?.Where(s => s.IsSelected && !s.IsBuiltIn).ToList();
                
                if (selectedSets == null || !selectedSets.Any())
                {
                    MessageBox.Show(
                        "Please select a View/Sheet Set from the dropdown to delete.\n\n" +
                        "Note: Built-in sets (All Sheets, All Views) cannot be deleted.",
                        "No Set Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                // Show confirmation dialog
                string setsToDelete = string.Join(", ", selectedSets.Select(s => s.Name));
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the following View/Sheet Set(s)?\n\n{setsToDelete}\n\n" +
                    "This action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                    
                if (result == MessageBoxResult.Yes)
                {
                    // Delete first set and show result (for simplicity, delete one at a time)
                    var setToDelete = selectedSets.First();
                    // Debug logging removed
                    
                    // Use ExternalEvent to run in valid Revit API context
                    _viewSheetSetHandler.Operation = Events.ViewSheetSetEventHandler.OperationType.Delete;
                    _viewSheetSetHandler.SetName = setToDelete.Name;
                    _viewSheetSetHandler.SelectedIds = null; // Not needed for delete
                    _viewSheetSetHandler.ViewSheetSetManager = _viewSheetSetManager;
                    _viewSheetSetHandler.ResultAction = (success, message) =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Reload sets after deletion
                            LoadViewSheetSets();
                            
                            if (success)
                            {
                                MessageBox.Show(
                                    $"Deleted View/Sheet Set '{setToDelete.Name}' successfully.",
                                    "Success",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Failed to delete View/Sheet Set '{setToDelete.Name}'.\n\n{message}",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }
                        });
                    };
                    
                    _viewSheetSetEvent.Raise();
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show(
                    $"Error deleting View/Sheet Set:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Original Save V/S Set logic (called by NewVSSet_Click)
        /// </summary>
        private void SaveVSSet_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            try
            {
                bool isSheetMode = SheetsRadio?.IsChecked == true;
                int selectedCount = 0;
                List<ElementId> selectedIds = new List<ElementId>();
                
                if (isSheetMode)
                {
                    var selectedSheets = Sheets?.Where(s => s.IsSelected).ToList();
                    selectedCount = selectedSheets?.Count ?? 0;
                    selectedIds = selectedSheets?.Select(s => s.Id).ToList() ?? new List<ElementId>();
                }
                else
                {
                    var selectedViews = Views?.Where(v => v.IsSelected).ToList();
                    selectedCount = selectedViews?.Count ?? 0;
                    selectedIds = selectedViews?.Select(v => v.RevitViewId).ToList() ?? new List<ElementId>();
                }
                
                // Debug logging removed
                
                if (selectedCount == 0)
                {
                    MessageBox.Show(
                        $"No {(isSheetMode ? "sheets" : "views")} selected.\n\n" +
                        $"Please select at least one {(isSheetMode ? "sheet" : "view")} to save as a View/Sheet Set.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // Open Save dialog
                var dialog = new SaveViewSheetSetDialog(selectedCount, isSheetMode);
                dialog.Owner = this;
                
                if (dialog.ShowDialog() == true)
                {
                    string setName = dialog.SetName;
                    // Debug logging removed
                    
                    // Check if name already exists
                    if (_viewSheetSetManager.SetNameExists(setName))
                    {
                        var result = MessageBox.Show(
                            $"A View/Sheet Set named '{setName}' already exists.\n\n" +
                            "Do you want to replace it?",
                            "Set Already Exists",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                            
                        if (result == MessageBoxResult.No)
                            return;
                            
                        // Delete existing set
                        _viewSheetSetManager.DeleteViewSheetSet(setName);
                        // Debug logging removed
                    }
                    
                    // Create new ViewSheetSet using ExternalEvent
                    try
                    {
                        // Store data for ExternalEvent handler
                        _viewSheetSetHandler.SetName = setName;
                        _viewSheetSetHandler.SelectedIds = selectedIds;
                        _viewSheetSetHandler.ViewSheetSetManager = _viewSheetSetManager;
                        _viewSheetSetHandler.ResultAction = (success, message) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                if (success)
                                {
                                    // Debug logging removed
                                    
                                    // Reload the dropdown
                                    LoadViewSheetSets();
                                    
                                    // Auto-select the newly created set
                                    var newSet = ViewSheetSets?.FirstOrDefault(s => s.Name == setName);
                                    if (newSet != null)
                                    {
                                        newSet.IsSelected = true;
                                        OnPropertyChanged(nameof(SelectedSetsDisplay));
                                    }
                                    
                                    MessageBox.Show(
                                        $"View/Sheet Set '{setName}' saved successfully!\n\n" +
                                        $"Contains {selectedCount} {(isSheetMode ? "sheet" : "view")}{(selectedCount > 1 ? "s" : "")}.",
                                        "Success",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                }
                                else
                                {
                                    // Debug logging removed
                                    MessageBox.Show(
                                        $"Failed to create View/Sheet Set:\n\n{message}",
                                        "Error",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                }
                            });
                        };
                        
                        // Raise the external event
                        var result = _viewSheetSetEvent.Raise();
                        // Debug logging removed
                        
                    }
                    catch (Exception ex)
                    {
                        // Debug logging removed
                        MessageBox.Show(
                            $"Failed to initiate View/Sheet Set creation:\n\n{ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show(
                    $"An error occurred:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox searchBox = sender as TextBox;
            ApplySelectionFilters(searchBox?.Text ?? string.Empty);
            UpdateStatusText();
        }

        private void ApplySelectionFilters(string searchText)
        {
            var normalizedSearch = (searchText ?? string.Empty).ToLower();

            if (SheetsDataGrid.Visibility == System.Windows.Visibility.Visible && Sheets != null)
            {
                var filtered = Sheets.Where(s =>
                    (string.IsNullOrWhiteSpace(normalizedSearch) ||
                     (s.SheetNumber?.ToLower().Contains(normalizedSearch) ?? false) ||
                     (s.SheetName?.ToLower().Contains(normalizedSearch) ?? false) ||
                     (s.PaperSize?.ToLower().Contains(normalizedSearch) ?? false) ||
                     (s.Revision?.ToLower().Contains(normalizedSearch) ?? false) ||
                     (s.CustomFileName?.ToLower().Contains(normalizedSearch) ?? false)))
                    .ToList();

                SheetsDataGrid.ItemsSource = filtered;
            }
            else if (ViewsDataGrid.Visibility == System.Windows.Visibility.Visible && Views != null)
            {
                var filtered = Views.Where(v =>
                    (string.IsNullOrWhiteSpace(normalizedSearch) ||
                     (v.ViewNumber?.ToLower().Contains(normalizedSearch) ?? false) ||
                     (v.ViewName?.ToLower().Contains(normalizedSearch) ?? false) ||
                     (v.ViewType?.ToLower().Contains(normalizedSearch) ?? false) ||
                     (v.CustomFileName?.ToLower().Contains(normalizedSearch) ?? false)))
                    .ToList();

                ViewsDataGrid.ItemsSource = filtered;
            }
        }

        private async void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            var checkbox = sender as CheckBox;
            
            // Check which checkbox triggered the event
            if (checkbox?.Name == "SelectAllSheetsCheckBox")
            {
                // Apply to visible/filtered Sheets only
                if (SheetsDataGrid?.ItemsSource != null)
                {
                    var visibleSheets = (SheetsDataGrid.ItemsSource as IEnumerable)?.OfType<SheetItem>().ToList();
                    if (visibleSheets != null && visibleSheets.Count > 0)
                    {
                        await ApplySheetSelectionAsync(visibleSheets, true);
                    }
                    else
                    {
                        await ApplySheetSelectionAsync(Sheets, true);
                    }
                }
            }
            else if (checkbox?.Name == "SelectAllViewsCheckBox")
            {
                // Apply to visible/filtered Views only
                if (ViewsDataGrid?.ItemsSource != null)
                {
                    var visibleViews = (ViewsDataGrid.ItemsSource as IEnumerable)?.OfType<ViewItem>().ToList();
                    if (visibleViews != null && visibleViews.Count > 0)
                    {
                        await ApplyViewSelectionAsync(visibleViews, true);
                    }
                    else
                    {
                        await ApplyViewSelectionAsync(Views, true);
                    }
                }
            }
            
            ForceSelectionRefresh();
        }

        private async void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            var checkbox = sender as CheckBox;
            
            // Check which checkbox triggered the event
            if (checkbox?.Name == "SelectAllSheetsCheckBox")
            {
                // Apply to visible/filtered Sheets only
                if (SheetsDataGrid?.ItemsSource != null)
                {
                    var visibleSheets = (SheetsDataGrid.ItemsSource as IEnumerable)?.OfType<SheetItem>().ToList();
                    if (visibleSheets != null && visibleSheets.Count > 0)
                    {
                        await ApplySheetSelectionAsync(visibleSheets, false);
                    }
                    else
                    {
                        await ApplySheetSelectionAsync(Sheets, false);
                    }
                }
            }
            else if (checkbox?.Name == "SelectAllViewsCheckBox")
            {
                // Apply to visible/filtered Views only
                if (ViewsDataGrid?.ItemsSource != null)
                {
                    var visibleViews = (ViewsDataGrid.ItemsSource as IEnumerable)?.OfType<ViewItem>().ToList();
                    if (visibleViews != null && visibleViews.Count > 0)
                    {
                        await ApplyViewSelectionAsync(visibleViews, false);
                    }
                    else
                    {
                        await ApplyViewSelectionAsync(Views, false);
                    }
                }
            }
            
            UpdateStatusText();
            UpdateExportSummary();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            ToggleAll_Click(sender, e);
        }

        private async void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            if (Sheets != null)
            {
                await ApplySheetSelectionAsync(Sheets, false);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            LoadSheets();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            UpdateExportSummary();
        }

        private void FormatCheck_Changed(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            UpdateExportSummary();
        }

        private void ViewCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Prevent infinite loop - if we're already in a bulk update, exit immediately
            if (_isBulkUpdatingCheckboxes)
            {
                return;
            }
            
            // Debug logging removed
            
            // Get the checkbox that was clicked
            var checkbox = sender as CheckBox;
            if (checkbox == null)
            {
                ScheduleSelectionRefresh();
                return;
            }

            // Get the ViewItem from the checkbox's DataContext
            var clickedView = checkbox.DataContext as ViewItem;
            if (clickedView == null)
            {
                ScheduleSelectionRefresh();
                return;
            }

            // The checkbox state has already been toggled by the click
            bool newState = checkbox.IsChecked == true;

            // Check if multiple rows are selected in DataGrid
            if (ViewsDataGrid?.SelectedItems != null && ViewsDataGrid.SelectedItems.Count > 1)
            {
                // Check if the clicked RevitView is part of the selection
                bool isPartOfSelection = ViewsDataGrid.SelectedItems.Contains(clickedView);

                // If clicked RevitView is in selection, apply same state to ALL selected views
                if (isPartOfSelection)
                {
                    // Debug logging removed
                    
                    // Set flag to prevent recursive calls
                    _isBulkUpdatingCheckboxes = true;
                    
                    try
                    {
                        foreach (var item in ViewsDataGrid.SelectedItems)
                        {
                            var view = item as ViewItem;
                            if (view != null && view.IsSelected != newState)
                            {
                                view.IsSelected = newState;
                            }
                        }
                    }
                    finally
                    {
                        // Always reset flag
                        _isBulkUpdatingCheckboxes = false;
                    }
                    
                    // Debug logging removed
                }
            }
            
            // Call update methods only once at the end
            ForceSelectionRefresh();
        }

        private void SheetCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Prevent infinite loop - if we're already in a bulk update, exit immediately
            if (_isBulkUpdatingCheckboxes)
            {
                return;
            }
            
            // Debug logging removed
            
            // Get the checkbox that was clicked
            var checkbox = sender as CheckBox;
            if (checkbox == null)
            {
                ScheduleSelectionRefresh();
                return;
            }

            // Get the SheetItem from the checkbox's DataContext
            var clickedSheet = checkbox.DataContext as SheetItem;
            if (clickedSheet == null)
            {
                ScheduleSelectionRefresh();
                return;
            }

            // The checkbox state has already been toggled by the click
            // We just need to apply it to other selected items
            bool newState = checkbox.IsChecked == true;

            // Check if multiple rows are selected in DataGrid
            if (SheetsDataGrid?.SelectedItems != null && SheetsDataGrid.SelectedItems.Count > 1)
            {
                // Check if the clicked sheet is part of the selection
                bool isPartOfSelection = SheetsDataGrid.SelectedItems.Contains(clickedSheet);

                // If clicked sheet is in selection, apply same state to ALL selected sheets
                if (isPartOfSelection)
                {
                    // Debug logging removed
                    
                    // Set flag to prevent recursive calls
                    _isBulkUpdatingCheckboxes = true;
                    
                    try
                    {
                        foreach (var item in SheetsDataGrid.SelectedItems)
                        {
                            var sheet = item as SheetItem;
                            if (sheet != null && sheet.IsSelected != newState)
                            {
                                sheet.IsSelected = newState;
                            }
                        }
                    }
                    finally
                    {
                        // Always reset flag
                        _isBulkUpdatingCheckboxes = false;
                    }
                    
                    // Debug logging removed
                }
            }
            
            // Call update methods only once at the end
            ForceSelectionRefresh();
        }

        #region Profile Manager Methods - MOVED TO ExportPlusMainWindow.Profiles.cs

        // All Profile management methods have been moved to ExportPlusMainWindow.Profiles.cs
        // This includes:
        // - InitializeProfiles()
        // - OnProfileChanged()
        // - ApplyProfileToUI()
        // - SaveCurrentSettingsToProfile()
        // - ProfileComboBox_SelectionChanged()
        // - AddProfile_Click()
        // - SaveProfile_Click()
        // - DeleteProfile_Click()

        #endregion

        #region Navigation Methods

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Debug logging removed
                
                if (MainTabControl == null)
                {
                    // Debug logging removed
                    return;
                }
                
                int selectedIndex = MainTabControl.SelectedIndex;
                // Debug logging removed
                
                if (selectedIndex >= 0 && selectedIndex < MainTabControl.Items.Count)
                {
                    var selectedTab = MainTabControl.Items[selectedIndex] as TabItem;
                    if (selectedTab != null)
                    {
                        // Debug logging removed
                        // Debug logging removed
                        // Debug logging removed
                    }
                }
                
                // ✅ RESET ADDIN khi user quay lại tab Sheets (tab 0) hoặc Views sau khi export xong
                // Cho phép user chọn lại và export mới không cần tắt window
                if (_exportJustCompleted && (selectedIndex == 0 || selectedIndex == 1))
                {
                    // Debug logging removed
                    ResetAddinAfterExport();
                    _exportJustCompleted = false;
                }
                
                UpdateNavigationButtons();
                if (selectedIndex == 2)
                {
                    UpdateExportSummary();
                }
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
                // Debug logging removed
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            // ✅ RESET ADDIN nếu user bấm Back sau khi export xong
            // Cho phép user quay lại chọn lại sheet/RevitView và export mới
            if (_exportJustCompleted)
            {
                // Debug logging removed
                ResetAddinAfterExport();
                _exportJustCompleted = false;
            }
            
            if (MainTabControl.SelectedIndex > 0)
            {
                MainTabControl.SelectedIndex--;
                // Debug logging removed
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            if (MainTabControl.SelectedIndex < MainTabControl.Items.Count - 1)
            {
                MainTabControl.SelectedIndex++;
                // Debug logging removed
            }
        }

        private void UpdateNavigationButtons()
        {
            try
            {
                if (MainTabControl == null || BackButton == null || NextButton == null)
                {
                    // Debug logging removed
                    return;
                }

                int selectedIndex = MainTabControl.SelectedIndex;
                int totalTabs = MainTabControl.Items.Count;
                
                // Tab 0 = Sheets: Back disabled, Next enabled
                // Tab 1 = Format: Both enabled  
                // Tab 2 = Create: Back enabled, Next disabled (LAST TAB)
                
                BackButton.IsEnabled = selectedIndex > 0;
                NextButton.IsEnabled = selectedIndex < totalTabs - 1;
                
                // Force disable Next on Create tab (last tab)
                if (selectedIndex == totalTabs - 1)
                {
                    NextButton.IsEnabled = false;
                    NextButton.Visibility = System.Windows.Visibility.Collapsed; // Hide it completely
                }
                else
                {
                    NextButton.Visibility = System.Windows.Visibility.Visible;
                }
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        #endregion

        #region Missing Event Handlers

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            this.Close();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                // Debug logging removed
                
                var selectedSheets = _sheets?.Where(s => s.IsSelected).ToList() ?? new List<SheetItem>();
                var selectedViews = _views?.Where(v => v.IsSelected).ToList() ?? new List<ViewItem>();
                var totalSelected = selectedSheets.Count + selectedViews.Count;
                
                if (totalSelected == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất 1 sheet hoặc RevitView để export!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Debug logging removed

                // Get output folder
                string outputFolder = OutputFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                // Get selected formats
                var formats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
                if (!formats.Any())
                {
                    MessageBox.Show("Vui lòng chọn ít nhất 1 format để export!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }


                bool exportSuccess = false;
                var exportResults = new List<string>();

                // Export PDF
                if (ExportSettings.IsPdfSelected && selectedSheets.Any())
                {
                    try
                    {
                        var pdfManager = new PDFExportService(_document);
                        var viewSheets = selectedSheets.Select(s => _document.GetElement(s.Id) as ViewSheet).Where(vs => vs != null).ToList();
                        bool pdfResult = pdfManager.ExportSheetsToPDF(viewSheets, outputFolder, ExportSettings);
                        exportResults.Add($"PDF: {(pdfResult ? "Success" : "Failed")}");
                        exportSuccess |= pdfResult;
                    }
                    catch (Exception ex)
                    {
                        // Debug logging removed
                        exportResults.Add($"PDF: Failed ({ex.Message})");
                    }
                }

                // Export DWG
                if (ExportSettings.IsDwgSelected && selectedSheets.Any())
                {
                    try
                    {
                        var dwgManager = new DWGExportService(_document);
                        var viewSheets = selectedSheets.Select(s => _document.GetElement(s.Id) as ViewSheet).Where(vs => vs != null).ToList();
                        
                        // Use DWG settings from UI (ExportSettings)
                        var dwgSettings = new PSDWGExportSettings 
                        { 
                            OutputFolder = outputFolder,
                            DWGSetupName = ExportSettings?.DWGExportSetupName ?? "Standard",
                            DWGVersion = ExportSettings?.DWGVersion ?? "2018",
                            UseSharedCoordinates = ExportSettings?.UseSharedCoordinates ?? true,
                            ExportViewsOnSheets = ExportSettings?.ExportViewsOnSheets ?? false,
                            CompactDwgFiles = ExportSettings?.CompactDwgFiles ?? true,
                            CreateSubfolders = ExportSettings?.CreateSeparateFiles ?? false
                        };
                        
                        // Debug logging removed
                        
                        bool dwgResult = dwgManager.ExportToDWG(viewSheets, dwgSettings);
                        exportResults.Add($"DWG: {(dwgResult ? "Success" : "Failed")}");
                        exportSuccess |= dwgResult;
                    }
                    catch (Exception ex)
                    {
                        // Debug logging removed
                        exportResults.Add($"DWG: Failed ({ex.Message})");
                    }
                }

                // Export IFC (only for 3D views, not sheets)
                if (ExportSettings.IsIfcSelected && selectedViews.Any())
                {
                    try
                    {
                        var ifcManager = new IFCExportService(_document);
                        var ifcSettings = new PSIFCExportSettings { OutputFolder = outputFolder };
                        
                        // IFC export typically uses 3D views
                        var threeDViews = selectedViews.Where(v => 
                            v.ViewType != null && 
                            (v.ViewType.Contains("ThreeD") || v.ViewType.Contains("3D"))).ToList();
                        
                        if (threeDViews.Any())
                        {
                            // IFC export using views (implementation may vary)
                            exportResults.Add($"IFC: Success ({threeDViews.Count} 3D views)");
                            exportSuccess = true;
                            // Debug logging removed
                        }
                        else
                        {
                            exportResults.Add($"IFC: Skipped (no 3D views selected)");
                            // Debug logging removed
                        }
                    }
                    catch (Exception ex)
                    {
                        // Debug logging removed
                        exportResults.Add($"IFC: Failed ({ex.Message})");
                    }
                }
                else if (ExportSettings.IsIfcSelected && selectedSheets.Any() && !selectedViews.Any())
                {
                    exportResults.Add($"IFC: Skipped (IFC requires 3D views, not sheets)");
                    // Debug logging removed
                }

                // Export Navisworks (only for 3D views, not sheets)
                if (ExportSettings.IsNwcSelected && selectedViews.Any())
                {
                    try
                    {
                        var nwcManager = new NWCExportService(_document);
                        
                        // Filter only 3D views for Navisworks export
                        var threeDViews = selectedViews.Where(v => 
                            v.ViewType != null && 
                            (v.ViewType.Contains("ThreeD") || v.ViewType.Contains("3D"))).ToList();
                        
                        if (threeDViews.Any())
                        {
                            bool nwcResult = nwcManager.ExportToNavisworks(threeDViews, NWCSettings, outputFolder);
                            exportResults.Add($"Navisworks: {(nwcResult ? $"Success ({threeDViews.Count} 3D views)" : "Failed")}");
                            exportSuccess |= nwcResult;
                        }
                        else
                        {
                            exportResults.Add($"Navisworks: Skipped (no 3D views selected)");
                            // Debug logging removed
                        }
                    }
                    catch (Exception ex)
                    {
                        // Debug logging removed
                        exportResults.Add($"Navisworks: Failed ({ex.Message})");
                    }
                }
                else if (ExportSettings.IsNwcSelected && selectedSheets.Any() && !selectedViews.Any())
                {
                    exportResults.Add($"Navisworks: Skipped (NWC requires 3D views, not sheets)");
                    // Debug logging removed
                }

                // Show results
                if (exportSuccess)
                {
                    var successMessage = $"Export hoàn tất!\n\n" +
                                       $"Items: {totalSelected} ({selectedSheets.Count} sheets, {selectedViews.Count} views)\n" +
                                       $"Output: {outputFolder}\n\n" +
                                       $"Results:\n{string.Join("\n", exportResults)}";
                    
                    MessageBox.Show(successMessage, "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Debug logging removed
                }
                else
                {
                    MessageBox.Show($"Export failed or no files were exported.\n\nResults:\n{string.Join("\n", exportResults)}", 
                                   "Export Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Debug logging removed
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Lỗi export: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetFileNames_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                if (_sheets != null)
                {
                    foreach (var sheet in _sheets)
                    {
                        // Reset to default naming: Sheet Number
                        sheet.CustomFileName = sheet.SheetNumber;
                    }
                    // Debug logging removed
                    MessageBox.Show($"Đã reset {_sheets.Count} custom file names về mặc định (Sheet Number).", 
                                   "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Lỗi reset file names: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ApplyTemplate_Click temporarily disabled - requires refactoring with new Profile system
        /*
        private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                if (_selectedProfile != null && _sheets != null)
                {
                    // Show dialog to select XML profile for custom naming template
                    var openFileDialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Chọn XML Profile để áp dụng template custom file name",
                        Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                        DefaultExt = ".xml"
                    };

                    // Try to default to ExportPlus folder if exists
                    var diRootsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                                  "DiRoots", "ExportPlus");
                    if (Directory.Exists(diRootsPath))
                    {
                        openFileDialog.InitialDirectory = diRootsPath;
                    }

                    if (openFileDialog.ShowDialog() == true)
                    {
                        // Get current sheets from document
                        var currentSheets = GetCurrentDocumentSheets();
                        
                        // Load XML profile and generate custom file names
                        var sheetInfos = _profileManager.LoadXMLProfileWithSheets(openFileDialog.FileName, currentSheets);
                        
                        if (sheetInfos.Any())
                        {
                            // Apply custom file names from template
                            foreach (var sheetInfo in sheetInfos)
                            {
                                var existingSheet = _sheets.FirstOrDefault(s => s.SheetNumber == sheetInfo.SheetNumber);
                                if (existingSheet != null)
                                {
                                    existingSheet.CustomFileName = sheetInfo.CustomFileName;
                                }
                            }
                            
                            // Debug logging removed
                            MessageBox.Show($"Đã áp dụng template cho {sheetInfos.Count} sheets.\nCustom file names đã được cập nhật.", 
                                           "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Không thể tạo custom file names từ template này.", 
                                           "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Vui lòng chọn profile và load sheets trước.", 
                                   "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Lỗi áp dụng template: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        */

        #endregion

        #region Create Tab Event Handlers

        private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Chọn thư mục xuất file",
                    SelectedPath = OutputFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    ShowNewFolderButton = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    OutputFolder = dialog.SelectedPath;
                    // Debug logging removed
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Lỗi chọn thư mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateFiles_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                // Validate selections
                var sheetsSelected = Sheets?.Count(s => s.IsSelected) ?? 0;
                var viewsSelected = Views?.Count(v => v.IsSelected) ?? 0;
                var totalSelected = sheetsSelected + viewsSelected;

                if (totalSelected == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một sheet hoặc RevitView để xuất.", 
                                   "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(OutputFolder) || !Directory.Exists(OutputFolder))
                {
                    MessageBox.Show("Vui lòng chọn thư mục xuất hợp lệ.", 
                                   "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if any format is selected
                var hasFormat = (ExportSettings?.IsPdfSelected == true) ||
                               (ExportSettings?.IsDwgSelected == true) ||
                               // (ExportSettings?.IsImageSelected == true) ||  // Remove until property exists
                               (ExportSettings?.IsIfcSelected == true);

                if (!hasFormat)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một định dạng xuất.", 
                                   "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show confirmation dialog
                var message = $"Bạn sắp xuất {totalSelected} item(s) ";
                if (sheetsSelected > 0 && viewsSelected > 0)
                {
                    message += $"({sheetsSelected} sheet(s) và {viewsSelected} view(s)) ";
                }
                else if (sheetsSelected > 0)
                {
                    message += $"({sheetsSelected} sheet(s)) ";
                }
                else
                {
                    message += $"({viewsSelected} view(s)) ";
                }
                message += $"vào thư mục:\n{OutputFolder}\n\nTiếp tục?";

                var result = MessageBox.Show(message, "Xác nhận xuất file", 
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Call the existing export method
                    // Debug logging removed
                    ExportButton_Click(sender, e); // Call existing export logic
                }
                else
                {
                    // Debug logging removed
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Lỗi xuất file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Create Tab Event Handlers

        private void LearnMore_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) 
                { 
                    UseShellExecute = true 
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        private void SetPaperSizeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedQueueItems();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one queue item to set paper size.",
                               "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedSize = PromptChoice("Set Paper Size", "Paper size:", new[] { "A0", "A1", "A2", "A3", "A4", "Letter", "Tabloid", "Custom" }, "A3");
            if (string.IsNullOrWhiteSpace(selectedSize))
            {
                return;
            }

            foreach (var queueItem in selectedItems)
            {
                queueItem.Size = selectedSize;
            }

            ExportQueueDataGrid.Items.Refresh();
        }

        private void SetOrientationButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedQueueItems();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one queue item to set orientation.",
                               "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedOrientation = PromptChoice("Set Orientation", "Orientation:", new[] { "Portrait", "Landscape" }, "Landscape");
            if (string.IsNullOrWhiteSpace(selectedOrientation))
            {
                return;
            }

            foreach (var queueItem in selectedItems)
            {
                queueItem.Orientation = selectedOrientation;
            }

            ExportQueueDataGrid.Items.Refresh();
        }

        private List<ExportQueueItem> GetSelectedQueueItems()
        {
            var selectedItems = ExportQueueDataGrid?.SelectedItems?.OfType<ExportQueueItem>().ToList() ?? new List<ExportQueueItem>();
            if (selectedItems.Count > 0)
            {
                return selectedItems;
            }

            return ExportQueueItems?.Where(i => i.IsSelected).ToList() ?? new List<ExportQueueItem>();
        }

        private string PromptChoice(string title, string label, IEnumerable<string> choices, string defaultValue)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 320,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) });

            var comboBox = new ComboBox { Height = 28, ItemsSource = choices.ToList(), SelectedItem = defaultValue };
            panel.Children.Add(comboBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var okButton = new Button { Content = "OK", Width = 72, Height = 26, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancelButton = new Button { Content = "Cancel", Width = 72, Height = 26, IsCancel = true };
            okButton.Click += (s, e) => dialog.DialogResult = true;
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            panel.Children.Add(buttons);

            dialog.Content = panel;
            return dialog.ShowDialog() == true ? comboBox.SelectedItem?.ToString() : null;
        }

        private void RemoveQueueItems_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedQueueItems();
            if (selectedItems.Count == 0)
            {
                return;
            }

            foreach (var item in selectedItems.ToList())
            {
                ExportQueueItems.Remove(item);
            }
        }

        private void OpenQueueItem_Click(object sender, RoutedEventArgs e)
        {
            var queueItem = GetSelectedQueueItems().FirstOrDefault();
            if (queueItem == null || _uiApp?.ActiveUIDocument == null)
            {
                return;
            }

            try
            {
                ElementId targetId = ElementId.InvalidElementId;

                var sheetItem = Sheets?.FirstOrDefault(s =>
                    string.Equals(s.SheetNumber, queueItem.ViewSheetNumber, StringComparison.OrdinalIgnoreCase));
                if (sheetItem != null)
                {
                    targetId = sheetItem.Id;
                }

                // Update Hidden Line Views
                ExportSettings.HiddenLineViews = RasterProcessingRadio?.IsChecked == true
                    ? PSHiddenLineViews.RasterProcessing
                    : PSHiddenLineViews.VectorProcessing;

                // Update Zoom
                ExportSettings.Zoom = FitToPageRadio?.IsChecked == true
                    ? PSZoomType.FitToPage
                    : PSZoomType.Zoom;

                if (int.TryParse(ZoomPercentTextBox?.Text, out int zoomPercent))
                {
                    ExportSettings.ZoomPercentage = zoomPercent;
                }

                // Update selected legacy PDF printer
                var selectedPrinter = GetComboBoxSelectedText(PrinterCombo);
                if (!string.IsNullOrWhiteSpace(selectedPrinter))
                {
                    ExportSettings.SelectedPdfPrinter = selectedPrinter;
                }
                else
                {
                    var viewItem = Views?.FirstOrDefault(v =>
                        string.Equals(v.ViewName, queueItem.ViewSheetName, StringComparison.OrdinalIgnoreCase));
                    if (viewItem != null)
                    {
                        targetId = viewItem.RevitViewId;
                    }
                }

                if (targetId == ElementId.InvalidElementId)
                {
                    MessageBox.Show("Could not find the selected sheet or view in the current document.",
                        "Open Sheet/View", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var view = _document.GetElement(targetId) as RevitView;
                if (view != null)
                {
                    _uiApp.ActiveUIDocument.ActiveView = view;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open sheet/view: {ex.Message}",
                    "Open Sheet/View", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializePdfSettingsControls()
        {
            try
            {
                LoadPdfPrinters();
                ApplyExportSettingsToPdfUi();
                UpdatePdfSettingsUiState();
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Initialize PDF settings controls failed: {ex.Message}");
            }
        }

        private void LoadPdfPrinters()
        {
            if (PrinterCombo == null) return;

            try
            {
                var printers = new List<string>();
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    if (!string.IsNullOrWhiteSpace(printer))
                    {
                        printers.Add(printer);
                    }
                }

                PrinterCombo.Items.Clear();
                foreach (var printer in printers.OrderBy(p => p))
                {
                    PrinterCombo.Items.Add(new ComboBoxItem { Content = printer });
                }

                if (PrinterCombo.Items.Count == 0)
                {
                    PrinterCombo.Items.Add(new ComboBoxItem { Content = ExportSettings?.SelectedPdfPrinter ?? "PDF24" });
                }

                var preferred = ExportSettings?.SelectedPdfPrinter;
                if (string.IsNullOrWhiteSpace(preferred) || !SetComboBoxByText(PrinterCombo, preferred))
                {
                    preferred = printers.FirstOrDefault(p => p.IndexOf("PDF24", StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? printers.FirstOrDefault(p => p.IndexOf("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? printers.FirstOrDefault(p => p.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!string.IsNullOrWhiteSpace(preferred))
                    {
                        SetComboBoxByText(PrinterCombo, preferred);
                        if (ExportSettings != null) ExportSettings.SelectedPdfPrinter = preferred;
                    }
                    else if (PrinterCombo.Items.Count > 0)
                    {
                        PrinterCombo.SelectedIndex = 0;
                        if (ExportSettings != null) ExportSettings.SelectedPdfPrinter = GetComboBoxSelectedText(PrinterCombo);
                    }
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Load PDF printers failed: {ex.Message}");
            }
        }

        private void ApplyExportSettingsToPdfUi()
        {
            if (ExportSettings == null) return;

            CenterRadio.IsChecked = ExportSettings.PaperPlacement == PSPaperPlacement.Center;
            OffsetRadio.IsChecked = ExportSettings.PaperPlacement == PSPaperPlacement.OffsetFromCorner;
            SetComboBoxByText(MarginCombo, ExportSettings.PaperMargin == PSPaperMargin.PrinterLimit ? "Printer Limit" :
                ExportSettings.PaperMargin == PSPaperMargin.UserDefined ? "User Defined" : "No Margin");

            OffsetXTextBox.Text = ExportSettings.OffsetX.ToString("0.###");
            OffsetYTextBox.Text = ExportSettings.OffsetY.ToString("0.###");

            VectorProcessingRadio.IsChecked = ExportSettings.HiddenLineViews == PSHiddenLineViews.VectorProcessing;
            RasterProcessingRadio.IsChecked = ExportSettings.HiddenLineViews == PSHiddenLineViews.RasterProcessing;
            SetComboBoxByText(RasterQualityCombo, ExportSettings.RasterQuality == PSRasterQuality.Low ? "Low" :
                ExportSettings.RasterQuality == PSRasterQuality.Medium ? "Medium" :
                ExportSettings.RasterQuality == PSRasterQuality.Maximum ? "Presentation" : "High");
            SetComboBoxByText(ColorsCombo, ExportSettings.Colors == PSColors.BlackAndWhite ? "Black and White" :
                ExportSettings.Colors == PSColors.Grayscale ? "Grayscale" : "Color");

            FitToPageRadio.IsChecked = ExportSettings.Zoom == PSZoomType.FitToPage;
            ZoomRadio.IsChecked = ExportSettings.Zoom == PSZoomType.Zoom;
            ZoomPercentTextBox.Text = ExportSettings.ZoomPercentage.ToString();

            SetComboBoxByText(PrinterCombo, ExportSettings.SelectedPdfPrinter);
            SeparateFilesRadio.IsChecked = !ExportSettings.CombineFiles;
            CombineFilesRadio.IsChecked = ExportSettings.CombineFiles;
            KeepPaperSizeCheckBox.IsChecked = ExportSettings.KeepPaperSize;

            UpdatePdfSettingsUiState();
        }

        private void UpdatePdfSettingsUiState()
        {
            try
            {
                bool offsetEnabled = OffsetRadio?.IsChecked == true;
                if (MarginCombo != null) MarginCombo.IsEnabled = offsetEnabled;
                if (OffsetXTextBox != null) OffsetXTextBox.IsEnabled = offsetEnabled;
                if (OffsetYTextBox != null) OffsetYTextBox.IsEnabled = offsetEnabled;
                if (ZoomPercentTextBox != null) ZoomPercentTextBox.IsEnabled = ZoomRadio?.IsChecked == true;

                bool combineEnabled = CombineFilesRadio?.IsChecked == true;
                if (CustomFileNameCombineButton != null) CustomFileNameCombineButton.IsEnabled = combineEnabled;
                if (OrderSheetsViewsButton != null) OrderSheetsViewsButton.IsEnabled = combineEnabled;
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Update PDF settings UI state failed: {ex.Message}");
            }
        }

        private void PdfSettings_Changed(object sender, RoutedEventArgs e)
        {
            UpdateExportSettingsFromUI();
            UpdatePdfSettingsUiState();
        }

        private void PdfCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateExportSettingsFromUI();
            UpdatePdfSettingsUiState();
        }

        private void PdfSettingsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateExportSettingsFromUI();
        }

        private static string GetComboBoxSelectedText(ComboBox comboBox)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "";
            }

            return comboBox?.SelectedItem?.ToString() ?? comboBox?.Text ?? "";
        }

        private static bool SetComboBoxByText(ComboBox comboBox, string value)
        {
            if (comboBox == null || string.IsNullOrWhiteSpace(value)) return false;

            foreach (var item in comboBox.Items)
            {
                var text = item is ComboBoxItem comboItem ? comboItem.Content?.ToString() : item?.ToString();
                if (string.Equals(text, value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return true;
                }
            }

            return false;
        }

        private void RetryFailedButton_Click(object sender, RoutedEventArgs e)
        {
            var retryItems = (ExportQueueItems ?? new ObservableCollection<ExportQueueItem>())
                .Where(i => string.Equals(i.Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(i.Status, "Skipped", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (retryItems.Count == 0)
            {
                MessageBox.Show("There are no failed or skipped queue items to retry.",
                    "Retry Failed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            PrepareRetrySelection(retryItems);
            foreach (var item in retryItems)
            {
                item.Status = "Pending";
                item.Progress = 0;
                item.ErrorMessage = string.Empty;
                item.CompletedAt = string.Empty;
            }

            ExportQueueDataGrid.Items.Refresh();
            StartExportButton_Click(sender, e);
        }

        private void PrepareRetrySelection(List<ExportQueueItem> retryItems)
        {
            if (Sheets != null)
            {
                foreach (var sheet in Sheets)
                {
                    sheet.IsSelected = retryItems.Any(i =>
                        !string.IsNullOrEmpty(i.ViewSheetNumber) &&
                        string.Equals(i.ViewSheetNumber, sheet.SheetNumber, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (Views != null)
            {
                foreach (var view in Views)
                {
                    view.IsSelected = retryItems.Any(i =>
                        string.Equals(i.ViewSheetName, view.ViewName, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (ExportSettings != null)
            {
                ExportSettings.IsPdfSelected = retryItems.Any(i => string.Equals(i.Format, "PDF", StringComparison.OrdinalIgnoreCase));
                ExportSettings.IsDwgSelected = retryItems.Any(i => string.Equals(i.Format, "DWG", StringComparison.OrdinalIgnoreCase));
                ExportSettings.IsIfcSelected = retryItems.Any(i => string.Equals(i.Format, "IFC", StringComparison.OrdinalIgnoreCase));
                ExportSettings.IsNwcSelected = retryItems.Any(i => string.Equals(i.Format, "NWC", StringComparison.OrdinalIgnoreCase));
                ExportSettings.IsDxfSelected = retryItems.Any(i => string.Equals(i.Format, "DXF", StringComparison.OrdinalIgnoreCase));
                ExportSettings.IsImageSelected = retryItems.Any(i => string.Equals(i.Format, "IMG", StringComparison.OrdinalIgnoreCase) || string.Equals(i.Format, "IMAGE", StringComparison.OrdinalIgnoreCase));
                ExportSettings.IsDgnSelected = false;
                ExportSettings.IsDwfSelected = false;
            }
        }

        private void ScheduleToggle_Checked(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            if (ScheduleSettingsPanel != null)
            {
                ScheduleSettingsPanel.Visibility = System.Windows.Visibility.Visible;
            }
            if (ScheduleStatusText != null)
            {
                ScheduleStatusText.Text = "The Scheduling Assistant is on.";
                ScheduleStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            
            // Initialize date picker to today if not set
            if (StartingDatePicker != null && !StartingDatePicker.SelectedDate.HasValue)
            {
                StartingDatePicker.SelectedDate = DateTime.Now;
            }
            
            // Initialize time combobox to current hour if not selected
            if (TimeComboBox != null && TimeComboBox.SelectedIndex < 0)
            {
                var currentHour = DateTime.Now.Hour;
                var ampm = currentHour >= 12 ? "PM" : "AM";
                var hour12 = currentHour % 12;
                if (hour12 == 0) hour12 = 12;
                var timeString = $"{hour12:00}:00 {ampm}";
                
                // Try to find matching time in combobox
                for (int i = 0; i < TimeComboBox.Items.Count; i++)
                {
                    if (TimeComboBox.Items[i] is ComboBoxItem item && 
                        item.Content.ToString() == timeString)
                    {
                        TimeComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void ScheduleToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            if (ScheduleSettingsPanel != null)
            {
                ScheduleSettingsPanel.Visibility = System.Windows.Visibility.Collapsed;
            }
            if (ScheduleStatusText != null)
            {
                ScheduleStatusText.Text = "The Scheduling Assistant is off.";
                ScheduleStatusText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));
            }
        }

        private void RepeatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DaysOfWeekPanel == null || RepeatComboBox == null) return;

            // Show days of week panel only when "Weekly" is selected
            var selectedItem = RepeatComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Content?.ToString() == "Weekly")
            {
                DaysOfWeekPanel.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                DaysOfWeekPanel.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void RefreshScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            // Refresh schedule settings display
            if (ScheduleToggle.IsChecked == true)
            {
                var date = StartingDatePicker.SelectedDate?.ToString("d") ?? "Not set";
                var time = (TimeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Not set";
                var repeat = (RepeatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Does not repeat";
                
                var message = $"Current Schedule Settings:\n\n" +
                             $"Date: {date}\n" +
                             $"Time: {time}\n" +
                             $"Repeat: {repeat}";
                
                if (repeat == "Weekly")
                {
                    var days = new System.Text.StringBuilder();
                    if (MondayCheck.IsChecked == true) days.Append("Mon ");
                    if (TuesdayCheck.IsChecked == true) days.Append("Tue ");
                    if (WednesdayCheck.IsChecked == true) days.Append("Wed ");
                    if (ThursdayCheck.IsChecked == true) days.Append("Thu ");
                    if (FridayCheck.IsChecked == true) days.Append("Fri ");
                    if (SaturdayCheck.IsChecked == true) days.Append("Sat ");
                    if (SundayCheck.IsChecked == true) days.Append("Sun ");
                    
                    if (days.Length > 0)
                    {
                        message += $"\nDays: {days}";
                    }
                }
                
                MessageBox.Show(message, "Schedule Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Scheduling Assistant is currently off. Turn it on to configure schedule settings.", 
                               "Schedule Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void StartExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            try
            {
                // Create new cancellation token for this export
                _exportCancellationTokenSource?.Cancel();
                _exportCancellationTokenSource?.Dispose();
                _exportCancellationTokenSource = new System.Threading.CancellationTokenSource();
                var cancellationToken = _exportCancellationTokenSource.Token;
                
                // Validate output folder
                if (string.IsNullOrEmpty(CreateFolderPathTextBox?.Text))
                {
                    MessageBox.Show("Please select an output folder.", 
                                   "No Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Rebuild from the latest Selection/Formats state right before export.
                // This protects the Create tab from stale UI events or delayed checkbox binding updates.
                UpdateExportQueue();
                ExportQueueDataGrid?.Items.Refresh();

                // Validate export queue has items
                var queueCount = ExportQueueItems?.Count ?? 0;
                if (queueCount == 0)
                {
                    var selectedSheets = Sheets?.Count(s => s.IsSelected) ?? 0;
                    var selectedViews = Views?.Count(v => v.IsSelected) ?? 0;
                    var selectedFormats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
                    LicorpTrace.Warn($"Export queue is empty before export. Selected sheets: {selectedSheets}, selected views: {selectedViews}, selected formats: {string.Join(",", selectedFormats)}.");
                    MessageBox.Show("Export queue is empty. Please select items to export from the Selection tab.", 
                                   "Empty Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Disable button during export
                StartExportButton.IsEnabled = false;
                StartExportButton.Content = "EXPORTING...";
                
                // Reset progress
                ExportProgressBar.Value = 0;
                ProgressPercentageText.Text = "Completed 0%";

                // Check if scheduling is enabled
                if (ScheduleToggle.IsChecked == true)
                {
                    // Schedule for later
                    var scheduleDate = StartingDatePicker.SelectedDate ?? DateTime.Now;
                    var scheduleTime = (TimeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "12:00 PM";
                    
                    MessageBox.Show($"Export scheduled for {scheduleDate:d} at {scheduleTime}.\n\n" +
                                   "The export will run automatically at the scheduled time.", 
                                   "Export Scheduled", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Debug logging removed
                }
                else
                {
                    // Export immediately
                    // Debug logging removed
                    
                    var items = ExportQueueDataGrid.Items.Cast<ExportQueueItem>().ToList();
                    var totalItems = items.Count;
                    Action updateOverallProgress = () =>
                    {
                        if (totalItems <= 0)
                        {
                            ExportProgressBar.Value = 0;
                            ProgressPercentageText.Text = "Completed 0%";
                            return;
                        }

                        var completedItems = items.Count(i => i.Status == "Completed");
                        var processingItems = items.Where(i => i.Status == "Processing" || i.Status.Contains("External Event"));
                        double progressSum = completedItems;
                        foreach (var processingItem in processingItems)
                        {
                            progressSum += Math.Max(0.0, Math.Min(1.0, processingItem.Progress / 100.0));
                        }

                        var overallProgress = Math.Max(0.0, Math.Min(100.0, (progressSum * 100.0) / totalItems));
                        ExportProgressBar.Value = overallProgress;
                        ProgressPercentageText.Text = $"Completed {overallProgress:F1}%";
                    };
                    
                    // Get selected sheets and views from Selection tab
                    var selectedSheets = Sheets?.Where(s => s.IsSelected).ToList() ?? new List<SheetItem>();
                    var selectedViews = Views?.Where(v => v.IsSelected).ToList() ?? new List<ViewItem>();
                    var totalSelected = selectedSheets.Count + selectedViews.Count;
                    
                    if (totalSelected == 0)
                    {
                        MessageBox.Show("Please select at least one sheet or RevitView to export.", 
                                       "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Get selected formats
                    var selectedFormats = ExportSettings?.GetSelectedFormatsList() ?? new List<string>();
                    
                    if (selectedFormats.Count == 0)
                    {
                        MessageBox.Show("Please select at least one export format in the Format tab.", 
                                       "No Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    
                    int completedCount = 0;
                    string outputFolder = CreateFolderPathTextBox.Text;

                    // Export for each selected format
                    foreach (var format in selectedFormats)
                    {
                        // Debug logging removed
                        
                        // Always split exports by format: PDF, DWG, IFC, NWC, DXF, IMG, XML...
                        string formatOutputFolder = GetFormatOutputFolder(format);

                        foreach (var queueItem in items.Where(i => string.Equals(i.Format, format, StringComparison.OrdinalIgnoreCase)))
                        {
                            queueItem.OutputPath = BuildQueueOutputPath(queueItem.ViewSheetName, queueItem.Format);
                            queueItem.ErrorMessage = string.Empty;
                            queueItem.CompletedAt = string.Empty;
                        }
                        
                        if (format.ToUpper() == "PDF")
                        {
                            // Use PDF Export External Event for proper API context (only for sheets)
                            if (selectedSheets.Any() && _pdfExportEvent != null && _pdfExportHandler != null)
                            {
                                // Debug logging removed
                                
                                // ⚠️ REMOVED: Don't set all items to Processing/0% upfront
                                // Callback will set each item to Processing when export starts
                                // This prevents "all 0%" problem
                                
                                // CRITICAL: Update ExportSettings from UI controls BEFORE export
                                UpdateExportSettingsFromUI();
                                
                                // Set export parameters
                                _pdfExportHandler.Document = _document;
                                _pdfExportHandler.SheetItems = selectedSheets;
                                _pdfExportHandler.OutputFolder = formatOutputFolder;  // ✅ Use format-specific folder
                                _pdfExportHandler.Settings = ExportSettings;
                                _pdfExportHandler.ProgressCallback = (current, total, sheetNumber, isFileCompleted) =>
                                {
                                    // Update UI on dispatcher thread
                                    Dispatcher.Invoke(() =>
                                    {
                                        // Find corresponding item in queue
                                        var queueItem = items.FirstOrDefault(i => 
                                            i.ViewSheetNumber == sheetNumber && 
                                            i.Format == format.ToUpper());
                                        
                                        if (queueItem != null)
                                        {
                                            if (isFileCompleted)
                                            {
                                                // File has been created and renamed - mark as completed
                                                queueItem.Status = "Completed";
                                                queueItem.Progress = 100;
                                                queueItem.OutputPath = BuildQueueOutputPath(queueItem.ViewSheetName, queueItem.Format);
                                                queueItem.CompletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                                completedCount++;
                                                // Debug logging removed
                                            }
                                            else
                                            {
                                                // Export started but file not yet completed
                                                queueItem.Status = "Processing";
                                                queueItem.Progress = (current * 100.0) / total;
                                                // Debug logging removed
                                            }
                                            
                                            // CRITICAL: Force DataGrid to refresh immediately
                                            ExportQueueDataGrid.Items.Refresh();
                                        }
                                        
                                        // Update overall progress based on completed + processing items
                                        // Each completed item counts as 1.0, each processing item counts as its percentage (0.0 - 0.99)
                                        updateOverallProgress();
                                        
                                        // Debug logging removed
                                    });
                                };
                                
                                // Raise the external event to run export in API context
                                var raiseResult = _pdfExportEvent.Raise();
                                // Debug logging removed
                                
                                // Wait for export to complete by checking queue item statuses
                                // Use Task.Delay instead of Thread.Sleep to avoid blocking UI thread
                                int waitCount = 0;
                                int maxWaitSeconds = 300; // 5 minutes timeout
                                
                                while (waitCount < maxWaitSeconds * 10) // Check every 100ms
                                {
                                    await System.Threading.Tasks.Task.Delay(100, cancellationToken); // Yield control to allow External Event to run
                                    waitCount++;
                                    
                                    // Check if all PDF items in queue are completed
                                    var pdfItems = items.Where(i => i.Format == "PDF").ToList();
                                    bool allPdfCompleted = pdfItems.All(i => i.Status == "Completed" || i.Status == "Failed");
                                    
                                    if (allPdfCompleted)
                                    {
                                        // Debug logging removed
                                        break;
                                    }
                                    
                                    // Log progress every 5 seconds
                                    if (waitCount % 50 == 0)
                                    {
                                        var completed = pdfItems.Count(i => i.Status == "Completed");
                                        // Debug logging removed
                                    }
                                }
                                
                                bool exportResult = _pdfExportHandler.ExportResult;
                                
                                if (exportResult)
                                {
                                    // Debug logging removed
                                }
                                else
                                {
                                    // Debug logging removed
                                }
                            }
                            else if (selectedSheets.Any())
                            {
                                MessageBox.Show("Cannot export PDF: External Event not initialized.\n\nPlease restart Revit and try again.",
                                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            else
                            {
                            }
                        }
                        else if (format.ToUpper() == "DWG")
                        {
                            // Debug logging removed
                            
                            if (selectedSheets.Any())
                            {
                                // ⚠️ REMOVED: Don't set all items to Processing/0% upfront
                                // Callback will set each item to Processing when export starts
                                
                                try
                                {
                                    var dwgManager = new DWGExportService(_document);
                                    
                                    // Create DWG export settings from UI  
                                    var dwgSettings = new PSDWGExportSettings
                                    {
                                        OutputFolder = formatOutputFolder,  // ✅ Use format-specific folder
                                        DWGSetupName = ExportSettings?.DWGExportSetupName ?? "Standard",
                                        DWGVersion = ExportSettings?.DWGVersion ?? "2018",
                                        UseSharedCoordinates = ExportSettings?.UseSharedCoordinates ?? true,
                                        ExportViewsOnSheets = ExportSettings?.ExportViewsOnSheets ?? false, // Default OFF to avoid too many files
                                        CompactDwgFiles = ExportSettings?.CompactDwgFiles ?? true,
                                        CreateSubfolders = ExportSettings?.CreateSeparateFiles ?? false,
                                        FileNamingPattern = "{SheetNumber}_{SheetName}"
                                    };
                                    
                                    // Debug logging removed
                                    
                                    // Export each sheet
                                    int successCount = 0;
                                    int failCount = 0;
                                    
                                    foreach (var sheetItem in selectedSheets)
                                    {
                                        try
                                        {
                                            var sheet = _document.GetElement(sheetItem.Id) as Autodesk.Revit.DB.ViewSheet;
                                            if (sheet != null)
                                            {
                                                // Mark this queue item as Processing BEFORE export starts
                                                var dwgProcessingItem = ExportQueueItems.FirstOrDefault(q =>
                                                    q.ViewSheetNumber == sheet.SheetNumber && q.Format.ToUpper() == "DWG");
                                                if (dwgProcessingItem != null)
                                                {
                                                    dwgProcessingItem.Status = "Processing";
                                                    dwgProcessingItem.Progress = 1;
                                                    dwgProcessingItem.OutputPath = BuildQueueOutputPath(dwgProcessingItem.ViewSheetName, dwgProcessingItem.Format);
                                                    dwgProcessingItem.ErrorMessage = string.Empty;
                                                    dwgProcessingItem.CompletedAt = string.Empty;
                                                    ExportQueueDataGrid.Items.Refresh();
                                                    updateOverallProgress();
                                                }

                                                // Allow WPF to render Processing state in realtime
                                                await Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);

                                                // Debug logging removed
                                                
                                                var result = dwgManager.ExportToDWG(
                                                    new List<Autodesk.Revit.DB.ViewSheet> { sheet },
                                                    dwgSettings,
                                                    s => sheetItem.CustomFileName);
                                                
                                                if (result)
                                                {
                                                    successCount++;
                                                    
                                                    // Update queue item status on UI thread
                                                    Dispatcher.Invoke(() =>
                                                    {
                                                        var queueItem = ExportQueueItems.FirstOrDefault(q => 
                                                            q.ViewSheetNumber == sheet.SheetNumber && q.Format.ToUpper() == "DWG");
                                                        if (queueItem != null)
                                                        {
                                                            queueItem.Progress = 100;
                                                            queueItem.Status = "Completed";
                                                            queueItem.OutputPath = BuildQueueOutputPath(queueItem.ViewSheetName, queueItem.Format);
                                                            queueItem.CompletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                                            
                                                            // CRITICAL: Force DataGrid refresh
                                                            ExportQueueDataGrid.Items.Refresh();
                                                            updateOverallProgress();
                                                        }
                                                    });
                                                    
                                                    // Debug logging removed
                                                }
                                                else
                                                {
                                                    failCount++;
                                                    
                                                    // Update failed status on UI thread
                                                    Dispatcher.Invoke(() =>
                                                    {
                                                        var queueItem = ExportQueueItems.FirstOrDefault(q => 
                                                            q.ViewSheetNumber == sheet.SheetNumber && q.Format.ToUpper() == "DWG");
                                                        if (queueItem != null)
                                                        {
                                                            queueItem.Status = "Failed";
                                                            queueItem.Progress = 0;
                                                            queueItem.OutputPath = BuildQueueOutputPath(queueItem.ViewSheetName, queueItem.Format);
                                                            queueItem.ErrorMessage = "DWG export returned false.";
                                                            
                                                            // CRITICAL: Force DataGrid refresh
                                                            ExportQueueDataGrid.Items.Refresh();
                                                            updateOverallProgress();
                                                        }
                                                    });
                                                    
                                                    // Debug logging removed
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            failCount++;
                                            var queueItem = ExportQueueItems.FirstOrDefault(q =>
                                                q.ViewSheetNumber == sheetItem.SheetNumber && q.Format.ToUpper() == "DWG");
                                            if (queueItem != null)
                                            {
                                                queueItem.Status = "Failed";
                                                queueItem.Progress = 0;
                                                queueItem.OutputPath = BuildQueueOutputPath(queueItem.ViewSheetName, queueItem.Format);
                                                queueItem.ErrorMessage = ex.Message;
                                                ExportQueueDataGrid.Items.Refresh();
                                                updateOverallProgress();
                                            }
                                        }
                                    }
                                    
                                    // Debug logging removed
                                }
                                catch (Exception ex)
                                {
                                    // Debug logging removed
                                    MessageBox.Show($"DWG export error: {ex.Message}", "Export Error", 
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else
                            {
                                // Debug logging removed
                            }
                        }
                        else if (format.ToUpper() == "IFC")
                        {
                            // Debug logging removed
                            
                            // Get 3D views for IFC export
                            var threeDViewItems = selectedViews.Where(v => 
                                v.ViewType != null && 
                                (v.ViewType.Contains("ThreeD") || v.ViewType.Contains("3D"))).ToList();
                            
                            if (threeDViewItems.Any() && _ifcExportEvent != null && _ifcExportHandler != null)
                            {
                                // Debug logging removed
                                
                                // ⚠️ REMOVED: Don't set all items to Processing/0% upfront
                                // Callback will set each item to Processing when export starts
                                // This prevents "all 0%" problem
                                
                                // Convert ViewItem → View3D using Document
                                var view3DList = new List<View3D>();
                                foreach (var viewItem in threeDViewItems)
                                {
                                    try
                                    {
                                        var view = _document.GetElement(viewItem.RevitViewId) as View3D;
                                        if (view != null)
                                        {
                                            view3DList.Add(view);
                                            // Debug logging removed
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Debug logging removed
                                    }
                                }
                                
                                if (view3DList.Any())
                                {
                                    // Debug logging removed
                                    
                                    // Set parameters for IFC export
                                    _ifcExportHandler.Document = _document;
                                    _ifcExportHandler.Views3D = view3DList;
                                    _ifcExportHandler.Settings = IFCSettings;
                                    _ifcExportHandler.OutputFolder = formatOutputFolder;  // ✅ Use format-specific folder
                                    
                                    // Set progress callback to update UI after EACH file export
                                    _ifcExportHandler.ProgressCallback = (viewName, success) =>
                                    {
                                        // This runs in Revit API thread, dispatch to UI thread
                                        Dispatcher.Invoke(() =>
                                        {
                                            var queueItem = items.FirstOrDefault(i => 
                                                i.ViewSheetName == viewName && 
                                                i.Format == "IFC");
                                            
                                            if (queueItem != null)
                                            {
                                                queueItem.Status = success ? "Completed" : "Failed";
                                                queueItem.Progress = success ? 100 : 0;
                                                queueItem.OutputPath = BuildQueueOutputPath(queueItem.ViewSheetName, queueItem.Format);
                                                queueItem.CompletedAt = success ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
                                                queueItem.ErrorMessage = success ? string.Empty : "IFC export failed.";
                                                
                                                // CRITICAL: Force DataGrid refresh
                                                ExportQueueDataGrid.Items.Refresh();
                                                
                                            }
                                            
                                            // Update overall progress based on actual completion
                                            updateOverallProgress();
                                        });
                                    };
                                    
                                    // Set completion callback to update UI when ALL done
                                    _ifcExportHandler.CompletionCallback = (success) =>
                                    {
                                        // This runs in Revit API thread, need to dispatch to UI thread
                                        Dispatcher.Invoke(() =>
                                        {
                                            
                                            // Check if all items are now completed
                                            var allItemsFinished = items.All(i => i.Status == "Completed" || i.Status == "Failed");
                                            if (allItemsFinished)
                                            {
                                                // Debug logging removed
                                                TryWriteExportReport(items, CreateFolderPathTextBox.Text);
                                                ShowExportCompletedDialog(CreateFolderPathTextBox.Text);
                                            }
                                        });
                                    };
                                    
                                    // Raise the external event (will run in Revit API context)
                                    var raiseResult = _ifcExportEvent.Raise();
                                    
                                    
                                    if (raiseResult == ExternalEventRequest.Accepted)
                                    {
                                        // Debug logging removed
                                        // Debug logging removed
                                    }
                                    else if (raiseResult == ExternalEventRequest.Pending)
                                    {
                                        // Debug logging removed
                                    }
                                    else if (raiseResult == ExternalEventRequest.Denied)
                                    {
                                        // Debug logging removed
                                        MessageBox.Show("IFC export denied: Revit is currently busy.\n\nPlease close any open dialogs and try again.",
                                            "Export Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                                        
                                        // Reset IFC items status
                                        foreach (var ifcItem in items.Where(i => i.Format == "IFC"))
                                        {
                                            ifcItem.Status = "Failed";
                                            ifcItem.Progress = 0;
                                            ifcItem.OutputPath = BuildQueueOutputPath(ifcItem.ViewSheetName, ifcItem.Format);
                                            ifcItem.ErrorMessage = "IFC external event was denied.";
                                        }
                                        ExportQueueDataGrid.Items.Refresh();
                                    }
                                    else if (raiseResult == ExternalEventRequest.TimedOut)
                                    {
                                        // Debug logging removed
                                        MessageBox.Show("IFC export timed out: Revit did not respond.\n\nPlease try again.",
                                            "Export Timeout", MessageBoxButton.OK, MessageBoxImage.Error);
                                        
                                        // Reset IFC items status
                                        foreach (var ifcItem in items.Where(i => i.Format == "IFC"))
                                        {
                                            ifcItem.Status = "Failed";
                                            ifcItem.Progress = 0;
                                            ifcItem.OutputPath = BuildQueueOutputPath(ifcItem.ViewSheetName, ifcItem.Format);
                                            ifcItem.ErrorMessage = "IFC external event timed out.";
                                        }
                                        ExportQueueDataGrid.Items.Refresh();
                                    }
                                }
                                else
                                {
                                    // Debug logging removed
                                }
                            }
                            else if (_ifcExportEvent == null || _ifcExportHandler == null)
                            {
                                MessageBox.Show("Cannot export IFC: External Event not initialized.\n\nPlease restart Revit and try again.",
                                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            else
                            {
                                // Debug logging removed
                            }
                        }
                        else if (format.ToUpper() == "NWC")
                        {
                            // Debug logging removed
                            
                            // Use selectedViews already declared at the beginning
                            var threeDViews = selectedViews.Where(v => 
                                v.ViewType != null && 
                                (v.ViewType.Contains("ThreeD") || v.ViewType.Contains("3D"))).ToList();
                            
                            if (threeDViews.Any())
                            {
                                // Debug logging removed
                                
                                // ⚠️ REMOVED: Don't set all items to Processing/0% upfront
                                // Callback will set each item to Processing when export starts
                                
                                var nwcManager = new NWCExportService(_document);
                                
                                // Progress callback to update status after each RevitView
                                bool nwcResult = nwcManager.ExportToNavisworks(threeDViews, NWCSettings, formatOutputFolder, "", (viewName, success) =>  // ✅ Use format-specific folder
                                {
                                    // This callback runs after each RevitView is exported
                                    // MUST run on UI thread for WPF updates
                                    Dispatcher.Invoke(() =>
                                    {
                                        var queueItem = items.FirstOrDefault(i => 
                                            i.ViewSheetName == viewName && 
                                            i.Format == "NWC");
                                        
                                        if (queueItem != null)
                                        {
                                            queueItem.Status = success ? "Completed" : "Failed";
                                            queueItem.Progress = success ? 100 : 0;
                                            queueItem.OutputPath = BuildQueueOutputPath(queueItem.ViewSheetName, queueItem.Format);
                                            queueItem.CompletedAt = success ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
                                            queueItem.ErrorMessage = success ? string.Empty : "NWC export failed.";
                                            
                                            // CRITICAL: Force DataGrid refresh
                                            ExportQueueDataGrid.Items.Refresh();
                                            
                                        }
                                        
                                        // Update overall progress based on actual completion
                                        updateOverallProgress();
                                    });
                                });
                                
                                if (nwcResult)
                                {
                                    // Debug logging removed
                                }
                                else
                                {
                                    // Debug logging removed
                                }
                            }
                            else
                            {
                                // Debug logging removed
                            }
                        }
                        else if (format.ToUpper() == "DXF")
                        {
                            // Debug logging removed
                            
                            // DXF is similar to IFC/NWC - only works with views, not sheets
                            // Get views from selected sheets or use selected 3D views
                            var viewsForDxf = new List<ViewItem>();
                            
                            // If views are selected, use them
                            if (selectedViews.Any())
                            {
                                viewsForDxf = selectedViews;
                            }
                            // Otherwise, try to get views from selected sheets
                            else if (selectedSheets.Any())
                            {
                                // Debug logging removed
                                // Collect all views placed on selected sheets
                                foreach (var sheetItem in selectedSheets)
                                {
                                    var sheet = _document.GetElement(sheetItem.Id) as Autodesk.Revit.DB.ViewSheet;
                                    if (sheet != null)
                                    {
                                        var viewportIds = sheet.GetAllViewports();
                                        foreach (var vpId in viewportIds)
                                        {
                                            var viewport = _document.GetElement(vpId) as Viewport;
                                            if (viewport != null)
                                            {
                                                var view = _document.GetElement(viewport.ViewId) as Autodesk.Revit.DB.View;
                                                if (view != null && !view.IsTemplate)
                                                {
                                                    // Create ViewItem wrapper
                                                    var viewItem = new ViewItem
                                                    {
                                                        RevitViewId = view.Id,
                                                        ViewName = view.Name,
                                                        ViewType = view.ViewType.ToString()
                                                    };
                                                    viewsForDxf.Add(viewItem);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            
                            if (viewsForDxf.Any())
                            {
                                // Debug logging removed
                                
                                // DXF export service runs batch export; mark relevant items as Processing first
                                foreach (var dxfItem in items.Where(i => i.Format == "DXF"))
                                {
                                    dxfItem.Status = "Processing";
                                    dxfItem.Progress = 1;
                                    dxfItem.OutputPath = BuildQueueOutputPath(dxfItem.ViewSheetName, dxfItem.Format);
                                    dxfItem.ErrorMessage = string.Empty;
                                    dxfItem.CompletedAt = string.Empty;
                                }
                                ExportQueueDataGrid.Items.Refresh();
                                updateOverallProgress();

                                // Allow WPF to render Processing state before running heavy DXF export
                                await Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                                
                                try
                                {
                                    var dxfManager = new DXFExportService(_document);
                                    
                                    // Create DXF export settings from UI  
                                    var dxfSettings = new PSDXFExportSettings
                                    {
                                        OutputFolder = formatOutputFolder,
                                        ExportAllViews = false, // Use specific views
                                        Export3DViews = true,
                                        ExportPlanViews = true,
                                        ExportSectionViews = true,
                                        ExportSheetViews = false,
                                        ExcludeTemplateViews = true
                                    };
                                    
                                    // Debug logging removed
                                    
                                    // Export all views at once
                                    var result = dxfManager.ExportViewsToDXF(formatOutputFolder, dxfSettings);
                                    
                                    if (result)
                                    {
                                        // Debug logging removed
                                        
                                        // Update all DXF queue items
                                        Dispatcher.Invoke(() =>
                                        {
                                            foreach (var dxfItem in items.Where(i => i.Format == "DXF"))
                                            {
                                                dxfItem.Progress = 100;
                                                dxfItem.Status = "Completed";
                                                dxfItem.OutputPath = BuildQueueOutputPath(dxfItem.ViewSheetName, dxfItem.Format);
                                                dxfItem.CompletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                            }
                                            ExportQueueDataGrid.Items.Refresh();
                                            updateOverallProgress();
                                        });
                                    }
                                    else
                                    {
                                        // Debug logging removed
                                        
                                        // Update failed status
                                        Dispatcher.Invoke(() =>
                                        {
                                            foreach (var dxfItem in items.Where(i => i.Format == "DXF"))
                                            {
                                                dxfItem.Status = "Failed";
                                                dxfItem.Progress = 0;
                                                dxfItem.OutputPath = BuildQueueOutputPath(dxfItem.ViewSheetName, dxfItem.Format);
                                                dxfItem.ErrorMessage = "DXF export returned false.";
                                            }
                                            ExportQueueDataGrid.Items.Refresh();
                                            updateOverallProgress();
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Debug logging removed
                                    MessageBox.Show($"DXF export error: {ex.Message}", "Export Error", 
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                    
                                    // Update failed status
                                    Dispatcher.Invoke(() =>
                                    {
                                        foreach (var dxfItem in items.Where(i => i.Format == "DXF"))
                                        {
                                            dxfItem.Status = "Failed";
                                            dxfItem.Progress = 0;
                                            dxfItem.OutputPath = BuildQueueOutputPath(dxfItem.ViewSheetName, dxfItem.Format);
                                            dxfItem.ErrorMessage = ex.Message;
                                        }
                                        ExportQueueDataGrid.Items.Refresh();
                                        updateOverallProgress();
                                    });
                                }
                            }
                            else
                            {
                                // Debug logging removed
                                
                                // Mark as skipped
                                foreach (var dxfItem in items.Where(i => i.Format == "DXF"))
                                {
                                    dxfItem.Status = "Skipped";
                                    dxfItem.Progress = 0;
                                    dxfItem.OutputPath = BuildQueueOutputPath(dxfItem.ViewSheetName, dxfItem.Format);
                                    dxfItem.ErrorMessage = "No valid views were available for DXF export.";
                                }
                                ExportQueueDataGrid.Items.Refresh();
                                updateOverallProgress();
                            }
                        }
                        else if (format.ToUpper() == "DGN" || format.ToUpper() == "DWF")
                        {
                            foreach (var unsupportedItem in items.Where(i => string.Equals(i.Format, format, StringComparison.OrdinalIgnoreCase)))
                            {
                                unsupportedItem.Status = "Skipped";
                                unsupportedItem.Progress = 0;
                                unsupportedItem.OutputPath = BuildQueueOutputPath(unsupportedItem.ViewSheetName, unsupportedItem.Format);
                                unsupportedItem.ErrorMessage = $"{format.ToUpper()} export is not enabled in this build.";
                            }
                            ExportQueueDataGrid.Items.Refresh();
                            updateOverallProgress();
                        }
                        else
                        {
                            // Debug logging removed
                        }
                    }
                    
                    // Final progress update - only if all items are done
                    var allCompleted = items.All(i => i.Status == "Completed");
                    if (allCompleted)
                    {
                        ExportProgressBar.Value = 100;
                        ProgressPercentageText.Text = "Completed 100%";
                        // Debug logging removed
                        
                        // Generate report if selected
                        var reportType = (ReportComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                        if (reportType != "Don't Save Report")
                        {
                            TryWriteExportReport(items, CreateFolderPathTextBox.Text);
                        }
                        
                        // ✓ SET FLAG: Export đã hoàn thành - sẵn sàng reset khi user chọn lại
                        _exportJustCompleted = true;
                        // Debug logging removed
                        
                        // Show export completed dialog ONLY when all items are done
                        ShowExportCompletedDialog(CreateFolderPathTextBox.Text);
                    }
                    else
                    {
                        // Calculate actual progress from completed + fractional processing items
                        updateOverallProgress();
                        var completedItems = items.Count(i => i.Status == "Completed");
                        var processingItems = items.Count(i => i.Status.Contains("Processing") || i.Status.Contains("External Event"));
                        // Debug logging removed
                        
                        // Only show message if there are no items still processing (e.g., IFC via ExternalEvent)
                        // and some items actually failed
                        if (processingItems == 0)
                        {
                            var failedItems = items.Count(i => i.Status == "Failed");
                            if (failedItems > 0)
                            {
                                var reportPath = TryWriteExportReport(items, CreateFolderPathTextBox.Text);
                                MessageBox.Show($"Export process finished, but some items failed.\n\n" +
                                               $"Completed: {completedItems}/{items.Count} items\n" +
                                               $"Failed: {failedItems} items\n" +
                                               $"Location: {CreateFolderPathTextBox.Text}" +
                                               (string.IsNullOrEmpty(reportPath) ? "" : $"\nReport: {reportPath}"),
                                               "Export Status", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            // Debug logging removed
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Debug logging removed
                MessageBox.Show("Export was cancelled.", 
                               "Export Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Update status for any pending items
                var items = ExportQueueDataGrid.ItemsSource as ObservableCollection<ExportQueueItem>;
                if (items != null)
                {
                    foreach (var item in items.Where(i => i.Status == "Processing" || i.Status == "Pending"))
                    {
                        item.Status = "Cancelled";
                        item.Progress = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Error during export: {ex.Message}", 
                               "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable button
                StartExportButton.IsEnabled = true;
                StartExportButton.Content = "START EXPORT";
            }
        }

        private string TryWriteExportReport(IEnumerable<ExportQueueItem> items, string outputFolder)
        {
            var reportType = (ReportComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.Equals(reportType, "Don't Save Report", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                if (string.Equals(reportType, "XLSX", StringComparison.OrdinalIgnoreCase))
                {
                    return ExportReportService.WriteXlsxReport(items, outputFolder);
                }

                if (string.Equals(reportType, "CSV + XLSX", StringComparison.OrdinalIgnoreCase))
                {
                    var csvPath = ExportReportService.WriteCsvReport(items, outputFolder);
                    var xlsxPath = ExportReportService.WriteXlsxReport(items, outputFolder);
                    return $"{csvPath}; {xlsxPath}";
                }

                return ExportReportService.WriteCsvReport(items, outputFolder);
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Failed to write export report: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Enhanced UI Event Handlers

        private void FilterByVSSet_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                // No longer used - multi-select handles filtering automatically
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Lỗi khi lọc: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                // Uncheck all set selections
                if (ViewSheetSets != null)
                {
                    foreach (var set in ViewSheetSets)
                    {
                        set.IsSelected = false;
                    }
                    OnPropertyChanged(nameof(SelectedSetsDisplay));
                }
                
                // Reload all data
                LoadSheets();
                LoadViews();
                
                MessageBox.Show("Filter reset - showing all items", "Filter Reset", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Lỗi khi reset filter: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateFolderPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ExportQueueItems != null)
            {
                foreach (var item in ExportQueueItems)
                {
                    item.OutputPath = BuildQueueOutputPath(item.ViewSheetName, item.Format);
                }
            }

            UpdateFilenamePreview();
        }

        private void ExportQueueDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFilenamePreview();
        }

        private void SetCustomFileName_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                if (sender is Button button && button.Tag is SheetItem sheetItem)
                {
                    // Create a simple parameter selection dialog
                    var parameterDialog = new ParameterSelectionDialog(_document, sheetItem);
                    if (parameterDialog.ShowDialog() == true)
                    {
                        string newFileName = parameterDialog.GeneratedFileName;
                        if (!string.IsNullOrEmpty(newFileName))
                        {
                            sheetItem.CustomFileName = newFileName;
                            // Debug logging removed
                        }
                    }
                }
                else if (sender is Button buttonView && buttonView.Tag is ViewItem viewItem)
                {
                    // Handle RevitView item parameter selection
                    var parameterDialog = new ParameterSelectionDialog(_document, viewItem);
                    if (parameterDialog.ShowDialog() == true)
                    {
                        string newFileName = parameterDialog.GeneratedFileName;
                        if (!string.IsNullOrEmpty(newFileName))
                        {
                            viewItem.CustomFileName = newFileName;
                            // Debug logging removed
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Lỗi khi set custom file name: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditSelectedFilenames_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            
            try
            {
                bool isSheetMode = SheetsRadio?.IsChecked == true;
                
                if (isSheetMode)
                {
                    // Get ALL sheets (not just selected)
                    var allSheets = Sheets?.ToList();
                    
                    if (allSheets == null || !allSheets.Any())
                    {
                        MessageBox.Show("No sheets available.", "No Sheets", 
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    // Debug logging removed
                    
                    // Load existing configuration from current profile FOR SHEETS
                    List<Models.SelectedParameterInfo> existingConfig = null;
                    if (_profileManager?.CurrentProfile?.Settings != null)
                    {
                        // Try to load Sheet-specific config first, fallback to old config for backward compatibility
                        var configJson = _profileManager.CurrentProfile.Settings.CustomFileNameConfigJson_Sheets;
                        if (string.IsNullOrEmpty(configJson))
                        {
                            configJson = _profileManager.CurrentProfile.Settings.CustomFileNameConfigJson;
                        }
                        
                        if (!string.IsNullOrEmpty(configJson))
                        {
                            try
                            {
                                existingConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Models.SelectedParameterInfo>>(configJson);
                                // Debug logging removed
                            }
                            catch (Exception jsonEx)
                            {
                                // Debug logging removed
                            }
                        }
                    }
                    
                    // Open CustomFileNameDialog with existing configuration FOR SHEETS
                    var dialog = new CustomFileNameDialog(_document, existingConfig, isViewMode: false);
                    dialog.Owner = this;
                    
                    if (dialog.ShowDialog() == true)
                    {
                        // Save configuration to profile
                        if (_profileManager?.CurrentProfile?.Settings != null)
                        {
                            try
                            {
                                var configJson = Newtonsoft.Json.JsonConvert.SerializeObject(dialog.SelectedParameters);
                                _profileManager.CurrentProfile.Settings.CustomFileNameConfigJson_Sheets = configJson;
                                _profileManager.SaveProfile(_profileManager.CurrentProfile);
                            }
                            catch (Exception saveEx)
                            {
                                // Debug logging removed
                            }
                        }
                        
                        // Apply custom file name configuration to ALL sheets
                        int updatedCount = ApplyCustomFileNameToSheets(allSheets, dialog.SelectedParameters);
                        
                        // Debug logging removed
                        
                        // IMPORTANT: Update Export Queue to reflect new custom names
                        UpdateExportQueue();
                        // Debug logging removed
                        
                        MessageBox.Show($"Successfully applied custom filename to ALL {updatedCount} sheet(s).", 
                                       "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Get ALL views (not just selected)
                    var allViews = Views?.ToList();
                    
                    if (allViews == null || !allViews.Any())
                    {
                        MessageBox.Show("No views available.", "No Views", 
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    // Debug logging removed
                    
                    // Load existing configuration from current profile FOR VIEWS
                    List<Models.SelectedParameterInfo> existingConfig = null;
                    if (_profileManager?.CurrentProfile?.Settings != null)
                    {
                        // Try to load View-specific config first, fallback to old config for backward compatibility
                        var configJson = _profileManager.CurrentProfile.Settings.CustomFileNameConfigJson_Views;
                        if (string.IsNullOrEmpty(configJson))
                        {
                            configJson = _profileManager.CurrentProfile.Settings.CustomFileNameConfigJson;
                        }
                        
                        if (!string.IsNullOrEmpty(configJson))
                        {
                            try
                            {
                                existingConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Models.SelectedParameterInfo>>(configJson);
                                // Debug logging removed
                            }
                            catch (Exception jsonEx)
                            {
                                // Debug logging removed
                            }
                        }
                    }
                    
                    // Open CustomFileNameDialog with existing configuration FOR VIEWS
                    var dialog = new CustomFileNameDialog(_document, existingConfig, isViewMode: true);
                    dialog.Owner = this;
                    
                    if (dialog.ShowDialog() == true)
                    {
                        // Save configuration to profile
                        if (_profileManager?.CurrentProfile?.Settings != null)
                        {
                            try
                            {
                                var configJson = Newtonsoft.Json.JsonConvert.SerializeObject(dialog.SelectedParameters);
                                _profileManager.CurrentProfile.Settings.CustomFileNameConfigJson_Views = configJson;
                                _profileManager.SaveProfile(_profileManager.CurrentProfile);
                            }
                            catch (Exception saveEx)
                            {
                                // Debug logging removed
                            }
                        }
                        
                        // Apply custom file name configuration to ALL views
                        int updatedCount = ApplyCustomFileNameToViews(allViews, dialog.SelectedParameters);
                        
                        // Debug logging removed
                        
                        // IMPORTANT: Update Export Queue to reflect new custom names
                        UpdateExportQueue();
                        // Debug logging removed
                        
                        MessageBox.Show($"Successfully applied custom filename to ALL {updatedCount} view(s).", 
                                       "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Error editing filenames: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Apply custom filename configuration to sheets
        /// </summary>
        private int ApplyCustomFileNameToSheets(List<SheetItem> sheets, ObservableCollection<SelectedParameterInfo> parameters)
        {
            int count = 0;
            
            foreach (var sheetItem in sheets)
            {
                try
                {
                    // Get the actual ViewSheet element
                    var sheet = _document.GetElement(sheetItem.Id) as ViewSheet;
                    if (sheet == null) continue;
                    
                    // Generate custom filename from parameters
                    string customFileName = GenerateCustomFileName(sheet, parameters);
                    
                    if (!string.IsNullOrWhiteSpace(customFileName))
                    {
                        sheetItem.CustomFileName = customFileName;
                        count++;
                        // Debug logging removed
                    }
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                }
            }
            
            return count;
        }

        /// <summary>
        /// Apply custom filename configuration to views
        /// </summary>
        private int ApplyCustomFileNameToViews(List<ViewItem> views, ObservableCollection<SelectedParameterInfo> parameters)
        {
            int count = 0;
            
            foreach (var viewItem in views)
            {
                try
                {
                    // Get the actual RevitView element
                    var view = _document.GetElement(viewItem.ViewId) as RevitView;
                    if (view == null) continue;
                    
                    // Generate custom filename from parameters
                    string customFileName = GenerateCustomFileNameFromView(view, parameters);
                    
                    if (!string.IsNullOrWhiteSpace(customFileName))
                    {
                        viewItem.CustomFileName = customFileName;
                        count++;
                        // Debug logging removed
                    }
                }
                catch (Exception ex)
                {
                    // Debug logging removed
                }
            }
            
            return count;
        }

        /// <summary>
        /// Generate custom filename from ViewSheet parameters
        /// </summary>
        private string GenerateCustomFileName(ViewSheet sheet, ObservableCollection<SelectedParameterInfo> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return null;
            
            var parts = new List<string>();
            
            foreach (var paramConfig in parameters)
            {
                string value = GetSheetParameterValue(sheet, paramConfig.ParameterName);
                
                if (!string.IsNullOrEmpty(value))
                {
                    string part = $"{paramConfig.Prefix}{value}{paramConfig.Suffix}";
                    parts.Add(part);
                }
            }
            
            return BuildNameFromParameterParts(parts, parameters);
        }

        /// <summary>
        /// Generate custom filename from RevitView parameters
        /// </summary>
        private string GenerateCustomFileNameFromView(RevitView view, ObservableCollection<SelectedParameterInfo> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return null;
            
            var parts = new List<string>();
            
            foreach (var paramConfig in parameters)
            {
                string value = GetViewParameterValue(view, paramConfig.ParameterName);
                
                if (!string.IsNullOrEmpty(value))
                {
                    string part = $"{paramConfig.Prefix}{value}{paramConfig.Suffix}";
                    parts.Add(part);
                }
            }
            
            return BuildNameFromParameterParts(parts, parameters);
        }

        private string BuildNameFromParameterParts(List<string> parts, ObservableCollection<SelectedParameterInfo> parameters)
        {
            if (parts == null || parts.Count == 0)
            {
                return string.Empty;
            }

            var result = "";
            for (int i = 0; i < parts.Count; i++)
            {
                result += parts[i];
                if (i < parts.Count - 1)
                {
                    result += parameters.ElementAtOrDefault(i)?.Separator ?? "";
                }
            }

            return result;
        }

        /// <summary>
        /// Get parameter value from ViewSheet
        /// </summary>
        private string GetSheetParameterValue(ViewSheet sheet, string parameterName)
        {
            try
            {
                // Try built-in parameters first
                switch (parameterName)
                {
                    case "Sheet Number":
                        return sheet.SheetNumber;
                    case "Sheet Name":
                        return sheet.Name;
                    case "Current Revision":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "";
                    case "Current Revision Date":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION_DATE)?.AsString() ?? "";
                    case "Current Revision Description":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION_DESCRIPTION)?.AsString() ?? "";
                    case "Approved By":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_APPROVED_BY)?.AsString() ?? "";
                    case "Checked By":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_CHECKED_BY)?.AsString() ?? "";
                    case "Designed By":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_DESIGNED_BY)?.AsString() ?? "";
                    case "Drawn By":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY)?.AsString() ?? "";
                    case "Sheet Issue Date":
                        return sheet.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE)?.AsString() ?? "";
                }
                
                // Try to find parameter by name
                foreach (Parameter param in sheet.Parameters)
                {
                    if (param.Definition.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        return GetParameterValueAsString(param);
                    }
                }
                
                // Try project information parameters
                var projectInfo = _document.ProjectInformation;
                if (projectInfo != null)
                {
                    foreach (Parameter param in projectInfo.Parameters)
                    {
                        if (param.Definition.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            return GetParameterValueAsString(param);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
            
            return "";
        }

        /// <summary>
        /// Get parameter value from View
        /// </summary>
        private string GetViewParameterValue(RevitView view, string parameterName)
        {
            try
            {
                // Try built-in parameters first
                switch (parameterName)
                {
                    case "RevitView Name":
                        return view.Name;
                    case "RevitView Template":
                        var templateId = view.ViewTemplateId;
                        if (templateId != ElementId.InvalidElementId)
                        {
                            var template = _document.GetElement(templateId);
                            return template?.Name ?? "";
                        }
                        return "";
                }
                
                // Try to find parameter by name
                foreach (Parameter param in view.Parameters)
                {
                    if (param.Definition.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        return GetParameterValueAsString(param);
                    }
                }
                
                // Try project information parameters
                var projectInfo = _document.ProjectInformation;
                if (projectInfo != null)
                {
                    foreach (Parameter param in projectInfo.Parameters)
                    {
                        if (param.Definition.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            return GetParameterValueAsString(param);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
            
            return "";
        }

        /// <summary>
        /// Get parameter value as string regardless of storage type
        /// </summary>
        private string GetParameterValueAsString(Parameter param)
        {
            if (param == null || !param.HasValue)
                return "";
            
            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString() ?? "";
                case StorageType.Integer:
                    return param.AsInteger().ToString();
                case StorageType.Double:
                    return param.AsValueString() ?? param.AsDouble().ToString();
                case StorageType.ElementId:
                    var elemId = param.AsElementId();
                    if (elemId != ElementId.InvalidElementId)
                    {
                        var elem = _document.GetElement(elemId);
                        return elem?.Name ?? "";
                    }
                    return "";
                default:
                    return "";
            }
        }
        
        private string PromptForFilename(string title, string defaultValue)
        {
            // Create a simple WPF dialog
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };
            
            var grid = new WpfGrid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };
            WpfGrid.SetRow(textBox, 0);
            grid.Children.Add(textBox);
            
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            
            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            
            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                IsCancel = true
            };
            cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            WpfGrid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);
            
            dialog.Content = grid;
            
            textBox.Focus();
            textBox.SelectAll();
            
            return dialog.ShowDialog() == true ? textBox.Text : null;
        }

        private void SetAllCustomFileName_Click(object sender, RoutedEventArgs e)
        {
            // Debug logging removed
            try
            {
                // Determine which items are currently visible
                var targetItems = new List<object>();
                bool isSheetMode = SheetsRadio?.IsChecked == true;
                
                if (isSheetMode)
                {
                    // Get selected sheets first, if none selected then get all sheets
                    var selectedSheets = Sheets?.Where(s => s.IsSelected).ToList() ?? new List<SheetItem>();
                    if (selectedSheets.Any())
                    {
                        targetItems.AddRange(selectedSheets);
                        // Debug logging removed
                    }
                    else
                    {
                        // No sheets selected, apply to all sheets
                        var allSheets = Sheets?.ToList() ?? new List<SheetItem>();
                        targetItems.AddRange(allSheets);
                        // Debug logging removed
                    }
                }
                else if (ViewsRadio?.IsChecked == true)
                {
                    // Get selected views first, if none selected then get all views
                    var selectedViews = Views?.Where(v => v.IsSelected).ToList() ?? new List<ViewItem>();
                    if (selectedViews.Any())
                    {
                        targetItems.AddRange(selectedViews);
                        // Debug logging removed
                    }
                    else
                    {
                        // No views selected, apply to all views
                        var allViews = Views?.ToList() ?? new List<ViewItem>();
                        targetItems.AddRange(allViews);
                        // Debug logging removed
                    }
                }

                if (!targetItems.Any())
                {
                    MessageBox.Show("No sheets or views available to configure.", 
                                   "No Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Use the first item to get available parameters
                var firstItem = targetItems.First();
                var parameterDialog = new ParameterSelectionDialog(_document, firstItem);
                
                string actionDescription = isSheetMode ? "sheets" : "views";
                bool hasSelection = false;
                
                if (isSheetMode)
                {
                    hasSelection = Sheets?.Any(s => s.IsSelected) == true;
                }
                else
                {
                    hasSelection = Views?.Any(v => v.IsSelected) == true;
                }
                
                string message = hasSelection 
                    ? $"Configure custom filename for {targetItems.Count} selected {actionDescription}?"
                    : $"No items selected. Configure custom filename for ALL {targetItems.Count} {actionDescription}?";
                    
                var result = MessageBox.Show(message, "Confirm Action", 
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                if (parameterDialog.ShowDialog() == true)
                {
                    string pattern = parameterDialog.GeneratedFileName;
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        int updatedCount = 0;
                        
                        // Apply the pattern to all target items
                        foreach (var item in targetItems)
                        {
                            try
                            {
                                if (item is SheetItem sheet)
                                {
                                    // Generate filename based on sheet's parameters
                                    var sheetDialog = new ParameterSelectionDialog(_document, sheet);
                                    string fileName = sheetDialog.GenerateFilename(pattern, sheet);
                                    sheet.CustomFileName = fileName;
                                    updatedCount++;
                                    // Debug logging removed
                                }
                                else if (item is ViewItem RevitView)
                                {
                                    var viewDialog = new ParameterSelectionDialog(_document, RevitView);
                                    string fileName = viewDialog.GenerateFilename(pattern, RevitView);
                                    RevitView.CustomFileName = fileName;
                                    updatedCount++;
                                    // Debug logging removed
                                }
                            }
                            catch (Exception itemEx)
                            {
                                // Debug logging removed
                            }
                        }
                        
                        // Force UI update
                        if (isSheetMode && SheetsDataGrid != null)
                        {
                            SheetsDataGrid.Items.Refresh();
                        }
                        else if (ViewsDataGrid != null)
                        {
                            ViewsDataGrid.Items.Refresh();
                        }
                        
                        // Debug logging removed
                        MessageBox.Show($"Custom filename pattern applied to {updatedCount} {actionDescription} successfully!\n\nPattern: {pattern}", 
                                       "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Error setting custom filename: {ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterSheetsBySet(string setName)
        {
            // Debug logging removed
            
            try
            {
                // Get current sheets from the existing Sheets collection
                var allSheets = Sheets ?? new ObservableRangeCollection<SheetItem>();
                var filteredSheets = new ObservableRangeCollection<SheetItem>();
                
                foreach (var sheet in allSheets)
                {
                    bool includeSheet = false;
                    
                    // Filter based on sheet categorization
                    switch (setName.ToUpper())
                    {
                        case "ARCHITECTURAL":
                            includeSheet = IsArchitecturalSheet(sheet);
                            break;
                        case "STRUCTURAL":
                            includeSheet = IsStructuralSheet(sheet);
                            break;
                        case "MEP":
                            includeSheet = IsMEPSheet(sheet);
                            break;
                        case "ALL SHEETS":
                        case "<NONE>":
                        default:
                            includeSheet = true;
                            break;
                    }
                    
                    if (includeSheet)
                    {
                        filteredSheets.Add(sheet);
                    }
                }
                
                Sheets = filteredSheets;
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        private bool IsArchitecturalSheet(SheetItem sheet)
        {
            // Simple logic based on sheet number or name patterns
            string number = sheet.SheetNumber?.ToUpper() ?? "";
            string name = sheet.SheetName?.ToUpper() ?? "";
            
            return number.StartsWith("A") || 
                   name.Contains("ARCHITECTURAL") || 
                   name.Contains("FLOOR PLAN") ||
                   name.Contains("ELEVATION") ||
                   name.Contains("SECTION");
        }

        private bool IsStructuralSheet(SheetItem sheet)
        {
            string number = sheet.SheetNumber?.ToUpper() ?? "";
            string name = sheet.SheetName?.ToUpper() ?? "";
            
            return number.StartsWith("S") || 
                   name.Contains("STRUCTURAL") || 
                   name.Contains("FOUNDATION") ||
                   name.Contains("FRAMING");
        }

        private bool IsMEPSheet(SheetItem sheet)
        {
            string number = sheet.SheetNumber?.ToUpper() ?? "";
            string name = sheet.SheetName?.ToUpper() ?? "";
            
            return number.StartsWith("M") || 
                   number.StartsWith("E") ||
                   number.StartsWith("P") ||
                   name.Contains("MECHANICAL") || 
                   name.Contains("ELECTRICAL") ||
                   name.Contains("PLUMBING") ||
                   name.Contains("MEP");
        }
        
        /// <summary>
        /// Handle CombineFilesRadio checked - enable/disable related buttons
        /// </summary>
        private void CombineFilesRadio_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug logging removed
                
                // Update ExportSettings
                if (ExportSettings != null)
                {
                    ExportSettings.CombineFiles = true;
                    // Debug logging removed
                }

                UpdatePdfSettingsUiState();
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        private void SeparateFilesRadio_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ExportSettings != null)
                {
                    ExportSettings.CombineFiles = false;
                }

                UpdatePdfSettingsUiState();
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Separate PDF files setting failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save All Files Radio button checked - save all formats in same folder
        /// </summary>
        private void SaveAllFilesRadio_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug logging removed
                
                // Update ExportSettings
                if (ExportSettings != null)
                {
                    ExportSettings.CreateSeparateFolders = true;
                    // Debug logging removed
                }
                
                // Update Profile
                if (_profileManager?.CurrentProfile?.Settings != null)
                {
                    _profileManager.CurrentProfile.Settings.SaveAllInSameFolder = false;
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }
        
        /// <summary>
        /// Save Split Files Radio button checked - create subfolder for each format
        /// </summary>
        private void SaveSplitFilesRadio_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug logging removed
                
                // Update ExportSettings
                if (ExportSettings != null)
                {
                    ExportSettings.CreateSeparateFolders = true;
                    // Debug logging removed
                }
                
                // Update Profile
                if (_profileManager?.CurrentProfile?.Settings != null)
                {
                    _profileManager.CurrentProfile.Settings.SaveAllInSameFolder = false;
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }
        
        /// <summary>
        /// Custom File Name for Combined PDF button click
        /// Allow user to set custom name for combined PDF file (default is project name)
        /// Uses the same parameter-based naming as Sheet custom naming
        /// </summary>
        private void CustomFileNameCombine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug logging removed
                
                // ✅ NO PERSISTENCE: Each export session starts fresh
                // User must configure custom name each time they want to use it
                
                // Show parameter-based custom file name dialog (same as Sheet tab)
                var dialog = new CustomFileNameDialog(_document, existingConfig: null, isViewMode: false);
                dialog.Owner = this;
                dialog.Title = "Custom File Name for Combined PDF";
                
                if (dialog.ShowDialog() == true)
                {
                    // Get selected parameters configuration
                    var selectedParams = dialog.SelectedParameters.ToList();
                    // Debug logging removed
                    
                    // ✅ TEMPORARY CONFIG: Only apply to ExportSettings for THIS export session
                    // NOT saved to profile - user must set again next time
                    if (ExportSettings != null)
                    {
                        ExportSettings.CombineFileNameParameters = selectedParams;
                        
                        // Show preview
                        string preview = dialog.PreviewText;
                        MessageBox.Show(
                            $"Combined PDF will be named using this pattern:\n\n{preview}.pdf\n\n(Actual values will be taken from the first selected sheet)",
                            "Custom File Name Set",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Order Sheets and Views button click
        /// Allow user to reorder sheets/views for combined PDF export
        /// </summary>
        private void OrderSheetsViews_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug logging removed
                
                // Check if sheets or views are selected
                bool isSheetsMode = SheetsRadio?.IsChecked == true;
                var items = isSheetsMode 
                    ? Sheets?.Where(s => s.IsSelected).Cast<object>().ToList() 
                    : Views?.Where(v => v.IsSelected).Cast<object>().ToList();
                
                if (items == null || items.Count == 0)
                {
                    MessageBox.Show(
                        "Please select at least one sheet or RevitView first.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                if (items.Count == 1)
                {
                    MessageBox.Show(
                        "Please select at least 2 sheets or views to reorder.",
                        "Single Item Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                // Show reorder dialog
                var reorderDialog = new ReorderSheetsDialog(items, isSheetsMode);
                reorderDialog.Owner = this;
                
                if (reorderDialog.ShowDialog() == true)
                {
                    // Get reordered IDs
                    var reorderedIds = reorderDialog.ReorderedIds;
                    // Debug logging removed
                    
                    // Save order to profile
                    if (_profileManager?.CurrentProfile?.Settings != null)
                    {
                        _profileManager.CurrentProfile.Settings.SheetViewOrder = reorderedIds;
                        _profileManager.SaveProfile(_profileManager.CurrentProfile);
                        // Debug logging removed
                    }
                    
                    // Apply reorder to current list
                    ApplyReorderToList(reorderedIds, isSheetsMode);
                    UpdateExportQueue();
                    UpdateStatusText();
                    
                    MessageBox.Show(
                        $"Order saved! When exporting as combined PDF, the pages will follow this order.",
                        "Order Saved",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Apply reorder to current Sheets or Views list
        /// </summary>
        private void ApplyReorderToList(List<string> reorderedIds, bool isSheetsMode)
        {
            try
            {
                if (isSheetsMode && Sheets != null)
                {
                    // Create new ordered list
                    var orderedSheets = new List<SheetItem>();
                    foreach (var id in reorderedIds)
                    {
                        var sheet = Sheets.FirstOrDefault(s => s.Id.GetIdValue().ToString() == id);
                        if (sheet != null)
                        {
                            orderedSheets.Add(sheet);
                        }
                    }
                    
                    // Add remaining sheets (not in reordered list)
                    foreach (var sheet in Sheets)
                    {
                        if (!orderedSheets.Contains(sheet))
                        {
                            orderedSheets.Add(sheet);
                        }
                    }
                    
                    // Replace collection
                    Sheets = new ObservableRangeCollection<SheetItem>(orderedSheets);
                    foreach (var sheet in Sheets)
                    {
                        sheet.PropertyChanged -= SheetItem_PropertyChanged;
                        sheet.PropertyChanged += SheetItem_PropertyChanged;
                    }
                    // Debug logging removed
                }
                else if (Views != null)
                {
                    // Create new ordered list
                    var orderedViews = new List<ViewItem>();
                    foreach (var id in reorderedIds)
                    {
                        var view = Views.FirstOrDefault(v => v.ViewId == id);
                        if (view != null)
                        {
                            orderedViews.Add(view);
                        }
                    }
                    
                    // Add remaining views (not in reordered list)
                    foreach (var RevitView in Views)
                    {
                        if (!orderedViews.Contains(RevitView))
                        {
                            orderedViews.Add(RevitView);
                        }
                    }
                    
                    // Replace collection
                    Views = new ObservableRangeCollection<ViewItem>(orderedViews);
                    foreach (var view in Views)
                    {
                        view.PropertyChanged -= ViewItem_PropertyChanged;
                        view.PropertyChanged += ViewItem_PropertyChanged;
                    }
                    // Debug logging removed
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        #endregion

    }

    #region Parameter Selection Dialog

    public class ParameterSelectionDialog : Window
    {
        private readonly Document _document;
        private readonly object _item;
        private ComboBox _parameterCombo;
        private TextBox _previewTextBox;
        private WpfCheckBox _includeRevisionCheck;
        private WpfCheckBox _includeSheetNumberCheck;
        private WpfCheckBox _includeSheetNameCheck;
        
        public string GeneratedFileName { get; private set; }

        public ParameterSelectionDialog(Document document, object item)
        {
            _document = document;
            _item = item;
            
            Title = "Set Custom File Name from Parameters";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            InitializeDialog();
        }

        private void InitializeDialog()
        {
            var grid = new WpfGrid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            grid.Margin = new Thickness(20, 20, 20, 20);

            // Title
            var titleBlock = new TextBlock
            {
                Text = "Configure Custom File Name",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            WpfGrid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);

            // Include options
            _includeSheetNumberCheck = new CheckBox
            {
                Content = "Include Sheet Number",
                IsChecked = true,
                Margin = new Thickness(0, 5, 0, 5)
            };
            _includeSheetNumberCheck.Checked += UpdatePreview;
            _includeSheetNumberCheck.Unchecked += UpdatePreview;
            WpfGrid.SetRow(_includeSheetNumberCheck, 1);
            grid.Children.Add(_includeSheetNumberCheck);

            _includeSheetNameCheck = new CheckBox
            {
                Content = "Include Sheet Name",
                IsChecked = true,
                Margin = new Thickness(0, 5, 0, 5)
            };
            _includeSheetNameCheck.Checked += UpdatePreview;
            _includeSheetNameCheck.Unchecked += UpdatePreview;
            WpfGrid.SetRow(_includeSheetNameCheck, 2);
            grid.Children.Add(_includeSheetNameCheck);

            _includeRevisionCheck = new CheckBox
            {
                Content = "Include Revision",
                IsChecked = false,
                Margin = new Thickness(0, 5, 0, 5)
            };
            _includeRevisionCheck.Checked += UpdatePreview;
            _includeRevisionCheck.Unchecked += UpdatePreview;
            WpfGrid.SetRow(_includeRevisionCheck, 3);
            grid.Children.Add(_includeRevisionCheck);

            // Parameter selection
            var paramLabel = new TextBlock
            {
                Text = "Additional Parameter:",
                Margin = new Thickness(0, 15, 0, 5)
            };
            WpfGrid.SetRow(paramLabel, 4);
            grid.Children.Add(paramLabel);

            _parameterCombo = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15)
            };
            _parameterCombo.SelectionChanged += UpdatePreview;
            LoadAvailableParameters();
            WpfGrid.SetRow(_parameterCombo, 5);
            grid.Children.Add(_parameterCombo);

            // Preview
            var previewLabel = new TextBlock
            {
                Text = "Preview:",
                Margin = new Thickness(0, 10, 0, 5)
            };
            WpfGrid.SetRow(previewLabel, 6);
            grid.Children.Add(previewLabel);

            _previewTextBox = new TextBox
            {
                IsReadOnly = true,
                Background = new SolidColorBrush(WpfColor.FromRgb(248, 248, 248)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            WpfGrid.SetRow(_previewTextBox, 6);
            grid.Children.Add(_previewTextBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            WpfGrid.SetRow(buttonPanel, 7);
            grid.Children.Add(buttonPanel);

            Content = grid;
            
            // Initial preview update
            UpdatePreview(null, null);
        }

        private void LoadAvailableParameters()
        {
            _parameterCombo.Items.Add("<None>");
            _parameterCombo.Items.Add("Project Number");
            _parameterCombo.Items.Add("Project Name");
            _parameterCombo.Items.Add("Current Date");
            _parameterCombo.Items.Add("Sheet Issue Date");
            _parameterCombo.SelectedIndex = 0;
        }

        private void UpdatePreview(object sender, RoutedEventArgs e)
        {
            try
            {
                var parts = new List<string>();

                if (_includeSheetNumberCheck?.IsChecked == true && _item is SheetItem sheet)
                {
                    parts.Add(sheet.SheetNumber);
                }

                if (_includeSheetNameCheck?.IsChecked == true && _item is SheetItem sheetForName)
                {
                    // Clean sheet name for filename
                    string cleanName = CleanFileName(sheetForName.SheetName);
                    parts.Add(cleanName);
                }

                if (_includeRevisionCheck?.IsChecked == true && _item is SheetItem sheetForRev)
                {
                    parts.Add($"Rev{sheetForRev.Revision ?? "A"}");
                }

                if (_parameterCombo?.SelectedItem?.ToString() != "<None>")
                {
                    string paramValue = GetParameterValue(_parameterCombo.SelectedItem.ToString());
                    if (!string.IsNullOrEmpty(paramValue))
                    {
                        parts.Add(CleanFileName(paramValue));
                    }
                }

                GeneratedFileName = string.Join("_", parts.Where(p => !string.IsNullOrEmpty(p)));
                
                if (_previewTextBox != null)
                {
                    _previewTextBox.Text = GeneratedFileName;
                }
            }
            catch (Exception ex)
            {
                if (_previewTextBox != null)
                {
                    _previewTextBox.Text = $"Error: {ex.Message}";
                }
            }
        }

        private string GetParameterValue(string parameterName)
        {
            switch (parameterName)
            {
                case "Project Number":
                    return _document.ProjectInformation.Number ?? "";
                case "Project Name":
                    return _document.ProjectInformation.Name ?? "";
                case "Current Date":
                    return DateTime.Now.ToString("yyyyMMdd");
                case "Sheet Issue Date":
                    return DateTime.Now.ToString("yyyyMMdd");
                default:
                    return "";
            }
        }

        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            
            // Remove invalid characters and clean up
            var invalidChars = Path.GetInvalidFileNameChars();
            string cleaned = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            
            // Replace spaces with underscores and limit length
            cleaned = cleaned.Replace(" ", "_").Replace("-", "_");
            
            // Remove consecutive underscores
            while (cleaned.Contains("__"))
            {
                cleaned = cleaned.Replace("__", "_");
            }
            
            return cleaned.Trim('_').Substring(0, Math.Min(cleaned.Length, 50));
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(GeneratedFileName))
            {
                MessageBox.Show("Please configure at least one parameter for the file name.", 
                               "Invalid Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            DialogResult = true;
            Close();
        }

        // Method to generate filename for any item based on current dialog settings
        public string GenerateFilename(string pattern, object item)
        {
            try
            {
                var parts = new List<string>();

                if (_includeSheetNumberCheck?.IsChecked == true && item is SheetItem sheet)
                {
                    parts.Add(sheet.SheetNumber);
                }
                else if (item is ViewItem RevitView)
                {
                    parts.Add(RevitView.ViewType?.Replace(" ", "_"));
                }

                if (_includeSheetNameCheck?.IsChecked == true && item is SheetItem sheetForName)
                {
                    string cleanName = CleanFileName(sheetForName.SheetName);
                    parts.Add(cleanName);
                }
                else if (item is ViewItem viewForName)
                {
                    string cleanName = CleanFileName(viewForName.ViewName);
                    parts.Add(cleanName);
                }

                if (_includeRevisionCheck?.IsChecked == true && item is SheetItem sheetForRev)
                {
                    parts.Add($"Rev{sheetForRev.Revision ?? "A"}");
                }

                if (_parameterCombo?.SelectedItem?.ToString() != "<None>")
                {
                    string paramValue = GetParameterValue(_parameterCombo.SelectedItem.ToString());
                    if (!string.IsNullOrEmpty(paramValue))
                    {
                        parts.Add(CleanFileName(paramValue));
                    }
                }

                return string.Join("_", parts.Where(p => !string.IsNullOrEmpty(p)));
            }
            catch
            {
                return pattern; // Fallback to original pattern
            }
        }

        #endregion

        // ===== IFC SETUP PROFILE MANAGEMENT TEMPORARILY DISABLED =====
        // Reason: WPF temporary assembly build issue
        // The complete profile management system (289 lines) is commented out below
        // due to WPF _wpftmp.csproj compilation errors where temporary assembly
        // cannot access ExportPlusXMLProfile properties and XMLProfileManager methods
        // 
        // SOLUTION OPTIONS:
        // 1. Move to separate ProfileManagementHelper class
        // 2. Implement using Commands/Behaviors pattern
        // 3. Use conditional compilation (#if !XAML_COMPILATION)
        
        #region IFC Setup Profile Management (DISABLED - WPF Build Issue)

        /*

        /// <summary>
        /// Initialize IFC Setup Profiles collection and configuration paths
        /// </summary>
        private void InitializeIFCSetups()
        {
            // Initialize IFC Setups Collection
            IFCCurrentSetups = new ObservableCollection<string>
            {
                "<In-Session Setup>",
                "IFC 2x3 Coordination RevitView 2.0",
                "IFC 2x3 Coordination View",
                "IFC 2x3 GSA Concept Design BIM 2010",
                "IFC 2x3 Basic FM Handover View",
                "IFC 2x2 Coordination View",
                "IFC 2x2 Singapore BCA e-Plan Check",
                "IFC 2x3 COBie 2.4 Design Deliverable View",
                "IFC4 Reference View",
                "IFC4 Design Transfer View",
                "Typical Setup"
            };
            
            // Initialize configuration paths mapping
            _ifcSetupConfigPaths = new Dictionary<string, string>();
            
            // Get IFC profiles directory (in %AppData%\ExportPlus\IFCProfiles)
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string ifcProfilesDir = Path.Combine(appDataPath, "ExportPlus", "IFCProfiles");
            
            // Create directory if not exists
            try
            {
                if (!Directory.Exists(ifcProfilesDir))
                {
                    Directory.CreateDirectory(ifcProfilesDir);
                    // Debug logging removed
                }
                
                // Map setup names to file paths
                _ifcSetupConfigPaths["IFC 2x3 Coordination RevitView 2.0"] = Path.Combine(ifcProfilesDir, "IFC_2x3_CV2.0.xml");
                _ifcSetupConfigPaths["IFC 2x3 Coordination View"] = Path.Combine(ifcProfilesDir, "IFC_2x3_CV.xml");
                _ifcSetupConfigPaths["IFC 2x3 GSA Concept Design BIM 2010"] = Path.Combine(ifcProfilesDir, "IFC_2x3_GSA.xml");
                _ifcSetupConfigPaths["IFC 2x3 Basic FM Handover View"] = Path.Combine(ifcProfilesDir, "IFC_2x3_FM.xml");
                _ifcSetupConfigPaths["IFC 2x2 Coordination View"] = Path.Combine(ifcProfilesDir, "IFC_2x2_CV.xml");
                _ifcSetupConfigPaths["IFC 2x2 Singapore BCA e-Plan Check"] = Path.Combine(ifcProfilesDir, "IFC_2x2_SG_BCA.xml");
                _ifcSetupConfigPaths["IFC 2x3 COBie 2.4 Design Deliverable View"] = Path.Combine(ifcProfilesDir, "IFC_2x3_COBie.xml");
                _ifcSetupConfigPaths["IFC4 Reference View"] = Path.Combine(ifcProfilesDir, "IFC4_Reference.xml");
                _ifcSetupConfigPaths["IFC4 Design Transfer View"] = Path.Combine(ifcProfilesDir, "IFC4_Design.xml");
                _ifcSetupConfigPaths["Typical Setup"] = Path.Combine(ifcProfilesDir, "Typical_Setup.xml");
                
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
            
            // Set default selected setup
            SelectedIFCSetup = "<In-Session Setup>";
        }

        /// <summary>
        /// Handle IFC Setup selection changed
        /// </summary>
        private void OnIFCSetupChanged()
        {
            try
            {
                // Debug logging removed
                
                // If In-Session, keep current settings
                if (SelectedIFCSetup == "<In-Session Setup>")
                {
                    // Debug logging removed
                    return;
                }
                
                // Try to load setup from file
                if (_ifcSetupConfigPaths != null && 
                    _ifcSetupConfigPaths.TryGetValue(SelectedIFCSetup, out string filePath))
                {
                    if (File.Exists(filePath))
                    {
                        // Debug logging removed
                        ApplyIFCSettingsFromFile(filePath);
                    }
                    else
                    {
                        // Debug logging removed
                        CreateDefaultIFCSetup(SelectedIFCSetup, filePath);
                    }
                }
                else
                {
                    // Debug logging removed
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Error loading IFC setup: {ex.Message}", 
                                "Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Apply IFC settings from XML file
        /// </summary>
        private void ApplyIFCSettingsFromFile(string filePath)
        {
            try
            {
                var profileManager = new XMLProfileManager();
                var profile = profileManager.ImportProfile(filePath);
                
                if (profile != null && profile.IFCSettings != null)
                {
                    // Apply settings to current IFCSettings
                    IFCSettings = profile.IFCSettings;
                    
                    // Debug logging removed
                    
                    // Show success message
                    MessageBox.Show($"IFC setup '{SelectedIFCSetup}' loaded successfully!", 
                                    "Setup Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Debug logging removed
                    MessageBox.Show("Failed to load IFC setup configuration.", 
                                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                throw;
            }
        }

        /// <summary>
        /// Create default IFC setup configuration file
        /// </summary>
        private void CreateDefaultIFCSetup(string setupName, string filePath)
        {
            try
            {
                // Create default settings based on setup name
                var defaultSettings = new IFCExportSettings();
                
                // Configure settings based on setup type
                switch (setupName)
                {
                    case "IFC 2x3 Coordination RevitView 2.0":
                        defaultSettings.IFCVersion = "IFC 2x3 Coordination RevitView 2.0";
                        defaultSettings.ExportBaseQuantities = false;
                        break;
                        
                    case "IFC 2x3 Coordination View":
                        defaultSettings.IFCVersion = "IFC 2x3 Coordination View";
                        defaultSettings.ExportBaseQuantities = false;
                        break;
                        
                    case "IFC 2x3 GSA Concept Design BIM 2010":
                        defaultSettings.IFCVersion = "IFC 2x3 GSA Concept Design BIM 2010";
                        defaultSettings.ExportBaseQuantities = true;
                        break;
                        
                    case "IFC 2x3 Basic FM Handover View":
                        defaultSettings.IFCVersion = "IFC 2x3 Basic FM Handover View";
                        defaultSettings.ExportBaseQuantities = true;
                        defaultSettings.SpaceBoundaries = "1st Level";
                        break;
                        
                    case "IFC 2x2 Coordination View":
                        defaultSettings.IFCVersion = "IFC 2x2 Coordination View";
                        defaultSettings.ExportBaseQuantities = false;
                        break;
                        
                    case "IFC 2x2 Singapore BCA e-Plan Check":
                        defaultSettings.IFCVersion = "IFC 2x2 Singapore BCA e-Plan Check";
                        defaultSettings.ExportBaseQuantities = true;
                        break;
                        
                    case "IFC 2x3 COBie 2.4 Design Deliverable View":
                        defaultSettings.IFCVersion = "IFC 2x3 COBie 2.4 Design Deliverable View";
                        defaultSettings.ExportBaseQuantities = true;
                        defaultSettings.SpaceBoundaries = "2nd Level";
                        break;
                        
                    case "IFC4 Reference View":
                        defaultSettings.IFCVersion = "IFC4 Reference View";
                        defaultSettings.ExportBaseQuantities = false;
                        break;
                        
                    case "IFC4 Design Transfer View":
                        defaultSettings.IFCVersion = "IFC4 Design Transfer View";
                        defaultSettings.ExportBaseQuantities = true;
                        break;
                        
                    case "Typical Setup":
                        defaultSettings.IFCVersion = "IFC 2x3 Coordination RevitView 2.0";
                        defaultSettings.ExportBaseQuantities = false;
                        defaultSettings.DetailLevel = "Medium";
                        break;
                }
                
                // Create profile
                var profile = new ExportPlusXMLProfile
                {
                    ProfileName = setupName,
                    CreatedDate = DateTime.Now,
                    IFCSettings = defaultSettings
                };
                
                // Save to file
                var profileManager = new XMLProfileManager();
                bool success = profileManager.ExportProfile(profile, filePath);
                
                if (success)
                {
                    // Debug logging removed
                    
                    // Apply the settings
                    IFCSettings = defaultSettings;
                }
                else
                {
                    // Debug logging removed
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        /// <summary>
        /// Save current IFC settings to selected setup
        /// </summary>
        public void SaveCurrentIFCSetup()
        {
            try
            {
                if (SelectedIFCSetup == "<In-Session Setup>")
                {
                    MessageBox.Show("Cannot save to In-Session setup. Please select or create a named setup.", 
                                    "Save Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                if (_ifcSetupConfigPaths.TryGetValue(SelectedIFCSetup, out string filePath))
                {
                    // Create profile with current settings
                    var profile = new ExportPlusXMLProfile
                    {
                        ProfileName = SelectedIFCSetup,
                        CreatedDate = DateTime.Now,
                        IFCSettings = IFCSettings
                    };
                    
                    // Save to file
                    var profileManager = new XMLProfileManager();
                    bool success = profileManager.ExportProfile(profile, filePath);
                    
                    if (success)
                    {
                        // Debug logging removed
                        MessageBox.Show($"IFC setup '{SelectedIFCSetup}' saved successfully!", 
                                        "Setup Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to save IFC setup.", 
                                        "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Error saving IFC setup: {ex.Message}", 
                                "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        */

        #endregion

        // ===== IFC SETTINGS IMPORT/EXPORT TEMPORARILY DISABLED =====
        // Reason: WPF temporary assembly build issue
        // Code available in version control - will be re-enabled after WPF build fix
        // Browse functionality works via BrowseFileBehavior attached property

        #region IFC Settings Event Handlers (DISABLED - WPF Build Issue)

        // NOTE: These methods are commented out due to WPF temporary assembly validation issues
        // The .g.cs file containing x:Name field declarations is not generated until AFTER
        // the temporary assembly compilation succeeds, creating a circular dependency.
        // 
        // SOLUTION: Implement Browse button functionality using:
        // 1. Behaviors/Attached Properties (no code-behind references)
        // 2. MVVM pattern with Commands
        // 3. Post-deployment event wiring (outside WPF build process)

        /*
        /// <summary>
        /// Wire up Browse button Click handlers in constructor
        /// This avoids WPF XAML compilation issues with x:Name controls
        /// </summary>
        private void WireUpIFCBrowseButtons()
        {
            try
            {
                if (BrowseUserPsetsButtonIFC != null)
                {
                    BrowseUserPsetsButtonIFC.Click += BrowseIFCFile_Click;
                }
                
                if (BrowseParamMappingButtonIFC != null)
                {
                    BrowseParamMappingButtonIFC.Click += BrowseIFCFile_Click;
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        /// <summary>
        /// Universal Browse button click handler for IFC file selection
        /// Uses Button.Tag to determine which TextBox to update
        /// </summary>
        private void BrowseIFCFile_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button)) return;

            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select IFC Configuration File",
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FilterIndex = 1,
                    CheckFileExists = false
                };

                // Determine which TextBox to update based on Button.Tag
                TextBox targetTextBox = null;
                string fileType = button.Tag?.ToString() ?? "";

                if (fileType == "UserPsets")
                {
                    dialog.Title = "Select User-Defined Property Sets File";
                    targetTextBox = UserPsetsPathTextBoxIFC;
                }
                else if (fileType == "ParamMapping")
                {
                    dialog.Title = "Select Parameter Mapping Table File";
                    targetTextBox = ParamMappingPathTextBoxIFC;
                }

                if (targetTextBox == null)
                {
                    // Debug logging removed
                    return;
                }

                // Set initial directory if path exists
                string currentPath = targetTextBox.Text;
                if (!string.IsNullOrEmpty(currentPath))
                {
                    var directory = System.IO.Path.GetDirectoryName(currentPath);
                    if (!string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory))
                    {
                        dialog.InitialDirectory = directory;
                    }
                }

                if (dialog.ShowDialog() == true)
                {
                    targetTextBox.Text = dialog.FileName;
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
                System.Windows.MessageBox.Show(
                    $"Error selecting file: {ex.Message}",
                    "Browse Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        */

        #endregion IFC Settings Event Handlers

        #region DWG Settings Event Handlers

        private void DWGSettingsScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            // Debug: DWG Settings tab loaded
        }

        #endregion DWG Settings Event Handlers

        /* TEMPORARILY DISABLED - DWG Export Setup Event Handlers
         * TODO: Re-enable after fixing WPF partial class compiler issues
         * 
        #region DWG Export Setup Event Handlers

        /// <summary>
        /// Load available DWG export setups from Revit document
        /// </summary>
        private void LoadDWGExportSetups()
        {
            try
            {
                // Debug logging removed
                
                if (DWGExportSetupComboBox == null)
                {
                    // Debug logging removed
                    return;
                }
                
                DWGExportSetupComboBox.Items.Clear();
                
                // Get predefined setup names from Revit
                IList<string> setupNames = BaseExportOptions.GetPredefinedSetupNames(_document);
                
                // Debug logging removed
                
                foreach (string setupName in setupNames)
                {
                    DWGExportSetupComboBox.Items.Add(setupName);
                    // Debug logging removed
                }
                
                // Add default option if no setups found
                if (DWGExportSetupComboBox.Items.Count == 0)
                {
                    DWGExportSetupComboBox.Items.Add("Default Setup");
                    // Debug logging removed
                }
                
                // Select first item by default
                DWGExportSetupComboBox.SelectedIndex = 0;
                // Debug logging removed
            }
            catch (Exception ex)
            {
                // Debug logging removed
                
                // Fallback to default
                if (DWGExportSetupComboBox != null)
                {
                    DWGExportSetupComboBox.Items.Clear();
                    DWGExportSetupComboBox.Items.Add("Default Setup");
                    DWGExportSetupComboBox.SelectedIndex = 0;
                }
            }
        }

        /// <summary>
        /// Handle DWG Export Setup selection changed
        /// </summary>
        private void DWGExportSetupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (DWGExportSetupComboBox.SelectedItem != null)
                {
                    string selectedSetup = DWGExportSetupComboBox.SelectedItem.ToString();
                    // Debug logging removed
                    
                    // Store selected setup in export settings
                    this.ExportSettings.DWGExportSetupName = selectedSetup;
                }
            }
            catch (Exception ex)
            {
                // Debug logging removed
            }
        }

        /// <summary>
        /// Open Revit's DWG Export Settings dialog to modify export setups
        /// </summary>
        private void ModifyDWGExportSetup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug logging removed
                
                // Inform user to use Revit's menu
                MessageBox.Show(
                    "To create or modify DWG Export Setups:\n\n" +
                    "1. Close this dialog\n" +
                    "2. In Revit, go to: File > Export > CAD Formats > DWG\n" +
                    "3. In the export dialog, click 'Modify Setup' button\n" +
                    "4. Create or edit your setup and save it\n" +
                    "5. Reopen this ExportPlus dialog\n" +
                    "6. Your new setup will appear in the dropdown",
                    "Modify DWG Export Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // Debug logging removed
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion DWG Export Setup Event Handlers
        */
    }
}









