using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using LicorpExportPlus.Models;
using LicorpExportPlus.Utils;
using LicorpExportPlus.Services;
using LicorpExportPlus.Dialogs;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using Profile = LicorpExportPlus.Models.Profile;

namespace LicorpExportPlus.Views
{
    /// <summary>
    /// Profile Management functionality for ExportPlusMainWindow
    /// </summary>
    public partial class ExportPlusMainWindow
    {
        /// <summary>
        /// Initialize Profile Manager and load profiles
        /// </summary>
        private void InitializeProfiles()
        {
            try
            {
                _profileManager = new ProfileManagerService();
                
                // Wire up profile changed event
                _profileManager.ProfileChanged += OnProfileChanged;
                
                // Bind profiles to ComboBox
                ProfileComboBox.ItemsSource = _profileManager.Profiles;
                ProfileComboBox.SelectedItem = _profileManager.CurrentProfile;
                
                
                // Apply current profile to UI
                if (_profileManager.CurrentProfile != null)
                {
                    ApplyProfileToUI(_profileManager.CurrentProfile);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing profiles: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handle profile change event
        /// </summary>
        private void OnProfileChanged(Profile profile)
        {
            if (profile != null)
            {
                ApplyProfileToUI(profile);
            }
        }

        /// <summary>
        /// Apply profile settings to UI
        /// QUAN TRỌNG: Profile "Default" KHÔNG apply format selection - để user tự do chọn
        /// </summary>
        private void ApplyProfileToUI(Profile profile)
        {
            if (profile?.Settings == null) return;

            try
            {
                var settings = profile.Settings;

                // Apply Create tab settings
                if (!string.IsNullOrEmpty(settings.OutputFolder))
                {
                    OutputFolder = settings.OutputFolder;
                }

                // ✓ SPECIAL CASE: Profile "Default" KHÔNG khóa format selection
                // User có thể tự do tick chọn format mỗi lần
                bool isDefaultProfile = profile.Name.Equals("Default", StringComparison.OrdinalIgnoreCase);
                
                if (isDefaultProfile)
                {
                }

                // Apply Format settings (EXCEPT for Default profile)
                if (ExportSettings != null)
                {
                    // Chỉ apply formats nếu KHÔNG phải Default profile
                    if (!isDefaultProfile)
                    {
                        ExportSettings.IsPdfSelected = settings.PDFEnabled;
                        ExportSettings.IsDwgSelected = settings.DWGEnabled;
                        ExportSettings.IsDgnSelected = settings.DGNEnabled;
                        ExportSettings.IsIfcSelected = settings.IFCEnabled;
                        ExportSettings.IsImgSelected = settings.IMGEnabled;
                    }
                    else
                    {
                    }
                    
                    // Other settings vẫn apply cho cả Default profile
                    ExportSettings.HideCropBoundaries = settings.HideCropBoundaries;
                    ExportSettings.HideScopeBoxes = settings.HideScopeBoxes;
                    ExportSettings.CreateSeparateFolders = !settings.SaveAllInSameFolder;
                    ExportSettings.CompactDwgFiles = settings.CompactDwgFiles;
                    ExportSettings.SelectedPdfPrinter = settings.PDFPrinterName;
                    ExportSettings.PaperPlacement = settings.PaperPlacementCenter && settings.PDFIsCenter
                        ? PSPaperPlacement.Center
                        : PSPaperPlacement.OffsetFromCorner;
                    ExportSettings.PaperMargin = ParsePaperMargin(!string.IsNullOrWhiteSpace(settings.PDFMarginType) ? settings.PDFMarginType : settings.MarginType);
                    ExportSettings.OffsetX = settings.OffsetX;
                    ExportSettings.OffsetY = settings.OffsetY;
                    ExportSettings.Zoom = (settings.FitToPage || settings.PDFFitToPage) ? PSZoomType.FitToPage : PSZoomType.Zoom;
                    ExportSettings.ZoomPercentage = settings.ZoomPercent;
                    ExportSettings.HiddenLineViews = (settings.VectorProcessing && settings.PDFVectorProcessing)
                        ? PSHiddenLineViews.VectorProcessing
                        : PSHiddenLineViews.RasterProcessing;
                    ExportSettings.RasterQuality = ParseRasterQuality(!string.IsNullOrWhiteSpace(settings.PDFRasterQuality) ? settings.PDFRasterQuality : settings.RasterQuality);
                    ExportSettings.Colors = ParseColors(!string.IsNullOrWhiteSpace(settings.PDFColorMode) ? settings.PDFColorMode : settings.ColorMode);
                    ExportSettings.ViewLinksInBlue = settings.ViewLinksInBlue;
                    ExportSettings.HideRefWorkPlanes = settings.HideRefWorkPlanes;
                    ExportSettings.HideUnreferencedViewTags = settings.HideUnreferencedViewTags;
                    ExportSettings.ReplaceHalftone = settings.ReplaceHalftone;
                    ExportSettings.RegionEdgesMask = settings.RegionEdgesMask;
                    ExportSettings.KeepPaperSize = settings.KeepPaperSizeOrientation;
                    ExportSettings.CombineFiles = settings.CombineMultipleSheets;
                    ExportSettings.SkipEmptySheets = settings.SkipEmptySheets; // ✅ Restore skip empty sheets option
                    
                    // ✅ Update radio button UI state to match ExportSettings
                    if (SaveAllFilesRadio != null && SaveSplitFilesRadio != null)
                    {
                        if (ExportSettings.CreateSeparateFolders)
                        {
                            SaveSplitFilesRadio.IsChecked = true;
                        }
                        else
                        {
                            SaveAllFilesRadio.IsChecked = true;
                        }
                    }
                    
                    // ✅ NEW: Restore PDF Combine settings
                    if (CombineFilesRadio != null && SeparateFilesRadio != null)
                    {
                        if (settings.CombineMultipleSheets)
                        {
                            CombineFilesRadio.IsChecked = true;
                        }
                        else
                        {
                            SeparateFilesRadio.IsChecked = true;
                        }
                    }
                    
                    // ✅ NEW: Restore custom filename for combined PDF
                    if (!string.IsNullOrEmpty(settings.CombineCustomFileName) && ExportSettings != null)
                    {
                        ExportSettings.CombineCustomFileName = settings.CombineCustomFileName;
                    }

                    ApplyExportSettingsToPdfUi();
                    
                    // ✅ REMOVED: No longer restore CombineFileNameParameters from profile
                    // User must set custom name each export session if needed
                }
                
                // ✅ NEW: Restore selected sheets and views
                RestoreSheetViewSelection(settings);
                
                // ✅ NEW: Restore selected View/Sheet Sets
                RestoreViewSheetSetSelection(settings);
                
                // Apply custom file names from XML (if this profile was imported from XML)
                if (!string.IsNullOrEmpty(profile.XmlFilePath))
                {
                    
                    if (System.IO.File.Exists(profile.XmlFilePath))
                    {
                        try
                        {
                            var xmlProfile = XMLProfileService.LoadProfileFromXML(profile.XmlFilePath);
                            if (xmlProfile != null)
                            {
                                ApplyCustomFileNamesFromXML(xmlProfile);
                                ApplyCustomFileNamesFromXML_Views(xmlProfile);
                                
                                // Convert XML parameters to SelectedParameterInfo and save to profile
                                ConvertAndSaveXMLParametersToProfile(xmlProfile, profile);
                                
                            }
                            else
                            {
                            }
                        }
                        catch (Exception xmlEx)
                        {
                        }
                    }
                    else
                    {
                    }
                }
                else
                {
                    
                    // Check if profile has saved custom file name configuration
                    bool hasCustomConfig = !string.IsNullOrEmpty(profile.Settings?.CustomFileNameConfigJson);
                    
                    if (hasCustomConfig)
                    {
                        // Configuration will be automatically loaded when user opens Custom File Name dialog
                        // No need to prompt for XML linking
                        return;
                    }
                    
                    // Don't show dialog for "Default" profile - just use default file names
                    if (profile.Name.Equals("Default", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                    
                    // Only ask user to link XML if NO custom configuration exists (and not Default profile)
                    var result = System.Windows.MessageBox.Show(
                        $"Profile '{profile.Name}' does not have custom file name settings.\n\n" +
                        "Would you like to link an XML profile file to load custom file names?\n\n" +
                        "(This is optional - click 'No' to use default file names)",
                        "Link XML Profile?",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        // Open file dialog
                        var openFileDialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Select ExportPlus XML Profile",
                            Filter = "XML Profile Files (*.xml)|*.xml|All Files (*.*)|*.*",
                            DefaultExt = ".xml"
                        };
                        
                        if (openFileDialog.ShowDialog() == true)
                        {
                            
                            try
                            {
                                // Load and apply custom file names
                                var xmlProfile = XMLProfileService.LoadProfileFromXML(openFileDialog.FileName);
                                if (xmlProfile != null)
                                {
                                    // Save XML file path to profile
                                    profile.XmlFilePath = openFileDialog.FileName;
                                    _profileManager.SaveProfile(profile);
                                    
                                    // Apply custom file names
                                    ApplyCustomFileNamesFromXML(xmlProfile);
                                    ApplyCustomFileNamesFromXML_Views(xmlProfile);
                                    
                                    System.Windows.MessageBox.Show(
                                        $"XML profile linked successfully!\n" +
                                        $"Custom file names have been applied.",
                                        "Success",
                                        System.Windows.MessageBoxButton.OK,
                                        System.Windows.MessageBoxImage.Information);
                                }
                            }
                            catch (Exception linkEx)
                            {
                                System.Windows.MessageBox.Show(
                                    $"Failed to load XML file:\n{linkEx.Message}",
                                    "Error",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                        }
                    }
                }

            }
            catch (Exception ex)
            {
            }
        }
        
        /// <summary>
        /// Restore sheet and view selection from profile settings
        /// </summary>
        private void RestoreSheetViewSelection(ProfileSettings settings)
        {
            try
            {
                if (settings.SelectedSheetIds != null && settings.SelectedSheetIds.Any() && Sheets != null)
                {
                    var perfTimer = System.Diagnostics.Stopwatch.StartNew();
                    
                    // ⚡ PERFORMANCE: Create HashSet for O(1) lookup instead of O(n) FirstOrDefault
                    var selectedIdsSet = new HashSet<long>();
                    foreach (var idStr in settings.SelectedSheetIds)
                    {
                        if (long.TryParse(idStr, out long id))
                            selectedIdsSet.Add(id);
                    }
                    
                    // ⚡ Single pass: Set IsSelected based on HashSet lookup
                    int restoredCount = 0;
                    foreach (var sheet in Sheets)
                    {
                        bool shouldBeSelected = selectedIdsSet.Contains(sheet.Id.GetIdValue());
                        if (sheet.IsSelected != shouldBeSelected)  // Only update if changed
                        {
                            sheet.IsSelected = shouldBeSelected;
                            if (shouldBeSelected) restoredCount++;
                        }
                    }
                    
                    perfTimer.Stop();
                }
                
                if (settings.SelectedViewIds != null && settings.SelectedViewIds.Any() && Views != null)
                {
                    var perfTimer = System.Diagnostics.Stopwatch.StartNew();
                    
                    // ⚡ PERFORMANCE: Create HashSet for O(1) lookup
                    var selectedViewIdsSet = new HashSet<string>(settings.SelectedViewIds);
                    
                    // ⚡ Single pass: Set IsSelected based on HashSet lookup
                    int restoredCount = 0;
                    foreach (var view in Views)
                    {
                        bool shouldBeSelected = selectedViewIdsSet.Contains(view.ViewId);
                        if (view.IsSelected != shouldBeSelected)
                        {
                            view.IsSelected = shouldBeSelected;
                            if (shouldBeSelected) restoredCount++;
                        }
                    }
                    
                    perfTimer.Stop();
                }
            }
            catch (Exception ex)
            {
            }
        }
        
        /// <summary>
        /// Restore View/Sheet Set selection from profile settings
        /// </summary>
        private void RestoreViewSheetSetSelection(ProfileSettings settings)
        {
            try
            {
                if (settings.SelectedViewSheetSets != null && settings.SelectedViewSheetSets.Any() && _viewSheetSets != null)
                {
                    var perfTimer = System.Diagnostics.Stopwatch.StartNew();
                    
                    // ⚡ PERFORMANCE: Create HashSet for O(1) lookup
                    var selectedSetsSet = new HashSet<string>(settings.SelectedViewSheetSets);
                    
                    // ⚡ Single pass: Set IsSelected based on HashSet lookup
                    int restoredCount = 0;
                    foreach (var vsSet in _viewSheetSets)
                    {
                        bool shouldBeSelected = selectedSetsSet.Contains(vsSet.Name);
                        if (vsSet.IsSelected != shouldBeSelected)
                        {
                            vsSet.IsSelected = shouldBeSelected;
                            if (shouldBeSelected) restoredCount++;
                        }
                    }
                    
                    perfTimer.Stop();
                    
                    // Update filter if any sets are selected
                    if (restoredCount > 0 && FilterByVSCheckBox != null)
                    {
                        FilterByVSCheckBox.IsChecked = true;
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Save current UI settings to profile
        /// QUAN TRỌNG: Profile "Default" KHÔNG lưu format selection - để user tự do chọn
        /// </summary>
        private void SaveCurrentSettingsToProfile(Profile profile)
        {
            if (profile?.Settings == null) return;

            try
            {
                var settings = profile.Settings;
                bool isDefaultProfile = profile.Name.Equals("Default", StringComparison.OrdinalIgnoreCase);
                UpdateExportSettingsFromUI();

                // Save Create tab settings
                settings.OutputFolder = OutputFolder ?? "";
                settings.SaveAllInSameFolder = !(ExportSettings?.CreateSeparateFolders ?? false);

                // Save Format settings (EXCEPT for Default profile)
                if (ExportSettings != null)
                {
                    // ✓ KHÔNG save format selection cho Default profile
                    if (!isDefaultProfile)
                    {
                        settings.PDFEnabled = ExportSettings.IsPdfSelected;
                        settings.DWGEnabled = ExportSettings.IsDwgSelected;
                        settings.DGNEnabled = ExportSettings.IsDgnSelected;
                        settings.IFCEnabled = ExportSettings.IsIfcSelected;
                        settings.IMGEnabled = ExportSettings.IsImgSelected;
                    }
                    else
                    {
                        // Không thay đổi format settings - giữ nguyên default values
                    }
                    
                    // Other settings vẫn save cho cả Default profile
                    settings.HideCropBoundaries = ExportSettings.HideCropBoundaries;
                    settings.HideScopeBoxes = ExportSettings.HideScopeBoxes;
                    settings.CompactDwgFiles = ExportSettings.CompactDwgFiles;
                    settings.PDFPrinterName = ExportSettings.SelectedPdfPrinter ?? "";
                    settings.PaperPlacementCenter = ExportSettings.PaperPlacement == PSPaperPlacement.Center;
                    settings.PDFIsCenter = settings.PaperPlacementCenter;
                    settings.MarginType = ToProfileMargin(ExportSettings.PaperMargin);
                    settings.PDFMarginType = settings.MarginType;
                    settings.OffsetX = ExportSettings.OffsetX;
                    settings.OffsetY = ExportSettings.OffsetY;
                    settings.FitToPage = ExportSettings.Zoom == PSZoomType.FitToPage;
                    settings.PDFFitToPage = settings.FitToPage;
                    settings.ZoomPercent = ExportSettings.ZoomPercentage;
                    settings.VectorProcessing = ExportSettings.HiddenLineViews == PSHiddenLineViews.VectorProcessing;
                    settings.PDFVectorProcessing = settings.VectorProcessing;
                    settings.RasterQuality = ToProfileRasterQuality(ExportSettings.RasterQuality);
                    settings.PDFRasterQuality = settings.RasterQuality;
                    settings.ColorMode = ToProfileColors(ExportSettings.Colors);
                    settings.PDFColorMode = settings.ColorMode;
                    settings.ViewLinksInBlue = ExportSettings.ViewLinksInBlue;
                    settings.HideRefWorkPlanes = ExportSettings.HideRefWorkPlanes;
                    settings.HideUnreferencedViewTags = ExportSettings.HideUnreferencedViewTags;
                    settings.ReplaceHalftone = ExportSettings.ReplaceHalftone;
                    settings.RegionEdgesMask = ExportSettings.RegionEdgesMask;
                    settings.CreateSeparateFiles = !ExportSettings.CombineFiles;
                    settings.KeepPaperSizeOrientation = ExportSettings.KeepPaperSize;
                    settings.SkipEmptySheets = ExportSettings.SkipEmptySheets; // ✅ Save skip empty sheets option
                    
                    // ✅ NEW: Save PDF Combine settings
                    if (CombineFilesRadio != null && SeparateFilesRadio != null)
                    {
                        settings.CombineMultipleSheets = CombineFilesRadio.IsChecked == true;
                    }
                    
                    // ✅ NEW: Save custom filename for combined PDF
                    if (ExportSettings != null && !string.IsNullOrEmpty(ExportSettings.CombineCustomFileName))
                    {
                        settings.CombineCustomFileName = ExportSettings.CombineCustomFileName;
                    }
                    
                    // ✅ REMOVED: No longer save CombineFileNameParameters to profile
                    // This setting is temporary per export session only
                }

                // ✅ NEW: Save selected sheets and views
                settings.SelectedSheetIds = Sheets?
                    .Where(s => s.IsSelected)
                    .Select(s => s.Id.GetIdValue().ToString())
                    .ToList() ?? new System.Collections.Generic.List<string>();
                    
                settings.SelectedViewIds = Views?
                    .Where(v => v.IsSelected)
                    .Select(v => v.ViewId)
                    .ToList() ?? new System.Collections.Generic.List<string>();
                

                // ✅ NEW: Save selected View/Sheet Sets
                if (_viewSheetSets != null)
                {
                    settings.SelectedViewSheetSets = _viewSheetSets
                        .Where(vs => vs.IsSelected)
                        .Select(vs => vs.Name)
                        .ToList();
                }

                // Save old selection for backward compatibility
                settings.SelectedSheetNumbers = Sheets?
                    .Where(s => s.IsSelected)
                    .Select(s => s.SheetNumber)
                    .ToList() ?? new System.Collections.Generic.List<string>();

                _profileManager.SaveProfile(profile);
            }
            catch (Exception ex)
            {
            }
        }

        private static PSPaperMargin ParsePaperMargin(string value)
        {
            switch (value)
            {
                case "Printer Limit":
                    return PSPaperMargin.PrinterLimit;
                case "User Defined":
                    return PSPaperMargin.UserDefined;
                default:
                    return PSPaperMargin.NoMargin;
            }
        }

        private static PSRasterQuality ParseRasterQuality(string value)
        {
            switch (value)
            {
                case "Low":
                    return PSRasterQuality.Low;
                case "Medium":
                    return PSRasterQuality.Medium;
                case "Presentation":
                case "Maximum":
                    return PSRasterQuality.Maximum;
                default:
                    return PSRasterQuality.High;
            }
        }

        private static PSColors ParseColors(string value)
        {
            switch (value)
            {
                case "Black and White":
                case "BlackLine":
                    return PSColors.BlackAndWhite;
                case "Grayscale":
                case "GrayScale":
                    return PSColors.Grayscale;
                default:
                    return PSColors.Color;
            }
        }

        private static string ToProfileMargin(PSPaperMargin value)
        {
            return value == PSPaperMargin.PrinterLimit ? "Printer Limit" :
                value == PSPaperMargin.UserDefined ? "User Defined" : "No Margin";
        }

        private static string ToProfileRasterQuality(PSRasterQuality value)
        {
            return value == PSRasterQuality.Low ? "Low" :
                value == PSRasterQuality.Medium ? "Medium" :
                value == PSRasterQuality.Maximum ? "Presentation" : "High";
        }

        private static string ToProfileColors(PSColors value)
        {
            return value == PSColors.BlackAndWhite ? "Black and White" :
                value == PSColors.Grayscale ? "Grayscale" : "Color";
        }

        /// <summary>
        /// Profile ComboBox selection changed
        /// </summary>
        private void ProfileComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem is Profile selectedProfile)
            {
                _selectedProfile = selectedProfile;
                
                // Enable Apply button when different profile is selected
                if (ApplyProfileButton != null)
                {
                    ApplyProfileButton.IsEnabled = true;
                }
                
                // Don't auto-apply, wait for user to click Apply button
            }
        }

        /// <summary>
        /// Apply selected profile button clicked
        /// </summary>
        private void ApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            
            if (ProfileComboBox.SelectedItem is Profile selectedProfile)
            {
                
                try
                {
                    // Switch to selected profile (this will trigger ProfileChanged event)
                    _profileManager.SwitchProfile(selectedProfile);
                    
                    // ✅ FIX: Keep the profile selected in ComboBox after applying
                    ProfileComboBox.SelectedItem = selectedProfile;
                    
                    // Disable Apply button after applying
                    if (ApplyProfileButton != null)
                    {
                        ApplyProfileButton.IsEnabled = false;
                    }
                    
                    
                    // Show notification
                    System.Windows.MessageBox.Show(
                        $"Profile '{selectedProfile.Name}' has been applied successfully.",
                        "Profile Applied",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to apply profile: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Please select a profile first.",
                    "No Profile Selected",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Add new profile button clicked
        /// </summary>
        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ProfileNameDialog
                {
                    Owner = this
                };
                
                if (dialog.ShowDialog() == true)
                {
                    string profileName = dialog.ProfileName;
                    var mode = dialog.SelectedMode;
                    
                    Models.Profile newProfile;
                    
                    switch (mode)
                    {
                        case ProfileNameDialog.ProfileCreationMode.CopyCurrent:
                            // Create profile and copy current settings
                            newProfile = _profileManager.CreateNewProfile(profileName);
                            if (newProfile != null)
                            {
                                SaveCurrentSettingsToProfile(newProfile);
                            }
                            break;
                            
                        case ProfileNameDialog.ProfileCreationMode.UseDefault:
                            // Create profile with default settings (empty)
                            newProfile = _profileManager.CreateNewProfile(profileName);
                            if (newProfile != null)
                            {
                                _profileManager.SaveProfile(newProfile);
                            }
                            break;
                            
                        case ProfileNameDialog.ProfileCreationMode.ImportFile:
                            // Import from XML file
                            newProfile = _profileManager.CreateNewProfile(profileName);
                            if (newProfile != null)
                            {
                                try
                                {
                                    // Load settings from XML file
                                    var xmlProfile = XMLProfileService.LoadProfileFromXML(dialog.ImportFilePath);
                                    
                                    if (xmlProfile != null && xmlProfile.TemplateInfo != null && newProfile.Settings != null)
                                    {
                                        var template = xmlProfile.TemplateInfo;
                                        
                                        // ===== APPLY ALL SETTINGS FROM XML =====
                                        
                                        // Format checkboxes
                                        newProfile.Settings.PDFEnabled = template.IsPDFChecked;
                                        newProfile.Settings.DWGEnabled = template.IsDWGChecked;
                                        newProfile.Settings.DGNEnabled = template.IsDGNChecked;
                                        newProfile.Settings.IFCEnabled = template.IsIFCChecked;
                                        newProfile.Settings.IMGEnabled = template.IsIMGChecked;
                                        newProfile.Settings.CompactDwgFiles = template.DWG_MergedViews;
                                        
                                        // View options
                                        newProfile.Settings.HideCropBoundaries = template.HideCropBoundaries;
                                        newProfile.Settings.HideScopeBoxes = template.HideScopeBox;
                                        
                                        // File settings
                                        newProfile.Settings.SaveAllInSameFolder = !template.IsSeparateFile;
                                        if (!string.IsNullOrEmpty(template.FilePath))
                                        {
                                            newProfile.Settings.OutputFolder = template.FilePath;
                                        }
                                        
                                        // PDF specific settings
                                        newProfile.Settings.PDFVectorProcessing = template.IsVectorProcessing;
                                        newProfile.Settings.PDFRasterQuality = template.RasterQuality;
                                        newProfile.Settings.PDFColorMode = template.Color;
                                        newProfile.Settings.PDFFitToPage = template.IsFitToPage;
                                        newProfile.Settings.PDFIsCenter = template.IsCenter;
                                        newProfile.Settings.PDFMarginType = template.SelectedMarginType;
                                        
                                        // DWF settings
                                        if (template.DWF != null)
                                        {
                                            newProfile.Settings.DWFImageFormat = template.DWF.OptImageFormat;
                                            newProfile.Settings.DWFImageQuality = template.DWF.OptImageQuality;
                                            newProfile.Settings.DWFExportTextures = template.DWF.OptExportTextures;
                                        }
                                        
                                        // NWC settings
                                        if (template.NWC != null)
                                        {
                                            newProfile.Settings.NWCConvertElementProperties = template.NWC.ConvertElementProperties;
                                            newProfile.Settings.NWCCoordinates = template.NWC.Coordinates;
                                            newProfile.Settings.NWCDivideFileIntoLevels = template.NWC.DivideFileIntoLevels;
                                            newProfile.Settings.NWCExportElementIds = template.NWC.ExportElementIds;
                                            newProfile.Settings.NWCExportParts = template.NWC.ExportParts;
                                            newProfile.Settings.NWCFacetingFactor = template.NWC.FacetingFactor;
                                        }
                                        
                                        // IFC settings
                                        if (template.IFC != null)
                                        {
                                            newProfile.Settings.IFCFileVersion = template.IFC.FileVersion;
                                            newProfile.Settings.IFCSpaceBoundaries = template.IFC.SpaceBoundaries;
                                            newProfile.Settings.IFCSitePlacement = template.IFC.SitePlacement;
                                            newProfile.Settings.IFCExportBaseQuantities = template.IFC.ExportBaseQuantities;
                                            newProfile.Settings.IFCExportIFCCommonPropertySets = template.IFC.ExportIFCCommonPropertySets;
                                            newProfile.Settings.IFCTessellationLevelOfDetail = template.IFC.TessellationLevelOfDetail;
                                        }
                                        
                                        // IMG settings
                                        if (template.IMG != null)
                                        {
                                            newProfile.Settings.IMGImageResolution = template.IMG.ImageResolution;
                                            newProfile.Settings.IMGFileType = template.IMG.HLRandWFViewsFileType;
                                            newProfile.Settings.IMGZoomType = template.IMG.ZoomType;
                                            newProfile.Settings.IMGPixelSize = template.IMG.PixelSize;
                                        }
                                        
                                        // Save profile with all settings
                                        
                                        // Save XML file path for future re-loading
                                        newProfile.XmlFilePath = dialog.ImportFilePath;
                                        
                                        _profileManager.SaveProfile(newProfile);
                                        
                                        // Apply settings to UI immediately
                                        ApplyProfileToUI(newProfile);
                                        
                                        // Apply custom file names from XML (if available)
                                        ApplyCustomFileNamesFromXML(xmlProfile);
                                        ApplyCustomFileNamesFromXML_Views(xmlProfile);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Failed to read settings from XML file.", "Import Error",
                                                       MessageBoxButton.OK, MessageBoxImage.Warning);
                                        return;
                                    }
                                }
                                catch (Exception importEx)
                                {
                                    MessageBox.Show($"Error importing profile: {importEx.Message}", "Import Error",
                                                   MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }
                            else
                            {
                            }
                            break;
                            
                        default:
                            return;
                    }
                    
                    if (newProfile != null)
                    {
                        // Switch to the new profile
                        _profileManager.SwitchProfile(newProfile);
                        ProfileComboBox.SelectedItem = newProfile;
                        
                        MessageBox.Show($"Profile '{profileName}' created successfully!", 
                                       "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating profile: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Save profile button clicked
        /// </summary>
        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentProfile = ProfileComboBox.SelectedItem as Profile;
                if (currentProfile != null)
                {
                    SaveCurrentSettingsToProfile(currentProfile);
                    
                    MessageBox.Show($"Profile '{currentProfile.Name}' saved successfully!", 
                                   "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Please select a profile first.", "Information",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving profile: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Profile files (*.json;*.xml)|*.json;*.xml|JSON profiles (*.json)|*.json|XML profiles (*.xml)|*.xml|All files (*.*)|*.*",
                    Title = "Import Profile"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                Profile importedProfile;
                var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (extension == ".json")
                {
                    importedProfile = _profileManager.ImportProfileFromFile(dialog.FileName);
                }
                else if (extension == ".xml")
                {
                    importedProfile = ImportXmlProfile(dialog.FileName);
                }
                else
                {
                    MessageBox.Show("Please select a .json or .xml profile file.", "Unsupported Profile",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _profileManager.SwitchProfile(importedProfile);
                ProfileComboBox.ItemsSource = _profileManager.Profiles;
                ProfileComboBox.SelectedItem = importedProfile;
                ApplyProfileToUI(importedProfile);

                MessageBox.Show($"Profile '{importedProfile.Name}' imported successfully.",
                    "Profile Imported", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing profile: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentProfile = ProfileComboBox.SelectedItem as Profile;
                if (currentProfile == null)
                {
                    MessageBox.Show("Please select a profile first.", "Information",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Export+ profile (*.json)|*.json",
                    FileName = $"{currentProfile.Name}.json",
                    Title = "Export Profile"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                _profileManager.ExportProfileToFile(currentProfile, dialog.FileName);
                MessageBox.Show($"Profile exported to:\n{dialog.FileName}", "Profile Exported",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting profile: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SharedProfileFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select shared profile folder";
                    dialog.SelectedPath = _profileManager.SharedProfilesFolder;
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    {
                        return;
                    }

                    _profileManager.SetSharedProfilesFolder(dialog.SelectedPath);
                    ProfileComboBox.ItemsSource = _profileManager.Profiles;
                    ProfileComboBox.SelectedItem = _profileManager.CurrentProfile;

                    MessageBox.Show($"Shared profile folder set to:\n{dialog.SelectedPath}",
                        "Shared Profiles", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting shared profile folder: {ex.Message}", "Shared Profile Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Profile ImportXmlProfile(string filePath)
        {
            var xmlProfile = XMLProfileService.LoadProfileFromXML(filePath);
            if (xmlProfile == null || xmlProfile.TemplateInfo == null)
            {
                throw new InvalidOperationException("Could not read XML profile.");
            }

            var name = Path.GetFileNameWithoutExtension(filePath);
            var profile = _profileManager.CreateNewProfile(name);
            if (profile == null)
            {
                profile = _profileManager.CreateNewProfile($"{name} ({DateTime.Now:HHmmss})");
            }

            if (profile == null)
            {
                throw new InvalidOperationException("Could not create imported profile.");
            }

            var template = xmlProfile.TemplateInfo;
            profile.XmlFilePath = filePath;
            profile.Settings.PDFEnabled = template.IsPDFChecked;
            profile.Settings.DWGEnabled = template.IsDWGChecked;
            profile.Settings.DGNEnabled = template.IsDGNChecked;
            profile.Settings.IFCEnabled = template.IsIFCChecked;
            profile.Settings.IMGEnabled = template.IsIMGChecked;
            profile.Settings.CompactDwgFiles = template.DWG_MergedViews;
            profile.Settings.HideCropBoundaries = template.HideCropBoundaries;
            profile.Settings.HideScopeBoxes = template.HideScopeBox;
            profile.Settings.SaveAllInSameFolder = !template.IsSeparateFile;
            profile.Settings.OutputFolder = string.IsNullOrWhiteSpace(template.FilePath)
                ? profile.Settings.OutputFolder
                : template.FilePath;
            profile.Settings.PDFVectorProcessing = template.IsVectorProcessing;
            profile.Settings.PDFRasterQuality = template.RasterQuality;
            profile.Settings.PDFColorMode = template.Color;
            profile.Settings.PDFFitToPage = template.IsFitToPage;
            profile.Settings.PDFIsCenter = template.IsCenter;
            profile.Settings.PDFMarginType = template.SelectedMarginType;

            ConvertAndSaveXMLParametersToProfile(xmlProfile, profile);
            _profileManager.SaveProfile(profile);
            return profile;
        }

        /// <summary>
        /// Delete profile button clicked
        /// </summary>
        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentProfile = ProfileComboBox.SelectedItem as Profile;
                if (currentProfile != null)
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete profile '{currentProfile.Name}'?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _profileManager.DeleteProfile(currentProfile);
                        MessageBox.Show($"Profile '{currentProfile.Name}' deleted successfully!",
                                       "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Please select a profile first.", "Information",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting profile: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Options CheckBox Event Handlers

        /// <summary>
        /// Event handler for Format checkboxes (PDF/DWG/etc.) to log and update queue
        /// </summary>
        private void FormatCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox)
            {
                bool isChecked = checkBox.IsChecked == true;
                string formatName = checkBox.Name.Replace("Check", ""); // PDFCheck → PDF
                
                
                // Log current state of ALL format checkboxes
                if (ExportSettings != null)
                {
                    var selectedFormats = ExportSettings.GetSelectedFormatsList();
                }
                
                // Update Export Queue to reflect new format selection
                try
                {
                    UpdateExportQueue();
                }
                catch (Exception ex)
                {
                }
            }
        }

        /// <summary>
        /// Event handler for all Options checkboxes to log and track changes
        /// </summary>
        private void OptionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox)
            {
                bool isChecked = checkBox.IsChecked == true;
                string optionName = checkBox.Content.ToString();
                
                
                // Log current state of all options
                if (ExportSettings != null)
                {
                    // Debug logging removed
                }
            }
        }

        #endregion Options CheckBox Event Handlers
        
        #region Custom File Name from XML
        
        /// <summary>
        /// Apply custom file names from XML profile to all sheets
        /// </summary>
        private void ApplyCustomFileNamesFromXML(ExportPlusXMLProfile xmlProfile)
        {
            if (xmlProfile == null || xmlProfile.TemplateInfo == null)
            {
                return;
            }
            
            var template = xmlProfile.TemplateInfo;
            
            // Check if custom file name parameters exist in SelectSheetParameters.CombineParameters
            if (template.SelectSheetParameters?.CombineParameters == null || 
                template.SelectSheetParameters.CombineParameters.Count == 0)
            {
                return;
            }
            
            
            try
            {
                // Get all ViewSheet elements from document
                var collector = new Autodesk.Revit.DB.FilteredElementCollector(_document);
                var allSheets = collector
                    .OfClass(typeof(Autodesk.Revit.DB.ViewSheet))
                    .Cast<Autodesk.Revit.DB.ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .ToList();
                
                
                // Apply custom names to sheets in UI
                int appliedCount = 0;
                foreach (var revitSheet in allSheets)
                {
                    // Build custom file name from parameters
                    // Each parameter has its own separator in xml:space_x003D_preserve attribute
                    var customFileNameBuilder = new System.Text.StringBuilder();
                    
                    for (int i = 0; i < template.SelectSheetParameters.CombineParameters.Count; i++)
                    {
                        var param = template.SelectSheetParameters.CombineParameters[i];
                        
                        // Convert ParameterId string to int
                        int paramId = 0;
                        int.TryParse(param.ParameterId, out paramId);
                        
                        // Get parameter value from sheet
                        string value = GetSheetParameterValue(revitSheet, param.ParameterName, paramId);
                        
                        bool isLastParam = (i == template.SelectSheetParameters.CombineParameters.Count - 1);
                        
                        if (!string.IsNullOrEmpty(value))
                        {
                            // Add value to filename
                            customFileNameBuilder.Append(value);
                            
                            // Add separator ONLY if not last parameter AND value is not empty
                            if (!isLastParam)
                            {
                                string separator = param.XmlSpaceAttribute;
                                if (string.IsNullOrEmpty(separator))
                                {
                                    separator = "-"; // Default separator
                                }
                                customFileNameBuilder.Append(separator);
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                    }
                    
                    string customFileName = customFileNameBuilder.ToString();
                    
                    // Find matching sheet in Sheets collection
                    var sheet = Sheets.FirstOrDefault(s => s.Number == revitSheet.SheetNumber);
                    if (sheet != null)
                    {
                        sheet.CustomFileName = customFileName;
                        appliedCount++;
                    }
                }
                
            }
            catch (Exception ex)
            {
            }
        }
        
        /// <summary>
        /// Apply custom file names from XML profile to all views
        /// </summary>
        private void ApplyCustomFileNamesFromXML_Views(ExportPlusXMLProfile xmlProfile)
        {
            
            if (xmlProfile == null || xmlProfile.TemplateInfo == null)
            {
                return;
            }
            
            var template = xmlProfile.TemplateInfo;
            
            // Check if custom file name parameters exist in SelectViewParameters.CombineParameters
            if (template.SelectViewParameters?.CombineParameters == null || 
                template.SelectViewParameters.CombineParameters.Count == 0)
            {
                if (template.SelectViewParameters != null)
                {
                }
                return;
            }
            
            
            // Check if Views collection is loaded - if not, load it first
            if (Views == null || Views.Count == 0)
            {
                LoadViews(); // Force load Views collection
                
                if (Views == null || Views.Count == 0)
                {
                    return;
                }
            }
            else
            {
            }
            
            try
            {
                // Get all View elements from document (3D views, sections, elevations, etc.)
                var collector = new Autodesk.Revit.DB.FilteredElementCollector(_document);
                var allViews = collector
                    .OfClass(typeof(Autodesk.Revit.DB.View))
                    .Cast<Autodesk.Revit.DB.View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted) // Only non-template, printable views
                    .ToList();
                
                
                // Apply custom names to views in UI
                int appliedCount = 0;
                int matchedCount = 0;
                
                foreach (var revitView in allViews)
                {
                    // Build custom file name from parameters
                    var customFileNameBuilder = new System.Text.StringBuilder();
                    
                    
                    for (int i = 0; i < template.SelectViewParameters.CombineParameters.Count; i++)
                    {
                        var param = template.SelectViewParameters.CombineParameters[i];
                        
                        // Convert ParameterId string to int
                        int paramId = 0;
                        int.TryParse(param.ParameterId, out paramId);
                        
                        // Get parameter value from view
                        string value = GetViewParameterValue(revitView, param.ParameterName, paramId);
                        
                        bool isLastParam = (i == template.SelectViewParameters.CombineParameters.Count - 1);
                        
                        if (!string.IsNullOrEmpty(value))
                        {
                            // Add value to filename
                            customFileNameBuilder.Append(value);
                            
                            // Add separator ONLY if not last parameter AND value is not empty
                            if (!isLastParam)
                            {
                                string separator = param.XmlSpaceAttribute;
                                if (string.IsNullOrEmpty(separator))
                                {
                                    separator = "_"; // Default separator
                                }
                                customFileNameBuilder.Append(separator);
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                    }
                    
                    string customFileName = customFileNameBuilder.ToString();
                    
                    // Find matching view in Views collection
                    var viewItem = Views?.FirstOrDefault(v => v.ViewName == revitView.Name);
                    if (viewItem != null)
                    {
                        matchedCount++;
                        viewItem.CustomFileName = customFileName;
                        appliedCount++;
                    }
                    else
                    {
                    }
                }
                
            }
            catch (Exception ex)
            {
            }
        }
        
        /// <summary>
        /// Get parameter value from sheet by name or ID
        /// </summary>
        private string GetSheetParameterValue(Autodesk.Revit.DB.ViewSheet sheet, string parameterName, int parameterId)
        {
            try
            {
                // Try by parameter ID first (more reliable)
                var param = sheet.get_Parameter((Autodesk.Revit.DB.BuiltInParameter)parameterId);
                if (param != null && param.HasValue)
                {
                    return param.AsString() ?? param.AsValueString() ?? "";
                }
                
                // Try by parameter name as fallback
                param = sheet.LookupParameter(parameterName);
                if (param != null && param.HasValue)
                {
                    return param.AsString() ?? param.AsValueString() ?? "";
                }
                
                // Try from ProjectInfo for project-level parameters (Project Name, Project Number, etc.)
                var projectInfo = _document.ProjectInformation;
                if (projectInfo != null)
                {
                    // Try by parameter ID
                    if (parameterId != 0)
                    {
                        param = projectInfo.get_Parameter((Autodesk.Revit.DB.BuiltInParameter)parameterId);
                        if (param != null && param.HasValue)
                        {
                            return param.AsString() ?? param.AsValueString() ?? "";
                        }
                    }
                    
                    // Try by parameter name
                    param = projectInfo.LookupParameter(parameterName);
                    if (param != null && param.HasValue)
                    {
                        return param.AsString() ?? param.AsValueString() ?? "";
                    }
                }
                
                return "";
            }
            catch
            {
                return "";
            }
        }
        
        /// <summary>
        /// Get parameter value from view by name or ID
        /// </summary>
        private string GetViewParameterValue(Autodesk.Revit.DB.View view, string parameterName, int parameterId)
        {
            try
            {
                // Try by parameter ID first (more reliable)
                var param = view.get_Parameter((Autodesk.Revit.DB.BuiltInParameter)parameterId);
                if (param != null && param.HasValue)
                {
                    return param.AsString() ?? param.AsValueString() ?? "";
                }
                
                // Try by parameter name as fallback
                param = view.LookupParameter(parameterName);
                if (param != null && param.HasValue)
                {
                    return param.AsString() ?? param.AsValueString() ?? "";
                }
                
                // Try from ProjectInfo for project-level parameters (Project Name, Project Number, etc.)
                var projectInfo = _document.ProjectInformation;
                if (projectInfo != null)
                {
                    // Try by parameter ID
                    if (parameterId != 0)
                    {
                        param = projectInfo.get_Parameter((Autodesk.Revit.DB.BuiltInParameter)parameterId);
                        if (param != null && param.HasValue)
                        {
                            return param.AsString() ?? param.AsValueString() ?? "";
                        }
                    }
                    
                    // Try by parameter name
                    param = projectInfo.LookupParameter(parameterName);
                    if (param != null && param.HasValue)
                    {
                        return param.AsString() ?? param.AsValueString() ?? "";
                    }
                }
                
                return "";
            }
            catch
            {
                return "";
            }
        }
        
        /// <summary>
        /// Convert XML profile parameters to SelectedParameterInfo and save to profile settings
        /// This allows the CustomFileNameDialog to load the correct parameter order
        /// </summary>
        private void ConvertAndSaveXMLParametersToProfile(ExportPlusXMLProfile xmlProfile, Profile profile)
        {
            try
            {
                
                // Check if we have Sheet or View parameters
                bool hasSheetParams = xmlProfile?.TemplateInfo?.SelectSheetParameters?.CombineParameters != null 
                                      && xmlProfile.TemplateInfo.SelectSheetParameters.CombineParameters.Count > 0;
                bool hasViewParams = xmlProfile?.TemplateInfo?.SelectViewParameters?.CombineParameters != null 
                                     && xmlProfile.TemplateInfo.SelectViewParameters.CombineParameters.Count > 0;
                
                if (!hasSheetParams && !hasViewParams)
                {
                    return;
                }
                
                if (profile?.Settings == null)
                {
                    return;
                }
                
                // Convert Sheet parameters if available
                if (hasSheetParams)
                {
                    var sheetParams = new System.Collections.Generic.List<Models.SelectedParameterInfo>();
                    var sourceParams = xmlProfile.TemplateInfo.SelectSheetParameters.CombineParameters;
                    
                    
                    foreach (var xmlParam in sourceParams)
                    {
                        var selectedParam = new Models.SelectedParameterInfo
                        {
                            ParameterName = xmlParam.ParameterName,
                            Prefix = xmlParam.Prefix ?? "",
                            Suffix = xmlParam.Suffix ?? "",
                            Separator = xmlParam.XmlSpaceAttribute ?? "_", // Default separator
                            SampleValue = "" // Will be filled by dialog preview
                        };
                        
                        sheetParams.Add(selectedParam);
                    }
                    
                    // Serialize and save Sheet config
                    var sheetConfigJson = Newtonsoft.Json.JsonConvert.SerializeObject(sheetParams);
                    profile.Settings.CustomFileNameConfigJson_Sheets = sheetConfigJson;
                    
                }
                
                // Convert View parameters if available
                if (hasViewParams)
                {
                    var viewParams = new System.Collections.Generic.List<Models.SelectedParameterInfo>();
                    var sourceParams = xmlProfile.TemplateInfo.SelectViewParameters.CombineParameters;
                    
                    
                    foreach (var xmlParam in sourceParams)
                    {
                        var selectedParam = new Models.SelectedParameterInfo
                        {
                            ParameterName = xmlParam.ParameterName,
                            Prefix = xmlParam.Prefix ?? "",
                            Suffix = xmlParam.Suffix ?? "",
                            Separator = xmlParam.XmlSpaceAttribute ?? "_", // Default separator
                            SampleValue = "" // Will be filled by dialog preview
                        };
                        
                        viewParams.Add(selectedParam);
                    }
                    
                    // Serialize and save View config
                    var viewConfigJson = Newtonsoft.Json.JsonConvert.SerializeObject(viewParams);
                    profile.Settings.CustomFileNameConfigJson_Views = viewConfigJson;
                    
                }
                
                // Save profile to disk
                _profileManager.SaveProfile(profile);
            }
            catch (Exception ex)
            {
            }
        }
        
        #endregion Custom File Name from XML
    }
}
