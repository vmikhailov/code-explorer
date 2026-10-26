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

    public static class CSharp
    {
        public const string ClassDeclaration = "class_declaration";
        public const string InterfaceDeclaration = "interface_declaration";
        public const string StructDeclaration = "struct_declaration";
        public const string RecordDeclaration = "record_declaration";
        public const string RecordStructDeclaration = "record_struct_declaration";
        public const string EnumDeclaration = "enum_declaration";
        public const string MethodDeclaration = "method_declaration";
        public const string ConstructorDeclaration = "constructor_declaration";
        public const string PropertyDeclaration = "property_declaration";
        public const string FieldDeclaration = "field_declaration";
        public const string DeclarationList = "declaration_list";
        public const string LocalDeclarationStatement = "local_declaration_statement";
        public const string VariableDeclaration = "variable_declaration";
        public const string VariableDeclarator = "variable_declarator";
        public const string Parameter = "parameter";
        public const string ParameterList = "parameter_list";
        public const string AttributeList = "attribute_list";
        public const string Attribute = "attribute";
        public const string InvocationExpression = "invocation_expression";
        public const string MemberAccessExpression = "member_access_expression";
        public const string ObjectCreationExpression = "object_creation_expression";
        public const string InterpolatedStringExpression = "interpolated_string_expression";
        public const string InterpolatedVerbatimStringExpression = "interpolated_verbatim_string_expression";
        public const string InterpolatedRawStringExpression = "interpolated_raw_string_expression";
        public const string StringLiteral = "string_literal";
        public const string VerbatimStringLiteral = "verbatim_string_literal";
        public const string EqualsValueClause = "equals_value_clause";
        public const string ArrowExpressionClause = "arrow_expression_clause";
        public const string UsingStatement = "using_statement";
        public const string UsingDirective = "using_directive";
        public const string BaseList = "base_list";
        public const string TypeIdentifier = "type_identifier";
        public const string ExpressionStatement = "expression_statement";
        public const string GlobalStatement = "global_statement";
        public const string GenericName = "generic_name";
        public const string QualifiedName = "qualified_name";
        public const string Argument = "argument";
        public const string AttributeArgumentList = "attribute_argument_list";
        public const string AttributeArgument = "attribute_argument";
        public const string TypeArgumentList = "type_argument_list";
        public const string TypeParameterList = "type_parameter_list";
        public const string TypeParameter = "type_parameter";
        public const string Block = "block";
        public const string LocalFunctionStatement = "local_function_statement";
        public const string CompilationUnit = "compilation_unit";
        public const string VariableName = "variable_name";
        public const string Const = "const";
        public const string Readonly = "readonly";
        public const string EnumMemberDeclaration = "enum_member_declaration";
        public const string Modifier = "modifier";
        public const string Interpolation = "interpolation";
        public const string InterpolatedStringText = "interpolated_string_text";
        public const string ElementAccessExpression = "element_access_expression";
        public const string BracketedArgumentList = "bracketed_argument_list";
        public const string PredefinedType = "predefined_type";
        public const string NullableType = "nullable_type";
    }
}

