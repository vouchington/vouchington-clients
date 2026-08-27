using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task RotatingAnEndpointSecretPreservesAConcurrentWindowsProviderSelection()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new BlockingClearSecretStore(profile.Id, "old-key");
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    var save = viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    await secrets.WaitForClearAsync().WaitAsync(TestContext.Current.CancellationToken);
    var selectionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var selectWindows = Task.Run(() =>
    {
      selectionStarted.TrySetResult(true);
      LocalLLMProviderSelection.Save(configurations, LocalChatProviderIds.WindowsSystemLanguageModel);
    }, TestContext.Current.CancellationToken);
    await selectionStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.False(selectWindows.IsCompleted);
    secrets.ReleaseClear();
    await save;
    await selectWindows;

    var persisted = configurations.Load();
    Assert.Equal(LocalChatProviderIds.WindowsSystemLanguageModel, persisted.SelectedProviderId);
  }

  [Fact]
  public async Task FailedFirstCredentialSaveLeavesTheEndpointDisabledUntilRetry()
  {
    var configurations = new InMemoryLocalLLMConfigurationStore();
    var secrets = new RetryableSaveSecretStore { Failing = true };
    var viewModel = Create(configurations, secrets);
    viewModel.LocalLLMEnabled = true;
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:11434";
    viewModel.LocalLLMModelsText = "one";
    viewModel.LocalLLMApiKey = "new-key";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    var staged = Assert.Single(configurations.Load().Profiles);
    Assert.False(staged.IsEnabled);
    Assert.Null(configurations.Load().SelectedProviderId);
    Assert.Equal("credential save failed", viewModel.LocalLLMStatusMessage);

    secrets.Failing = false;
    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    var saved = Assert.Single(configurations.Load().Profiles);
    Assert.True(saved.IsEnabled);
    Assert.Equal("new-key", await secrets.ReadApiKeyAsync(saved.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task FailedCredentialRemovalKeepsANewlyEnabledProfileDisabledUntilRetry()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", false, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile]));
    var secrets = new RetryableRemovalSecretStore(profile.Id, "old-key") { Failing = true };
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEnabled = true;
    viewModel.LocalLLMApiKey = string.Empty;

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.False(Assert.Single(configurations.Load().Profiles).IsEnabled);
    Assert.Equal("old-key", await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
    Assert.Equal("credential removal failed", viewModel.LocalLLMStatusMessage);

    secrets.Failing = false;
    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.True(Assert.Single(configurations.Load().Profiles).IsEnabled);
    Assert.Null(await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task FailedOriginClearRequiresCleanupBeforeRetryCanEnableTheNewOrigin()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new RetryableOriginClearSecretStore(profile.Id, "old-key") { Failing = true };
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(string.Empty, viewModel.LocalLLMApiKey);
    var tombstone = Assert.Single(configurations.Load().Profiles);
    Assert.False(tombstone.IsEnabled);
    Assert.True(tombstone.RequiresCredentialReplacement);
    Assert.Equal("old-key", await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));

    viewModel.LocalLLMEnabled = false;
    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    Assert.True(Assert.Single(configurations.Load().Profiles).RequiresCredentialReplacement);
    Assert.Equal("old-key", await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));

    var retryViewModel = Create(configurations, secrets);
    await retryViewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(string.Empty, retryViewModel.LocalLLMApiKey);
    retryViewModel.LocalLLMEnabled = true;
    await retryViewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.True(Assert.Single(configurations.Load().Profiles).RequiresCredentialReplacement);

    secrets.Failing = false;
    var finalRetryViewModel = Create(configurations, secrets);
    await finalRetryViewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(string.Empty, finalRetryViewModel.LocalLLMApiKey);
    finalRetryViewModel.LocalLLMEnabled = true;
    await finalRetryViewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.True(Assert.Single(configurations.Load().Profiles).IsEnabled);
    Assert.False(Assert.Single(configurations.Load().Profiles).RequiresCredentialReplacement);
    Assert.Null(await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
    Assert.DoesNotContain("old-key", secrets.SavedApiKeys);

    await finalRetryViewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    Assert.Null(await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task FailedOriginClearWithReplacementRequiresCleanupAfterReload()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new RetryableOriginClearSecretStore(profile.Id, "old-key") { Failing = true };
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";
    viewModel.LocalLLMApiKey = "replacement-key";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    var tombstone = Assert.Single(configurations.Load().Profiles);
    Assert.False(tombstone.IsEnabled);
    Assert.True(tombstone.RequiresCredentialReplacement);
    Assert.Equal("old-key", await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));

    var retryViewModel = Create(configurations, secrets);
    await retryViewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(string.Empty, retryViewModel.LocalLLMApiKey);
    retryViewModel.LocalLLMEnabled = true;
    retryViewModel.LocalLLMApiKey = "replacement-key";
    secrets.Failing = false;

    await retryViewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    var saved = Assert.Single(configurations.Load().Profiles);
    Assert.True(saved.IsEnabled);
    Assert.False(saved.RequiresCredentialReplacement);
    Assert.Equal("replacement-key", await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
    Assert.DoesNotContain("old-key", secrets.SavedApiKeys);
  }

  [Fact]
  public async Task FailedFinalOriginWriteClearsTheLoadedCredentialBeforeRetry()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new FailingFinalWriteConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id))) { FailFinalWrite = true };
    var secrets = new RecordingSecretStore(profile.Id, "old-key");
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(string.Empty, viewModel.LocalLLMApiKey);
    Assert.Equal("final configuration write failed", viewModel.LocalLLMStatusMessage);
    Assert.False(Assert.Single(configurations.Load().Profiles).IsEnabled);

    configurations.FailFinalWrite = false;
    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.True(Assert.Single(configurations.Load().Profiles).IsEnabled);
    Assert.DoesNotContain("old-key", secrets.SavedApiKeys);
    Assert.Null(await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task DeletingAnEndpointQueuesAConcurrentSaveUntilItsSecretIsRemoved()
  {
    var oldProfile = new LocalLLMEndpointProfile(Guid.NewGuid(), "old", true, "http://127.0.0.1:11434", ["old"], "old");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([oldProfile], oldProfile.Id, LocalChatProviderIds.OpenAICompatible(oldProfile.Id)));
    var secrets = new BlockingClearSecretStore(oldProfile.Id, "old-key");
    var deletingViewModel = Create(configurations, secrets);
    var savingViewModel = Create(configurations, secrets);
    savingViewModel.LocalLLMEnabled = true;
    savingViewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";
    savingViewModel.LocalLLMModelsText = "new";
    savingViewModel.LocalLLMApiKey = "new-key";

    var delete = deletingViewModel.DeleteLocalLLMEndpointAsync(oldProfile, TestContext.Current.CancellationToken);
    await secrets.WaitForClearAsync().WaitAsync(TestContext.Current.CancellationToken);
    var saveStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var save = Task.Run(async () =>
    {
      saveStarted.TrySetResult(true);
      await savingViewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    }, TestContext.Current.CancellationToken);
    await saveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.False(save.IsCompleted);
    secrets.ReleaseClear();
    await delete;
    await save;

    var saved = Assert.Single(configurations.Load().Profiles);
    Assert.Equal("http://127.0.0.1:1234", saved.Endpoint);
    Assert.Equal("new-key", await secrets.ReadApiKeyAsync(saved.Id, TestContext.Current.CancellationToken));
    Assert.Null(await secrets.ReadApiKeyAsync(oldProfile.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task SelectingASecondProfileWhileTheFirstProfilesSecretIsStillLoadingPreservesTheSecondProfilesKey()
  {
    var first = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var second = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([first, second]));
    var secrets = new BlockingReadSecretStore(first.Id, new() { [first.Id] = "one-key", [second.Id] = "two-key" });
    var viewModel = Create(configurations, secrets);

    var selectFirst = viewModel.SelectLocalLLMEndpointDraftAsync(first, TestContext.Current.CancellationToken);
    await secrets.WaitForReadStartAsync().WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectLocalLLMEndpointDraftAsync(second, TestContext.Current.CancellationToken);
    Assert.Equal("two-key", viewModel.LocalLLMApiKey);
    Assert.Equal(second.Id, viewModel.LocalLLMDraftEndpointId);
    secrets.ReleaseRead();
    await selectFirst;

    Assert.Equal("two-key", viewModel.LocalLLMApiKey);
    Assert.Equal(second.Id, viewModel.LocalLLMDraftEndpointId);
  }

  [Fact]
  public async Task SavingWhileSwitchingToAnotherProfileDoesNotLeakThePreviousProfilesKeyIntoIt()
  {
    var first = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var second = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([first, second]));
    var secrets = new BlockingReadSecretStore(second.Id, new() { [first.Id] = "one-key", [second.Id] = "two-key" });
    var viewModel = Create(configurations, secrets);
    await viewModel.SelectLocalLLMEndpointDraftAsync(first, TestContext.Current.CancellationToken);
    Assert.Equal("one-key", viewModel.LocalLLMApiKey);

    var select = viewModel.SelectLocalLLMEndpointDraftAsync(second, TestContext.Current.CancellationToken);
    await secrets.WaitForReadStartAsync().WaitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(string.Empty, viewModel.LocalLLMApiKey);

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    secrets.ReleaseRead();
    await select;

    Assert.Equal("two-key", viewModel.LocalLLMApiKey);
    Assert.Equal("one-key", await secrets.ReadApiKeyAsync(first.Id, TestContext.Current.CancellationToken));
    Assert.Equal("two-key", await secrets.ReadApiKeyAsync(second.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task DeletingAnEndpointStagesItDisabledBeforeClearingItsSecretAndBeforeRemovingItFromTheConfiguration()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var callOrder = new List<string>();
    var configurations = new OrderTrackingConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)), profile.Id, callOrder);
    var secrets = new OrderTrackingSecretStore(profile.Id, "old-key", callOrder);
    var viewModel = Create(configurations, secrets);

    await viewModel.DeleteLocalLLMEndpointAsync(profile, TestContext.Current.CancellationToken);

    Assert.Equal(["staged-disable", "clear-secret", "removed"], callOrder);
    Assert.Empty(configurations.Load().Profiles);
    Assert.Null(await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task EditingTheApiKeyWhileItsOwnProfileCredentialIsStillLoadingPreservesTheEdit()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new BlockingReadSecretStore(profile.Id, new() { [profile.Id] = "loaded-key" });
    var viewModel = Create(configurations, secrets);

    var load = viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    await secrets.WaitForReadStartAsync().WaitAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMApiKey = "edited-key";
    secrets.ReleaseRead();
    await load;

    Assert.Equal("edited-key", viewModel.LocalLLMApiKey);
  }

  [Fact]
  public async Task SavingAfterItsProfileWasDeletedRejectsTheSaveInsteadOfResurrectingIt()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new RecordingSecretStore(profile.Id, "old-key");
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    var deletingViewModel = Create(configurations, secrets);
    await deletingViewModel.DeleteLocalLLMEndpointAsync(profile, TestContext.Current.CancellationToken);
    Assert.Empty(configurations.Load().Profiles);

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Empty(configurations.Load().Profiles);
    Assert.Equal("This local model no longer exists.", viewModel.LocalLLMStatusMessage);
  }

  private sealed class BlockingReadSecretStore(Guid blockedProfileId, Dictionary<Guid, string> apiKeys) : ILocalLLMSecretStore
  {
    private readonly TaskCompletionSource<bool> readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> readReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
      if (id != blockedProfileId) return apiKeys.GetValueOrDefault(id);
      readStarted.TrySetResult(true);
      await readReleased.Task.WaitAsync(cancellationToken);
      return apiKeys.GetValueOrDefault(id);
    }
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default) { apiKeys[id] = value; return Task.CompletedTask; }
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default) { apiKeys.Remove(id); return Task.CompletedTask; }
    public Task WaitForReadStartAsync() => readStarted.Task;
    public void ReleaseRead() => readReleased.TrySetResult(true);
  }

  private sealed class OrderTrackingConfigurationStore(LocalLLMConfiguration configuration, Guid trackedProfileId, List<string> callOrder) : ILocalLLMConfigurationStore
  {
    public LocalLLMConfiguration Load() => configuration;
    public void Save(LocalLLMConfiguration next) => configuration = next;
    public void Update(Func<LocalLLMConfiguration, LocalLLMConfiguration> mutate) => configuration = mutate(configuration);
    public async Task UpdateAsync(Func<LocalLLMConfiguration, Task<LocalLLMConfiguration>> mutate, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      configuration = await mutate(configuration);
    }
    public async Task TransactionAsync(Func<ILocalLLMConfigurationTransaction, Task> mutate, CancellationToken cancellationToken = default)
    {
      var transaction = new OrderTrackingTransaction(configuration, next => configuration = next, trackedProfileId, callOrder);
      await mutate(transaction);
    }
    public void Clear() => configuration = new();
  }

  private sealed class OrderTrackingTransaction(LocalLLMConfiguration configuration, Action<LocalLLMConfiguration> save, Guid trackedProfileId, List<string> callOrder) : ILocalLLMConfigurationTransaction
  {
    public LocalLLMConfiguration Configuration { get; private set; } = configuration;
    public void Save(LocalLLMConfiguration next)
    {
      var staged = next.Profiles.SingleOrDefault(item => item.Id == trackedProfileId);
      callOrder.Add(staged is null ? "removed" : staged.IsEnabled ? "enabled" : "staged-disable");
      save(next);
      Configuration = next;
    }
    public void Clear() { save(new()); Configuration = new(); }
  }

  private sealed class OrderTrackingSecretStore(Guid profileId, string initialApiKey, List<string> callOrder) : ILocalLLMSecretStore
  {
    private string? apiKey = initialApiKey;
    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == profileId ? apiKey : null);
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default) { if (id == profileId) apiKey = value; return Task.CompletedTask; }
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default) { callOrder.Add("clear-secret"); if (id == profileId) apiKey = null; return Task.CompletedTask; }
  }

  private sealed class BlockingClearSecretStore(Guid profileId, string initialApiKey) : ILocalLLMSecretStore
  {
    private readonly TaskCompletionSource<bool> clearStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> clearReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<Guid, string> apiKeys = new() { [profileId] = initialApiKey };

    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(apiKeys.GetValueOrDefault(id));
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default) { apiKeys[id] = value; return Task.CompletedTask; }
    public async Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
      clearStarted.TrySetResult(true);
      await clearReleased.Task.WaitAsync(cancellationToken);
      apiKeys.Remove(id);
    }
    public Task WaitForClearAsync() => clearStarted.Task;
    public void ReleaseClear() => clearReleased.TrySetResult(true);
  }

  private sealed class BlockingSaveSecretStore(Guid profileId, string initialApiKey) : ILocalLLMSecretStore
  {
    private readonly TaskCompletionSource<bool> saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> saveReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<Guid, string> apiKeys = new() { [profileId] = initialApiKey };

    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(apiKeys.GetValueOrDefault(id));
    public async Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default)
    {
      saveStarted.TrySetResult(true);
      await saveReleased.Task.WaitAsync(cancellationToken);
      apiKeys[id] = value;
    }
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default) { apiKeys.Remove(id); return Task.CompletedTask; }
    public Task WaitForSaveStartAsync() => saveStarted.Task;
    public void ReleaseSave() => saveReleased.TrySetResult(true);
  }

  private sealed class RetryableSaveSecretStore : ILocalLLMSecretStore
  {
    private readonly Dictionary<Guid, string> apiKeys = [];
    public bool Failing { get; set; }
    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(apiKeys.GetValueOrDefault(id));
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default)
    {
      if (Failing) return Task.FromException(new InvalidOperationException("credential save failed"));
      apiKeys[id] = value;
      return Task.CompletedTask;
    }
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default) { apiKeys.Remove(id); return Task.CompletedTask; }
  }

  private sealed class RetryableRemovalSecretStore(Guid profileId, string initialApiKey) : ILocalLLMSecretStore
  {
    private string? apiKey = initialApiKey;
    public bool Failing { get; set; }
    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == profileId ? apiKey : null);
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default)
    {
      if (Failing) return Task.FromException(new InvalidOperationException("credential removal failed"));
      if (id == profileId) apiKey = string.IsNullOrWhiteSpace(value) ? null : value;
      return Task.CompletedTask;
    }
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default) { if (id == profileId) apiKey = null; return Task.CompletedTask; }
  }

  private sealed class RetryableOriginClearSecretStore(Guid profileId, string initialApiKey) : ILocalLLMSecretStore
  {
    private string? apiKey = initialApiKey;
    public bool Failing { get; set; }
    public List<string> SavedApiKeys { get; } = [];
    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == profileId ? apiKey : null);
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default) { SavedApiKeys.Add(value); if (id == profileId) apiKey = string.IsNullOrWhiteSpace(value) ? null : value; return Task.CompletedTask; }
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
      if (Failing) return Task.FromException(new InvalidOperationException("secret clear failed"));
      if (id == profileId) apiKey = null;
      return Task.CompletedTask;
    }
  }

  private sealed class RecordingSecretStore(Guid profileId, string initialApiKey) : ILocalLLMSecretStore
  {
    private string? apiKey = initialApiKey;
    public List<string> SavedApiKeys { get; } = [];
    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == profileId ? apiKey : null);
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default) { SavedApiKeys.Add(value); apiKey = string.IsNullOrWhiteSpace(value) ? null : value; return Task.CompletedTask; }
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default) { if (id == profileId) apiKey = null; return Task.CompletedTask; }
  }

  private sealed class FailingFinalWriteConfigurationStore(LocalLLMConfiguration configuration) : ILocalLLMConfigurationStore
  {
    public bool FailFinalWrite { get; set; }
    public LocalLLMConfiguration Load() => configuration;
    public void Save(LocalLLMConfiguration next) => configuration = next;
    public void Update(Func<LocalLLMConfiguration, LocalLLMConfiguration> mutate) => configuration = mutate(configuration);
    public async Task UpdateAsync(Func<LocalLLMConfiguration, Task<LocalLLMConfiguration>> mutate, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      configuration = await mutate(configuration);
    }
    public async Task TransactionAsync(Func<ILocalLLMConfigurationTransaction, Task> mutate, CancellationToken cancellationToken = default)
    {
      var transaction = new FailingFinalWriteTransaction(configuration, next => configuration = next, () => configuration = new(), () => FailFinalWrite);
      await mutate(transaction);
    }
    public void Clear() => configuration = new();
  }

  private sealed class FailingFinalWriteTransaction(LocalLLMConfiguration configuration, Action<LocalLLMConfiguration> save, Action clear, Func<bool> shouldFail) : ILocalLLMConfigurationTransaction
  {
    private int saves;
    public LocalLLMConfiguration Configuration { get; private set; } = configuration;
    public void Save(LocalLLMConfiguration next)
    {
      if (saves++ == 1 && shouldFail()) throw new InvalidOperationException("final configuration write failed");
      save(next);
      Configuration = next;
    }
    public void Clear() { clear(); Configuration = new(); }
  }
}
