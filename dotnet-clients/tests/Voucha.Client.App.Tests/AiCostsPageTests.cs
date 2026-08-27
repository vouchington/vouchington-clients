using System.Collections;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class AiCostsPageTests
{
  [Fact]
  public async Task RendersExactPopulatedMetricsAndPagination()
  {
    var service = new PageService(new([
      new("community-1", "alpha-community", 4_294_967_296, 9_876_543_210, 7_654_321_098, 42,
          new("1234567890123456789012345", "usd")),
    ], new("opaque-cursor", true, null)));
    var viewModel = new AiCostsViewModel(service, UiLocalization.English);
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);
    var collection = Find<CollectionView>(page, "ai-costs-list");
    var row = Assert.IsAssignableFrom<Element>(collection.ItemTemplate.CreateContent());
    row.BindingContext = Assert.Single(viewModel.Rows);

    Assert.Equal("USD\u00A01,234,567,890,123,456,789.012345", Find<Label>(row, "ai-cost-row-cost").Text);
    Assert.Equal("42 unpriced requests", Find<Label>(row, "ai-cost-row-unpriced").Text);
    Assert.True(Find<Label>(row, "ai-cost-row-unpriced").IsVisible);
    Assert.Equal(1, Grid.GetRow((BindableObject)Find<Label>(row, "ai-cost-row-unpriced").Parent!));
    Assert.True(Find<HybridPaginationControl>(Assert.IsAssignableFrom<Element>(collection.Footer), "pagination-ai-costs").HasMore);
  }

  [Fact]
  public async Task RendersEmptyAndRetainedRefreshErrorStates()
  {
    var service = new PageService(new([], new(null, false, null)));
    var viewModel = new AiCostsViewModel(service, UiLocalization.English);
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);
    var collection = Find<CollectionView>(page, "ai-costs-list");
    var header = Assert.IsAssignableFrom<Element>(collection.Header);

    Assert.True(Find<Label>(header, "ai-costs-empty").IsVisible);
    service.Response = new([
      new("community-1", "alpha-community", 1, 2, 3, 0, new("1000000", "usd")),
    ], new(null, false, null));
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    service.Failure = new HttpRequestException("offline");
    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.Rows);
    Assert.True(Find<VerticalStackLayout>(header, "ai-costs-error").IsVisible);
  }

  private static AiCostsPage CreatePage(AiCostsViewModel viewModel)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    foreach (var key in UiMessageKey.All)
      application.Resources[key.Value] = UiLocalization.English.Localize(key);
    return new AiCostsPage(viewModel);
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

  private sealed class PageService(AiCostTotalsResponse response) : IEngineeringService
  {
    public AiCostTotalsResponse Response { get; set; } = response;
    public Exception? Failure { get; set; }
    public Task<AiCostTotalsResponse> FetchAiCostsAsync(string? after = null, CancellationToken cancellationToken = default) =>
        Failure is null ? Task.FromResult(Response) : Task.FromException<AiCostTotalsResponse>(Failure);
    public Task<QueueStatsSummaryResponse> FetchQueueStatsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<QueueStatsResponse> FetchQueuesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> PauseQueueAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> ResumeQueueAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ScheduledJobsResponse> FetchScheduledJobsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EngineeringSuccessResponse> TriggerScheduledJobAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<BackfillsResponse> FetchBackfillsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
