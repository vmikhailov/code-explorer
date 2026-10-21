using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Finds parser assemblies (marked with <see cref="ParserAssemblyAttribute"/>) and the instantiable parser types they export.
/// </summary>
public static class ParserAssemblyDiscovery
{
    private static readonly string AttributeName = nameof(ParserAssemblyAttribute);
    private static readonly string AttributeNamespace = typeof(ParserAssemblyAttribute).Namespace!;

    private static readonly Type[] ParserContracts =
    [
        typeof(IProjectParser),
        typeof(IFileParser),
        typeof(ISharedFileParser),
        typeof(Components.IComponentLibraryParser)
    ];

    /// <summary>
    /// Returns every parser assembly that is already loaded or deployed next to the application, ordered by name.
    /// Unloaded DLLs are inspected through their metadata and loaded only when they carry the marker attribute.
    /// </summary>
    public static IReadOnlyList<Assembly> Discover() => Discover(AppContext.BaseDirectory);

    public static IReadOnlyList<Assembly> Discover(string probeDirectory)
    {
        var parserAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(IsParserAssembly)
            .ToDictionary(a => a.GetName().Name ?? a.FullName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        foreach (var dllPath in EnumerateDlls(probeDirectory))
        {
            var name = Path.GetFileNameWithoutExtension(dllPath);
            if (parserAssemblies.ContainsKey(name) || !HasParserAssemblyAttribute(dllPath)) continue;

            var assembly = TryLoad(dllPath);
            if (assembly != null)
            {
                parserAssemblies[name] = assembly;
            }
        }

        return [.. parserAssemblies.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Value)];
    }

    public static bool IsParserAssembly(Assembly assembly) =>
        !assembly.IsDynamic && assembly.IsDefined(typeof(ParserAssemblyAttribute), inherit: false);

    /// <summary>Returns true if the type is a concrete class implementing one of the parser contracts.</summary>
    public static bool IsParserType(Type type) =>
        type is { IsAbstract: false, IsInterface: false } &&
        ParserContracts.Any(contract => contract.IsAssignableFrom(type));

    /// <summary>Parser types in the assembly that can be created through a public parameterless constructor, ordered by name.</summary>
    public static IReadOnlyList<Type> GetParserTypes(Assembly assembly) =>
    [
        .. GetLoadableExportedTypes(assembly)
            .Where(t => IsParserType(t) && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
    ];

    /// <summary>Reads the assembly manifest without loading it into the runtime.</summary>
    internal static bool HasParserAssemblyAttribute(string dllPath)
    {
        try
        {
            using var stream = File.OpenRead(dllPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return false;

            var md = pe.GetMetadataReader();
            if (!md.IsAssembly) return false;

            foreach (var handle in md.GetAssemblyDefinition().GetCustomAttributes())
            {
                var ctor = md.GetCustomAttribute(handle).Constructor;
                if (ctor.Kind != HandleKind.MemberReference) continue;

                var parent = md.GetMemberReference((MemberReferenceHandle)ctor).Parent;
                if (parent.Kind != HandleKind.TypeReference) continue;

                var typeRef = md.GetTypeReference((TypeReferenceHandle)parent);
                if (md.StringComparer.Equals(typeRef.Name, AttributeName) &&
                    md.StringComparer.Equals(typeRef.Namespace, AttributeNamespace))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException)
        {
            // Native libraries and locked files are not parser assemblies.
        }

        return false;
    }

    private static IEnumerable<string> EnumerateDlls(string directory)
    {
        if (!Directory.Exists(directory)) return [];

        try
        {
            return Directory.GetFiles(directory, "*.dll");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ParserAssemblyDiscovery] Warning: Failed to enumerate directory '{directory}': {ex.Message}");
            return [];
        }
    }

    private static Assembly? TryLoad(string dllPath)
    {
        try
        {
            return Assembly.LoadFrom(dllPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ParserAssemblyDiscovery] Warning: Could not load parser assembly '{dllPath}': {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<Type> GetLoadableExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var loaderEx in ex.LoaderExceptions.OfType<Exception>())
            {
                Console.Error.WriteLine($"[ParserAssemblyDiscovery] Warning: Loader error in '{assembly.GetName().Name}': {loaderEx.Message}");
            }
            return ex.Types.OfType<Type>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ParserAssemblyDiscovery] Warning: Failed to load exported types from '{assembly.GetName().Name}': {ex.Message}");
            return [];
        }
    }
}
