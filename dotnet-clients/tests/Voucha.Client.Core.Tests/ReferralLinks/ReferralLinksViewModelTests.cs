using Voucha.Client.Core.Api;
using Voucha.Client.Core.ReferralLinks;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.ReferralLinks;

public sealed partial class ReferralLinksViewModelTests
{
  [Fact]
  public async Task LoadFeedAsyncUsesFollowingOrMutualFeedTypes()
  {
    var service = new RecordingReferralLinksService
    {
      FeedResponse = new ReferralLinkFeedResponse(
          [new ReferralLinkFeedItem(
              "link-1",
              "user-1",
              "program-1",
              "Program",
              "program",
              "https://example.com/ref",
              "Referral")],
          new Dictionary<string, ReferralLinkFeedUser>(StringComparer.Ordinal),
          new PageInfo(null, false, null)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadFeedAsync(mutual: true, TestContext.Current.CancellationToken);

    Assert.Equal("mutual_follows", service.LastFeedRequest?.FeedType);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Collection(viewModel.Items, item =>
    {
      Assert.Equal("link-1", item.Id);
      Assert.Equal("Referral", item.Title);
      Assert.Equal(new Uri("https://example.com/ref"), item.Url);
    });
  }

  [Fact]
  public async Task LoadPrioritizedAsyncBuildsPriorityRows()
  {
    var service = new RecordingReferralLinksService
    {
      PrioritizedResponse = new PrioritizedReferralLinksResponse(
          [new PrioritizedReferralLink(
              "link-2",
              "user-2",
              false,
              "program-1",
              "https://example.com/p2",
              null,
              2,
              1,
              1,
              5,
              null,
              null,
              null)],
          new Dictionary<string, ReferralLinkFeedUser>(StringComparer.Ordinal)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadPrioritizedAsync("program-1", all: true, TestContext.Current.CancellationToken);

    Assert.Equal("program-1", service.LastPrioritizedRequest?.ReferralProgramId);
    Assert.True(service.LastPrioritizedRequest?.All);
    Assert.Collection(viewModel.Items, item => Assert.Equal("Priority group 2", item.Detail));
  }

  [Fact]
  public async Task LoadMineAsyncBuildsRowsAndCancellationReturnsIdle()
  {
    var service = new RecordingReferralLinksService
    {
      MineResponse = new ReferralLinksResponse(
          [new ReferralLink(
              "link-3",
              "user-1",
              "program-1",
              "url-1",
              "https://example.com/mine",
              "Mine",
              DateTimeOffset.UtcNow,
              null,
              DateTimeOffset.UtcNow,
              DateTimeOffset.UtcNow,
              "Program",
              "program")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadMineAsync(TestContext.Current.CancellationToken);
    Assert.Collection(viewModel.Items, item =>
    {
      Assert.Equal("Program", item.Detail);
      Assert.True(item.CanManage);
      Assert.True(item.IsActive);
      Assert.Equal("program-1", item.ReferralProgramId);
      Assert.Equal("program", item.ReferralProgramSlug);
    });

    service.CancelMine = true;
    await viewModel.LoadMineAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Idle, viewModel.State);
  }

  [Fact]
  public async Task LoadTrendingProgramsAsyncUsesPublicReferralPrograms()
  {
    var service = new RecordingReferralLinksService
    {
      TrendingResponse = new TrendingReferralProgramsResponse(
          [new TrendingReferralProgram("program-1", LinkCount: 4)],
          new PageInfo(null, false, null)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadTrendingProgramsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Collection(viewModel.Items, item =>
    {
      Assert.Equal("program-1", item.Id);
      Assert.Equal("program-1", item.Title);
      Assert.Null(item.Url);
      Assert.Equal("4 active links", item.Detail);
    });
  }

  [Fact]
  public async Task LoadAnalyticsAsyncBuildsReferralClickRows()
  {
    var service = new RecordingReferralLinksService
    {
      ClickResponse = new ReferralClickLogResponse(
          [new ReferralClickLogResult("click-1")],
          new Dictionary<string, ReferralClickLogEntry>(StringComparer.Ordinal)
          {
            ["click-1"] = new(
                "click-1",
                "https://example.com/referrals",
                DateTimeOffset.UtcNow,
                "user-2",
                DateTimeOffset.UtcNow),
          },
          new Dictionary<string, User>(StringComparer.Ordinal)
          {
            ["user-2"] = new("user-2", "newmember", null),
          },
          new PageInfo(null, false, null)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadAnalyticsAsync(TestContext.Current.CancellationToken);

    Assert.Collection(viewModel.Items, item =>
    {
      Assert.True(item.IsAnalytics);
      Assert.Equal("Signup: @newmember", item.LocalizedSignupStatus);
      Assert.Equal(new Uri("https://example.com/referrals"), item.Url);
    });
  }

  [Fact]
  public async Task LoadMoreAnalyticsAsyncUsesStoredCursorAndAppendsRows()
  {
    var service = new RecordingReferralLinksService
    {
      ClickResponses =
      [
        ClickResponse("click-1", "https://example.com/one", new PageInfo("cursor-1", true, null)),
        ClickResponse("click-2", "https://example.com/two", new PageInfo(null, false, null)),
      ],
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadAnalyticsAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMoreAnalytics);

    await viewModel.LoadMoreAnalyticsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("cursor-1", service.LastClickRequest?.After);
    Assert.False(viewModel.HasMoreAnalytics);
    Assert.Equal(["click-1", "click-2"], viewModel.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task LoadAnalyticsAsyncHandlesCancellationAndErrors()
  {
    var service = new RecordingReferralLinksService
    {
      CancelClicks = true,
    };
    var viewModel = new ReferralLinksViewModel(service);

    await viewModel.LoadAnalyticsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Idle, viewModel.State);

    service.CancelClicks = false;
    service.ThrowClicks = true;
    await viewModel.LoadAnalyticsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task SearchReferralProgramsAsyncUsesReferralProgramFilter()
  {
    var service = new RecordingReferralLinksService
    {
      TopicSearchResponse = new TopicSearchResponse(
          [new EntityReference(
              EntityType: "topic",
              EntityId: null,
              Id: "program-1",
              Name: "Cards",
              Slug: "cards",
              TopicType: "referral_program",
              ReadAt: null,
              DeliveryType: null,
              SharedByUserId: null,
              SharedAt: null,
              StoryId: null)],
          new PageInfo(null, false, null),
          new Dictionary<string, Topic>(StringComparer.Ordinal)
          {
            ["program-1"] = new("program-1", "Cards", "cards", "referral_program"),
          },
          new Dictionary<string, object>(StringComparer.Ordinal)),
    };
    var viewModel = new ReferralLinksViewModel(service);

    var choices = await viewModel.SearchReferralProgramsAsync("cards", TestContext.Current.CancellationToken);

    Assert.Equal("cards", service.LastTopicSearchRequest?.Query);
    Assert.Equal("referral_program", service.LastTopicSearchRequest?.TopicTypes);
    Assert.Equal(10, service.LastTopicSearchRequest?.Limit);
    Assert.Collection(choices, choice =>
    {
      Assert.Equal("program-1", choice.Id);
      Assert.Equal("Cards", choice.Name);
      Assert.Equal("cards", choice.Slug);
    });
  }

  private sealed class RecordingReferralLinksService : IReferralLinksService
  {
    public FetchReferralLinksFeedRequest? LastFeedRequest { get; private set; }

    public FetchPrioritizedReferralLinksRequest? LastPrioritizedRequest { get; private set; }

    public SearchTopicsRequest? LastTopicSearchRequest { get; private set; }

    public FetchReferralClickLogsRequest? LastClickRequest { get; private set; }

    public FetchReferralLinksRequest? LastMineRequest { get; private set; }

    public string? LastValidationInfoId { get; private set; }

    public ReferralLinkFeedResponse? FeedResponse { get; init; }

    public PrioritizedReferralLinksResponse? PrioritizedResponse { get; init; }

    public ReferralLinksResponse? MineResponse { get; init; }

    public TaskCompletionSource<ReferralLinksResponse>? DeferredMineResponse { get; init; }

    public IReadOnlyList<ReferralLinksResponse>? MineResponses { get; init; }

    public ReferralClickLogResponse? ClickResponse { get; init; }

    public TaskCompletionSource<ReferralClickLogResponse>? DeferredClickResponse { get; init; }

    public IReadOnlyList<ReferralClickLogResponse>? ClickResponses { get; init; }

    public TrendingReferralProgramsResponse? TrendingResponse { get; init; }

    public TopicSearchResponse? TopicSearchResponse { get; init; }

    public ReferralProgramValidationInfoResponse? ValidationInfoResponse { get; init; }

    public CreateReferralLinkBody? CreatedBody { get; private set; }

    public string? LastUpdatedId { get; private set; }

    public UpdateReferralLinkBody? UpdatedBody { get; private set; }

    public string? LastDeletedId { get; private set; }

    public string? LastActivatedId { get; private set; }

    public string? LastDeactivatedId { get; private set; }

    public bool CancelMine { get; set; }

    public bool CancelClicks { get; set; }

    public bool ThrowClicks { get; set; }

    public bool CancelMutation { get; set; }

    public bool ThrowMutation { get; set; }

    public Exception? MutationException { get; set; }

    public Task<ReferralLinkFeedResponse> FetchFeedAsync(
        FetchReferralLinksFeedRequest request,
        CancellationToken cancellationToken = default)
    {
      LastFeedRequest = request;
      return Task.FromResult(FeedResponse ?? throw new InvalidOperationException("Missing response."));
    }

    public Task<ReferralLinksResponse> FetchMineAsync(
        FetchReferralLinksRequest request,
        CancellationToken cancellationToken = default)
    {
      LastMineRequest = request;
      if (DeferredMineResponse is not null)
      {
        return DeferredMineResponse.Task;
      }

      return CancelMine
          ? Task.FromCanceled<ReferralLinksResponse>(new CancellationToken(canceled: true))
          : Task.FromResult(NextMineResponse());
    }

    public Task<ReferralClickLogResponse> FetchClicksAsync(
        FetchReferralClickLogsRequest request,
        CancellationToken cancellationToken = default)
    {
      LastClickRequest = request;
      if (DeferredClickResponse is not null)
      {
        return DeferredClickResponse.Task;
      }

      return CancelClicks
            ? Task.FromCanceled<ReferralClickLogResponse>(new CancellationToken(canceled: true))
            : ThrowClicks
                ? Task.FromException<ReferralClickLogResponse>(new InvalidOperationException("offline"))
                : Task.FromResult(NextClickResponse());
    }

    public Task<TopicSearchResponse> SearchReferralProgramsAsync(
        SearchTopicsRequest request,
        CancellationToken cancellationToken = default)
    {
      LastTopicSearchRequest = request;
      return Task.FromResult(TopicSearchResponse ?? throw new InvalidOperationException("Missing response."));
    }

    public Task<ReferralProgramValidationInfoResponse> FetchValidationInfoAsync(
        string topicIdOrSlug,
        CancellationToken cancellationToken = default)
    {
      LastValidationInfoId = topicIdOrSlug;
      return Task.FromResult(ValidationInfoResponse ?? throw new InvalidOperationException("Missing response."));
    }

    public Task<PrioritizedReferralLinksResponse> FetchPrioritizedAsync(
        FetchPrioritizedReferralLinksRequest request,
        CancellationToken cancellationToken = default)
    {
      LastPrioritizedRequest = request;
      return Task.FromResult(PrioritizedResponse ?? throw new InvalidOperationException("Missing response."));
    }

    public Task<TrendingReferralProgramsResponse> FetchTrendingProgramsAsync(
        FetchTrendingReferralProgramsRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(TrendingResponse ?? throw new InvalidOperationException("Missing response."));

    public Task CreateAsync(CreateReferralLinkBody body, CancellationToken cancellationToken = default) =>
        RunMutation(() => CreatedBody = body);

    public Task UpdateAsync(
        string referralLinkId,
        UpdateReferralLinkBody body,
        CancellationToken cancellationToken = default) =>
        RunMutation(() =>
        {
          LastUpdatedId = referralLinkId;
          UpdatedBody = body;
        });

    public Task DeleteAsync(string referralLinkId, CancellationToken cancellationToken = default) =>
        RunMutation(() => LastDeletedId = referralLinkId);

    public Task ActivateAsync(string referralLinkId, CancellationToken cancellationToken = default) =>
        RunMutation(() => LastActivatedId = referralLinkId);

    public Task DeactivateAsync(string referralLinkId, CancellationToken cancellationToken = default)
    {
      return RunMutation(() => LastDeactivatedId = referralLinkId);
    }

    private Task RunMutation(Action record)
    {
      if (CancelMutation)
      {
        return Task.FromCanceled(new CancellationToken(canceled: true));
      }

      if (ThrowMutation)
      {
        return Task.FromException(new InvalidOperationException("offline"));
      }

      if (MutationException is not null)
      {
        return Task.FromException(MutationException);
      }

      record();
      return Task.CompletedTask;
    }

    private int clickResponseIndex;

    private int mineResponseIndex;

    private ReferralLinksResponse NextMineResponse()
    {
      if (MineResponses is { Count: > 0 })
      {
        return MineResponses[Math.Min(mineResponseIndex++, MineResponses.Count - 1)];
      }

      return MineResponse ?? throw new InvalidOperationException("Missing response.");
    }

    private ReferralClickLogResponse NextClickResponse()
    {
      if (ClickResponses is { Count: > 0 })
      {
        return ClickResponses[Math.Min(clickResponseIndex++, ClickResponses.Count - 1)];
      }

      return ClickResponse ?? throw new InvalidOperationException("Missing response.");
    }
  }

  private static ReferralClickLogResponse ClickResponse(
      string id,
      string landingUrl,
      PageInfo pageInfo) =>
      new(
          [new ReferralClickLogResult(id)],
          new Dictionary<string, ReferralClickLogEntry>(StringComparer.Ordinal)
          {
            [id] = new(id, landingUrl, null, null, DateTimeOffset.UtcNow),
          },
          new Dictionary<string, User>(StringComparer.Ordinal),
          pageInfo);

  private static ReferralLinksResponse MineResponse(
      string id,
      string url,
      PageInfo pageInfo) =>
      new(
          [new ReferralLink(
              id,
              "user-1",
              "program-1",
              $"{id}-url",
              url,
              id,
              DateTimeOffset.UtcNow,
              null,
              DateTimeOffset.UtcNow,
              DateTimeOffset.UtcNow,
              "Program",
              "program")],
          pageInfo);
}
