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
        private readonly string _settingsFile;
        private const string PROFILES_FOLDER = "ExportPlusProfiles";
        private const string DEFAULT_PROFILE = "Default";

        public ObservableCollection<Models.Profile> Profiles { get; private set; }
        public Models.Profile CurrentProfile { get; private set; }
        public string SharedProfilesFolder { get; private set; }

        public event Action<Models.Profile> ProfileChanged;

        public ProfileManagerService()
        {
            _profilesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                PROFILES_FOLDER);

            Directory.CreateDirectory(_profilesFolder);
            _settingsFile = Path.Combine(_profilesFolder, "settings.json");
            SharedProfilesFolder = LoadSharedProfilesFolder();
            Profiles = new ObservableCollection<Models.Profile>();
            LoadProfiles();

        }

        public void LoadProfiles()
        {
            Profiles.Clear();

            try
            {
                var profileFiles = Directory.GetFiles(_profilesFolder, "*.json")
                    .Where(file => !string.Equals(Path.GetFileName(file), "settings.json", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var file in profileFiles)
                {
                    AddProfileFromFile(file, isShared: false);
                }

                if (!string.IsNullOrWhiteSpace(SharedProfilesFolder) && Directory.Exists(SharedProfilesFolder))
                {
                    foreach (var file in Directory.GetFiles(SharedProfilesFolder, "*.json"))
                    {
                        AddProfileFromFile(file, isShared: true);
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

        public Models.Profile ImportProfileFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Profile file not found.", filePath);
            }

            var json = File.ReadAllText(filePath);
            var profile = JsonConvert.DeserializeObject<Models.Profile>(json);
            if (profile == null)
            {
                throw new InvalidOperationException("Could not read profile JSON.");
            }

            profile.Id = Guid.NewGuid().ToString();
            profile.Name = GetUniqueProfileName(string.IsNullOrWhiteSpace(profile.Name)
                ? Path.GetFileNameWithoutExtension(filePath)
                : profile.Name);
            profile.Description = string.IsNullOrWhiteSpace(profile.Description)
                ? $"Imported from {Path.GetFileName(filePath)}"
                : profile.Description;

            SaveProfile(profile);
            return profile;
        }

        public void ExportProfileToFile(Models.Profile profile, string filePath)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Export file path is required.", nameof(filePath));
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public void SetSharedProfilesFolder(string folderPath)
        {
            SharedProfilesFolder = folderPath ?? string.Empty;
            var json = JsonConvert.SerializeObject(new ProfileManagerSettings { SharedProfilesFolder = SharedProfilesFolder }, Formatting.Indented);
            File.WriteAllText(_settingsFile, json);
            LoadProfiles();
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

        private void AddProfileFromFile(string file, bool isShared)
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonConvert.DeserializeObject<Models.Profile>(json);
                if (profile == null)
                {
                    return;
                }

                if (isShared && Profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                Profiles.Add(profile);
            }
            catch
            {
            }
        }

        private string GetUniqueProfileName(string baseName)
        {
            var candidate = baseName;
            var index = 2;
            while (Profiles.Any(p => p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{baseName} ({index})";
                index++;
            }

            return candidate;
        }

        private string LoadSharedProfilesFolder()
        {
            try
            {
                if (!File.Exists(_settingsFile))
                {
                    return string.Empty;
                }

                var settings = JsonConvert.DeserializeObject<ProfileManagerSettings>(File.ReadAllText(_settingsFile));
                return settings?.SharedProfilesFolder ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private class ProfileManagerSettings
        {
            public string SharedProfilesFolder { get; set; } = string.Empty;
        }

        private void WriteDebugLog(string message)
        {
            return;
        }
    }
}
