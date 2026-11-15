using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CodeExplorer.TreeSitter;

internal enum TSInputEncoding
{
    UTF8 = 0,
    UTF16 = 1
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativeTSPoint
{
    public readonly uint Row;
    public readonly uint Column;
}

public readonly record struct Point(int Row, int Column)
{
    internal Point(NativeTSPoint native)
        : this((int)native.Row, TreeSitterNative.ByteToCharIndex(native.Column))
    {
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct TSNode
{
    private readonly uint _context0;
    private readonly uint _context1;
    private readonly uint _context2;
    private readonly uint _context3;
    public readonly IntPtr Id;
    private readonly IntPtr _tree;

    public bool IsNull => Id == IntPtr.Zero;

    public bool IsNamed => !IsNull && TreeSitterNative.ts_node_is_named(this);

    public bool HasError => !IsNull && TreeSitterNative.ts_node_has_error(this);

    public int StartIndex => TreeSitterNative.ByteToCharIndex(TreeSitterNative.ts_node_start_byte(this));

    public int EndIndex => TreeSitterNative.ByteToCharIndex(TreeSitterNative.ts_node_end_byte(this));

    public Point StartPosition => new(TreeSitterNative.ts_node_start_point(this));

    public Point EndPosition => new(TreeSitterNative.ts_node_end_point(this));

    public string GetNodeType() => IsNull ? string.Empty : TreeSitterNativeLoader.GetCachedStaticString(TreeSitterNative.ts_node_type(this));

    public TreeCursor Walk() => new(this);
}

[StructLayout(LayoutKind.Sequential)]
internal struct TSTreeCursor
{
    public IntPtr Tree;
    public IntPtr Id;
    public uint Context0;
    public uint Context1;
    public uint Context2;
}

internal static unsafe class TreeSitterNativeLoader
{
    private const string GrammarLibPrefix = "tree-sitter-";
    private const string GrammarFuncPrefix = "tree_sitter_";
    private const string UnixLibPrefix = "lib";
    private const string RuntimesFolder = "runtimes";
    private const string NativeFolder = "native";
    private const string WinDllExt = ".dll";
    private const string OsxDylibExt = ".dylib";
    private const string LinuxSoExt = ".so";
    private const string RidWinX64 = "win-x64";
    private const string RidWinArm64 = "win-arm64";
    private const string RidOsxArm64 = "osx-arm64";
    private const string RidOsxX64 = "osx-x64";
    private const string RidLinuxX64 = "linux-x64";
    private const string RidLinuxArm64 = "linux-arm64";
    private const string NativeDllSearchDirectoriesKey = "NATIVE_DLL_SEARCH_DIRECTORIES";
    private const char HyphenChar = '-';
    private const char UnderscoreChar = '_';
    private const uint MinSupportedAbi = 13;
    private const uint MaxSupportedAbi = 15;

    private static readonly ConcurrentDictionary<IntPtr, string> StaticStringCache = new();
    private static readonly ConcurrentDictionary<string, IntPtr> LoadedLibraryHandles = new(StringComparer.OrdinalIgnoreCase);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetCachedStaticString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            return string.Empty;
        }
        return StaticStringCache.GetOrAdd(ptr, static p =>
        {
            var str = Marshal.PtrToStringUTF8(p) ?? string.Empty;
            return string.Intern(str);
        });
    }

    internal static string NormalizeLanguageId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }
        var trimmed = id.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "csharp" or "c#" or "cs" => "c-sharp",
            "ts" => "typescript",
            "js" => "javascript",
            "py" => "python",
            "rs" => "rust",
            "sh" or "shell" => "bash",
            _ => trimmed
        };
    }

    internal static IntPtr LoadLanguageExport(string languageId)
    {
        var normalized = NormalizeLanguageId(languageId);
        var libLogicalName = GrammarLibPrefix + normalized;
        var exportName = GrammarFuncPrefix + normalized.Replace(HyphenChar, UnderscoreChar);

        var libHandle = LoadNativeLibraryByName(libLogicalName);
        if (libHandle == IntPtr.Zero)
        {
            throw new DllNotFoundException($"Unable to load Tree-sitter native grammar library '{libLogicalName}'.");
        }

        if (!NativeLibrary.TryGetExport(libHandle, exportName, out var fnPtr) || fnPtr == IntPtr.Zero)
        {
            throw new EntryPointNotFoundException($"Entry point '{exportName}' not found in '{libLogicalName}'.");
        }

        var langFactory = (delegate* unmanaged[Cdecl]<IntPtr>)fnPtr;
        var langPtr = langFactory();
        if (langPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Grammar factory '{exportName}' returned null TSLanguage pointer.");
        }

        var abi = TreeSitterNative.ts_language_abi_version(langPtr);
        if (abi is < MinSupportedAbi or > MaxSupportedAbi)
        {
            throw new NotSupportedException($"Grammar '{normalized}' ABI version {abi} is outside supported range [{MinSupportedAbi}..{MaxSupportedAbi}].");
        }

        return langPtr;
    }

    internal static bool TryLoadLanguageExport(string languageId, out IntPtr langPtr)
    {
        langPtr = IntPtr.Zero;
        var normalized = NormalizeLanguageId(languageId);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        var libLogicalName = GrammarLibPrefix + normalized;
        var exportName = GrammarFuncPrefix + normalized.Replace(HyphenChar, UnderscoreChar);
        var libHandle = LoadNativeLibraryByName(libLogicalName);
        if (libHandle == IntPtr.Zero)
        {
            return false;
        }

        if (!NativeLibrary.TryGetExport(libHandle, exportName, out var fnPtr) || fnPtr == IntPtr.Zero)
        {
            return false;
        }

        var langFactory = (delegate* unmanaged[Cdecl]<IntPtr>)fnPtr;
        langPtr = langFactory();
        return langPtr != IntPtr.Zero;
    }

    internal static IntPtr ResolveDllImport(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath) =>
        LoadNativeLibraryByName(libraryName);

    private static IntPtr LoadNativeLibraryByName(string logicalName)
    {
        return LoadedLibraryHandles.GetOrAdd(logicalName, static name =>
        {
            var rid = GetCurrentRid();
            var fileName = GetPlatformLibraryFileName(name);

            foreach (var baseDir in GetProbeBaseDirectories())
            {
                var ridCandidate = Path.Combine(baseDir, RuntimesFolder, rid, NativeFolder, fileName);
                if (File.Exists(ridCandidate) && NativeLibrary.TryLoad(ridCandidate, out var ridHandle))
                {
                    return ridHandle;
                }

                var directCandidate = Path.Combine(baseDir, fileName);
                if (File.Exists(directCandidate) && NativeLibrary.TryLoad(directCandidate, out var directHandle))
                {
                    return directHandle;
                }
            }

            if (NativeLibrary.TryLoad(name, typeof(TreeSitterNativeLoader).Assembly, null, out var defaultHandle))
            {
                return defaultHandle;
            }

            return IntPtr.Zero;
        });
    }

    private static IEnumerable<string> GetProbeBaseDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir) && seen.Add(baseDir))
        {
            yield return baseDir;
        }

        if (AppContext.GetData(NativeDllSearchDirectoriesKey) is string nativeSearchDirs && !string.IsNullOrEmpty(nativeSearchDirs))
        {
            foreach (var dir in nativeSearchDirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (seen.Add(dir))
                {
                    yield return dir;
                }
            }
        }
    }

    private static string GetCurrentRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return arch == Architecture.Arm64 ? RidWinArm64 : RidWinX64;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return arch == Architecture.Arm64 ? RidOsxArm64 : RidOsxX64;
        }
        return arch == Architecture.Arm64 ? RidLinuxArm64 : RidLinuxX64;
    }

    private static string GetPlatformLibraryFileName(string logicalName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return logicalName + WinDllExt;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return UnixLibPrefix + logicalName + OsxDylibExt;
        }
        return UnixLibPrefix + logicalName + LinuxSoExt;
    }
}

internal static class TreeSitterNative
{
    private const string CoreLibraryName = "tree-sitter";

    static TreeSitterNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(TreeSitterNative).Assembly, TreeSitterNativeLoader.ResolveDllImport);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ByteToCharIndex(uint byteOffset) =>
        byteOffset >= int.MaxValue ? int.MaxValue : (int)(byteOffset >> 1);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ts_parser_new();

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ts_parser_delete(IntPtr self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool ts_parser_set_language(IntPtr self, IntPtr language);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ts_parser_parse_string_encoding(
        IntPtr self,
        IntPtr oldTree,
        IntPtr text,
        uint byteLength,
        TSInputEncoding encoding);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ts_tree_delete(IntPtr self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern TSNode ts_tree_root_node(IntPtr self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ts_node_type(TSNode self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint ts_node_start_byte(TSNode self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeTSPoint ts_node_start_point(TSNode self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint ts_node_end_byte(TSNode self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeTSPoint ts_node_end_point(TSNode self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool ts_node_is_named(TSNode self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool ts_node_has_error(TSNode self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern TSTreeCursor ts_tree_cursor_new(TSNode node);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ts_tree_cursor_delete(ref TSTreeCursor self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern TSNode ts_tree_cursor_current_node(ref TSTreeCursor self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ts_tree_cursor_current_field_name(ref TSTreeCursor self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool ts_tree_cursor_goto_parent(ref TSTreeCursor self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool ts_tree_cursor_goto_next_sibling(ref TSTreeCursor self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool ts_tree_cursor_goto_first_child(ref TSTreeCursor self);

    [DllImport(CoreLibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint ts_language_abi_version(IntPtr self);
}
