using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace LicorpExportPlus.Dialogs
{
    /// <summary>
    /// Dialog for creating new profile with multiple options
    /// </summary>
    public partial class ProfileNameDialog : Window
    {
        public string ProfileName { get; private set; }
        public string ImportFilePath { get; private set; }
        
        public enum ProfileCreationMode 
        { 
            CopyCurrent, 
            UseDefault, 
            ImportFile 
        }
        
        public ProfileCreationMode SelectedMode { get; private set; }

        public ProfileNameDialog()
        {
            InitializeComponent();
            ProfileNameTextBox.Focus();
            
            // Wire up validation events
            ProfileNameTextBox.TextChanged += (s, e) => 
            {
                ValidateInputs();
            };
            CopyCurrentRadio.Checked += (s, e) => 
            {
                ValidateInputs();
            };
            UseDefaultRadio.Checked += (s, e) => 
            {
                ValidateInputs();
            };
            ImportFileRadio.Checked += (s, e) => 
            {
                ValidateInputs();
            };
        }

        private void BrowseProfileFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "ExportPlus Profile (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Import Profile File"
            };
            
            if (dlg.ShowDialog() == true)
            {
                ImportFilePath = dlg.FileName;
                
                // Auto-fill profile name from file name if textbox is empty
                if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(ImportFilePath);
                    ProfileNameTextBox.Text = fileNameWithoutExtension;
                }
                
                ImportedFilePathTextBlock.Text = $"📄 {Path.GetFileName(ImportFilePath)}";
                ImportFileRadio.IsChecked = true;
                ValidateInputs();
            }
            else
            {
            }
        }

        private void ValidateInputs()
        {
            bool hasName = !string.IsNullOrWhiteSpace(ProfileNameTextBox.Text);
            
            
            // Enable Create button if name is entered (we'll validate file path on Create click)
            CreateButton.IsEnabled = hasName;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            ProfileName = ProfileNameTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                System.Windows.MessageBox.Show("Please enter a profile name.", "Validation Error", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfileNameTextBox.Focus();
                return;
            }

            // Validate profile name (no special characters)
            if (ProfileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                System.Windows.MessageBox.Show("Profile name contains invalid characters.", "Validation Error", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                ProfileNameTextBox.Focus();
                return;
            }

            // Determine selected mode
            if (CopyCurrentRadio.IsChecked == true)
            {
                SelectedMode = ProfileCreationMode.CopyCurrent;
            }
            else if (UseDefaultRadio.IsChecked == true)
            {
                SelectedMode = ProfileCreationMode.UseDefault;
            }
            else if (ImportFileRadio.IsChecked == true)
            {
                SelectedMode = ProfileCreationMode.ImportFile;
                
                // Validate that a file was selected for import mode
                if (string.IsNullOrEmpty(ImportFilePath))
                {
                    System.Windows.MessageBox.Show("Please select a file to import by clicking the '...' button.", 
                                   "File Required", 
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

