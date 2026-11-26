using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

/// <summary>
/// Declarative semantic extension for Google Web Toolkit (GWT RPC servlets, gwt-dispatch handlers, and UiBinder views).
/// </summary>
public class GwtLibraryParser : DeclarativeSemanticExtension
{
    public GwtLibraryParser()
    {
        Id = "google-gwt";
        Name = "Google Web Toolkit (GWT)";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "com.google.gwt*",
            "*gwt-user*",
            "*gwt-servlet*",
            "*gwt-dev*",
            "*gwt-codeserver*",
            "*gwt-gin*",
            "*gwt-presenter*",
            "*gwt-dispatch*",
            "*gwt-log*",
            "*gwteventservice*",
            "net.customware.gwt*",
            "com.allen-sauer.gwt*",
            "de.novanic.eventservice*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["RemoteServiceRelativePath"] = "POST"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                PathArgumentKeys = new[] { "value" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        ClassHierarchySinkRules =
        [
            new ClassHierarchySinkRule
            {
                BaseTypeSubstrings = new[] { "RemoteServiceServlet", "ActionHandler", "AbstractActionHandler" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Exposes,
                TargetPrefix = "gwt-rpc:"
            }
        ];
    }
}
