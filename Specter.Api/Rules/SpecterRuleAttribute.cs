using System;

namespace Specter.Rules
{
    /// <summary>
    /// Attribute for PowerShell functions that implement Specter rules.
    /// Applied to function ScriptBlocks to mark them for rule discovery.
    /// This is a standalone attribute (not derived from RuleAttribute) because
    /// RuleAttribute is sealed and targeted at C# classes. The discovery layer
    /// maps this attribute to a RuleInfo via RuleInfo.TryGetFromFunctionInfo().
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    public sealed class SpecterRuleAttribute : ScriptAnalyzerAttribute
    {
        public SpecterRuleAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }

        public string Description { get; }

        public DiagnosticSeverity Severity { get; set; } = DiagnosticSeverity.Warning;

        public string? Namespace { get; set; }
    }
}
