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

    public static class Go
    {
        public const string FunctionDeclaration = "function_declaration";
        public const string MethodDeclaration = "method_declaration";
        public const string TypeDeclaration = "type_declaration";
        public const string TypeSpec = "type_spec";
        public const string StructType = "struct_type";
        public const string InterfaceType = "interface_type";
        public const string ConstSpec = "const_spec";
        public const string VarSpec = "var_spec";
        public const string ShortVarDeclaration = "short_var_declaration";
        public const string SelectorExpression = "selector_expression";
        public const string CallExpression = "call_expression";
        public const string ParameterList = "parameter_list";
        public const string ParameterDeclaration = "parameter_declaration";
        public const string FieldDeclaration = "field_declaration";
        public const string FieldDeclarationList = "field_declaration_list";
        public const string ImportDeclaration = "import_declaration";
        public const string ImportSpec = "import_spec";
        public const string PackageClause = "package_clause";
        public const string InterpretedStringLiteral = "interpreted_string_literal";
        public const string RawStringLiteral = "raw_string_literal";
        public const string ArgumentList = "argument_list";
        public const string StringLiteral = "string_literal";
        public const string Identifier = "identifier";
        public const string ExpressionList = "expression_list";
        public const string Block = "block";
        public const string VariableName = "variable_name";
        public const string CompositeLiteral = "composite_literal";
    }
}

