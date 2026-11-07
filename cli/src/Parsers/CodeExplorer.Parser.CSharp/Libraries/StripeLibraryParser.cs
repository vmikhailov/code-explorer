namespace CodeExplorer.Parser.CSharp.Libraries;

public sealed class StripeLibraryParser : DeclarativeSemanticExtension
{
    public StripeLibraryParser()
    {
        Id = "stripe";
        Name = "Stripe";
        Type = SemanticExtensionType.Cloud;
        LibraryRole = LibraryRole.CloudSdk;
        SupportedPatterns = ["Stripe", "Stripe.*", "Stripe.net"];
    }
}
