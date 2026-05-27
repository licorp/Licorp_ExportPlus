using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services.Interfaces;
using Newtonsoft.Json;

namespace LicorpExportPlus.Services;

public class ProfileServiceAdapter : IProfileService
{
    private readonly string _profilesFolder;
    private const string PROFILES_FOLDER = "ExportPlusProfiles";

    public ProfileServiceAdapter()
    {
        _profilesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            PROFILES_FOLDER);
        Directory.CreateDirectory(_profilesFolder);
    }

    public List<Profile> LoadAllProfiles()
    {
        var profiles = new List<Profile>();
        try
        {
            var profileFiles = Directory.GetFiles(_profilesFolder, "*.json")
                .Where(f => !Path.GetFileName(f).Equals("settings.json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in profileFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonConvert.DeserializeObject<Profile>(json);
                    if (profile != null)
                    {
                        profiles.Add(profile);
                    }
                }
                catch (Exception ex)
                {
                    LicorpTrace.Warn($"[ProfileService] Failed to load profile {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[ProfileService] Failed to load profiles: {ex.Message}", ex);
        }
        return profiles;
    }

    public Profile LoadProfile(string profileName)
    {
        try
        {
            var filePath = Path.Combine(_profilesFolder, $"{profileName}.json");
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<Profile>(json);
            }
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[ProfileService] Failed to load profile '{profileName}': {ex.Message}", ex);
        }
        return null;
    }

    public void SaveProfile(Profile profile)
    {
        try
        {
            profile.LastModified = DateTime.Now;
            var filePath = Path.Combine(_profilesFolder, $"{profile.Name}.json");
            var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(filePath, json);
            LicorpTrace.Info($"[ProfileService] Profile '{profile.Name}' saved");
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[ProfileService] Failed to save profile '{profile.Name}': {ex.Message}", ex);
            throw;
        }
    }

    public void DeleteProfile(string profileName)
    {
        try
        {
            var filePath = Path.Combine(_profilesFolder, $"{profileName}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                LicorpTrace.Info($"[ProfileService] Profile '{profileName}' deleted");
            }
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[ProfileService] Failed to delete profile '{profileName}': {ex.Message}", ex);
            throw;
        }
    }

    public Profile CreateDefaultProfile()
    {
        var profile = new Profile
        {
            Name = "Default",
            Description = "Default export profile",
            CreatedDate = DateTime.Now,
            LastModified = DateTime.Now,
            Settings = new ProfileSettings()
        };
        SaveProfile(profile);
        return profile;
    }

    public void ExportProfile(Profile profile, string filePath)
    {
        try
        {
            var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(filePath, json);
            LicorpTrace.Info($"[ProfileService] Profile '{profile.Name}' exported to {filePath}");
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[ProfileService] Failed to export profile: {ex.Message}", ex);
            throw;
        }
    }

    public Profile ImportProfile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var profile = JsonConvert.DeserializeObject<Profile>(json);
            if (profile != null)
            {
                SaveProfile(profile);
                LicorpTrace.Info($"[ProfileService] Profile '{profile.Name}' imported from {filePath}");
            }
            return profile;
        }
        catch (Exception ex)
        {
            LicorpTrace.Error($"[ProfileService] Failed to import profile: {ex.Message}", ex);
            throw;
        }
    }
}
