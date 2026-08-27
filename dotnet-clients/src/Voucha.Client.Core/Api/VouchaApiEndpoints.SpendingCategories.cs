namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest SpendingCategories(string? after = null, int limit = 25) =>
      Get("/api/v1/my/spending-categories", Query(("after", after), ("limit", limit)));
  public static ApiRequest CreateSpendingCategory(CreateSpendingCategoryBody body) =>
      new(HttpMethod.Post, "/api/v1/my/spending-categories") { Body = body ?? throw new ArgumentNullException(nameof(body)) };
  public static ApiRequest UpdateSpendingCategory(string id, UpdateSpendingCategoryBody body) =>
      new(HttpMethod.Patch, $"/api/v1/my/spending-categories/{Path(id)}") { Body = body ?? throw new ArgumentNullException(nameof(body)) };
  public static ApiRequest DeleteSpendingCategory(string id) =>
      new(HttpMethod.Delete, $"/api/v1/my/spending-categories/{Path(id)}");
  public static ApiRequest SpendingCategoryTopics(string query, int limit = 10) =>
      Get("/api/v1/topics", Query(("q", query), ("spending_category", "true"), ("limit", limit)));
}
