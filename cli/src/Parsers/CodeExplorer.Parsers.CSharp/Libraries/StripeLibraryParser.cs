using System.Collections.Frozen;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public sealed class StripeLibraryParser : DeclarativeSemanticExtension
{
    public StripeLibraryParser()
    {
        Id = "stripe";
        Name = "Stripe";
        Type = SemanticExtensionType.Cloud;
        LibraryRole = LibraryRole.CloudSdk;
        SupportedPatterns = ["Stripe", "Stripe.*", "Stripe.net"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "CreateAsync", "UpdateAsync", "GetAsync", "ListAsync", "DeleteAsync",
                    "CancelAsync", "CaptureAsync", "ConfirmAsync", "ConstructEvent"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[]
                {
                    "PaymentIntent", "Customer", "Subscription", "Checkout", "SessionService",
                    "Invoice", "Charge", "Refund", "Price", "Product", "Webhook", "Stripe"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "stripe:api"
            }
        ];
    }
}
