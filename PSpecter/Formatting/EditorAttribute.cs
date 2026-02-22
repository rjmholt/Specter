using System;

namespace PSpecter.Formatting
{
    /// <summary>
    /// Declares metadata for a formatting editor.
    /// Used by the formatter infrastructure to discover and identify editors.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EditorAttribute : Attribute
    {
        public EditorAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// The unique name of this editor (e.g. "PlaceOpenBrace").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// A human-readable description of what this editor does.
        /// </summary>
        public string? Description { get; set; }
    }
}
