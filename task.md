# Licorp_Export+ — Task Tracker

## Decisions ✅
- **Assembly**: `LicorpExportPlus.dll`
- **ricaun**: Dùng cho tất cả versions
- **EPPlus + ClosedXML**: Giữ cả 2
- **Primary source**: `Export + _2025-2026` (chuẩn, tính năng tốt nhất)
- **Ribbon**: Tab "Licorp", Panel "Export"
- **Scope**: CHỈ Export+ features, KHÔNG MEP Tools
- **Template**: Theo pattern Licorp_ScheduleEditor (R20/R25/Shared)

---

## Phase 1: Project Skeleton (Ngày 1-2)
- [ ] Tạo folder structure: `Source/LicorpExportPlus.{R20,R25,Shared}`
- [ ] `Directory.Build.props` (Company=Licorp, VendorId=LICORP)
- [ ] `Directory.Build.targets` (auto-deploy addin manifest)
- [ ] `.gitignore`
- [ ] `LicorpExportPlus.Shared.shproj` + `.projitems` (auto-include *.cs, Views/*.xaml)
- [ ] `LicorpExportPlus.R20.csproj` (net48, configs: Debug + Release R20..R25)
- [ ] `LicorpExportPlus.R25.csproj` (net8.0-windows, configs: Debug + Release R26)
- [ ] `LicorpExportPlus.sln` (3 projects: R20, R25, Shared)
- [ ] `ExportPlusApplication.cs` (IExternalApplication, Ribbon "Licorp" → Panel "Export")
- [ ] `ExportPlusCommand.cs` (IExternalCommand, window caching)
- [ ] `Logger.cs` (Serilog wrapper)
- [ ] `BaseViewModel.cs` (INotifyPropertyChanged base)
- [ ] `deploy-bundle.ps1` (build + deploy tất cả versions)
- [ ] Git init
- [ ] ✅ Verify: `dotnet build` R20 + R25 passes (empty shell)

## Phase 2: Migrate Models + Utils (Ngày 3-4)
Source: `G:\My Drive\Tool Revit Code\Export + _2025-2026\Export\`
- [ ] Migrate Models/ (17 files) → `Shared/Models/`
- [ ] Migrate Utils/ (10 files) → `Shared/Utils/`
- [ ] Migrate Converters/ (2 files) → `Shared/Converters/`
- [ ] Migrate Helpers/ (FileNameHelper.cs) → `Shared/Utils/`
- [ ] Namespace rename: `Quoc_MEP.Export.*` → `LicorpExportPlus.*`
- [ ] Copy Resources/Icons/sheet.png → `Shared/Resources/Icons/`
- [ ] ✅ Verify: Build passes với models

## Phase 3: Migrate Services (Ngày 5-8)
Source: `G:\My Drive\Tool Revit Code\Export + _2025-2026\Export\Managers\`
- [ ] PDFExportManager.cs → `Services/PDFExportService.cs`
- [ ] PDFExportManager_PrintManager.cs → `Services/PDFPrintService.cs`
- [ ] PDFOptionsApplier.cs → `Services/PDFOptionsApplier.cs`
- [ ] DWGExportManager.cs → `Services/DWGExportService.cs`
- [ ] DXFExportManager.cs → `Services/DXFExportService.cs`
- [ ] IFCExportManager.cs → `Services/IFCExportService.cs`
- [ ] NavisworksExportManager.cs → `Services/NWCExportService.cs`
- [ ] ImageExportManager.cs → `Services/ImageExportService.cs`
- [ ] XMLExportManager.cs → `Services/XMLExportService.cs`
- [ ] BatchExportManager.cs → `Services/BatchExportService.cs`
- [ ] ProfileManager.cs + ProfileManagerService.cs → `Services/ProfileService.cs`
- [ ] XMLProfileManager.cs → `Services/XMLProfileService.cs`
- [ ] ExportPlusProfileManager.cs → `Services/ExportProfileService.cs`
- [ ] ViewSheetSetManager.cs → `Services/ViewSheetSetService.cs`
- [ ] PaperSizeManager.cs → `Services/PaperSizeService.cs`
- [ ] DWGCleanupManager.cs → `Services/DWGCleanupService.cs`
- [ ] AutoCADBindManager.cs → `Services/AutoCADBindService.cs`
- [ ] DrawingTransmittalManager.cs → `Services/DrawingTransmittalService.cs`
- [ ] SchedulingAssistant.cs → `Services/SchedulingAssistant.cs`
- [ ] SheetBatchLoader.cs → `Services/SheetLoaderService.cs`
- [ ] FileNameGenerator.cs → `Services/FileNameGeneratorService.cs`
Source: `Export + _2025-2026\Export\Commands\` + `Events\`
- [ ] ExportHandler.cs → `Events/ExportHandler.cs`
- [ ] IFCExportHandler.cs → `Events/IFCExportHandler.cs`
- [ ] PDFExportEventHandler.cs → `Events/PDFExportEventHandler.cs`
- [ ] ViewSheetSetEventHandler (tạo mới from MainWindow) → `Events/ViewSheetSetEventHandler.cs`
- [ ] Namespace rename tất cả
- [ ] Replace `Debug.WriteLine` → `Logger.Info/Warning/Error` (Serilog)
- [ ] ✅ Verify: Build passes

## Phase 4: Migrate Views + Create ViewModels (Ngày 9-14)
Source: `G:\My Drive\Tool Revit Code\Export + _2025-2026\Export\Views\`
### 4a. Copy Views (giữ UI layout)
- [ ] ExportPlusMainWindow.xaml + .xaml.cs (175KB + 344KB)
- [ ] CustomFileNameDialog.xaml + .xaml.cs
- [ ] CustomFileNameInputDialog.xaml + .xaml.cs
- [ ] ExportCompletedDialog.xaml + .xaml.cs
- [ ] ProfileNameDialog.xaml + .xaml.cs
- [ ] ReorderSheetsDialog.xaml + .xaml.cs
- [ ] SaveViewSheetSetDialog.xaml + .xaml.cs
- [ ] SelectExistingSetDialog.xaml + .xaml.cs
- [ ] Namespace rename trong XAML + code-behind

### 4b. Create ViewModels (refactor từ code-behind 8,132 dòng)
- [ ] `ExportMainViewModel.cs` — orchestrator, export queue, format selection
- [ ] `SheetListViewModel.cs` — sheet loading, filtering, selection, batch check
- [ ] `ViewListViewModel.cs` — view loading, filtering, selection
- [ ] `ProfileViewModel.cs` — từ ExportPlusMainWindow.Profiles.cs (55KB)
- [ ] `ViewSheetSetViewModel.cs` — từ ExportPlusMainWindow.ViewSheetSets.cs (14KB)
- [ ] `ExportQueueViewModel.cs` — export queue management

### 4c. Wire up
- [ ] Data binding (XAML `{Binding}` → ViewModel)
- [ ] Giảm `ExportPlusMainWindow.xaml.cs` xuống ~200 dòng
- [ ] ✅ Verify: UI hiển thị đúng, basic interactions work

## Phase 5: Integration + Test (Ngày 15-17)
- [ ] DI registration trong `ExportPlusApplication.cs`
- [ ] Wire up `ricaun.Revit.UI.Tasks` cho async export operations
- [ ] Build tất cả configurations:
  - [ ] `dotnet build -c "Release R20"` (Revit 2020)
  - [ ] `dotnet build -c "Release R23"` (Revit 2023)
  - [ ] `dotnet build -c "Release R25"` (Revit 2025)
  - [ ] `dotnet build -c "Release R26"` (Revit 2026)
- [ ] `deploy-bundle.ps1` → deploy tất cả versions
- [ ] Test Revit 2023: Tab "Licorp" → Panel "Export" → Export+ button
- [ ] Test Revit 2026: Tab "Licorp" → Panel "Export" → Export+ button
- [ ] Test cùng lúc với ScheduleEditor → cả 2 tools trong tab "Licorp"
- [ ] Test PDF export → verify output
- [ ] Test DWG export → verify output
- [ ] Test Profile save/load → persistence
- [ ] Test Batch export 50+ sheets → performance
