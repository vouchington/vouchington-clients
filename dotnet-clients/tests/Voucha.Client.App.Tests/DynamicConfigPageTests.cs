using System.Collections;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class DynamicConfigPageTests
{
  [Fact]
  public async Task RendersSearchSelectionTypedControlsAndReadOnlyState()
  {
    var service = new PageService { Selected = TypedNamespace(canUpdate: false) };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.SearchText = "typed";
    var page = CreatePage(viewModel);

    var search = Find<SearchBar>(page, "dynamic-config-namespace-search");
    var namespaces = Find<CollectionView>(page, "dynamic-config-namespaces");
    Assert.Equal("typed", search.Text);
    Assert.Equal(
        "typed-config",
        Assert.Single(
            Assert.IsAssignableFrom<IEnumerable>(namespaces.ItemsSource)
                .Cast<DynamicConfigNamespaceOption>()).ProtocolNamespace);
    Assert.Contains(
        Descendants<Label>(page),
        label => label.Text == UiLocalization.English.Localize(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsReadOnly));
    Assert.False(Find<Switch>(page, "dynamic-config-enabled-global").IsEnabled);
    Assert.Equal(Keyboard.Numeric, Find<Entry>(page, "dynamic-config-threshold-value").Keyboard);
    Assert.False(Find<Button>(page, "dynamic-config-threshold-save").IsEnabled);
    Assert.Equal(
        UiLocalization.English.Format(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNumberRange,
            ("kind", UiLocalization.English.Localize(
                UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNumber)),
            ("minimum", 0),
            ("maximum", 1)),
        Find<Label>(page, "dynamic-config-threshold-constraint").Text);
    Assert.Equal(
        UiLocalization.English.Format(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNumberRange,
            ("kind", UiLocalization.English.Localize(
                UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsWholeNumber)),
            ("minimum", 1),
            ("maximum", 100)),
        Find<Label>(page, "dynamic-config-limit-constraint").Text);
    Assert.DoesNotContain(Descendants<Picker>(page), picker => picker.AutomationId?.EndsWith("-override", StringComparison.Ordinal) == true);
  }

  [Fact]
  public async Task RendersValidationForStringIntegerAndNumberEditors()
  {
    var service = new PageService { Selected = TypedNamespace(canUpdate: true) };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);

    var text = Find<Entry>(page, "dynamic-config-mode-value");
    text.Text = "observe";
    Assert.Equal("observe", viewModel.Fields.Single(field => field.Name == "mode").Draft);

    var integer = Find<Entry>(page, "dynamic-config-limit-value");
    integer.Text = "5.5";
    Find<Button>(page, "dynamic-config-limit-save").SendClicked();
    await WaitUntilAsync(() => viewModel.Fields.Single(field => field.Name == "limit").HasValidationError);
    Assert.Equal(
        UiLocalization.English.Localize(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsWholeNumber),
        Find<Label>(page, "dynamic-config-limit-validation").Text);

    var number = Find<Entry>(page, "dynamic-config-threshold-value");
    number.Text = "1.5";
    Find<Button>(page, "dynamic-config-threshold-save").SendClicked();
    await WaitUntilAsync(() => viewModel.Fields.Single(field => field.Name == "threshold").HasValidationError);
    Assert.Equal(
        UiLocalization.English.Format(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsMaximumInput,
            ("value", 1)),
        Find<Label>(page, "dynamic-config-threshold-validation").Text);
  }

  [Fact]
  public async Task FailedBooleanMutationRestoresRenderedSwitchAndShowsError()
  {
    var service = new PageService
    {
      Selected = FeatureFlagsNamespace(),
      UpdateFailure = new HttpRequestException("test failure"),
    };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);

    var toggle = Find<Switch>(page, "dynamic-config-fediverse-global");
    toggle.IsToggled = true;

    await WaitUntilAsync(() => viewModel.HasError && !viewModel.IsSaving && !toggle.IsToggled);
    Assert.False(toggle.IsToggled);
    Assert.False(Find<Switch>(page, "dynamic-config-fediverse-global").IsToggled);
    var expectedError = UiLocalization.English.Format(
        UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsUpdateFailedReason,
        ("error", UiText.ExternalContent("test failure")));
    Assert.Contains(Descendants<Label>(page), label => label.Text == expectedError);
  }

  [Fact]
  public async Task ChangedGlobalWriteLocksControlsAndRefreshesRenderedHistory()
  {
    var service = new PageService { Selected = FeatureFlagsNamespace(), HoldUpdate = true, Changed = true };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);

    Find<Switch>(page, "dynamic-config-fediverse-global").IsToggled = true;
    await service.UpdateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.False(Find<Switch>(page, "dynamic-config-fediverse-global").IsEnabled);
    service.ReleaseUpdate();
    await WaitUntilAsync(() =>
        !viewModel.IsSaving &&
        viewModel.FeedbackMessage == UiLocalization.English.Localize(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsUpdated));

    Assert.Equal(2, service.HistoryCalls);
    Assert.True(Find<Switch>(page, "dynamic-config-fediverse-global").IsToggled);
    Assert.Contains(Descendants<Label>(page), label => label.Text?.Contains("fediverse: false → true", StringComparison.Ordinal) == true);
  }

  [Fact]
  public async Task FailedTextAndNumericWritesRetainRenderedDrafts()
  {
    var service = new PageService
    {
      Selected = TypedNamespace(canUpdate: true),
      UpdateFailure = new HttpRequestException("write rejected"),
    };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);

    var text = Find<Entry>(page, "dynamic-config-mode-value");
    text.Text = "observe";
    Find<Button>(page, "dynamic-config-mode-save").SendClicked();
    await WaitUntilAsync(() => service.UpdateCalls == 1 && !viewModel.IsSaving);
    Assert.Equal("observe", text.Text);
    Assert.Equal("observe", viewModel.Fields.Single(field => field.Name == "mode").Draft);

    var number = Find<Entry>(page, "dynamic-config-threshold-value");
    number.Text = "0.7";
    Find<Button>(page, "dynamic-config-threshold-save").SendClicked();
    await WaitUntilAsync(() => service.UpdateCalls == 2 && !viewModel.IsSaving);
    Assert.Equal("0.7", number.Text);
    Assert.Equal("0.7", viewModel.Fields.Single(field => field.Name == "threshold").Draft);
  }

  [Fact]
  public async Task SelectionInFlightKeepsPriorControlsRenderedDisabledAndUnableToMutate()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new PageService
    {
      Selected = TypedNamespace(canUpdate: true),
      SlowNamespace = "other-config",
    };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.LoadAsync(token);
    var page = CreatePage(viewModel);

    var selection = viewModel.SelectAsync("other-config", token);
    await service.SelectionStarted.Task.WaitAsync(token);

    Assert.True(viewModel.IsSelecting);
    Assert.NotEmpty(Descendants<Entry>(page));
    Assert.NotEmpty(Descendants<Switch>(page));
    Assert.All(Descendants<Entry>(page), entry => Assert.False(entry.IsEnabled));
    Assert.All(Descendants<Switch>(page), toggle => Assert.False(toggle.IsEnabled));
    Assert.All(
        Descendants<Button>(page).Where(button =>
            button.AutomationId?.EndsWith("-save", StringComparison.Ordinal) == true),
        button => Assert.False(button.IsEnabled));

    Find<Switch>(page, "dynamic-config-enabled-global").IsToggled = true;
    Find<Button>(page, "dynamic-config-mode-save").SendClicked();
    await Task.Yield();
    Assert.Equal(0, service.UpdateCalls);
    Assert.False(Find<Switch>(page, "dynamic-config-enabled-global").IsToggled);

    service.ReleaseSelection();
    await selection;
    Assert.False(viewModel.IsSelecting);
    Assert.True(Find<Switch>(page, "dynamic-config-enabled-global").IsEnabled);
    Assert.True(Find<Button>(page, "dynamic-config-mode-save").IsEnabled);
  }

  [Fact]
  public void EngineeringDynamicConfigRouteResolvesTheRealNativePage()
  {
    using var state = new FeatureFlagState(new MemoryStore());
    var page = CreatePage(new DynamicConfigViewModel(new PageService { Selected = FeatureFlagsNamespace() }, state));
    var services = new ServiceCollection().AddSingleton(page).BuildServiceProvider();

    Assert.True(DynamicConfigRoutePageFactory.TryCreate(services, "/admin/dynamic-config", out var resolved));
    Assert.Same(page, resolved);
    Assert.False(DynamicConfigRoutePageFactory.TryCreate(services, "/admin/queues", out _));
    Assert.False(DynamicConfigRoutePageFactory.TryCreate(services, "/admin/dynamic-config-legacy", out _));
  }

  private static DynamicConfigPage CreatePage(DynamicConfigViewModel viewModel)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    foreach (var key in UiMessageKey.All)
      application.Resources[key.Value] = UiLocalization.English.Localize(key);
    return new DynamicConfigPage(viewModel);
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

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }

  private static DynamicConfigNamespace TypedNamespace(bool canUpdate) => new(
      "typed-config", "Typed Config", "Typed controls", 4, true, canUpdate,
      new Dictionary<string, DynamicConfigValue>(),
      [
        Field("enabled", "boolean", DynamicConfigValues.From(false)),
        Field("mode", "string", DynamicConfigValues.From("off")),
        Field("threshold", "number", DynamicConfigValues.From(0.5), min: 0, max: 1),
        Field("limit", "number", DynamicConfigValues.From(5), integer: true, min: 1, max: 100),
      ]);

  private static DynamicConfigNamespace FeatureFlagsNamespace() => new(
      "feature-flags", "Feature Flags", "Runtime flags", 1, true, true,
      new Dictionary<string, DynamicConfigValue> { ["fediverse"] = DynamicConfigValues.From(false) },
      [Field("fediverse", "boolean", DynamicConfigValues.From(false))]);

  private static DynamicConfigField Field(
      string name,
      string type,
      DynamicConfigValue value,
      bool integer = false,
      double? min = null,
      double? max = null) => new(name, type, name + " description", value, value, min, max, integer);

  private sealed class PageService : IDynamicConfigService
  {
    private readonly TaskCompletionSource releaseSelection = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource releaseUpdate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public required DynamicConfigNamespace Selected { get; init; }
    public string? SlowNamespace { get; init; }
    public bool HoldUpdate { get; init; }
    public bool Changed { get; init; }
    public Exception? UpdateFailure { get; init; }
    public int HistoryCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public TaskCompletionSource SelectionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource UpdateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<DynamicConfigNamespacesResponse> FetchNamespacesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DynamicConfigNamespacesResponse([
          new(Selected.Namespace, Selected.Label, Selected.Description, Selected.FieldCount, true, Selected.CanUpdate),
          new("other-config", "Other", "Unrelated", 0, true, false),
        ]));

    public async Task<DynamicConfigNamespaceResponse> FetchNamespaceAsync(
        string namespaceName,
        CancellationToken cancellationToken = default)
    {
      if (namespaceName == SlowNamespace)
      {
        SelectionStarted.TrySetResult();
        await releaseSelection.Task.WaitAsync(cancellationToken);
      }
      return new DynamicConfigNamespaceResponse(Selected);
    }

    public async Task<DynamicConfigUpdateResponse> UpdateFieldAsync(
        string namespaceName,
        string field,
        DynamicConfigValue value,
        CancellationToken cancellationToken = default)
    {
      UpdateCalls++;
      UpdateStarted.TrySetResult();
      if (HoldUpdate) await releaseUpdate.Task.WaitAsync(cancellationToken);
      if (UpdateFailure is not null) throw UpdateFailure;
      var updatedFields = Selected.Fields.Select(item => item.Name == field ? item with { Value = value } : item).ToArray();
      var updatedConfig = new Dictionary<string, DynamicConfigValue>(Selected.Config, StringComparer.Ordinal) { [field] = value };
      return new DynamicConfigUpdateResponse(Changed, Selected with { Config = updatedConfig, Fields = updatedFields });
    }

    public Task<DynamicConfigHistoryResponse> FetchHistoryAsync(string namespaceName, CancellationToken cancellationToken = default)
    {
      HistoryCalls++;
      return Task.FromResult(new DynamicConfigHistoryResponse([
        new("history", namespaceName, DateTimeOffset.UnixEpoch, new("user", "qa-developer"),
            new Dictionary<string, DynamicConfigValue>(), new Dictionary<string, DynamicConfigValue>(),
            new Dictionary<string, DynamicConfigFieldChange> { ["fediverse"] = new(DynamicConfigValues.From(false), DynamicConfigValues.From(true)) }),
      ]));
    }

    public void ReleaseSelection() => releaseSelection.TrySetResult();
    public void ReleaseUpdate() => releaseUpdate.TrySetResult();
  }

  private sealed class MemoryStore : IFeatureFlagOverrideStore
  {
    private IReadOnlyDictionary<string, bool> values = new Dictionary<string, bool>();
    public Exception? Failure { get; init; }
    public Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(values);
    public Task SaveAsync(IReadOnlyDictionary<string, bool> newValues, CancellationToken cancellationToken = default)
    {
      if (Failure is not null) return Task.FromException(Failure);
      values = new Dictionary<string, bool>(newValues);
      return Task.CompletedTask;
    }
  }


  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action)
    {
      action();
      return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
      action();
      return true;
    }

    public IDispatcherTimer CreateTimer() => new ImmediateDispatcherTimer();
  }

  private sealed class ImmediateDispatcherTimer : IDispatcherTimer
  {
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; }
    public bool IsRunning { get; private set; }
    public event EventHandler? Tick;
    public void Start()
    {
      IsRunning = true;
      Tick?.Invoke(this, EventArgs.Empty);
      if (!IsRepeating) IsRunning = false;
    }

    public void Stop() => IsRunning = false;
  }
}
