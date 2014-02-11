using System;

namespace Conveyor.NDeproxy
{
    public static class StringHelper
    {
        public static bool EqualsIgnoreCase(this string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}

