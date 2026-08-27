using Voucha.Client.Core.Api;
using Voucha.Client.Core.PaymentCards;
using Xunit;

namespace Voucha.Client.Core.Tests.PaymentCards;

public sealed class PaymentCardTests
{
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void WithoutDeletedParentClearsBothFieldsWhenEitherRepresentationMatches(bool tombstoneIsId)
  {
    const string tombstone = "deleted-parent";
    const string other = "other-parent";
    var parentId = tombstoneIsId ? tombstone : other;
    var summaryId = tombstoneIsId ? other : tombstone;
    var card = new PaymentCard(
        "child", "topic-child", null, null, null, null, true, parentId, null,
        new PaymentCardTopic("topic-child", "Child", "child"),
        new PaymentCardParentSummary(
            summaryId, null, null, new PaymentCardTopic("topic-parent", "Parent", "parent")));

    var cleared = card.WithoutDeletedParent(tombstone);

    Assert.Null(cleared.AuthorizedUserOfId);
    Assert.Null(cleared.AuthorizedUserOfCard);
    Assert.True(cleared.IsAuthorizedUser);
  }
}
