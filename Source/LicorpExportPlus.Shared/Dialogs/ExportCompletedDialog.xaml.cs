using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LicorpExportPlus.Dialogs
{
    public partial class ExportCompletedDialog : Window
    {
        private string _folderPath;

        public ExportCompletedDialog(string folderPath)
        {
            InitializeComponent();
            _folderPath = folderPath;
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_folderPath))
                {
                    System.Windows.MessageBox.Show("Folder path is not available.", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if folder exists
                if (!Directory.Exists(_folderPath))
                {
                    System.Windows.MessageBox.Show($"Folder does not exist:\n{_folderPath}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Open folder in Windows Explorer
                Process.Start("explorer.exe", _folderPath);
                
                // Close the dialog
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening folder:\n{ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}

