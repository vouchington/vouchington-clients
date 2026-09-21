using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class IntegrityPenaltyViewModelTests
{
  private static readonly NavigationViewer Admin = new(true, ["administrator"]);

  [Fact]
  public async Task ReportLedgerForwardsCursorDeduplicatesAndPreservesRowsOnRetry()
  {
    var continuationFails = true;
    var requests = new List<(IntegrityPenaltyStatus Status, string? After)>();
    var service = new ModerationIntegrityTestService
    {
      FetchReportPenalties = (status, after, _) =>
      {
        requests.Add((status, after));
        if (after is null) return Task.FromResult(ReportPage([ReportPenalty("p1")], "opaque", true));
        return continuationFails
            ? Task.FromException<ReportIntegrityPenaltiesResponse>(new HttpRequestException("more failed"))
            : Task.FromResult(ReportPage([ReportPenalty("p1"), ReportPenalty("p2")]));
      },
    };
    var viewModel = new ReportIntegrityPenaltyViewModel(service, Admin);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("p1", viewModel.Items.Single().Id);
    Assert.Equal("more failed", viewModel.ErrorMessage);
    continuationFails = false;
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["p1", "p2"], viewModel.Items.Select(item => item.Id));
    Assert.Equal("opaque", requests[1].After);
    await viewModel.SelectStatusAsync(IntegrityPenaltyStatus.All, TestContext.Current.CancellationToken);
    Assert.Equal(IntegrityPenaltyStatus.All, requests.Last().Status);
  }

  [Fact]
  public async Task ConfirmedRevocationRemovesActiveAndReplacesAllRow()
  {
    var active = ReportPenalty("p1");
    var revoked = active with { RevokedAt = DateTimeOffset.Parse("2026-06-02T12:00:00Z"), RevokedById = "admin" };
    var service = new ModerationIntegrityTestService
    {
      FetchReportPenalties = (_, _, _) => Task.FromResult(ReportPage([active])),
      RevokeReportPenalty = (_, _) => Task.FromResult(new ReportAbusePenaltyResponse(revoked)),
    };
    var viewModel = new ReportIntegrityPenaltyViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.RevokeAsync("p1", TestContext.Current.CancellationToken);
    Assert.Empty(viewModel.Items);

    await viewModel.SelectStatusAsync(IntegrityPenaltyStatus.All, TestContext.Current.CancellationToken);
    await viewModel.RevokeAsync("p1", TestContext.Current.CancellationToken);
    Assert.Equal("admin", viewModel.Items.Single().RevokedById);
  }

  [Fact]
  public async Task AmbiguousRevocationReloadsExactRowBeforeUnlocking()
  {
    var active = VotePenalty("p1");
    var revoked = active with { RevokedAt = DateTimeOffset.UtcNow, RevokedById = "admin" };
    var reloads = 0;
    var service = new ModerationIntegrityTestService
    {
      FetchVotePenalties = (_, _, _, _) => Task.FromResult(VotePage([active])),
      RevokeVotePenalty = (_, _) => Task.FromException<VoteWeightPenaltyResponse>(
          new VouchaApiException("uncertain", null, HttpStatusCode.InternalServerError)),
      FetchVotePenalty = (_, _) =>
      {
        reloads++;
        return Task.FromResult(new VoteWeightPenaltyResponse(revoked));
      },
    };
    var viewModel = new VoteIntegrityPenaltyViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.RevokeAsync("p1", TestContext.Current.CancellationToken);

    Assert.Equal(1, reloads);
    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.IsActionInFlight("p1"));
    Assert.False(viewModel.NeedsReconciliation("p1"));
  }

  [Fact]
  public async Task DecodeFailureRetainsRevokeLockAndPreventsDuplicateDelete()
  {
    var revokes = 0;
    var active = VotePenalty("p1");
    var service = new ModerationIntegrityTestService
    {
      FetchVotePenalties = (_, _, _, _) => Task.FromResult(VotePage([active])),
      RevokeVotePenalty = (_, _) =>
      {
        revokes++;
        return Task.FromException<VoteWeightPenaltyResponse>(
            new System.Text.Json.JsonException("invalid success body"));
      },
      FetchVotePenalty = (_, _) => Task.FromException<VoteWeightPenaltyResponse>(
          new HttpRequestException("reload failed")),
    };
    var viewModel = new VoteIntegrityPenaltyViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.RevokeAsync("p1", TestContext.Current.CancellationToken);
    await viewModel.RevokeAsync("p1", TestContext.Current.CancellationToken);

    Assert.Equal(1, revokes);
    Assert.True(viewModel.NeedsReconciliation("p1"));
    Assert.False(viewModel.CanRevoke("p1"));
  }

  [Theory]
  [InlineData(false, null)]
  [InlineData(true, "moderator")]
  [InlineData(true, "member")]
  public async Task NonAdministratorsCannotLoadOrRevoke(bool authenticated, string? role)
  {
    var roles = role is null ? Array.Empty<string>() : new[] { role };
    var service = new ModerationIntegrityTestService();
    var viewModel = new ReportIntegrityPenaltyViewModel(
        service, new NavigationViewer(authenticated, roles));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.RevokeAsync("p1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAuthorized);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task LocaleChangeRefreshesAuthorizationAndReconciliationCopy()
  {
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var unauthorized = new ReportIntegrityPenaltyViewModel(
        new ModerationIntegrityTestService(), new NavigationViewer(false, []),
        localization, controller);
    await unauthorized.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Contains("administrators", unauthorized.ErrorMessage, StringComparison.Ordinal);

    var voteService = new ModerationIntegrityTestService
    {
      FetchVotePenalties = (_, _, _, _) => Task.FromResult(VotePage([VotePenalty("p1")])),
      RevokeVotePenalty = (_, _) => Task.FromException<VoteWeightPenaltyResponse>(
          new HttpRequestException("uncertain")),
      FetchVotePenalty = (_, _) => Task.FromException<VoteWeightPenaltyResponse>(
          new HttpRequestException("reload failed")),
    };
    using var vote = new VoteIntegrityPenaltyViewModel(
        voteService, Admin, localization, controller);
    await vote.LoadAsync(TestContext.Current.CancellationToken);
    await vote.RevokeAsync("p1", TestContext.Current.CancellationToken);
    Assert.Equal("The result could not be reloaded. Try again.", vote.ActionError("p1"));

    controller.ApplySavedLocale("fr");

    Assert.Contains("administrateurs", unauthorized.ErrorMessage, StringComparison.Ordinal);
    Assert.Equal("Impossible de recharger le résultat. Réessayez.", vote.ActionError("p1"));
  }

  [Fact]
  public async Task ReviewAndRevokeCapabilitiesAreIndependent()
  {
    var service = new ModerationIntegrityTestService
    {
      FetchReportPenalties = (_, _, _) => Task.FromResult(ReportPage([ReportPenalty("p1")])),
    };
    var capabilities = new IntegrityCapabilities(true, true, true, true, false);
    var viewModel = new ReportIntegrityPenaltyViewModel(
        service, Admin, capabilities: capabilities);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsAuthorized);
    Assert.False(viewModel.CanRevoke("p1"));

    var noReview = new ReportIntegrityPenaltyViewModel(
        service, Admin, capabilities: capabilities with
        {
          CanReviewPenalties = false,
          CanRevokePenalties = true,
        });
    await noReview.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(noReview.IsAuthorized);
    Assert.Equal(LoadState.Error, noReview.State);
  }

  [Theory]
  [InlineData(typeof(OperationCanceledException), true)]
  [InlineData(typeof(VouchaApiDecodeException), true)]
  [InlineData(typeof(System.Text.Json.JsonException), true)]
  [InlineData(typeof(InvalidOperationException), false)]
  public void MutationClassifierDoesNotSwallowProgrammingErrors(Type type, bool ambiguous)
  {
    var exception = (Exception)Activator.CreateInstance(type)!;
    Assert.Equal(ambiguous, IntegrityMutationFailure.IsAmbiguous(exception));
  }

  [Theory]
  [InlineData(null, true)]
  [InlineData(408, true)]
  [InlineData(500, true)]
  [InlineData(400, false)]
  [InlineData(409, false)]
  public void MutationClassifierOnlyTreatsTimeoutAndServerHttpFailuresAsAmbiguous(
      int? statusCode,
      bool ambiguous)
  {
    var exception = statusCode is null
        ? new HttpRequestException("mutation failed")
        : new HttpRequestException("mutation failed", null, (HttpStatusCode)statusCode);

    Assert.Equal(ambiguous, IntegrityMutationFailure.IsAmbiguous(exception));
  }

  private static ReportAbusePenalty ReportPenalty(string id) => new(
      id, "user-1", "mass_report_campaign", "flag-1", "admin-1", null, null,
      DateTimeOffset.Parse("2026-06-01T12:00:00Z"), DateTimeOffset.Parse("2026-06-01T12:00:00Z"));

  private static VoteWeightPenalty VotePenalty(string id) => new(
      id, "user-1", 0.2, "voting_ring", "flag-1", "admin-1", null, null,
      DateTimeOffset.Parse("2026-06-01T12:00:00Z"));

  private static ReportIntegrityPenaltiesResponse ReportPage(
      IReadOnlyList<ReportAbusePenalty> rows, string? cursor = null, bool more = false) =>
      new(rows, new PageInfo(cursor, more, rows.FirstOrDefault()?.Id));

  private static VoteIntegrityPenaltiesResponse VotePage(
      IReadOnlyList<VoteWeightPenalty> rows, string? cursor = null, bool more = false) =>
      new(rows, new PageInfo(cursor, more, rows.FirstOrDefault()?.Id), new("flag", null));

  private sealed class StubLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
