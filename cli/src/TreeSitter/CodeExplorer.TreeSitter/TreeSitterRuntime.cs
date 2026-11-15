namespace CodeExplorer.TreeSitter;

public sealed class Language : IDisposable
{
    internal IntPtr Handle { get; }
    public string Name { get; }

    public Language(string name)
    {
        Name = TreeSitterNativeLoader.NormalizeLanguageId(name);
        Handle = TreeSitterNativeLoader.LoadLanguageExport(Name);
    }

    public static bool IsAvailable(string name) =>
        TreeSitterNativeLoader.TryLoadLanguageExport(name, out _);

    public void Dispose()
    {
        // Static grammar descriptors live for the lifetime of the native module.
    }
}

public sealed unsafe class Parser : IDisposable
{
    private IntPtr _handle;
    private int _disposed;

    public Language Language { get; }

    public Parser(Language language)
    {
        ArgumentNullException.ThrowIfNull(language);
        Language = language;
        _handle = TreeSitterNative.ts_parser_new();
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to allocate native TSParser instance.");
        }

        if (!TreeSitterNative.ts_parser_set_language(_handle, language.Handle))
        {
            TreeSitterNative.ts_parser_delete(_handle);
            _handle = IntPtr.Zero;
            throw new InvalidOperationException($"Failed to assign Tree-sitter language '{language.Name}'.");
        }
    }

    public Tree? Parse(string source)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        source ??= string.Empty;

        fixed (char* charPtr = source)
        {
            var byteLength = (uint)(source.Length * sizeof(char));
            var treePtr = TreeSitterNative.ts_parser_parse_string_encoding(
                _handle,
                IntPtr.Zero,
                (IntPtr)charPtr,
                byteLength,
                TSInputEncoding.UTF16);

            return treePtr != IntPtr.Zero ? new Tree(treePtr) : null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _handle != IntPtr.Zero)
        {
            TreeSitterNative.ts_parser_delete(_handle);
            _handle = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~Parser()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _handle != IntPtr.Zero)
        {
            TreeSitterNative.ts_parser_delete(_handle);
            _handle = IntPtr.Zero;
        }
    }
}

public sealed class Tree : IDisposable
{
    private IntPtr _handle;
    private int _disposed;

    public TSNode RootNode { get; }

    internal Tree(IntPtr handle)
    {
        _handle = handle;
        RootNode = TreeSitterNative.ts_tree_root_node(handle);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _handle != IntPtr.Zero)
        {
            TreeSitterNative.ts_tree_delete(_handle);
            _handle = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~Tree()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _handle != IntPtr.Zero)
        {
            TreeSitterNative.ts_tree_delete(_handle);
            _handle = IntPtr.Zero;
        }
    }
}

public struct TreeCursor : IDisposable
{
    private TSTreeCursor _cursor;
    private int _disposed;

    internal TreeCursor(TSNode rootNode)
    {
        _cursor = TreeSitterNative.ts_tree_cursor_new(rootNode);
        _disposed = 0;
    }

    public TSNode CurrentNode => TreeSitterNative.ts_tree_cursor_current_node(ref _cursor);

    public string? CurrentFieldName
    {
        get
        {
            var ptr = TreeSitterNative.ts_tree_cursor_current_field_name(ref _cursor);
            return ptr == IntPtr.Zero ? null : TreeSitterNativeLoader.GetCachedStaticString(ptr);
        }
    }

    public bool GotoFirstChild() => TreeSitterNative.ts_tree_cursor_goto_first_child(ref _cursor);

    public bool GotoNextSibling() => TreeSitterNative.ts_tree_cursor_goto_next_sibling(ref _cursor);

    public bool GotoParent() => TreeSitterNative.ts_tree_cursor_goto_parent(ref _cursor);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _cursor.Tree != IntPtr.Zero)
        {
            TreeSitterNative.ts_tree_cursor_delete(ref _cursor);
            _cursor = default;
        }
    }
}
