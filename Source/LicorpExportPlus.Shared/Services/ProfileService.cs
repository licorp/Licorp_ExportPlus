using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using LicorpExportPlus.Models;
using Licorp.Diagnostics;
using Newtonsoft.Json;

namespace LicorpExportPlus.Services
{
    public class ProfileService
    {
        private readonly string _profilesPath;
        private readonly string _prosheeetsProfilesPath;
        private ObservableCollection<ExportPlusProfile> _profiles;

        public ObservableCollection<ExportPlusProfile> Profiles => _profiles ?? new ObservableCollection<ExportPlusProfile>();

        public ProfileService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var ExportPlusFolder = Path.Combine(appDataPath, "ExportPlusAddin");
            var diRootsFolder = Path.Combine(appDataPath, "DiRoots", "ExportPlus");

            if (!Directory.Exists(ExportPlusFolder))
                Directory.CreateDirectory(ExportPlusFolder);

            _profilesPath = Path.Combine(ExportPlusFolder, "profiles.json");
            _prosheeetsProfilesPath = diRootsFolder;
            LoadProfiles();
        }

        public List<ExportPlusProfile> GetProfiles()
        {
            return _profiles?.ToList() ?? new List<ExportPlusProfile>();
        }

        public void SaveProfile(ExportPlusProfile profile)
        {
            if (_profiles == null)
                _profiles = new ObservableCollection<ExportPlusProfile>();

            var existingProfile = _profiles.FirstOrDefault(p => p.ProfileName == profile.ProfileName);
            if (existingProfile != null)
            {
                _profiles.Remove(existingProfile);
            }

            _profiles.Add(profile);
            SaveProfiles();
        }

        public void SaveProfile(string profileName, ExportPlusProfile profile)
        {
            profile.ProfileName = profileName;
            SaveProfile(profile);
        }

        public void DeleteProfile(string profileName)
        {
            if (_profiles == null) return;

            var profile = _profiles.FirstOrDefault(p => p.ProfileName == profileName);
            if (profile != null)
            {
                _profiles.Remove(profile);
                SaveProfiles();
            }
        }

        public ExportPlusProfile GetProfile(string name)
        {
            return _profiles?.FirstOrDefault(p => p.ProfileName == name);
        }

        public void LoadExportPlusProfile(string jsonFilePath)
        {
            try
            {
                if (File.Exists(jsonFilePath))
                {
                    var json = File.ReadAllText(jsonFilePath);
                    var profile = JsonConvert.DeserializeObject<ExportPlusProfile>(json);

                    if (profile != null && !string.IsNullOrEmpty(profile.ProfileName))
                    {
                        SaveProfile(profile);
                    }
                }
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"Error loading ExportPlus profile: {ex.Message}");
            }
        }

        private void LoadProfiles()
        {
            try
            {
                if (File.Exists(_profilesPath))
                {
                    var json = File.ReadAllText(_profilesPath);
                    var profilesList = JsonConvert.DeserializeObject<List<ExportPlusProfile>>(json);
                    _profiles = new ObservableCollection<ExportPlusProfile>(profilesList ?? new List<ExportPlusProfile>());
                }
                else
                {
                    _profiles = CreateDefaultProfiles();
                }

                LoadExistingExportPlusProfiles();
            }
            catch (Exception ex)
            {
                _profiles = CreateDefaultProfiles();
                Licorp.Diagnostics.LicorpTrace.Dbg($"Error loading profiles: {ex.Message}");
            }
        }

        private void LoadExistingExportPlusProfiles()
        {
            try
            {
                if (Directory.Exists(_prosheeetsProfilesPath))
                {
                    var jsonFiles = Directory.GetFiles(_prosheeetsProfilesPath, "*.json");
                    foreach (var jsonFile in jsonFiles)
                    {
                        LoadExportPlusProfile(jsonFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"Error loading existing ExportPlus profiles: {ex.Message}");
            }
        }

        private void SaveProfiles()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_profiles?.ToList(), Formatting.Indented);
                File.WriteAllText(_profilesPath, json);
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"Error saving profiles: {ex.Message}");
            }
        }

        private ObservableCollection<ExportPlusProfile> CreateDefaultProfiles()
        {
            return new ObservableCollection<ExportPlusProfile>
            {
                new ExportPlusProfile
                {
                    ProfileName = "Mặc định DWG",
                    OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    SelectedFormats = new List<string> { "DWG" },
                    CreateSeparateFolders = false,
                    PaperSize = "A3",
                    Orientation = "Landscape"
                },
                new ExportPlusProfile
                {
                    ProfileName = "Xuất đầy đủ",
                    OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    SelectedFormats = new List<string> { "DWG", "JPG" },
                    CreateSeparateFolders = true,
                    PaperSize = "A3",
                    Orientation = "Landscape"
                }
            };
        }
    }
}
