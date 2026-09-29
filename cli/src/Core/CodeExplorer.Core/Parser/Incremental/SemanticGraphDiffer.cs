namespace CodeExplorer.Core.Parser.Incremental;

/// <summary>
/// Result of comparing two semantic snapshots of a file.
/// </summary>
public class GraphPatch
{
    public string RelativePath { get; }
    public List<SymbolFootprint> AddedSymbols { get; } = [];
    public List<SymbolFootprint> RemovedSymbols { get; } = [];
    public List<SymbolFootprint> LogicOnlyChangedSymbols { get; } = [];
    public List<SymbolFootprint> LineShiftedSymbols { get; } = [];
    public Dictionary<string, (HashSet<string> Added, HashSet<string> Removed)> OutgoingCallDiffs { get; } = [];
    public Dictionary<string, (HashSet<string> Added, HashSet<string> Removed)> OutgoingTypeDiffs { get; } = [];

    public GraphPatch(string relativePath)
    {
        RelativePath = relativePath;
    }

    /// <summary>
    /// Indicates whether any graph relationships or nodes were added or removed.
    /// Returns false if changes are purely inner-logic (e.g. 2+2) or line shifts.
    /// </summary>
    public bool HasGraphStructuralChanges =>
        AddedSymbols.Count > 0 ||
        RemovedSymbols.Count > 0 ||
        OutgoingCallDiffs.Count > 0 ||
        OutgoingTypeDiffs.Count > 0;
}

/// <summary>
/// Fine-grained differ that compares an old file semantic snapshot with a new one.
/// Isolates inner-method logic modifications (0 graph writes) from true structural/call changes.
/// </summary>
public static class SemanticGraphDiffer
{
    public static GraphPatch ComputeDiff(FileGraphSnapshot oldSnapshot, FileGraphSnapshot newSnapshot)
    {
        var patch = new GraphPatch(newSnapshot.RelativePath);

        var oldSymbols = oldSnapshot.Symbols;
        var newSymbols = newSnapshot.Symbols;

        // 1. Identify added symbols
        foreach (var (id, newSym) in newSymbols)
        {
            if (!oldSymbols.TryGetValue(id, out var oldSym))
            {
                patch.AddedSymbols.Add(newSym);
            }
            else
            {
                // Both exist: compare footprints
                var lineShifted = oldSym.StartLine != newSym.StartLine ||
                                  oldSym.EndLine != newSym.EndLine ||
                                  oldSym.StartCol != newSym.StartCol ||
                                  oldSym.EndCol != newSym.EndCol;

                if (lineShifted)
                {
                    patch.LineShiftedSymbols.Add(newSym);
                }

                // Check signature and dependencies
                var signatureEqual = string.Equals(oldSym.SignatureHash, newSym.SignatureHash, StringComparison.Ordinal);
                var callsEqual = oldSym.OutgoingCalls.SetEquals(newSym.OutgoingCalls);
                var typesEqual = oldSym.OutgoingTypes.SetEquals(newSym.OutgoingTypes);

                if (signatureEqual && callsEqual && typesEqual)
                {
                    // Logic-only change (e.g. 2+2 or formatting)
                    if (!string.Equals(oldSym.BodyHash, newSym.BodyHash, StringComparison.Ordinal))
                    {
                        patch.LogicOnlyChangedSymbols.Add(newSym);
                    }
                }
                else
                {
                    // Calls or dependencies changed
                    if (!callsEqual)
                    {
                        var addedCalls = new HashSet<string>(newSym.OutgoingCalls);
                        addedCalls.ExceptWith(oldSym.OutgoingCalls);

                        var removedCalls = new HashSet<string>(oldSym.OutgoingCalls);
                        removedCalls.ExceptWith(newSym.OutgoingCalls);

                        if (addedCalls.Count > 0 || removedCalls.Count > 0)
                        {
                            patch.OutgoingCallDiffs[id] = (addedCalls, removedCalls);
                        }
                    }

                    if (!typesEqual)
                    {
                        var addedTypes = new HashSet<string>(newSym.OutgoingTypes);
                        addedTypes.ExceptWith(oldSym.OutgoingTypes);

                        var removedTypes = new HashSet<string>(oldSym.OutgoingTypes);
                        removedTypes.ExceptWith(newSym.OutgoingTypes);

                        if (addedTypes.Count > 0 || removedTypes.Count > 0)
                        {
                            patch.OutgoingTypeDiffs[id] = (addedTypes, removedTypes);
                        }
                    }

                    // If signature itself changed (e.g. parameters/return type renamed), mark as replaced
                    if (!signatureEqual)
                    {
                        patch.RemovedSymbols.Add(oldSym);
                        patch.AddedSymbols.Add(newSym);
                    }
                }
            }
        }

        // 2. Identify removed symbols
        foreach (var (id, oldSym) in oldSymbols)
        {
            if (!newSymbols.ContainsKey(id))
            {
                patch.RemovedSymbols.Add(oldSym);
            }
        }

        return patch;
    }
}
