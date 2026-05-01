using System;
using System.Collections.Generic;
using System.ComponentModel;
using Licorp.Diagnostics;
using Autodesk.Revit.DB;
using LicorpExportPlus.Utils;

namespace LicorpExportPlus.Models
{
    public class ViewItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _customFileName;
        private string _viewNumber;
        private string _viewType;
        private bool _isFullyLoaded;
        private string _scale;
        private string _detailLevel;
        private string _discipline;
        private string _viewInfo;

        public string ViewId { get; set; }
        public string ViewName { get; set; }

        public string ViewNumber
        {
            get => _viewNumber;
            set
            {
                if (_viewNumber != value)
                {
                    _viewNumber = value;
                    OnPropertyChanged(nameof(ViewNumber));
                }
            }
        }

        public string ViewType
        {
            get => _viewType;
            set
            {
                if (_viewType != value)
                {
                    _viewType = value;
                    OnPropertyChanged(nameof(ViewType));
                }
            }
        }

        public string Scale
        {
            get => _scale;
            set
            {
                if (_scale != value)
                {
                    _scale = value;
                    OnPropertyChanged(nameof(Scale));
                }
            }
        }

        public string DetailLevel
        {
            get => _detailLevel;
            set
            {
                if (_detailLevel != value)
                {
                    _detailLevel = value;
                    OnPropertyChanged(nameof(DetailLevel));
                }
            }
        }

        public string Discipline
        {
            get => _discipline;
            set
            {
                if (_discipline != value)
                {
                    _discipline = value;
                    OnPropertyChanged(nameof(Discipline));
                }
            }
        }

        // 🆕 EXTRA INFO: For 3D views, show template/orientation; for others, show discipline
        public string ViewInfo
        {
            get => _viewInfo;
            set
            {
                if (_viewInfo != value)
                {
                    _viewInfo = value;
                    OnPropertyChanged(nameof(ViewInfo));
                }
            }
        } public ElementId RevitViewId { get; set; }

        // Store reference to Revit View for lazy loading
        public Autodesk.Revit.DB.View RevitView { get; set; }

        /// <summary>
        /// Indicates whether full details have been loaded
        /// </summary>
        public bool IsFullyLoaded
        {
            get => _isFullyLoaded;
            set
            {
                if (_isFullyLoaded != value)
                {
                    _isFullyLoaded = value;
                    OnPropertyChanged(nameof(IsFullyLoaded));
                }
            }
        }

        // For dropdown in DataGrid
        public List<string> AvailableViewTypes { get; set; } = new List<string>
        {
            "3D", "Rendering", "Section", "Elevation", "Floor Plan", "Detail", "Legend"
        };

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string CustomFileName
        {
            get => _customFileName;
            set
            {
                if (_customFileName != value)
                {
                    _customFileName = value;
                    OnPropertyChanged(nameof(CustomFileName));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Public method to force UI refresh from external callers
        /// </summary>
        public void RefreshUI()
        {
            OnPropertyChanged(nameof(DetailLevel));
            OnPropertyChanged(nameof(Discipline));
            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(ViewInfo));
            OnPropertyChanged(nameof(ViewNumber));
        }

        public ViewItem()
        {
            IsSelected = false;
            CustomFileName = ViewName;
        }

        /// <summary>
        /// Full loading constructor (old behavior) - loads all details immediately
        /// </summary>
    public ViewItem(Autodesk.Revit.DB.View revitView)
    {
        if (revitView != null)
        {
            RevitView = revitView;
            RevitViewId = revitView.Id;
            ViewId = revitView.Id.GetIdValueString();
            ViewName = revitView.Name;
            ViewNumber = "";
            ViewType = GetViewTypeString(revitView.ViewType);
            Scale = GetViewScale(revitView);
            DetailLevel = revitView.DetailLevel.ToString();
            Discipline = GetViewDiscipline(revitView);
            CustomFileName = ViewName;
            IsSelected = false;
            IsFullyLoaded = true;
        }
    }

    /// <summary>
    /// Lightweight constructor - loads only basic information for fast initialization
    /// </summary>
    public ViewItem(Autodesk.Revit.DB.View revitView, bool loadFullDetails)
    {
        if (revitView != null)
        {
            RevitView = revitView;
            RevitViewId = revitView.Id;
            ViewId = revitView.Id.GetIdValueString();
            ViewName = revitView.Name;
                ViewNumber = "";
                ViewType = GetViewTypeString(revitView.ViewType);
                CustomFileName = ViewName;
                IsSelected = false;

                if (loadFullDetails)
                {
                    LoadFullDetails();
                }
                else
                {
                    // Set default values for lazy loading
                    Scale = "Not loaded";
                    DetailLevel = "Not loaded";
                    Discipline = "Not loaded";
                    ViewInfo = "Not loaded";
                    IsFullyLoaded = false;
                }
            }
        }

        /// <summary>
        /// Load full details when view is selected
        /// </summary>
        public void LoadFullDetails(Document document = null)
        {
            if (IsFullyLoaded)
            {
                System.Diagnostics.Debug.WriteLine($"👁️ [LoadFullDetails] SKIPPED - Already loaded for {ViewName}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"👁️ [LoadFullDetails] LOADING for {ViewName}...");

            try
            {
                // 🆕 CRITICAL: Get View from Document if RevitView is null (lazy load optimization)
                if (RevitView == null && RevitViewId != null && document != null)
                {
                    System.Diagnostics.Debug.WriteLine($"👁️ [LoadFullDetails] Getting View from Document (ID={RevitViewId})...");
                    RevitView = document.GetElement(RevitViewId) as Autodesk.Revit.DB.View;

                    if (RevitView == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"👁️ [LoadFullDetails] ❌ ERROR: Could not get View from Document");
                        throw new Exception("View not found in Document");
                    }

                    System.Diagnostics.Debug.WriteLine($"👁️ [LoadFullDetails] ✅ Got View: {RevitView.Name}");
                }

                if (RevitView == null)
                {
                    System.Diagnostics.Debug.WriteLine($"👁️ [LoadFullDetails] ❌ ERROR: RevitView is null and no Document provided");
                    throw new Exception("RevitView is null - cannot load details");
                }

                Scale = GetViewScale(RevitView);
                DetailLevel = RevitView.DetailLevel.ToString();
                Discipline = GetViewDiscipline(RevitView);
                ViewInfo = GetViewInfo(RevitView); // 🆕 Get extra info
                IsFullyLoaded = true;

                System.Diagnostics.Debug.WriteLine($"👁️ [LoadFullDetails] ✅ SUCCESS - DetailLevel={DetailLevel}, Discipline={Discipline}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"👁️ [LoadFullDetails] ❌ ERROR: {ex.Message}");

                // If loading fails, keep default values
                // Intentionally ignore exception details for graceful degradation
                Scale = "Error";
                DetailLevel = "Error";
                Discipline = "Error";
                ViewInfo = "Error";
                IsFullyLoaded = true; // 🆕 Mark as loaded even on error to prevent retrying
            }
        }

        private string GetViewTypeString(ViewType viewType)
        {
            switch (viewType)
            {
                case Autodesk.Revit.DB.ViewType.ThreeD: return "3D";
                case Autodesk.Revit.DB.ViewType.FloorPlan: return "Floor Plan";
                case Autodesk.Revit.DB.ViewType.Elevation: return "Elevation";
                case Autodesk.Revit.DB.ViewType.Section: return "Section";
                case Autodesk.Revit.DB.ViewType.Detail: return "Detail";
                case Autodesk.Revit.DB.ViewType.Rendering: return "Rendering";
                case Autodesk.Revit.DB.ViewType.Legend: return "Legend";
                default: return viewType.ToString();
            }
        }

        private string GetViewScale(Autodesk.Revit.DB.View view)
        {
            try
            {
                var scale = view.Scale;
                return scale > 0 ? $"1 : {scale}" : "Custom";
            }
            catch
            {
                return "Custom";
            }
        }

        private string GetViewDiscipline(Autodesk.Revit.DB.View view)
        {
            try
            {
                var disciplineParam = view.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE);
                return disciplineParam?.AsValueString() ?? "Architectural";
            }
            catch
            {
                return "Architectural";
            }
        }

        /// <summary>
        /// Get additional info for view:
        /// - For 3D views: Show if it's perspective/isometric + camera orientation
        /// - For others: Show discipline or view template name
        /// </summary>
        private string GetViewInfo(Autodesk.Revit.DB.View view)
        {
            try
            {
                // For 3D views, show perspective type and orientation
                if (view is View3D view3D)
                {
                    var isPerspective = view3D.IsPerspective;
                    var viewOrientation = view3D.GetOrientation();

                    // Get view direction (simplified)
                    var forward = viewOrientation.ForwardDirection;
                    string direction = GetDirectionName(forward);

                    return isPerspective
                        ? $"Perspective - {direction}"
                        : $"Isometric - {direction}";
                }

                // For other views, show discipline
                var disciplineParam = view.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE);
                if (disciplineParam != null && !string.IsNullOrEmpty(disciplineParam.AsValueString()))
                {
                    return disciplineParam.AsValueString();
                }

                // Fallback: show view template name
                var templateId = view.ViewTemplateId;
                if (templateId != null && templateId != ElementId.InvalidElementId)
                {
                    var doc = view.Document;
                    var template = doc.GetElement(templateId);
                    return template?.Name ?? "No Template";
                }

                return "Architectural";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Convert XYZ direction to human-readable name
        /// </summary>
        private string GetDirectionName(XYZ direction)
        {
            try
            {
                // Normalize and check primary direction
                var normalized = direction.Normalize();

                // Check if primarily looking in X, Y, or Z direction
                if (Math.Abs(normalized.X) > 0.7)
                    return normalized.X > 0 ? "East" : "West";
                if (Math.Abs(normalized.Y) > 0.7)
                    return normalized.Y > 0 ? "North" : "South";
                if (Math.Abs(normalized.Z) > 0.7)
                    return normalized.Z > 0 ? "Up" : "Down";

                // Diagonal views
                if (normalized.X > 0 && normalized.Y > 0)
                    return "Northeast";
                if (normalized.X > 0 && normalized.Y < 0)
                    return "Southeast";
                if (normalized.X < 0 && normalized.Y > 0)
                    return "Northwest";
                if (normalized.X < 0 && normalized.Y < 0)
                    return "Southwest";

                return "Custom";
            }
            catch
            {
                return "Custom";
            }
        }
    }
}
