using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class LocalLLMProviderSelectionTests
{
  [Fact]
  public void EndpointAndWindowsSelectionPersistWithoutReplacingEndpointConfiguration()
  {
    var endpoint = new LocalLLMEndpointProfile(Guid.NewGuid(), "Local", true, "http://127.0.0.1:11434", ["model"], "model");
    var store = new InMemoryLocalLLMConfigurationStore(new([endpoint], null));

    LocalLLMProviderSelection.Save(store, LocalChatProviderIds.OpenAICompatible(endpoint.Id));
    Assert.Equal(endpoint.Id, store.Load().SelectedEndpointId);
    Assert.Equal(LocalChatProviderIds.OpenAICompatible(endpoint.Id), store.Load().SelectedProviderId);

    LocalLLMProviderSelection.Save(store, LocalChatProviderIds.WindowsSystemLanguageModel);
    Assert.Equal(endpoint.Id, store.Load().SelectedEndpointId);
    Assert.Equal(LocalChatProviderIds.WindowsSystemLanguageModel, store.Load().SelectedProviderId);
  }

  [Fact]
  public void HyphenEndpointSelectionIsPersistedAsTheCanonicalUnderscoreId()
  {
    var endpoint = new LocalLLMEndpointProfile(Guid.NewGuid(), "Local", true, "http://127.0.0.1:11434", ["model"], "model");
    var store = new InMemoryLocalLLMConfigurationStore(new([endpoint], null));

    LocalLLMProviderSelection.Save(store, $"openai-compatible:{endpoint.Id:D}");

    Assert.Equal(endpoint.Id, store.Load().SelectedEndpointId);
    Assert.Equal(LocalChatProviderIds.OpenAICompatible(endpoint.Id), store.Load().SelectedProviderId);
  }

  [Fact]
  public void HostedSelectionClearsTheExplicitProvider()
  {
    var store = new InMemoryLocalLLMConfigurationStore(new([], null, LocalChatProviderIds.WindowsSystemLanguageModel));

    LocalLLMProviderSelection.Save(store, null);

    Assert.Null(store.Load().SelectedProviderId);
  }

  [Fact]
  public async Task ConfigurationUpdatesDoNotLoseInterleavedProfileAndProviderChanges()
  {
    var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    try
    {
      var path = Path.Combine(directory, "local-llm.json");
      var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Local", true, "http://127.0.0.1:11434", ["model"], "model");
      var store = new FileLocalLLMConfigurationStore(path);
      using var firstMutationEntered = new ManualResetEventSlim();
      using var secondMutationAttempted = new ManualResetEventSlim();
      using var secondMutationEntered = new ManualResetEventSlim();
      using var releaseFirstMutation = new ManualResetEventSlim();
      var first = Task.Run(() => store.Update(configuration =>
      {
        firstMutationEntered.Set();
        releaseFirstMutation.Wait(TestContext.Current.CancellationToken);
        return configuration with { Endpoints = [profile] };
      }), TestContext.Current.CancellationToken);
      Assert.True(firstMutationEntered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
      var second = Task.Run(() =>
      {
        secondMutationAttempted.Set();
        store.Update(configuration =>
        {
          secondMutationEntered.Set();
          return configuration with { SelectedProviderId = LocalChatProviderIds.WindowsSystemLanguageModel };
        });
      }, TestContext.Current.CancellationToken);
      Assert.True(secondMutationAttempted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
      Assert.False(secondMutationEntered.Wait(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
      releaseFirstMutation.Set();
      await Task.WhenAll(first, second);

      var configuration = store.Load();
      Assert.Equal(profile.Id, Assert.Single(configuration.Profiles).Id);
      Assert.Equal(LocalChatProviderIds.WindowsSystemLanguageModel, configuration.SelectedProviderId);
    }
    finally
    {
      if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
  }

  [Theory]
  [InlineData("endpoint")]
  [InlineData("windows")]
  [InlineData("hosted")]
  public void FreshViewModelUsesThePersistedProviderSelection(string selection)
  {
    var endpoint = new LocalLLMEndpointProfile(Guid.NewGuid(), "Local", true, "http://127.0.0.1:11434", ["model"], "model");
    var store = new InMemoryLocalLLMConfigurationStore(new([endpoint]));
    var endpointId = LocalChatProviderIds.OpenAICompatible(endpoint.Id);
    LocalLLMProviderSelection.Save(store, selection switch
    {
      "endpoint" => endpointId,
      "windows" => LocalChatProviderIds.WindowsSystemLanguageModel,
      _ => null,
    });

    var viewModel = new ChatConversationViewModel(new FakeChatService(), new ConfigurationResolver(store, endpointId), new StaticProvider("fallback"));

    Assert.Equal(selection switch
    {
      "endpoint" => endpointId,
      "windows" => LocalChatProviderIds.WindowsSystemLanguageModel,
      _ => "hosted",
    }, viewModel.SelectedProviderStatus.ModelProvider ?? "hosted");
  }

  [Fact]
  public void FreshViewModelUsesHostedWhenTheOnlyEndpointIsDisabled()
  {
    var endpoint = new LocalLLMEndpointProfile(Guid.NewGuid(), "Local", false, "http://127.0.0.1:11434", ["model"], "model");
    var store = new InMemoryLocalLLMConfigurationStore(new([endpoint], null, null));
    var endpointId = LocalChatProviderIds.OpenAICompatible(endpoint.Id);

    var viewModel = new ChatConversationViewModel(new FakeChatService(), new ConfigurationResolver(store, endpointId), new StaticProvider("fallback"));

    Assert.Equal("hosted", viewModel.SelectedProviderStatus.ModelProvider ?? "hosted");
  }

  private sealed class ConfigurationResolver(ILocalLLMConfigurationStore store, string endpointId) : IChatProviderResolver
  {
    private static readonly ChatProviderStatus Hosted = new(ChatProviderKind.Hosted, "Hosted", true, "Hosted");
    private static readonly ChatProviderStatus Windows = new(ChatProviderKind.Local, "Windows", true, "Ready.", LocalChatProviderIds.WindowsSystemLanguageModel);
    private ChatProviderStatus Endpoint => new(ChatProviderKind.Local, "Endpoint", true, "Ready.", endpointId);
    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [Hosted, Windows, Endpoint];
    public ChatProviderStatus GetDefaultProviderStatus() => store.Load().SelectedProviderId switch
    {
      LocalChatProviderIds.WindowsSystemLanguageModel => Windows,
      var id when id == endpointId => Endpoint,
      _ => Hosted,
    };
  }

  private sealed class StaticProvider(string id) : ILocalChatProvider
  {
    public string Id => id;
    public ChatProviderStatus Status => new(ChatProviderKind.Local, id, false, "Unavailable.", id);
    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) =>
        Task.FromException<LocalChatGenerationResult>(new InvalidOperationException("Unavailable."));
  }
}
