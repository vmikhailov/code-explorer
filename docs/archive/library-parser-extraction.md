# Library Parser Extraction — Step-by-Step Instructions

Extract five inline detection methods from `TypeScriptParser` and `CSharpParser` into proper `ILibraryParser` implementations. Each step is independent and self-contained. Build and verify after each one.

---

## Context

`ILibraryParser` has the correct interface: `MapNodeType`, `ExtractIdentifier`, `CollectReferences`. `TreeSitterFileParser` already dispatches to library parsers **first** during tree traversal — if a library parser matches, the language parser is skipped for that node. The problem is that NestJS, Express, `fetch`, ASP.NET Core, and `HttpClient` detection still live as inline methods in the language parsers and fire unconditionally on AST shape regardless of whether the library is imported.

Use `AxiosLibraryParser` and `FlurlLibraryParser` as structural templates for new parsers.

---

## Step 1 — `NestJsLibraryParser` (TypeScript)

**Moves:** `IsTsDecoratorEntryPoint` + `ExtractTsDecoratorRoute` + the IMPLEMENTS reference in `CollectReferences`

**Create:** `Parsers/CodeExplorer.Parser.TypeScript/Libraries/NestJsLibraryParser.cs`

```csharp
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class NestJsLibraryParser : ILibraryParser
{
    public string Name => "NestJS";
    public string Id => "nestjs";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["@nestjs/common", "@nestjs/core", "@nestjs/microservices", "@nestjs/websockets"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsNestDecorator(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsNestDecorator(node)) return ExtractRoute(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // When visiting a method_definition, check if the previous sibling is a NestJS decorator
        // and emit IMPLEMENTS to link the function to its EntryPoint
        if (node.Type == "method_definition")
        {
            var prev = GetPreviousNamedSibling(node);
            if (prev != null && prev.Type == "decorator" && IsNestDecorator(prev))
            {
                var route = ExtractRoute(prev);
                if (!string.IsNullOrEmpty(route))
                {
                    references.Add(new Reference(scopeSymbolId, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                }
            }
        }
    }

    internal static bool IsNestDecorator(Node node)
    {
        if (node.Type != "decorator") return false;
        var call = node.Children.FirstOrDefault(c => c.Type == "call_expression");
        if (call == null) return false;
        var func = call.GetChildForField("function")
                   ?? (call.Children.Count > 0 ? call.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;
        return func.Text is "Controller" or "Get" or "Post" or "Put" or "Delete" or "Patch" or "SubscribeMessage";
    }

    internal static string? ExtractRoute(Node node)
    {
        var call = node.Children.FirstOrDefault(c => c.Type == "call_expression");
        if (call == null) return null;
        var func = call.GetChildForField("function")
                   ?? (call.Children.Count > 0 ? call.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return null;
        var name = func.Text;

        var args = call.Children.FirstOrDefault(c => c.Type == "arguments");
        string routeVal = "/";
        if (args != null && args.Children.Count > 2)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null) routeVal = firstArg.Text.Trim('\'', '"', '`');
        }

        if (name == "SubscribeMessage") return $"ws:{routeVal}";
        return $"{(name == "Controller" ? "GET" : name.ToUpperInvariant())}:{routeVal}";
    }

    private static Node? GetPreviousNamedSibling(Node node)
    {
        var parent = node.Parent;
        if (parent == null || parent.Id == IntPtr.Zero) return null;
        var children = parent.Children;
        var idx = children.ToList().FindIndex(c => c.Id == node.Id);
        return idx > 0 ? children[idx - 1] : null;
    }
}
```

**Wire it in** — add to `TypeScriptParser.LibraryParsers`:

```csharp
new NestJsLibraryParser(),
```

**Delete from `TypeScriptParser`:**

- `IsTsDecoratorEntryPoint` method
- `ExtractTsDecoratorRoute` method
- The two `IsTsDecoratorEntryPoint` calls in `MapNodeType` and `ExtractIdentifier`
- The IMPLEMENTS block in `CollectReferences` (the `node.Type == "method_definition"` block)

**Build and verify** `Test_TypeScriptParser_ApiIngressEgress` still passes.

---

## Step 2 — `ExpressLibraryParser` (TypeScript)

**Moves:** `IsExpressRoute` + `ExtractExpressRoute`

**Create:** `Parsers/CodeExplorer.Parser.TypeScript/Libraries/ExpressLibraryParser.cs`

```csharp
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ExpressLibraryParser : ILibraryParser
{
    public string Name => "Express";
    public string Id => "express";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["express", "@types/express"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> HttpMethods = ["get", "post", "put", "delete", "patch"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsExpressRoute(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsExpressRoute(node)) return ExtractRoute(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsExpressRoute(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type != "member_expression") return false;
        var obj = func.GetChildForField("object");
        var prop = func.GetChildForField("property");
        return obj != null && prop != null && prop.Id != IntPtr.Zero
            && obj.Text is "app" or "router" or "express"
            && HttpMethods.Contains(prop.Text);
    }

    private static string? ExtractRoute(Node node)
    {
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null) return null;
        var prop = func.GetChildForField("property");
        if (prop == null) return null;

        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        string routeVal = "/";
        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null) routeVal = firstArg.Text.Trim('\'', '"', '`');
        }
        return $"{prop.Text.ToUpperInvariant()}:{routeVal}";
    }
}
```

**Wire it in** — add to `TypeScriptParser.LibraryParsers`:

```csharp
new ExpressLibraryParser(),
```

**Delete from `TypeScriptParser`:** `IsExpressRoute`, `ExtractExpressRoute`, and their call sites in `MapNodeType`/`ExtractIdentifier`.

**Build and verify.**

---

## Step 3 — `FetchLibraryParser` (TypeScript)

**Moves:** `IsTsHttpClientCall` + `ExtractTsHttpClientTarget` for `fetch`, `got`, `superagent`, `node-fetch`

**Create:** `Parsers/CodeExplorer.Parser.TypeScript/Libraries/FetchLibraryParser.cs`

```csharp
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class FetchLibraryParser : ILibraryParser
{
    public string Name => "Fetch";
    public string Id => "fetch";
    public string Type => "api";
    // fetch is built-in; node-fetch/got/superagent are npm packages
    public IReadOnlyList<string> SupportedPatterns => ["node-fetch", "got", "superagent", "cross-fetch", "isomorphic-fetch"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;   // fetch is available without import in browsers/Node 18+

    private static readonly HashSet<string> DirectCallNames = ["fetch", "nodeFetch", "got", "superagent"];
    private static readonly HashSet<string> ObjectNames = ["got", "superagent", "request", "http", "https"];
    private static readonly HashSet<string> HttpMethods = ["get", "post", "put", "delete", "request", "patch", "head"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHttpCall(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHttpCall(node)) return ExtractTarget(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsHttpCall(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "identifier") return DirectCallNames.Contains(func.Text);

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");
            var prop = func.GetChildForField("property");
            if (obj != null && prop != null && prop.Id != IntPtr.Zero)
                return ObjectNames.Contains(obj.Text) && HttpMethods.Contains(prop.Text);
        }
        return false;
    }

    private static string? ExtractTarget(Node node)
    {
        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null)
            {
                var url = firstArg.Text.Trim('\'', '"', '`');
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) return uri.Host;
                return url;
            }
        }
        return "http:unknown-service";
    }
}
```

**Wire it in** — add to `TypeScriptParser.LibraryParsers`:

```csharp
new FetchLibraryParser(),
```

**Delete from `TypeScriptParser`:** `IsTsHttpClientCall`, `ExtractTsHttpClientTarget`, and their call sites. After deletion, `MapNodeType` and `ExtractIdentifier` should have no more `if (IsXxx)` blocks at the top — only the `node.Type switch`.

**Build and verify** `Test_TypeScriptParser_ApiIngressEgress` still passes.

---

## Step 4 — `AspNetCoreLibraryParser` (C#)

**Moves:** The `node.Type == "attribute"` block from `CSharpParser.MapNodeType`/`ExtractIdentifier` + `ExtractCSharpAttributeRoute`

**Create:** `Parsers/CodeExplorer.Parser.CSharp/Libraries/AspNetCoreLibraryParser.cs`

```csharp
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class AspNetCoreLibraryParser : ILibraryParser
{
    public string Name => "ASP.NET Core";
    public string Id => "aspnetcore";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["Microsoft.AspNetCore", "Microsoft.AspNetCore.Mvc"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> RouteAttributes = ["Route", "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRouteAttribute(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRouteAttribute(node)) return ExtractRoute(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsRouteAttribute(Node node)
    {
        if (node.Type != "attribute") return false;
        var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
        return nameNode != null && RouteAttributes.Contains(nameNode.Text);
    }

    private static string? ExtractRoute(Node node)
    {
        var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
        if (nameNode == null) return null;
        var name = nameNode.Text;
        if (!RouteAttributes.Contains(name)) return null;

        var argList = node.Children.FirstOrDefault(c => c.Type == "attribute_argument_list");
        string routeVal = "/";
        if (argList != null)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "attribute_argument");
            if (arg != null)
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode != null) routeVal = strNode.Text.Trim('"');
            }
        }
        return $"{(name == "Route" ? "GET" : name.Replace("Http", "").ToUpperInvariant())}:{routeVal}";
    }
}
```

**Wire it in** — add to `CSharpParser.LibraryParsers`:

```csharp
new AspNetCoreLibraryParser(),
```

**Delete from `CSharpParser`:** The `node.Type == "attribute"` block from `MapNodeType`, the `node.Type == "attribute"` block from `ExtractIdentifier`, and the `ExtractCSharpAttributeRoute` method.

**Build and verify** `Test_CSharpParser_ApiIngressEgress` still passes.

---

## Step 5 — `HttpClientLibraryParser` (C#)

**Moves:** `IsHttpClientCall` + `ExtractHttpClientTarget`

**Create:** `Parsers/CodeExplorer.Parser.CSharp/Libraries/HttpClientLibraryParser.cs`

```csharp
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class HttpClientLibraryParser : ILibraryParser
{
    public string Name => "HttpClient";
    public string Id => "httpclient";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["System.Net.Http"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    private static readonly HashSet<string> HttpMethods =
    [
        "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "SendAsync",
        "PostAsJsonAsync", "GetFromJsonAsync", "PatchAsync", "PutAsJsonAsync"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node)) return ExtractTarget(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsHttpClientCall(Node node)
    {
        if (node.Type != "invocation_expression") return false;
        var func = node.GetChildForField("function")
                   ?? (node.Children.Count > 0 ? node.Children[0] : null);
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_access_expression")
        {
            var nameChild = func.GetChildForField("name");
            if (nameChild != null && nameChild.Id != IntPtr.Zero)
                return HttpMethods.Contains(nameChild.Text);
        }
        return false;
    }

    private static string? ExtractTarget(Node node)
    {
        var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (argList != null)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "argument");
            if (arg != null)
            {
                var valNode = arg.Children.FirstOrDefault();
                if (valNode != null)
                {
                    var text = valNode.Text.Trim('"');
                    if (Uri.TryCreate(text, UriKind.Absolute, out var uri)) return uri.Host;
                    return $"http:{text}";
                }
            }
        }
        return "http:unknown-service";
    }
}
```

**Wire it in** — replace the existing `GenericLibraryParser("httpclient", ...)` entry in `CSharpParser.LibraryParsers` with:

```csharp
new HttpClientLibraryParser(),
```

**Delete from `CSharpParser`:** `IsHttpClientCall`, `ExtractHttpClientTarget`, and their call sites in `MapNodeType`/`ExtractIdentifier`.

**Build and verify** `Test_CSharpParser_ApiIngressEgress` still passes.

---

## Step 6 — Delete default constructors from SyntaxEnrichers

After all five parsers are done, remove the parameterless default constructors that create throwaway parser instances just to get `LibraryParsers`:

| File | Constructor to remove |
| --- | --- |
| `TypeScriptSyntaxEnricher.cs` | `public TypeScriptSyntaxEnricher() : base(new TypeScriptParser().LibraryParsers, syntaxTree)` |
| `CSharpSyntaxEnricher.cs` | `public CSharpSyntaxEnricher() : base(new CSharpParser().LibraryParsers, syntaxTree)` |
| `GoSyntaxEnricher.cs` | `public GoSyntaxEnricher() : base(new GoParser().LibraryParsers, syntaxTree)` |
| `PythonSyntaxEnricher.cs` | `public PythonSyntaxEnricher() : base(new PythonParser().LibraryParsers, syntaxTree)` |
| `TypeScriptSemanticModel.cs` (old debug file) | `public TypeScriptSemanticModel() : base(new TypeScriptParser().LibraryParsers, syntaxTree)` |

Enrichers must only be created via `parser.GetSyntaxEnricher(syntaxTree)`. The compiler will flag any remaining callers.

---

## Verification checklist

After all steps:

- [ ] `dotnet build` — zero errors
- [ ] `Test_CSharpParser_ApiIngressEgress` passes
- [ ] `Test_TypeScriptParser_ApiIngressEgress` passes
- [ ] `TypeScriptParser.MapNodeType` contains only the `node.Type switch` — no `if (IsXxx)` blocks
- [ ] `CSharpParser.MapNodeType` contains only the `node.Type switch` plus the string SQL check — no attribute detection block
- [ ] Search codebase for `IsTsDecoratorEntryPoint`, `IsExpressRoute`, `IsTsHttpClientCall`, `IsHttpClientCall` (CSharpParser), `ExtractCSharpAttributeRoute` — all should be gone
