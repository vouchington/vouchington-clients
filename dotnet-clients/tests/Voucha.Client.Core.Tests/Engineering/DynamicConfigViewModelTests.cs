using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.Core.Localization;
using System.Globalization;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class DynamicConfigViewModelTests
{
  [Fact]
  public async Task FiltersNamespacesBySearch()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService();
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.LoadAsync(token);

    viewModel.SearchText = "captcha";

    Assert.Equal("recaptcha-config", Assert.Single(viewModel.FilteredNamespaces).Namespace);
  }

  [Fact]
  public async Task ConcurrentLoadsUseOneServiceRequest()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { HoldNamespaceList = true };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);

    var first = viewModel.LoadAsync(token);
    await service.NamespaceListStarted.Task.WaitAsync(token);
    await viewModel.LoadAsync(token);
    service.ReleaseNamespaceList();
    await first;

    Assert.Equal(1, service.NamespaceListCalls);
  }

  [Theory]
  [InlineData("fr-FR", "1,25", 1.25)]
  [InlineData("en-US", "1.25", 1.25)]
  public void NumericDraftsRoundTripUsingEditorCulture(string cultureName, string draft, double expected)
  {
    var culture = CultureInfo.GetCultureInfo(cultureName);
    var field = new DynamicConfigFieldViewModel(
        new DynamicConfigField("threshold", "number", "", DynamicConfigValues.From(expected), null),
        culture);

    Assert.Equal(draft, field.Draft);
    Assert.True(field.TryValue(out var value));
    Assert.Equal(expected, Assert.IsType<DynamicConfigNumericValue>(value).Value);
  }

  [Fact]
  public void NumericDraftFormattingAndParsingFollowLiveLocaleWithoutOverwritingDirtyDrafts()
  {
    var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var field = new DynamicConfigFieldViewModel(
        new DynamicConfigField(
            "threshold",
            "number",
            "",
            DynamicConfigValues.From(1.25),
            null),
        new UiLocalization(controller));

    Assert.Equal("1.25", field.Draft);

    controller.ApplySavedLocale("fr");
    field.OnUiLocaleChanged();

    Assert.Equal("1,25", field.Draft);
    field.Draft = "2,5";
    Assert.True(field.TryValue(out var value));
    Assert.Equal(2.5, Assert.IsType<DynamicConfigNumericValue>(value).Value);

    controller.ApplySavedLocale("en");
    field.OnUiLocaleChanged();

    Assert.Equal("2,5", field.Draft);
  }

  [Fact]
  public async Task SelectedNumericFieldsUseTheViewModelLiveLocaleForFormattingAndParsing()
  {
    var token = TestContext.Current.CancellationToken;
    var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var service = new StubService();
    using var state = new FeatureFlagState(new MemoryStore());
    using var viewModel = new DynamicConfigViewModel(
        service,
        state,
        new UiLocalization(controller),
        controller);

    await viewModel.SelectAsync("recaptcha-config", token);
    var field = Assert.Single(viewModel.Fields);
    Assert.Equal("0.5", field.Draft);

    controller.ApplySavedLocale("fr");

    Assert.Equal("0,5", field.Draft);
    field.Draft = "0,75";
    Assert.True(field.TryValue(out var value));
    Assert.Equal(0.75, Assert.IsType<DynamicConfigNumericValue>(value).Value);
  }

  [Fact]
  public void FieldTypeMismatchIsSurfacedOnConstructionAndApply()
  {
    var field = new DynamicConfigFieldViewModel(
        new DynamicConfigField("threshold", "number", "", DynamicConfigValues.From("wrong"), null));

    Assert.True(field.HasValidationError);
    Assert.False(field.TryValue(out _));
    field.Apply(new DynamicConfigField("threshold", "string", "", DynamicConfigValues.From(false), null));
    Assert.Contains("does not match", field.ValidationMessage);
  }

  [Fact]
  public async Task SelectLoadsDetailAndHistoryAndRejectsStaleResponses()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { SlowNamespace = DynamicConfigViewModel.FeatureFlagsNamespace };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);

    var stale = viewModel.SelectAsync(DynamicConfigViewModel.FeatureFlagsNamespace, token);
    await viewModel.SelectAsync("recaptcha-config", token);
    service.ReleaseSlowSelection();
    await stale;

    Assert.Equal("recaptcha-config", viewModel.SelectedNamespace?.Namespace);
    Assert.Single(viewModel.History);
  }

  private sealed class StubLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }

  [Fact]
  public async Task SelectionTransitionBlocksWritesUntilLatestSelectionCompletes()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { SlowNamespace = "recaptcha-config" };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync(DynamicConfigViewModel.FeatureFlagsNamespace, token);
    var oldField = Assert.Single(viewModel.Fields);

    var stale = viewModel.SelectAsync("recaptcha-config", token);
    await service.SelectionStarted.Task.WaitAsync(token);
    Assert.True(viewModel.IsSelecting);
    Assert.False(viewModel.CanMutate);
    await viewModel.SaveBooleanAsync(oldField, true, token);
    Assert.Equal(0, service.UpdateCalls);

    await viewModel.SelectAsync("app-attestation-config", token);
    Assert.False(viewModel.IsSelecting);
    Assert.Equal("app-attestation-config", viewModel.SelectedNamespace?.Namespace);
    await viewModel.SaveBooleanAsync(oldField, true, token);
    Assert.Equal(0, service.UpdateCalls);
    service.ReleaseSlowSelection();
    await stale;

    Assert.False(viewModel.IsSelecting);
    Assert.Equal("app-attestation-config", viewModel.SelectedNamespace?.Namespace);
    Assert.True(viewModel.CanMutate);
  }

  [Fact]
  public async Task SaveRetainsInvalidDraftAndPreventsConcurrentMutation()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { HoldUpdates = true };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync("recaptcha-config", token);
    var number = viewModel.Fields.Single(field => field.Name == "threshold");
    number.Draft = "not-a-number";
    await viewModel.SaveDraftAsync(number, token);
    Assert.True(number.HasValidationError);
    Assert.Equal("not-a-number", number.Draft);

    number.Draft = "0.7";
    var first = viewModel.SaveDraftAsync(number, token);
    await service.UpdateStarted.Task;
    await viewModel.SaveDraftAsync(number, token);
    Assert.Equal(1, service.UpdateCalls);
    service.ReleaseUpdate();
    await first;
  }

  [Fact]
  public async Task SaveRefreshesHistoryOnlyWhenChanged()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { Changed = false };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync(DynamicConfigViewModel.FeatureFlagsNamespace, token);
    var field = Assert.Single(viewModel.Fields);
    var initialHistoryCalls = service.HistoryCalls;

    await viewModel.SaveBooleanAsync(field, true, token);
    Assert.Equal(initialHistoryCalls, service.HistoryCalls);
    Assert.Equal("No change to save", viewModel.FeedbackMessage);

    service.Changed = true;
    await viewModel.SaveBooleanAsync(viewModel.Fields.Single(), true, token);
    Assert.Equal(initialHistoryCalls + 1, service.HistoryCalls);
    Assert.Equal("Dynamic config updated", viewModel.FeedbackMessage);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task SaveUpdatesServerStateWithoutDiscardingOtherFieldDrafts(bool changed)
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { Changed = changed };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync("drafts-config", token);
    var saved = viewModel.Fields.Single(field => field.Name == "mode");
    var text = viewModel.Fields.Single(field => field.Name == "note");
    var number = viewModel.Fields.Single(field => field.Name == "threshold");
    saved.Draft = "requested-mode";
    text.Draft = "local-note";
    number.Draft = "not-a-number";
    Assert.False(number.TryValue(out _));
    var validationMessage = number.ValidationMessage;

    await viewModel.SaveDraftAsync(saved, token);

    Assert.Same(saved, viewModel.Fields.Single(field => field.Name == "mode"));
    Assert.Equal("server-mode", saved.Draft);
    Assert.Equal("Updated mode", saved.Description);
    Assert.Equal("Updated drafts", viewModel.SelectedNamespace?.Label);
    Assert.Equal("local-note", text.Draft);
    Assert.Equal("server-note", Assert.IsType<DynamicConfigStringValue>(text.Field.Value).Value);
    Assert.Equal("Updated note", text.Description);
    Assert.Equal("not-a-number", number.Draft);
    Assert.Equal(validationMessage, number.ValidationMessage);
    Assert.Equal(0.25, Assert.IsType<DynamicConfigNumericValue>(number.Field.Value).Value);
    Assert.Equal(0.1, number.Field.MinValue);
  }

  [Fact]
  public async Task BooleanSavePreservesSiblingDraft()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService();
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync("drafts-config", token);
    var saved = viewModel.Fields.Single(field => field.Name == "enabled");
    var sibling = viewModel.Fields.Single(field => field.Name == "note");
    sibling.Draft = "local-note";

    await viewModel.SaveBooleanAsync(saved, true, token);

    Assert.True(saved.BooleanValue);
    Assert.Equal("local-note", sibling.Draft);
    Assert.Equal("server-note", Assert.IsType<DynamicConfigStringValue>(sibling.Field.Value).Value);
  }

  [Fact]
  public async Task DuplicateNamesReconcileByOccurrenceInResponseOrder()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService();
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync("duplicate-config", token);
    var sibling = viewModel.Fields[0];
    var saved = viewModel.Fields[1];
    sibling.Draft = "local-sibling";
    saved.Draft = "requested-saved";

    await viewModel.SaveDraftAsync(saved, token);

    Assert.Same(sibling, viewModel.Fields[0]);
    Assert.Equal("local-sibling", sibling.Draft);
    Assert.Equal("server-first", Assert.IsType<DynamicConfigStringValue>(sibling.Field.Value).Value);
    Assert.Same(saved, viewModel.Fields[1]);
    Assert.Equal("server-saved", saved.Draft);
  }

  [Fact]
  public async Task FailedBooleanSaveRestoresDisplayedServerValue()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { UpdateFailure = new InvalidOperationException("write rejected") };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync(DynamicConfigViewModel.FeatureFlagsNamespace, token);
    var field = Assert.Single(viewModel.Fields);

    await viewModel.SaveBooleanAsync(field, true, token);

    Assert.False(field.BooleanValue);
    Assert.Equal("Failed to update dynamic config: write rejected", viewModel.ErrorMessage);
  }

  [Theory]
  [InlineData("recaptcha-config", "threshold", "0.7")]
  [InlineData("app-attestation-config", "mode", "observe")]
  public async Task FailedTextAndNumericSavesRetainOperatorDraft(
      string namespaceName,
      string fieldName,
      string draft)
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { UpdateFailure = new InvalidOperationException("write rejected") };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync(namespaceName, token);
    var field = viewModel.Fields.Single(item => item.Name == fieldName);
    field.Draft = draft;

    await viewModel.SaveDraftAsync(field, token);

    Assert.Equal(draft, field.Draft);
    Assert.Equal("Failed to update dynamic config: write rejected", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task MutationCompletionDoesNotReplaceNewerNamespaceSelection()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService { HoldUpdates = true };
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync(DynamicConfigViewModel.FeatureFlagsNamespace, token);

    var mutation = viewModel.SaveBooleanAsync(Assert.Single(viewModel.Fields), true, token);
    await service.UpdateStarted.Task.WaitAsync(token);
    await viewModel.SelectAsync("recaptcha-config", token);
    service.ReleaseUpdate();
    await mutation;

    Assert.Equal("recaptcha-config", viewModel.SelectedNamespace?.Namespace);
    Assert.Equal("threshold", Assert.Single(viewModel.Fields).Name);
    Assert.Null(viewModel.FeedbackMessage);
  }

  [Fact]
  public async Task SuccessfulSaveReportsHistoryRefreshFailureWithoutRetryingWrite()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService();
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new DynamicConfigViewModel(service, state);
    await viewModel.SelectAsync(DynamicConfigViewModel.FeatureFlagsNamespace, token);
    service.HistoryFailure = new IOException("history unavailable");

    await viewModel.SaveBooleanAsync(Assert.Single(viewModel.Fields), true, token);

    Assert.Equal(1, service.UpdateCalls);
    Assert.True(Assert.Single(viewModel.Fields).BooleanValue);
    Assert.Equal("Dynamic config updated", viewModel.FeedbackMessage);
    Assert.Equal("Dynamic config updated, but history could not be refreshed: history unavailable", viewModel.ErrorMessage);
  }

  private sealed class StubService : IDynamicConfigService
  {
    private readonly TaskCompletionSource releaseNamespaceList = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource releaseSelection = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource releaseUpdate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource UpdateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource SelectionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string? SlowNamespace { get; init; }
    public bool HoldUpdates { get; init; }
    public bool Changed { get; set; } = true;
    public Exception? UpdateFailure { get; init; }
    public Exception? HistoryFailure { get; set; }
    public int UpdateCalls { get; private set; }
    public int HistoryCalls { get; private set; }
    public int NamespaceListCalls { get; private set; }
    public bool HoldNamespaceList { get; init; }
    public TaskCompletionSource NamespaceListStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<DynamicConfigNamespacesResponse> FetchNamespacesAsync(CancellationToken cancellationToken = default)
    {
      NamespaceListCalls++;
      NamespaceListStarted.TrySetResult();
      if (HoldNamespaceList) await releaseNamespaceList.Task.WaitAsync(cancellationToken);
      return new DynamicConfigNamespacesResponse([
          Summary(DynamicConfigViewModel.FeatureFlagsNamespace, "Feature Flags"), Summary("recaptcha-config", "reCAPTCHA"),
          Summary("app-attestation-config", "App Attestation"),
        ]);
    }

    public async Task<DynamicConfigNamespaceResponse> FetchNamespaceAsync(string namespaceName, CancellationToken cancellationToken = default)
    {
      if (namespaceName == SlowNamespace)
      {
        SelectionStarted.TrySetResult();
        await releaseSelection.Task.WaitAsync(cancellationToken);
      }
      return new DynamicConfigNamespaceResponse(Namespace(namespaceName));
    }

    public async Task<DynamicConfigUpdateResponse> UpdateFieldAsync(string namespaceName, string field, DynamicConfigValue value, CancellationToken cancellationToken = default)
    {
      UpdateCalls++;
      UpdateStarted.TrySetResult();
      if (HoldUpdates) await releaseUpdate.Task.WaitAsync(cancellationToken);
      if (UpdateFailure is not null) throw UpdateFailure;
      return new DynamicConfigUpdateResponse(Changed, Namespace(namespaceName, value, field));
    }

    public Task<DynamicConfigHistoryResponse> FetchHistoryAsync(string namespaceName, CancellationToken cancellationToken = default)
    {
      HistoryCalls++;
      if (HistoryFailure is not null) return Task.FromException<DynamicConfigHistoryResponse>(HistoryFailure);
      return Task.FromResult(new DynamicConfigHistoryResponse([
        new("history", namespaceName, DateTimeOffset.UnixEpoch, null,
            new Dictionary<string, DynamicConfigValue>(), new Dictionary<string, DynamicConfigValue>(),
            new Dictionary<string, DynamicConfigFieldChange>()),
      ]));
    }

    public void ReleaseSlowSelection() => releaseSelection.TrySetResult();
    public void ReleaseUpdate() => releaseUpdate.TrySetResult();
    public void ReleaseNamespaceList() => releaseNamespaceList.TrySetResult();

    private static DynamicConfigNamespaceSummary Summary(string name, string label) => new(name, label, "description", 1, true, true);
    private static DynamicConfigNamespace Namespace(
        string name,
        DynamicConfigValue? updatedValue = null,
        string? updatedFieldName = null)
    {
      if (name == "drafts-config") return DraftsNamespace(updatedValue, updatedFieldName);
      if (name == "duplicate-config") return DuplicateNamespace(updatedFieldName);
      var field = name switch
      {
        DynamicConfigViewModel.FeatureFlagsNamespace => new DynamicConfigField("fediverse", "boolean", "description", updatedValue ?? DynamicConfigValues.From(false), DynamicConfigValues.From(false)),
        "app-attestation-config" => new DynamicConfigField("mode", "string", "description", updatedValue ?? DynamicConfigValues.From("off"), DynamicConfigValues.From("off")),
        _ => new DynamicConfigField("threshold", "number", "description", updatedValue ?? DynamicConfigValues.From(0.5), DynamicConfigValues.From(0.5), 0, 1),
      };
      return new(name, name, "description", 1, true, true,
          new Dictionary<string, DynamicConfigValue> { [field.Name] = field.Value }, [field]);
    }

    private static DynamicConfigNamespace DraftsNamespace(DynamicConfigValue? updatedValue, string? updatedFieldName)
    {
      var updated = updatedFieldName is not null;
      var enabled = updatedFieldName == "enabled" ? updatedValue ?? DynamicConfigValues.From(false) : DynamicConfigValues.From(false);
      DynamicConfigField[] fields =
      [
        new("enabled", "boolean", updated ? "Updated enabled" : "Enabled", enabled, DynamicConfigValues.From(false)),
        new("mode", "string", updated ? "Updated mode" : "Mode", DynamicConfigValues.From(updated ? "server-mode" : "off"), DynamicConfigValues.From("off")),
        new("note", "string", updated ? "Updated note" : "Note", DynamicConfigValues.From(updated ? "server-note" : "initial-note"), DynamicConfigValues.From("initial-note")),
        new("threshold", "number", updated ? "Updated threshold" : "Threshold", DynamicConfigValues.From(updated ? 0.25 : 0.5), DynamicConfigValues.From(0.5), updated ? 0.1 : 0, 1),
      ];
      return new(
          "drafts-config", updated ? "Updated drafts" : "Drafts", "description", fields.Length, true, true,
          fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal), fields);
    }

    private static DynamicConfigNamespace DuplicateNamespace(string? updatedFieldName)
    {
      var updated = updatedFieldName is not null;
      DynamicConfigField[] fields =
      [
        new("mode", "string", "First", DynamicConfigValues.From(updated ? "server-first" : "first"), null),
        new("mode", "string", "Second", DynamicConfigValues.From(updated ? "server-saved" : "second"), null),
      ];
      return new(
          "duplicate-config", "Duplicates", "description", fields.Length, true, true,
          new Dictionary<string, DynamicConfigValue> { ["mode"] = fields[1].Value }, fields);
    }
  }

  private sealed class MemoryStore : IFeatureFlagOverrideStore
  {
    public Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, bool>>(new Dictionary<string, bool>());
    public Task SaveAsync(IReadOnlyDictionary<string, bool> values, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
