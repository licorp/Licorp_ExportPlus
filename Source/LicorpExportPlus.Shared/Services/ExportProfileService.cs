using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using LicorpExportPlus.Models;
using Autodesk.Revit.DB;

namespace LicorpExportPlus.Services
{
    public class ExportProfileService
    {
        private readonly string _ExportPlusProfileFolder;
        private readonly string _exportPlusProfileFolder;

        public ObservableCollection<ExportPlusProfile> Profiles { get; set; }

        public ExportProfileService()
        {

            _ExportPlusProfileFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DiRoots", "ExportPlus", "Profiles"
            );

            _exportPlusProfileFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DiRoots", "ExportPlus", "Profiles"
            );

            Profiles = new ObservableCollection<ExportPlusProfile>();


            EnsureDirectoriesExist();
            LoadProfiles();
        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(_exportPlusProfileFolder))
                {
                    Directory.CreateDirectory(_exportPlusProfileFolder);
                }
            }
            catch (Exception)
            {
            }
        }

        public void LoadProfiles()
        {
            Profiles.Clear();

            LoadExportPlusProfiles();

            LoadExistingExportPlusProfiles();

            if (Profiles.Count == 0)
            {
                var defaultProfile = CreateDefaultProfile();
                Profiles.Add(defaultProfile);
            }

        }

        public void LoadExportPlusProfile(string jsonFilePath)
        {
            try
            {
                if (File.Exists(jsonFilePath))
                {

                    string extension = Path.GetExtension(jsonFilePath).ToLower();
                    ExportPlusProfile profile = null;

                    if (extension == ".xml")
                    {
                        var xmlProfile = XMLProfileService.LoadProfileFromXML(jsonFilePath);
                        if (xmlProfile != null)
                        {
                            profile = XMLProfileService.ConvertXMLToProfile(xmlProfile);
                        }
                    }
                    else if (extension == ".json")
                    {
                        string json = File.ReadAllText(jsonFilePath);
                        profile = JsonConvert.DeserializeObject<ExportPlusProfile>(json);
                    }

                    if (profile != null)
                    {
                        if (string.IsNullOrEmpty(profile.ProfileName))
                        {
                            profile.ProfileName = Path.GetFileNameWithoutExtension(jsonFilePath);
                        }

                        var existingProfile = Profiles.FirstOrDefault(p => p.ProfileName == profile.ProfileName);
                        if (existingProfile != null)
                        {
                            Profiles.Remove(existingProfile);
                        }

                        Profiles.Add(profile);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public List<SheetFileNameInfo> LoadXMLProfileWithSheets(string xmlFilePath, List<ViewSheet> sheets)
        {
            try
            {
                var xmlProfile = XMLProfileService.LoadProfileFromXML(xmlFilePath);
                if (xmlProfile != null)
                {
                    return XMLProfileService.GenerateCustomFileNames(xmlProfile, sheets);
                }
                return new List<SheetFileNameInfo>();
            }
            catch (Exception)
            {
                return new List<SheetFileNameInfo>();
            }
        }

        public List<string> GetAvailableXMLProfiles()
        {
            return XMLProfileService.GetAvailableXMLProfiles();
        }

        private void LoadExportPlusProfiles()
        {
            try
            {
                if (Directory.Exists(_ExportPlusProfileFolder))
                {
                    var jsonFiles = Directory.GetFiles(_ExportPlusProfileFolder, "*.json");

                    foreach (var file in jsonFiles)
                    {
                        try
                        {
                            string json = File.ReadAllText(file);
                            var profile = JsonConvert.DeserializeObject<ExportPlusProfile>(json);

                            if (profile != null)
                            {
                                if (string.IsNullOrEmpty(profile.ProfileName))
                                {
                                    profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                                }

                                Profiles.Add(profile);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                else
                {
                }
            }
            catch (Exception)
            {
            }
        }

        private void LoadExistingExportPlusProfiles()
        {
            try
            {
                if (Directory.Exists(_exportPlusProfileFolder))
                {
                    var jsonFiles = Directory.GetFiles(_exportPlusProfileFolder, "*.json");

                    foreach (var file in jsonFiles)
                    {
                        try
                        {
                            string json = File.ReadAllText(file);
                            var profile = JsonConvert.DeserializeObject<ExportPlusProfile>(json);

                            if (profile != null)
                            {
                                if (string.IsNullOrEmpty(profile.ProfileName))
                                {
                                    profile.ProfileName = Path.GetFileNameWithoutExtension(file);
                                }

                                Profiles.Add(profile);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public void SaveProfile(ExportPlusProfile profile)
        {
            try
            {
                if (profile == null || string.IsNullOrEmpty(profile.ProfileName))
                {
                    return;
                }

                string fileName = $"{profile.ProfileName}.json";
                string filePath = Path.Combine(_exportPlusProfileFolder, fileName);

                string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                File.WriteAllText(filePath, json);


                if (!Profiles.Any(p => p.ProfileName == profile.ProfileName))
                {
                    Profiles.Add(profile);
                }
            }
            catch (Exception)
            {
            }
        }

        public void DeleteProfile(ExportPlusProfile profile)
        {
            try
            {
                if (profile == null || string.IsNullOrEmpty(profile.ProfileName))
                {
                    return;
                }

                string fileName = $"{profile.ProfileName}.json";
                string filePath = Path.Combine(_exportPlusProfileFolder, fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Profiles.Remove(profile);
                }
                else
                {
                }
            }
            catch (Exception)
            {
            }
        }

        private ExportPlusProfile CreateDefaultProfile()
        {
            return new ExportPlusProfile
            {
                ProfileName = "Default ExportPlus",
                OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                SelectedFormats = new List<string> { "DWG" },
                CreateSeparateFolders = false,
                PaperSize = "Auto",
                Orientation = "Auto",
                PlaceCenterDrawing = true,
                ZoomTo100 = false,
                HideCropRegions = true,
                HideScopeboxes = true
            };
        }

        public ExportPlusProfile GetProfile(string profileName)
        {
            return Profiles.FirstOrDefault(p => p.ProfileName == profileName);
        }

        public ExportPlusProfile CreateProfileFromSettings(ExportSettings settings, string profileName)
        {
            var profile = new ExportPlusProfile
            {
                ProfileName = profileName,
                OutputFolder = settings?.OutputFolder ?? "",
                SelectedFormats = settings?.GetSelectedFormatsList() ?? new List<string>(),
                CreateSeparateFolders = settings?.CreateSeparateFolders ?? false,
                HideCropRegions = settings?.HideCropBoundaries ?? true,
                HideScopeboxes = settings?.HideCropBoundaries ?? true
            };

            return profile;
        }

        public ExportPlusProfile LoadProfileFromFile(string jsonFilePath)
        {
            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(jsonFilePath);
                var profile = JsonConvert.DeserializeObject<ExportPlusProfile>(json);

                if (profile != null)
                {
                    return profile;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void ExportProfileToFile(ExportPlusProfile profile, string jsonFilePath)
        {
            try
            {
                if (profile == null)
                {
                    return;
                }

                string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                File.WriteAllText(jsonFilePath, json);

            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
