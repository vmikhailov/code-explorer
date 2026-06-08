using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class NodeSelector
{
    private readonly List<Func<Node?, Node?>> _steps = [];

    private NodeSelector() { }

    private NodeSelector(List<Func<Node?, Node?>> steps)
    {
        _steps.AddRange(steps);
    }

    public static NodeSelector New() => new();

    public NodeSelector HasType(string type)
    {
        return new NodeSelector([.. _steps, node => node.IsValid() && node!.Type == type ? node : null]);
    }

    public NodeSelector FirstChild => new([.. _steps, node =>
    {
        if (!node.IsValid()) return null;
        foreach (var child in node!.Children)
        {
            if (child.IsValid() && !string.IsNullOrEmpty(child.Type) && (char.IsLetter(child.Type[0]) || child.Type[0] == '_'))
            {
                return child;
            }
        }
        return null;
    }]);

    public NodeSelector FunctionNode => new([.. _steps, node => node.IsValid() ? node!.GetFunctionNode() : null]);

    public NodeSelector GetChildForField(string fieldName)
    {
        return new NodeSelector([.. _steps, node => node.IsValid() ? node!.GetChildForField(fieldName) : null]);
    }

    public NodeSelector Text(string regexPattern)
    {
        var parts = regexPattern.Split('|');
        return new NodeSelector([.. _steps, node =>
        {
            if (!node.IsValid()) return null;
            var text = node!.Text;
            foreach (var part in parts)
            {
                if (text == part) return node;
            }
            return null;
        }]);
    }

    public NodeSelector TextContains(string pattern)
    {
        var parts = pattern.Split('|');
        return new NodeSelector([.. _steps, node =>
        {
            if (!node.IsValid()) return null;
            var text = node!.Text;
            foreach (var part in parts)
            {
                if (text.Contains(part)) return node;
            }
            return null;
        }]);
    }

    public NodeSelector Where(Predicate<Node?> predicate)
    {
        return new NodeSelector([.. _steps, node => node.IsValid() && predicate(node) ? node : null]);
    }

    public NodeSelector Where(NodeSelector subSelector)
    {
        return new NodeSelector([.. _steps, node => node.IsValid() && subSelector.Matches(node) ? node : null]);
    }

    public NodeSelector HasChild(string fieldName, NodeSelector subSelector)
    {
        return new NodeSelector([.. _steps, node =>
        {
            if (!node.IsValid()) return null;
            var child = node!.GetChildForField(fieldName);
            return child.IsValid() && subSelector.Matches(child) ? node : null;
        }]);
    }

    public NodeSelector HasChild(NodeSelector subSelector)
    {
        return new NodeSelector([.. _steps, node =>
        {
            if (!node.IsValid()) return null;
            foreach (var child in node!.Children)
            {
                if (child.IsValid() && subSelector.Matches(child))
                {
                    return node;
                }
            }
            return null;
        }]);
    }

    public static NodeSelector Or(params NodeSelector[] selectors)
    {
        return New().Where(node =>
        {
            foreach (var selector in selectors)
            {
                if (selector.Matches(node)) return true;
            }
            return false;
        });
    }

    public bool Matches(Node? node)
    {
        var current = node;
        foreach (var step in _steps)
        {
            if (!current.IsValid()) return false;
            current = step(current);
        }
        return current.IsValid();
    }

    public Node? Select(Node? node)
    {
        var current = node;
        foreach (var step in _steps)
        {
            if (!current.IsValid()) return null;
            current = step(current);
        }
        return current.IsValid() ? current : null;
    }

    public static implicit operator Predicate<Node?>(NodeSelector selector)
    {
        return node => selector.Matches(node);
    }
}
