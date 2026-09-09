using System;
using NUnit.Framework;

namespace CodeExplorer.Tests.Shared;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class ParserFileSourceAttribute : TestCaseSourceAttribute
{
    public ParserFileSourceAttribute(string directory, string searchPattern = "*.test")
        : base(typeof(ParserTestData), nameof(ParserTestData.GetFiles), new object[] { directory, searchPattern })
    {
    }
}

