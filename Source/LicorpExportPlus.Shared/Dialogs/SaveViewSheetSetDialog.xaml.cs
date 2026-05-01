using System;
using System.Windows;

namespace LicorpExportPlus.Dialogs
{
    /// <summary>
    /// Dialog for saving View/Sheet Set
    /// </summary>
    public partial class SaveViewSheetSetDialog : Window
    {
        public string SetName { get; private set; }
        public int SelectedCount { get; set; }
        public bool IsSheetMode { get; set; }
        
        public SaveViewSheetSetDialog()
        {
            InitializeComponent();
            SetName = string.Empty;
            
            // Focus on TextBox when dialog opens
            Loaded += (s, e) => SetNameTextBox.Focus();
        }
        
        public SaveViewSheetSetDialog(int selectedCount, bool isSheetMode) : this()
        {
            SelectedCount = selectedCount;
            IsSheetMode = isSheetMode;
            UpdateInfoText();
        }
        
        private void UpdateInfoText()
        {
            string itemType = IsSheetMode ? "sheet" : "view";
            string itemTypePlural = IsSheetMode ? "sheets" : "views";
            
            if (SelectedCount == 0)
            {
                InfoTextBlock.Text = $"No {itemTypePlural} selected. Please select at least one {itemType}.";
                InfoTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            }
            else if (SelectedCount == 1)
            {
                InfoTextBlock.Text = $"This set will contain 1 {itemType}.";
                InfoTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                InfoTextBlock.Text = $"This set will contain {SelectedCount} {itemTypePlural}.";
                InfoTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }
        
        private void SetNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string name = SetNameTextBox.Text.Trim();
            
            // Hide error message
            ErrorTextBlock.Visibility = System.Windows.Visibility.Collapsed;
            
            // Validate name
            if (string.IsNullOrWhiteSpace(name))
            {
                SaveButton.IsEnabled = false;
                return;
            }
            
            // Check for invalid characters
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                if (name.Contains(c.ToString()))
                {
                    ErrorTextBlock.Text = $"Set name contains invalid character: {c}";
                    ErrorTextBlock.Visibility = System.Windows.Visibility.Visible;
                    SaveButton.IsEnabled = false;
                    return;
                }
            }
            
            // Check for reserved names
            if (name.Equals("All Sheets", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("All Views", StringComparison.OrdinalIgnoreCase))
            {
                ErrorTextBlock.Text = "This name is reserved. Please choose a different name.";
                ErrorTextBlock.Visibility = System.Windows.Visibility.Visible;
                SaveButton.IsEnabled = false;
                return;
            }
            
            // Valid name
            SaveButton.IsEnabled = true;
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SetName = SetNameTextBox.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(SetName))
            {
                System.Windows.MessageBox.Show("Please enter a name for the View/Sheet Set.", 
                               "Name Required", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Warning);
                SetNameTextBox.Focus();
                return;
            }
            
            if (SelectedCount == 0)
            {
                string itemType = IsSheetMode ? "sheets" : "views";
                System.Windows.MessageBox.Show($"No {itemType} selected. Please select at least one item.", 
                               "No Selection", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Warning);
                DialogResult = false;
                return;
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

