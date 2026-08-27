using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Moderation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ModerationPageTransparencyRangeTests
{
  [Fact]
  public async Task SelectedRangeDoesNotCancelTheInFlightLoad()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var service = new SuspendedTransparencyService();
    var page = new ModerationPage(new ModerationViewModel(service));
    var load = page.ApplyContextAsync(TransparencyContext());
    await service.FirstRequestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    Find<Button>(page, "moderation-transparency-range-30d").SendClicked();
    service.ReleaseFirstResponse();
    await load.WaitAsync(TestContext.Current.CancellationToken);

    var viewModel = Assert.IsType<ModerationViewModel>(page.BindingContext);
    Assert.False(service.FirstRequestCanceled.Task.IsCompleted);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal(1, service.RequestCount);
    Assert.Equal("transparency-2026-08-01-reports-spam", viewModel.Items.Single().Id);
  }

  [Fact]
  public void ChangedRangeStartsItsReplacementLoad()
  {
    Assert.True(ModerationTransparencyRangeSelection.ShouldStartLoad(
        ModerationTransparencyRange.Default,
        ModerationTransparencyRange.SevenDays));
  }

  [Fact]
  public async Task DeepLinkRangeLoadsTheResolvedPeriod()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://moderation-transparency?range=all",
        new NavigationViewer(true, []));
    Assert.True(ModerationRoutes.TryResolve(resolution.Match, out var context));
    var service = new RecordingTransparencyService();
    var page = new ModerationPage(new ModerationViewModel(service));

    await page.ApplyContextAsync(context);

    Assert.Equal([ModerationTransparencyRange.All], service.RequestedRanges);
  }

  [Fact]
  public void AppShellPreservesQueryForNewAndExistingModerationPages()
  {
    var source = AppSource("AppShell.ModerationRoutes.cs");
    const string structuralResolution = "ModerationRoutes.TryResolve(match, out var context)";

    Assert.Equal(2, source.Split(structuralResolution, StringSplitOptions.None).Length - 1);
    Assert.DoesNotContain("ModerationRoutes.TryResolve(match.Path", source, StringComparison.Ordinal);
  }

  private static ModerationRouteContext TransparencyContext() => new(
      "/moderation-transparency",
      Voucha.Client.Core.Localization.UiMessageKey.NativeSwiftCommunityRowsModerationTransparency,
      ModerationRouteKind.Transparency);

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(root.GetVisualTreeDescendants().OfType<T>(), item => item.AutomationId == automationId);

  private static string AppSource(string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src")))
    {
      root = root.Parent;
    }
    return File.ReadAllText(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src",
        "Voucha.Client.App",
        file));
  }

  private sealed class RecordingTransparencyService : ReviewQueueModerationServiceStub
  {
    public List<string> RequestedRanges { get; } = [];

    public override Task<ModerationTransparencyResponse> FetchTransparencyAsync(
        string range = ModerationTransparencyRange.Default,
        string? after = null,
        CancellationToken cancellationToken = default)
    {
      RequestedRanges.Add(range);
      return Task.FromResult(new ModerationTransparencyResponse(range, []));
    }
  }

  private sealed class SuspendedTransparencyService : ReviewQueueModerationServiceStub
  {
    private readonly TaskCompletionSource<ModerationTransparencyResponse> firstResponse = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    public int RequestCount { get; private set; }
    public TaskCompletionSource FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource FirstRequestCanceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async Task<ModerationTransparencyResponse> FetchTransparencyAsync(
        string range = ModerationTransparencyRange.Default,
        string? after = null,
        CancellationToken cancellationToken = default)
    {
      RequestCount++;
      FirstRequestStarted.SetResult();
      using var registration = cancellationToken.Register(FirstRequestCanceled.SetResult);
      return await firstResponse.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void ReleaseFirstResponse() => firstResponse.SetResult(Response(
        ModerationTransparencyRange.Default,
        "2026-08-01",
        "spam"));

    private static ModerationTransparencyResponse Response(string range, string date, string category) => new(
        range,
        [new ModerationTransparencyBucket(date, "reports", category, 1)]);
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
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
