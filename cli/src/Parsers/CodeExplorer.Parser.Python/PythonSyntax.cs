namespace CodeExplorer.Core.Parser;

internal static class TreeSitterSyntax
{
    public static class Fields
    {
        public const string Name = "name";
        public const string Function = "function";
        public const string Type = "type";
        public const string Value = "value";
        public const string Pattern = "pattern";
        public const string Left = "left";
        public const string Right = "right";
        public const string Body = "body";
        public const string Arguments = "arguments";
        public const string Parameters = "parameters";
        public const string Return = "return";
        public const string Operator = "operator";
        public const string Expression = "expression";
        public const string Property = "property";
        public const string Object = "object";
        public const string Source = "source";
        public const string Constructor = "constructor";
        public const string Field = "field";
        public const string Path = "path";
        public const string Key = "key";
        public const string Operand = "operand";
        public const string Returns = "returns";
        public const string ReturnType = "return_type";
        public const string Index = "index";
        public const string Subscript = "subscript";
        public const string ModuleName = "module_name";
        public const string Tag = "tag";
    }

    public static class Common
    {
        public const string Identifier = "identifier";
        public const string TypeIdentifier = "type_identifier";
        public const string String = "string";
        public const string StringLiteral = "string_literal";
        public const string CallExpression = "call_expression";
        public const string MemberExpression = "member_expression";
        public const string ArgumentList = "argument_list";
        public const string Arguments = "arguments";
        public const string Argument = "argument";
        public const string AssignmentExpression = "assignment_expression";
        public const string BinaryExpression = "binary_expression";
        public const string IndexExpression = "index_expression";
        public const string Comment = "comment";
        public const string FunctionDeclaration = "function_declaration";
        public const string AwaitExpression = "await_expression";
        public const string ParenthesizedExpression = "parenthesized_expression";
        public const string ClassDeclaration = "class_declaration";
        public const string VariableName = "variable_name";
        public const string Block = "block";
        public const string MethodDeclaration = "method_declaration";
    }

    public static class Symbols
    {
        public const string Colon = ":";
        public new const string Equals = "=";
        public const string Equal = "=";
    }

    public static class Python
    {
        public const string FunctionDefinition = "function_definition";
        public const string ClassDefinition = "class_definition";
        public const string DecoratedDefinition = "decorated_definition";
        public const string Decorator = "decorator";
        public const string Call = "call";
        public const string Attribute = "attribute";
        public const string ArgumentList = "argument_list";
        public const string String = "string";
        public const string ImportStatement = "import_statement";
        public const string ImportFromStatement = "import_from_statement";
        public const string Parameters = "parameters";
        public const string Pattern = "pattern";
        public const string Assignment = "assignment";
        public const string DottedName = "dotted_name";
        public const string KeywordArgument = "keyword_argument";
        public const string List = "list";
        public const string Identifier = "identifier";
        public const string AliasedName = "aliased_name";
        public const string VariableName = "variable_name";
        public const string BinaryOperator = "binary_operator";
        public const string Subscript = "subscript";
        public const string FormatString = "format_string";
        public const string Block = "block";
        public const string Module = "module";
        public const string ExpressionStatement = "expression_statement";
        public const string TypedDefaultParameter = "typed_default_parameter";
        public const string TypedParameter = "typed_parameter";
        public const string DefaultParameter = "default_parameter";
        public const string Type = "type";
    }
}

