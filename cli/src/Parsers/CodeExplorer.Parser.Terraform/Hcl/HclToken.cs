using Superpower.Display;

namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// Token kinds recognized by <see cref="HclTokenizer"/> for HashiCorp Configuration Language (HCL2).
/// </summary>
public enum HclToken
{
    None,

    [Token(Description = "identifier")]
    Identifier,

    [Token(Description = "string literal")]
    StringLiteral,

    [Token(Description = "heredoc literal")]
    HeredocLiteral,

    [Token(Description = "number")]
    Number,

    [Token(Example = "{")]
    LBrace,

    [Token(Example = "}")]
    RBrace,

    [Token(Example = "[")]
    LBracket,

    [Token(Example = "]")]
    RBracket,

    [Token(Example = "(")]
    LParen,

    [Token(Example = ")")]
    RParen,

    [Token(Example = "=")]
    Equals,

    [Token(Example = "=>")]
    FatArrow,

    [Token(Example = "...")]
    Ellipsis,

    [Token(Example = ",")]
    Comma,

    [Token(Example = ".")]
    Dot,

    [Token(Example = ":")]
    Colon,

    [Token(Example = "?")]
    Question,

    [Token(Description = "operator")]
    Operator
}
