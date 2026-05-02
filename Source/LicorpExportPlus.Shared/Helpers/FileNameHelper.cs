using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LicorpExportPlus.Helpers
{
    public static class FileNameHelper
    {
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Untitled";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder sanitized = new StringBuilder(fileName);

            foreach (char c in invalidChars)
            {
                sanitized.Replace(c, '_');
            }

            string result = sanitized.ToString().Trim();
            result = result
                .Replace(":", "_")
                .Replace(";", "_")
                .Replace(",", "_")
                .Replace(" ", "_")
                .Replace("{", "_")
                .Replace("}", "_")
                .Replace("[", "_")
                .Replace("]", "_");

            result = Regex.Replace(result, "_+", "_").Trim('_');
            result = result.TrimEnd('.');

            while (result.Contains("  "))
                result = result.Replace("  ", " ");

            if (result.Length > 200)
                result = result.Substring(0, 200).TrimEnd();

            if (string.IsNullOrWhiteSpace(result))
                result = "Untitled";

            string[] reservedNames = {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            string upperResult = result.ToUpper();
            if (reservedNames.Contains(upperResult))
                result = "_" + result;

            return result;
        }

        public static string SanitizeFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            char[] invalidChars = Path.GetInvalidPathChars();
            foreach (char c in invalidChars)
            {
                path = path.Replace(c, '_');
            }

            return path.Trim();
        }

        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            return !fileName.Any(c => invalidChars.Contains(c));
        }

        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
