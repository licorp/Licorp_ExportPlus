using Autodesk.Revit.DB;
using Licorp.Diagnostics;

namespace LicorpExportPlus.Services
{
    internal class RevitFailurePreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages();
            foreach (var failure in failures)
            {
                var severity = failure.GetSeverity();
                var description = failure.GetDescriptionText();

                if (severity == FailureSeverity.Warning)
                {
                    LicorpTrace.Warn($"Revit warning suppressed: {description}");
                    failuresAccessor.DeleteWarning(failure);
                }
                else
                {
                    LicorpTrace.Error($"Revit failure during transaction: {description}");
                }
            }

            return FailureProcessingResult.Continue;
        }

        public static void ApplyTo(Transaction transaction)
        {
            var options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new RevitFailurePreprocessor());
            transaction.SetFailureHandlingOptions(options);
        }
    }
}
