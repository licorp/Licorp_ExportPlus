using System;
using System.Collections.Generic;

namespace LicorpExportPlus.Models
{
    public class XMLExportSettings
    {
        public string OutputFolder { get; set; }
        public string FileName { get; set; } = "SheetData";
        public bool IncludeProjectInfo { get; set; } = true;
        public bool IncludeSheetParameters { get; set; } = true;
        public bool IncludeRevisions { get; set; } = true;
        public bool IncludeDateTime { get; set; } = true;
        public List<string> ParameterNames { get; set; } = new List<string>();
        public List<CustomField> CustomFields { get; set; } = new List<CustomField>();
        public bool ExportAllParameters { get; set; } = false;
        public bool GroupBySheetType { get; set; } = false;
        public string RootElementName { get; set; } = "ProjectSheets";
        public string SheetElementName { get; set; } = "Sheet";
        public bool IncludeElementIds { get; set; } = false;
        public bool IncludeUserInfo { get; set; } = true;
        public string Encoding { get; set; } = "UTF-8";
        public bool PrettyPrint { get; set; } = true;

        public XMLExportSettings()
        {
            // Default parameters to export
            ParameterNames.AddRange(new[]
            {
                "Drawn By",
                "Checked By",
                "Approved By",
                "Issue Date",
                "Scale",
                "Project Phase"
            });
        }

        public XMLExportSettings Clone()
        {
            var clone = new XMLExportSettings
            {
                OutputFolder = this.OutputFolder,
                FileName = this.FileName,
                IncludeProjectInfo = this.IncludeProjectInfo,
                IncludeSheetParameters = this.IncludeSheetParameters,
                IncludeRevisions = this.IncludeRevisions,
                IncludeDateTime = this.IncludeDateTime,
                ExportAllParameters = this.ExportAllParameters,
                GroupBySheetType = this.GroupBySheetType,
                RootElementName = this.RootElementName,
                SheetElementName = this.SheetElementName,
                IncludeElementIds = this.IncludeElementIds,
                IncludeUserInfo = this.IncludeUserInfo,
                Encoding = this.Encoding,
                PrettyPrint = this.PrettyPrint
            };

            clone.ParameterNames = new List<string>(this.ParameterNames);
            clone.CustomFields = new List<CustomField>();

            foreach (var field in this.CustomFields)
            {
                clone.CustomFields.Add(field.Clone());
            }

            return clone;
        }
    }

    public class CustomField
    {
        public string ElementName { get; set; }
        public string Value { get; set; }
        public string DataType { get; set; } = "String";
        public bool IsRequired { get; set; } = false;
        public string DefaultValue { get; set; } = "";

        public CustomField()
        {
        }

        public CustomField(string elementName, string value)
        {
            ElementName = elementName;
            Value = value;
        }

        public CustomField Clone()
        {
            return new CustomField
            {
                ElementName = this.ElementName,
                Value = this.Value,
                DataType = this.DataType,
                IsRequired = this.IsRequired,
                DefaultValue = this.DefaultValue
            };
        }

        public override string ToString()
        {
            return $"{ElementName}: {Value}";
        }
    }
}
