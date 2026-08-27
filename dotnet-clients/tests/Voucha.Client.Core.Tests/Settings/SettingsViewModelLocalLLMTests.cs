using System.Net;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task SavingTheFirstEndpointProfilePreservesHostedSelectionAndPersistsItsSecret()
  {
    var configurationStore = new InMemoryLocalLLMConfigurationStore(); var secretStore = new InMemoryLocalLLMSecretStore();
    var viewModel = Create(configurationStore, secretStore);
    viewModel.LocalLLMEnabled = true; viewModel.LocalLLMEndpoint = "http://127.0.0.1:11434/v1";
    viewModel.LocalLLMModelsText = "gpt-oss-20b\nllama-local"; viewModel.LocalLLMSelectedModel = "gpt-oss-20b"; viewModel.LocalLLMApiKey = "local-key";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    var profile = Assert.Single(configurationStore.Load().Profiles);
    Assert.Null(configurationStore.Load().SelectedEndpointId);
    Assert.Null(configurationStore.Load().SelectedProviderId);
    Assert.Equal("local-key", await secretStore.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task SavingANewlyCreatedDraftAgainAfterItWasDeletedElsewhereRejectsTheSaveInsteadOfResurrectingIt()
  {
    var configurations = new InMemoryLocalLLMConfigurationStore();
    var secrets = new InMemoryLocalLLMSecretStore();
    var viewModel = Create(configurations, secrets);
    viewModel.LocalLLMEnabled = true;
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:11434";
    viewModel.LocalLLMModelsText = "one";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    var created = Assert.Single(configurations.Load().Profiles);

    var deletingViewModel = Create(configurations, secrets);
    await deletingViewModel.DeleteLocalLLMEndpointAsync(created, TestContext.Current.CancellationToken);
    Assert.Empty(configurations.Load().Profiles);

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Empty(configurations.Load().Profiles);
    Assert.Equal("This local model no longer exists.", viewModel.LocalLLMStatusMessage);
  }

  [Fact]
  public async Task SavingAnEndpointProfilePreservesAnExistingHostedSelection()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Null(configurations.Load().SelectedProviderId);
    Assert.Equal(profile.Id, configurations.Load().SelectedEndpointId);
  }

  [Fact]
  public async Task SavingAnEndpointProfilePreservesAnExistingWindowsSelection()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.WindowsSystemLanguageModel));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LocalChatProviderIds.WindowsSystemLanguageModel, configurations.Load().SelectedProviderId);
  }

  [Fact]
  public async Task DeletingTheSelectedEndpointRemovesItsSecretAndFallsBackSelectionToTheNextRemainingEndpoint()
  {
    var first = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var second = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurationStore = new InMemoryLocalLLMConfigurationStore(new([first, second], first.Id, LocalChatProviderIds.OpenAICompatible(first.Id)));
    var secrets = new InMemoryLocalLLMSecretStore();
    await secrets.SaveApiKeyAsync(first.Id, "one", TestContext.Current.CancellationToken);
    await secrets.SaveApiKeyAsync(second.Id, "two", TestContext.Current.CancellationToken);
    var viewModel = Create(configurationStore, secrets);

    await viewModel.DeleteLocalLLMEndpointAsync(first, TestContext.Current.CancellationToken);

    var remaining = Assert.Single(configurationStore.Load().Profiles);
    Assert.Equal(second.Id, remaining.Id);
    Assert.Equal(second.Id, configurationStore.Load().SelectedEndpointId);
    Assert.Null(configurationStore.Load().SelectedProviderId);
    Assert.Null(await secrets.ReadApiKeyAsync(first.Id, TestContext.Current.CancellationToken));
    Assert.Equal("two", await secrets.ReadApiKeyAsync(second.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task DeletingAnUnselectedEndpointLeavesTheSelectionUnchanged()
  {
    var first = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var second = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurationStore = new InMemoryLocalLLMConfigurationStore(new([first, second], first.Id, LocalChatProviderIds.OpenAICompatible(first.Id)));
    var secrets = new InMemoryLocalLLMSecretStore();
    await secrets.SaveApiKeyAsync(second.Id, "two", TestContext.Current.CancellationToken);
    var viewModel = Create(configurationStore, secrets);

    await viewModel.DeleteLocalLLMEndpointAsync(second, TestContext.Current.CancellationToken);

    var remaining = Assert.Single(configurationStore.Load().Profiles);
    Assert.Equal(first.Id, remaining.Id);
    Assert.Equal(first.Id, configurationStore.Load().SelectedEndpointId);
    Assert.Equal(LocalChatProviderIds.OpenAICompatible(first.Id), configurationStore.Load().SelectedProviderId);
    Assert.Null(await secrets.ReadApiKeyAsync(second.Id, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task DeletingTheProfileCurrentlyLoadedInTheDraftResetsTheDraft()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurationStore = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new InMemoryLocalLLMSecretStore();
    await secrets.SaveApiKeyAsync(profile.Id, "key", TestContext.Current.CancellationToken);
    var viewModel = Create(configurationStore, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    await viewModel.DeleteLocalLLMEndpointAsync(profile, TestContext.Current.CancellationToken);

    Assert.Equal(string.Empty, viewModel.LocalLLMDisplayName);
    Assert.Empty(configurationStore.Load().Profiles);
    Assert.NotEqual(profile.Id, viewModel.LocalLLMDraftEndpointId);
  }

  [Fact]
  public async Task FailedDeleteLeavesTheEndpointAndItsSelectionIntact()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new FailingUpdateConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new TrackingSecretStore(profile.Id, "old-key");
    var viewModel = Create(configurations, secrets);

    await viewModel.DeleteLocalLLMEndpointAsync(profile, TestContext.Current.CancellationToken);

    Assert.Equal("configuration write failed", viewModel.LocalLLMStatusMessage);
    var persisted = Assert.Single(configurations.Load().Profiles);
    Assert.Equal(profile.Id, persisted.Id);
    Assert.Equal(profile.Id, configurations.Load().SelectedEndpointId);
    Assert.Equal("old-key", await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
    Assert.Equal(0, secrets.ClearCalls);
  }

  [Fact]
  public async Task FailedSecretClearDuringDeleteLeavesTheEndpointDisabledAndUnselected()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var viewModel = Create(configurations, new FailingSecretStore());

    await viewModel.DeleteLocalLLMEndpointAsync(profile, TestContext.Current.CancellationToken);

    Assert.Equal("secret clear failed", viewModel.LocalLLMStatusMessage);
    var persisted = Assert.Single(configurations.Load().Profiles);
    Assert.Equal(profile.Id, persisted.Id);
    Assert.False(persisted.IsEnabled);
    Assert.Null(configurations.Load().SelectedEndpointId);
    Assert.Null(configurations.Load().SelectedProviderId);
    var reflected = Assert.Single(viewModel.LocalLLMEndpoints);
    Assert.False(reflected.IsEnabled);
    Assert.Null(viewModel.LocalLLMSelectedEndpointId);
    Assert.Null(viewModel.LocalLLMSelectedProviderId);
  }

  [Fact]
  public async Task ActivatingAnEndpointUpdatesTheSelectionWithoutTouchingTheDraft()
  {
    var first = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var second = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurationStore = new InMemoryLocalLLMConfigurationStore(new([first, second], first.Id, LocalChatProviderIds.OpenAICompatible(first.Id)));
    var viewModel = Create(configurationStore, new InMemoryLocalLLMSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    await viewModel.ActivateLocalLLMEndpointAsync(second, TestContext.Current.CancellationToken);

    Assert.Equal(second.Id, configurationStore.Load().SelectedEndpointId);
    Assert.Equal(LocalChatProviderIds.OpenAICompatible(second.Id), configurationStore.Load().SelectedProviderId);
    Assert.Equal(second.Id, viewModel.LocalLLMSelectedEndpointId);
    Assert.Equal(first.DisplayName, viewModel.LocalLLMDisplayName);
  }

  [Fact]
  public async Task FailedActivateLeavesTheSelectionUnchanged()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var other = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurations = new FailingUpdateConfigurationStore(new([profile, other], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());

    await viewModel.ActivateLocalLLMEndpointAsync(other, TestContext.Current.CancellationToken);

    Assert.Equal("configuration write failed", viewModel.LocalLLMStatusMessage);
    Assert.Equal(profile.Id, configurations.Load().SelectedEndpointId);
  }

  [Fact]
  public async Task ActivatingADisabledEndpointIsRefused()
  {
    var enabled = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var disabled = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", false, "http://127.0.0.1:1234", ["two"], "two");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([enabled, disabled], enabled.Id, LocalChatProviderIds.OpenAICompatible(enabled.Id)));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());

    await viewModel.ActivateLocalLLMEndpointAsync(disabled, TestContext.Current.CancellationToken);

    Assert.Equal("This local model is disabled. Enable it before making it current.", viewModel.LocalLLMStatusMessage);
    Assert.Equal(enabled.Id, configurations.Load().SelectedEndpointId);
  }

  [Fact]
  public async Task ActivatingAnEndpointThatNoLongerExistsIsRefused()
  {
    var enabled = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var deleted = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([enabled], enabled.Id, LocalChatProviderIds.OpenAICompatible(enabled.Id)));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());

    await viewModel.ActivateLocalLLMEndpointAsync(deleted, TestContext.Current.CancellationToken);

    Assert.Equal("This local model no longer exists.", viewModel.LocalLLMStatusMessage);
    Assert.Equal(enabled.Id, configurations.Load().SelectedEndpointId);
  }

  [Fact]
  public async Task SelectingAnEndpointDraftLoadsItsFieldsAndSecretWithoutChangingThePersistedSelection()
  {
    var first = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var second = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurationStore = new InMemoryLocalLLMConfigurationStore(new([first, second], first.Id, LocalChatProviderIds.OpenAICompatible(first.Id)));
    var secrets = new InMemoryLocalLLMSecretStore();
    await secrets.SaveApiKeyAsync(second.Id, "two-key", TestContext.Current.CancellationToken);
    var viewModel = Create(configurationStore, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    await viewModel.SelectLocalLLMEndpointDraftAsync(second, TestContext.Current.CancellationToken);

    Assert.Equal("two", viewModel.LocalLLMDisplayName);
    Assert.Equal("http://127.0.0.1:1234", viewModel.LocalLLMEndpoint);
    Assert.Equal("two-key", viewModel.LocalLLMApiKey);
    Assert.Equal(second.Id, viewModel.LocalLLMDraftEndpointId);
    Assert.Equal(first.Id, configurationStore.Load().SelectedEndpointId);
  }

  [Fact]
  public void ResettingTheDraftBlanksEveryFieldAndAssignsAFreshId()
  {
    var configurationStore = new InMemoryLocalLLMConfigurationStore();
    var viewModel = Create(configurationStore, new InMemoryLocalLLMSecretStore());
    viewModel.LocalLLMDisplayName = "one";
    viewModel.LocalLLMEnabled = false;
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:11434";
    viewModel.LocalLLMModelsText = "one";
    viewModel.LocalLLMSelectedModel = "one";
    viewModel.LocalLLMApiKey = "secret";
    var draftId = viewModel.LocalLLMDraftEndpointId;

    viewModel.ResetLocalLLMEndpointDraft();

    Assert.NotEqual(draftId, viewModel.LocalLLMDraftEndpointId);
    Assert.Equal(string.Empty, viewModel.LocalLLMDisplayName);
    Assert.True(viewModel.LocalLLMEnabled);
    Assert.Equal(string.Empty, viewModel.LocalLLMEndpoint);
    Assert.Equal(string.Empty, viewModel.LocalLLMModelsText);
    Assert.Equal(string.Empty, viewModel.LocalLLMSelectedModel);
    Assert.Equal(string.Empty, viewModel.LocalLLMApiKey);
  }

  [Fact]
  public async Task LoadingAndChangingAnEndpointOriginClearsThatProfileSecretBeforeSaving()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new TrackingSecretStore(profile.Id, "old-key");
    var viewModel = Create(configurations, secrets);

    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";
    viewModel.LocalLLMApiKey = "new-key";
    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(1, secrets.ClearCalls);
    Assert.Equal(profile.Id, secrets.ClearedProfileId);
    Assert.Equal("new-key", await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
    Assert.Equal("http://127.0.0.1:1234", configurations.Load().SelectedEndpoint?.Endpoint);
  }

  [Fact]
  public async Task ChangingAnEndpointOriginDoesNotReuseItsPriorCredential()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new TrackingSecretStore(profile.Id, "old-key");
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Null(await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
    Assert.Equal("http://127.0.0.1:1234", configurations.Load().SelectedEndpoint?.Endpoint);
  }

  [Fact]
  public async Task FailedOriginSecretClearLeavesTheNewOriginDisabledAndUnselected()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var viewModel = Create(configurations, new FailingSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("secret clear failed", viewModel.LocalLLMStatusMessage);
    var persisted = Assert.Single(configurations.Load().Profiles);
    Assert.Equal("http://127.0.0.1:1234", persisted.Endpoint);
    Assert.False(persisted.IsEnabled);
    Assert.Null(configurations.Load().SelectedEndpointId);
    Assert.Null(configurations.Load().SelectedProviderId);
  }

  [Fact]
  public async Task FailedSafetyWritePreservesThePriorOriginAndCredential()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new FailingUpdateConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new TrackingSecretStore(profile.Id, "old-key");
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("configuration write failed", viewModel.LocalLLMStatusMessage);
    Assert.Equal(profile.Endpoint, configurations.Load().SelectedEndpoint?.Endpoint);
    Assert.Equal("old-key", await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
    Assert.Equal(0, secrets.ClearCalls);
  }

  [Fact]
  public async Task FailedReplacementCredentialSaveLeavesTheNewOriginDisabledAndUnselected()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var viewModel = Create(configurations, new FailingReplacementSecretStore(profile.Id, "old-key"));
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";
    viewModel.LocalLLMApiKey = "new-key";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    var persisted = Assert.Single(configurations.Load().Profiles);
    Assert.False(persisted.IsEnabled);
    Assert.Null(configurations.Load().SelectedEndpointId);
    Assert.Null(configurations.Load().SelectedProviderId);
    Assert.Equal("replacement save failed", viewModel.LocalLLMStatusMessage);
  }

  [Fact]
  public async Task SavingADisabledSelectedEndpointReturnsSelectionToHosted()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEnabled = false;

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.False(Assert.Single(configurations.Load().Profiles).IsEnabled);
    Assert.Null(configurations.Load().SelectedEndpointId);
    Assert.Null(configurations.Load().SelectedProviderId);
  }

  [Fact]
  public async Task SavingADisabledEndpointPreservesAnActiveWindowsSelection()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.WindowsSystemLanguageModel));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEnabled = false;

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Null(configurations.Load().SelectedEndpointId);
    Assert.Equal(LocalChatProviderIds.WindowsSystemLanguageModel, configurations.Load().SelectedProviderId);
  }

  [Fact]
  public async Task ResettingTheDraftWhileACredentialIsStillLoadingClearsTheLoadingLatchSoSavingWorksAfterward()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new BlockingReadSecretStore(profile.Id, new() { [profile.Id] = "loaded-key" });
    var viewModel = Create(configurations, secrets);

    var load = viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    await secrets.WaitForReadStartAsync().WaitAsync(TestContext.Current.CancellationToken);
    viewModel.ResetLocalLLMEndpointDraft();
    secrets.ReleaseRead();
    await load;
    viewModel.LocalLLMEnabled = true;
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:9999";
    viewModel.LocalLLMModelsText = "fresh";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, configurations.Load().Profiles.Count);
    Assert.Contains(configurations.Load().Profiles, item => item.Endpoint == "http://127.0.0.1:9999");
  }

  [Fact]
  public async Task SavingAnEndpointsSecretWhileItIsBeingDeletedNeverWritesTheSecretBackAfterDeletion()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new BlockingSaveSecretStore(profile.Id, "old-key");
    var savingViewModel = Create(configurations, secrets);
    var deletingViewModel = Create(configurations, secrets);
    await savingViewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    savingViewModel.LocalLLMApiKey = "new-key";

    var save = savingViewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    await secrets.WaitForSaveStartAsync().WaitAsync(TestContext.Current.CancellationToken);
    var deleteStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var delete = Task.Run(async () =>
    {
      deleteStarted.TrySetResult(true);
      await deletingViewModel.DeleteLocalLLMEndpointAsync(profile, TestContext.Current.CancellationToken);
    }, TestContext.Current.CancellationToken);
    await deleteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.False(delete.IsCompleted);
    secrets.ReleaseSave();
    await save;
    await delete;

    Assert.Empty(configurations.Load().Profiles);
    Assert.Null(await secrets.ReadApiKeyAsync(profile.Id, TestContext.Current.CancellationToken));
  }

  private static SettingsViewModel Create(ILocalLLMConfigurationStore configurations, ILocalLLMSecretStore secrets) =>
      new(new FakeSettingsService(), configurations, secrets, new OpenAICompatibleResponsesClient(new ReadyHandler()),
          new LocalLLMFeaturePolicy(new Dictionary<string, string?> { ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED"] = "true" }));

  private sealed class ReadyHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"output_text":"ready"}""") });
  }

  private sealed class TrackingSecretStore(Guid profileId, string initialApiKey) : ILocalLLMSecretStore
  {
    private string? apiKey = initialApiKey;
    public int ClearCalls { get; private set; }
    public Guid? ClearedProfileId { get; private set; }
    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == profileId ? apiKey : null);
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default) { if (id == profileId) apiKey = value; return Task.CompletedTask; }
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default) { ClearCalls++; ClearedProfileId = id; if (id == profileId) apiKey = null; return Task.CompletedTask; }
  }

  private sealed class FailingSecretStore : ILocalLLMSecretStore
  {
    public Task<string?> ReadApiKeyAsync(Guid profileId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task SaveApiKeyAsync(Guid profileId, string apiKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ClearApiKeyAsync(Guid profileId, CancellationToken cancellationToken = default) => Task.FromException(new InvalidOperationException("secret clear failed"));
  }

  private sealed class FailingUpdateConfigurationStore(LocalLLMConfiguration configuration) : ILocalLLMConfigurationStore
  {
    public LocalLLMConfiguration Load() => configuration;
    public void Save(LocalLLMConfiguration next) => configuration = next;
    public void Update(Func<LocalLLMConfiguration, LocalLLMConfiguration> mutate) => throw new InvalidOperationException("configuration write failed");
    public Task UpdateAsync(Func<LocalLLMConfiguration, Task<LocalLLMConfiguration>> mutate, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("configuration write failed"));
    public Task TransactionAsync(Func<ILocalLLMConfigurationTransaction, Task> mutate, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("configuration write failed"));
    public void Clear() => configuration = new();
  }

  private sealed class FailingReplacementSecretStore(Guid profileId, string initialApiKey) : ILocalLLMSecretStore
  {
    private string? apiKey = initialApiKey;
    public Task<string?> ReadApiKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == profileId ? apiKey : null);
    public Task SaveApiKeyAsync(Guid id, string value, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("replacement save failed"));
    public Task ClearApiKeyAsync(Guid id, CancellationToken cancellationToken = default) { if (id == profileId) apiKey = null; return Task.CompletedTask; }
  }
}
