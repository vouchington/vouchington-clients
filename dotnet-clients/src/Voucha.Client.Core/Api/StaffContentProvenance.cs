using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

/// <summary>Raw creation facts supplied only to authorized staff responses.</summary>
public sealed record StaffContentProvenance(
    [property: JsonPropertyName("created_via")] string CreatedVia,
    [property: JsonPropertyName("oauth_client")] StaffProvenanceOAuthClient? OAuthClient = null);

public sealed record StaffProvenanceOAuthClient(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("metadata_url")] Uri? MetadataUrl,
    [property: JsonPropertyName("verified")] bool Verified);
