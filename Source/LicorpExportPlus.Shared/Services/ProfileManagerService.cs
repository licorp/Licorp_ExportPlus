using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Windows;
using LicorpExportPlus.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LicorpExportPlus.Services
{
    public class ProfileManagerService
    {
        private readonly string _profilesFolder;
        private const string PROFILES_FOLDER = "ExportPlusProfiles";
        private const string DEFAULT_PROFILE = "Default";

        public ObservableCollection<Models.Profile> Profiles { get; private set; }
        public Models.Profile CurrentProfile { get; private set; }

        public event Action<Models.Profile> ProfileChanged;

        public ProfileManagerService()
        {
            _profilesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                PROFILES_FOLDER);

            Directory.CreateDirectory(_profilesFolder);
            Profiles = new ObservableCollection<Models.Profile>();
            LoadProfiles();

        }

        public void LoadProfiles()
        {
            Profiles.Clear();

            try
            {
                var profileFiles = Directory.GetFiles(_profilesFolder, "*.json");

                foreach (var file in profileFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var profile = JsonConvert.DeserializeObject<Models.Profile>(json);
                        if (profile != null)
                        {
                            Profiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }

                if (!Profiles.Any())
                {
                    CreateDefaultProfile();
                }

                CurrentProfile = Profiles.FirstOrDefault(p => p.Name == DEFAULT_PROFILE)
                ?? Profiles.FirstOrDefault();

            }
            catch (Exception ex)
            {
System.Windows.MessageBox.Show($"Error loading profiles: {ex.Message}", "Error",
MessageBoxButton.OK, MessageBoxImage.Error);
                CreateDefaultProfile();
            }
        }

        public void SaveProfile(Models.Profile profile)
        {
            try
            {
                profile.LastModified = DateTime.Now;
                var json = JsonConvert.SerializeObject(profile, Formatting.Indented);

                var filePath = Path.Combine(_profilesFolder, $"{profile.Name}.json");
                File.WriteAllText(filePath, json);


                var existing = Profiles.FirstOrDefault(p => p.Id == profile.Id);
                if (existing != null)
                {
                    var index = Profiles.IndexOf(existing);
                    Profiles[index] = profile;
                }
                else
                {
                    Profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
System.Windows.MessageBox.Show($"Error saving profile: {ex.Message}", "Error",
MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DeleteProfile(Models.Profile profile)
        {
            if (profile.Name == DEFAULT_PROFILE)
            {
System.Windows.MessageBox.Show("Cannot delete the default profile.", "Warning",
MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var filePath = Path.Combine(_profilesFolder, $"{profile.Name}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                Profiles.Remove(profile);

                if (CurrentProfile?.Id == profile.Id)
                {
                    var defaultProfile = Profiles.FirstOrDefault(p => p.Name == DEFAULT_PROFILE)
                    ?? Profiles.FirstOrDefault();
                    SwitchProfile(defaultProfile);
                }
            }
            catch (Exception ex)
            {
System.Windows.MessageBox.Show($"Error deleting profile: {ex.Message}", "Error",
MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SwitchProfile(Models.Profile profile)
        {
            if (profile != null)
            {
                CurrentProfile = profile;
                ProfileChanged?.Invoke(profile);
            }
        }

        public Models.Profile CreateNewProfile(string name)
        {
            if (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
System.Windows.MessageBox.Show($"Profile '{name}' already exists.", "Warning",
MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var newProfile = new Models.Profile
            {
                Name = name,
                Description = $"Profile created on {DateTime.Now:yyyy-MM-dd HH:mm}",
                Settings = new ProfileSettings()
            };

            SaveProfile(newProfile);
            return newProfile;
        }

        private void CreateDefaultProfile()
        {
            var defaultProfile = new Models.Profile
            {
                Name = DEFAULT_PROFILE,
                Description = "Default ExportPlus profile with standard settings",
                CreatedDate = DateTime.Now,
                Settings = new ProfileSettings
                {
                    PDFEnabled = false,
                    PDFPrinterName = "PDF24",
                    PaperPlacementCenter = true,
                    FitToPage = false,
                    ZoomPercent = 100,
                    VectorProcessing = true,
                    ColorMode = "Color",
                    RasterQuality = "High",
                    CreateSeparateFiles = true,
                    HideCropBoundaries = true,
                    HideScopeBoxes = true,
                    OutputFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "Export +"),
                    SaveAllInSameFolder = true,
                    ReportType = "Don't Save Report"
                }
            };

            SaveProfile(defaultProfile);
            CurrentProfile = defaultProfile;
        }

        private void WriteDebugLog(string message)
        {
            return;
        }
    }
}
