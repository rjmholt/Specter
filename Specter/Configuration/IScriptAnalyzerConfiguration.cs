using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Specter.Configuration
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RuleExecutionMode
    {
        [EnumMember(Value = "default")]
        Default = 0,

        [EnumMember(Value = "parallel")]
        Parallel = 1,

        [EnumMember(Value = "sequential")]
        Sequential = 2,
    }

    /// <summary>
    /// Controls whether Specter will load and execute external rule code.
    /// This is the central policy gate -- all external rule loading checks this first.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ExternalRulePolicy
    {
        /// <summary>
        /// External rules are loaded only when explicitly requested via -CustomRulePath,
        /// settings file RulePaths, or the builder API. This is the default.
        /// </summary>
        [EnumMember(Value = "explicit")]
        Explicit = 0,

        /// <summary>
        /// All external rule loading is disabled. -CustomRulePath and RulePaths are ignored.
        /// Use in environments where only built-in rules should run.
        /// </summary>
        [EnumMember(Value = "disabled")]
        Disabled = 1,

        /// <summary>
        /// External rules are loaded with relaxed validation (ownership checks skipped).
        /// Not recommended for production use.
        /// </summary>
        [EnumMember(Value = "unrestricted")]
        Unrestricted = 2,
    }

    public interface IScriptAnalyzerConfiguration
    {
        BuiltinRulePreference? BuiltinRules { get; }

        RuleExecutionMode? RuleExecution { get; }

        IReadOnlyList<string> RulePaths { get; }

        IReadOnlyDictionary<string, IRuleConfiguration?> RuleConfiguration { get; }

        ExternalRulePolicy ExternalRules { get; }
    }
}

