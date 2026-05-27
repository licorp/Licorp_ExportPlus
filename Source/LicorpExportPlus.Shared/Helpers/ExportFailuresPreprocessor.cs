using System.Collections.Generic;
using Autodesk.Revit.DB;
using Licorp.Diagnostics;

namespace LicorpExportPlus.Helpers;

public class ExportFailuresPreprocessor : IFailuresPreprocessor
{
    public List<string> FailureMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public bool HasFailures => FailureMessages.Count > 0;
    public bool HasWarnings => WarningMessages.Count > 0;

    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        foreach (var failure in failuresAccessor.GetFailureMessages())
        {
            var message = failure.GetDescriptionText();
            var severity = failure.GetSeverity();

            if (severity == FailureSeverity.Warning)
            {
                WarningMessages.Add(message);
                LicorpTrace.Warn($"[ExportFailures] Warning: {message}");
                failuresAccessor.DeleteWarning(failure);
            }
            else if (severity == FailureSeverity.Error)
            {
                FailureMessages.Add(message);
                LicorpTrace.Error($"[ExportFailures] Error: {message}");
            }
        }

        return FailureProcessingResult.Continue;
    }

    public void Clear()
    {
        FailureMessages.Clear();
        WarningMessages.Clear();
    }

    public string GetSummary()
    {
        var parts = new List<string>();
        if (FailureMessages.Count > 0)
            parts.Add($"{FailureMessages.Count} error(s)");
        if (WarningMessages.Count > 0)
            parts.Add($"{WarningMessages.Count} warning(s)");
        return parts.Count > 0 ? string.Join(", ", parts) : "No issues";
    }
}
