using Voucha.Client.App.Chat;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class ChatProviderResolverTests
{
  [Fact]
  public void UnselectedProviderUsesFirstAvailableConfiguredEndpoint()
  {
    var disabled = new LocalLLMEndpointProfile(Guid.NewGuid(), "Disabled");
    var available = new LocalLLMEndpointProfile(Guid.NewGuid(), "Available", true,
        "http://127.0.0.1:11434/v1", ["local-model"], "local-model");
    using var store = new InMemoryLocalLLMConfigurationStore(new([disabled, available]));
    using var responses = new OpenAICompatibleResponsesClient();
    var resolver = CreateResolver(store, responses);

    var status = resolver.GetDefaultProviderStatus();

    Assert.True(status.IsAvailable);
    Assert.Equal(LocalChatProviderIds.OpenAICompatible(available.Id), status.ModelProvider);
    Assert.Equal("local-model", status.ModelName);
  }

  [Fact]
  public void ExplicitUnavailableSelectionRemainsAuthoritative()
  {
    var disabled = new LocalLLMEndpointProfile(Guid.NewGuid(), "Disabled");
    var available = new LocalLLMEndpointProfile(Guid.NewGuid(), "Available", true,
        "http://127.0.0.1:11434/v1", ["local-model"], "local-model");
    using var store = new InMemoryLocalLLMConfigurationStore(new([disabled, available],
        SelectedProviderId: LocalChatProviderIds.OpenAICompatible(disabled.Id)));
    using var responses = new OpenAICompatibleResponsesClient();

    var status = CreateResolver(store, responses).GetDefaultProviderStatus();

    Assert.False(status.IsAvailable);
    Assert.Equal(LocalChatProviderIds.OpenAICompatible(disabled.Id), status.ModelProvider);
  }

  [Fact]
  public void NoAvailableEndpointRetainsUnavailableDefault()
  {
    using var store = new InMemoryLocalLLMConfigurationStore(new(
        [new LocalLLMEndpointProfile(Guid.NewGuid(), "Disabled")]));
    using var responses = new OpenAICompatibleResponsesClient();

    var status = CreateResolver(store, responses).GetDefaultProviderStatus();

    Assert.False(status.IsAvailable);
    Assert.Null(status.ModelProvider);
  }

  private static ChatProviderResolver CreateResolver(InMemoryLocalLLMConfigurationStore store,
      OpenAICompatibleResponsesClient responses) => new(store, new InMemoryLocalLLMSecretStore(),
      responses, new LocalLLMFeaturePolicy(new Dictionary<string, string?>
      {
        ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED"] = "true",
      }), UiLocalization.English);
}
