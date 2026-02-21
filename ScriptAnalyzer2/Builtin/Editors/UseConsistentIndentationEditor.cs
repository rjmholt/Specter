using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.PowerShell.ScriptAnalyzer.Formatting;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Editors
{
    public enum PipelineIndentationStyle
    {
        IncreaseIndentationForFirstPipeline,
        IncreaseIndentationAfterEveryPipeline,
        NoIndentation,
        None,
    }

    public sealed class UseConsistentIndentationEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        public int IndentationSize { get; set; } = 4;
        public bool UseTabs { get; set; } = false;
        public PipelineIndentationStyle PipelineIndentation { get; set; } = PipelineIndentationStyle.IncreaseIndentationForFirstPipeline;
    }

    [Editor("UseConsistentIndentation", Description = "Enforces consistent indentation by walking the token stream and tracking nesting depth")]
    public sealed class UseConsistentIndentationEditor : IScriptEditor, IConfigurableEditor<UseConsistentIndentationEditorConfiguration>
    {
        public UseConsistentIndentationEditor(UseConsistentIndentationEditorConfiguration configuration)
        {
            Configuration = configuration ?? new UseConsistentIndentationEditorConfiguration();
        }

        public UseConsistentIndentationEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string filePath)
        {
            if (scriptContent is null) throw new ArgumentNullException(nameof(scriptContent));

            var edits = new List<ScriptEdit>();
            int indentationLevel = 0;
            bool onNewLine = false;
            int pipelineIndentIncrease = 0;
            bool skipNextPipelineLine = false;

            var lParenSkippedIndent = new Stack<bool>();
            var multiLinePipelines = GetMultiLinePipelineRanges(ast);

            for (int k = 0; k < tokens.Count; k++)
            {
                Token token = tokens[k];

                switch (token.Kind)
                {
                    case TokenKind.LCurly:
                    case TokenKind.AtCurly:
                        indentationLevel++;
                        AddIndentEditIfOnNewLine(ref onNewLine, scriptContent, tokens, k, indentationLevel - 1, edits);
                        break;

                    case TokenKind.RCurly:
                        indentationLevel = Math.Max(0, indentationLevel - 1);
                        AddIndentEditIfOnNewLine(ref onNewLine, scriptContent, tokens, k, indentationLevel, edits);
                        break;

                    case TokenKind.LParen:
                    case TokenKind.DollarParen:
                    case TokenKind.AtParen:
                        bool skipIndent = IsSingleLineParen(tokens, k);
                        lParenSkippedIndent.Push(skipIndent);
                        if (!skipIndent)
                        {
                            indentationLevel++;
                        }
                        AddIndentEditIfOnNewLine(ref onNewLine, scriptContent, tokens, k, indentationLevel - (skipIndent ? 0 : 1), edits);
                        break;

                    case TokenKind.RParen:
                        if (lParenSkippedIndent.Count > 0 && !lParenSkippedIndent.Pop())
                        {
                            indentationLevel = Math.Max(0, indentationLevel - 1);
                        }
                        AddIndentEditIfOnNewLine(ref onNewLine, scriptContent, tokens, k, indentationLevel, edits);
                        break;

                    case TokenKind.Pipe:
                        if (Configuration.PipelineIndentation == PipelineIndentationStyle.None
                            && IsInMultiLinePipeline(token, multiLinePipelines)
                            && k + 1 < tokens.Count
                            && (tokens[k + 1].Kind == TokenKind.NewLine || tokens[k + 1].Kind == TokenKind.LineContinuation))
                        {
                            skipNextPipelineLine = true;
                        }
                        else if (Configuration.PipelineIndentation != PipelineIndentationStyle.None
                            && Configuration.PipelineIndentation != PipelineIndentationStyle.NoIndentation
                            && IsInMultiLinePipeline(token, multiLinePipelines)
                            && k + 1 < tokens.Count
                            && (tokens[k + 1].Kind == TokenKind.NewLine || tokens[k + 1].Kind == TokenKind.LineContinuation))
                        {
                            bool shouldIncrement = Configuration.PipelineIndentation == PipelineIndentationStyle.IncreaseIndentationAfterEveryPipeline
                                || (Configuration.PipelineIndentation == PipelineIndentationStyle.IncreaseIndentationForFirstPipeline
                                    && pipelineIndentIncrease == 0);

                            if (shouldIncrement)
                            {
                                indentationLevel++;
                                pipelineIndentIncrease++;
                            }
                        }
                        AddIndentEditIfOnNewLine(ref onNewLine, scriptContent, tokens, k, indentationLevel, edits);
                        break;

                    case TokenKind.NewLine:
                    case TokenKind.LineContinuation:
                        onNewLine = true;

                        if (pipelineIndentIncrease > 0 && !IsStillInMultiLinePipeline(tokens, k, multiLinePipelines))
                        {
                            indentationLevel = Math.Max(0, indentationLevel - pipelineIndentIncrease);
                            pipelineIndentIncrease = 0;
                        }
                        break;

                    case TokenKind.EndOfInput:
                        break;

                    default:
                        if (skipNextPipelineLine)
                        {
                            skipNextPipelineLine = false;
                            if (token.Kind != TokenKind.Comment)
                            {
                                onNewLine = false;
                                break;
                            }
                        }

                        int extra = 0;

                        if (k > 0 && tokens[k - 1].Kind == TokenKind.LineContinuation)
                        {
                            extra = 1;
                        }
                        else if (k > 0 && tokens[k - 1].Kind == TokenKind.NewLine && token.Kind == TokenKind.Comment)
                        {
                            for (int j = k - 2; j >= 0; j--)
                            {
                                if (tokens[j].Kind == TokenKind.LineContinuation)
                                {
                                    extra = 1;
                                    break;
                                }
                                if (tokens[j].Kind != TokenKind.Comment && tokens[j].Kind != TokenKind.NewLine)
                                {
                                    break;
                                }
                            }
                        }

                        AddIndentEditIfOnNewLine(ref onNewLine, scriptContent, tokens, k, indentationLevel + extra, edits);
                        break;
                }
            }

            return edits;
        }

        private void AddIndentEditIfOnNewLine(
            ref bool onNewLine,
            string scriptContent,
            IReadOnlyList<Token> tokens,
            int k,
            int expectedLevel,
            List<ScriptEdit> edits)
        {
            if (!onNewLine) return;
            onNewLine = false;

            Token token = tokens[k];
            if (token.Kind == TokenKind.NewLine || token.Kind == TokenKind.EndOfInput)
            {
                return;
            }

            string expectedIndent = GetIndentString(expectedLevel);
            int actualIndentStart = GetLineStartOffset(scriptContent, token.Extent.StartOffset);
            int actualIndentEnd = token.Extent.StartOffset;
            string actualIndent = scriptContent.Substring(actualIndentStart, actualIndentEnd - actualIndentStart);

            if (actualIndent != expectedIndent)
            {
                edits.Add(new ScriptEdit(actualIndentStart, actualIndentEnd, expectedIndent));
            }
        }

        private string GetIndentString(int level)
        {
            if (level <= 0) return string.Empty;

            if (Configuration.UseTabs)
            {
                return new string('\t', level);
            }

            return new string(' ', level * Configuration.IndentationSize);
        }

        private static int GetLineStartOffset(string content, int offset)
        {
            for (int i = offset - 1; i >= 0; i--)
            {
                if (content[i] == '\n')
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private static bool IsSingleLineParen(IReadOnlyList<Token> tokens, int lParenIdx)
        {
            int depth = 1;
            for (int i = lParenIdx + 1; i < tokens.Count; i++)
            {
                if (tokens[i].Kind == TokenKind.LParen
                    || tokens[i].Kind == TokenKind.DollarParen
                    || tokens[i].Kind == TokenKind.AtParen)
                {
                    depth++;
                }
                else if (tokens[i].Kind == TokenKind.RParen)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return tokens[i].Extent.StartLineNumber == tokens[lParenIdx].Extent.StartLineNumber;
                    }
                }
            }

            return true;
        }

        private static List<OffsetRange> GetMultiLinePipelineRanges(Ast ast)
        {
            var ranges = new List<OffsetRange>();
            var pipelines = ast.FindAll(a => a is PipelineAst pipeline && pipeline.PipelineElements.Count > 1, searchNestedScriptBlocks: true);

            foreach (PipelineAst pipeline in pipelines.Cast<PipelineAst>())
            {
                if (pipeline.Extent.StartLineNumber != pipeline.Extent.EndLineNumber)
                {
                    ranges.Add(new OffsetRange(pipeline.Extent.StartOffset, pipeline.Extent.EndOffset));
                }
            }

            return ranges;
        }

        private static bool IsInMultiLinePipeline(Token token, List<OffsetRange> pipelineRanges)
        {
            int offset = token.Extent.StartOffset;
            for (int i = 0; i < pipelineRanges.Count; i++)
            {
                if (offset >= pipelineRanges[i].Start && offset < pipelineRanges[i].End)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsStillInMultiLinePipeline(
            IReadOnlyList<Token> tokens, int newLineIdx, List<OffsetRange> pipelineRanges)
        {
            for (int i = newLineIdx + 1; i < tokens.Count; i++)
            {
                if (tokens[i].Kind == TokenKind.NewLine || tokens[i].Kind == TokenKind.Comment)
                {
                    continue;
                }

                return IsInMultiLinePipeline(tokens[i], pipelineRanges);
            }
            return false;
        }

        private readonly struct OffsetRange
        {
            public OffsetRange(int start, int end) { Start = start; End = end; }
            public int Start { get; }
            public int End { get; }
        }
    }
}
