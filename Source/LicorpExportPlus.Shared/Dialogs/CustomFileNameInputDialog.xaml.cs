using System;
using System.Windows;
using Autodesk.Revit.DB;

namespace LicorpExportPlus.Dialogs
{
    /// <summary>
    /// Dialog for entering custom file name for combined PDF
    /// </summary>
    public partial class CustomFileNameInputDialog : Window
    {
        private readonly Document _document;

        public string CustomFileName { get; private set; }

        public CustomFileNameInputDialog(string currentName, Document document)
        {
            InitializeComponent();
            
            _document = document;
            FileNameTextBox.Text = currentName;
            FileNameTextBox.SelectAll();
            FileNameTextBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            string input = FileNameTextBox.Text?.Trim() ?? "";
            
            // If empty, use default project name
            if (string.IsNullOrEmpty(input))
            {
                CustomFileName = _document.Title;
            }
            else
            {
                CustomFileName = input;
            }
            
            DialogResult = true;
            Close();
        }
    }
}

