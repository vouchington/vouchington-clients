using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record UserList(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("owner_user_id")] string OwnerUserId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("visibility")] string Visibility,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("removed_at")] DateTimeOffset? RemovedAt = null,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record ListItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("list_id")] string ListId,
    [property: JsonPropertyName("item_type")] string ItemType,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("order_index")] int? OrderIndex,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("media_type")] string? MediaType,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record ListsSearchResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("lists")] IReadOnlyDictionary<string, UserList> Lists);

public sealed record ListItemsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<EntityReference> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("list_items")] IReadOnlyDictionary<string, ListItem> ListItems);

public sealed record ListResponse([property: JsonPropertyName("list")] UserList List);

public sealed record ListItemResponse([property: JsonPropertyName("list_item")] ListItem ListItem);

public sealed record ListResponse<T>(
    [property: JsonPropertyName("results")] IReadOnlyList<T> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ListsContainingResponse([property: JsonPropertyName("list_ids")] IReadOnlyList<string> ListIds);

public sealed record ImportCommunityListResponse(
    [property: JsonPropertyName("posts")] int Posts,
    [property: JsonPropertyName("items")] int Items);
