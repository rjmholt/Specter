using System;

namespace PSpecter.Rules
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class RuleCollectionAttribute : ScriptAnalyzerAttribute
    {
        public string? Name { get; set; }
    }
}
