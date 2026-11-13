using Voucha.Client.Core.Api;
using Voucha.Client.Core.Households;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

public sealed class HouseholdInvitationsViewModelTests
{
  [Fact]
  public async Task ReceivedPaginationForwardsCursorDeduplicatesAndKeepsRemovedRowsGone()
  {
    var first = HouseholdInvitationsTestService.Invitation();
    var second = HouseholdInvitationsTestService.Invitation("second");
    var service = new HouseholdInvitationsTestService
    {
      Fetch = cursor => Task.FromResult(cursor is null
          ? new HouseholdInvitationsResponse([first], new("opaque+/=", true, null))
          : HouseholdInvitationsTestService.Page(first, second)),
    };
    using var model = new HouseholdInvitationsViewModel(service);
    Assert.True(model.IsReceived);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasMore);
    Assert.Single(model.Rows);
    Assert.True(await model.AcceptAsync(first, TestContext.Current.CancellationToken));
    Assert.Empty(model.Rows);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("second", Assert.Single(model.Rows).Invitation.Id);
    Assert.Null(service.Requests[1].Household);
    Assert.Equal("opaque+/=", service.Requests[1].Cursor);
    Assert.Equal(25, service.Requests[1].Limit);
    Assert.False(model.HasMore);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, service.Requests.Count);
    Assert.True(await model.DeclineAsync(second, TestContext.Current.CancellationToken));
    Assert.True(model.ShowsEmpty);
    Assert.False(await model.AcceptAsync(first, TestContext.Current.CancellationToken));
    Assert.Equal(["accept", "decline"], service.Actions);
  }

  [Theory]
  [InlineData(null)]
  [InlineData(" partner ")]
  public async Task OwnerSendsTrimmedUsernameAndOptionalRelationshipThenRevokes(string? relationship)
  {
    var invitation = HouseholdInvitationsTestService.Invitation();
    var service = new HouseholdInvitationsTestService { Fetch = _ => Task.FromResult(HouseholdInvitationsTestService.Page(invitation)) };
    using var model = new HouseholdInvitationsViewModel(service);
    Assert.False(await model.SendAsync("invitee", null, TestContext.Current.CancellationToken));
    Assert.Throws<ArgumentException>(() => model.ConfigureSent(" "));
    model.ConfigureSent("home");
    Assert.True(model.IsSent);
    Assert.False(await model.SendAsync(" ", null, TestContext.Current.CancellationToken));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(await model.SendAsync(" invitee ", relationship, TestContext.Current.CancellationToken));
    Assert.Equal("invitee", service.Username);
    Assert.Equal(relationship?.Trim(), service.Relationship);
    Assert.Equal("home", service.Requests[0].Household);
    Assert.False(await model.AcceptAsync(invitation, TestContext.Current.CancellationToken));
    Assert.True(await model.RevokeAsync(invitation, TestContext.Current.CancellationToken));
    Assert.Equal("Household invitation revoked.", model.Message);
    Assert.Empty(model.Rows);
    Assert.False(await model.RevokeAsync(invitation, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task FailureAndCancellationPreserveInvitationAndPermitRetry()
  {
    var invitation = HouseholdInvitationsTestService.Invitation();
    var service = new HouseholdInvitationsTestService { Fetch = _ => throw new HttpRequestException("private server text") };
    using var model = new HouseholdInvitationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasError);
    Assert.DoesNotContain("private server text", model.LoadError, StringComparison.Ordinal);
    service.Fetch = _ => throw new OperationCanceledException();
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.False(model.IsBusy);
    service.Fetch = _ => Task.FromResult(HouseholdInvitationsTestService.Page(invitation));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.False(model.HasError);
    service.Failure = new HttpRequestException("private server text");
    Assert.False(await model.DeclineAsync(invitation, TestContext.Current.CancellationToken));
    Assert.Single(model.Rows);
    Assert.DoesNotContain("private server text", model.Message, StringComparison.Ordinal);
    service.Failure = new OperationCanceledException();
    Assert.False(await model.AcceptAsync(invitation, TestContext.Current.CancellationToken));
    Assert.False(model.IsBusy);
    Assert.Equal(string.Empty, model.Message);
    service.Failure = null;
    Assert.True(await model.DeclineAsync(invitation, TestContext.Current.CancellationToken));
    Assert.False(model.IsBusy);
    model.OnUiLocaleChanged();
  }

  [Fact]
  public async Task InflightLoadsAndMutationsRejectDuplicateActionsAndStaleRefreshResults()
  {
    var pending = new TaskCompletionSource<HouseholdInvitationsResponse>();
    var service = new HouseholdInvitationsTestService { Fetch = _ => pending.Task };
    using var model = new HouseholdInvitationsViewModel(service);
    var loading = model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.IsBusy);
    Assert.False(model.CanMutate);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Single(service.Requests);
    Assert.False(await model.AcceptAsync(HouseholdInvitationsTestService.Invitation(), TestContext.Current.CancellationToken));
    service.Fetch = _ => Task.FromResult(HouseholdInvitationsTestService.Page());
    await model.LoadAsync(TestContext.Current.CancellationToken);
    pending.SetResult(HouseholdInvitationsTestService.Page(HouseholdInvitationsTestService.Invitation()));
    await loading;
    Assert.Empty(model.Rows);
    Assert.True(model.ShowsEmpty);
  }

  [Fact]
  public async Task DuplicateCreateSurfacesReturnedOlderInvitationWithoutLosingContinuation()
  {
    var service = new HouseholdInvitationsTestService
    {
      Fetch = cursor => Task.FromResult(cursor is null
          ? new HouseholdInvitationsResponse([HouseholdInvitationsTestService.Invitation("newer")], new("opaque", true, null))
          : HouseholdInvitationsTestService.Page(HouseholdInvitationsTestService.Invitation())),
    };
    using var model = new HouseholdInvitationsViewModel(service);
    model.ConfigureSent("home");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(await model.SendAsync("invitee", null, TestContext.Current.CancellationToken));
    Assert.Equal(new[] { "invite", "newer" }, model.Rows.Select(row => row.Invitation.Id));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("opaque", service.Requests[1].Cursor);
    Assert.Equal(2, model.Rows.Count);
  }

  [Fact]
  public async Task SendFailureKeepsOwnerStateAndNoReloadOccurs()
  {
    var service = new HouseholdInvitationsTestService { Failure = new InvalidOperationException("denied") };
    using var model = new HouseholdInvitationsViewModel(service);
    model.ConfigureSent("home");
    Assert.False(await model.SendAsync("invitee", " ", TestContext.Current.CancellationToken));
    Assert.Null(service.Relationship);
    Assert.Empty(service.Requests);
    Assert.NotEmpty(model.Message);
  }

  [Fact]
  public async Task OnlyPendingRowsRenderAndForeignHouseholdCannotBeRevoked()
  {
    Assert.Throws<ArgumentNullException>(() => new HouseholdInvitationsViewModel(null!));
    var invitation = HouseholdInvitationsTestService.Invitation();
    var service = new HouseholdInvitationsTestService
    {
      Fetch = _ => Task.FromResult(HouseholdInvitationsTestService.Page(invitation,
          invitation with { Id = "accepted", AcceptedAt = DateTimeOffset.UnixEpoch },
          invitation with { Id = "declined", DeclinedAt = DateTimeOffset.UnixEpoch },
          invitation with { Id = "revoked", RevokedAt = DateTimeOffset.UnixEpoch })),
    };
    using var model = new HouseholdInvitationsViewModel(service);
    Assert.Empty(model.Message);
    Assert.Empty(model.LoadError);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Single(model.Rows);
    Assert.False(await model.RevokeAsync(invitation, TestContext.Current.CancellationToken));
    model.ConfigureSent("other-household");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(await model.RevokeAsync(invitation, TestContext.Current.CancellationToken));
    Assert.Empty(service.Actions);
  }

  [Fact]
  public void RowUsesEmbeddedUsernamesWithSafeFallbackAndRelationship()
  {
    var invitation = HouseholdInvitationsTestService.Invitation(username: null, inviter: "admin");
    var sent = new HouseholdInvitationRow(invitation, true, UiLocalization.English);
    Assert.Equal("Invitation for A Voucha member", sent.LocalizedTitle);
    Assert.Equal("partner", sent.LocalizedRelationship);
    var received = new HouseholdInvitationRow(invitation, false, UiLocalization.English);
    Assert.True(received.IsReceived);
    Assert.Equal("Invited by @inviter to @ownername's household", received.LocalizedTitle);
    var owner = new HouseholdInvitationRow(HouseholdInvitationsTestService.Invitation(), false, UiLocalization.English);
    Assert.Equal("Invited by @ownername", owner.LocalizedTitle);
    Assert.Equal(string.Empty, (owner with { Invitation = owner.Invitation with { Relationship = null } }).LocalizedRelationship);
  }
}
