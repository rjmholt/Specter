using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Specter.Formatting
{
    /// <summary>
    /// Lightweight metadata about a formatting editor, parsed from <see cref="EditorAttribute"/>.
    /// </summary>
    public sealed class EditorInfo
    {
        private static readonly ConcurrentDictionary<Type, EditorInfo?> s_cache = new ConcurrentDictionary<Type, EditorInfo?>();

        private EditorInfo(string name, string? description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }

        public string? Description { get; }

        /// <summary>
        /// Attempts to read <see cref="EditorAttribute"/> from the given type and build an <see cref="EditorInfo"/>.
        /// </summary>
        public static bool TryGetFromEditorType(Type editorType, out EditorInfo? editorInfo)
        {
            if (s_cache.TryGetValue(editorType, out editorInfo))
            {
                return editorInfo != null;
            }

            var attr = editorType.GetCustomAttribute<EditorAttribute>();
            if (attr is null)
            {
                s_cache.TryAdd(editorType, null);
                editorInfo = null;
                return false;
            }

            editorInfo = new EditorInfo(attr.Name, attr.Description);
            s_cache.TryAdd(editorType, editorInfo);
            return true;
        }

        /// <summary>
        /// Gets the <see cref="IEditorConfiguration"/> type for an editor that implements
        /// <see cref="IConfigurableEditor{T}"/>, or null if the editor is not configurable.
        /// </summary>
        public static Type? GetConfigurationType(Type editorType)
        {
            foreach (Type iface in editorType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IConfigurableEditor<>))
                {
                    return iface.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
