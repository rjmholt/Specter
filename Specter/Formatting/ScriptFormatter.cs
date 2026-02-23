using System;
using System.Collections.Generic;
using System.Reflection;
using Specter.Builtin;
using Specter.Rules;

namespace Specter.Formatting
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
        public static ScriptFormatter FromEditorConfigs(
            IReadOnlyDictionary<string, IEditorConfiguration> configs,
            IReadOnlyDictionary<Type, object>? services = null)
        {
            if (configs is null) { throw new ArgumentNullException(nameof(configs)); }

            return FromEditorConfigs(BuiltinEditors.DefaultEditors, configs, services);
        }

        /// <summary>
        /// Creates a formatter from a config dictionary and an explicit list of editor types.
        /// The list order determines execution order. Editors whose config has
        /// <see cref="CommonEditorConfiguration.Enabled"/> = false are skipped.
        /// Editors without a matching config entry are skipped.
        /// </summary>
        public static ScriptFormatter FromEditorConfigs(
            IReadOnlyList<Type> editorTypes,
            IReadOnlyDictionary<string, IEditorConfiguration> configs,
            IReadOnlyDictionary<Type, object>? services = null)
        {
            if (editorTypes is null) { throw new ArgumentNullException(nameof(editorTypes)); }
            if (configs is null) { throw new ArgumentNullException(nameof(configs)); }

            var builder = new Builder();
            if (services is not null)
            {
                foreach (var kvp in services)
                {
                    builder.AddService(kvp.Key, kvp.Value);
                }
            }
            builder.AddConfiguredEditors(editorTypes, configs);
            return builder.Build();
        }

        /// <summary>
        /// Format a script string, returning the formatted result.
        /// </summary>
        public string Format(string scriptContent, string? filePath = null)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var buffer = ScriptFormatBuffer.FromScript(scriptContent, filePath);

            foreach (IScriptEditor editor in _editors)
            {
                var edits = editor.GetEdits(buffer.Content, buffer.Ast, buffer.Tokens, filePath ?? string.Empty);
                buffer.ApplyEdits(edits);
            }

            return buffer.ToString();
        }

        public sealed class Builder
        {
            private readonly List<IScriptEditor> _editors = new List<IScriptEditor>();
            private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

            public Builder AddEditor(IScriptEditor editor)
            {
                if (editor is null) { throw new ArgumentNullException(nameof(editor)); }
                _editors.Add(editor);
                return this;
            }

            /// <summary>
            /// Registers a service instance that can be injected into editor constructors.
            /// </summary>
            public Builder AddService(Type serviceType, object instance)
            {
                _services[serviceType] = instance;
                return this;
            }

            /// <summary>
            /// Registers a service instance that can be injected into editor constructors.
            /// </summary>
            public Builder AddService<T>(T instance)
            {
                if (instance is null) { throw new ArgumentNullException(nameof(instance)); }
                _services[typeof(T)] = instance;
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
                if (editorTypes is null) { throw new ArgumentNullException(nameof(editorTypes)); }
                if (configs is null) { throw new ArgumentNullException(nameof(configs)); }

                foreach (Type editorType in editorTypes)
                {
                    if (!EditorInfo.TryGetFromEditorType(editorType, out EditorInfo? info) || info is null)
                    {
                        continue;
                    }

                    if (!configs.TryGetValue(info.Name, out IEditorConfiguration? editorConfig) || editorConfig is null)
                    {
                        continue;
                    }

                    if (editorConfig.Common is not null && !editorConfig.Common.Enabled)
                    {
                        continue;
                    }

                    IScriptEditor? editor = CreateEditorInstance(editorType, editorConfig);
                    if (editor is not null)
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
                if (rules is null) { throw new ArgumentNullException(nameof(rules)); }

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

            private IScriptEditor? CreateEditorInstance(Type editorType, IEditorConfiguration config)
            {
                Type? configType = EditorInfo.GetConfigurationType(editorType);

                // Try constructor with config + services
                if (configType is not null)
                {
                    foreach (ConstructorInfo ctor in editorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        ParameterInfo[] parameters = ctor.GetParameters();
                        if (parameters.Length == 0)
                        {
                            continue;
                        }

                        object[]? args = TryResolveConstructorArgs(parameters, configType, config);
                        if (args is not null)
                        {
                            return (IScriptEditor)ctor.Invoke(args);
                        }
                    }
                }

                // Fall back to parameterless constructor
                ConstructorInfo? defaultCtor = editorType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, Type.EmptyTypes, modifiers: null);
                if (defaultCtor is not null)
                {
                    return (IScriptEditor)defaultCtor.Invoke(null);
                }

                return null;
            }

            private object[]? TryResolveConstructorArgs(
                ParameterInfo[] parameters,
                Type configType,
                IEditorConfiguration config)
            {
                var args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type paramType = parameters[i].ParameterType;
                    if (paramType.IsAssignableFrom(configType))
                    {
                        args[i] = config;
                    }
                    else if (_services.TryGetValue(paramType, out object? service) && service is not null)
                    {
                        args[i] = service;
                    }
                    else
                    {
                        return null;
                    }
                }
                return args;
            }
        }
    }
}
