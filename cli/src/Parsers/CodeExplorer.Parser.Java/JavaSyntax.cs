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

    public static class Java
    {
        public const string ClassDeclaration = "class_declaration";
        public const string RecordDeclaration = "record_declaration";
        public const string EnumDeclaration = "enum_declaration";
        public const string InterfaceDeclaration = "interface_declaration";
        public const string AnnotationTypeDeclaration = "annotation_type_declaration";
        public const string MethodDeclaration = "method_declaration";
        public const string ConstructorDeclaration = "constructor_declaration";
        public const string CompactConstructorDeclaration = "compact_constructor_declaration";
        public const string FieldDeclaration = "field_declaration";
        public const string ConstantDeclaration = "constant_declaration";
        public const string VariableDeclarator = "variable_declarator";
        public const string LocalVariableDeclaration = "local_variable_declaration";
        public const string FormalParameter = "formal_parameter";
        public const string SpreadParameter = "spread_parameter";
        public const string ReceiverParameter = "receiver_parameter";
        public const string FormalParameters = "formal_parameters";
        public const string PackageDeclaration = "package_declaration";
        public const string ImportDeclaration = "import_declaration";
        public const string MethodInvocation = "method_invocation";
        public const string ObjectCreationExpression = "object_creation_expression";
        public const string ExplicitConstructorInvocation = "explicit_constructor_invocation";
        public const string SuperConstructorInvocation = "super_constructor_invocation";
        public const string Superclass = "superclass";
        public const string SuperInterfaces = "super_interfaces";
        public const string ExtendsInterfaces = "extends_interfaces";
        public const string StringLiteral = "string_literal";
        public const string TextBlock = "text_block";
        public const string MarkerAnnotation = "marker_annotation";
        public const string Annotation = "annotation";
        public const string Modifiers = "modifiers";
        public const string ScopedIdentifier = "scoped_identifier";
        public const string TypeIdentifier = "type_identifier";
        public const string Identifier = "identifier";
        public const string ArgumentList = "argument_list";
        public const string Block = "block";
        public const string ClassBody = "class_body";
        public const string InterfaceBody = "interface_body";
        public const string EnumBody = "enum_body";
        public const string AnnotationTypeBody = "annotation_type_body";
        public const string ElementValuePair = "element_value_pair";
        public const string ElementValueArrayInitializer = "element_value_array_initializer";
        public const string EnumConstant = "enum_constant";
        public const string Modifier = "modifier";
        public const string ArrayAccess = "array_access";
    }
}

