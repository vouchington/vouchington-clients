using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record SpendingCategorySummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug);

public sealed record SpendingCategoryWire(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("spending_category_topic_id")] string SpendingCategoryId,
    [property: JsonPropertyName("amount")] Money Amount,
    [property: JsonPropertyName("spending_frequency")] string SpendingFrequency,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("owner_type")] string OwnerType,
    [property: JsonPropertyName("can_manage")] bool CanManage = true,
    [property: JsonPropertyName("spending_category")] SpendingCategorySummary SpendingCategory = null!);

public sealed record SpendingCategoriesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<SpendingCategoryWire> Results,
    [property: JsonPropertyName("page_info")] PageInfo? PageInfo);

public sealed record SpendingCategoryResponse(
    [property: JsonPropertyName("spending_category")] SpendingCategoryWire SpendingCategory);

public sealed record CreateSpendingCategoryBody(
    [property: JsonPropertyName("spending_category_topic_id")] string SpendingCategoryId,
    [property: JsonPropertyName("amount")] Money Amount,
    [property: JsonPropertyName("spending_frequency")] string SpendingFrequency,
    [property: JsonPropertyName("note")] string? Note = null);

public sealed record UpdateSpendingCategoryBody(
    [property: JsonPropertyName("amount")] Money? Amount = null,
    [property: JsonPropertyName("spending_frequency")] string? SpendingFrequency = null,
    [property: JsonPropertyName("note")] JsonNullableString? Note = null);
