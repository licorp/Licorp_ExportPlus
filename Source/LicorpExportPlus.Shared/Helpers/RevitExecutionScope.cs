using System;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit;

namespace LicorpExportPlus.Helpers
{
    internal sealed class RevitExecutionScope : IDisposable
    {
        private readonly IDisposable _dialogScope;
        private readonly IDisposable _failureScope;

        private RevitExecutionScope(IDisposable dialogScope, IDisposable failureScope)
        {
            _dialogScope = dialogScope;
            _failureScope = failureScope;
        }

        public static RevitExecutionScope Create(bool suppressDialogs = true, bool resolveFailures = true)
        {
            if (!RevitContext.IsRevitInApiMode)
            {
                return new RevitExecutionScope(NoopDisposable.Instance, NoopDisposable.Instance);
            }

            var dialogScope = suppressDialogs
                ? RevitContext.BeginDialogSuppressionScope(TaskDialogResult.Ok)
                : NoopDisposable.Instance;
            var failureScope = resolveFailures
                ? RevitApiContext.BeginFailureSuppressionScope(true)
                : NoopDisposable.Instance;

            return new RevitExecutionScope(dialogScope, failureScope);
        }

        public void Dispose()
        {
            _failureScope?.Dispose();
            _dialogScope?.Dispose();
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();

            public void Dispose()
            {
            }
        }
    }
}
