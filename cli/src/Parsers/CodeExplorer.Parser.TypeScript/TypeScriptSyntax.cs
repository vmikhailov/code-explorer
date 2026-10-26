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
        public const string MemberExpression = "member_expression";
        public const string Arguments = "arguments";
        public const string ClassBody = "class_body";
        public const string StatementBlock = "statement_block";
        public const string Program = "program";
        public const string TemplateSubstitution = "template_substitution";
        public const string Object = "object";
        public const string Array = "array";
        public const string Property = "property";
        public const string Identifier = "identifier";
        public const string VariableName = "variable_name";
        public const string TaggedTemplateExpression = "tagged_template_expression";
        public const string EnumDeclaration = "enum_declaration";
        public const string Pair = "pair";
        public const string EnumBody = "enum_body";
        public const string EnumAssignment = "enum_assignment";
        public const string Function = "function";
        public const string GeneratorFunction = "generator_function";
        public const string Constructor = "constructor";
        public const string SubscriptExpression = "subscript_expression";
        public const string ReturnStatement = "return_statement";
    }
}

