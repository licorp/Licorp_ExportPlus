using Autodesk.Revit.DB;

namespace LicorpExportPlus.Utils
{
    public static class ElementIdExtensions
    {
        public static long GetIdValue(this ElementId id)
        {
#if REVIT2025_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static string GetIdValueString(this ElementId id)
        {
            return id.GetIdValue().ToString();
        }
    }
}
