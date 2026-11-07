using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class OpenTelemetryDotNetLibraryParser : DeclarativeSemanticExtension
{
    public OpenTelemetryDotNetLibraryParser()
    {
        Id = "opentelemetry-dotnet";
        Name = "OpenTelemetry .NET";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "OpenTelemetry",
            "OpenTelemetry.*",
            "System.Diagnostics.DiagnosticSource"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "StartActivity", "AddOpenTelemetry" }.ToFrozenSet(),
                TargetRelation = OntologyConstants.Relationships.Calls,
                TargetPrefix = "otel:",
                UseMethodNameAsTarget = true
            }
        ];
    }
}
