using System;
using System.Collections.Generic;
using System.Numerics;

namespace AxarDB.Helpers
{
    public class UniversalComparer : IComparer<object>
    {
        public static readonly UniversalComparer Instance = new UniversalComparer();

        public int Compare(object x, object y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (x is DateTime dx && y is DateTime dy) return dx.CompareTo(dy);
            if (x is DateTimeOffset dox && y is DateTimeOffset doy) return dox.CompareTo(doy);
            if (x is TimeSpan tx && y is TimeSpan ty) return tx.CompareTo(ty);
            
            // Check if both are numeric
            if (IsNumeric(x) && IsNumeric(y))
            {
                // Try converting to decimal for high precision
                try
                {
                    decimal d1 = Convert.ToDecimal(x);
                    decimal d2 = Convert.ToDecimal(y);
                    return d1.CompareTo(d2);
                }
                catch
                {
                    // Fallback to double (e.g. for BigInteger or extremely large floats)
                    try
                    {
                        double d1 = Convert.ToDouble(x);
                        double d2 = Convert.ToDouble(y);
                        return d1.CompareTo(d2);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            if (x is string sx && y is string sy) return string.Compare(sx, sy, StringComparison.Ordinal);

            if (x is IComparable cx && x.GetType() == y.GetType()) return cx.CompareTo(y);

            // Cannot compare, treat as equal to avoid exceptions during max/min
            return 0;
        }

        private bool IsNumeric(object value)
        {
            return value is sbyte
                || value is byte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal
                || value is BigInteger;
        }
    }
}
