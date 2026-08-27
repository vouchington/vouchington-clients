using System.Collections;
using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Moderation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ReviewQueuePageTests
{
  [Fact]
  public async Task RendersPostContextAndClearanceActions()
  {
    var viewModel = new ReviewQueueViewModel(new QueueService(Response([
      Post(AdminReviewQueueClearanceStatus.Rejected, "rejected", "Rendered review title"),
      Post(AdminReviewQueueClearanceStatus.InReview, "in-review", "In-review title"),
    ])));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);
    var collection = Find<CollectionView>(page, "review-queue-items");
    var rejected = CreateRow(collection, viewModel.Items.Single(item => item.ClearanceStatus == AdminReviewQueueClearanceStatus.Rejected));
    var inReview = CreateRow(collection, viewModel.Items.Single(item => item.ClearanceStatus == AdminReviewQueueClearanceStatus.InReview));

    Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable>(collection.ItemsSource).Cast<ReviewQueueRow>().Count());
    Assert.Equal("Rendered review title", Find<Label>(rejected, "review-queue-row-title").Text);
    Assert.Equal("Rendered preview", Find<Label>(rejected, "review-queue-row-preview").Text);
    Assert.Equal("Author: author-1", Find<Label>(rejected, "review-queue-row-author").Text);
    Assert.Equal("Post type: discussion", Find<Label>(rejected, "review-queue-row-post-type").Text);
    Assert.Equal("Root thread: article · root-slug · root-1", Find<Label>(rejected, "review-queue-row-root").Text);
    Assert.True(Find<Button>(rejected, "review-queue-approve").IsEnabled);
    Assert.True(Find<Button>(rejected, "review-queue-reject").IsEnabled);
    Assert.True(Find<Button>(rejected, "review-queue-re-review").IsEnabled);
    Assert.True(Find<Button>(inReview, "review-queue-approve").IsEnabled);
    Assert.True(Find<Button>(inReview, "review-queue-reject").IsEnabled);
    Assert.False(Find<Button>(inReview, "review-queue-re-review").IsEnabled);
  }

  [Fact]
  public async Task RendersLoadingEmptyErrorAndDrainedPaginationControls()
  {
    var loadingService = new BlockingLoadService();
    var loadingViewModel = new ReviewQueueViewModel(loadingService);
    var loading = loadingViewModel.LoadAsync(TestContext.Current.CancellationToken);
    await loadingService.Started.Task;
    Assert.True(Find<ActivityIndicator>(CreatePage(loadingViewModel), "review-queue-loading").IsVisible);
    loadingService.Release();
    await loading;

    var emptyViewModel = await LoadedAsync(new QueueService(Response([])));
    var emptyPage = CreatePage(emptyViewModel);
    Assert.True(Find<Label>(emptyPage, "review-queue-empty").IsVisible);
    Assert.False(FooterButton(emptyPage).IsVisible);

    var errorViewModel = await LoadedAsync(new QueueService(new HttpRequestException("offline")));
    var errorPage = CreatePage(errorViewModel);
    Assert.True(Find<Border>(errorPage, "review-queue-error").IsVisible);
    Assert.Contains(Descendants<Label>(errorPage), label => label.Text == "Could not load the review queue. Try again.");

    var drainedViewModel = await LoadedAsync(new QueueService(Response([], new PageInfo("next", true, null))));
    var drainedPage = CreatePage(drainedViewModel);
    Assert.False(Find<Label>(drainedPage, "review-queue-empty").IsVisible);
    Assert.True(FooterButton(drainedPage).IsVisible);
    Assert.True(FooterButton(drainedPage).IsEnabled);
  }

  [Fact]
  public async Task RendersMutationLockedRowActions()
  {
    var service = new QueueService(
        Response([Post(AdminReviewQueueClearanceStatus.Rejected, "post-1", "Rendered review title")]),
        holdMutation: true);
    var viewModel = await LoadedAsync(service);
    var mutation = viewModel.PerformAsync(
        Assert.Single(viewModel.Items),
        PostClearanceAction.Rejected,
        TestContext.Current.CancellationToken);
    await service.MutationStarted.Task;
    var page = CreatePage(viewModel);
    var row = CreateRow(
        Find<CollectionView>(page, "review-queue-items"),
        Assert.Single(viewModel.Items));

    Assert.False(Find<Button>(row, "review-queue-approve").IsEnabled);
    Assert.False(Find<Button>(row, "review-queue-reject").IsEnabled);
    Assert.False(Find<Button>(row, "review-queue-re-review").IsEnabled);
    Assert.True(Find<ActivityIndicator>(row, "review-queue-row-mutating").IsVisible);

    service.ReleaseMutation();
    await mutation;
  }

  [Fact]
  public async Task RendersSensitiveMediaGateThenTheWholeRevealedGroup()
  {
    var post = Post(
        AdminReviewQueueClearanceStatus.Rejected,
        "post-1",
        "Sensitive review",
        new AdminReviewQueueMediaContext(true, [
          new AdminReviewQueueImage("image-1", 0, "First image"),
          new AdminReviewQueueImage("image-2", 1, "Second image"),
        ]));
    var exposure = new ExposureService();
    var viewModel = new ReviewQueueViewModel(
        new QueueService(Response([post])),
        exposure,
        new AppConfig(new Uri("https://api.test")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var collection = Find<CollectionView>(CreatePage(viewModel), "review-queue-items");

    var gated = CreateRow(collection, Assert.Single(viewModel.Items));
    Assert.True(Find<Button>(gated, "review-queue-reveal").IsVisible);
    Assert.True(Find<Button>(gated, "review-queue-reveal").IsEnabled);
    Assert.False(Find<VerticalStackLayout>(gated, "review-queue-media").IsVisible);

    await viewModel.RevealMediaAsync(
        Assert.Single(viewModel.Items),
        TestContext.Current.CancellationToken);

    var revealed = CreateRow(collection, Assert.Single(viewModel.Items));
    Assert.True(Find<VerticalStackLayout>(revealed, "review-queue-media").IsVisible);
    Assert.Equal(2, Assert.Single(viewModel.Items).Media.Count);
    Assert.Equal(["post-1"], exposure.RevealedPostIds);
    Assert.True(Find<Button>(revealed, "review-queue-approve").IsEnabled);
  }

  [Fact]
  public async Task RendersExposureRecoveryAfterAmbiguousSingleRowReveal()
  {
    var exposure = new ExposureService
    {
      RevealFailure = new HttpRequestException("response lost"),
    };
    var viewModel = await LoadedMediaAsync(exposure);

    await viewModel.RevealMediaAsync(
        Assert.Single(viewModel.Items),
        TestContext.Current.CancellationToken);

    var page = CreatePage(viewModel);
    var row = CreateRow(
        Find<CollectionView>(page, "review-queue-items"),
        Assert.Single(viewModel.Items));
    Assert.True(Find<VerticalStackLayout>(row, "review-queue-media").IsVisible);
    Assert.True(Find<Button>(row, "review-queue-check-exposure").IsVisible);
    Assert.True(Find<HorizontalStackLayout>(
        row,
        "review-queue-exposure-controls").IsVisible);
  }

  [Fact]
  public async Task RendersRevealProgressAfterOptimisticMediaDisplay()
  {
    var exposure = new ExposureService { HoldReveal = true };
    var viewModel = await LoadedMediaAsync(exposure);

    var reveal = viewModel.RevealMediaAsync(
        Assert.Single(viewModel.Items),
        TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;

    var page = CreatePage(viewModel);
    var row = CreateRow(
        Find<CollectionView>(page, "review-queue-items"),
        Assert.Single(viewModel.Items));
    Assert.True(Find<VerticalStackLayout>(row, "review-queue-media").IsVisible);
    Assert.True(Find<ActivityIndicator>(
        row,
        "review-queue-reveal-loading").IsVisible);
    Assert.True(Find<HorizontalStackLayout>(
        row,
        "review-queue-exposure-controls").IsVisible);

    exposure.ReleaseReveal();
    await reveal;
  }

  [Fact]
  public async Task RenderedPageReloadReplacesRows()
  {
    var service = new SequenceQueueService(
        Response([Post(AdminReviewQueueClearanceStatus.Rejected, "post-1", "Before refresh")]),
        Response([]));
    var viewModel = await LoadedAsync(service);
    var page = CreatePage(viewModel);
    var collection = Find<CollectionView>(page, "review-queue-items");
    Assert.Single(Assert.IsAssignableFrom<IEnumerable>(collection.ItemsSource).Cast<ReviewQueueRow>());

    await page.ReloadAsync();

    Assert.Empty(Assert.IsAssignableFrom<IEnumerable>(collection.ItemsSource).Cast<ReviewQueueRow>());
    Assert.True(Find<Label>(page, "review-queue-empty").IsVisible);
  }

  private static async Task<ReviewQueueViewModel> LoadedAsync(IModerationService service)
  {
    var viewModel = new ReviewQueueViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    return viewModel;
  }

  private static async Task<ReviewQueueViewModel> LoadedMediaAsync(
      ExposureService exposure)
  {
    var post = Post(
        AdminReviewQueueClearanceStatus.Rejected,
        "post-1",
        "Sensitive review",
        new AdminReviewQueueMediaContext(true, [
          new AdminReviewQueueImage("image-1", 0, "Sensitive image"),
        ]));
    var viewModel = new ReviewQueueViewModel(
        new QueueService(Response([post])),
        exposure,
        new AppConfig(new Uri("https://api.test")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    return viewModel;
  }

  private static ReviewQueuePage CreatePage(ReviewQueueViewModel viewModel)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    return new ReviewQueuePage(viewModel);
  }

  private static Element CreateRow(CollectionView collection, ReviewQueueRow item)
  {
    var row = Assert.IsAssignableFrom<Element>(collection.ItemTemplate.CreateContent());
    row.BindingContext = item;
    return row;
  }

  private static Button FooterButton(Element page)
  {
    var control = Assert.IsType<HybridPaginationControl>(Find<CollectionView>(page, "review-queue-items").Footer);
    control.BindingContext = page.BindingContext;
    return Assert.Single(Descendants<Button>(control));
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static AdminReviewQueueResponse Response(
      IReadOnlyList<AdminReviewQueuePost> posts,
      PageInfo? pageInfo = null) =>
      new(posts, pageInfo ?? new PageInfo(null, false, null));

  private static AdminReviewQueuePost Post(
      AdminReviewQueueClearanceStatus status,
      string id,
      string title,
      AdminReviewQueueMediaContext? media = null) => new(
      id,
      title,
      "rendered-review",
      "Rendered preview",
      "discussion",
      "author-1",
      DateTimeOffset.UnixEpoch,
      "root-1",
      "article",
      "root-slug",
      status,
      DateTimeOffset.UnixEpoch,
      true,
      0.92,
      EmptyJson(),
      false,
      EmptyJson(),
      media);

  private static JsonElement EmptyJson() => JsonDocument.Parse("{}").RootElement.Clone();

  private sealed class BlockingLoadService : ReviewQueueModerationServiceStub
  {
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() => release.TrySetResult();

    public override async Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      Started.TrySetResult();
      await release.Task.WaitAsync(cancellationToken);
      return Response([]);
    }
  }

  private sealed class QueueService : ReviewQueueModerationServiceStub
  {
    private readonly AdminReviewQueueResponse? response;
    private readonly Exception? failure;
    private readonly bool holdMutation;
    private readonly TaskCompletionSource mutationRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public QueueService(AdminReviewQueueResponse response, bool holdMutation = false)
    {
      this.response = response;
      this.holdMutation = holdMutation;
    }

    public QueueService(Exception failure) => this.failure = failure;
    public TaskCompletionSource MutationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void ReleaseMutation() => mutationRelease.TrySetResult();

    public override Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        failure is null
            ? Task.FromResult(response!)
            : Task.FromException<AdminReviewQueueResponse>(failure);

    public override async Task<ClearanceUpdateResponse> UpdatePostClearanceAsync(
        string postId,
        PostClearanceAction status,
        CancellationToken cancellationToken = default)
    {
      MutationStarted.TrySetResult();
      if (holdMutation) await mutationRelease.Task.WaitAsync(cancellationToken);
      return new ClearanceUpdateResponse(AdminReviewQueueClearanceStatus.Rejected);
    }
  }

  private sealed class SequenceQueueService(params AdminReviewQueueResponse[] responses)
      : ReviewQueueModerationServiceStub
  {
    private int index;

    public override Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(responses[index++]);
  }

  private sealed class ExposureService : IModerationExposureService
  {
    private readonly TaskCompletionSource revealRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<string> RevealedPostIds { get; } = [];
    public bool HoldReveal { get; init; }
    public Exception? RevealFailure { get; init; }
    public TaskCompletionSource RevealStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ModerationExposureResponse> FetchExposureAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModerationExposureResponse(new(1, 10, false, null)));

    public async Task<ModerationExposureResponse> RecordReviewQueueRevealAsync(
        string postId,
        CancellationToken cancellationToken = default)
    {
      RevealedPostIds.Add(postId);
      RevealStarted.TrySetResult();
      if (HoldReveal) await revealRelease.Task.WaitAsync(cancellationToken);
      if (RevealFailure is not null) throw RevealFailure;
      return new ModerationExposureResponse(new(2, 10, false, null));
    }

    public void ReleaseReveal() => revealRelease.TrySetResult();
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => new ImmediateDispatcherTimer();
  }

  private sealed class ImmediateDispatcherTimer : IDispatcherTimer
  {
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; }
    public bool IsRunning { get; private set; }
    public event EventHandler? Tick;
    public void Start() { IsRunning = true; Tick?.Invoke(this, EventArgs.Empty); if (!IsRepeating) IsRunning = false; }
    public void Stop() => IsRunning = false;
  }
}
