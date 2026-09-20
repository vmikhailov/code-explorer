namespace CodeExplorer.Cypher.Ast;

public enum BinaryOperator
{
    Equal,            // =
    NotEqual,         // != or <>
    LessThan,         // <
    GreaterThan,      // >
    LessOrEqual,      // <=
    GreaterOrEqual,   // >=
    And,              // AND
    Or,               // OR
    StartsWith,       // STARTS WITH
    EndsWith,         // ENDS WITH
    Contains,         // CONTAINS
    In,               // IN
    Add,              // +
    Subtract,         // -
    Multiply,         // *
    Divide,           // /
    Modulo,           // %
    Power,            // ^
    RegexMatch,       // =~
    Xor               // XOR
}

public enum UnaryOperator
{
    Not,        // NOT
    IsNull,     // IS NULL
    IsNotNull,  // IS NOT NULL
    Minus,      // -
    Plus        // +
}
