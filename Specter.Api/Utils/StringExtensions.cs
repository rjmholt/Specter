using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Specter.Utils
{
    internal static class StringExtensions
    {
        public static bool CaseInsensitiveEquals(this string s, string other)
        {
            return string.Equals(s, other, StringComparison.OrdinalIgnoreCase);
        }
    }
}
