using System.Collections.Concurrent;
using System.Collections.Frozen;
using TreeSitter;

namespace CodeExplorer.Core.Parser.Syntax;

/// <summary>
/// Optional language-specific hook interface for customizing CST-to-Syntax conversion.
/// </summary>
public interface ILanguageSyntaxHooks
{
    void EnrichSyntaxNode(SyntaxNode node, SyntaxDocument document) { }
}

/// <summary>
/// Universal CST-to-Syntax builder.
/// This is the ONLY class in the entire CodeExplorer solution that interacts with native TreeSitter.Tree and TreeSitter.Node.
/// It parses source code into a strongly-typed <see cref="SyntaxDocument"/> hierarchy and immediately disposes the native Tree-sitter tree and parser.
/// </summary>
public static class UniversalSyntaxBuilder
{
    private static readonly ConcurrentDictionary<string, Language> LanguageCache = new(StringComparer.OrdinalIgnoreCase);

    private static Language GetLanguage(string languageName) =>
        LanguageCache.GetOrAdd(languageName, static name => new Language(name));

    private static readonly FrozenSet<string> NamespaceKinds = new[]
    {
        "namespace_declaration", "file_scoped_namespace_declaration",
        "package_declaration", "package_clause", "mod_item", "module", "internal_module"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ImportKinds = new[]
    {
        "using_directive", "import_statement", "import_from_statement", "import_declaration",
        "import_spec", "use_declaration", "extern_crate_declaration", "export_statement"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ClassKinds = new[]
    {
        "class_declaration", "class_definition", "class_specifier", "object_declaration", "companion_object"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> InterfaceKinds = new[]
    {
        "interface_declaration", "trait_item", "protocol_declaration"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> StructKinds = new[]
    {
        "struct_declaration", "struct_item", "struct_specifier", "type_spec"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> RecordKinds = new[]
    {
        "record_declaration", "record_struct_declaration"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> EnumKinds = new[]
    {
        "enum_declaration", "enum_item", "enum_specifier"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TypeAliasKinds = new[]
    {
        "type_alias_declaration", "type_item", "union_item"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ImplKinds = new[]
    {
        "impl_item"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> FunctionKinds = new[]
    {
        "method_declaration", "function_declaration", "function_definition", "function_item",
        "function_signature_item", "method_definition", "method_signature", "local_function_statement",
        "generator_function_declaration", "macro_definition"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ConstructorKinds = new[]
    {
        "constructor_declaration", "primary_constructor", "destructor_declaration"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> AccessorKinds = new[]
    {
        "accessor_declaration", "operator_declaration", "conversion_operator_declaration"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ParameterKinds = new[]
    {
        "parameter", "parameter_declaration", "required_parameter", "optional_parameter",
        "formal_parameter", "spread_parameter", "default_parameter", "typed_parameter",
        "typed_default_parameter", "self_parameter", "variadic_parameter", "receiver"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> MemberKinds = new[]
    {
        "field_declaration", "property_declaration", "event_declaration", "event_field_declaration",
        "public_field_definition", "property_signature", "enum_member_declaration", "enum_constant",
        "enum_variant", "enum_assignment", "const_item", "static_item", "const_spec"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> VariableKinds = new[]
    {
        "variable_declarator", "variable_declaration", "lexical_declaration", "local_declaration_statement",
        "let_declaration", "short_var_declaration", "var_spec", "var_declaration", "const_declaration",
        "global_variable_declaration"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> AnnotationKinds = new[]
    {
        "attribute", "attribute_item", "inner_attribute_item", "annotation", "marker_annotation", "decorator"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> CallKinds = new[]
    {
        "invocation_expression", "call_expression", "call", "method_invocation",
        "object_creation_expression", "implicit_object_creation_expression", "new_expression",
        "macro_invocation", "explicit_constructor_invocation", "super_constructor_invocation"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ArgumentKinds = new[]
    {
        "argument", "attribute_argument", "annotation_argument", "keyword_argument"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> StringLiteralKinds = new[]
    {
        "string_literal", "verbatim_string_literal", "raw_string_literal", "interpreted_string_literal",
        "string", "character_literal", "char_literal", "text_block"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> InterpolatedStringKinds = new[]
    {
        "interpolated_string_expression", "template_string", "format_string", "concatenated_string"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> NumberLiteralKinds = new[]
    {
        "integer_literal", "real_literal", "number", "integer", "float", "float_literal",
        "decimal_integer_literal", "hex_integer_literal", "int_literal"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> BoolOrNullLiteralKinds = new[]
    {
        "boolean_literal", "true", "false", "null_literal", "null", "none", "nil"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> IdentifierKinds = new[]
    {
        "identifier", "type_identifier", "field_identifier", "property_identifier",
        "variable_name", "simple_identifier", "shorthand_property_identifier",
        "this", "self", "super", "base"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> MemberAccessKinds = new[]
    {
        "member_access_expression", "member_expression", "field_expression",
        "selector_expression", "attribute", "scoped_identifier", "qualified_name"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> IndexAccessKinds = new[]
    {
        "element_access_expression", "subscript_expression", "subscript", "index_expression", "slice_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> BinaryKinds = new[]
    {
        "binary_expression", "binary_operator", "boolean_operator", "comparison_operator", "is_expression", "as_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> UnaryKinds = new[]
    {
        "prefix_unary_expression", "postfix_unary_expression", "unary_expression",
        " unary_operator", "not_operator", "reference_expression", "pointer_expression",
        "typeof_expression", "sizeof_expression", "nameof_expression", "update_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> AssignmentKinds = new[]
    {
        "assignment_expression", "assignment", "augmented_assignment", "assignment_statement",
        "compound_assignment_expr"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ConditionalKinds = new[]
    {
        "conditional_expression", "ternary_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> LambdaKinds = new[]
    {
        "lambda_expression", "arrow_function", "function_expression", "closure_expression",
        "func_literal", "lambda"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> AwaitOrTryKinds = new[]
    {
        "await_expression", "try_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> CollectionKinds = new[]
    {
        "array_creation_expression", "implicit_array_creation_expression", "collection_expression",
        "initializer_expression", "array", "object", "list", "dictionary", "set", "tuple",
        "array_expression", "tuple_expression", "struct_expression", "composite_literal", "array_initializer"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ComprehensionKinds = new[]
    {
        "query_expression", "list_comprehension", "dictionary_comprehension",
        "set_comprehension", "generator_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> JsxKinds = new[]
    {
        "jsx_element", "jsx_self_closing_element", "jsx_fragment"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> BlockStmtKinds = new[]
    {
        "block", "statement_block", "compound_statement", "unsafe_statement", "checked_statement"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> IfStmtKinds = new[]
    {
        "if_statement", "if_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> SwitchOrMatchKinds = new[]
    {
        "switch_statement", "switch_expression", "match_statement", "match_expression",
        "expression_switch_statement", "type_switch_statement", "select_statement"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> LoopStmtKinds = new[]
    {
        "for_statement", "for_each_statement", "for_in_statement", "enhanced_for_statement",
        "while_statement", "do_statement", "loop_expression", "while_expression", "for_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TryStmtKinds = new[]
    {
        "try_statement", "try_with_resources_statement", "try_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> CatchClauseKinds = new[]
    {
        "catch_clause", "except_clause", "recovery"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> UsingOrWithKinds = new[]
    {
        "using_statement", "with_statement"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> DeferOrGoKinds = new[]
    {
        "defer_statement", "go_statement"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ReturnOrYieldKinds = new[]
    {
        "return_statement", "yield_statement", "return_expression", "yield"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ThrowKinds = new[]
    {
        "throw_statement", "throw_expression", "raise_statement"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> BranchKinds = new[]
    {
        "break_statement", "continue_statement", "goto_statement", "fallthrough_statement",
        "break_expression", "continue_expression"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> LockKinds = new[]
    {
        "lock_statement", "synchronized_statement"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TypeRefKinds = new[]
    {
        "generic_name", "generic_type", "nullable_type", "array_type", "pointer_type",
        "reference_type", "tuple_type", "function_type", "predefined_type", "scoped_type_identifier",
        "qualified_type", "slice_type", "map_type", "channel_type", "union_type", "intersection_type"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Parses source text using Tree-sitter into a complete <see cref="SyntaxDocument"/>,
    /// immediately disposing the native <see cref="TreeSitter.Tree"/> and <see cref="TreeSitter.Parser"/>.
    /// </summary>
    public static SyntaxDocument Parse(
        string sourceText,
        string languageName,
        string filePath = "",
        string relativePath = "",
        LanguageSyntaxProfile? profile = null,
        ILanguageSyntaxHooks? hooks = null)
    {
        sourceText ??= string.Empty;
        var language = GetLanguage(languageName);
        using var parser = new TreeSitter.Parser(language);
        using var tree = parser.Parse(sourceText);

        var fullSpan = ComputeFullSpan(sourceText, tree?.RootNode);
        var doc = new SyntaxDocument
        {
            NodeId = 1,
            Span = fullSpan,
            RawGrammarKind = tree?.RootNode != null ? tree.RootNode.Type : "document",
            FilePath = filePath,
            RelativePath = relativePath,
            LanguageName = languageName,
            SourceText = sourceText
        };
        doc.Document = doc;

        if (tree?.RootNode == null)
        {
            return doc;
        }

        var nextNodeId = 2;
        var allTypes = new List<TypeDeclSyntax>();
        var allFunctions = new List<FunctionDeclSyntax>();
        var allMembers = new List<MemberDeclSyntax>();
        var allVariables = new List<VariableDeclSyntax>();
        var allCalls = new List<CallExprSyntax>();
        var imports = new List<ImportDeclSyntax>();
        var declarations = new List<DeclarationSyntax>();
        var topStatements = new List<StatementSyntax>();

        SyntaxNode ConvertNode(Node tsNode, SyntaxNode parent, string? fieldRole)
        {
            var id = nextNodeId++;
            var span = new TextSpan(
                tsNode.StartIndex,
                tsNode.EndIndex,
                tsNode.StartPosition.Row,
                tsNode.StartPosition.Column,
                tsNode.EndPosition.Row,
                tsNode.EndPosition.Column
            );
            var rawKind = tsNode.Type;
            var isNamed = tsNode.IsNamed;

            SyntaxNode syntaxNode = InstantiateTypedSyntaxNode(rawKind, isNamed, span, parent.RawGrammarKind, profile);
            syntaxNode.NodeId = id;
            syntaxNode.FieldRole = fieldRole;
            syntaxNode.Document = doc;
            syntaxNode.Parent = parent;

            var tsChildren = tsNode.Children;
            var childCount = tsChildren.Count;
            if (childCount > 0)
            {
                var convertedChildren = new List<SyntaxNode>(childCount);
                for (int i = 0; i < childCount; i++)
                {
                    var tsChild = tsChildren[i];
                    if (tsChild.Id == IntPtr.Zero) continue;
                    var childField = tsNode.GetFieldNameForChild(i);
                    var convertedChild = ConvertNode(tsChild, syntaxNode, childField);
                    convertedChild.ChildIndex = convertedChildren.Count;
                    convertedChildren.Add(convertedChild);
                }
                syntaxNode.Children = convertedChildren;
            }

            PopulateTypedProperties(syntaxNode, doc);
            hooks?.EnrichSyntaxNode(syntaxNode, doc);

            // Index into document fast collections
            switch (syntaxNode)
            {
                case TypeDeclSyntax td:
                    allTypes.Add(td);
                    break;
                case FunctionDeclSyntax fd:
                    allFunctions.Add(fd);
                    break;
                case MemberDeclSyntax md:
                    allMembers.Add(md);
                    break;
                case VariableDeclSyntax vd:
                    allVariables.Add(vd);
                    break;
                case CallExprSyntax ce:
                    allCalls.Add(ce);
                    break;
                case ImportDeclSyntax imp:
                    imports.Add(imp);
                    break;
            }

            return syntaxNode;
        }

        var rootTsNode = tree.RootNode;
        var rootChildren = rootTsNode.Children;
        if (rootChildren.Count > 0)
        {
            var docChildren = new List<SyntaxNode>(rootChildren.Count);
            for (int i = 0; i < rootChildren.Count; i++)
            {
                var tsChild = rootChildren[i];
                if (tsChild.Id == IntPtr.Zero) continue;
                var childField = rootTsNode.GetFieldNameForChild(i);
                var childSyntax = ConvertNode(tsChild, doc, childField);
                childSyntax.ChildIndex = docChildren.Count;
                docChildren.Add(childSyntax);

                if (childSyntax is DeclarationSyntax decl && decl is not ImportDeclSyntax)
                {
                    declarations.Add(decl);
                }
                else if (childSyntax is StatementSyntax stmt)
                {
                    topStatements.Add(stmt);
                }
            }
            doc.Children = docChildren;
        }

        doc.Imports = imports;
        doc.Declarations = declarations;
        doc.TopLevelStatements = topStatements;
        doc.AllTypes = allTypes;
        doc.AllFunctions = allFunctions;
        doc.AllMembers = allMembers;
        doc.AllVariables = allVariables;
        doc.AllCalls = allCalls;

        // Native TreeSitter.Tree and TreeSitter.Parser are disposed automatically here via `using`.
        return doc;
    }

    /// <summary>
    /// Creates a synthetic <see cref="SyntaxDocument"/> for non-Tree-sitter files (e.g. Kotlin, SQL, GraphQL, ColdFusion, XML)
    /// so that 100% of files across all projects carry a valid <see cref="SyntaxDocument"/>.
    /// </summary>
    public static SyntaxDocument CreateDocument(
        string sourceText,
        string languageName,
        string filePath = "",
        string relativePath = "",
        IReadOnlyList<SyntaxNode>? children = null)
    {
        sourceText ??= string.Empty;
        var span = ComputeFullSpan(sourceText, null);
        var doc = new SyntaxDocument
        {
            NodeId = 1,
            Span = span,
            RawGrammarKind = "document",
            FilePath = filePath,
            RelativePath = relativePath,
            LanguageName = languageName,
            SourceText = sourceText
        };
        doc.Document = doc;

        if (children is { Count: > 0 })
        {
            var nextId = 2;
            var allTypes = new List<TypeDeclSyntax>();
            var allFunctions = new List<FunctionDeclSyntax>();
            var allMembers = new List<MemberDeclSyntax>();
            var allVariables = new List<VariableDeclSyntax>();
            var allCalls = new List<CallExprSyntax>();
            var allSql = new List<SqlStmtSyntax>();
            var imports = new List<ImportDeclSyntax>();
            var declarations = new List<DeclarationSyntax>();

            void AttachRecursive(SyntaxNode node, SyntaxNode parent)
            {
                node.NodeId = nextId++;
                node.Document = doc;
                node.Parent = parent;
                switch (node)
                {
                    case TypeDeclSyntax td: allTypes.Add(td); break;
                    case FunctionDeclSyntax fd: allFunctions.Add(fd); break;
                    case MemberDeclSyntax md: allMembers.Add(md); break;
                    case VariableDeclSyntax vd: allVariables.Add(vd); break;
                    case CallExprSyntax ce: allCalls.Add(ce); break;
                    case SqlStmtSyntax sq: allSql.Add(sq); break;
                    case ImportDeclSyntax im: imports.Add(im); break;
                }
                for (int i = 0; i < node.Children.Count; i++)
                {
                    node.Children[i].ChildIndex = i;
                    AttachRecursive(node.Children[i], node);
                }
            }

            for (int i = 0; i < children.Count; i++)
            {
                var c = children[i];
                c.ChildIndex = i;
                AttachRecursive(c, doc);
                if (c is DeclarationSyntax d && d is not ImportDeclSyntax)
                {
                    declarations.Add(d);
                }
            }

            doc.Children = children;
            doc.Imports = imports;
            doc.Declarations = declarations;
            doc.AllTypes = allTypes;
            doc.AllFunctions = allFunctions;
            doc.AllMembers = allMembers;
            doc.AllVariables = allVariables;
            doc.AllCalls = allCalls;
            doc.AllSqlStatements = allSql;
        }

        return doc;
    }

    private static TextSpan ComputeFullSpan(string sourceText, Node? rootNode)
    {
        if (rootNode != null && rootNode.Id != IntPtr.Zero)
        {
            return new TextSpan(
                rootNode.StartIndex,
                rootNode.EndIndex,
                rootNode.StartPosition.Row,
                rootNode.StartPosition.Column,
                rootNode.EndPosition.Row,
                rootNode.EndPosition.Column
            );
        }

        int lines = 0;
        int lastLineStart = 0;
        for (int i = 0; i < sourceText.Length; i++)
        {
            if (sourceText[i] == '\n')
            {
                lines++;
                lastLineStart = i + 1;
            }
        }
        return new TextSpan(0, sourceText.Length, 0, 0, lines, sourceText.Length - lastLineStart);
    }

    private static SyntaxNode InstantiateTypedSyntaxNode(
        string rawKind,
        bool isNamed,
        TextSpan span,
        string parentKind,
        LanguageSyntaxProfile? profile)
    {
        if (!isNamed)
        {
            return new TokenSyntax { Span = span, RawGrammarKind = rawKind, IsNamed = false };
        }

        // Note: In Python, "attribute" is a MemberAccessExpr (obj.attr), whereas in C#/Rust it is an Annotation.
        if (rawKind == "attribute" && parentKind is not "attribute_list")
        {
            if (profile != null && profile.MemberAccessExpressions.Contains("attribute"))
            {
                return new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind };
            }
        }

        if (CallKinds.Contains(rawKind) || (profile != null && profile.Calls.Contains(rawKind)))
        {
            var callKind = rawKind is "object_creation_expression" or "implicit_object_creation_expression" or "new_expression"
                ? CallSyntaxKind.Constructor
                : rawKind == "macro_invocation"
                    ? CallSyntaxKind.Macro
                    : CallSyntaxKind.Method;
            return new CallExprSyntax { Span = span, RawGrammarKind = rawKind, CallKind = callKind };
        }

        if (ClassKinds.Contains(rawKind) || (profile != null && profile.ClassDeclarations.Contains(rawKind)))
        {
            var kind = rawKind switch
            {
                "object_declaration" => TypeSyntaxKind.ObjectSingleton,
                "companion_object" => TypeSyntaxKind.CompanionObject,
                _ => TypeSyntaxKind.Class
            };
            return new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = kind };
        }

        if (InterfaceKinds.Contains(rawKind) || (profile != null && profile.InterfaceDeclarations.Contains(rawKind)))
        {
            var kind = rawKind == "trait_item" ? TypeSyntaxKind.Trait : TypeSyntaxKind.Interface;
            return new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = kind };
        }

        if (StructKinds.Contains(rawKind) || (profile != null && profile.StructDeclarations.Contains(rawKind)))
        {
            return new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Struct };
        }

        if (RecordKinds.Contains(rawKind))
        {
            return new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Record };
        }

        if (EnumKinds.Contains(rawKind) || (profile != null && profile.EnumDeclarations.Contains(rawKind)))
        {
            return new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = TypeSyntaxKind.Enum };
        }

        if (TypeAliasKinds.Contains(rawKind))
        {
            var kind = rawKind == "union_item" ? TypeSyntaxKind.Union : TypeSyntaxKind.TypeAlias;
            return new TypeDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = kind };
        }

        if (ImplKinds.Contains(rawKind))
        {
            return new ImplDeclSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (FunctionKinds.Contains(rawKind) ||
            (profile != null && (profile.MethodDeclarations.Contains(rawKind) || profile.FunctionDeclarations.Contains(rawKind))))
        {
            var fnKind = rawKind == "macro_definition"
                ? FunctionSyntaxKind.Macro
                : rawKind.StartsWith("method", StringComparison.Ordinal)
                    ? FunctionSyntaxKind.Method
                    : FunctionSyntaxKind.Function;
            return new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = fnKind };
        }

        if (ConstructorKinds.Contains(rawKind))
        {
            var fnKind = rawKind == "destructor_declaration" ? FunctionSyntaxKind.Destructor : FunctionSyntaxKind.Constructor;
            return new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = fnKind };
        }

        if (AccessorKinds.Contains(rawKind))
        {
            var fnKind = rawKind.Contains("operator", StringComparison.Ordinal) ? FunctionSyntaxKind.Operator : FunctionSyntaxKind.Accessor;
            return new FunctionDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = fnKind };
        }

        if (ParameterKinds.Contains(rawKind) || (profile != null && profile.Parameters.Contains(rawKind)))
        {
            return new ParameterDeclSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (MemberKinds.Contains(rawKind))
        {
            var mKind = rawKind.Contains("enum", StringComparison.Ordinal)
                ? MemberSyntaxKind.EnumMember
                : rawKind.Contains("property", StringComparison.Ordinal)
                    ? MemberSyntaxKind.Property
                    : rawKind.Contains("event", StringComparison.Ordinal)
                        ? MemberSyntaxKind.Event
                        : rawKind.StartsWith("const", StringComparison.Ordinal)
                            ? MemberSyntaxKind.Constant
                            : MemberSyntaxKind.Field;
            return new MemberDeclSyntax { Span = span, RawGrammarKind = rawKind, Kind = mKind };
        }

        if (VariableKinds.Contains(rawKind) ||
            (profile != null && (profile.VariableDeclarations.Contains(rawKind) || profile.ConstantDeclarations.Contains(rawKind))))
        {
            return new VariableDeclSyntax
            {
                Span = span,
                RawGrammarKind = rawKind,
                IsConstant = rawKind.StartsWith("const", StringComparison.Ordinal) || (profile != null && profile.ConstantDeclarations.Contains(rawKind))
            };
        }

        if (NamespaceKinds.Contains(rawKind) || (profile != null && profile.PackageDeclarationNodeTypes.Contains(rawKind)))
        {
            return new NamespaceDeclSyntax
            {
                Span = span,
                RawGrammarKind = rawKind,
                IsFileScoped = rawKind == "file_scoped_namespace_declaration"
            };
        }

        if (ImportKinds.Contains(rawKind) || (profile != null && profile.Imports.Contains(rawKind)))
        {
            return new ImportDeclSyntax { Span = span, RawGrammarKind = rawKind, IsReExport = rawKind == "export_statement" };
        }

        if (AnnotationKinds.Contains(rawKind) || (profile != null && profile.AnnotationNodeTypes.Contains(rawKind)))
        {
            return new AnnotationSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (ArgumentKinds.Contains(rawKind))
        {
            return new ArgumentSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (InterpolatedStringKinds.Contains(rawKind) || (profile != null && profile.StringInterpolations.Contains(rawKind)))
        {
            return new InterpolatedStringExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (StringLiteralKinds.Contains(rawKind) || (profile != null && profile.StringLiterals.Contains(rawKind)))
        {
            var litKind = rawKind.Contains("raw", StringComparison.Ordinal) || rawKind.Contains("verbatim", StringComparison.Ordinal)
                ? LiteralSyntaxKind.RawString
                : rawKind.Contains("char", StringComparison.Ordinal)
                    ? LiteralSyntaxKind.Char
                    : LiteralSyntaxKind.String;
            return new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = litKind };
        }

        if (NumberLiteralKinds.Contains(rawKind))
        {
            var litKind = rawKind.Contains("float", StringComparison.Ordinal) || rawKind.Contains("real", StringComparison.Ordinal)
                ? LiteralSyntaxKind.Float
                : LiteralSyntaxKind.Integer;
            return new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = litKind };
        }

        if (BoolOrNullLiteralKinds.Contains(rawKind))
        {
            var litKind = rawKind is "null_literal" or "null" or "none" or "nil"
                ? LiteralSyntaxKind.Null
                : LiteralSyntaxKind.Boolean;
            return new LiteralExprSyntax { Span = span, RawGrammarKind = rawKind, LiteralKind = litKind };
        }

        if (IdentifierKinds.Contains(rawKind) || (profile != null && profile.IdentifierChildTypes.Contains(rawKind)))
        {
            return new IdentifierExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (MemberAccessKinds.Contains(rawKind) || (profile != null && profile.MemberAccessExpressions.Contains(rawKind)))
        {
            return new MemberAccessExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (IndexAccessKinds.Contains(rawKind) || (profile != null && profile.ElementAccessExpressions.Contains(rawKind)))
        {
            return new IndexOrSliceExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (BinaryKinds.Contains(rawKind) || (profile != null && profile.BinaryExpressions.Contains(rawKind)))
        {
            return new BinaryExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (UnaryKinds.Contains(rawKind))
        {
            return new UnaryExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (AssignmentKinds.Contains(rawKind))
        {
            return new AssignmentExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (ConditionalKinds.Contains(rawKind))
        {
            return new ConditionalExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (LambdaKinds.Contains(rawKind))
        {
            return new LambdaExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (AwaitOrTryKinds.Contains(rawKind))
        {
            return new AwaitOrTryExprSyntax { Span = span, RawGrammarKind = rawKind, IsTryPropagate = rawKind == "try_expression" };
        }

        if (CollectionKinds.Contains(rawKind))
        {
            return new CollectionExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (ComprehensionKinds.Contains(rawKind))
        {
            return new QueryOrComprehensionExprSyntax { Span = span, RawGrammarKind = rawKind, IsLinq = rawKind == "query_expression" };
        }

        if (JsxKinds.Contains(rawKind))
        {
            return new JsxElementExprSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (BlockStmtKinds.Contains(rawKind))
        {
            return new BlockStmtSyntax { Span = span, RawGrammarKind = rawKind, IsUnsafe = rawKind == "unsafe_statement" };
        }

        if (IfStmtKinds.Contains(rawKind))
        {
            return new IfStmtSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (SwitchOrMatchKinds.Contains(rawKind))
        {
            return new SwitchOrMatchStmtSyntax { Span = span, RawGrammarKind = rawKind, IsChannelSelect = rawKind == "select_statement" };
        }

        if (LoopStmtKinds.Contains(rawKind))
        {
            return new LoopStmtSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (TryStmtKinds.Contains(rawKind))
        {
            return new TryStmtSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (CatchClauseKinds.Contains(rawKind) || (profile != null && profile.CatchClauses.Contains(rawKind)))
        {
            return new CatchClauseSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (UsingOrWithKinds.Contains(rawKind))
        {
            return new UsingOrWithStmtSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (DeferOrGoKinds.Contains(rawKind))
        {
            return new DeferOrGoStmtSyntax { Span = span, RawGrammarKind = rawKind, IsGoroutine = rawKind == "go_statement" };
        }

        if (ReturnOrYieldKinds.Contains(rawKind))
        {
            return new ReturnOrYieldStmtSyntax { Span = span, RawGrammarKind = rawKind, IsYield = rawKind.Contains("yield", StringComparison.Ordinal) };
        }

        if (ThrowKinds.Contains(rawKind))
        {
            return new ThrowStmtSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (BranchKinds.Contains(rawKind))
        {
            return new BranchStmtSyntax { Span = span, RawGrammarKind = rawKind, BranchKind = rawKind };
        }

        if (LockKinds.Contains(rawKind))
        {
            return new LockStmtSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (TypeRefKinds.Contains(rawKind))
        {
            return new TypeRefSyntax { Span = span, RawGrammarKind = rawKind };
        }

        if (rawKind == "expression_statement")
        {
            return new ExprStmtSyntax { Span = span, RawGrammarKind = rawKind };
        }

        return new UnmappedSyntax { Span = span, RawGrammarKind = rawKind };
    }

    private static void PopulateTypedProperties(SyntaxNode node, SyntaxDocument doc)
    {
        switch (node)
        {
            case IdentifierExprSyntax idExpr:
                idExpr.Name = idExpr.Text;
                break;

            case LiteralExprSyntax litExpr:
                var hasInterp = false;
                for (int i = 0; i < litExpr.Children.Count; i++)
                {
                    if (litExpr.Children[i].RawGrammarKind is "interpolation" or "template_substitution")
                    {
                        hasInterp = true;
                        break;
                    }
                }
                litExpr.ValueText = litExpr.LiteralKind is LiteralSyntaxKind.String or LiteralSyntaxKind.RawString or LiteralSyntaxKind.Char
                    ? AstValueResolver.Unquote(litExpr.Text)
                    : litExpr.Text;
                if (!hasInterp)
                {
                    litExpr.ResolvedValue = litExpr.ValueText;
                }
                break;

            case DeclarationSyntax decl:
                var nameChild = decl.GetChildForField("name") ??
                                decl.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                if (nameChild != null)
                {
                    decl.Name = nameChild.Text;
                }
                if (decl is NamespaceDeclSyntax ns && !string.IsNullOrEmpty(ns.Name) && string.IsNullOrEmpty(doc.PackageOrNamespace))
                {
                    doc.PackageOrNamespace = ns.Name;
                }
                break;

            case CallExprSyntax callExpr:
                var fnNode = callExpr.GetChildForField("function") ??
                             callExpr.GetChildForField("name") ??
                             callExpr.GetChildForField("macro") ??
                             callExpr.GetChildForField("type");
                if (fnNode is ExprSyntax fnExpr)
                {
                    callExpr.Callee = fnExpr;
                }
                if (fnNode is MemberAccessExprSyntax memberCallee)
                {
                    callExpr.Receiver = memberCallee.Object;
                    callExpr.MethodName = memberCallee.MemberName;
                }
                else if (fnNode != null)
                {
                    callExpr.MethodName = fnNode.Text;
                }
                break;

            case MemberAccessExprSyntax memExpr:
                var objNode = memExpr.GetChildForField("object") ??
                              memExpr.GetChildForField("value") ??
                              memExpr.GetChildForField("operand") ??
                              memExpr.GetChildForField("expression") ??
                              memExpr.GetChildForField("path");
                var propNode = memExpr.GetChildForField("property") ??
                               memExpr.GetChildForField("field") ??
                               memExpr.GetChildForField("name") ??
                               memExpr.GetChildForField("attribute");
                if (objNode is ExprSyntax objExpr) memExpr.Object = objExpr;
                if (propNode != null) memExpr.MemberName = propNode.Text;
                break;

            case BinaryExprSyntax binExpr:
                if (binExpr.GetChildForField("left") is ExprSyntax leftExpr) binExpr.Left = leftExpr;
                if (binExpr.GetChildForField("right") is ExprSyntax rightExpr) binExpr.Right = rightExpr;
                var opNode = binExpr.GetChildForField("operator");
                if (opNode != null) binExpr.Operator = opNode.Text;
                break;
        }
    }
}
