using System.Management.Automation.Language;

namespace Specter
{
    public class Correction
    {
        public Correction(IScriptExtent extent, string correctionText, string description)
        {
            Extent = extent;
            CorrectionText = correctionText;
            Description = description;
        }

        public IScriptExtent Extent { get; }

        public string CorrectionText { get; }

        public string Description { get; }
    }

    public class AstCorrection : Correction
    {
        public AstCorrection(Ast correctedAst, string correctionText, string description)
            : base(correctedAst.Extent, correctionText, description)
        {
            Ast = correctedAst;
        }

        public Ast Ast { get; }
    }

    public class TokenCorrection : Correction
    {
        public TokenCorrection(Token correctedToken, string correctionText, string description)
            : base(correctedToken.Extent, correctionText, description)
        {
            Token = correctedToken;
        }

        public Token Token { get; }
    }
}
