namespace Voucha.Client.Core.Navigation;

public sealed record BottomTabPreferences(
    IReadOnlyList<string> OrderedIntentIds,
    IReadOnlyCollection<string> HiddenIntentIds)
{
  public static BottomTabPreferences Default { get; } =
      new(Array.Empty<string>(), Array.Empty<string>());

  public IReadOnlyList<NavIntent> Apply(IReadOnlyList<NavIntent> visibleBottomTabs)
  {
    ArgumentNullException.ThrowIfNull(visibleBottomTabs);

    var byId = visibleBottomTabs.ToDictionary(tab => tab.Id, StringComparer.Ordinal);
    var hidden = HiddenIntentIds.ToHashSet(StringComparer.Ordinal);
    var orderedSet = OrderedIntentIds.ToHashSet(StringComparer.Ordinal);
    var orderedIds = OrderedIntentIds
        .Where(byId.ContainsKey)
        .Concat(visibleBottomTabs.Select(tab => tab.Id).Where(id => !orderedSet.Contains(id)))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    var tabs = orderedIds
        .Where(id => !hidden.Contains(id))
        .Select(id => byId[id])
        .ToArray();

    return tabs.Length > 0 ? tabs : visibleBottomTabs.Take(1).ToArray();
  }

  public BottomTabPreferences Normalize(IReadOnlyList<NavIntent> visibleBottomTabs)
  {
    ArgumentNullException.ThrowIfNull(visibleBottomTabs);

    var visibleIds = visibleBottomTabs.Select(tab => tab.Id).ToArray();
    var visibleIdSet = visibleIds.ToHashSet(StringComparer.Ordinal);
    var ordered = OrderedIntentIds
        .Where(visibleIdSet.Contains)
        .Concat(visibleIds.Where(id => !OrderedIntentIds.Contains(id, StringComparer.Ordinal)))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    var hidden = HiddenIntentIds
        .Where(visibleIdSet.Contains)
        .ToHashSet(StringComparer.Ordinal);

    if (ordered.All(hidden.Contains) && ordered.Length > 0)
    {
      hidden.Remove(ordered[0]);
    }

    return new BottomTabPreferences(ordered, hidden);
  }
}
