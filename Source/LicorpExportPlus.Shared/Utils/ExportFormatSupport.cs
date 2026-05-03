using System;
using System.Collections.Generic;
using System.Linq;

namespace LicorpExportPlus.Utils
{
    internal static class ExportFormatSupport
    {
        private static readonly HashSet<string> UnsupportedFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DGN",
            "DWF"
        };

        public static bool IsSupported(string format)
        {
            return !UnsupportedFormats.Contains(Normalize(format));
        }

        public static bool IsUnsupported(string format)
        {
            return UnsupportedFormats.Contains(Normalize(format));
        }

        public static string Normalize(string format)
        {
            return (format ?? string.Empty).Trim().ToUpperInvariant();
        }

        public static IReadOnlyList<string> FilterSupported(IEnumerable<string> formats)
        {
            if (formats == null)
            {
                return Array.Empty<string>();
            }

            return formats
                .Select(Normalize)
                .Where(format => !string.IsNullOrWhiteSpace(format))
                .Where(IsSupported)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void DisableUnsupported(IDictionary<string, bool> formats)
        {
            if (formats == null)
            {
                return;
            }

            foreach (var format in UnsupportedFormats)
            {
                formats[format] = false;
            }
        }
    }
}
