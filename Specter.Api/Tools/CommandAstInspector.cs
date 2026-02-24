using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.CommandDatabase;

namespace Specter.Tools
{
    /// <summary>
    /// Lightweight static command-argument inspection for rules.
    /// This avoids runtime PowerShell parameter binding APIs.
    /// </summary>
    public static class CommandAstInspector
    {
        public static bool TryGetBoundParameterConstantValue(
            CommandAst commandAst,
            CommandMetadata? commandMetadata,
            string parameterName,
            out object? value)
        {
            if (!TryGetBoundParameterArgument(commandAst, commandMetadata, parameterName, out CommandElementAst? argument))
            {
                value = null;
                return false;
            }

            if (argument is null)
            {
                // Switch parameter explicitly present without an argument.
                value = true;
                return true;
            }

            if (TryGetArgumentConstantValue(argument, out object? constant))
            {
                value = constant;
                return true;
            }

            value = null;
            return true;
        }

        public static bool TryGetBoundParameterArgument(
            CommandAst commandAst,
            CommandMetadata? commandMetadata,
            string parameterName,
            out CommandElementAst? argument)
        {
            argument = null;
            if (commandAst is null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            IReadOnlyList<CommandElementAst> elements = commandAst.CommandElements;
            if (elements.Count <= 1)
            {
                return false;
            }

            string? targetParameter = ResolveTargetParameterName(commandMetadata, parameterName);
            int positionalIndex = 0;

            for (int i = 1; i < elements.Count; i++)
            {
                CommandElementAst element = elements[i];
                if (element is CommandParameterAst parameterAst)
                {
                    string? boundParameterName = ResolveBoundParameterName(parameterAst.ParameterName, commandMetadata);
                    CommandElementAst? boundArgument = parameterAst.Argument;
                    bool consumedNext = false;
                    if (boundArgument is null
                        && i + 1 < elements.Count
                        && elements[i + 1] is not CommandParameterAst)
                    {
                        boundArgument = elements[i + 1];
                        consumedNext = true;
                    }

                    if (IsTargetParameter(boundParameterName, targetParameter, parameterName))
                    {
                        argument = boundArgument;
                        return true;
                    }

                    if (consumedNext)
                    {
                        i++;
                    }

                    continue;
                }

                if (IsPositionalMatch(commandMetadata, targetParameter, parameterName, positionalIndex))
                {
                    argument = element;
                    return true;
                }

                positionalIndex++;
            }

            return false;
        }

        private static bool TryGetArgumentConstantValue(CommandElementAst argument, out object? value)
        {
            if (argument is ExpressionAst expressionAst)
            {
                value = AstTools.GetSafeValueFromAst(expressionAst);
                return true;
            }

            value = null;
            return false;
        }

        private static bool IsTargetParameter(string? boundParameterName, string? targetParameter, string rawTarget)
        {
            if (!string.IsNullOrEmpty(boundParameterName) && !string.IsNullOrEmpty(targetParameter))
            {
                if (string.Equals(boundParameterName, targetParameter, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (string.IsNullOrEmpty(boundParameterName))
            {
                return false;
            }

            string targetParameterText = targetParameter ?? string.Empty;
            if (targetParameterText.Length > 0
                && targetParameterText.StartsWith(boundParameterName, StringComparison.OrdinalIgnoreCase))
            {
                // When metadata is unavailable, allow unambiguous caller-intended prefix usage (e.g. -Sc for -Scope).
                return true;
            }

            return string.Equals(boundParameterName, rawTarget, StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolveTargetParameterName(CommandMetadata? commandMetadata, string parameterName)
        {
            if (commandMetadata is null)
            {
                return parameterName;
            }

            return ResolveBoundParameterName(parameterName, commandMetadata);
        }

        private static string? ResolveBoundParameterName(string? providedParameterName, CommandMetadata? commandMetadata)
        {
            if (string.IsNullOrEmpty(providedParameterName))
            {
                return null;
            }

            if (commandMetadata is null || commandMetadata.Parameters.Count == 0)
            {
                return providedParameterName;
            }

            ParameterMetadata? exactMatch = null;
            for (int i = 0; i < commandMetadata.Parameters.Count; i++)
            {
                ParameterMetadata parameter = commandMetadata.Parameters[i];
                if (string.Equals(parameter.Name, providedParameterName, StringComparison.OrdinalIgnoreCase))
                {
                    exactMatch = parameter;
                    break;
                }
            }

            if (exactMatch is not null)
            {
                return exactMatch.Name;
            }

            ParameterMetadata? prefixMatch = null;
            int prefixMatches = 0;
            for (int i = 0; i < commandMetadata.Parameters.Count; i++)
            {
                ParameterMetadata parameter = commandMetadata.Parameters[i];
                if (parameter.Name.StartsWith(providedParameterName, StringComparison.OrdinalIgnoreCase))
                {
                    prefixMatch = parameter;
                    prefixMatches++;
                    if (prefixMatches > 1)
                    {
                        return null;
                    }
                }
            }

            return prefixMatches == 1 ? prefixMatch!.Name : null;
        }

        private static bool IsPositionalMatch(
            CommandMetadata? commandMetadata,
            string? targetParameter,
            string rawTargetParameter,
            int position)
        {
            if (commandMetadata is null || commandMetadata.Parameters.Count == 0)
            {
                return false;
            }

            string? effectiveTarget = targetParameter ?? rawTargetParameter;
            ParameterMetadata? target = null;
            int positionedParameterCount = 0;

            for (int i = 0; i < commandMetadata.Parameters.Count; i++)
            {
                ParameterMetadata parameter = commandMetadata.Parameters[i];
                bool hasPosition = HasPosition(parameter, position);
                if (!hasPosition)
                {
                    continue;
                }

                positionedParameterCount++;
                if (string.Equals(parameter.Name, effectiveTarget, StringComparison.OrdinalIgnoreCase))
                {
                    target = parameter;
                }
            }

            if (target is null)
            {
                return false;
            }

            // If multiple parameters can bind at this position across sets,
            // treat as ambiguous and do not guess.
            return positionedParameterCount == 1;
        }

        private static bool HasPosition(ParameterMetadata parameter, int position)
        {
            IReadOnlyList<ParameterSetInfo> sets = parameter.ParameterSets;
            for (int i = 0; i < sets.Count; i++)
            {
                int? setPosition = sets[i].Position;
                if (setPosition.HasValue && setPosition.Value == position)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
