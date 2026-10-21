namespace CodeExplorer.Core.Parser;

/// <summary>
/// Marks an assembly as containing parsers. <see cref="ParserAssemblyDiscovery"/> only loads and scans
/// assemblies that carry this attribute; parser types in unmarked assemblies are never auto-registered.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class ParserAssemblyAttribute : Attribute
{
}
