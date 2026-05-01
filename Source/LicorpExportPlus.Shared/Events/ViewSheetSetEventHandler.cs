using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LicorpExportPlus.Services;
using System;
using System.Collections.Generic;

namespace LicorpExportPlus.Events
{
    public class ViewSheetSetEventHandler : IExternalEventHandler
    {
        public enum OperationType
        {
            CreateNew,
            AddToExisting,
            Delete
        }

        public OperationType Operation { get; set; }
        public string SetName { get; set; }
        public List<ElementId> SelectedIds { get; set; }
        public ViewSheetSetService ViewSheetSetManager { get; set; }
        public Action<bool, string> ResultAction { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                if (ViewSheetSetManager == null)
                {
                    ResultAction?.Invoke(false, "ViewSheetSetManager is not initialized");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SetName))
                {
                    ResultAction?.Invoke(false, "Set name cannot be empty");
                    return;
                }

                if (Operation != OperationType.Delete)
                {
                    if (SelectedIds == null || SelectedIds.Count == 0)
                    {
                        ResultAction?.Invoke(false, "No sheets or views selected");
                        return;
                    }
                }

                switch (Operation)
                {
                    case OperationType.CreateNew:
                        var viewSheetSet = ViewSheetSetManager.CreateViewSheetSet(SetName, SelectedIds);

                        if (viewSheetSet != null)
                        {
                            ResultAction?.Invoke(true, $"Created ViewSheetSet: {SetName}");
                        }
                        else
                        {
                            ResultAction?.Invoke(false, "Failed to create ViewSheetSet (returned null)");
                        }
                        break;

                    case OperationType.AddToExisting:
                        bool success = ViewSheetSetManager.AddToExistingSet(SetName, SelectedIds);

                        if (success)
                        {
                            ResultAction?.Invoke(true, $"Added {SelectedIds.Count} items to '{SetName}'");
                        }
                        else
                        {
                            ResultAction?.Invoke(false, $"Failed to add items to '{SetName}'");
                        }
                        break;

                    case OperationType.Delete:
                        bool deleted = ViewSheetSetManager.DeleteViewSheetSet(SetName);

                        if (deleted)
                        {
                            ResultAction?.Invoke(true, $"Deleted ViewSheetSet: {SetName}");
                        }
                        else
                        {
                            ResultAction?.Invoke(false, $"Failed to delete '{SetName}'");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                ResultAction?.Invoke(false, ex.Message);
            }
        }

        public string GetName()
        {
            return "ViewSheetSet Creation Handler";
        }
    }
}
