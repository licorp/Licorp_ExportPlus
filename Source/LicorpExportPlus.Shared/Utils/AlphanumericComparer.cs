using System;
using System.Collections.Generic;

namespace LicorpExportPlus.Utils
{
    /// <summary>
    /// Custom comparer for alphanumeric sorting: numbers first (numerically), then alphabetically
    /// Examples: 1, 2, 10, 100, A, B, Z, P100, P101, P200
    /// </summary>
    public class AlphanumericComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int xIndex = 0, yIndex = 0;

            while (xIndex < x.Length && yIndex < y.Length)
            {
                // Check if both current characters are digits
                bool xIsDigit = char.IsDigit(x[xIndex]);
                bool yIsDigit = char.IsDigit(y[yIndex]);

                if (xIsDigit && yIsDigit)
                {
                    // Extract numeric parts
                    string xNum = ExtractNumber(x, ref xIndex);
                    string yNum = ExtractNumber(y, ref yIndex);

                    // Parse and compare numerically
                    if (int.TryParse(xNum, out int xInt) && int.TryParse(yNum, out int yInt))
                    {
                        int numCompare = xInt.CompareTo(yInt);
                        if (numCompare != 0) return numCompare;
                    }
                }
                else if (xIsDigit && !yIsDigit)
                {
                    // Numbers come before letters
                    return -1;
                }
                else if (!xIsDigit && yIsDigit)
                {
                    // Letters come after numbers
                    return 1;
                }
                else
                {
                    // Both are non-digits, compare alphabetically (case-insensitive)
                    int charCompare = string.Compare(x[xIndex].ToString(), y[yIndex].ToString(),
                        StringComparison.OrdinalIgnoreCase);
                    if (charCompare != 0) return charCompare;

                    xIndex++;
                    yIndex++;
                }
            }

            // If one string is a prefix of the other
            return x.Length.CompareTo(y.Length);
        }

        private string ExtractNumber(string str, ref int index)
        {
            int startIndex = index;
            while (index < str.Length && char.IsDigit(str[index]))
            {
                index++;
            }
            return str.Substring(startIndex, index - startIndex);
        }
    }
}
