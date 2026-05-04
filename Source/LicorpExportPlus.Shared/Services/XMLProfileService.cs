using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Linq;
using Autodesk.Revit.DB;
using LicorpExportPlus.Models;
using LicorpExportPlus.Utils;

namespace LicorpExportPlus.Services
{
    public class XMLProfileService
    {
        private static string ProfilesFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ExportPlusAddin", "Profiles");

        private static string DiRootsProfilesFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DiRoots", "ExportPlus");

        static XMLProfileService()
        {
            if (!Directory.Exists(ProfilesFolder))
                Directory.CreateDirectory(ProfilesFolder);
        }

        public static ExportPlusXMLProfile LoadProfileFromXML(string filePath)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(ExportPlusProfileList));
                using (var reader = new StreamReader(filePath))
                {
                    var profileList = (ExportPlusProfileList)serializer.Deserialize(reader);
                    var profile = profileList.Profiles.FirstOrDefault();
                    if (profile != null)
                    {
                    }
                    return profile;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading profile: {ex.Message}");
            }
        }

        public static void SaveProfileToXML(ExportPlusXMLProfile profile, string filePath)
        {
            try
            {
                var profileList = new ExportPlusProfileList();
                profileList.Profiles.Add(profile);

                var serializer = new XmlSerializer(typeof(ExportPlusProfileList));
                using (var writer = new StreamWriter(filePath))
                {
                    serializer.Serialize(writer, profileList);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving profile: {ex.Message}");
            }
        }

        public static List<string> GetAvailableXMLProfiles()
        {
            var profiles = new List<string>();

            if (Directory.Exists(ProfilesFolder))
            {
                var ourProfiles = Directory.GetFiles(ProfilesFolder, "*.xml")
                    .Select(Path.GetFileNameWithoutExtension);
                profiles.AddRange(ourProfiles);
            }

            if (Directory.Exists(DiRootsProfilesFolder))
            {
                var diRootsProfiles = Directory.GetFiles(DiRootsProfilesFolder, "*.xml")
                    .Select(f => "DiRoots: " + Path.GetFileNameWithoutExtension(f));
                profiles.AddRange(diRootsProfiles);
            }

            return profiles;
        }

        public static List<SheetFileNameInfo> GenerateCustomFileNames(
            ExportPlusXMLProfile profile,
            List<ViewSheet> sheets)
        {
            var result = new List<SheetFileNameInfo>();

            List<SelectionParameter> parameters = null;
            string separator = "-";

            if (profile.TemplateInfo.SelectionSheets?.SelectedParamsVirtual?.SelectionParameters != null)
            {
                parameters = profile.TemplateInfo.SelectionSheets.SelectedParamsVirtual.SelectionParameters
                    .Where(p => p.IsSelected)
                    .ToList();

                separator = profile.TemplateInfo.SelectionSheets.FieldSeparator ?? "-";
            }

if (parameters == null || !parameters.Any())
{
foreach (var sheet in sheets)
{
result.Add(new SheetFileNameInfo
{
SheetId =
                        sheet.Id.GetIdValueString()
,
SheetNumber = sheet.SheetNumber,
SheetName = sheet.Name,
Revision = GetSheetRevision(sheet),
Size = GetSheetPaperSize(sheet),
CustomFileName = sheet.SheetNumber,
IsSelected = true
});
}
}
            else
            {
                foreach (var sheet in sheets)
                {
                    var fileName = BuildCustomFileNameFromSelectionParams(sheet, parameters, separator);

result.Add(new SheetFileNameInfo
{
SheetId =
                        sheet.Id.GetIdValueString()
,
SheetNumber = sheet.SheetNumber,
SheetName = sheet.Name,
Revision = GetSheetRevision(sheet),
Size = GetSheetPaperSize(sheet),
CustomFileName = fileName,
IsSelected = true
});
                }
            }

            return result;
        }

        private static string BuildCustomFileNameFromSelectionParams(
            ViewSheet sheet,
            List<SelectionParameter> parameters,
            string separator)
        {
            var parts = new List<string>();

            foreach (var param in parameters)
            {
                string value = "";

                if (param.Type == "CustemSeparator")
                {
                    if (!string.IsNullOrEmpty(param.DisplayName))
                    {
                        parts.Add(param.DisplayName.Trim());
                    }
                    continue;
                }

                string paramName = param.DisplayName?.Trim() ?? "";

                switch (paramName)
                {
                    case "Sheet Number":
                        value = sheet.SheetNumber;
                        break;
                    case "Sheet Number Prefix":
                        var sheetNumber = sheet.SheetNumber;
                        var dashIndex = sheetNumber.IndexOf('-');
                        value = dashIndex > 0 ? sheetNumber.Substring(0, dashIndex) : "";
                        break;
                    case "Sheet Name":
                        value = sheet.Name;
                        break;
                    case "Current Revision":
                        value = GetSheetRevision(sheet);
                        break;
                    default:
                        var sheetParam = sheet.LookupParameter(paramName);
                        if (sheetParam != null)
                        {
                            value = GetParameterValueAsString(sheetParam);
                        }
                        break;
                }

                if (!string.IsNullOrEmpty(value))
                {
                    parts.Add(value);
                }
            }

            var fileName = string.Join("", parts);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = sheet.SheetNumber;
            }

            return fileName;
        }

        private static string GetParameterValueAsString(Parameter param)
        {
            try
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        return param.AsString() ?? "";
                    case StorageType.Integer:
                        return param.AsInteger().ToString();
                    case StorageType.Double:
                        return param.AsDouble().ToString("F2");
                    case StorageType.ElementId:
                        return param.AsValueString() ?? "";
                    default:
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }

        private static string GetParameterValue(ViewSheet sheet, string parameterName)
        {
            try
            {
                switch (parameterName)
                {
                    case "Sheet Number":
                        return sheet.SheetNumber;
                    case "Sheet Name":
                        return sheet.Name;
                    case "Current Revision":
                        return GetSheetRevision(sheet);
                    default:
                        var param = sheet.LookupParameter(parameterName);
                        if (param != null)
                        {
                            switch (param.StorageType)
                            {
                                case StorageType.String:
                                    return param.AsString() ?? "";
                                case StorageType.Integer:
                                    return param.AsInteger().ToString();
                                case StorageType.Double:
                                    return param.AsDouble().ToString();
                                default:
                                    return param.AsValueString() ?? "";
                            }
                        }
                        return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string GetSheetRevision(ViewSheet sheet)
        {
            try
            {
                var revParam = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION);
                return revParam?.AsString() ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string GetSheetPaperSize(ViewSheet sheet)
        {
            try
            {
                var titleBlocks = new FilteredElementCollector(sheet.Document, sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .ToElements();

                if (titleBlocks.Any())
                {
                    var titleBlock = titleBlocks.First();
                    var sizeParam = titleBlock.LookupParameter("Sheet Size");
                    if (sizeParam != null)
                    {
                        return sizeParam.AsString() ?? "A3";
                    }
                }

                var outline = sheet.Outline;
                var width = outline.Max.U - outline.Min.U;
                var height = outline.Max.V - outline.Min.V;

                var widthMm = width * 304.8;
                var heightMm = height * 304.8;

                if (Math.Abs(widthMm - 420) < 50 && Math.Abs(heightMm - 297) < 50) return "A3";
                if (Math.Abs(widthMm - 297) < 50 && Math.Abs(heightMm - 210) < 50) return "A4";
                if (Math.Abs(widthMm - 594) < 50 && Math.Abs(heightMm - 420) < 50) return "A2";
                if (Math.Abs(widthMm - 841) < 50 && Math.Abs(heightMm - 594) < 50) return "A1";
                if (Math.Abs(widthMm - 1189) < 50 && Math.Abs(heightMm - 841) < 50) return "A0";

                return "A3";
            }
            catch (Exception)
            {
                return "A3";
            }
        }

        public static ExportPlusProfile ConvertXMLToProfile(ExportPlusXMLProfile xmlProfile)
        {
            var profile = new ExportPlusProfile
            {
                ProfileName = xmlProfile.Name,
                OutputFolder = xmlProfile.FilePath ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                CreateSeparateFolders = xmlProfile.TemplateInfo.IsSeparateFile,
                HideCropRegions = xmlProfile.TemplateInfo.HideCropBoundaries,
                HideScopeboxes = xmlProfile.TemplateInfo.HideScopeBox,
                PaperSize = xmlProfile.TemplateInfo.PaperSize,
                SelectedFormats = new List<string>()
            };

            if (xmlProfile.TemplateInfo.IsPDFChecked) profile.SelectedFormats.Add("PDF");
            if (xmlProfile.TemplateInfo.IsDWGChecked) profile.SelectedFormats.Add("DWG");
            if (xmlProfile.TemplateInfo.IsIFCChecked) profile.SelectedFormats.Add("IFC");
            if (xmlProfile.TemplateInfo.IsIMGChecked) profile.SelectedFormats.Add("JPG");
            if (xmlProfile.TemplateInfo.IsNWCChecked) profile.SelectedFormats.Add("NWC");
            profile.SelectedFormats = ExportFormatSupport.FilterSupported(profile.SelectedFormats).ToList();

            return profile;
        }

        public static void ApplyXMLProfileToUI(ExportPlusXMLProfile xmlProfile,
            Action<string, object> setUIProperty)
        {
            if (xmlProfile == null || setUIProperty == null)
            {
                return;
            }

            var template = xmlProfile.TemplateInfo;

            try
            {
                setUIProperty("IsVectorProcessing", template.IsVectorProcessing);
                setUIProperty("RasterQuality", template.RasterQuality);
                setUIProperty("ColorMode", template.Color);
                setUIProperty("IsFitToPage", template.IsFitToPage);

                setUIProperty("IsCenter", template.IsCenter);
                setUIProperty("SelectedMarginType", template.SelectedMarginType);
                setUIProperty("PaperSize", template.PaperSize);

                setUIProperty("ViewLinksInBlue", template.ViewLink);
                setUIProperty("HideRefWorkPlanes", template.HidePlanes);
                setUIProperty("HideScopeboxes", template.HideScopeBox);
                setUIProperty("HideUnreferencedViewTags", template.HideUnreferencedTags);
                setUIProperty("HideCropBoundaries", template.HideCropBoundaries);
                setUIProperty("ReplaceHalftone", template.ReplaceHalftone);
                setUIProperty("MaskCoincidentLines", template.MaskCoincidentLines);
                setUIProperty("CompactDwgFiles", template.DWG_MergedViews);

                setUIProperty("CreateSeparateFiles", template.IsSeparateFile);
                setUIProperty("OutputFolder", template.FilePath ?? "");

                if (template.DWF != null)
                {
                    setUIProperty("DWF_ImageFormat", template.DWF.OptImageFormat);
                    setUIProperty("DWF_ImageQuality", template.DWF.OptImageQuality);
                    setUIProperty("DWF_ExportTextures", template.DWF.OptExportTextures);
                }

                if (template.NWC != null)
                {
                    setUIProperty("NWC_ConvertConstructionParts", template.NWC.ConvertConstructionParts);
                    setUIProperty("NWC_ConvertElementIds", template.NWC.ConvertElementIds);
                    setUIProperty("NWC_ConvertElementParameters", template.NWC.ConvertElementParameters);
                    setUIProperty("NWC_ConvertElementProperties", template.NWC.ConvertElementProperties);
                    setUIProperty("NWC_ConvertLinkedFiles", template.NWC.ConvertLinkedFiles);
                    setUIProperty("NWC_ConvertRoomAsAttribute", template.NWC.ConvertRoomAsAttribute);
                    setUIProperty("NWC_ConvertURLs", template.NWC.ConvertURLs);
                    setUIProperty("NWC_Coordinates", template.NWC.Coordinates);
                    setUIProperty("NWC_DivideFileIntoLevels", template.NWC.DivideFileIntoLevels);

                    setUIProperty("NWC_EmbedTextures", template.NWC.EmbedTextures);
                    setUIProperty("NWC_ExportScope", template.NWC.ExportScope);
                    setUIProperty("NWC_SeparateCustomProperties", template.NWC.SeparateCustomProperties);
                    setUIProperty("NWC_StrictSectioning", template.NWC.StrictSectioning);
                    setUIProperty("NWC_TypePropertiesOnElements", template.NWC.TypePropertiesOnElements);

                    setUIProperty("NWC_ExportRoomGeometry", template.NWC.ExportRoomGeometry);
                    setUIProperty("NWC_TryAndFindMissingMaterials", template.NWC.TryAndFindMissingMaterials);
                    setUIProperty("NWC_ConvertLinkedCADFormats", template.NWC.ConvertLinkedCADFormats);
                    setUIProperty("NWC_ConvertLights", template.NWC.ConvertLights);
                    setUIProperty("NWC_FacetingFactor", template.NWC.FacetingFactor);
                }

                if (template.IFC != null)
                {
                    setUIProperty("IFC_FileVersion", template.IFC.FileVersion);
                    setUIProperty("IFC_SpaceBoundaries", template.IFC.SpaceBoundaries);
                    setUIProperty("IFC_SitePlacement", template.IFC.SitePlacement);
                    setUIProperty("IFC_ExportBaseQuantities", template.IFC.ExportBaseQuantities);
                    setUIProperty("IFC_ExportIFCCommonPropertySets", template.IFC.ExportIFCCommonPropertySets);
                    setUIProperty("IFC_TessellationLevelOfDetail", template.IFC.TessellationLevelOfDetail);
                    setUIProperty("IFC_VisibleElementsOfCurrentView", template.IFC.VisibleElementsOfCurrentView);
                }

                if (template.IMG != null)
                {
                    setUIProperty("IMG_ImageResolution", template.IMG.ImageResolution);
                    setUIProperty("IMG_FileType", template.IMG.HLRandWFViewsFileType);
                    setUIProperty("IMG_ZoomType", template.IMG.ZoomType);
                    setUIProperty("IMG_PixelSize", template.IMG.PixelSize);
                }

                setUIProperty("IsPDFChecked", template.IsPDFChecked);
                setUIProperty("IsDWGChecked", template.IsDWGChecked);
                setUIProperty("IsDGNChecked", false);
                setUIProperty("IsIFCChecked", template.IsIFCChecked);
                setUIProperty("IsIMGChecked", template.IsIMGChecked);
                setUIProperty("IsNWCChecked", template.IsNWCChecked);
                setUIProperty("IsDWFChecked", false);

                if (template.SelectionSheets?.SelectedParamsVirtual?.SelectionParameters != null)
                {
                    var selectedParams = template.SelectionSheets.SelectedParamsVirtual.SelectionParameters
                        .Where(p => p.IsSelected)
                        .Select(p => p.DisplayName?.Trim())
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();

                    setUIProperty("CustomFileNameParameters", selectedParams);
                }

            }
            catch (Exception)
            {
            }
        }

        public static Dictionary<string, object> GetFormatSettings(ExportPlusXMLProfile xmlProfile, string format)
        {
            var settings = new Dictionary<string, object>();

            if (xmlProfile?.TemplateInfo == null) return settings;

            var template = xmlProfile.TemplateInfo;

            switch (format.ToUpper())
            {
                case "PDF":
                    settings["VectorProcessing"] = template.IsVectorProcessing;
                    settings["RasterQuality"] = template.RasterQuality;
                    settings["ColorMode"] = template.Color;
                    settings["FitToPage"] = template.IsFitToPage;
                    settings["IsCenter"] = template.IsCenter;
                    settings["MarginType"] = template.SelectedMarginType;
                    break;

                case "DWF":
                    if (template.DWF != null)
                    {
                        settings["IsDwfx"] = template.DWF.IsDwfx;
                        settings["ImageFormat"] = template.DWF.OptImageFormat;
                        settings["ImageQuality"] = template.DWF.OptImageQuality;
                        settings["ExportTextures"] = template.DWF.OptExportTextures;
                        settings["FitToPage"] = template.DWF.IsFitToPage;
                        settings["RasterQuality"] = template.DWF.RasterQuality;
                    }
                    break;

                case "NWC":
                    if (template.NWC != null)
                    {
                        settings["ConvertElementProperties"] = template.NWC.ConvertElementProperties;
                        settings["Coordinates"] = template.NWC.Coordinates;
                        settings["DivideFileIntoLevels"] = template.NWC.DivideFileIntoLevels;
                        settings["ExportElementIds"] = template.NWC.ExportElementIds;
                        settings["ExportParts"] = template.NWC.ExportParts;
                        settings["ExportRoomAsAttribute"] = template.NWC.ExportRoomAsAttribute;
                        settings["FacetingFactor"] = template.NWC.FacetingFactor;
                    }
                    break;

                case "IFC":
                    if (template.IFC != null)
                    {
                        settings["FileVersion"] = template.IFC.FileVersion;
                        settings["SpaceBoundaries"] = template.IFC.SpaceBoundaries;
                        settings["SitePlacement"] = template.IFC.SitePlacement;
                        settings["ExportBaseQuantities"] = template.IFC.ExportBaseQuantities;
                        settings["ExportIFCCommonPropertySets"] = template.IFC.ExportIFCCommonPropertySets;
                        settings["TessellationLevelOfDetail"] = template.IFC.TessellationLevelOfDetail;
                        settings["VisibleElementsOfCurrentView"] = template.IFC.VisibleElementsOfCurrentView;
                    }
                    break;

                case "IMG":
                case "JPG":
                case "PNG":
                    if (template.IMG != null)
                    {
                        settings["ImageResolution"] = template.IMG.ImageResolution;
                        settings["FileType"] = template.IMG.HLRandWFViewsFileType;
                        settings["ZoomType"] = template.IMG.ZoomType;
                        settings["PixelSize"] = template.IMG.PixelSize;
                    }
                    break;
            }

            return settings;
        }

        private static void WriteDebugLog(string message)
        {
            return;
        }
    }
}
