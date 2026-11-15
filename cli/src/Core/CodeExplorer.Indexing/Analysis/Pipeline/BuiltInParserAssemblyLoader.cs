using System.Reflection;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Anchors built-in parser assemblies in metadata so single-file and trimmed CLI builds
/// load all 13 parser assemblies into the current AppDomain prior to attribute discovery.
/// </summary>
internal static class BuiltInParserAssemblyLoader
{
    private static readonly Assembly[] AnchoredAssemblies =
    [
        typeof(Parsers.Android.AndroidManifestFileParser).Assembly,
        typeof(Parsers.ColdFusion.ColdFusionFileParser).Assembly,
        typeof(Parsers.CSharp.CSharpFileParser).Assembly,
        typeof(Parsers.Go.GoFileParser).Assembly,
        typeof(Parsers.Java.JavaFileParser).Assembly,
        typeof(Parsers.Kotlin.KotlinProjectParser).Assembly,
        typeof(Parsers.Python.PythonFileParser).Assembly,
        typeof(Parsers.Rust.RustFileParser).Assembly,
        typeof(Parsers.Script.ScriptProjectParser).Assembly,
        typeof(Parsers.Shared.OpenApiFileParser).Assembly,
        typeof(Parsers.Sql.SqlFileParser).Assembly,
        typeof(Parsers.Terraform.TerraformFileParser).Assembly,
        typeof(Parsers.TypeScript.TypeScriptFileParser).Assembly
    ];

    public static IReadOnlyList<Assembly> EnsureLoaded() => AnchoredAssemblies;
}
