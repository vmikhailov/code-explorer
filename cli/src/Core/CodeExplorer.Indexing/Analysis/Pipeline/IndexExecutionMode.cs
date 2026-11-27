namespace CodeExplorer.Core.Parser;

public enum IndexExecutionMode : byte
{
    UpToDate = 0,
    LogicOnly = 1,
    Incremental = 2,
    Full = 3
}
