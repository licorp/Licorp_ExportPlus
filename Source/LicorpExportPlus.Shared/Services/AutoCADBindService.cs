using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Licorp.Diagnostics;
using Microsoft.Win32;

namespace LicorpExportPlus.Services
{
    /// <summary>
    /// Manages AutoCAD operations to bind XREF files into a single DWG
    /// </summary>
    public class AutoCADBindService
    {
        private static readonly string[] AutoCADVersions = new[]
        {
            "AutoCAD.Application.27", // AutoCAD 2027
            "AutoCAD.Application.26", // AutoCAD 2026
            "AutoCAD.Application.25", // AutoCAD 2025
            "AutoCAD.Application.24", // AutoCAD 2024
            "AutoCAD.Application.23", // AutoCAD 2023
            "AutoCAD.Application.22", // AutoCAD 2022
            "AutoCAD.Application.21", // AutoCAD 2021
            "AutoCAD.Application.20", // AutoCAD 2020
        };

        /// <summary>
        /// Find AutoCAD installation path
        /// </summary>
        public static string FindAutoCADPath()
        {
            try
            {
                // Prefer registry discovery because Autodesk install paths and vertical products vary by version.
                var autocadPath = FindAutoCADFromRegistry();
                if (!string.IsNullOrEmpty(autocadPath) && File.Exists(autocadPath))
                {
                    LicorpTrace.Info($"[AutoCAD] Found AutoCAD from registry: {autocadPath}");
                    return autocadPath;
                }

                // Try common installation paths
                string[] possiblePaths = new[]
                {
                    @"C:\Program Files\Autodesk\AutoCAD 2027\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2026\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2025\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2024\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2023\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2022\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2021\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2020\acad.exe",
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        LicorpTrace.Info($"[AutoCAD] Found AutoCAD at: {path}");
                        return path;
                    }
                }

                LicorpTrace.Warn("[AutoCAD] AutoCAD not found");
                return null;
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"[AutoCAD] Error finding AutoCAD: {ex.Message}");
                return null;
            }
        }

        private static string FindAutoCADFromRegistry()
        {
            try
            {
                foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    foreach (var basePath in new[] { @"SOFTWARE\Autodesk\AutoCAD", @"SOFTWARE\WOW6432Node\Autodesk\AutoCAD" })
                    {
                        using (var key = root.OpenSubKey(basePath))
                        {
                            if (key == null)
                            {
                                continue;
                            }

                            var subKeys = key.GetSubKeyNames().OrderByDescending(k => k).ToList();
                            foreach (var subKeyName in subKeys)
                            {
                                using (var subKey = key.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null)
                                    {
                                        continue;
                                    }

                                    var directAcadPath = subKey.GetValue("AcadLocation") as string;
                                    if (!string.IsNullOrEmpty(directAcadPath))
                                    {
                                        var directExePath = Path.Combine(directAcadPath, "acad.exe");
                                        if (File.Exists(directExePath))
                                        {
                                            return directExePath;
                                        }
                                    }

                                    foreach (var installKeyName in subKey.GetSubKeyNames())
                                    {
                                        using (var installKey = subKey.OpenSubKey(installKeyName))
                                        {
                                            var acadPath = installKey?.GetValue("AcadLocation") as string;
                                            if (string.IsNullOrEmpty(acadPath))
                                            {
                                                continue;
                                            }

                                            var exePath = Path.Combine(acadPath, "acad.exe");
                                            if (File.Exists(exePath))
                                            {
                                                return exePath;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LicorpTrace.Warn($"[AutoCAD] Registry search failed: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Bind all XREFs in a DWG file using AutoCAD
        /// </summary>
        public static bool BindXRefsInDWG(string mainDwgPath, bool deleteXRefFiles = true)
        {
            try
            {
                if (!File.Exists(mainDwgPath))
                {
                    Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] File not found: {mainDwgPath}");
                    return false;
                }

                var autocadPath = FindAutoCADPath();
                if (string.IsNullOrEmpty(autocadPath))
                {
                    Licorp.Diagnostics.LicorpTrace.Dbg("[AutoCAD Bind] AutoCAD not installed - skipping XREF binding");
                    return false;
                }

                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] ========================================");
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Starting XREF binding for: {Path.GetFileName(mainDwgPath)}");
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Using AutoCAD: {autocadPath}");

                // Create AutoCAD script to bind XREFs
                var scriptPath = CreateBindScript(mainDwgPath);
                if (string.IsNullOrEmpty(scriptPath))
                {
                    return false;
                }

                // Run AutoCAD with script
                var success = RunAutoCADScript(autocadPath, mainDwgPath, scriptPath);

                // Clean up script file
                try
                {
                    if (File.Exists(scriptPath))
                    {
                        File.Delete(scriptPath);
                    }
                }
                catch (Exception ex)
                {
                    LicorpTrace.Warn($"[AutoCAD Bind] Could not delete temporary script '{scriptPath}': {ex.Message}");
                }

                if (success && deleteXRefFiles)
                {
                    // Delete XREF files after binding
                    DeleteXRefFiles(mainDwgPath);
                }

                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] ========================================");
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Binding completed: {(success ? "SUCCESS" : "FAILED")}");

                return success;
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] ERROR: {ex.Message}");
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Create AutoCAD script file to bind all XREFs
        /// </summary>
        private static string CreateBindScript(string dwgPath)
        {
            try
            {
                var scriptPath = Path.Combine(Path.GetTempPath(), $"ExportPlus_Bind_{Guid.NewGuid()}.scr");

                var script = new StringBuilder();

                // Open the DWG file
                script.AppendLine($"OPEN \"{dwgPath}\"");

                // Bind all XREFs with INSERT method (merges into main drawing)
                script.AppendLine("-XREF");
                script.AppendLine("B"); // Bind
                script.AppendLine("*"); // All XREFs
                script.AppendLine("I"); // Insert method (better than Bind method)
                script.AppendLine(""); // Confirm

                // Purge to clean up
                script.AppendLine("-PURGE");
                script.AppendLine("A"); // All
                script.AppendLine("*"); // All items
                script.AppendLine("N"); // No to nested purge

                // Save and close
                script.AppendLine("QSAVE");
                script.AppendLine("QUIT");
                script.AppendLine("Y"); // Yes to save changes

                File.WriteAllText(scriptPath, script.ToString());

                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Script created: {scriptPath}");
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Script content:\n{script}");

                return scriptPath;
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Failed to create script: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Run AutoCAD with the binding script
        /// </summary>
        private static bool RunAutoCADScript(string autocadPath, string dwgPath, string scriptPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = autocadPath,
                    Arguments = $"/b \"{scriptPath}\" /nossm", // /b = run script, /nossm = no startup screen
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Starting AutoCAD...");
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Command: {autocadPath} {startInfo.Arguments}");

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Licorp.Diagnostics.LicorpTrace.Dbg("[AutoCAD Bind] Failed to start AutoCAD process");
                        return false;
                    }

                    Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] AutoCAD process started (PID: {process.Id})");
                    Licorp.Diagnostics.LicorpTrace.Dbg("[AutoCAD Bind] Waiting for AutoCAD to complete binding...");

                    // Wait for AutoCAD to finish (max 2 minutes)
                    var completed = process.WaitForExit(120000); // 2 minutes timeout

                    if (!completed)
                    {
                        Licorp.Diagnostics.LicorpTrace.Dbg("[AutoCAD Bind] AutoCAD process timeout - killing process");
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception killEx)
                        {
                            LicorpTrace.Warn($"[AutoCAD Bind] Could not kill timed-out AutoCAD process (PID: {process.Id}): {killEx.Message}");
                        }
                        return false;
                    }

                    Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] AutoCAD process completed with exit code: {process.ExitCode}");

                    // Exit code 0 = success
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Failed to run AutoCAD: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete XREF files after successful binding
        /// </summary>
        private static void DeleteXRefFiles(string mainDwgPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(mainDwgPath);
                var mainFileName = Path.GetFileNameWithoutExtension(mainDwgPath);

                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Looking for XREF files to delete in: {directory}");

                // Find all DWG files with the same base name (XREFs have suffixes)
                var allDwgFiles = Directory.GetFiles(directory, "*.dwg");
                var deletedCount = 0;

                foreach (var file in allDwgFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);

                    // XREF files contain the main sheet name + additional suffixes
                    // Example: P101-PLAN - L1 SANITARY-3D View - Cover.dwg
                    // Main file: P101-PLAN - L1 SANITARY.dwg

                    if (file != mainDwgPath && fileName.StartsWith(mainFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                            Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] ✓ Deleted XREF file: {Path.GetFileName(file)}");
                        }
                        catch (Exception ex)
                        {
                            Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Could not delete {Path.GetFileName(file)}: {ex.Message}");
                        }
                    }
                }

                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Deleted {deletedCount} XREF files");
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"[AutoCAD Bind] Error deleting XREF files: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if AutoCAD is installed
        /// </summary>
        public static bool IsAutoCADInstalled()
        {
            return !string.IsNullOrEmpty(FindAutoCADPath());
        }
    }
}
