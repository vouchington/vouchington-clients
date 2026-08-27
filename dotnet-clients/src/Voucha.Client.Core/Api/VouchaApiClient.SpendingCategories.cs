namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<SpendingCategoriesResponse> FetchSpendingCategoriesAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      SendAsync<SpendingCategoriesResponse>(VouchaApiEndpoints.SpendingCategories(after, limit), cancellationToken);
  public Task<SpendingCategoryResponse> CreateSpendingCategoryAsync(CreateSpendingCategoryBody body, CancellationToken cancellationToken = default) =>
      SendAsync<SpendingCategoryResponse>(VouchaApiEndpoints.CreateSpendingCategory(body), cancellationToken);
  public Task<SpendingCategoryResponse> UpdateSpendingCategoryAsync(string id, UpdateSpendingCategoryBody body, CancellationToken cancellationToken = default) =>
      SendAsync<SpendingCategoryResponse>(VouchaApiEndpoints.UpdateSpendingCategory(id, body), cancellationToken);
  public Task DeleteSpendingCategoryAsync(string id, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteSpendingCategory(id), cancellationToken);
  public Task<TopicSearchResponse> SearchSpendingCategoryTopicsAsync(string query, int limit = 10, CancellationToken cancellationToken = default) =>
      SendAsync<TopicSearchResponse>(VouchaApiEndpoints.SpendingCategoryTopics(query, limit), cancellationToken);
}
