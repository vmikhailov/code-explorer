using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Canonical semantic roles for call literals and invocations in the code graph.
/// </summary>
public static class CallSemanticRole
{
    public const string Business = "Business";
    public const string Routing = "Routing";
    public const string ParamBinding = "ParamBinding";
    public const string Config = "Config";
    public const string Logging = "Logging";
    public const string QueryBuilder = "QueryBuilder";
    public const string DependencyInjection = "DI";
    public const string Security = "Security";
    public const string OrmMapping = "OrmMapping";
    public const string Validation = "Validation";
    public const string Event = "Event";
    public const string Assertion = "Assertion";
    public const string Formatting = "Formatting";
    public const string Exception = "Exception";
    public const string Reflection = "Reflection";
    public const string Decorator = "Decorator";
    public const string FormAccess = "FormAccess";
    public const string Storage = "Storage";
    public const string WebAPI = "WebAPI";
    public const string TestIdentifier = "TestIdentifier";
    public const string State = "State";
}

/// <summary>
/// Semantically classifies method calls and decorator/attribute invocations into architectural roles.
/// Replaces brittle name-based string exclusions with contextual semantic categorization.
/// </summary>
public static class CallSemanticClassifier
{
    private static readonly HashSet<string> RoutingDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get", "Post", "Put", "Delete", "Patch", "Head", "Options", "All", "Route", "BasePath",
        "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "HttpHead", "HttpOptions",
        "RouteAttribute", "HttpGetAttribute", "HttpPostAttribute", "HttpPutAttribute", "HttpDeleteAttribute", "HttpPatchAttribute",
        "Controller", "ApiController", "RestController"
    };

    private static readonly HashSet<string> ParamBindingDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "Param", "Query", "Body", "Headers", "Header", "Req", "Res", "Next", "Session", "Ip", "HostParam",
        "FromQuery", "FromRoute", "FromBody", "FromHeader", "FromForm",
        "FromQueryAttribute", "FromRouteAttribute", "FromBodyAttribute", "FromHeaderAttribute", "FromFormAttribute"
    };

    private static readonly HashSet<string> DiDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "Inject", "Injectable", "Optional", "InjectRepository", "InjectDataSource",
        "FromServices", "FromServicesAttribute", "FromKeyedServices", "FromKeyedServicesAttribute",
        "Autowired", "Resource", "Component", "Service", "Repository"
    };

    private static readonly HashSet<string> SecurityDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "Roles", "UseGuards", "Permissions", "Authorize", "AuthorizeAttribute", "AllowAnonymous", "AllowAnonymousAttribute"
    };

    private static readonly HashSet<string> OrmDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "Entity", "Table", "Column", "PrimaryGeneratedColumn", "Index", "Unique", "JoinColumn", "ManyToOne", "OneToMany",
        "TableAttribute", "ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute"
    };

    private static readonly HashSet<string> ValidationDecorators = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApiProperty", "ApiTags", "ApiOperation", "ApiResponse", "ApiParam", "ApiQuery", "ApiHeader",
        "Length", "Min", "Max", "IsNotEmpty", "IsString", "IsNumber", "IsBoolean", "IsOptional",
        "MaxLength", "MinLength", "Required", "RegularExpression"
    };

    private static readonly HashSet<string> LoggingMethodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "clog", "log", "warn", "error", "debug", "info", "trace",
        "LogInformation", "LogWarning", "LogError", "LogDebug", "LogTrace", "LogCritical",
        "WriteLine", "Write", "Print", "Printf", "Println", "Verbose", "Markup", "MarkupLine"
    };

    private static readonly HashSet<string> FormattingMethodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Format", "Join", "Concat", "Split", "Replace", "StartsWith", "EndsWith", "Contains",
        "Includes", "IndexOf", "LastIndexOf", "Trim", "TrimStart", "TrimEnd", "ToString",
        "PadLeft", "PadRight", "Substring", "Slice", "Combine", "Match", "Matches",
        "Append", "AppendLine", "AppendFormat", "AppendJoin",
        "Equals", "Compare", "CompareTo", "ReferenceEquals"
    };

    private static readonly HashSet<string> QueryBuilderMethodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "createQueryBuilder", "createSelectQueryBuilder", "where", "andWhere", "orWhere",
        "innerJoin", "leftJoin", "innerJoinAndSelect", "leftJoinAndSelect",
        "from", "select", "addSelect", "having", "groupBy", "orderBy", "addOrderBy"
    };

    private static readonly HashSet<string> DiMethodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "setGlobalPrefix", "listen", "enableCors", "provide", "inject", "useClass", "useValue", "useFactory"
    };

    public static (string Role, bool IsDecorator) Classify(Node callNode, string? callName)
    {
        // 1. Check if node is inside a decorator or attribute
        var curr = callNode.Parent;
        for (var i = 0; i < 3 && curr.IsValid(); i++, curr = curr.Parent)
        {
            if (curr.Type is "decorator" or "attribute")
            {
                var decName = ExtractDecoratorName(curr) ?? callName ?? "";
                var cleanDecName = CleanDecoratorName(decName);

                if (RoutingDecorators.Contains(cleanDecName)) return (CallSemanticRole.Routing, true);
                if (ParamBindingDecorators.Contains(cleanDecName)) return (CallSemanticRole.ParamBinding, true);
                if (DiDecorators.Contains(cleanDecName)) return (CallSemanticRole.DependencyInjection, true);
                if (SecurityDecorators.Contains(cleanDecName)) return (CallSemanticRole.Security, true);
                if (OrmDecorators.Contains(cleanDecName)) return (CallSemanticRole.OrmMapping, true);
                if (ValidationDecorators.Contains(cleanDecName)) return (CallSemanticRole.Validation, true);

                return (CallSemanticRole.Decorator, true);
            }
        }

        if (string.IsNullOrEmpty(callName))
        {
            return (CallSemanticRole.Business, false);
        }

        var dotIdx = callName.LastIndexOf('.');
        var receiver = dotIdx > 0 ? callName[..dotIdx].ToLowerInvariant() : "";
        var methodName = dotIdx >= 0 ? callName[(dotIdx + 1)..] : callName;

        // 2. Exception constructors and guards
        if (methodName.EndsWith("Exception", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("Throw", StringComparison.OrdinalIgnoreCase) ||
            callName.StartsWith("throw new ", StringComparison.OrdinalIgnoreCase) ||
            callName.StartsWith("new Argument", StringComparison.OrdinalIgnoreCase))
        {
            return (CallSemanticRole.Exception, false);
        }

        // 3. Reflection / Type checking
        if (methodName is "nameof" or "GetType" or "typeof")
        {
            return (CallSemanticRole.Reflection, false);
        }

        // 4. Configuration calls
        if (receiver.Contains("config") || receiver.Contains("env") || receiver.Contains("environment") ||
            callName.StartsWith("process.env", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("getOrThrow", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("getRequiredSection", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("GetConnectionString", StringComparison.OrdinalIgnoreCase) ||
            ((receiver.EndsWith("config") || receiver.EndsWith("configuration") || receiver.EndsWith("configservice") || receiver == "env") &&
             (methodName.Equals("get", StringComparison.OrdinalIgnoreCase) || methodName.Equals("has", StringComparison.OrdinalIgnoreCase) || methodName.Equals("getValue", StringComparison.OrdinalIgnoreCase))))
        {
            return (CallSemanticRole.Config, false);
        }

        // 5. Logging calls
        if (methodName.Equals("clog", StringComparison.OrdinalIgnoreCase) ||
            receiver.Contains("logger") || receiver.Contains("console") || receiver == "log" ||
            (LoggingMethodNames.Contains(methodName) && (receiver.Contains("log") || receiver.Contains("console") || string.IsNullOrEmpty(receiver))))
        {
            return (CallSemanticRole.Logging, false);
        }

        // 6. Formatting & String/Path manipulation
        if (receiver.Contains("stringbuilder") || receiver == "sb" || receiver == "path" || receiver == "string" ||
            (FormattingMethodNames.Contains(methodName) && (receiver.Contains("string") || receiver == "sb" || receiver == "path" || string.IsNullOrEmpty(receiver))))
        {
            return (CallSemanticRole.Formatting, false);
        }

        // 7. QueryBuilder calls
        if (methodName.Equals("createQueryBuilder", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("createSelectQueryBuilder", StringComparison.OrdinalIgnoreCase) ||
            receiver.Contains("builder") || receiver.Contains("querybuilder") || receiver == "qb" ||
            ((receiver.Contains("datasource") || receiver.Contains("queryrunner") || receiver.Contains("repo") || receiver == "qb") &&
             QueryBuilderMethodNames.Contains(methodName)))
        {
            return (CallSemanticRole.QueryBuilder, false);
        }

        // 8. Dependency Injection & framework setup
        if (DiMethodNames.Contains(methodName))
        {
            return (CallSemanticRole.DependencyInjection, false);
        }

        // 9. Event calls
        if (methodName is "emit" or "publish" or "sendEvent" or "dispatch" or "on" or "once" or "addListener" or "removeListener" or "addEventListener" or "removeEventListener")
        {
            if (receiver.Contains("event") || receiver.Contains("bus") || receiver.Contains("emitter") || receiver.Contains("socket") || string.IsNullOrEmpty(receiver))
            {
                return (CallSemanticRole.Event, false);
            }
        }

        // 10. Assertion calls
        if (receiver.StartsWith("expect") || receiver.StartsWith("assert") || methodName.StartsWith("Assert"))
        {
            return (CallSemanticRole.Assertion, false);
        }

        // 11. Form access (Angular reactive forms, form groups, form controls)
        if (receiver.Contains("form") || receiver.Contains("control") || receiver == "fb" || receiver.Contains("formbuilder") ||
            methodName is "addControl" or "removeControl" or "setControl" ||
            ((receiver.EndsWith("group") || receiver.EndsWith("form")) && (methodName is "get" or "has" or "patchValue" or "setValue" or "getRawValue")))
        {
            if (methodName is "get" or "has" or "getRawValue" or "patchValue" or "setValue" or "group" or "control" or "addControl" or "removeControl" or "setControl")
            {
                return (CallSemanticRole.FormAccess, false);
            }
        }

        // 12. Client storage (localStorage, sessionStorage)
        if (receiver.Contains("localstorage") || receiver.Contains("sessionstorage") || receiver.Contains("storage") || receiver == "prefs" || receiver.Contains("cookies"))
        {
            if (methodName is "getItem" or "setItem" or "removeItem" or "hasItem" or "clear" or "get" or "set")
            {
                return (CallSemanticRole.Storage, false);
            }
        }

        // 13. Web API / Browser DOM calls
        if (receiver is "window" or "document" or "navigator" || receiver.EndsWith(".window") || receiver.EndsWith(".document") || receiver.StartsWith("window.") || receiver.StartsWith("document."))
        {
            if (methodName is "open" or "addEventListener" or "removeEventListener" or "postMessage" or "getElementById" or "querySelector" or "querySelectorAll" or "createElement" or "setAttribute" or "getAttribute" or "alert" or "confirm" or "prompt")
            {
                return (CallSemanticRole.WebAPI, false);
            }
        }

        // 14. Test automation identifiers / QA attributes
        if (methodName.EndsWith("TestId", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("getByTestId", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("findByTestId", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("testId", StringComparison.OrdinalIgnoreCase))
        {
            return (CallSemanticRole.TestIdentifier, false);
        }

        // 15. Reactive state & signals (Angular / Solid / Preact)
        if (methodName is "signal" or "computed" ||
            (receiver.Contains("signal") && methodName is "set" or "update" or "mutate"))
        {
            return (CallSemanticRole.State, false);
        }

        // 16. Default: Business logic
        return (CallSemanticRole.Business, false);
    }

    private static string? ExtractDecoratorName(Node decoratorNode)
    {
        // For TypeScript: @Get(...) -> call_expression -> function -> identifier
        var callExpr = decoratorNode.FindChildOfType("call_expression");
        if (callExpr.IsValid())
        {
            var fn = callExpr.GetChildFieldText("function") ??
                     callExpr.Children.FirstOrDefault(c => c.Type.Contains("identifier"))?.Text;
            if (!string.IsNullOrEmpty(fn)) return fn;
        }

        // For C#: [HttpGet(...)] -> attribute -> identifier
        var ident = decoratorNode.Children.FirstOrDefault(c => c.Type.Contains("identifier"));
        if (ident.IsValid()) return ident.Text;

        return decoratorNode.Text.TrimStart('@', '[').TrimEnd(']');
    }

    private static string CleanDecoratorName(string name)
    {
        var dot = name.LastIndexOf('.');
        if (dot >= 0) name = name[(dot + 1)..];
        return name.TrimStart('@');
    }
}
