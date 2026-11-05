using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

public abstract partial class BaseParserVisitor : TreeSitterAstVisitor
{

    protected override void VisitClassDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitInterfaceDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitMethodDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitFunctionDeclaration(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        if (Profile.VariableDeclaratorNodeTypes.Count > 0)
        {
            CollectProfileVariables(node);
        }
        VisitSymbolOrBase(node, depth);
    }

    protected virtual void CollectProfileVariables(Node node)
    {
        var typeNode = node.GetField(SyntaxFields.Type);
        if (!typeNode.IsValid())
        {
            foreach (var child in node.Children)
            {
                if (child.IsValid() && Profile.IdentifierChildTypes.Contains(child.Type))
                {
                    typeNode = child;
                    break;
                }
            }
        }

        var typeName = typeNode.IsValid() ? typeNode.Text : "var";
        var isConstant = Profile.ConstantDeclarations.Contains(node.Type) || HasProfileConstantModifier(node);
        var scope = SymbolStack.Peek().Kind switch
        {
            CodeExplorer.Cypher.Common.SyntacticSymbolKind.Class or CodeExplorer.Cypher.Common.SyntacticSymbolKind.Interface or CodeExplorer.Cypher.Common.SyntacticSymbolKind.Struct => "class",
            _ => "local"
        };

        foreach (var declarator in node.Children)
        {
            if (declarator.IsValid() && Profile.VariableDeclaratorNodeTypes.Contains(declarator.Type))
            {
                var varName = ExtractProfileIdentifier(declarator);
                if (!string.IsNullOrEmpty(varName))
                {
                    var valNode = declarator.GetField(SyntaxFields.Value);
                    var initText = valNode.IsValid() ? valNode.GetBoundedInitializerText() : "";

                    RawVariables.Add(new RawVariable(
                        varName,
                        initText,
                        scope,
                        isConstant,
                        "",
                        declarator.StartPosition.Row + 1,
                        declarator.EndPosition.Row + 1,
                        declarator.StartPosition.Column + 1,
                        declarator.EndPosition.Column + 1
                    ));

                    RawTypeBindings.Add(new RawTypeBinding(varName, typeName, "", SymbolStack.Peek().Name));
                }
            }
        }
    }

    protected bool HasProfileConstantModifier(Node node)
    {
        if (Profile.ConstantModifierKeywords.Count == 0) return false;
        foreach (var child in node.Children)
        {
            if (!child.IsValid()) continue;
            if (Profile.ConstantModifierKeywords.Contains(child.Text)) return true;
            if (Profile.AnnotationContainerNodeTypes.Contains(child.Type))
            {
                foreach (var mod in child.Children)
                {
                    if (mod.IsValid() && Profile.ConstantModifierKeywords.Contains(mod.Text))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    protected override void VisitParameter(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        if (Profile.ImportIdentifierChildTypes.Count > 0 && !Profile.PackageDeclarationNodeTypes.Contains(node.Type))
        {
            CollectProfileImport(node);
        }
        VisitSymbolOrBase(node, depth);
    }

    protected virtual void CollectProfileImport(Node node)
    {
        string? importText = null;
        foreach (var pf in Profile.ImportPathFields)
        {
            var fNode = node.GetField(pf);
            if (fNode.IsValid())
            {
                importText = CleanStringLiteral(fNode.Text);
                break;
            }
        }

        if (string.IsNullOrEmpty(importText))
        {
            foreach (var child in node.Children)
            {
                if (child.IsValid() && Profile.ImportIdentifierChildTypes.Contains(child.Type))
                {
                    importText = CleanStringLiteral(child.Text);
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(importText))
        {
            var importType = FileParser.ResolveImportType(importText, RelativePath, AbsoluteWorkspacePath);
            RawImports.Add(new RawImport(importText, "", importType));
            ResolveAndInjectLibraryParser(importText);
        }
    }

    protected override void VisitInheritanceClause(Node node, int depth)
    {
        var currentScope = SymbolStack.Peek();
        if (currentScope.Kind != CodeExplorer.Cypher.Common.SyntacticSymbolKind.File)
        {
            foreach (var child in node.Children)
            {
                if (child.IsValid() && (child.Type.Contains("identifier", StringComparison.Ordinal) || child.Type.Contains("name", StringComparison.Ordinal)))
                {
                    var baseName = child.Text;
                    var refKind = baseName.StartsWith('I') && baseName.Length > 1 && char.IsUpper(baseName[1])
                        ? "IMPLEMENTS"
                        : "INHERITS";
                    currentScope.References.Add(new Reference("", baseName, refKind));
                }
            }
        }
        base.VisitInheritanceClause(node, depth);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool MayContainSymbolsOrReferences(Node node)
    {
        var type = node.Type;
        if (type is "identifier" or "type_identifier" or "predefined_type" or "generic_name" or "qualified_name"
            or "block" or "statement_block" or "argument_list" or "parameter_list" or "type_argument_list"
            or "equals_value_clause" or "variable_declarator" or "return_statement" or "if_statement"
            or "for_statement" or "foreach_statement" or "while_statement" or "binary_expression"
            or "parenthesized_expression" or "assignment_expression" or "member_access_expression"
            or "cast_expression" or "conditional_expression" or "empty_statement")
        {
            return false;
        }
        return true;
    }

    protected override void VisitDefault(Node node, int depth)
    {
        if (!MayContainSymbolsOrReferences(node))
        {
            VisitChildren(node, depth);
            return;
        }

        VisitSymbolOrBase(node, depth);
    }

    protected override void VisitStringLiteral(Node node, int depth)
    {
        var currentScope = SymbolStack.Peek();
        if (currentScope.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Query)
        {
            var cleanedLiteral = CleanStringLiteral(node.Text);
            if ((string.IsNullOrEmpty(currentScope.Text) || !NestedSqlParser.TryParseSql(currentScope.Text, out _, out _)) &&
                NestedSqlParser.TryParseSql(cleanedLiteral, out _, out _))
            {
                currentScope.Text = cleanedLiteral;
            }
            if (!WorkspaceConventions.IsFrontendFilePath(RelativePath))
            {
                NestedSqlParser.TryDetectSqlDependencies(cleanedLiteral, "", currentScope.References);
                var enclosingFunc = SymbolStack.FirstOrDefault(s => s.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function);
                if (enclosingFunc != null)
                {
                    foreach (var r in currentScope.References)
                    {
                        if (r.Kind == OntologyConstants.Relationships.DependsOn && !enclosingFunc.References.Contains(r))
                        {
                            enclosingFunc.References.Add(r);
                        }
                    }
                }
            }
            base.VisitStringLiteral(node, depth);
            return;
        }

        if (!WorkspaceConventions.IsFrontendFilePath(RelativePath) && NestedSqlParser.TryParseSql(node.Text, out _, out _))
        {
            var name = "Query";

            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                name = $"{firstWord} Query";
            }

            VisitSymbolNode(node, OntologyConstants.NodeLabels.Query, name, depth,
                () => base.VisitStringLiteral(node, depth));
        }
        else
        {
            if (currentScope.Kind != CodeExplorer.Cypher.Common.SyntacticSymbolKind.File && !WorkspaceConventions.IsFrontendFilePath(RelativePath))
            {
                NestedSqlParser.TryDetectSqlDependencies(node.Text, "", currentScope.References);
            }

            base.VisitStringLiteral(node, depth);
        }
    }

    protected override void VisitCallExpression(Node node, int depth)
    {
        VisitSymbolOrBase(node, depth, () =>
        {
            var currentScope = SymbolStack.Peek();

            foreach (var libParser in LibraryParsers)
            {
                libParser.CollectReferences(node, "", currentScope.References, Context!);
            }

            var callName = FindCallName(node);

            if (!string.IsNullOrEmpty(callName) && currentScope.Kind != CodeExplorer.Cypher.Common.SyntacticSymbolKind.File)
            {
                currentScope.References.Add(new Reference("", callName, "CALLS",
                    node.StartPosition.Row + 1,
                    node.StartPosition.Column + 1));
            }


            CollectCallLiteralArguments(node, callName, currentScope);

            base.VisitCallExpression(node, depth);
        });
    }

    protected virtual void CollectCallLiteralArguments(Node callNode, string? callName, SyntacticSymbol currentScope)
    {
        if (IsTestFile)
        {
            return;
        }

        callName ??= FindCallName(callNode);
        var (role, isDecorator) = CallSemanticClassifier.Classify(callNode, callName);

        var argsNode = callNode.GetField(SyntaxFields.Arguments) ??
                       callNode.FindChildOfType("argument_list") ??
                       callNode.FindChildOfType("arguments") ??
                       callNode.FindChildOfType("attribute_argument_list");

        if (!argsNode.IsValid())
        {
            argsNode = callNode.Children.FirstOrDefault(c => c.Type.Contains("argument"));
        }

        if (!argsNode.IsValid()) return;

        var argIndex = 0;
        foreach (var child in argsNode.Children)
        {
            if (child.Type is "(" or ")" or "," or ";" or "{" or "}" or "[" or "]")
            {
                continue;
            }

            var exprNode = child;
            if (child.Type is "argument" or "attribute_argument")
            {
                var valueCandidate = child.Children.LastOrDefault(c => c.Type is not "name_colon" and not ":" and not ",");
                if (valueCandidate.IsValid())
                {
                    exprNode = valueCandidate;
                }
            }

            if (IsStringLiteralNode(exprNode))
            {
                var cleaned = CleanStringLiteral(exprNode.Text);

                if (!string.IsNullOrWhiteSpace(cleaned) &&
                    !NestedSqlParser.TryParseSql(cleaned, out _, out _))
                {
                    var displayName = cleaned.Length > 80 ? cleaned[..77] + "..." : cleaned;

                    var literalSymbol = new SyntacticSymbol(CodeExplorer.Cypher.Common.SyntacticSymbolKind.CallLiteral, displayName, exprNode)
                    {
                        Text = cleaned,
                        Callee = callName ?? "",
                        ArgIndex = argIndex,
                        Role = role,
                        IsDecorator = isDecorator
                    };

                    currentScope.Children.Add(literalSymbol);
                }
            }

            argIndex++;
        }
    }

    protected override void VisitAttribute(Node node, int depth)
    {
        var attrName = node.Children.FirstOrDefault(c => c.Type.Contains("identifier"))?.Text ?? node.Text;
        if (!string.IsNullOrEmpty(attrName))
        {
            var currentScope = SymbolStack.Peek();
            CollectCallLiteralArguments(node, attrName, currentScope);
        }
        base.VisitAttribute(node, depth);
    }

    protected override void VisitCatchClause(Node node, int depth)
    {
        if (IsTestFile)
        {
            VisitChildren(node, depth);
            return;
        }

        string? exType = null;
        Node? bodyBlock = null;

        foreach (var child in node.Children)
        {
            var cType = child.Type;
            if (cType is "catch_declaration" or "catch_formal_parameter")
            {
                foreach (var grandChild in child.Children)
                {
                    if (grandChild.Type is "type_identifier" or "identifier" or "qualified_name" or "generic_name")
                    {
                        exType = grandChild.Text;
                        break;
                    }
                }
            }
            else if (cType is "block" or "statement_block")
            {
                bodyBlock = child;
            }
            else if (cType is "identifier" && exType == null)
            {
                exType = child.Text;
            }
            else if (cType is "as_pattern")
            {
                var first = child.Children.FirstOrDefault();
                if (first != null && first.IsValid()) exType = first.Text;
            }
        }

        var statementCount = 0;
        var hasComment = false;

        if (bodyBlock != null && bodyBlock.IsValid())
        {
            var text = bodyBlock.Text;
            if (text.Contains("//") || text.Contains("/*") || text.Contains("#"))
            {
                hasComment = true;
            }

            foreach (var child in bodyBlock.Children)
            {
                var cType = child.Type;
                if (cType is "{" or "}" or ":" or ";")
                {
                    continue;
                }

                if (cType.Contains("comment"))
                {
                    hasComment = true;
                    continue;
                }

                // In Python, pass_statement is the syntactical placeholder for an empty block
                if (cType == "pass_statement")
                {
                    continue;
                }

                statementCount++;
            }
        }

        var isEmpty = statementCount == 0;
        var currentScope = SymbolStack.Peek();
        var catchSymbol = new SyntacticSymbol(CodeExplorer.Cypher.Common.SyntacticSymbolKind.CatchClause, exType ?? "catch", node)
        {
            RequestType = exType ?? "",
            IsEmptyCatch = isEmpty,
            HasCatchComment = hasComment,
            ArgIndex = statementCount
        };

        currentScope.Children.Add(catchSymbol);

        VisitChildren(node, depth);
    }

    protected bool IsStringLiteralNode(Node node)
    {
        if (!node.IsValid()) return false;
        var type = node.Type;

        if (Profile.StringLiterals.Contains(type)) return true;

        if (type is "string_literal" or "verbatim_string_literal" or "raw_string_literal" or "string" or "character_literal")
        {
            return true;
        }

        return false;
    }

    protected static string CleanStringLiteral(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var s = text.Trim();

        if (s.StartsWith("r#\"", StringComparison.Ordinal) && s.EndsWith("\"#", StringComparison.Ordinal) && s.Length >= 5)
        {
            return s[3..^2];
        }
        if (s.StartsWith("r##\"", StringComparison.Ordinal) && s.EndsWith("\"##", StringComparison.Ordinal) && s.Length >= 7)
        {
            return s[4..^3];
        }
        if (s.StartsWith("\"\"\"") && s.EndsWith("\"\"\"") && s.Length >= 6)
        {
            return s[3..^3].Trim('\r', '\n');
        }
        if (s.StartsWith("@\"") && s.EndsWith("\"") && s.Length >= 3)
        {
            return s[2..^1].Replace("\"\"", "\"");
        }
        if (s.StartsWith("$\"") && s.EndsWith("\"") && s.Length >= 3)
        {
            return s[2..^1];
        }
        if (s.StartsWith('"') && s.EndsWith('"') && s.Length >= 2)
        {
            return s[1..^1];
        }
        if (s.StartsWith('\'') && s.EndsWith('\'') && s.Length >= 2)
        {
            return s[1..^1];
        }
        if (s.StartsWith('`') && s.EndsWith('`') && s.Length >= 2)
        {
            return s[1..^1];
        }

        return s;
    }
}

