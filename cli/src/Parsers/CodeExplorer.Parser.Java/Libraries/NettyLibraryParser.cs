using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class NettyLibraryParser : DeclarativeSemanticExtension
{
    public NettyLibraryParser()
    {
        Id = "netty";
        Name = "Netty";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["io.netty.*"];
        ClassHierarchySinkRules =
        [
            new ClassHierarchySinkRule
            {
                BaseTypeSubstrings = new[]
                {
                    "ChannelInboundHandlerAdapter", "SimpleChannelInboundHandler"
                }.ToFrozenSet(StringComparer.Ordinal),
                TargetPrefix = "TCP:/netty/",
                TargetRelation = OntologyConstants.Relationships.Exposes
            }
        ];
    }
}
