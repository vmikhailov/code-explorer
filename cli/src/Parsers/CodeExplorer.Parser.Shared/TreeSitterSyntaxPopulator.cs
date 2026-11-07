using CodeExplorer.Core.Analysis.Dataflow;

namespace CodeExplorer.Core.Parser.Syntax;

/// <summary>
/// Populates strongly-typed properties across Universal AST (<see cref="SyntaxNode"/>) constructs
/// after child nodes have been attached by <see cref="TreeSitterSyntaxConverterBase"/>.
/// </summary>
internal static class TreeSitterSyntaxPopulator
{
    private const string FieldAlias = "alias";
    private const string FieldParameter = "parameter";
    private const string FieldDefault = "default";
    private const string FieldHandler = "handler";
    private const string KindPython = "python";
    private const string KindArrowExprClause = "arrow_expression_clause";
    private const string KindTypeAnnotation = "type_annotation";
    private const string KindEqualsValueClause = "equals_value_clause";
    private const string KindBracketedArgList = "bracketed_argument_list";
    private const string KindCatchDeclaration = "catch_declaration";
    private const string KindCatchFormalParam = "catch_formal_parameter";
    private const string KindFinallyClause = "finally_clause";
    private const string KindArgument = "argument";
    private const string OpenBracketToken = "[";

    public static void PopulateDeclarationHeader(DeclarationSyntax decl, SyntaxDocument doc)
    {
        var nameChild = decl.GetChildForField(SyntaxFields.Name) ??
                        decl.FirstChildOfType<IdentifierExprSyntax>();
        if (nameChild != null)
        {
            decl.NameSlice = new SourceSlice(nameChild.Span.StartByte, nameChild.Span.Length);
        }
        else if (!decl.HasName && decl.Children.Count == 0 && !decl.Span.IsEmpty)
        {
            decl.NameSlice = new SourceSlice(decl.Span.StartByte, decl.Span.Length);
        }

        if (decl is NamespaceDeclSyntax ns && ns.HasName && string.IsNullOrEmpty(doc.PackageOrNamespace))
        {
            doc.PackageOrNamespace = ns.Name;
        }
    }

    public static void PopulateImport(ImportDeclSyntax imp)
    {
        if (imp.Modifiers.HasFlag(SyntaxModifiers.Static))
        {
            imp.IsStatic = true;
        }

        var aliasNode = imp.GetChildForField(FieldAlias);
        if (aliasNode != null)
        {
            imp.Alias = aliasNode.Text;
        }

        if (!string.IsNullOrEmpty(imp.ModulePath))
        {
            return;
        }

        var sourceNode = imp.GetChildForField(SyntaxFields.Source) ??
                         imp.GetChildForField(SyntaxFields.Path) ??
                         imp.GetChildForField(SyntaxFields.ModuleName) ??
                         imp.GetChildForField(SyntaxFields.Name);

        if (sourceNode is LiteralExprSyntax sourceLit)
        {
            imp.ModulePath = sourceLit.ValueText;
        }
        else if (sourceNode != null)
        {
            imp.ModulePath = sourceNode.Text.Trim('"', '\'', '`');
        }
        else if (imp.FirstChildOfType<LiteralExprSyntax>() is { } firstLit)
        {
            imp.ModulePath = firstLit.ValueText;
        }
        else if (imp.FirstChildOfType<MemberAccessExprSyntax>() is { } memPath)
        {
            imp.ModulePath = memPath.Text;
        }

        if (imp.IsReExport)
        {
            for (int i = 0; i < imp.Children.Count; i++)
            {
                if (imp.Children[i] is DeclarationSyntax childDecl)
                {
                    childDecl.Modifiers |= SyntaxModifiers.Export;
                }
            }
        }
    }

    public static void PopulateTypeRef(TypeRefSyntax typeRef)
    {
        if (typeRef.NameSlice.IsEmpty && !typeRef.Span.IsEmpty)
        {
            typeRef.NameSlice = new SourceSlice(typeRef.Span.StartByte, typeRef.Span.Length);
        }
    }

    public static void PopulateParameter(ParameterDeclSyntax param)
    {
        param.ParameterType ??= ExtractTypeRef(param);
        param.DefaultValue ??= ExtractDefaultValue(param);

        if (param.PassingMode == ParameterPassingMode.Receiver)
        {
            var span = param.AsSpan();
            if (span.Contains("mut", StringComparison.Ordinal))
            {
                param.PassingMode = ParameterPassingMode.MutBorrow;
            }
        }
        else if (param.NameSpan is "self" or "this")
        {
            param.PassingMode = ParameterPassingMode.Receiver;
        }
    }

    public static void PopulateFunction(FunctionDeclSyntax fn)
    {
        fn.Parameters = ExtractParameters(fn);

        var recvNode = fn.GetChildForField(SyntaxFields.Receiver);
        if (recvNode is ParameterDeclSyntax directRecv)
        {
            directRecv.PassingMode = ParameterPassingMode.Receiver;
            fn.Receiver = directRecv;
        }
        else if (recvNode?.FirstChildOfType<ParameterDeclSyntax>() is { } nestedRecv)
        {
            nestedRecv.PassingMode = ParameterPassingMode.Receiver;
            fn.Receiver = nestedRecv;
        }
        else if (fn.Parameters.Count > 0 && fn.Parameters[0].PassingMode is ParameterPassingMode.Receiver or ParameterPassingMode.MutBorrow)
        {
            fn.Receiver = fn.Parameters[0];
        }

        fn.Body = fn.GetChildForField(SyntaxFields.Body) as BlockStmtSyntax ?? fn.FirstChildOfType<BlockStmtSyntax>();
        for (int i = 0; i < fn.Children.Count; i++)
        {
            if (fn.Children[i] is ExprSyntax exprChild && exprChild is not IdentifierExprSyntax)
            {
                fn.ExpressionBody = exprChild;
                break;
            }
        }

        FunctionDataflowAnalyzer.Analyze(fn);
    }

    public static void PopulateLambda(LambdaExprSyntax lambda)
    {
        lambda.Parameters = ExtractParameters(lambda);
        if (lambda.Parameters.Count == 0 && lambda.GetChildForField(FieldParameter) is IdentifierExprSyntax singleParamId)
        {
            lambda.Parameters =
            [
                new ParameterDeclSyntax
                {
                    NameSlice = new SourceSlice(singleParamId.Span.StartByte, singleParamId.Span.Length),
                    Span = singleParamId.Span,
                    Document = singleParamId.Document,
                    Parent = lambda
                }
            ];
        }

        var bodyField = lambda.GetChildForField(SyntaxFields.Body);
        if (bodyField != null)
        {
            lambda.Body = bodyField;
        }
        else if (lambda.FirstChildOfType<BlockStmtSyntax>() is { } blockBody)
        {
            lambda.Body = blockBody;
        }
        else if (lambda.Children.Count > 0 && lambda.Children[^1] is ExprSyntax tailExpr)
        {
            lambda.Body = tailExpr;
        }
    }

    public static IReadOnlyList<ParameterDeclSyntax> ExtractParameters(SyntaxNode owner)
    {
        var paramsContainer = owner.GetChildForField(SyntaxFields.Parameters);
        if (paramsContainer != null)
        {
            List<ParameterDeclSyntax>? paramList = null;
            for (int i = 0; i < paramsContainer.Children.Count; i++)
            {
                var c = paramsContainer.Children[i];
                if (c is ParameterDeclSyntax p)
                {
                    (paramList ??= new List<ParameterDeclSyntax>(paramsContainer.Children.Count)).Add(p);
                }
                else if (c is IdentifierExprSyntax id)
                {
                    (paramList ??= new List<ParameterDeclSyntax>(paramsContainer.Children.Count)).Add(new ParameterDeclSyntax
                    {
                        NameSlice = new SourceSlice(id.Span.StartByte, id.Span.Length),
                        Span = id.Span,
                        Document = id.Document,
                        Parent = owner,
                        PassingMode = id.NameSpan is "self" or "this" ? ParameterPassingMode.Receiver : ParameterPassingMode.Value
                    });
                }
            }
            return paramList ?? (IReadOnlyList<ParameterDeclSyntax>)[];
        }

        List<ParameterDeclSyntax>? directParams = null;
        for (int i = 0; i < owner.Children.Count; i++)
        {
            if (owner.Children[i] is ParameterDeclSyntax dp && dp.FieldRole != SyntaxFields.Receiver)
            {
                (directParams ??= new List<ParameterDeclSyntax>(4)).Add(dp);
            }
        }
        return directParams ?? (IReadOnlyList<ParameterDeclSyntax>)[];
    }

    public static void PopulateAnnotation(AnnotationSyntax ann)
    {
        var args = ExtractArguments(ann);
        if (args.Count > 0)
        {
            ann.Arguments = args;
            return;
        }

        if (ann.FirstChildOfType<CallExprSyntax>() is { } callChild)
        {
            if (!ann.HasName && !callChild.MethodNameSlice.IsEmpty)
            {
                ann.NameSlice = callChild.MethodNameSlice;
            }
            if (callChild.Arguments.Count > 0)
            {
                ann.Arguments = callChild.Arguments;
            }
        }
    }

    public static IReadOnlyList<ArgumentSyntax> ExtractArguments(SyntaxNode owner)
    {
        var argsContainer = owner.GetChildForField(SyntaxFields.Arguments);
        if (argsContainer == null)
        {
            for (int i = 0; i < owner.Children.Count; i++)
            {
                if (owner.Children[i] is SyntaxList { Kind: SyntaxListKind.Arguments } or CollectionExprSyntax { Kind: CollectionSyntaxKind.Tuple })
                {
                    argsContainer = owner.Children[i];
                    break;
                }
            }
        }

        if (argsContainer != null)
        {
            List<ArgumentSyntax>? args = null;
            for (int i = 0; i < argsContainer.Children.Count; i++)
            {
                var child = argsContainer.Children[i];
                if (child is ArgumentSyntax arg)
                {
                    (args ??= new List<ArgumentSyntax>(argsContainer.Children.Count)).Add(arg);
                }
                else if (child is ExprSyntax expr)
                {
                    (args ??= new List<ArgumentSyntax>(argsContainer.Children.Count)).Add(new ArgumentSyntax
                    {
                        Value = expr,
                        Span = expr.Span,
                        Parent = owner,
                        Document = owner.Document
                    });
                }
            }
            return args ?? (IReadOnlyList<ArgumentSyntax>)[];
        }

        List<ArgumentSyntax>? directArgs = null;
        for (int i = 0; i < owner.Children.Count; i++)
        {
            if (owner.Children[i] is ArgumentSyntax da)
            {
                (directArgs ??= new List<ArgumentSyntax>(4)).Add(da);
            }
        }
        return directArgs ?? (IReadOnlyList<ArgumentSyntax>)[];
    }

    public static void PopulateArgument(ArgumentSyntax argSyntax)
    {
        var argName = argSyntax.GetChildForField(SyntaxFields.Name);
        if (argName != null)
        {
            argSyntax.NameSlice = new SourceSlice(argName.Span.StartByte, argName.Span.Length);
        }

        if (argSyntax.GetChildForField(SyntaxFields.Value) is ExprSyntax valExpr)
        {
            argSyntax.Value = valExpr;
            return;
        }

        for (int i = 0; i < argSyntax.Children.Count; i++)
        {
            if (argSyntax.Children[i] is ExprSyntax exprChild && !ReferenceEquals(exprChild, argName))
            {
                argSyntax.Value = exprChild;
                return;
            }
        }
    }

    public static void PopulateIndex(IndexExprSyntax idx)
    {
        idx.Target = (idx.GetChildForField(SyntaxFields.Object) ??
                      idx.GetChildForField(SyntaxFields.Value) ??
                      idx.GetChildForField(SyntaxFields.Operand) ??
                      idx.GetChildForField(SyntaxFields.Expression)) as ExprSyntax ??
                     idx.NthChildOfType<ExprSyntax>(0);

        var indexNode = idx.GetChildForField(SyntaxFields.Index) ??
                        idx.GetChildForField(SyntaxFields.Subscript) ??
                        idx.GetChildForField(SyntaxFields.Right);
        if (indexNode is ExprSyntax directIdx)
        {
            idx.Index = directIdx;
            return;
        }

        for (int i = 0; i < idx.Children.Count; i++)
        {
            var c = idx.Children[i];
            if (c is SyntaxList { Kind: SyntaxListKind.Arguments } || (c.Children.Count > 0 && c.SpanStartsWith(OpenBracketToken)))
            {
                if (c.FirstChildOfType<ArgumentSyntax>() is { Value: { } argVal })
                {
                    idx.Index = argVal;
                    return;
                }
                if (c.FirstChildOfType<ExprSyntax>() is { } innerExpr)
                {
                    idx.Index = innerExpr;
                    return;
                }
            }
        }

        var secondExpr = idx.NthChildOfType<ExprSyntax>(1);
        if (secondExpr != null && !ReferenceEquals(secondExpr, idx.Target))
        {
            idx.Index = secondExpr;
        }
    }

    public static void PopulateConditional(ConditionalExprSyntax cond, SyntaxDocument doc)
    {
        if (string.Equals(doc.LanguageName, KindPython, StringComparison.OrdinalIgnoreCase) &&
            cond.GetChildForField(SyntaxFields.Condition) == null)
        {
            cond.WhenTrue = cond.NthChildOfType<ExprSyntax>(0);
            cond.Condition = cond.NthChildOfType<ExprSyntax>(1);
            cond.WhenFalse = cond.NthChildOfType<ExprSyntax>(2);
            return;
        }

        cond.Condition = cond.GetChildForField(SyntaxFields.Condition) as ExprSyntax ?? cond.NthChildOfType<ExprSyntax>(0);
        cond.WhenTrue = cond.GetChildForField(SyntaxFields.Consequence) as ExprSyntax ?? cond.NthChildOfType<ExprSyntax>(1);
        cond.WhenFalse = cond.GetChildForField(SyntaxFields.Alternative) as ExprSyntax ?? cond.NthChildOfType<ExprSyntax>(2);
    }

    public static void PopulateCollection(CollectionExprSyntax col)
    {
        if (col.Children.Count == 0) return;
        var elements = new List<ExprSyntax>(col.Children.Count);
        for (int i = 0; i < col.Children.Count; i++)
        {
            var c = col.Children[i];
            if (c is ExprSyntax expr)
            {
                elements.Add(expr);
            }
            else if (c.GetChildForField(SyntaxFields.Value) is ExprSyntax pairVal)
            {
                elements.Add(pairVal);
            }
            else if (c.LastChildOfType<ExprSyntax>() is { } nestedExpr)
            {
                elements.Add(nestedExpr);
            }
        }
        if (elements.Count > 0)
        {
            col.Elements = elements;
        }
    }

    public static void PopulateCatchClause(CatchClauseSyntax catchClause)
    {
        catchClause.Body = catchClause.FirstChildOfType<BlockStmtSyntax>();
        catchClause.ExceptionType = ExtractExceptionTypeRef(catchClause);
    }

    public static void PopulateControlFlow(ControlFlowStmtSyntax cf)
    {
        switch (cf.Kind)
        {
            case ControlFlowKind.If:
                cf.Condition = cf.GetChildForField(SyntaxFields.Condition) as ExprSyntax ??
                               cf.FirstChildOfType<ExprSyntax>() ??
                               cf.GetChildForField(SyntaxFields.Condition)?.FirstChildOfType<ExprSyntax>();
                cf.Body = cf.GetChildForField(SyntaxFields.Consequence) ?? cf.FirstChildOfType<StatementSyntax>();
                cf.Alternative = cf.GetChildForField(SyntaxFields.Alternative) ?? cf.NthChildOfType<StatementSyntax>(1);
                break;

            case ControlFlowKind.Loop:
                cf.Condition = cf.GetChildForField(SyntaxFields.Condition) as ExprSyntax ??
                               cf.FirstChildOfType<ExprSyntax>() ??
                               cf.GetChildForField(SyntaxFields.Condition)?.FirstChildOfType<ExprSyntax>();
                cf.Body = cf.GetChildForField(SyntaxFields.Body) ?? cf.LastChildOfType<StatementSyntax>();
                break;

            case ControlFlowKind.Try:
                cf.Body = cf.GetChildForField(SyntaxFields.Body) as BlockStmtSyntax ??
                          cf.FirstChildOfType<BlockStmtSyntax>();
                SyntaxNode? finallyNode = cf.GetChildForField(SyntaxFields.Finalizer);
                if (finallyNode == null)
                {
                    for (int i = 0; i < cf.Children.Count; i++)
                    {
                        var c = cf.Children[i];
                        if (c.FieldRole is SyntaxFields.Finalizer or KindFinallyClause || c is SyntaxList { Kind: SyntaxListKind.Clauses })
                        {
                            finallyNode = c;
                            break;
                        }
                    }
                }
                cf.Alternative = finallyNode as BlockStmtSyntax ?? finallyNode?.FirstChildOfType<BlockStmtSyntax>();
                break;

            case ControlFlowKind.Lock:
                cf.Body = cf.FirstChildOfType<BlockStmtSyntax>();
                break;

            case ControlFlowKind.Using:
                cf.Body = cf.LastChildOfType<StatementSyntax>();
                break;
        }
    }

    public static TypeRefSyntax? ExtractTypeRef(SyntaxNode owner)
    {
        var typeNode = owner.GetChildForField(SyntaxFields.Type);
        if (typeNode == null)
        {
            for (int i = 0; i < owner.Children.Count; i++)
            {
                var c = owner.Children[i];
                if (c is TypeRefSyntax { Kind: TypeRefKind.Annotation } annTr)
                {
                    typeNode = annTr;
                    break;
                }
                if (c is TypeRefSyntax directTr)
                {
                    return directTr;
                }
            }
        }

        return ConvertNodeToTypeRef(typeNode, owner);
    }

    private static TypeRefSyntax? ExtractExceptionTypeRef(CatchClauseSyntax catchClause)
    {
        if (catchClause.GetChildForField(SyntaxFields.Type) is { } directType)
        {
            return ConvertNodeToTypeRef(directType, catchClause);
        }

        for (int i = 0; i < catchClause.Children.Count; i++)
        {
            var c = catchClause.Children[i];
            if (c is BlockStmtSyntax) continue;
            if (c is TypeRefSyntax { Kind: not TypeRefKind.Annotation } tr) return tr;
            if (c is SyntaxList { Kind: SyntaxListKind.Clauses } or TypeRefSyntax { Kind: TypeRefKind.Annotation } or ParameterDeclSyntax)
            {
                var nested = ExtractTypeRef(c) ?? ConvertNodeToTypeRef(c.FirstChildOfType<IdentifierExprSyntax>(), catchClause);
                if (nested != null) return nested;
            }
            else if (c is IdentifierExprSyntax id && c.FieldRole != SyntaxFields.Name)
            {
                return ConvertNodeToTypeRef(id, catchClause);
            }
        }

        return null;
    }

    private static TypeRefSyntax? ConvertNodeToTypeRef(SyntaxNode? typeNode, SyntaxNode owner)
    {
        if (typeNode == null) return null;
        if (typeNode is TypeRefSyntax { Kind: not TypeRefKind.Annotation } tr) return tr;
        if (typeNode.FirstChildOfType<TypeRefSyntax>() is { } childTr) return childTr;

        var targetNode = typeNode is IdentifierExprSyntax
            ? typeNode
            : (typeNode.FirstChildOfType<IdentifierExprSyntax>() ?? typeNode);

        if (targetNode.Span.IsEmpty) return null;

        return new TypeRefSyntax
        {
            Span = targetNode.Span,
            NameSlice = new SourceSlice(targetNode.Span.StartByte, targetNode.Span.Length),
            Document = owner.Document,
            Parent = owner
        };
    }

    private static ExprSyntax? ExtractDefaultValue(ParameterDeclSyntax param)
    {
        var valNode = param.GetChildForField(SyntaxFields.Value) ??
                      param.GetChildForField(FieldDefault) ??
                      param.GetChildForField(SyntaxFields.Right);
        if (valNode is ExprSyntax directExpr)
        {
            return directExpr;
        }
        if (valNode != null && valNode.FirstChildOfType<ExprSyntax>() is { } nestedVal)
        {
            return nestedVal;
        }

        for (int i = 0; i < param.Children.Count; i++)
        {
            var c = param.Children[i];
            if (c is SyntaxList { Kind: SyntaxListKind.Initializer } && c.FirstChildOfType<ExprSyntax>() is { } eqVal)
            {
                return eqVal;
            }
        }

        return null;
    }

    public static void PopulateVariable(VariableDeclSyntax varDecl)
    {
        varDecl.VariableType ??= ExtractTypeRef(varDecl);
        var varInitNode = varDecl.GetChildForField(SyntaxFields.Value) ??
                          varDecl.GetChildForField(SyntaxFields.Right);
        if (varInitNode is ExprSyntax directVarInit)
        {
            varDecl.Initializer = directVarInit;
        }
        else if (varDecl.Children.Count >= 2)
        {
            var vdNameSpan = varDecl.NameSpan;
            for (int i = varDecl.Children.Count - 1; i >= 1; i--)
            {
                if (varDecl.Children[i] is SyntaxList { Kind: SyntaxListKind.Initializer, Children.Count: > 0 } initClause &&
                    initClause.Children[^1] is ExprSyntax initExpr)
                {
                    varDecl.Initializer = initExpr;
                    break;
                }
                if (varDecl.Children[i] is ExprSyntax exprCandidate &&
                    exprCandidate.FieldRole is not (SyntaxFields.Name or SyntaxFields.Type or SyntaxFields.Pattern or SyntaxFields.Left) &&
                    !exprCandidate.AsSpan().Equals(vdNameSpan, StringComparison.Ordinal))
                {
                    varDecl.Initializer = exprCandidate;
                    break;
                }
            }
        }
        if (varDecl.Modifiers.HasFlag(SyntaxModifiers.Const) || varDecl.Modifiers.HasFlag(SyntaxModifiers.ReadOnly))
        {
            varDecl.IsConstant = true;
        }
        for (int i = 0; i < varDecl.Children.Count; i++)
        {
            if (varDecl.Children[i] is VariableDeclSyntax childVd)
            {
                PropagateVariableDeclMetadata(varDecl, childVd);
            }
        }
    }

    public static void PopulateMember(MemberDeclSyntax memDecl)
    {
        memDecl.MemberType ??= ExtractTypeRef(memDecl);
        var memInitNode = memDecl.GetChildForField(SyntaxFields.Value);
        if (memInitNode == null)
        {
            for (int i = 0; i < memDecl.Children.Count; i++)
            {
                if (memDecl.Children[i] is SyntaxList { Kind: SyntaxListKind.Initializer })
                {
                    memInitNode = memDecl.Children[i];
                    break;
                }
            }
        }
        if (memInitNode is SyntaxList { Kind: SyntaxListKind.Initializer, Children.Count: > 0 })
        {
            memInitNode = memInitNode.Children[^1];
        }
        if (memInitNode is ExprSyntax directMemInit)
        {
            memDecl.Initializer = directMemInit;
        }
        else if (memDecl.Children.Count >= 2)
        {
            var mdNameSpan = memDecl.NameSpan;
            for (int i = memDecl.Children.Count - 1; i >= 1; i--)
            {
                if (memDecl.Children[i] is ExprSyntax exprCandidate &&
                    exprCandidate.FieldRole is not (SyntaxFields.Name or SyntaxFields.Type or SyntaxFields.Pattern or SyntaxFields.Left) &&
                    !exprCandidate.AsSpan().Equals(mdNameSpan, StringComparison.Ordinal))
                {
                    memDecl.Initializer = exprCandidate;
                    break;
                }
            }
        }
        if (memDecl.Modifiers.HasFlag(SyntaxModifiers.Const) || memDecl.Modifiers.HasFlag(SyntaxModifiers.ReadOnly))
        {
            for (int i = 0; i < memDecl.Children.Count; i++)
            {
                if (memDecl.Children[i] is VariableDeclSyntax childVd)
                {
                    childVd.IsConstant = true;
                    childVd.Modifiers |= memDecl.Modifiers & (SyntaxModifiers.Const | SyntaxModifiers.ReadOnly | SyntaxModifiers.Static);
                    for (int j = 0; j < childVd.Children.Count; j++)
                    {
                        if (childVd.Children[j] is VariableDeclSyntax subVd)
                        {
                            subVd.IsConstant = true;
                            subVd.Modifiers |= memDecl.Modifiers & (SyntaxModifiers.Const | SyntaxModifiers.ReadOnly | SyntaxModifiers.Static);
                        }
                    }
                }
            }
        }
    }

    public static void PopulateKeyValue(KeyValueExprSyntax kv)
    {
        var keyNode = kv.GetChildForField(SyntaxFields.Key) ??
                      kv.GetChildForField(SyntaxFields.Name) ??
                      kv.GetChildForField(SyntaxFields.Field) ??
                      (kv.Children.Count > 0 ? kv.Children[0] : null);
        if (keyNode is ParenthesizedExprSyntax { Children.Count: > 0 } pKey)
        {
            keyNode = pKey.Children[0];
        }
        kv.Key = keyNode as ExprSyntax;
        if (keyNode != null && !keyNode.Span.IsEmpty)
        {
            kv.KeySlice = new SourceSlice(keyNode.Span.StartByte, keyNode.Span.Length);
        }

        var valNode = kv.GetChildForField(SyntaxFields.Value) ??
                      (kv.Children.Count > 1 ? kv.Children[^1] : null);
        if (valNode is ParenthesizedExprSyntax { Children.Count: > 0 } pVal)
        {
            valNode = pVal.Children[0];
        }
        kv.Value = valNode as ExprSyntax;
    }

    public static void PopulateParenthesized(ParenthesizedExprSyntax paren)
    {
        paren.Expression = paren.FirstChildOfType<ExprSyntax>();
    }

    public static void PopulateTypeCast(TypeCastExprSyntax castExpr)
    {
        castExpr.TargetType = castExpr.GetChildForField(SyntaxFields.Type) as TypeRefSyntax ??
                              castExpr.FirstChildOfType<TypeRefSyntax>();
        castExpr.Operand = (castExpr.GetChildForField(SyntaxFields.Value) ??
                            castExpr.GetChildForField(SyntaxFields.Expression)) as ExprSyntax ??
                           castExpr.FirstChildOfType<ExprSyntax>();
    }

    public static void PopulateImpl(ImplDeclSyntax implDecl)
    {
        implDecl.ImplementedTrait = implDecl.GetChildForField(SyntaxFields.Trait) as TypeRefSyntax;
        implDecl.TargetType = implDecl.GetChildForField(SyntaxFields.Type) as TypeRefSyntax;
        if (implDecl.TargetType == null && implDecl.Children.Count > 0)
        {
            var firstType = implDecl.NthChildOfType<TypeRefSyntax>(0);
            var secondType = implDecl.NthChildOfType<TypeRefSyntax>(1);
            if (secondType != null)
            {
                implDecl.ImplementedTrait ??= firstType;
                implDecl.TargetType = secondType;
            }
            else if (firstType != null)
            {
                implDecl.TargetType = firstType;
            }
        }
    }

    private static void PropagateVariableDeclMetadata(VariableDeclSyntax parent, VariableDeclSyntax child)
    {
        if (parent.IsConstant)
        {
            child.IsConstant = true;
        }
        child.Modifiers |= parent.Modifiers;
        child.VariableType ??= parent.VariableType;
        for (int i = 0; i < child.Children.Count; i++)
        {
            if (child.Children[i] is VariableDeclSyntax grandChild)
            {
                PropagateVariableDeclMetadata(child, grandChild);
            }
        }
    }
}
