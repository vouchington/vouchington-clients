using Voucha.Client.Core.Api;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using System.Net;
using System.Text.Json.Nodes;
using System.Globalization;

namespace Voucha.Client.Core.Tests.Fediverse;

public sealed class FediverseInstanceViewModelTests
{
  [Fact]
  public async Task DirectoryLoadsDedicatedFixtureAndMapsTrustTiers()
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.fediverse.instances.default"));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new FediverseInstancesViewModel(client);

    await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/fediverse/instances?limit=25&sort=best", handler.PathAndQuery);
    Assert.Equal(2, viewModel.Items.Count);
    Assert.Equal(FediverseTrustTier.Trusted, viewModel.Items[0].TrustTier);
    Assert.Equal(FediverseTrustTier.Unrated, viewModel.Items[1].TrustTier);
    Assert.False(viewModel.Items[1].IsClassified);
    Assert.True(viewModel.CanLoadMore);
  }

  [Fact]
  public async Task DirectoryKeepsUnclassifiedTopicWhenInstanceSidecarIsOmitted()
  {
    var fixture = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.fediverse.instances.default"))!;
    const string topicId = "00000000-0000-7000-8000-00000000f001";
    Assert.True(fixture["fediverse_instances"]!.AsObject().Remove(topicId));
    var handler = new RecordingHandler(fixture.ToJsonString());
    var viewModel = MakeDirectory(handler);

    await viewModel.LoadAsync("social", TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Items, candidate => candidate.Id == topicId);
    Assert.False(row.IsClassified);
    Assert.Equal("Unclassified", row.LocalizedSoftwareLabel);
    Assert.Equal("/api/v1/fediverse/instances?limit=25&q=social&sort=best", handler.PathAndQuery);
  }

  [Theory]
  [InlineData(null, 0, 0, FediverseTrustTier.Unrated)]
  [InlineData(100d, 0, 0, FediverseTrustTier.Unrated)]
  [InlineData(3d, 5, 2, FediverseTrustTier.Trusted)]
  [InlineData(3d, 4, 1, FediverseTrustTier.Neutral)]
  [InlineData(2d, 5, 3, FediverseTrustTier.Neutral)]
  [InlineData(-3d, 1, 4, FediverseTrustTier.Distrusted)]
  [InlineData(-2d, 1, 3, FediverseTrustTier.Neutral)]
  public void TrustTierUsesCrossClientThresholds(
      double? score,
      int votesCountUp,
      int votesCountDown,
      FediverseTrustTier expected)
  {
    var election = score is null
        ? null
        : new HostnameElection(null, "host", score.Value, votesCountUp, votesCountDown);
    Assert.Equal(expected, FediverseTrust.FromElection(election));
  }

  [Fact]
  public async Task DetailLoadsDedicatedFixtureMetadata()
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.fediverse.instance.slug"));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new FediverseInstanceDetailViewModel(client);

    await viewModel.LoadAsync("social-example", TestContext.Current.CancellationToken);

    Assert.Equal("social.example", viewModel.Name);
    Assert.Equal("mastodon 4.4.0", viewModel.Software);
    Assert.Equal("activitypub", viewModel.Protocol);
    Assert.Equal("1,200", viewModel.LocalizedTotalUsers);
    Assert.Equal(FediverseTrustTier.Trusted, viewModel.TrustTier);
  }

  [Fact]
  public async Task DetailHydratesExistingTopicActionsFromGenericOwner()
  {
    var dedicated = ApiFixtureLoader.LoadResponse("native.fediverse.instance.slug");
    var topic = JsonNode.Parse(dedicated)!["topic"]!.ToJsonString();
    var actions = """
        {"topic":TOPIC,"topic_election":{"id":"vote-1","votes_score_net":9,"votes_count_up":12,"votes_count_down":3},"election_vote":{"choice":"like"},"bookmarks":{"00000000-0000-7000-8000-00000000f001":{"follow":true,"mute":true}}}
        """.Replace("TOPIC", topic, StringComparison.Ordinal);
    var handler = new RecordingHandler([
      new RecordedResponse(dedicated),
      new RecordedResponse(actions),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new FediverseInstanceDetailViewModel(client);

    await viewModel.LoadAsync("social-example", TestContext.Current.CancellationToken);

    Assert.True(viewModel.Actions.IsFollowingTopic);
    Assert.True(viewModel.Actions.IsMutedTopic);
    Assert.Equal(ElectionVoteChoice.Like, viewModel.Actions.CurrentVoteChoice);
    Assert.Equal("/api/v1/topics/00000000-0000-7000-8000-00000000f001", handler.PathAndQuery);
  }

  [Fact]
  public async Task DetailPublishesLocaleChangesAndDisposesBothSubscriptions()
  {
    var dedicated = ApiFixtureLoader.LoadResponse("native.fediverse.instance.slug");
    var topic = JsonNode.Parse(dedicated)!["topic"]!.ToJsonString();
    var handler = new RecordingHandler([
      new RecordedResponse(dedicated),
      new RecordedResponse($$"""{"topic":{{topic}}}"""),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var localeController = new RecordingLocaleController();
    var viewModel = new FediverseInstanceDetailViewModel(
        client,
        localization: new UiLocalization(localeController),
        localeController: localeController);
    var localeNotifications = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (string.IsNullOrEmpty(args.PropertyName)) localeNotifications++;
    };
    await viewModel.LoadAsync("social-example", TestContext.Current.CancellationToken);
    localeNotifications = 0;

    localeController.ApplySavedLocale("fr");

    Assert.Equal(1, localeNotifications);
    Assert.Equal(2, localeController.SubscriptionCount);
    viewModel.Dispose();
    Assert.Equal(2, localeController.DisposalCount);
    localeController.ApplySavedLocale("es");
    Assert.Equal(1, localeNotifications);
  }

  [Fact]
  public async Task DirectoryContinuationKeepsActiveQuerySortAndRowsOnFailure()
  {
    var first = ApiFixtureLoader.LoadResponse("native.fediverse.instances.default");
    var handler = new RecordingHandler([
      new RecordedResponse(first),
      new RecordedResponse("{\"error\":\"unavailable\"}", HttpStatusCode.ServiceUnavailable),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new FediverseInstancesViewModel(client);

    await viewModel.LoadAsync("  social  ", TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, viewModel.Items.Count);
    Assert.NotNull(viewModel.ErrorMessage);
    Assert.Equal("/api/v1/fediverse/instances?after=next&limit=25&q=social&sort=best", handler.PathAndQuery);
  }

  [Fact]
  public async Task DirectoryRepresentsInitialEmptyAndErrorStates()
  {
    var empty = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.fediverse.instances.default"))!;
    empty["results"] = new JsonArray();
    empty["page_info"]!["has_next_page"] = false;
    empty["page_info"]!["end_cursor"] = null;
    var emptyViewModel = MakeDirectory(new RecordingHandler(empty.ToJsonString()));
    await emptyViewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.Empty(emptyViewModel.Items);
    Assert.False(emptyViewModel.CanLoadMore);
    Assert.Null(emptyViewModel.ErrorMessage);

    var errorViewModel = MakeDirectory(new RecordingHandler(
        "{\"error\":\"unavailable\"}",
        HttpStatusCode.ServiceUnavailable));
    await errorViewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.Empty(errorViewModel.Items);
    Assert.NotNull(errorViewModel.ErrorMessage);
  }

  [Fact]
  public async Task DirectoryExposesLoadingAndCancelsCleanly()
  {
    var handler = new SupersededRequestHandler(
        ApiFixtureLoader.LoadResponse("native.fediverse.instances.default"));
    var viewModel = MakeDirectory(handler);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

    var load = viewModel.LoadAsync(cancellationToken: cancellation.Token);
    await handler.FirstRequestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsLoading);
    cancellation.Cancel();
    await load;

    Assert.False(viewModel.IsLoading);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task DirectoryAppendsPageTwoAndDeduplicatesExistingRows()
  {
    var first = ApiFixtureLoader.LoadResponse("native.fediverse.instances.default");
    var second = first
        .Replace("00000000-0000-7000-8000-00000000f001", "00000000-0000-7000-8000-00000000f009", StringComparison.Ordinal)
        .Replace("social.example", "new.example", StringComparison.Ordinal)
        .Replace("social-example", "new-example", StringComparison.Ordinal);
    var handler = new RecordingHandler([new RecordedResponse(first), new RecordedResponse(second)]);
    var viewModel = MakeDirectory(handler);

    await viewModel.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items.Count);
    Assert.Equal(3, viewModel.Items.Select(item => item.Id).Distinct().Count());
    Assert.Contains(viewModel.Items, item => item.Name == "new.example");
  }

  [Fact]
  public async Task NewDirectorySearchCancelsAndCannotApplyThePreviousRequest()
  {
    var response = ApiFixtureLoader.LoadResponse("native.fediverse.instances.default");
    var handler = new SupersededRequestHandler(response);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new FediverseInstancesViewModel(client);

    var superseded = viewModel.LoadAsync("old", TestContext.Current.CancellationToken);
    await handler.FirstRequestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    var current = viewModel.LoadAsync("new", TestContext.Current.CancellationToken);
    await Task.WhenAll(superseded, current);

    Assert.True(handler.FirstRequestCancelled);
    Assert.Equal(2, viewModel.Items.Count);
    Assert.Contains("q=new", handler.Paths[1], StringComparison.Ordinal);
    Assert.False(viewModel.IsLoading);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task FailedReplacementRetainsTheEntirePreviousTraversal()
  {
    var page = ApiFixtureLoader.LoadResponse("native.fediverse.instances.default");
    var handler = new RecordingHandler([
      new RecordedResponse(page),
      new RecordedResponse("{\"error\":\"unavailable\"}", HttpStatusCode.ServiceUnavailable),
      new RecordedResponse(page),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new FediverseInstancesViewModel(client);

    await viewModel.LoadAsync("stable", TestContext.Current.CancellationToken);
    await viewModel.LoadAsync("failed", TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, viewModel.Items.Count);
    Assert.Equal(
        "/api/v1/fediverse/instances?after=next&limit=25&q=stable&sort=best",
        handler.PathAndQuery);
  }

  private sealed class SupersededRequestHandler(string response) : HttpMessageHandler
  {
    private int requestCount;

    public TaskCompletionSource FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool FirstRequestCancelled { get; private set; }
    public List<string> Paths { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      if (Interlocked.Increment(ref requestCount) == 1)
      {
        FirstRequestStarted.SetResult();
        try
        {
          await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          FirstRequestCancelled = true;
          throw;
        }
      }

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(response),
        RequestMessage = request,
      };
    }
  }

  private static FediverseInstancesViewModel MakeDirectory(HttpMessageHandler handler) =>
      new(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

  private sealed class RecordingLocaleController : IUiLocaleController
  {
    private readonly List<IUiLocaleChangeListener> listeners = [];
    public event EventHandler? LocaleChanged;
    public string EffectiveLocale { get; private set; } = "en";
    public CultureInfo Culture => CultureInfo.GetCultureInfo(EffectiveLocale);
    public int SubscriptionCount { get; private set; }
    public int DisposalCount { get; private set; }

    public void ApplySavedLocale(string? locale)
    {
      EffectiveLocale = locale ?? "en";
      LocaleChanged?.Invoke(this, EventArgs.Empty);
      foreach (var listener in listeners.ToArray()) listener.OnUiLocaleChanged();
    }

    public IDisposable SubscribeLocaleChanges(IUiLocaleChangeListener listener)
    {
      listeners.Add(listener);
      SubscriptionCount++;
      return new RecordingSubscription(() =>
      {
        listeners.Remove(listener);
        DisposalCount++;
      });
    }

    private sealed class RecordingSubscription(Action dispose) : IDisposable
    {
      public void Dispose() => dispose();
    }
  }
}
