using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.SpendingCategories;

public interface ISpendingCategoriesService
{
  Task<SpendingCategoryPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default);
  Task<SpendingCategory> CreateAsync(CreateSpendingCategoryBody body, CancellationToken cancellationToken = default);
  Task<SpendingCategory> UpdateAsync(string id, UpdateSpendingCategoryBody body, CancellationToken cancellationToken = default);
  Task DeleteAsync(string id, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<SpendingCategoryOption>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
