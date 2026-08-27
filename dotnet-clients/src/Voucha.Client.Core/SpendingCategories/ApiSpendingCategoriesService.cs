using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.SpendingCategories;

public sealed class ApiSpendingCategoriesService(VouchaApiClient client) : ISpendingCategoriesService
{
  private static readonly PageInfo TerminalPage = new(null, false, null);
  public async Task<SpendingCategoryPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    var response = await client.FetchSpendingCategoriesAsync(after, limit, cancellationToken).ConfigureAwait(false);
    return new(response.Results.Select(SpendingCategory.FromWire).ToArray(), response.PageInfo ?? TerminalPage);
  }
  public async Task<SpendingCategory> CreateAsync(CreateSpendingCategoryBody body, CancellationToken cancellationToken = default) =>
      SpendingCategory.FromWire((await client.CreateSpendingCategoryAsync(body, cancellationToken).ConfigureAwait(false)).SpendingCategory);
  public async Task<SpendingCategory> UpdateAsync(string id, UpdateSpendingCategoryBody body, CancellationToken cancellationToken = default) =>
      SpendingCategory.FromWire((await client.UpdateSpendingCategoryAsync(id, body, cancellationToken).ConfigureAwait(false)).SpendingCategory);
  public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => client.DeleteSpendingCategoryAsync(id, cancellationToken);
  public async Task<IReadOnlyList<SpendingCategoryOption>> SearchAsync(string query, CancellationToken cancellationToken = default)
  {
    var response = await client.SearchSpendingCategoryTopicsAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false);
    return response.Results.Where(value => value.Id is not null && value.Name is not null && value.Slug is not null)
        .Select(value => new SpendingCategoryOption(value.Id!, value.Name!, value.Slug!)).ToArray();
  }
}
