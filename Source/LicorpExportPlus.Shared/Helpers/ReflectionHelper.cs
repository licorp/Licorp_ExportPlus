using System;
using System.Linq;
using System.Reflection;

namespace LicorpExportPlus.Helpers
{
    public static class ReflectionHelper
    {
        public static bool TrySetProperty(object target, string propertyName, object value)
        {
            if (target == null) return false;

            try
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || !property.CanWrite)
                {
                    return false;
                }

                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object convertedValue = value;

                if (value != null && !targetType.IsInstanceOfType(value))
                {
                    if (targetType.IsEnum)
                    {
                        if (value is string enumName)
                        {
                            convertedValue = Enum.Parse(targetType, enumName, ignoreCase: true);
                        }
                        else
                        {
                            convertedValue = Enum.ToObject(targetType, value);
                        }
                    }
                    else
                    {
                        convertedValue = Convert.ChangeType(value, targetType);
                    }
                }

                property.SetValue(target, convertedValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetProperty<T>(object target, string propertyName, out T value)
        {
            value = default;

            if (target == null) return false;

            try
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || !property.CanRead)
                {
                    return false;
                }

                var propertyValue = property.GetValue(target);
                if (propertyValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static object TryGetEnumValue(Assembly assembly, string fullTypeName, string valueName)
        {
            if (assembly == null || string.IsNullOrWhiteSpace(fullTypeName) || string.IsNullOrWhiteSpace(valueName))
            {
                return null;
            }

            try
            {
                var enumType = assembly.GetType(fullTypeName);
                if (enumType == null || !enumType.IsEnum)
                {
                    return null;
                }

                return Enum.Parse(enumType, valueName);
            }
            catch
            {
                return null;
            }
        }

        public static object TryGetEnumValueByShortName(Assembly assembly, string shortTypeName, string valueName)
        {
            if (assembly == null || string.IsNullOrWhiteSpace(shortTypeName) || string.IsNullOrWhiteSpace(valueName))
            {
                return null;
            }

            try
            {
                var enumType = assembly.GetTypes().FirstOrDefault(t => t.IsEnum && t.Name == shortTypeName);
                if (enumType == null)
                {
                    return null;
                }

                return Enum.Parse(enumType, valueName);
            }
            catch
            {
                return null;
            }
        }
    }
}
