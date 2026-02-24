using System.Management.Automation.Language;

namespace Specter.Tools
{
    public static class ExtentExtensions
    {
        public static bool Contains(this IScriptExtent thisExtent, IScriptExtent thatExtent)
        {
            return thisExtent.StartOffset <= thatExtent.StartOffset
                && thisExtent.EndOffset >= thatExtent.EndOffset;
        }

        /// <summary>
        /// Returns true if the extent text is wrapped in parentheses.
        /// Used for clang-style implicit suppression of warnings.
        /// </summary>
        public static bool IsWrappedInParentheses(this IScriptExtent extent)
        {
            return extent.Text.StartsWith("(") && extent.Text.EndsWith(")");
        }
    }
}
