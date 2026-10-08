using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task SavingADisabledSelectedHyphenEndpointClearsSelection()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, $"openai-compatible:{profile.Id:D}"));
    var viewModel = Create(configurations, new InMemoryLocalLLMSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEnabled = false;

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.False(Assert.Single(configurations.Load().Profiles).IsEnabled);
    Assert.Null(configurations.Load().SelectedEndpointId);
    Assert.Null(configurations.Load().SelectedProviderId);
  }

  [Fact]
  public async Task FailedOriginSecretClearClearsALeftoverHyphenProviderSelection()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "one", true, "http://127.0.0.1:11434", ["one"], "one");
    var configurations = new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id, $"openai-compatible:{profile.Id:D}"));
    var viewModel = Create(configurations, new FailingSecretStore());
    await viewModel.LoadLocalLLMSettingsAsync(TestContext.Current.CancellationToken);
    viewModel.LocalLLMEndpoint = "http://127.0.0.1:1234";

    await viewModel.SaveLocalLLMSettingsAsync(TestContext.Current.CancellationToken);

    Assert.Null(configurations.Load().SelectedEndpointId);
    Assert.Null(configurations.Load().SelectedProviderId);
    Assert.False(Assert.Single(configurations.Load().Profiles).IsEnabled);
  }
}
