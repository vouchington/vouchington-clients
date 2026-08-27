using System.Globalization;
using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Growth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Growth;

public sealed class GrowthDashboardViewModelTests
{
  [Fact]
  public async Task LoadAsyncBuildsRowsFromSharedFixture()
  {
    var service = new RecordingGrowthMetricsService { Response = LoadFixture() };
    var viewModel = new GrowthDashboardViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal(GrowthMetricsRange.ThirtyDays, service.LastRange);
    Assert.Equal("30d", viewModel.Metrics?.Range);
    Assert.Contains(viewModel.Rows, row => row is { Group: "Users", Label: "Total users", Value: "1,200" });
    Assert.Contains(viewModel.Rows, row => row is { Group: "Engagement", Label: "Votes", Value: "2,100" });
    Assert.Contains(viewModel.Rows, row => row is { Group: "Infrastructure", Label: "Queue throughput", Value: "14,200" });
  }

  [Fact]
  public async Task SelectRangeAsyncLoadsSelectedRange()
  {
    var service = new RecordingGrowthMetricsService { Response = LoadFixture() };
    var viewModel = new GrowthDashboardViewModel(service);

    await viewModel.SelectRangeAsync(GrowthMetricsRange.SevenDays, TestContext.Current.CancellationToken);

    Assert.Equal(GrowthMetricsRange.SevenDays, viewModel.SelectedRange);
    Assert.Equal(GrowthMetricsRange.SevenDays, service.LastRange);
  }

  [Fact]
  public async Task ApplyInitialRangeLoadsDeepLinkedRange()
  {
    var service = new RecordingGrowthMetricsService { Response = LoadFixture() };
    var viewModel = new GrowthDashboardViewModel(service);

    viewModel.ApplyInitialRange(GrowthMetricsRange.NinetyDays);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(GrowthMetricsRange.NinetyDays, viewModel.SelectedRange);
    Assert.Equal(GrowthMetricsRange.NinetyDays, service.LastRange);
  }

  [Fact]
  public async Task SelectRangeAsyncDoesNotLetStaleResponseOverwriteCurrentMetrics()
  {
    var fixture = LoadFixture();
    var first = new TaskCompletionSource<GrowthMetricsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var second = new TaskCompletionSource<GrowthMetricsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingGrowthMetricsService();
    service.Enqueue(first);
    service.Enqueue(second);
    var viewModel = new GrowthDashboardViewModel(service);

    var firstLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await service.WaitForCallCountAsync(1, TestContext.Current.CancellationToken);
    var secondLoad = viewModel.SelectRangeAsync(GrowthMetricsRange.SevenDays, TestContext.Current.CancellationToken);
    await service.WaitForCallCountAsync(2, TestContext.Current.CancellationToken);
    second.SetResult(fixture with { Range = "7d", UserGrowth = fixture.UserGrowth with { TotalUsers = 700 } });
    first.SetResult(fixture);
    await Task.WhenAll(firstLoad, secondLoad);

    Assert.Equal(GrowthMetricsRange.SevenDays, viewModel.SelectedRange);
    Assert.Equal("7d", viewModel.Metrics?.Range);
    Assert.Equal(700, viewModel.Metrics?.UserGrowth.TotalUsers);
  }

  [Fact]
  public async Task LoadAsyncFormatsMissingInfrastructureMetricsAsUnavailable()
  {
    var fixture = LoadFixture();
    var service = new RecordingGrowthMetricsService
    {
      Response = fixture with
      {
        Infrastructure = fixture.Infrastructure with
        {
          CrawlerSuccessRate = null,
          QueueThroughput = null,
          CacheHitRate = null,
          AiTokenUsage = null,
        },
      },
    };
    var viewModel = new GrowthDashboardViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.Rows, row => row is { Group: "Infrastructure", Label: "Crawler success", Value: "Unavailable" });
    Assert.Contains(viewModel.Rows, row => row is { Group: "Infrastructure", Label: "Queue throughput", Value: "Unavailable" });
  }

  [Fact]
  public async Task LoadAsyncFormatsLargeInfrastructureCounters()
  {
    var fixture = LoadFixture();
    var service = new RecordingGrowthMetricsService
    {
      Response = fixture with
      {
        Infrastructure = fixture.Infrastructure with
        {
          QueueThroughput = 3_000_000_000L,
          AiTokenUsage = 4_000_000_000L,
        },
      },
    };
    var viewModel = new GrowthDashboardViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.Rows, row => row is { Group: "Infrastructure", Label: "Queue throughput", Value: "3,000,000,000" });
    Assert.Contains(viewModel.Rows, row => row is { Group: "Infrastructure", Label: "AI token usage", Value: "4,000,000,000" });
  }

  [Fact]
  public async Task LoadAsyncFormatsMrrAsUsd()
  {
    var previousCulture = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
    try
    {
      var service = new RecordingGrowthMetricsService { Response = LoadFixture() };
      var viewModel = new GrowthDashboardViewModel(service);

      await viewModel.LoadAsync(TestContext.Current.CancellationToken);

      Assert.Contains(
          viewModel.Rows,
          row => row is { Group: "Revenue", Label: "MRR", Value: "USD\u00A02,850" });
    }
    finally
    {
      CultureInfo.CurrentCulture = previousCulture;
    }
  }

  [Fact]
  public async Task LoadAsyncPreservesScaledMrrPrecision()
  {
    var fixture = LoadFixture();
    var service = new RecordingGrowthMetricsService
    {
      Response = fixture with
      {
        Revenue = fixture.Revenue with
        {
          MrrByCurrency = [new ScaledMoneyAggregate("180143985094819820000", "usd")],
        },
      },
    };
    var viewModel = new GrowthDashboardViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains(
        viewModel.Rows,
        row => row is
        {
          Group: "Revenue",
          Label: "MRR",
          Value: "USD\u00A0180,143,985,094,819.82",
        });
  }

  [Fact]
  public async Task LoadAsyncFormatsEmptyMrrAsZero()
  {
    var fixture = LoadFixture();
    var service = new RecordingGrowthMetricsService
    {
      Response = fixture with
      {
        Revenue = fixture.Revenue with { MrrByCurrency = [] },
      },
    };
    var viewModel = new GrowthDashboardViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains(
        viewModel.Rows,
        row => row is { Group: "Revenue", Label: "MRR", Value: "0" });
  }

  [Fact]
  public async Task LocaleChangeRebuildsVisibleLocalizedRows()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    var localization = new UiLocalization(controller);
    using var viewModel = new GrowthDashboardViewModel(
        new RecordingGrowthMetricsService { Response = LoadFixture() },
        localization,
        controller);
    var rowsChanged = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(GrowthDashboardViewModel.Rows)) rowsChanged++;
    };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    controller.ApplySavedLocale("es");

    Assert.Contains(viewModel.Rows, row => row is { Group: "Usuarios", Label: "Usuarios totales" });
    Assert.True(rowsChanged >= 2);
  }

  [Fact]
  public async Task LoadAsyncSurfacesTransportError()
  {
    var service = new RecordingGrowthMetricsService { ThrowOnFetch = true };
    var viewModel = new GrowthDashboardViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Contains("offline", viewModel.ErrorMessage, StringComparison.Ordinal);
  }

  [Fact]
  public void ReportUnexpectedErrorSurfacesErrorState()
  {
    var service = new RecordingGrowthMetricsService();
    var viewModel = new GrowthDashboardViewModel(service);

    viewModel.ReportUnexpectedError(new InvalidOperationException("binding failed"));

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("binding failed", viewModel.ErrorMessage);
  }

  private static GrowthMetricsResponse LoadFixture() =>
      JsonSerializer.Deserialize<GrowthMetricsResponse>(
          ApiFixtureLoader.LoadResponse("web.growth-metrics.default"),
          VouchaApiJson.Options) ?? throw new InvalidOperationException("Fixture did not decode.");

  private sealed class RecordingGrowthMetricsService : IGrowthMetricsService
  {
    private readonly Queue<TaskCompletionSource<GrowthMetricsResponse>> queuedResponses = [];
    private int callCount;

    public GrowthMetricsResponse Response { get; init; } = LoadFixture();
    public GrowthMetricsRange LastRange { get; private set; }
    public bool ThrowOnFetch { get; init; }
    public int CallCount => callCount;

    public void Enqueue(TaskCompletionSource<GrowthMetricsResponse> response) =>
        queuedResponses.Enqueue(response);

    public async Task WaitForCallCountAsync(int expected, CancellationToken cancellationToken)
    {
      while (CallCount < expected)
      {
        await Task.Delay(10, cancellationToken).ConfigureAwait(true);
      }
    }

    public Task<GrowthMetricsResponse> FetchGrowthMetricsAsync(
        GrowthMetricsRange range,
        CancellationToken cancellationToken = default)
    {
      LastRange = range;
      callCount++;
      if (ThrowOnFetch)
      {
        throw new HttpRequestException("offline", null, HttpStatusCode.ServiceUnavailable);
      }

      if (queuedResponses.Count > 0)
      {
        return queuedResponses.Dequeue().Task;
      }

      return Task.FromResult(Response);
    }
  }

  private sealed class EnglishDeviceLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }
}
