using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.CompilerServices;

namespace Specter.Tools
{
    public sealed class ParameterSymbol
    {
        internal ParameterSymbol(
            ParameterAst ast,
            string name,
            Type? staticType,
            bool isMandatory,
            bool valueFromPipeline,
            bool isSwitchParameter,
            bool isCatchAll)
        {
            Ast = ast;
            Name = name;
            StaticType = staticType;
            IsMandatory = isMandatory;
            ValueFromPipeline = valueFromPipeline;
            IsSwitchParameter = isSwitchParameter;
            IsCatchAll = isCatchAll;
        }

        public ParameterAst Ast { get; }

        public string Name { get; }

        public Type? StaticType { get; }

        public bool IsMandatory { get; }

        public bool ValueFromPipeline { get; }

        public bool IsSwitchParameter { get; }

        public bool IsCatchAll { get; }
    }

    public sealed class FunctionSymbol
    {
        internal FunctionSymbol(
            FunctionDefinitionAst ast,
            string name,
            string? verb,
            string? noun,
            bool hasCmdletBinding,
            bool supportsShouldProcess,
            bool hasOutputType,
            bool isNested,
            bool isExported,
            bool isInModule,
            IReadOnlyList<ParameterSymbol> parameters)
        {
            Ast = ast;
            Name = name;
            Verb = verb;
            Noun = noun;
            HasCmdletBinding = hasCmdletBinding;
            SupportsShouldProcess = supportsShouldProcess;
            HasOutputType = hasOutputType;
            IsNested = isNested;
            IsExported = isExported;
            IsInModule = isInModule;
            Parameters = parameters;
            HasVerbNounName = verb is not null && noun is not null;
            IsVariadic = HasCatchAllParameter(parameters);
            IsCmdletStyle = HasCmdletBinding || (IsExported && HasVerbNounName);
        }

        public FunctionDefinitionAst Ast { get; }

        public string Name { get; }

        public string? Verb { get; }

        public string? Noun { get; }

        public bool HasCmdletBinding { get; }

        public bool SupportsShouldProcess { get; }

        public bool HasOutputType { get; }

        public bool IsNested { get; }

        public bool IsExported { get; }

        public bool IsInModule { get; }

        public bool HasVerbNounName { get; }

        public bool IsVariadic { get; }

        public bool IsCmdletStyle { get; }

        public IReadOnlyList<ParameterSymbol> Parameters { get; }

        private static bool HasCatchAllParameter(IReadOnlyList<ParameterSymbol> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].IsCatchAll)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class ScriptModel
    {
        private static readonly ConditionalWeakTable<Ast, ScriptModel> s_cache = new ConditionalWeakTable<Ast, ScriptModel>();

        private readonly Dictionary<FunctionDefinitionAst, FunctionSymbol> _functionByAst;
        private readonly Dictionary<string, FunctionSymbol> _functionByName;

        private ScriptModel(Ast scriptAst, string? scriptPath)
        {
            ScriptPath = scriptPath;
            IsModuleScript = AstExtensions.IsModuleScript(scriptPath);

            HashSet<string>? exportedSet = AstExtensions.GetExportedFunctionNames(scriptAst);
            if (exportedSet is null)
            {
                ExportedFunctionNames = null;
            }
            else
            {
                var exported = new List<string>(exportedSet.Count);
                foreach (string name in exportedSet)
                {
                    exported.Add(name);
                }

                ExportedFunctionNames = exported;
            }

            _functionByAst = new Dictionary<FunctionDefinitionAst, FunctionSymbol>();
            _functionByName = new Dictionary<string, FunctionSymbol>(StringComparer.OrdinalIgnoreCase);

            var functions = new List<FunctionSymbol>();
            foreach (Ast foundAst in scriptAst.FindAll(static ast => ast is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var functionAst = (FunctionDefinitionAst)foundAst;
                FunctionSymbol symbol = CreateFunctionSymbol(functionAst, IsModuleScript, exportedSet);
                _functionByAst[functionAst] = symbol;
                if (!string.IsNullOrEmpty(symbol.Name) && !_functionByName.ContainsKey(symbol.Name))
                {
                    _functionByName[symbol.Name] = symbol;
                }

                functions.Add(symbol);
            }

            Functions = functions;
            HasScriptCmdletBinding = scriptAst is ScriptBlockAst scriptBlock
                && scriptBlock.ParamBlock.HasCmdletBinding();
        }

        public static ScriptModel GetOrCreate(Ast scriptAst, IReadOnlyList<Token> scriptTokens, string? scriptPath)
        {
            return s_cache.GetValue(scriptAst, ast => new ScriptModel(ast, scriptPath));
        }

        public FunctionSymbol? GetFunctionSymbol(FunctionDefinitionAst funcAst)
        {
            if (_functionByAst.TryGetValue(funcAst, out FunctionSymbol? symbol))
            {
                return symbol;
            }

            return null;
        }

        public FunctionSymbol? GetFunctionSymbol(string functionName)
        {
            if (_functionByName.TryGetValue(functionName, out FunctionSymbol? symbol))
            {
                return symbol;
            }

            return null;
        }

        public IReadOnlyList<FunctionSymbol> Functions { get; }

        public IReadOnlyList<string>? ExportedFunctionNames { get; }

        public bool IsModuleScript { get; }

        public bool HasScriptCmdletBinding { get; }

        public string? ScriptPath { get; }

        private static FunctionSymbol CreateFunctionSymbol(
            FunctionDefinitionAst functionAst,
            bool isModuleScript,
            HashSet<string>? exportedNames)
        {
            string functionName = functionAst.GetNameWithoutScope() ?? functionAst.Name;
            SplitVerbNoun(functionName, out string? verb, out string? noun);

            bool hasCmdletBinding = functionAst.Body.ParamBlock.HasCmdletBinding();
            bool supportsShouldProcess = HasSupportsShouldProcess(functionAst.Body.ParamBlock);
            bool hasOutputType = HasOutputType(functionAst.Body.ParamBlock);
            bool isNested = IsNestedFunction(functionAst);

            bool isExported = false;
            if (isModuleScript)
            {
                if (exportedNames is null)
                {
                    isExported = true;
                }
                else if (exportedNames.Contains(functionName))
                {
                    isExported = true;
                }
            }

            var parameters = BuildParameters(functionAst);
            return new FunctionSymbol(
                functionAst,
                functionName,
                verb,
                noun,
                hasCmdletBinding,
                supportsShouldProcess,
                hasOutputType,
                isNested,
                isExported,
                isModuleScript,
                parameters);
        }

        private static IReadOnlyList<ParameterSymbol> BuildParameters(FunctionDefinitionAst functionAst)
        {
            var parameterSymbols = new List<ParameterSymbol>();

            if (functionAst.Parameters is not null)
            {
                AddParameterSymbols(functionAst.Parameters, parameterSymbols);
            }

            if (functionAst.Body.ParamBlock?.Parameters is not null)
            {
                AddParameterSymbols(functionAst.Body.ParamBlock.Parameters, parameterSymbols);
            }

            return parameterSymbols;
        }

        private static void AddParameterSymbols(IReadOnlyList<ParameterAst> parameterAsts, List<ParameterSymbol> symbols)
        {
            for (int i = 0; i < parameterAsts.Count; i++)
            {
                ParameterAst parameterAst = parameterAsts[i];
                string parameterName = parameterAst.Name.GetNameWithoutScope();
                Type? staticType = parameterAst.StaticType;

                bool isMandatory = false;
                bool valueFromPipeline = false;
                bool valueFromRemainingArguments = false;

                if (parameterAst.Attributes is not null)
                {
                    for (int attrIndex = 0; attrIndex < parameterAst.Attributes.Count; attrIndex++)
                    {
                        if (parameterAst.Attributes[attrIndex] is not AttributeAst attributeAst)
                        {
                            continue;
                        }

                        Type? attrType = attributeAst.TypeName.GetReflectionAttributeType();
                        if (attrType != typeof(ParameterAttribute))
                        {
                            continue;
                        }

                        foreach (NamedAttributeArgumentAst namedArg in attributeAst.NamedArguments)
                        {
                            if (string.Equals(namedArg.ArgumentName, "Mandatory", StringComparison.OrdinalIgnoreCase))
                            {
                                isMandatory = AstTools.IsTrue(namedArg.GetValue());
                                continue;
                            }

                            if (string.Equals(namedArg.ArgumentName, "ValueFromPipeline", StringComparison.OrdinalIgnoreCase))
                            {
                                valueFromPipeline = AstTools.IsTrue(namedArg.GetValue());
                                continue;
                            }

                            if (string.Equals(namedArg.ArgumentName, "ValueFromRemainingArguments", StringComparison.OrdinalIgnoreCase))
                            {
                                valueFromRemainingArguments = AstTools.IsTrue(namedArg.GetValue());
                            }
                        }
                    }
                }

                bool isSwitchParameter = staticType == typeof(SwitchParameter);
                bool isCatchAll = valueFromRemainingArguments || IsCatchAllArrayType(staticType);

                symbols.Add(new ParameterSymbol(
                    parameterAst,
                    parameterName,
                    staticType,
                    isMandatory,
                    valueFromPipeline,
                    isSwitchParameter,
                    isCatchAll));
            }
        }

        private static bool IsCatchAllArrayType(Type? staticType)
        {
            if (staticType is null || !staticType.IsArray)
            {
                return false;
            }

            Type? elementType = staticType.GetElementType();
            return elementType == typeof(string) || elementType == typeof(object);
        }

        private static bool HasSupportsShouldProcess(ParamBlockAst? paramBlock)
        {
            if (paramBlock?.Attributes is null)
            {
                return false;
            }

            return AstTools.TryGetShouldProcessAttributeArgumentAst(paramBlock.Attributes, out NamedAttributeArgumentAst? shouldProcessArgument)
                && shouldProcessArgument is not null
                && AstTools.IsTrue(shouldProcessArgument.GetValue());
        }

        private static bool HasOutputType(ParamBlockAst? paramBlock)
        {
            if (paramBlock?.Attributes is null)
            {
                return false;
            }

            foreach (AttributeAst attribute in paramBlock.Attributes)
            {
                string fullName = attribute.TypeName.FullName;
                if (string.Equals(fullName, "OutputType", StringComparison.OrdinalIgnoreCase)
                    || fullName.EndsWith(".OutputType", StringComparison.OrdinalIgnoreCase)
                    || fullName.EndsWith(".OutputTypeAttribute", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNestedFunction(FunctionDefinitionAst functionAst)
        {
            Ast? parent = functionAst.Parent;
            while (parent is not null)
            {
                if (parent is FunctionDefinitionAst)
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }

        private static void SplitVerbNoun(string name, out string? verb, out string? noun)
        {
            int dashIndex = name.IndexOf('-');
            if (dashIndex <= 0 || dashIndex >= name.Length - 1)
            {
                verb = null;
                noun = null;
                return;
            }

            verb = name.Substring(0, dashIndex);
            noun = name.Substring(dashIndex + 1);
        }

    }
}
