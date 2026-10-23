using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ScopeCatalogEntry(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("audience")] string Audience,
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("surfaces")] IReadOnlyList<string> Surfaces,
    [property: JsonPropertyName("description_key")] string? DescriptionKey,
    [property: JsonPropertyName("requires")] string? Requires);

public sealed record ScopeAudiencePolicy(
    [property: JsonPropertyName("audience")] string Audience,
    [property: JsonPropertyName("accepts_api_keys")] bool AcceptsApiKeys,
    [property: JsonPropertyName("requires_administrator")] bool RequiresAdministrator);

[method: JsonConstructor]
public sealed record ScopeCatalogResponse(
    [property: JsonPropertyName("scopes")] IReadOnlyList<ScopeCatalogEntry> Scopes,
    [property: JsonPropertyName("audiences")] IReadOnlyList<ScopeAudiencePolicy> Audiences)
{
  public ScopeCatalogResponse(IReadOnlyList<ScopeCatalogEntry> scopes)
      : this(scopes, [])
  {
  }
}

public sealed record OAuthGrantClient(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("verified")] bool Verified);

public sealed record OAuthGrant(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("client")] OAuthGrantClient Client,
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("scopes")] IReadOnlyList<string> Scopes,
    [property: JsonPropertyName("consented_at")] DateTimeOffset ConsentedAt,
    [property: JsonPropertyName("last_used_at"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? LastUsedAt,
    [property: JsonPropertyName("attestation_status"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? AttestationStatus = null,
    [property: JsonPropertyName("session_id"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? SessionId = null);

public sealed record OAuthGrantListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<OAuthGrant> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
