using Voucha.Client.Core.Api;
using System.Text.Json;

namespace Voucha.Client.Core.SpendingCategories;

public sealed record SpendingCategory(
    string Id, string SpendingCategoryId, Money Amount, string SpendingFrequency,
    string? Note, string OwnerType, bool CanManage, SpendingCategorySummary Topic)
{
  internal static SpendingCategory FromWire(SpendingCategoryWire wire)
  {
    ArgumentNullException.ThrowIfNull(wire);
    return wire.SpendingCategory is { Id: not null, Name: not null, Slug: not null } topic
        ? new(wire.Id, wire.SpendingCategoryId, wire.Amount, wire.SpendingFrequency,
            wire.Note, wire.OwnerType, wire.CanManage, topic)
        : throw new JsonException("Spending category responses require a complete spending_category object.");
  }
}

public sealed record SpendingCategoryOption(string Id, string Name, string Slug);
public sealed record SpendingCategoryPage(IReadOnlyList<SpendingCategory> Results, PageInfo PageInfo);
