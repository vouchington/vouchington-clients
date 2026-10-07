using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class TagRelationVoteEligibilityConverterTests
{
  [Theory]
  [InlineData(ElectionVoteChoice.Confirm, null, false, true)]
  [InlineData(ElectionVoteChoice.Dispute, null, false, true)]
  [InlineData(null, null, false, false)]
  [InlineData(null, ElectionVoteChoice.Confirm, false, false)]
  [InlineData(ElectionVoteChoice.Confirm, ElectionVoteChoice.Confirm, true, false)]
  public void AuthenticatedViewerEligibilityCombinesChoiceCurrentVoteAndMutationState(
      ElectionVoteChoice? targetChoice,
      ElectionVoteChoice? currentVote,
      bool isMutating,
      bool expected)
  {
    var converter = new TagRelationVoteEligibilityConverter(
        new SessionStore(new SessionSnapshot(new User("user-1", "alice", Roles: []))),
        () => false,
        targetChoice);

    Assert.Equal(expected, converter.Convert([currentVote, isMutating], typeof(bool), null, null!));
  }

  [Fact]
  public void AnonymousAndIneligibleUserTagSessionsCannotCreateVotes()
  {
    var anonymous = new TagRelationVoteEligibilityConverter(new SessionStore(SessionSnapshot.Anonymous), () => true, ElectionVoteChoice.Confirm);
    var investor = new TagRelationVoteEligibilityConverter(
        new SessionStore(new SessionSnapshot(new User("investor-1", "investor", Roles: ["investor"], AccountType: AccountType.Official))),
        () => true,
        ElectionVoteChoice.Dispute);
    var administrator = new TagRelationVoteEligibilityConverter(
        new SessionStore(new SessionSnapshot(new User("staff-1", "staff", Roles: ["administrator"]))),
        () => true,
        ElectionVoteChoice.Confirm);

    Assert.False((bool)anonymous.Convert([null, false], typeof(bool), null, null!));
    Assert.False((bool)investor.Convert([null, false], typeof(bool), null, null!));
    Assert.True((bool)administrator.Convert([null, false], typeof(bool), null, null!));
  }

  [Fact]
  public void OfficialSessionCanClearAHydratedBallot()
  {
    var official = new TagRelationVoteEligibilityConverter(
        new SessionStore(new SessionSnapshot(new User("staff-1", "staff", Roles: ["administrator"]))),
        () => false,
        null);

    Assert.True((bool)official.Convert([ElectionVoteChoice.Confirm, false], typeof(bool), null, null!));
    Assert.False((bool)official.Convert([null, false], typeof(bool), null, null!));
  }

  private sealed class SessionStore(SessionSnapshot current) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; } = current;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
