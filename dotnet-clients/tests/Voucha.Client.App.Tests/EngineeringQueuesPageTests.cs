using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class EngineeringQueuesPageTests
{
  [Fact]
  public async Task RendersDelayedBacklogBesideWaitingForTotalsAndQueue()
  {
    var queue = new QueueStats("priority", 0, 2, 3, 4, false, 7);
    var viewModel = new EngineeringQueuesViewModel(new QueueService(queue));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var controller = new UiLocaleController(new EnglishLanguages());
    using var version = new UiLocaleVersion(controller);
    var page = CreatePage(viewModel, controller, version);

    Assert.Equal("Delayed: 9", Find<Label>(page, "queue-total-delayed").Text);
    var collection = Find<CollectionView>(page, "engineering-queues-list");
    var row = Assert.IsAssignableFrom<Element>(collection.ItemTemplate.CreateContent());
    row.BindingContext = queue;
    Assert.Equal("Delayed: 7", Find<Label>(row, "queue-row-delayed").Text);

    controller.ApplySavedLocale("es");
    Assert.Equal("Retrasados: 9", Find<Label>(page, "queue-total-delayed").Text);
    Assert.Equal("Retrasados: 7", Find<Label>(row, "queue-row-delayed").Text);
  }

  [Fact]
  public async Task RendersZeroDelayedCountRatherThanHidingIt()
  {
    var queue = new QueueStats("ordinary", 3, 0, 0, 0, false, 0);
    var viewModel = new EngineeringQueuesViewModel(new QueueService(queue, totalDelayed: 0));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var controller = new UiLocaleController(new EnglishLanguages());
    using var version = new UiLocaleVersion(controller);
    var page = CreatePage(viewModel, controller, version);

    Assert.Equal("Delayed: 0", Find<Label>(page, "queue-total-delayed").Text);
    var collection = Find<CollectionView>(page, "engineering-queues-list");
    var row = Assert.IsAssignableFrom<Element>(collection.ItemTemplate.CreateContent());
    row.BindingContext = queue;
    Assert.Equal("Delayed: 0", Find<Label>(row, "queue-row-delayed").Text);
  }

  private static EngineeringQueuesPage CreatePage(
      EngineeringQueuesViewModel viewModel,
      UiLocaleController controller,
      UiLocaleVersion version)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var localization = new UiLocalization(controller);
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Eyebrow"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
        ["UiLocaleVersion"] = version,
        ["UiLocalizedValue"] = new UiLocalizedValueConverter(localization),
      },
    };
    return new EngineeringQueuesPage(viewModel);
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

  private sealed class QueueService(QueueStats queue, int totalDelayed = 9) : IEngineeringService
  {
    public Task<QueueStatsSummaryResponse> FetchQueueStatsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new QueueStatsSummaryResponse(new(0, 2, 3, 4, 1, totalDelayed)));
    public Task<QueueStatsResponse> FetchQueuesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new QueueStatsResponse([queue], 1));
    public Task<ScheduledJobsResponse> FetchScheduledJobsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ScheduledJobsResponse([]));
    public Task<BackfillsResponse> FetchBackfillsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new BackfillsResponse([]));
    public Task<AiCostTotalsResponse> FetchAiCostsAsync(string? after = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> PauseQueueAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> ResumeQueueAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> TriggerBackfillAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PsqlMigrationsResponse> FetchMigrationsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PartitionStatusResponse> FetchPartitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> EnqueuePsqlJobAsync(string type, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ArticleSyncTriggerResponse> TriggerArticleSyncAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ArticleSyncJobStatusResponse> FetchArticleSyncStatusAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CacheGroupsResponse> FetchCacheGroupsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<RebuildBloomFilterResponse> RebuildBloomFilterAsync(string filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ClearCacheResponse> ClearCacheAsync(string group, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FlushValkeyResponse> FlushValkeyAsync(string concern, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  { public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance; }

  private sealed class EnglishLanguages : IDeviceLanguageProvider
  { public IReadOnlyList<string> PreferredLanguages => ["en"]; }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
