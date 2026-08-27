namespace Voucha.Client.Core.Households;

public sealed partial class HouseholdViewModel
{
  private HouseholdNotificationPlan ApplyDisplayedHouseholdsLocked()
  {
    var notifications = new HouseholdNotificationPlan { Sections = true };
    var households = ownedHousehold is null
        ? memberHouseholdPagination.Items
        : new[] { ownedHousehold! }.Concat(memberHouseholdPagination.Items);
    var previous = Sections.ToDictionary(section => section.Id, StringComparer.Ordinal);
    sections = households.Select(household =>
    {
      var isOwned = household.Id == ownedHousehold?.Id;
      if (previous.TryGetValue(household.Id, out var existing))
      {
        existing.RefreshSilently(household, isOwned);
        notifications.RefreshedSections.Add(existing);
        return existing;
      }
      return new HouseholdSection(household, isOwned, localization);
    }).ToArray();
    return notifications;
  }

  private void NotifyMemberPaginationChanged()
  {
    OnPropertyChanged(nameof(HasMoreMemberHouseholds));
    OnPropertyChanged(nameof(IsLoadingMoreMemberHouseholds));
    OnPropertyChanged(nameof(HasMemberHouseholdError));
    OnPropertyChanged(nameof(ExternalContentMemberHouseholdError));
  }
}
