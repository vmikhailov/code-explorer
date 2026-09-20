using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests.Shared;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class CypherFileSourceAttribute : TestCaseSourceAttribute
{
    public CypherFileSourceAttribute(string directory, string searchPattern = "*.cypher")
        : base(typeof(CypherTestData), nameof(CypherTestData.GetFiles), [directory, searchPattern])
    {
    }
}

