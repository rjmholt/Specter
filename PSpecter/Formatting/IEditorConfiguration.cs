using PSpecter.Configuration;

namespace PSpecter.Formatting
{
    /// <summary>
    /// Common configuration shared by all formatting editors.
    /// </summary>
    public class CommonEditorConfiguration
    {
        public static CommonEditorConfiguration Default { get; } = new CommonEditorConfiguration();

        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Base interface for editor-specific configuration types.
    /// Each editor defines its own configuration record implementing this interface.
    /// Also implements <see cref="IRuleConfiguration"/> so formatting editors can
    /// double as configurable analysis rules without a separate config type.
    /// </summary>
    public interface IEditorConfiguration : IRuleConfiguration
    {
        new CommonEditorConfiguration Common { get; }
    }

    /// <summary>
    /// Implemented by editors that accept typed configuration.
    /// The formatter infrastructure discovers the config type via this interface.
    /// </summary>
    public interface IConfigurableEditor<TConfiguration> where TConfiguration : IEditorConfiguration
    {
        TConfiguration Configuration { get; }
    }
}
