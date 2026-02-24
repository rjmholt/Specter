using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Specter.Utils
{
    /// <summary>
    /// Simple file glob matcher supporting * (single segment) and ** (cross-directory) wildcards.
    /// Matches against the full path or just the file name depending on pattern structure.
    /// </summary>
    internal static class GlobMatcher
    {
        private static readonly bool s_caseInsensitive =
#if CORECLR
            OperatingSystem.IsWindows();
#else
            Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif

        public static bool IsMatch(string path, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            string normalizedPath = NormalizePath(path);
            string normalizedPattern = NormalizePath(pattern);

            Regex regex = GlobToRegex(normalizedPattern);
            return regex.IsMatch(normalizedPath);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static Regex GlobToRegex(string pattern)
        {
            var sb = new StringBuilder(pattern.Length * 2);

            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];

                if (c == '*')
                {
                    if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                    {
                        // ** matches anything including path separators
                        sb.Append(".*");
                        i++;

                        // Skip a trailing / after **
                        if (i + 1 < pattern.Length && pattern[i + 1] == '/')
                        {
                            i++;
                        }
                    }
                    else
                    {
                        // * matches anything except /
                        sb.Append("[^/]*");
                    }
                }
                else if (c == '?')
                {
                    sb.Append("[^/]");
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }

            // Allow the pattern to match the end of the path (not just a full path)
            string regexPattern = sb.ToString();

            // If pattern doesn't start with / or **, allow matching anywhere in the path
            if (!pattern.StartsWith("/", StringComparison.Ordinal)
                && !pattern.StartsWith("*", StringComparison.Ordinal))
            {
                regexPattern = "(^|/)" + regexPattern;
            }

            regexPattern = regexPattern + "$";

            RegexOptions options = RegexOptions.Compiled;
            if (s_caseInsensitive)
            {
                options |= RegexOptions.IgnoreCase;
            }

            return new Regex(regexPattern, options);
        }
    }
}
