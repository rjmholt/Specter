using System;
using System.Collections.Generic;
using System.Reflection;
using PSpecter.Builtin;
using PSpecter.Rules;

namespace PSpecter.Formatting
{
    /// <summary>
    /// Orchestrates a sequence of <see cref="IScriptEditor"/>s over a script,
    /// applying each editor's edits in order and reparsing between passes.
    /// </summary>
    public sealed class ScriptFormatter
    {
        private readonly IReadOnlyList<IScriptEditor> _editors;

        private ScriptFormatter(IReadOnlyList<IScriptEditor> editors)
        {
            _editors = editors;
        }

        /// <summary>
        /// Creates a formatter from a config dictionary, using the built-in editor types
        /// in their canonical execution order. Editors whose config has
        /// <see cref="CommonEditorConfiguration.Enabled"/> = false are skipped.
        /// </summary>
        public static ScriptFormatter FromEditorConfigs(IReadOnlyDictionary<string, IEditorConfiguration> configs)
        {
            if (configs is null) throw new ArgumentNullException(nameof(configs));

            return FromEditorConfigs(BuiltinEditors.DefaultEditors, configs);
        }

        /// <summary>
        /// Creates a formatter from a config dictionary and an explicit list of editor types.
        /// The list order determines execution order. Editors whose config has
        /// <see cref="CommonEditorConfiguration.Enabled"/> = false are skipped.
        /// Editors without a matching config entry are skipped.
        /// </summary>
        public static ScriptFormatter FromEditorConfigs(
            IReadOnlyList<Type> editorTypes,
            IReadOnlyDictionary<string, IEditorConfiguration> configs)
        {
            if (editorTypes is null) throw new ArgumentNullException(nameof(editorTypes));
            if (configs is null) throw new ArgumentNullException(nameof(configs));

            var builder = new Builder();
            builder.AddConfiguredEditors(editorTypes, configs);
            return builder.Build();
        }

        /// <summary>
        /// Format a script string, returning the formatted result.
        /// </summary>
        public string Format(string scriptContent, string filePath = null)
        {
            if (scriptContent is null) throw new ArgumentNullException(nameof(scriptContent));

            var buffer = ScriptFormatBuffer.FromScript(scriptContent, filePath);

            foreach (IScriptEditor editor in _editors)
            {
                var edits = editor.GetEdits(buffer.Content, buffer.Ast, buffer.Tokens, filePath);
                buffer.ApplyEdits(edits);
            }

            return buffer.ToString();
        }

        public sealed class Builder
        {
            private readonly List<IScriptEditor> _editors = new List<IScriptEditor>();

            public Builder AddEditor(IScriptEditor editor)
            {
                if (editor is null) throw new ArgumentNullException(nameof(editor));
                _editors.Add(editor);
                return this;
            }

            /// <summary>
            /// Discovers and instantiates editors from the given types, configuring each
            /// with its matching entry from the configs dictionary. Editors are added in
            /// the order they appear in <paramref name="editorTypes"/>.
            /// </summary>
            public Builder AddConfiguredEditors(
                IReadOnlyList<Type> editorTypes,
                IReadOnlyDictionary<string, IEditorConfiguration> configs)
            {
                if (editorTypes is null) throw new ArgumentNullException(nameof(editorTypes));
                if (configs is null) throw new ArgumentNullException(nameof(configs));

                foreach (Type editorType in editorTypes)
                {
                    if (!EditorInfo.TryGetFromEditorType(editorType, out EditorInfo info))
                    {
                        continue;
                    }

                    if (!configs.TryGetValue(info.Name, out IEditorConfiguration editorConfig))
                    {
                        continue;
                    }

                    if (editorConfig.Common != null && !editorConfig.Common.Enabled)
                    {
                        continue;
                    }

                    IScriptEditor editor = CreateEditorInstance(editorType, editorConfig);
                    if (editor != null)
                    {
                        _editors.Add(editor);
                    }
                }

                return this;
            }

            /// <summary>
            /// Discovers editors from rules that implement <see cref="IFormattingRule"/>
            /// and adds them in the order the rules are provided.
            /// </summary>
            public Builder AddEditorsFromRules(IEnumerable<ScriptRule> rules)
            {
                if (rules is null) throw new ArgumentNullException(nameof(rules));

                foreach (ScriptRule rule in rules)
                {
                    if (rule is IFormattingRule formattingRule)
                    {
                        _editors.Add(formattingRule.CreateEditor());
                    }
                }

                return this;
            }

            public ScriptFormatter Build()
            {
                return new ScriptFormatter(new List<IScriptEditor>(_editors));
            }

            private static IScriptEditor CreateEditorInstance(Type editorType, IEditorConfiguration config)
            {
                // Look for a constructor that takes the config type
                Type configType = EditorInfo.GetConfigurationType(editorType);
                if (configType != null)
                {
                    ConstructorInfo ctor = editorType.GetConstructor(new[] { configType });
                    if (ctor != null)
                    {
                        return (IScriptEditor)ctor.Invoke(new object[] { config });
                    }
                }

                // Fall back to parameterless constructor
                ConstructorInfo defaultCtor = editorType.GetConstructor(Type.EmptyTypes);
                if (defaultCtor != null)
                {
                    return (IScriptEditor)defaultCtor.Invoke(null);
                }

                return null;
            }
        }
    }
}
