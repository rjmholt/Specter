using System;
using System.Collections;
using System.Management.Automation.Language;
using PSpecter.Configuration.Psd;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer
{
    /// <summary>
    /// Compatibility shim providing static helper methods that the original
    /// PSScriptAnalyzer exposed on its Helper class.
    /// Delegates to the engine's <see cref="PsdDataParser"/> for AST evaluation.
    /// </summary>
    public static class Helper
    {
        private static readonly PsdDataParser s_parser = new PsdDataParser();

        /// <summary>
        /// Safely evaluates a PowerShell expression AST to a .NET value,
        /// provided the expression is statically evaluable (constants, arrays, hashtables, $true/$false/$null).
        /// </summary>
        public static object? GetSafeValueFromExpressionAst(ExpressionAst exprAst)
        {
            if (exprAst == null)
            {
                throw new ArgumentNullException(nameof(exprAst));
            }

            return s_parser.ConvertAstValue(exprAst);
        }

        internal static Hashtable? GetSafeValueFromHashtableAst(HashtableAst hashtableAst)
        {
            return s_parser.ConvertAstValue(hashtableAst);
        }
    }
}
