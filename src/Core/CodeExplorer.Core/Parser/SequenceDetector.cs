using TreeSitter;

namespace CodeExplorer.Core.Parser;

public enum SequenceMatchStrategy
{
    /// <summary>
    /// The end of the current path must match the pattern.
    /// </summary>
    Suffix,

    /// <summary>
    /// The entire current path must match the pattern (lengths must be equal).
    /// </summary>
    Exact,

    /// <summary>
    /// The pattern must appear as a subsequence in the current path (elements match in order, but not necessarily contiguously).
    /// </summary>
    Subsequence
}

public class SequenceDetector<T>
{
    private readonly List<T> _currentPath = [];
    private readonly List<SequenceRule<T>> _rules = [];

    public IReadOnlyList<T> CurrentPath => _currentPath;

    public void Push(T element)
    {
        _currentPath.Add(element);
        CheckRules();
    }

    public void Pop()
    {
        if (_currentPath.Count > 0)
        {
            _currentPath.RemoveAt(_currentPath.Count - 1);
        }
    }

    public void Clear()
    {
        _currentPath.Clear();
    }

    public void Register(
        IEnumerable<Predicate<T>> pattern,
        Action<IReadOnlyList<T>> callback,
        SequenceMatchStrategy strategy = SequenceMatchStrategy.Suffix)
    {
        _rules.Add(new SequenceRule<T>(pattern.ToList(), callback, strategy));
    }

    public void Register(
        IEnumerable<Predicate<T>> pattern,
        Action callback,
        SequenceMatchStrategy strategy = SequenceMatchStrategy.Suffix)
    {
        Register(pattern, _ => callback(), strategy);
    }

    public void Register(
        IEnumerable<T> pattern,
        Action<IReadOnlyList<T>> callback,
        SequenceMatchStrategy strategy = SequenceMatchStrategy.Suffix)
    {
        var predicates = pattern.Select(item => new Predicate<T>(x => EqualityComparer<T>.Default.Equals(x, item)));
        Register(predicates, callback, strategy);
    }

    public void Register(
        IEnumerable<T> pattern,
        Action callback,
        SequenceMatchStrategy strategy = SequenceMatchStrategy.Suffix)
    {
        Register(pattern, _ => callback(), strategy);
    }

    private void CheckRules()
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(_currentPath))
            {
                rule.Callback(_currentPath);
            }
        }
    }
}

internal record SequenceRule<T>(
    List<Predicate<T>> Pattern,
    Action<IReadOnlyList<T>> Callback,
    SequenceMatchStrategy Strategy)
{
    public bool Matches(List<T> currentPath)
    {
        switch (Strategy)
        {
            case SequenceMatchStrategy.Exact:
                if (currentPath.Count != Pattern.Count) return false;
                return !Pattern.Where((t, i) => !t(currentPath[i])).Any();

            case SequenceMatchStrategy.Subsequence:
                var patternIndex = 0;
                for (var i = 0; i < currentPath.Count && patternIndex < Pattern.Count; i++)
                {
                    if (Pattern[patternIndex](currentPath[i]))
                    {
                        patternIndex++;
                    }
                }
                return patternIndex == Pattern.Count;

            case SequenceMatchStrategy.Suffix:
            default:
                if (currentPath.Count < Pattern.Count) return false;
                for (var i = 0; i < Pattern.Count; i++)
                {
                    var pathIndex = currentPath.Count - Pattern.Count + i;
                    if (!Pattern[i](currentPath[pathIndex])) return false;
                }
                return true;
        }
    }
}

public static class SequenceDetectorExtensions
{
    public static void RegisterNodeTypes(
        this SequenceDetector<Node> detector,
        IEnumerable<string> nodeTypes,
        Action<IReadOnlyList<Node>> callback,
        SequenceMatchStrategy strategy = SequenceMatchStrategy.Suffix)
    {
        var predicates = nodeTypes.Select(type => new Predicate<Node>(node => node.Type == type));
        detector.Register(predicates, callback, strategy);
    }

    public static void RegisterNodeTypes(
        this SequenceDetector<Node> detector,
        IEnumerable<string> nodeTypes,
        Action callback,
        SequenceMatchStrategy strategy = SequenceMatchStrategy.Suffix)
    {
        detector.RegisterNodeTypes(nodeTypes, _ => callback(), strategy);
    }
}
