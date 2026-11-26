using System.Text.Json;

namespace CodeExplorer.Core.Parser;

public static class DeclarativeDetectorSchemaGenerator
{
    public const string StarterYamlTemplate = """
        # Place custom L4 framework/library detectors in .codeexplorer/detectors/<name>.yaml
        schema_version: 1
        id: acme-internal-rpc
        name: Acme Internal RPC & Events
        languages: [java]          # csharp, java, kotlin, go, python, typescript, javascript, rust, php, perl, coldfusion, or *
        type: framework            # framework, library, relational_db, document_db, key_value_db, queue, cloud, http_client, grpc_framework, graphql_framework
        library_role: web_service  # general, web_framework, web_service, orm_or_database, message_broker, egress_client, test_framework, worker_service, scheduler
        override: false            # set true only when intentionally replacing a built-in detector with the same id
        supported_patterns:
          - "com.acme.rpc*"
          - "*acme-rpc-core*"

        annotation_endpoints:
          - annotation_to_http_method:
              RpcQuery: GET
              RpcCommand: POST
            route_prefix_annotation_names: [RpcService]

        messaging_brokers:
          - protocol: acme-bus
            listener_annotation_names: [OnAcmeEvent]
            publish_method_names: [publishEvent]
            publish_receiver_substrings: [AcmeEventBus]
        """;

    private static class ReqFields
    {
        public static readonly string[] AnnotationToHttpMethod = ["annotation_to_http_method"];
        public static readonly string[] MethodToHttpVerb = ["method_to_http_verb"];
        public static readonly string[] CallMethodNames = ["call_method_names"];
        public static readonly string[] AnnotationNames = ["annotation_names"];
        public static readonly string[] BaseTypeSubstrings = ["base_type_substrings"];
    }

    public static string BuildJsonSchema()
    {
        var types = Enum.GetNames<SemanticExtensionType>().Select(WorkspaceDetectorLoader.ToSnakeCase).ToArray();
        var roles = Enum.GetNames<LibraryRole>().Select(WorkspaceDetectorLoader.ToSnakeCase).ToArray();
        var languages = WorkspaceDetectorLanguageMap.SupportedCanonicalLanguages.ToArray();

        var schemaObj = new Dictionary<string, object>
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["title"] = "CodeExplorer Declarative L4 Detector (v1)",
            ["description"] = "Declarative Layer-4 semantic detector definition (.codeexplorer/detectors/*.yaml) mapped 1:1 to DeclarativeSemanticExtension.",
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new[] { "schema_version", "id", "name", "languages", "supported_patterns" },
            ["properties"] = new Dictionary<string, object>
            {
                ["schema_version"] = new Dictionary<string, object> { ["type"] = "integer", ["const"] = 1 },
                ["id"] = new Dictionary<string, object> { ["type"] = "string", ["pattern"] = "^[a-z0-9][a-z0-9._-]{0,127}$" },
                ["name"] = new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 1 },
                ["languages"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["minItems"] = 1,
                    ["items"] = new Dictionary<string, object> { ["type"] = "string", ["examples"] = languages }
                },
                ["type"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = types, ["default"] = "framework" },
                ["library_role"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = roles, ["default"] = "general" },
                ["override"] = new Dictionary<string, object> { ["type"] = "boolean", ["default"] = false },
                ["supported_patterns"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["minItems"] = 1,
                    ["maxItems"] = 128,
                    ["items"] = new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 1 }
                },
                ["annotation_endpoints"] = ArrayOf(new Dictionary<string, object>
                {
                    ["annotation_to_http_method"] = StringMap(),
                    ["path_argument_keys"] = StringArray(),
                    ["route_prefix_annotation_names"] = StringArray(),
                    ["web_socket_annotation_names"] = StringArray(),
                    ["annotation_node_types"] = StringArray(),
                    ["enrich_java_security_and_schemas"] = BoolProp()
                }, ReqFields.AnnotationToHttpMethod),
                ["inbound_route_calls"] = ArrayOf(new Dictionary<string, object>
                {
                    ["method_to_http_verb"] = StringMap(),
                    ["receiver_substrings"] = StringArray(),
                    ["excluded_receivers"] = StringArray(),
                    ["group_method_names"] = StringArray(),
                    ["resolve_chained_route_groups"] = BoolProp(),
                    ["path_argument_index"] = IntProp(),
                    ["static_route"] = StringProp(),
                    ["route_prefix"] = StringProp(),
                    ["emit_entry_point_node"] = BoolProp(),
                    ["require_leading_slash_on_path"] = BoolProp(),
                    ["emit_handler_triggers"] = BoolProp(),
                    ["call_node_types"] = StringArray()
                }, ReqFields.MethodToHttpVerb),
                ["outbound_http"] = ArrayOf(new Dictionary<string, object>
                {
                    ["call_method_names"] = StringArray(),
                    ["client_annotation_suffixes"] = StringArray(),
                    ["fallback_target"] = StringProp(),
                    ["resolve_fluent_chains"] = BoolProp(),
                    ["call_node_types"] = StringArray(),
                    ["annotation_node_types"] = StringArray()
                }),
                ["sql_sinks"] = ArrayOf(new Dictionary<string, object>
                {
                    ["call_method_names"] = StringArray(),
                    ["query_annotation_suffixes"] = StringArray(),
                    ["require_extractable_sql_on_call"] = BoolProp(),
                    ["append_method_name_when_no_sql"] = BoolProp(),
                    ["fallback_query_prefix"] = StringProp(),
                    ["call_node_types"] = StringArray(),
                    ["annotation_node_types"] = StringArray()
                }),
                ["orm_tables"] = ArrayOf(new Dictionary<string, object>
                {
                    ["table_annotation_names"] = StringArray(),
                    ["name_argument_keys"] = StringArray(),
                    ["base_class_names"] = StringArray(),
                    ["repository_interface_names"] = StringArray(),
                    ["annotation_node_types"] = StringArray(),
                    ["class_declaration_node_types"] = StringArray()
                }),
                ["messaging_brokers"] = ArrayOf(new Dictionary<string, object>
                {
                    ["protocol"] = StringProp(),
                    ["listener_annotation_names"] = StringArray(),
                    ["producer_annotation_names"] = StringArray(),
                    ["channel_annotation_names"] = StringArray(),
                    ["method_level_event_annotations"] = StringArray(),
                    ["topic_argument_keys"] = StringArray(),
                    ["fallback_topic_name"] = StringProp(),
                    ["publish_method_names"] = StringArray(),
                    ["publish_receiver_substrings"] = StringArray(),
                    ["extract_event_type_from_object_creation_arg"] = BoolProp(),
                    ["prefer_second_arg_when_three_or_more_args"] = BoolProp(),
                    ["publish_topic_argument_index"] = IntProp(),
                    ["static_publish_topic"] = StringProp(),
                    ["subscribe_method_names"] = StringArray(),
                    ["subscribe_receiver_substrings"] = StringArray(),
                    ["consumer_interface_substrings"] = StringArray(),
                    ["resolve_fluent_chains"] = BoolProp(),
                    ["call_node_types"] = StringArray(),
                    ["annotation_node_types"] = StringArray(),
                    ["class_declaration_node_types"] = StringArray()
                }),
                ["data_store_access"] = ArrayOf(new Dictionary<string, object>
                {
                    ["call_method_names"] = StringArray(),
                    ["receiver_substrings"] = StringArray(),
                    ["target_selector_methods"] = StringArray(),
                    ["read_method_names"] = StringArray(),
                    ["write_method_names"] = StringArray(),
                    ["default_target_relation"] = StringProp(),
                    ["resolve_fluent_chains"] = BoolProp(),
                    ["table_argument_index"] = IntProp(),
                    ["static_target_name"] = StringProp(),
                    ["parse_sql_from_target"] = BoolProp(),
                    ["call_node_types"] = StringArray()
                }, ReqFields.CallMethodNames),
                ["service_call_sinks"] = ArrayOf(new Dictionary<string, object>
                {
                    ["call_method_names"] = StringArray(),
                    ["receiver_substrings"] = StringArray(),
                    ["target_relation"] = StringProp(),
                    ["static_target_name"] = StringProp(),
                    ["target_argument_index"] = IntProp(),
                    ["collect_all_identifier_arguments"] = BoolProp(),
                    ["require_url_or_path_argument"] = BoolProp(),
                    ["use_method_name_as_target"] = BoolProp(),
                    ["lowercase_method_name_target"] = BoolProp(),
                    ["allow_identifier_argument"] = BoolProp(),
                    ["target_prefix"] = StringProp(),
                    ["call_node_types"] = StringArray()
                }, ReqFields.CallMethodNames),
                ["annotation_sinks"] = ArrayOf(new Dictionary<string, object>
                {
                    ["annotation_names"] = StringArray(),
                    ["target_relation"] = StringProp(),
                    ["target_prefix"] = StringProp(),
                    ["use_annotation_name_as_target"] = BoolProp(),
                    ["static_target_name"] = StringProp(),
                    ["annotation_node_types"] = StringArray()
                }, ReqFields.AnnotationNames),
                ["class_hierarchy_sinks"] = ArrayOf(new Dictionary<string, object>
                {
                    ["base_type_substrings"] = StringArray(),
                    ["target_relation"] = StringProp(),
                    ["target_prefix"] = StringProp(),
                    ["target_suffix"] = StringProp(),
                    ["lowercase_class_name"] = BoolProp(),
                    ["emit_table_node"] = BoolProp(),
                    ["class_declaration_node_types"] = StringArray()
                }, ReqFields.BaseTypeSubstrings)
            }
        };

        return JsonSerializer.Serialize(schemaObj, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, object> ArrayOf(Dictionary<string, object> itemProps, string[]? required = null)
    {
        var item = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = itemProps
        };
        if (required is { Length: > 0 })
        {
            item["required"] = required;
        }
        return new Dictionary<string, object>
        {
            ["type"] = "array",
            ["items"] = item
        };
    }

    private static Dictionary<string, object> StringArray() => new() { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "string" } };
    private static Dictionary<string, object> StringMap() => new() { ["type"] = "object", ["additionalProperties"] = new Dictionary<string, object> { ["type"] = "string" } };
    private static Dictionary<string, object> StringProp() => new() { ["type"] = "string" };
    private static Dictionary<string, object> BoolProp() => new() { ["type"] = "boolean" };
    private static Dictionary<string, object> IntProp() => new() { ["type"] = "integer" };
}
