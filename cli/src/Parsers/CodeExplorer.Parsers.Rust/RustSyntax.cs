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

    public static class Rust
    {
        // Item Declarations
        public const string SourceFile = "source_file";
        public const string ModItem = "mod_item";
        public const string StructItem = "struct_item";
        public const string EnumItem = "enum_item";
        public const string EnumVariant = "enum_variant";
        public const string EnumVariantList = "enum_variant_list";
        public const string UnionItem = "union_item";
        public const string TraitItem = "trait_item";
        public const string ImplItem = "impl_item";
        public const string FunctionItem = "function_item";
        public const string FunctionSignatureItem = "function_signature_item";
        public const string MacroDefinition = "macro_definition";
        public const string MacroInvocation = "macro_invocation";
        public const string ConstItem = "const_item";
        public const string StaticItem = "static_item";
        public const string TypeItem = "type_item";
        public const string UseDeclaration = "use_declaration";
        public const string ExternCrateDeclaration = "extern_crate_declaration";
        public const string ForeignModItem = "foreign_mod_item";

        // Structural Elements & Bodies
        public const string DeclarationList = "declaration_list";
        public const string FieldDeclarationList = "field_declaration_list";
        public const string FieldDeclaration = "field_declaration";
        public const string OrderedFieldDeclarationList = "ordered_field_declaration_list";
        public const string Parameters = "parameters";
        public const string Parameter = "parameter";
        public const string SelfParameter = "self_parameter";
        public const string Block = "block";
        public const string TypeParameters = "type_parameters";
        public const string WhereClause = "where_clause";
        public const string VisibilityModifier = "visibility_modifier";
        public const string AttributeItem = "attribute_item";
        public const string InnerAttributeItem = "inner_attribute_item";

        // Expressions & Calls
        public const string CallExpression = "call_expression";
        public const string FieldExpression = "field_expression";
        public const string AwaitExpression = "await_expression";
        public const string TryExpression = "try_expression";
        public const string ClosureExpression = "closure_expression";
        public const string MatchExpression = "match_expression";
        public const string LetDeclaration = "let_declaration";
        public const string FieldInitializer = "field_initializer";
        public const string AssignmentExpression = "assignment_expression";
        public const string BinaryExpression = "binary_expression";
        public const string UnaryExpression = "unary_expression";
        public const string ReturnExpression = "return_expression";
        public const string GenericFunction = "generic_function";

        // Literals & Identifiers
        public const string Identifier = "identifier";
        public const string TypeIdentifier = "type_identifier";
        public const string FieldIdentifier = "field_identifier";
        public const string ScopedIdentifier = "scoped_identifier";
        public const string ScopedTypeIdentifier = "scoped_type_identifier";
        public const string StringLiteral = "string_literal";
        public const string RawStringLiteral = "raw_string_literal";
        public const string IntegerLiteral = "integer_literal";
        public const string BooleanLiteral = "boolean_literal";

        // Fields
        public const string Trait = "trait";
        public const string ReturnType = "return_type";
        public const string GenericType = "generic_type";
    }
}

