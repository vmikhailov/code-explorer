namespace CodeExplorer.Core.Parser;

/// <summary>
/// Centralized strongly-typed Tree-sitter AST node types and field names.
/// Prevents hardcoded strings across file visitors and library parsers.
/// </summary>
public static class TreeSitterSyntax
{
    public static class Fields
    {
        public const string Name = "name";
        public const string Function = "function";
        public const string Type = "type";
        public const string Value = "value";
        public const string Left = "left";
        public const string Right = "right";
        public const string Body = "body";
        public const string Arguments = "arguments";
        public const string Parameters = "parameters";
        public const string Return = "return";
        public const string Operator = "operator";
        public const string Expression = "expression";
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
        public const string Comment = "comment";
    }

    public static class CSharp
    {
        public const string ClassDeclaration = "class_declaration";
        public const string InterfaceDeclaration = "interface_declaration";
        public const string StructDeclaration = "struct_declaration";
        public const string RecordDeclaration = "record_declaration";
        public const string EnumDeclaration = "enum_declaration";
        public const string MethodDeclaration = "method_declaration";
        public const string ConstructorDeclaration = "constructor_declaration";
        public const string PropertyDeclaration = "property_declaration";
        public const string FieldDeclaration = "field_declaration";
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
        public const string EqualsValueClause = "equals_value_clause";
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
        public const string Block = "block";
        public const string LocalFunctionStatement = "local_function_statement";
        public const string CompilationUnit = "compilation_unit";
        public const string VariableName = "variable_name";
        public const string Const = "const";
        public const string Readonly = "readonly";
    }

    public static class TypeScript
    {
        public const string ClassDeclaration = "class_declaration";
        public const string ClassExpression = "class_expression";
        public const string InterfaceDeclaration = "interface_declaration";
        public const string TypeAliasDeclaration = "type_alias_declaration";
        public const string MethodDefinition = "method_definition";
        public const string FunctionDeclaration = "function_declaration";
        public const string FunctionExpression = "function_expression";
        public const string ArrowFunction = "arrow_function";
        public const string LexicalDeclaration = "lexical_declaration";
        public const string VariableDeclaration = "variable_declaration";
        public const string VariableDeclarator = "variable_declarator";
        public const string PublicFieldDefinition = "public_field_definition";
        public const string PropertyDefinition = "property_definition";
        public const string Parameter = "parameter";
        public const string RequiredParameter = "required_parameter";
        public const string OptionalParameter = "optional_parameter";
        public const string ParameterProperty = "parameter_property";
        public const string TypeAnnotation = "type_annotation";
        public const string Decorator = "decorator";
        public const string TemplateString = "template_string";
        public const string String = "string";
        public const string PropertyIdentifier = "property_identifier";
        public const string ExtendsClause = "extends_clause";
        public const string ImplementsClause = "implements_clause";
        public const string NewExpression = "new_expression";
        public const string CallExpression = "call_expression";
        public const string ExportStatement = "export_statement";
        public const string ImportStatement = "import_statement";
        public const string AssignmentExpression = "assignment_expression";
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
    }
}
