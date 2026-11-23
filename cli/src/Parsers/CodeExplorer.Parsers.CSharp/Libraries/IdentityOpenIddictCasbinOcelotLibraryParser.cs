using System.Collections.Frozen;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public class IdentityOpenIddictCasbinOcelotLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "identity-openiddict-casbin-ocelot";
    private const string ExtensionName = "Duende IdentityServer, OpenIddict, Casbin.NET, Ocelot, GraphQL.NET & CoreWCF/SoapCore";
    private const string PolicyPrefix = "authz:";

    public IdentityOpenIddictCasbinOcelotLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "Duende.IdentityServer*",
            "OpenIddict*",
            "Casbin.NET*",
            "NetCasbin*",
            "Ocelot*",
            "GraphQL*",
            "CoreWCF*",
            "SoapCore*",
            "protobuf-net.Grpc*",
            "OllamaSharp*",
            "Betalgo.OpenAI*",
            "Abp*",
            "Volo.Abp*",
            "OrchardCore*",
            "FluentValidation*",
            "Serilog*",
            "NLog*",
            "Autofac*",
            "Scrutor*",
            "SimpleInjector*",
            "AutoMapper*",
            "Mapster*",
            "Riok.Mapperly*",
            "Ardalis*",
            "FluentResults*",
            "ErrorOr*",
            "OneOf*",
            "LanguageExt*",
            "Aspire*",
            "Microsoft.Data.Sqlite*",
            "MySqlConnector*",
            "MailKit*",
            "MimeKit*",
            "Finbuckle.MultiTenant*",
            "Microsoft.Kiota*",
            "Microsoft.Playwright*",
            "Microsoft.Extensions.Hosting*",
            "Microsoft.Extensions.Diagnostics.HealthChecks*"
        ];
        AnnotationSinkRules =
        [
            new AnnotationSinkRule
            {
                AnnotationNames = new[]
                {
                    "Authorize", "AllowAnonymous", "OperationContract", "ServiceContract", "AbpAuthorize"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetPrefix = PolicyPrefix,
                TargetRelation = OntologyConstants.Relationships.Calls
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "Enforce", "EnforceAsync", "CreateCompletion", "GenerateAsync", "ChatAsync"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "enforcer", "casbin", "ollama", "openAi", "chat" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "auth-or-llm:service"
            }
        ];
    }
}
