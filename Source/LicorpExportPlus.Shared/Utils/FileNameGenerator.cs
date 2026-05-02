using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using LicorpExportPlus.Helpers;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Utils
{
    public static class FileNameGenerator
    {
        public static string GenerateFileName(ViewSheet sheet, Document doc, string template, string extension)
        {
            string fileName = template;

            // Replace core placeholders
            fileName = fileName.Replace("{SheetNumber}", sheet.SheetNumber ?? "");
            fileName = fileName.Replace("{SheetName}", sheet.Name ?? "");

            // Project information
            ProjectInfo projectInfo = doc.ProjectInformation;
            fileName = fileName.Replace("{ProjectNumber}",
                ParameterUtils.GetParameterValue(projectInfo, BuiltInParameter.PROJECT_NUMBER));
            fileName = fileName.Replace("{ProjectName}",
                ParameterUtils.GetParameterValue(projectInfo, BuiltInParameter.PROJECT_NAME));
            fileName = fileName.Replace("{ClientName}",
                ParameterUtils.GetParameterValue(projectInfo, "Client Name"));
            fileName = fileName.Replace("{Author}",
                ParameterUtils.GetParameterValue(projectInfo, BuiltInParameter.PROJECT_AUTHOR));

            // Revision information
            Parameter revisionParam = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
            string revision = revisionParam?.AsString() ?? "";
            fileName = fileName.Replace("{Revision}", revision);
            fileName = fileName.Replace("{Rev}", revision);

            // Date and time
            fileName = fileName.Replace("{Date}", System.DateTime.Now.ToString("yyyy-MM-dd"));
            fileName = fileName.Replace("{Time}", System.DateTime.Now.ToString("HH-mm-ss"));
            fileName = fileName.Replace("{DateTime}", System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

            // User and computer
            fileName = fileName.Replace("{User}", System.Environment.UserName);
            fileName = fileName.Replace("{Computer}", System.Environment.MachineName);
            fileName = ResolveEnvironmentVariables(fileName, sheet.SheetNumber, System.DateTime.Now, sanitize: false);

            // Custom parameters
            var matches = Regex.Matches(template, @"\{([^}]+)\}");
            foreach (Match match in matches)
            {
                string paramName = match.Groups[1].Value;

                if (IsStandardPlaceholder(paramName))
                    continue;

                Parameter customParam = sheet.LookupParameter(paramName);
                if (customParam != null && customParam.HasValue)
                {
                    string paramValue = ParameterUtils.GetParameterValueAsString(customParam);
                    fileName = fileName.Replace($"{{{paramName}}}", paramValue);
                }
                else
                {
                    fileName = fileName.Replace($"{{{paramName}}}", "");
                }
            }

            fileName = SanitizeFileName(fileName);

            if (!string.IsNullOrEmpty(extension))
            {
                fileName = $"{fileName}.{extension}";
            }

            return fileName;
        }

        public static string ResolveEnvironmentVariables(string value, string drawingName = "", System.DateTime? issueDate = null, bool sanitize = true)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var date = issueDate ?? System.DateTime.Now;
            var resolved = value
                .Replace("%UserName%", System.Environment.UserName)
                .Replace("%ComputerName%", System.Environment.MachineName)
                .Replace("%DrawingName%", drawingName ?? string.Empty)
                .Replace("%IssueDate%", date.ToString("yyyy-MM-dd"))
                .Replace("%YYYY%", date.ToString("yyyy"))
                .Replace("%Y%", date.ToString("yyyy"))
                .Replace("%YY%", date.ToString("yy"))
                .Replace("%y%", date.ToString("yy"))
                .Replace("%mm%", date.ToString("MM"))
                .Replace("%m%", date.Month.ToString())
                .Replace("%dd%", date.ToString("dd"))
                .Replace("%d%", date.Day.ToString())
                .Replace("%HH%", date.ToString("HH"))
                .Replace("%H%", date.Hour.ToString())
                .Replace("%MM%", date.ToString("mm"))
                .Replace("%M%", date.Minute.ToString())
                .Replace("%SS%", date.ToString("ss"))
                .Replace("%S%", date.Second.ToString());

            return sanitize ? SanitizeFileName(resolved) : resolved;
        }

        public static string BuildNameFromParameters(System.Collections.Generic.IEnumerable<SelectedParameterInfo> parameters, System.Func<string, string> valueResolver, bool sanitize = true)
        {
            if (parameters == null)
            {
                return string.Empty;
            }

            var parts = parameters
                .Select(paramInfo =>
                {
                    var value = paramInfo.IsStaticText
                        ? paramInfo.SampleValue
                        : valueResolver?.Invoke(paramInfo.ParameterName);

                    if (string.IsNullOrEmpty(value) && !paramInfo.IsStaticText)
                    {
                        value = paramInfo.SampleValue;
                    }

                    value = ResolveEnvironmentVariables(value ?? string.Empty, string.Empty, System.DateTime.Now, sanitize: false);

                    var part = $"{paramInfo.Prefix}{value}{paramInfo.Suffix}";
                    return string.IsNullOrEmpty(part) ? null : new { Part = part, Separator = paramInfo.Separator ?? string.Empty };
                })
                .Where(part => part != null)
                .ToList();

            var result = string.Empty;
            for (int i = 0; i < parts.Count; i++)
            {
                result += parts[i].Part;
                if (i < parts.Count - 1)
                {
                    result += parts[i].Separator;
                }
            }

            result = ResolveEnvironmentVariables(result, string.Empty, System.DateTime.Now, sanitize: false);
            return sanitize ? SanitizeFileName(result) : result;
        }

        private static bool IsStandardPlaceholder(string placeholder)
        {
            string[] standardPlaceholders =
            {
                "SheetNumber", "SheetName", "ProjectNumber", "ProjectName",
                "ClientName", "Author", "Revision", "Rev", "Date", "Time",
                "DateTime", "User", "Computer"
            };

            return System.Array.Exists(standardPlaceholders,
                p => p.Equals(placeholder, System.StringComparison.OrdinalIgnoreCase));
        }

        public static string SanitizeFileName(string fileName)
        {
            return FileNameHelper.SanitizeFileName(fileName);
        }

        public static string GenerateSubfolderPath(ViewSheet sheet, Document doc, PSExportSettings settings)
        {
            if (!settings.CreateSubfolders || string.IsNullOrEmpty(settings.SubfolderTemplate))
            {
                return ResolveEnvironmentVariables(settings.OutputFolder, sheet?.SheetNumber, System.DateTime.Now, sanitize: false);
            }

            string subfolderName = settings.SubfolderTemplate;

            subfolderName = subfolderName.Replace("{DrawingType}", GetDrawingType(sheet));
            subfolderName = subfolderName.Replace("{Discipline}", GetDiscipline(sheet));
            subfolderName = subfolderName.Replace("{Level}", GetLevel(sheet));
            subfolderName = subfolderName.Replace("{Phase}", GetPhase(sheet));

            subfolderName = GenerateFileName(sheet, doc, subfolderName, "");
            subfolderName = ResolveEnvironmentVariables(subfolderName, sheet.SheetNumber, System.DateTime.Now, sanitize: false);
            subfolderName = SanitizeFileName(subfolderName);

            var outputFolder = ResolveEnvironmentVariables(settings.OutputFolder, sheet.SheetNumber, System.DateTime.Now, sanitize: false);
            return Path.Combine(outputFolder, subfolderName);
        }

        private static string GetDrawingType(ViewSheet sheet)
        {
            string sheetNumber = sheet.SheetNumber ?? "";

            if (sheetNumber.StartsWith("A"))
                return "Architectural";
            if (sheetNumber.StartsWith("S"))
                return "Structural";
            if (sheetNumber.StartsWith("M"))
                return "Mechanical";
            if (sheetNumber.StartsWith("E"))
                return "Electrical";
            if (sheetNumber.StartsWith("P"))
                return "Plumbing";
            if (sheetNumber.StartsWith("C"))
                return "Civil";
            if (sheetNumber.StartsWith("L"))
                return "Landscape";

            return "General";
        }

        private static string GetDiscipline(ViewSheet sheet)
        {
            Parameter disciplineParam = sheet.LookupParameter("Discipline");
            if (disciplineParam != null && disciplineParam.HasValue)
            {
                return disciplineParam.AsString();
            }

            return GetDrawingType(sheet);
        }

        private static string GetLevel(ViewSheet sheet)
        {
            Parameter levelParam = sheet.LookupParameter("Level") ??
                sheet.LookupParameter("Floor") ??
                sheet.LookupParameter("Storey");

            if (levelParam != null && levelParam.HasValue)
            {
                return levelParam.AsString();
            }

            return "All_Levels";
        }

        private static string GetPhase(ViewSheet sheet)
        {
            Parameter phaseParam = sheet.LookupParameter("Phase") ??
                sheet.LookupParameter("Construction Phase");

            if (phaseParam != null && phaseParam.HasValue)
            {
                return phaseParam.AsString();
            }

            return "Current";
        }
    }
}
