namespace Microsoft.PowerShell.ScriptAnalyzer.Formatting
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
    /// </summary>
    public interface IEditorConfiguration
    {
        CommonEditorConfiguration Common { get; }
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
