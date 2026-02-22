using System.Collections.Generic;
using System.Management.Automation.Language;

namespace PSpecter.Formatting
{
    /// <summary>
    /// A stateless formatting editor that examines a script snapshot and produces
    /// offset-based text edits. Implementations must not mutate any of their inputs.
    /// </summary>
    public interface IScriptEditor
    {
        /// <summary>
        /// Examine the script and return zero or more non-overlapping edits.
        /// Edits are specified as offset ranges into <paramref name="scriptContent"/>.
        /// </summary>
        /// <param name="scriptContent">The full text of the script.</param>
        /// <param name="ast">The parsed AST of the script.</param>
        /// <param name="tokens">The token stream (includes comments and whitespace).</param>
        /// <param name="filePath">The file path of the script, or null for in-memory scripts.</param>
        /// <returns>A list of non-overlapping edits sorted by offset.</returns>
        IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath);
    }
}
