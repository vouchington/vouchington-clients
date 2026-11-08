using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityApplicationSummaryTests
{
  [Fact]
  public void ListFixtureDecodesSummaryAndApplicantWithoutAnswers()
  {
    var json = ApiFixtureLoader.LoadResponse("web.communities.applications.default");
    var response = JsonSerializer.Deserialize<CommunityApplicationsResponse>(json, VouchaApiJson.Options)!;
    var summary = response.CommunityApplicationSummaries[response.Results[0].Id!];
    Assert.Equal("pending", summary.Status);
    Assert.Equal(0, summary.AnswerCount);
    Assert.Equal("I would like to join.", summary.MessagePreview);
    var row = CommunityApplicationRow.FromApplication(summary, response.Users, UiLocalization.English);
    Assert.Equal("testuser", row.Applicant);
    Assert.Equal(summary.CreatedAt, row.CreatedAt);
    Assert.DoesNotContain("\"answers\"", json, StringComparison.Ordinal);
    Assert.Equal("Deleted user", CommunityApplicationRow.FromApplication(summary, new Dictionary<string, PublicUser>(), UiLocalization.English).Applicant);
    Assert.Equal("Display", CommunityApplicationRow.FromApplication(summary,
        new Dictionary<string, PublicUser> { [summary.UserId] = new(summary.UserId, "username", "Display") }, UiLocalization.English).Applicant);
  }

  [Theory]
  [InlineData("pending", "en attente")]
  [InlineData("approved", "approuvé")]
  [InlineData("rejected", "rejeté")]
  public void RowShowsLocalizedReviewStatus(string status, string expected)
  {
    var response = JsonSerializer.Deserialize<CommunityApplicationsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.applications.default"), VouchaApiJson.Options)!;
    var summary = response.CommunityApplicationSummaries.Values.Single() with { Status = status };
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("fr"));
    var row = CommunityApplicationRow.FromApplication(summary, response.Users, new UiLocalization(controller));

    Assert.Equal(expected, row.StatusLabel);
  }

  [Fact]
  public async Task DetailServiceReadsTheSelectedApplicationRoute()
  {
    using var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse("web.communities.application.default"));
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiCommunitiesService(new VouchaApiClient(http));
    using var vm = new CommunityApplicationDetailViewModel(service, "a community", "application-1");

    await vm.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/communities/a%20community/applications/application-1", handler.PathAndQuery);
    Assert.Equal(LoadState.Loaded, vm.State);
    Assert.Equal("I would like to join.", vm.Application!.Message);
  }

  [Fact]
  public async Task CanceledReadReturnsToIdleWithoutPublishingTheResponse()
  {
    var completion = new TaskCompletionSource<CommunityApplicationResponse>();
    var service = new ScriptedCommunitiesService { ApplicationDetail = completion.Task };
    using var canceled = new CancellationTokenSource();
    using var vm = new CommunityApplicationDetailViewModel(service, "community", "application-1");
    var loading = vm.LoadAsync(canceled.Token);
    canceled.Cancel();
    completion.SetResult(JsonSerializer.Deserialize<CommunityApplicationResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.application.default"), VouchaApiJson.Options)!);

    await loading;

    Assert.Equal(LoadState.Idle, vm.State);
    Assert.Null(vm.Application);
    Assert.Null(vm.Error);
  }

  [Fact]
  public async Task DetailLoadsFullMessageAndAnswersThenClearsThemOnFailure()
  {
    var completion = new TaskCompletionSource<CommunityApplicationResponse>();
    var service = new ScriptedCommunitiesService { ApplicationDetail = completion.Task };
    using var vm = new CommunityApplicationDetailViewModel(service, "a community", "application-1");
    var loading = vm.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Loading, vm.State);
    Assert.Null(vm.Application);
    var detail = JsonSerializer.Deserialize<CommunityApplicationResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.application.default"), VouchaApiJson.Options)!;
    detail = detail with
    {
      CommunityApplication = detail.CommunityApplication with
      {
        Message = new string('x', 350),
        Answers = new Dictionary<string, object> { ["question"] = "Full answer" }
      }
    };
    completion.SetResult(detail);
    await loading;
    Assert.Equal(LoadState.Loaded, vm.State);
    Assert.Equal(350, vm.Application!.Message!.Length);
    Assert.Equal("Full answer", vm.Application.Answers["question"]);
    Assert.Equal(("a community", "application-1"), service.ApplicationDetailRequest);
    service.ApplicationDetail = Task.FromException<CommunityApplicationResponse>(new HttpRequestException("unavailable"));
    await vm.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, vm.State);
    Assert.Equal("unavailable", vm.Error?.VerbatimValue);
    Assert.Null(vm.Application);
  }

  [Fact]
  public async Task MalformedDetailShowsErrorInsteadOfEscapingLoad()
  {
    var service = new ScriptedCommunitiesService
    {
      ApplicationDetail = Task.FromException<CommunityApplicationResponse>(new JsonException("invalid detail"))
    };
    using var vm = new CommunityApplicationDetailViewModel(service, "community", "application-1");

    await vm.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, vm.State);
    Assert.Equal("invalid detail", vm.Error?.VerbatimValue);
    Assert.Null(vm.Application);
  }

  [Fact]
  public void DetailRouteEscapesCommunityAndApplicationIds()
  {
    var request = VouchaApiEndpoints.CommunityApplication("a community", "app/id");
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/communities/a%20community/applications/app%2Fid", request.Path);
  }
}

internal sealed partial class ScriptedCommunitiesService
{
  public Task<CommunityApplicationResponse> ApplicationDetail { get; set; } = null!;
  public (string Community, string Application) ApplicationDetailRequest { get; private set; }
  public Task<CommunityApplicationResponse> FetchApplicationAsync(string idOrSlug, string applicationId, CancellationToken cancellationToken = default)
  {
    ApplicationDetailRequest = (idOrSlug, applicationId);
    return ApplicationDetail;
  }
}
