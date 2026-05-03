using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Licorp.Diagnostics;
using LicorpExportPlus.Helpers;
using ricaun.Revit.DI;
using ricaun.Revit.UI.Tasks;
using ricaun.DI;
using System.Windows.Media.Imaging;

namespace LicorpExportPlus
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportPlusApplication : IExternalApplication
    {
        private static readonly string AddInName = typeof(ExportPlusApplication).Namespace;
        private static readonly string TabName = "Licorp";
        private static readonly string PanelName = "Export";

        private static RevitTaskService _revitTaskService;
        public static IRevitTask RevitTask => _revitTaskService;
        public static Container Container { get; private set; }

        static ExportPlusApplication()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            LicorpTrace.Init("ExportPlus");
            LicorpTrace.Section("OnStartup");
            LicorpTrace.Info("ExportPlusApplication starting...");

            try
            {
                using var startupScope = RevitExecutionScope.Create();

                Container = new Container();
                Container.AddRevitSingleton(application);
                LicorpTrace.Info("DI Container created.");

                _revitTaskService = new RevitTaskService(application);
                _revitTaskService.Initialize();
                Container.AddSingleton<IRevitTask>(_revitTaskService);
                LicorpTrace.Info("RevitTaskService initialized.");

                CreateRibbonTab(application);
                LicorpTrace.Add("Ribbon Tab created.");

                LicorpTrace.Add("OnStartup completed successfully.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                LicorpTrace.Error("ExportPlusApplication startup failed.", ex);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            LicorpTrace.Info("ExportPlusApplication shutting down...");
            _revitTaskService?.Dispose();
            LicorpTrace.Info("OnShutdown completed.");
            return Result.Succeeded;
        }

        private void CreateRibbonTab(UIControlledApplication application)
        {
            try
            {
                LicorpTrace.Info($"Creating Ribbon Tab: {TabName}");
                application.CreateRibbonTab(TabName);
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"CreateRibbonTab error (might already exist): {ex.Message}");
            }

            RibbonPanel panel = application.GetRibbonPanels(TabName)
                .FirstOrDefault(p => p.Name == PanelName);

            if (panel == null)
            {
                LicorpTrace.Info($"Creating Ribbon Panel: {PanelName}");
                panel = application.CreateRibbonPanel(TabName, PanelName);
            }

            if (!panel.GetItems().Any(item => item.Name == "ExportPlus"))
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                LicorpTrace.Dbg($"Assembly path: {assemblyPath}");

                PushButtonData buttonData = new PushButtonData(
                    "ExportPlus",
                    "Export+",
                    assemblyPath,
                    typeof(ExportPlusCommand).FullName);

                var smallIcon = LoadRibbonIcon("export_plus_16.png");
                var largeIcon = LoadRibbonIcon("export_plus_32.png");

                if (smallIcon != null)
                    buttonData.Image = smallIcon;

                if (largeIcon != null)
                    buttonData.LargeImage = largeIcon;

                PushButton pushButton = panel.AddItem(buttonData) as PushButton;

                if (pushButton != null)
                {
                    pushButton.ToolTip = "Export+ - Batch export to PDF, DWG, IFC, NWC, Image";
                    LicorpTrace.Add("PushButton created successfully.");
                }
                else
                {
                    LicorpTrace.Warn("PushButton is null!");
                }
            }
            else
            {
                LicorpTrace.Info("ExportPlus button already exists.");
            }
        }

        private static BitmapImage LoadRibbonIcon(string fileName)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                // Load from embedded WPF Resource to avoid deployment path issues.
                // Resource path is defined by Shared project:
                //   Source/LicorpExportPlus.Shared/Resources/Icons/*
                var packUri = $"pack://application:,,,/LicorpExportPlus;component/Resources/Icons/{fileName}";
                bitmap.UriSource = new Uri(packUri, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"Failed to load ribbon icon '{fileName}': {ex.Message}");
                return null;
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            var folder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var assemblyFile = System.IO.Path.Combine(folder, assemblyName.Name + ".dll");
            if (System.IO.File.Exists(assemblyFile))
            {
                return Assembly.LoadFrom(assemblyFile);
            }

            return null;
        }
    }
}
