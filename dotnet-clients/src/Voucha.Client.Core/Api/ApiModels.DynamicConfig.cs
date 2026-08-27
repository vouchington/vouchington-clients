using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record DynamicConfigNamespaceSummary(
    [property: JsonPropertyName("namespace")] string Namespace,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("field_count")] int FieldCount,
    [property: JsonPropertyName("can_view")] bool CanView,
    [property: JsonPropertyName("can_update")] bool CanUpdate);

public sealed record DynamicConfigNamespacesResponse(
    [property: JsonPropertyName("namespaces")] IReadOnlyList<DynamicConfigNamespaceSummary> Namespaces);

public sealed record DynamicConfigField(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("value")] DynamicConfigValue Value,
    [property: JsonPropertyName("default_value")] DynamicConfigValue? DefaultValue,
    [property: JsonPropertyName("min_value")] double? MinValue = null,
    [property: JsonPropertyName("max_value")] double? MaxValue = null,
    [property: JsonPropertyName("integer")] bool IsInteger = false,
    [property: JsonPropertyName("max_value_exemption")] string? MaxValueExemption = null);

public sealed record DynamicConfigNamespace(
    [property: JsonPropertyName("namespace")] string Namespace,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("field_count")] int FieldCount,
    [property: JsonPropertyName("can_view")] bool CanView,
    [property: JsonPropertyName("can_update")] bool CanUpdate,
    [property: JsonPropertyName("config")] IReadOnlyDictionary<string, DynamicConfigValue> Config,
    [property: JsonPropertyName("fields")] IReadOnlyList<DynamicConfigField> Fields);

public sealed record DynamicConfigNamespaceResponse(
    [property: JsonPropertyName("namespace")] DynamicConfigNamespace Namespace);

public sealed record DynamicConfigUpdateResponse(
    [property: JsonPropertyName("changed")] bool Changed,
    [property: JsonPropertyName("namespace")] DynamicConfigNamespace Namespace);

public sealed record DynamicConfigPatchBody(
    [property: JsonPropertyName("config")] IReadOnlyDictionary<string, DynamicConfigValue> Config);
