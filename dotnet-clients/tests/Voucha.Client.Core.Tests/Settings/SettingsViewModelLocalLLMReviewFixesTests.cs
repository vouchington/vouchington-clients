using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task ActivatingAnEndpointWithABlankUrlIsRefused()
  {
    var valid = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var invalid = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "", ["two"], "two");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([valid, invalid], valid.Id, LocalChatProviderIds.OpenAICompatible(valid.Id)));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());

    await viewModel.ActivateLocalLLMEndpointAsync(invalid, TestContext.Current.CancellationToken);

    Assert.Equal("Enter a local Responses API endpoint.", viewModel.LocalLLMStatusMessage);
    Assert.Equal(valid.Id, configurations.Load().SelectedEndpointId);
    Assert.False(invalid.IsSelectableAsCurrent);
  }

  [Fact]
  public async Task ActivatingAnEndpointWithNoSelectedModelIsRefused()
  {
    var valid = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var invalid = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([valid, invalid], valid.Id, LocalChatProviderIds.OpenAICompatible(valid.Id)));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());

    await viewModel.ActivateLocalLLMEndpointAsync(invalid, TestContext.Current.CancellationToken);

    Assert.Equal("Choose a local model.", viewModel.LocalLLMStatusMessage);
    Assert.Equal(valid.Id, configurations.Load().SelectedEndpointId);
    Assert.False(invalid.IsSelectableAsCurrent);
  }

  [Fact]
  public async Task ActivatingDoesNotPersistASelectionWhenTheEndpointIsDisabledInsideTheUpdate()
  {
    var enabled = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var target = new LocalLLMEndpointProfile(Guid.NewGuid(), "two", true, "http://127.0.0.1:1234", ["two"], "two");
    var configurations = new DisableDuringUpdateConfigurationStore(new([enabled, target], enabled.Id, LocalChatProviderIds.OpenAICompatible(enabled.Id)), target.Id);
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());

    await viewModel.ActivateLocalLLMEndpointAsync(target, TestContext.Current.CancellationToken);

    Assert.Equal("This local model is disabled. Enable it before making it current.", viewModel.LocalLLMStatusMessage);
    Assert.Equal(enabled.Id, configurations.Load().SelectedEndpointId);
    Assert.Equal(LocalChatProviderIds.OpenAICompatible(enabled.Id), configurations.Load().SelectedProviderId);
  }

  [Fact]
  public async Task SavingDoesNotMarkANewerDraftAsExistingWhenThePriorSaveCompletes()
  {
    var first = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([first], first.Id, LocalChatProviderIds.OpenAICompatible(first.Id)));
    var secrets = new BlockingSaveSecretStore(first.Id, "one-key");
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMSelectedModel = "one-updated";

    var save = viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    await secrets.WaitForSaveStartAsync().WaitAsync(TestContext.Current.CancellationToken);
    viewModel.ResetLocalLLMEndpointDraft();
    viewModel.LocalLLMEnabled = true;
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:9999";
    viewModel.LocalLLMModelsText = "fresh";
    viewModel.LocalLLMSelectedModel = "fresh";
    secrets.ReleaseSave();
    await save;

    Assert.Equal("one-updated", configurations.Load().Profiles.Single(item => item.Id == first.Id).SelectedModelName);
    Assert.NotEqual(first.Id, viewModel.LocalLLMDraftEndpointId);
    Assert.Equal("fresh", viewModel.LocalLLMSelectedModel);

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, configurations.Load().Profiles.Count);
    Assert.Contains(configurations.Load().Profiles, item => item.Endpoint == "http://127.0.0.1:9999");
    Assert.NotEqual("This local model no longer exists.", viewModel.LocalLLMStatusMessage);
  }

  [Fact]
  public async Task FailedOriginChangeSaveRefreshesTheBoundEndpointList()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var viewModel = Create(configurations, new FailingSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("secret clear failed", viewModel.LocalLLMStatusMessage);
    var reflected = Assert.Single(viewModel.LocalLLMEndpoints);
    Assert.False(reflected.IsEnabled);
    Assert.Null(viewModel.LocalLLMSelectedEndpointId);
    Assert.Null(viewModel.LocalLLMSelectedProviderId);
  }

  [Fact]
  public async Task SavingPreservesASelectedModelEditedWhileTheSaveIsInFlight()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new BlockingSaveSecretStore(profile.Id, "one-key");
    var viewModel = Create(configurations, secrets);
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMDisplayName = "renamed";

    var save = viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    await secrets.WaitForSaveStartAsync().WaitAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMSelectedModel = "edited-while-saving";
    secrets.ReleaseSave();
    await save;

    Assert.Equal("edited-while-saving", viewModel.LocalLLMSelectedModel);
  }

  [Fact]
  public async Task CredentialLoadIsRejectedWhenTheStoredOriginChangesMidRead()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    var secrets = new BlockingReadSecretStore(profile.Id, new() { [profile.Id] = "origin-a-key" });
    var viewModel = Create(configurations, secrets);

    var load = viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    await secrets.WaitForReadStartAsync().WaitAsync(TestContext.Current.CancellationToken);
    await secrets.SaveApiKeyAsync(profile.Id, "origin-b-key", TestContext.Current.CancellationToken);
    configurations.Save(new([profile with { Endpoint = "http://127.0.0.1:1234" }], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    secrets.ReleaseRead();
    await load;

    Assert.Equal(string.Empty, viewModel.LocalLLMApiKey);
  }

  private sealed class DisableDuringUpdateConfigurationStore(LocalLLMConfiguration configuration, Guid targetId) : ILocalLLMConfigurationStore
  {
    public LocalLLMConfiguration Load() => configuration;
    public void Save(LocalLLMConfiguration next) => configuration = next;
    public void Update(Func<LocalLLMConfiguration, LocalLLMConfiguration> mutate)
    {
      configuration = configuration with
      {
        Endpoints = configuration.Profiles.Select(item => item.Id == targetId ? item with { IsEnabled = false } : item).ToArray(),
      };
      configuration = mutate(configuration);
    }
    public Task UpdateAsync(Func<LocalLLMConfiguration, Task<LocalLLMConfiguration>> mutate, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task TransactionAsync(Func<ILocalLLMConfigurationTransaction, Task> mutate, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public void Clear() => configuration = new();
  }
}
