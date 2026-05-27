using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services.Interfaces;

namespace LicorpExportPlus.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IProfileService _profileService;
    private readonly INotificationService _notificationService;

    [ObservableProperty] public partial ObservableCollection<Profile> Profiles { get; set; } = [];
    [ObservableProperty] public partial Profile SelectedProfile { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }

    public ProfileViewModel(IProfileService profileService, INotificationService notificationService)
    {
        _profileService = profileService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    private void LoadProfiles()
    {
        IsLoading = true;
        try
        {
            var profiles = _profileService.LoadAllProfiles();
            Profiles = new ObservableCollection<Profile>(profiles);
            LicorpTrace.Info($"[ProfileViewModel] Loaded {Profiles.Count} profiles");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to load profiles", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SaveProfile(string profileName)
    {
        if (SelectedProfile == null) return;
        try
        {
            SelectedProfile.Name = profileName ?? SelectedProfile.Name;
            _profileService.SaveProfile(SelectedProfile);
            _notificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' saved");
            LoadProfiles();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to save profile", ex);
        }
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;
        try
        {
            _profileService.DeleteProfile(SelectedProfile.Name);
            _notificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' deleted");
            LoadProfiles();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to delete profile", ex);
        }
    }

    [RelayCommand]
    private void CreateDefault()
    {
        try
        {
            var profile = _profileService.CreateDefaultProfile();
            Profiles.Insert(0, profile);
            SelectedProfile = profile;
            _notificationService.ShowSuccess("Default profile created");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to create default profile", ex);
        }
    }

    [RelayCommand]
    private void ExportProfile()
    {
        if (SelectedProfile == null) return;
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|XML files (*.xml)|*.xml",
                FileName = $"{SelectedProfile.Name}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                _profileService.ExportProfile(SelectedProfile, dialog.FileName);
                _notificationService.ShowSuccess($"Profile exported to {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to export profile", ex);
        }
    }

    [RelayCommand]
    private void ImportProfile()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Profile files (*.json;*.xml)|*.json;*.xml|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var profile = _profileService.ImportProfile(dialog.FileName);
                if (profile != null)
                {
                    Profiles.Insert(0, profile);
                    SelectedProfile = profile;
                    _notificationService.ShowSuccess($"Profile '{profile.Name}' imported");
                }
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Failed to import profile", ex);
        }
    }

    [RelayCommand]
    private void ApplyProfile()
    {
        if (SelectedProfile == null) return;
        _notificationService.ShowInfo($"Profile '{SelectedProfile.Name}' applied");
    }
}
