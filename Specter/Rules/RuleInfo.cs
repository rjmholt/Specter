using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;
using System.Reflection;

namespace Specter.Rules
{
    public class RuleInfo
    {
        private static readonly ConcurrentDictionary<RuleAttribute, RuleInfo> s_ruleInfoCache = new ConcurrentDictionary<RuleAttribute, RuleInfo>();
        private static readonly string s_rulesNamespace = typeof(RuleAttribute).Namespace ?? "Specter.Rules";
        private static readonly string s_ruleAttributeFullName = $"{s_rulesNamespace}.{nameof(RuleAttribute)}";
        private static readonly string s_specterRuleAttributeFullName = $"{s_rulesNamespace}.{nameof(SpecterRuleAttribute)}";
        private static readonly string s_threadsafeRuleAttributeFullName = $"{s_rulesNamespace}.{nameof(ThreadsafeRuleAttribute)}";
        private static readonly string s_idempotentRuleAttributeFullName = $"{s_rulesNamespace}.{nameof(IdempotentRuleAttribute)}";
        private static readonly string s_ruleCollectionAttributeFullName = $"{s_rulesNamespace}.{nameof(RuleCollectionAttribute)}";

        public static bool TryGetFromRuleType(Type ruleType, out RuleInfo? ruleInfo)
        {
            return TryGetFromRuleType(ruleType, SourceType.Assembly, out ruleInfo);
        }

        internal static bool TryGetBuiltinRule(Type ruleType, out RuleInfo? ruleInfo)
        {
            return TryGetFromRuleType(ruleType, SourceType.Builtin, out ruleInfo);
        }

        internal static bool TryGetFromRuleType(Type ruleType, SourceType source, out RuleInfo? ruleInfo)
        {
            string defaultRuleCollectionName = GetRuleCollectionName(ruleType.Assembly)
                ?? ruleType.Assembly.GetName().Name
                ?? string.Empty;

            // Use CustomAttributeData so rule discovery is ALC-agnostic.
            return TryGetFromCustomAttributeDataList(ruleType.CustomAttributes, source, defaultRuleCollectionName, out ruleInfo);
        }

        public static bool TryGetFromFunctionInfo(FunctionInfo functionInfo, out RuleInfo? ruleInfo)
        {
            return TryGetFromAttributeList(functionInfo.ScriptBlock.Attributes, SourceType.PowerShellModule, functionInfo.ModuleName ?? string.Empty, out ruleInfo);
        }

        private static bool TryGetFromAttributeList(IEnumerable<Attribute> attributes, SourceType source, string ruleCollectionName, out RuleInfo? ruleInfo)
        {
            RuleAttribute? ruleAttribute = null;
            SpecterRuleAttribute? specterRuleAttribute = null;
            ThreadsafeRuleAttribute? threadsafeAttribute = null;
            IdempotentRuleAttribute? idempotentAttribute = null;
            foreach (Attribute attribute in attributes)
            {
                switch (attribute)
                {
                    case RuleAttribute ruleAttr:
                        ruleAttribute = ruleAttr;
                        continue;

                    case SpecterRuleAttribute specterAttr:
                        specterRuleAttribute = specterAttr;
                        continue;

                    case ThreadsafeRuleAttribute tsAttr:
                        threadsafeAttribute = tsAttr;
                        continue;

                    case IdempotentRuleAttribute idempotentAttr:
                        idempotentAttribute = idempotentAttr;
                        continue;
                }
            }

            if (ruleAttribute is not null)
            {
                string ruleNamespace = ruleAttribute.Namespace ?? ruleCollectionName;
                ruleInfo = new RuleInfo(ruleAttribute.Name, ruleNamespace)
                {
                    DefaultSeverity = ruleAttribute.Severity,
                    Description = ruleAttribute.Description,
                    Source = source,
                    IsIdempotent = idempotentAttribute != null,
                    IsThreadsafe = threadsafeAttribute != null,
                };
                return true;
            }

            if (specterRuleAttribute is not null)
            {
                string ruleNamespace = specterRuleAttribute.Namespace ?? ruleCollectionName;
                ruleInfo = new RuleInfo(specterRuleAttribute.Name, ruleNamespace)
                {
                    DefaultSeverity = specterRuleAttribute.Severity,
                    Description = specterRuleAttribute.Description,
                    Source = source == SourceType.Assembly ? SourceType.PowerShellModule : source,
                    IsIdempotent = idempotentAttribute != null,
                    IsThreadsafe = threadsafeAttribute != null,
                };
                return true;
            }

            ruleInfo = null;
            return false;
        }

        private static bool TryGetFromCustomAttributeDataList(
            IEnumerable<CustomAttributeData> attributes,
            SourceType source,
            string ruleCollectionName,
            out RuleInfo? ruleInfo)
        {
            string? ruleName = null;
            string? ruleDescription = null;
            DiagnosticSeverity ruleSeverity = DiagnosticSeverity.Warning;
            string? ruleNamespace = null;
            bool hasRuleAttribute = false;

            string? specterRuleName = null;
            string? specterRuleDescription = null;
            DiagnosticSeverity specterRuleSeverity = DiagnosticSeverity.Warning;
            string? specterRuleNamespace = null;
            bool hasSpecterRuleAttribute = false;

            bool isThreadsafe = false;
            bool isIdempotent = false;

            foreach (CustomAttributeData attribute in attributes)
            {
                string? fullName = attribute.AttributeType.FullName;
                if (fullName is null)
                {
                    continue;
                }

                if (fullName == s_ruleAttributeFullName)
                {
                    if (TryReadRuleAttributeData(attribute, out ruleName, out ruleDescription, out ruleSeverity, out ruleNamespace))
                    {
                        hasRuleAttribute = true;
                    }

                    continue;
                }

                if (fullName == s_specterRuleAttributeFullName)
                {
                    if (TryReadRuleAttributeData(attribute, out specterRuleName, out specterRuleDescription, out specterRuleSeverity, out specterRuleNamespace))
                    {
                        hasSpecterRuleAttribute = true;
                    }

                    continue;
                }

                if (fullName == s_threadsafeRuleAttributeFullName)
                {
                    isThreadsafe = true;
                    continue;
                }

                if (fullName == s_idempotentRuleAttributeFullName)
                {
                    isIdempotent = true;
                    continue;
                }
            }

            if (hasRuleAttribute && !string.IsNullOrEmpty(ruleName))
            {
                string finalNamespace = ruleNamespace ?? ruleCollectionName;
                ruleInfo = new RuleInfo(ruleName!, finalNamespace)
                {
                    DefaultSeverity = ruleSeverity,
                    Description = ruleDescription,
                    Source = source,
                    IsIdempotent = isIdempotent,
                    IsThreadsafe = isThreadsafe,
                };
                return true;
            }

            if (hasSpecterRuleAttribute && !string.IsNullOrEmpty(specterRuleName))
            {
                string finalNamespace = specterRuleNamespace ?? ruleCollectionName;
                ruleInfo = new RuleInfo(specterRuleName!, finalNamespace)
                {
                    DefaultSeverity = specterRuleSeverity,
                    Description = specterRuleDescription,
                    Source = source == SourceType.Assembly ? SourceType.PowerShellModule : source,
                    IsIdempotent = isIdempotent,
                    IsThreadsafe = isThreadsafe,
                };
                return true;
            }

            ruleInfo = null;
            return false;
        }

        private static string? GetRuleCollectionName(Assembly assembly)
        {
            foreach (CustomAttributeData attribute in assembly.CustomAttributes)
            {
                if (attribute.AttributeType.FullName != s_ruleCollectionAttributeFullName)
                {
                    continue;
                }

                for (int i = 0; i < attribute.NamedArguments.Count; i++)
                {
                    CustomAttributeNamedArgument namedArg = attribute.NamedArguments[i];
                    if (namedArg.MemberName == "Name" && namedArg.TypedValue.Value is string namedValue)
                    {
                        return namedValue;
                    }
                }

                if (attribute.ConstructorArguments.Count > 0 && attribute.ConstructorArguments[0].Value is string ctorValue)
                {
                    return ctorValue;
                }
            }

            return null;
        }

        private static bool TryReadRuleAttributeData(
            CustomAttributeData attribute,
            out string? name,
            out string? description,
            out DiagnosticSeverity severity,
            out string? ruleNamespace)
        {
            name = null;
            description = null;
            severity = DiagnosticSeverity.Warning;
            ruleNamespace = null;

            if (attribute.ConstructorArguments.Count == 0 || attribute.ConstructorArguments[0].Value is not string constructorName)
            {
                return false;
            }

            name = constructorName;
            if (attribute.ConstructorArguments.Count >= 2)
            {
                CustomAttributeTypedArgument secondArg = attribute.ConstructorArguments[1];
                if (secondArg.ArgumentType == typeof(string))
                {
                    description = secondArg.Value as string;
                }
                else if (attribute.ConstructorArguments.Count >= 3
                    && secondArg.ArgumentType == typeof(Type)
                    && secondArg.Value is Type resourceProviderType
                    && attribute.ConstructorArguments[2].Value is string resourceKey)
                {
                    description = GetStringFromResourceProvider(resourceProviderType, resourceKey);
                }
            }

            for (int i = 0; i < attribute.NamedArguments.Count; i++)
            {
                CustomAttributeNamedArgument namedArg = attribute.NamedArguments[i];
                if (namedArg.MemberName == "Namespace")
                {
                    ruleNamespace = namedArg.TypedValue.Value as string;
                    continue;
                }

                if (namedArg.MemberName == "Severity")
                {
                    if (TryConvertSeverity(namedArg.TypedValue.Value, out DiagnosticSeverity parsedSeverity))
                    {
                        severity = parsedSeverity;
                    }
                }
            }

            return true;
        }

        private static bool TryConvertSeverity(object? value, out DiagnosticSeverity severity)
        {
            severity = DiagnosticSeverity.Warning;
            if (value is null)
            {
                return false;
            }

            if (value is DiagnosticSeverity directSeverity)
            {
                severity = directSeverity;
                return true;
            }

            try
            {
                int numericSeverity = Convert.ToInt32(value);
                if (Enum.IsDefined(typeof(DiagnosticSeverity), numericSeverity))
                {
                    severity = (DiagnosticSeverity)numericSeverity;
                    return true;
                }
            }
            catch
            {
                // Ignore and keep default severity.
            }

            return false;
        }

        private static string? GetStringFromResourceProvider(Type resourceProvider, string resourceKey)
        {
            PropertyInfo? resourceProperty = resourceProvider.GetProperty(resourceKey, BindingFlags.Static | BindingFlags.NonPublic);
            return (string?)resourceProperty?.GetValue(null);
        }

        private RuleInfo(
            string name,
            string @namespace)
        {
            Name = name;
            Namespace = @namespace;
            FullName = $"{@namespace}/{name}";
        }

        public string Name { get; }

        public string Namespace { get; }

        public string FullName { get; }

        public override string ToString() => FullName;

        public string? Description { get; private set; }

        public DiagnosticSeverity DefaultSeverity { get; private set; }

        public bool IsThreadsafe { get; private set; }

        public bool IsIdempotent { get; private set;  }

        public SourceType Source { get; private set;  }
    }
}
