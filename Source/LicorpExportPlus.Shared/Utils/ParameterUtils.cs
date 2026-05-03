using Autodesk.Revit.DB;

namespace LicorpExportPlus.Utils
{
    public static class ParameterUtils
    {
        public static string GetParameterValue(Element element, BuiltInParameter paramName)
        {
            Parameter param = element.get_Parameter(paramName);
            return GetParameterValueAsString(param);
        }

        public static string GetParameterValue(Element element, string paramName)
        {
            Parameter param = element.LookupParameter(paramName);
            return GetParameterValueAsString(param);
        }

        public static string GetParameterValueAsString(Parameter param)
        {
            if (param == null || !param.HasValue)
                return "";

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString() ?? "";

                case StorageType.Integer:
                    // Check if parameter is Yes/No type by testing the value
                    var intVal = param.AsInteger();
                    if (intVal == 0 || intVal == 1)
                    {
                        return intVal == 1 ? "Yes" : "No";
                    }
                    return intVal.ToString();

                case StorageType.Double:
                    return param.AsValueString() ?? param.AsDouble().ToString("F2");

                case StorageType.ElementId:
                    ElementId elementId = param.AsElementId();
                    if (elementId == ElementId.InvalidElementId)
                        return "";

            Document doc = param.Element.Document;
            Element element = doc.GetElement(elementId);
            return element?.Name ?? elementId.GetIdValueString();

                default:
                    return "";
            }
        }

        public static T GetParameterValue<T>(Element element, string paramName, T defaultValue = default(T))
        {
            Parameter param = element.LookupParameter(paramName);
            if (param == null || !param.HasValue)
                return defaultValue;

            try
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)GetParameterValueAsString(param);
                }
                else if (typeof(T) == typeof(int))
                {
                    return (T)(object)param.AsInteger();
                }
                else if (typeof(T) == typeof(double))
                {
                    return (T)(object)param.AsDouble();
                }
                else if (typeof(T) == typeof(bool))
                {
                    return (T)(object)(param.AsInteger() == 1);
                }
                else if (typeof(T) == typeof(ElementId))
                {
                    return (T)(object)param.AsElementId();
                }

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public static bool SetParameterValue(Element element, string paramName, object value)
        {
            Parameter param = element.LookupParameter(paramName);
            if (param == null || param.IsReadOnly)
                return false;

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        return param.Set(value?.ToString() ?? "");

                    case StorageType.Integer:
                        if (value is bool boolValue)
                        {
                            return param.Set(boolValue ? 1 : 0);
                        }
                        else if (value is int intValue)
                        {
                            return param.Set(intValue);
                        }
                        else if (int.TryParse(value?.ToString(), out int parsedInt))
                        {
                            return param.Set(parsedInt);
                        }
                        break;

                    case StorageType.Double:
                        if (value is double doubleValue)
                        {
                            return param.Set(doubleValue);
                        }
                        else if (double.TryParse(value?.ToString(), out double parsedDouble))
                        {
                            return param.Set(parsedDouble);
                        }
                        break;

                    case StorageType.ElementId:
                        if (value is ElementId elementIdValue)
                        {
                            return param.Set(elementIdValue);
                        }
                        else if (value is int idInt)
                        {
#if REVIT2024_OR_GREATER
                            return param.Set(new ElementId((long)idInt));
#else
                            return param.Set(new ElementId(idInt));
#endif
                        }
                        break;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool HasParameter(Element element, string paramName)
        {
            Parameter param = element.LookupParameter(paramName);
            return param != null;
        }

        public static bool IsParameterEmpty(Element element, string paramName)
        {
            Parameter param = element.LookupParameter(paramName);
            return param == null || !param.HasValue || string.IsNullOrEmpty(GetParameterValueAsString(param));
        }

        public static System.Collections.Generic.Dictionary<string, string> GetAllParameters(Element element)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, string>();

            foreach (Parameter param in element.Parameters)
            {
                if (param.Definition != null && !string.IsNullOrEmpty(param.Definition.Name))
                {
                    string value = GetParameterValueAsString(param);
                    parameters[param.Definition.Name] = value;
                }
            }

            return parameters;
        }

        public static System.Collections.Generic.List<string> GetParameterNames(Element element, bool includeBuiltIn = false)
        {
            var parameterNames = new System.Collections.Generic.List<string>();

            foreach (Parameter param in element.Parameters)
            {
                if (param.Definition != null && !string.IsNullOrEmpty(param.Definition.Name))
                {
                    // Bỏ qua built-in parameters nếu không yêu cầu
                    if (!includeBuiltIn && param.IsShared == false && param.Definition is InternalDefinition)
                        continue;

                    parameterNames.Add(param.Definition.Name);
                }
            }

            parameterNames.Sort();
            return parameterNames;
        }
    }
}
