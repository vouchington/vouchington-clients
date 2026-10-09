using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Copyright;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Copyright;

public sealed partial class CopyrightNoticesViewModelTests
{
  [Fact]
  public async Task SignedOutReadsNeverCallTheCaseService()
  {
    var service = new CaseService();
    var model = new CopyrightNoticesViewModel(service, NavigationViewer.Anonymous);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadCaseAsync("case", TestContext.Current.CancellationToken);
    Assert.Empty(service.Calls);
    Assert.Empty(model.Notices);
    Assert.Null(model.SelectedCase);
  }

  [Theory]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.NotFound)]
  public async Task NonParticipantReadsPublicDetailWithoutPrivateParticipantState(HttpStatusCode status)
  {
    var service = new CaseService
    {
      Participant = (_, _) => Task.FromException<CopyrightParticipantNoticeResponse>(new HttpRequestException("denied", null, status)),
    };
    var model = Model(service);
    await model.LoadCaseAsync("case", TestContext.Current.CancellationToken);
    Assert.Equal(["participant:case", "detail:case"], service.Calls);
    Assert.NotNull(model.SelectedCase);
    Assert.Null(model.SelectedCase.Participant);
    Assert.NotNull(model.SelectedCase.Notice.Claimant);
    Assert.False(model.HasError);
  }

  [Fact]
  public async Task ParticipantServerFailureDoesNotMasqueradeAsPublicAccess()
  {
    var service = new CaseService
    {
      Participant = (_, _) => Task.FromException<CopyrightParticipantNoticeResponse>(
          new HttpRequestException("unavailable", null, HttpStatusCode.InternalServerError)),
    };
    var model = Model(service);
    await model.LoadCaseAsync("case", TestContext.Current.CancellationToken);
    Assert.Equal(["participant:case"], service.Calls);
    Assert.True(model.HasError);
    Assert.Null(model.SelectedCase);
  }

  [Fact]
  public async Task AuthorizedNonEuCaseUsesPublicIdentityAndParticipantTimeline()
  {
    var service = new CaseService();
    var model = Model(service);
    await model.LoadCaseAsync("case", TestContext.Current.CancellationToken);
    Assert.Equal(["participant:case", "detail:case"], service.Calls);
    Assert.NotNull(model.SelectedCase);
    Assert.NotNull(model.SelectedCase.Participant);
    Assert.Equal(2, model.SelectedCase.Timeline.Count);
    Assert.Single(model.SelectedCase.Participant.Statements);
  }

  [Fact]
  public async Task EuCaseUsesAuthorizedParticipantWithoutFetchingPublicDetail()
  {
    var service = new CaseService
    {
      Participant = (_, _) => Task.FromResult(Fixture<CopyrightParticipantNoticeResponse>(
          "web.copyright.eu.participant.no-action-complaint")),
    };
    var model = Model(service);
    await model.LoadCaseAsync("eu", TestContext.Current.CancellationToken);
    Assert.Equal(["participant:eu"], service.Calls);
    Assert.NotNull(model.SelectedCase);
    Assert.NotNull(model.SelectedCase.Participant?.Eu);
    Assert.Null(model.SelectedCase.AcceptedAt);
    Assert.Null(model.SelectedCase.Notice.Claimant);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CancelledHeldParticipantResponseDoesNotPopulateTheCase(bool failsLate)
  {
    var response = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.notice.participant.populated");
    var held = new TaskCompletionSource<CopyrightParticipantNoticeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new CaseService { Participant = (_, _) => held.Task };
    var model = Model(service);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var pending = model.LoadCaseAsync("case", cancellation.Token);
    try
    {
      Assert.True(model.IsLoading);
      cancellation.Cancel();
      if (failsLate) held.TrySetException(new HttpRequestException("late offline response"));
      else held.TrySetResult(response);
      await pending;
      Assert.Null(model.SelectedCase);
      Assert.False(model.HasError);
      Assert.Equal(["participant:case"], service.Calls);
    }
    finally
    {
      held.TrySetResult(response);
      await pending;
    }
  }
}
