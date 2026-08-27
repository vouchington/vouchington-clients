using System.ComponentModel;
using Voucha.Client.Core.Households;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

public sealed class HouseholdSectionStateTests
{
  [Fact]
  public void EmptyStateRequiresSettledSuccessfulLoadAndNotifiesWhenItChanges()
  {
    var section = new HouseholdSection(FakeHouseholdService.Household("owned", "me"), isOwned: true);
    var notifications = new List<string?>();
    section.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

    Assert.True(section.ShowsEmptyState);
    var request = Assert.IsType<Voucha.Client.Core.Pagination.CursorPageRequest>(section.BeginNextPage());
    Assert.False(section.ShowsEmptyState);
    section.ApplyMembers(request, [], new Voucha.Client.Core.Api.PageInfo(null, false, null));

    Assert.True(section.ShowsEmptyState);
    Assert.Contains(nameof(HouseholdSection.ShowsEmptyState), notifications);

    section.BeginLoad();
    section.FailLoad("failed");
    Assert.False(section.ShowsEmptyState);
  }
}
