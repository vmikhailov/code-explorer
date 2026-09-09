namespace CodeExplorer.Cypher.Parser;

public enum CypherToken
{
    None,

    // Keywords
    Match,
    Optional,
    Where,
    Return,
    Distinct,
    As,
    Order,
    By,
    Asc,
    Desc,
    Skip,
    Limit,
    And,
    Or,
    Not,
    Starts,
    Ends,
    Contains,
    In,
    Is,
    Null,
    True,
    False,
    Case,
    When,
    Then,
    Else,
    End,
    With,

    // Punctuation & Delimiters
    LParen,        // (
    RParen,        // )
    LBracket,      // [
    RBracket,      // ]
    LBrace,        // {
    RBrace,        // }
    Colon,         // :
    Comma,         // ,
    Dot,           // .
    DotDot,        // ..
    Asterisk,      // *
    Pipe,          // |
    Dollar,        // $
    Plus,          // +

    // Graph Arrows
    ArrowRight,    // ->
    ArrowLeft,     // <-
    Dash,          // -

    // Comparison Operators
    Equal,         // =
    NotEqual,      // <> or !=
    LessThan,      // <
    GreaterThan,   // >
    LessOrEqual,   // <=
    GreaterOrEqual,// >=

    // Literals & Identifiers
    Identifier,
    StringLiteral,
    Number,
    Parameter      // $paramName
}
