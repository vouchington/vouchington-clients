using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityActionViewModelTests
{
  [Fact]
  public async Task SubmitAsyncCreatesCommunityWithBlankSlugWhenUnset()
  {
    var (viewModel, handler) = CreateViewModel(
        CommunityActionKind.Create,
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.archive.default")));

    viewModel.Name = "Test Community Name";
    viewModel.Slug = "";
    viewModel.Markdown = "About";
    viewModel.TurnstileToken = "turnstile-token";

    Assert.True(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.False(viewModel.HasError);
    Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    Assert.Equal("/api/v1/communities", handler.Requests[0].PathAndQuery);
    Assert.Contains("\"name\":\"Test Community Name\"", handler.Requests[0].Body!, StringComparison.Ordinal);
    Assert.Contains("\"markdown\":\"About\"", handler.Requests[0].Body!, StringComparison.Ordinal);
    Assert.Contains("\"cf_turnstile_response\":\"turnstile-token\"", handler.Requests[0].Body!, StringComparison.Ordinal);
    Assert.DoesNotContain("\"slug\"", handler.Requests[0].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SubmitAsyncBlocksCreateWithoutCaptchaUnlessBypassed()
  {
    var (viewModel, handler) = CreateViewModel(CommunityActionKind.Create, new RecordedResponse("{}"));
    viewModel.Name = "Test Community Name";

    Assert.False(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));

    Assert.Equal("Captcha token is required.", viewModel.ErrorMessage);
    Assert.Empty(handler.Requests);

    var (bypassViewModel, bypassHandler) = CreateViewModel(
        CommunityActionKind.Create,
        new AppConfig(new Uri("https://api.test"), "site-key", AllowCommunityCreateCaptchaBypass: true),
        new RecordedResponse("{}"));
    bypassViewModel.Name = "Test Community Name";

    Assert.True(await bypassViewModel.SubmitAsync(TestContext.Current.CancellationToken));
    Assert.DoesNotContain("cf_turnstile_response", bypassHandler.Requests[0].Body!, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("Two Words", "", "Community name must have at least 3 words.")]
  [InlineData("Test Community Name", "Bad_Slug", "Slug must use lowercase letters, numbers, and hyphens.")]
  public async Task SubmitAsyncValidatesCreateInputs(string name, string slug, string expectedError)
  {
    var (viewModel, handler) = CreateViewModel(CommunityActionKind.Create, new RecordedResponse("{}"));
    viewModel.Name = name;
    viewModel.Slug = slug;
    viewModel.TurnstileToken = "token";

    Assert.False(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));

    Assert.Equal(expectedError, viewModel.ErrorMessage);
    Assert.Empty(handler.Requests);
  }

  [Fact]
  public async Task SubmitAsyncAppliesCommunityApplicationMessage()
  {
    var (viewModel, handler) = CreateViewModel(
        CommunityActionKind.Apply,
        new RecordedResponse("""
            {
              "questions": [
                {
                  "id": "question-1",
                  "community_id": "community-1",
                  "question": "Why do you want to join?",
                  "field_type": "text",
                  "options": null,
                  "order_index": 0,
                  "required": true,
                  "created_at": "2026-07-01T00:00:00Z"
                }
              ]
            }
            """),
        new RecordedResponse("{}"));

    viewModel.CommunitySlug = "test-community";
    viewModel.Message = "Join me";
    await viewModel.LoadApplicationQuestionsAsync(TestContext.Current.CancellationToken);
    viewModel.SetApplicationAnswer("question-1", "I can help");

    Assert.True(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Single(viewModel.ApplicationQuestions);
    Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    Assert.Equal("/api/v1/communities/test-community/application-questions", handler.Requests[0].PathAndQuery);
    Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
    Assert.Equal("/api/v1/communities/test-community/applications", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"answers\":{\"question-1\":\"I can help\"}", handler.Requests[1].Body!, StringComparison.Ordinal);
    Assert.Contains("\"message\":\"Join me\"", handler.Requests[1].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SubmitAsyncSendsInviteEmailAndUsername()
  {
    var (viewModel, handler) = CreateViewModel(CommunityActionKind.Invite, new RecordedResponse("{}"));

    viewModel.CommunitySlug = "test-community";
    viewModel.Email = "person@example.com";
    viewModel.Username = "tester";

    Assert.True(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));

    Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    Assert.Equal("/api/v1/communities/test-community/invites", handler.Requests[0].PathAndQuery);
    Assert.Contains("\"email\":\"person@example.com\"", handler.Requests[0].Body!, StringComparison.Ordinal);
    Assert.Contains("\"username\":\"tester\"", handler.Requests[0].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SubmitAsyncRedeemsInviteCode()
  {
    var (viewModel, handler) = CreateViewModel(CommunityActionKind.RedeemInvite, new RecordedResponse("{}"));

    viewModel.Code = "invite-code";

    Assert.True(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));

    Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    Assert.Equal("/api/v1/communities/invite-redemptions", handler.Requests[0].PathAndQuery);
    Assert.Contains("\"code\":\"invite-code\"", handler.Requests[0].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SubmitAsyncReturnsIdleWhenCanceled()
  {
    var (viewModel, _) = CreateViewModel(CommunityActionKind.Create, new RecordedResponse("{}"));
    var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();
    viewModel.Name = "Test Community Name";
    viewModel.TurnstileToken = "token";

    Assert.False(await viewModel.SubmitAsync(cancellationTokenSource.Token));

    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task SubmitAsyncCapturesApiFailures()
  {
    var (viewModel, handler) = CreateViewModel(
        CommunityActionKind.Create,
        new RecordedResponse("""{"error":"nope"}""", HttpStatusCode.BadRequest));

    viewModel.Name = "Test Community Name";
    viewModel.TurnstileToken = "token";

    Assert.False(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.NotNull(viewModel.ErrorMessage);
    Assert.Empty(viewModel.TurnstileToken);
    Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    Assert.Equal("/api/v1/communities", handler.Requests[0].PathAndQuery);
  }

  [Fact]
  public async Task SubmitAsyncRaisesHasErrorChangeWhenApiFails()
  {
    var (viewModel, _) = CreateViewModel(
        CommunityActionKind.Create,
        new RecordedResponse("""{"error":"nope"}""", HttpStatusCode.BadRequest));
    var changed = new List<string?>();
    viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

    await viewModel.SubmitAsync(TestContext.Current.CancellationToken);

    Assert.Contains(nameof(CommunityActionViewModel.HasError), changed);
  }

  [Fact]
  public void ReportTurnstileChallengeFailureSetsVisibleErrorAndClearsToken()
  {
    var (viewModel, _) = CreateViewModel(CommunityActionKind.Create);
    viewModel.TurnstileToken = "token";

    viewModel.ReportTurnstileChallengeFailure("Missing VOUCHA_TURNSTILE_SITE_KEY");

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("Missing VOUCHA_TURNSTILE_SITE_KEY", viewModel.ErrorMessage);
    Assert.Empty(viewModel.TurnstileToken);
  }

  private static (CommunityActionViewModel ViewModel, RecordingHandler Handler) CreateViewModel(
      CommunityActionKind kind,
      params RecordedResponse[] responses)
  {
    return CreateViewModel(kind, null, responses);
  }

  private static (CommunityActionViewModel ViewModel, RecordingHandler Handler) CreateViewModel(
      CommunityActionKind kind,
      AppConfig? appConfig,
      params RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    var service = new ApiCommunitiesService(new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    }));
    appConfig ??= new AppConfig(new Uri("https://api.test"), "site-key");
    return (new CommunityActionViewModel(service, kind, appConfig), handler);
  }
}
