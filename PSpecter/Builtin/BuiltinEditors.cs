using System;
using System.Collections.Generic;
using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using PSpecter.Internal;

namespace PSpecter.Builtin
{
    /// <summary>
    /// Registry of built-in formatting editors.
    /// The list order defines the default execution order for the formatter.
    /// </summary>
    public static class BuiltinEditors
    {
        /// <summary>
        /// Built-in editor types in their canonical execution order.
        /// </summary>
        public static IReadOnlyList<Type> DefaultEditors { get; } = new[]
        {
            typeof(PlaceCloseBraceEditor),
            typeof(PlaceOpenBraceEditor),
            typeof(UseConsistentWhitespaceEditor),
            typeof(UseConsistentIndentationEditor),
            typeof(AlignAssignmentStatementEditor),
            typeof(UseCorrectCasingEditor),
            typeof(AvoidTrailingWhitespaceEditor),
            typeof(AvoidSemicolonsAsLineTerminatorsEditor),
            typeof(AvoidUsingDoubleQuotesForConstantStringEditor),
        };

        /// <summary>
        /// Default configuration for all built-in editors (all enabled with default settings).
        /// </summary>
        public static IReadOnlyDictionary<string, IEditorConfiguration> DefaultConfigurations { get; } =
            new Dictionary<string, IEditorConfiguration>(StringComparer.OrdinalIgnoreCase)
            {
                ["PlaceCloseBrace"] = new PlaceCloseBraceEditorConfiguration(),
                ["PlaceOpenBrace"] = new PlaceOpenBraceEditorConfiguration(),
                ["UseConsistentWhitespace"] = new UseConsistentWhitespaceEditorConfiguration(),
                ["UseConsistentIndentation"] = new UseConsistentIndentationEditorConfiguration(),
                ["AlignAssignmentStatement"] = new AlignAssignmentStatementEditorConfiguration(),
                ["UseCorrectCasing"] = new UseCorrectCasingEditorConfiguration(),
                ["AvoidTrailingWhitespace"] = new AvoidTrailingWhitespaceEditorConfiguration(),
                ["AvoidSemicolonsAsLineTerminators"] = new AvoidSemicolonsAsLineTerminatorsEditorConfiguration(),
                ["AvoidUsingDoubleQuotesForConstantString"] = new AvoidUsingDoubleQuotesForConstantStringEditorConfiguration(),
            };
    }

    /// <summary>
    /// Well-known formatting presets. Presets are config dictionaries that override
    /// specific editor configurations from the default.
    /// </summary>
    public static class FormatterPresets
    {
        /// <summary>
        /// Default preset: K&amp;R / Stroustrup brace style.
        /// </summary>
        public static IReadOnlyDictionary<string, IEditorConfiguration> Default
            => BuiltinEditors.DefaultConfigurations;

        /// <summary>
        /// Stroustrup preset: identical to Default.
        /// </summary>
        public static IReadOnlyDictionary<string, IEditorConfiguration> Stroustrup
            => Default;

        /// <summary>
        /// OTBS preset: open braces on same line, branches cuddled (} else {).
        /// </summary>
        public static IReadOnlyDictionary<string, IEditorConfiguration> OTBS { get; } = CreateOTBS();

        /// <summary>
        /// Allman preset: open braces on their own line, close braces followed by newline.
        /// </summary>
        public static IReadOnlyDictionary<string, IEditorConfiguration> Allman { get; } = CreateAllman();

        private static IReadOnlyDictionary<string, IEditorConfiguration> CreateOTBS()
        {
            var configs = Polyfill.CopyDictionary(BuiltinEditors.DefaultConfigurations, StringComparer.OrdinalIgnoreCase);
            configs["PlaceCloseBrace"] = new PlaceCloseBraceEditorConfiguration { NewLineAfter = false };
            return configs;
        }

        private static IReadOnlyDictionary<string, IEditorConfiguration> CreateAllman()
        {
            var configs = Polyfill.CopyDictionary(BuiltinEditors.DefaultConfigurations, StringComparer.OrdinalIgnoreCase);
            configs["PlaceOpenBrace"] = new PlaceOpenBraceEditorConfiguration { OnSameLine = false };
            return configs;
        }
    }
}
