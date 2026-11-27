namespace CodeExplorer.Core.Parser;

public readonly record struct ModuleInitStub(
    string FileId,
    string FilePath,
    string FullPath,
    string FunctionId);
