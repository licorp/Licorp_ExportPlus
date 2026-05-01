using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LicorpExportPlus.Services
{
    /// <summary>
    /// Cleans companion DWG files created by XREF-style exports.
    /// Prefer compact DWG export with MergedViews enabled so this cleanup is not needed.
    /// </summary>
    public class DWGCleanupService
    {
        public static void CleanupDWGExport(string mainDwgPath)
        {
            try
            {
                if (!File.Exists(mainDwgPath))
                {
                    Debug.WriteLine($"[DWG Cleanup] File not found: {mainDwgPath}");
                    return;
                }

                Debug.WriteLine("[DWG Cleanup] ========================================");
                Debug.WriteLine($"[DWG Cleanup] Starting cleanup for: {Path.GetFileName(mainDwgPath)}");

                var directory = Path.GetDirectoryName(mainDwgPath);
                var mainFileName = Path.GetFileNameWithoutExtension(mainDwgPath);

                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(mainFileName))
                {
                    Debug.WriteLine("[DWG Cleanup] Invalid DWG path.");
                    return;
                }

                var allDwgFiles = Directory.GetFiles(directory, "*.dwg")
                    .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(mainFileName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => new FileInfo(f).Length)
                    .ToList();

                Debug.WriteLine($"[DWG Cleanup] Found {allDwgFiles.Count} related DWG files");

                if (allDwgFiles.Count <= 1)
                {
                    Debug.WriteLine("[DWG Cleanup] Only 1 file found - no cleanup needed");
                    return;
                }

                var realMainFile = allDwgFiles.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Equals(mainFileName, StringComparison.OrdinalIgnoreCase))
                    ?? allDwgFiles[0];

                Debug.WriteLine($"[DWG Cleanup] Main file identified: {Path.GetFileName(realMainFile)}");

                var deletedCount = 0;
                foreach (var file in allDwgFiles)
                {
                    if (file.Equals(realMainFile, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[DWG Cleanup] KEEP: {Path.GetFileName(file)} (main file)");
                        continue;
                    }

                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                        Debug.WriteLine($"[DWG Cleanup] DELETED: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DWG Cleanup] Could not delete {Path.GetFileName(file)}: {ex.Message}");
                    }
                }

                Debug.WriteLine("[DWG Cleanup] ========================================");
                Debug.WriteLine($"[DWG Cleanup] Cleanup completed: Deleted {deletedCount} companion DWG files");
                Debug.WriteLine("[DWG Cleanup] NOTE: Cleanup does not bind XREF references inside the main DWG.");
                Debug.WriteLine("[DWG Cleanup] ========================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DWG Cleanup] ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// DWG XREF references cannot be safely removed with netDxf because it does not support DWG writing.
        /// Use compact DWG export (MergedViews = true) or AutoCAD bind for reliable self-contained DWG output.
        /// </summary>
        public static bool RemoveXRefReferences(string dwgPath)
        {
            Debug.WriteLine($"[DWG Cleanup] XREF reference removal skipped for: {dwgPath}");
            Debug.WriteLine("[DWG Cleanup] Use compact DWG export (MergedViews = true) or AutoCAD bind for reliable self-contained DWG output.");
            return false;
        }

        public static bool HasXRefReferences(string dwgPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(dwgPath);
                var baseName = Path.GetFileNameWithoutExtension(dwgPath);

                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(baseName) || !Directory.Exists(directory))
                {
                    return false;
                }

                var xrefFiles = Directory.GetFiles(directory, "*.dwg")
                    .Where(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        return !name.Equals(baseName, StringComparison.OrdinalIgnoreCase)
                            && name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                return xrefFiles.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
