#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PSpecter.Builtin.Rules.Dsc
{
    internal readonly struct MofProperty
    {
        public readonly string Name;
        public readonly bool IsKey;
        public readonly bool IsRequired;

        public MofProperty(string name, bool isKey, bool isRequired)
        {
            Name = name;
            IsKey = isKey;
            IsRequired = isRequired;
        }
    }

    internal readonly struct MofClass
    {
        public readonly string Name;
        public readonly string SuperClassName;
        public readonly IReadOnlyList<MofProperty> Properties;

        public MofClass(string name, string superClassName, IReadOnlyList<MofProperty> properties)
        {
            Name = name;
            SuperClassName = superClassName;
            Properties = properties;
        }

        public bool HasSuperClass => !string.IsNullOrEmpty(SuperClassName);
    }

    internal static class MofParser
    {
        private static readonly Regex s_classRegex = new(
            @"class\s+(\w+)\s*(?::\s*(\w+))?\s*\{([^}]*)\}",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex s_propertyRegex = new(
            @"\[([^\]]*)\]\s*\w+\s+(\w+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static IReadOnlyList<MofClass> Parse(string mofContent)
        {
            var classes = new List<MofClass>();

            foreach (Match classMatch in s_classRegex.Matches(mofContent))
            {
                string className = classMatch.Groups[1].Value;
                string superClassName = classMatch.Groups[2].Success ? classMatch.Groups[2].Value : null;
                string body = classMatch.Groups[3].Value;

                var properties = new List<MofProperty>();
                foreach (Match propMatch in s_propertyRegex.Matches(body))
                {
                    string qualifiers = propMatch.Groups[1].Value;
                    string propName = propMatch.Groups[2].Value;

                    bool isKey = ContainsQualifier(qualifiers, "key");
                    bool isRequired = ContainsQualifier(qualifiers, "required");

                    properties.Add(new MofProperty(propName, isKey, isRequired));
                }

                classes.Add(new MofClass(className, superClassName, properties));
            }

            return classes;
        }

        internal static IDictionary<string, string> GetKeyAndRequiredProperties(string mofFilePath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(mofFilePath))
            {
                return result;
            }

            string content;
            try
            {
                content = File.ReadAllText(mofFilePath);
            }
            catch
            {
                return result;
            }

            IReadOnlyList<MofClass> classes = Parse(content);

            MofClass? resourceClass = null;
            foreach (MofClass cls in classes)
            {
                if (cls.HasSuperClass)
                {
                    resourceClass = cls;
                    break;
                }
            }

            if (resourceClass is null)
            {
                return result;
            }

            foreach (MofProperty prop in resourceClass.Value.Properties)
            {
                if (prop.IsKey)
                {
                    result[prop.Name] = "Key";
                }
                else if (prop.IsRequired)
                {
                    result[prop.Name] = "Required";
                }
            }

            return result;
        }

        private static bool ContainsQualifier(string qualifiers, string qualifier)
        {
            foreach (string part in qualifiers.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Equals(qualifier, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
