using System.Net;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class OpenAICompatibleLocalChatProviderTests
{
  [Fact]
  public void StatusIsScopedToItsProfile()
  {
    var first = Profile("first", "first-model"); var second = Profile("second", "second-model");
    var provider = Provider(first, second);
    var status = provider.Status;
    Assert.True(status.IsAvailable); Assert.Equal(LocalChatProviderIds.OpenAICompatible(first.Id), provider.Id);
    Assert.Equal(provider.Id, status.ModelProvider); Assert.Equal("first-model", status.ModelName);
  }

  [Fact]
  public void MissingProfileIsAnUnavailableTombstone()
  {
    var missing = Guid.NewGuid(); var provider = Provider(Profile("other", "model"), profileId: missing);
    Assert.False(provider.Status.IsAvailable); Assert.Equal(LocalChatProviderIds.OpenAICompatible(missing), provider.Id);
  }

  [Fact]
  public void StatusUsesTheProfilesDistinctDisplayName()
  {
    var first = Profile("First endpoint", "first-model");
    var second = Profile("Second endpoint", "second-model");

    Assert.Equal("First endpoint", Provider(first, second).Status.DisplayName);
    Assert.Equal("Second endpoint", Provider(second, first).Status.DisplayName);
  }

  [Fact]
  public void BlankProfileDisplayNameFallsBackOnlyToItsValidatedEndpointOrigin()
  {
    var valid = new LocalLLMEndpointProfile(Guid.NewGuid(), " ", true, "http://127.0.0.1:11434/v1", ["model"], "model");
    var invalid = valid with { Id = Guid.NewGuid(), Endpoint = "https://user:password@example.com/v1" };

    Assert.Equal("http://127.0.0.1:11434", Provider(valid).Status.DisplayName);
    Assert.Equal("Local", Provider(invalid).Status.DisplayName);
  }

  [Fact]
  public async Task GenerationUsesOnlyTheSelectedProfilesSecretAndHistory()
  {
    var first = Profile("first", "first-model"); var second = Profile("second", "second-model");
    var handler = new RecordingHandler(); var secrets = new InMemoryLocalLLMSecretStore();
    await secrets.SaveApiKeyAsync(first.Id, "first-secret", TestContext.Current.CancellationToken);
    await secrets.SaveApiKeyAsync(second.Id, "second-secret", TestContext.Current.CancellationToken);
    var provider = Provider(first, second, first.Id, secrets, handler);
    var response = await provider.GenerateAssistantContentAsync("now", [new("assistant", "earlier")], TestContext.Current.CancellationToken);
    Assert.Equal("ok", response.AssistantContent); Assert.Equal("first-model", response.ModelName);
    Assert.Equal("first-secret", handler.AuthorizationParameter); Assert.Contains("earlier", handler.Body, StringComparison.Ordinal);
  }

  [Fact]
  public async Task GenerationDoesNotFallbackWhenTheProfileIsDisabled()
  {
    var profile = Profile("one", "model") with { IsEnabled = false }; var handler = new RecordingHandler();
    var provider = Provider(profile, handler: handler);
    await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GenerateAssistantContentAsync("now", [], TestContext.Current.CancellationToken));
    Assert.Equal(0, handler.RequestCount);
  }

  private static LocalLLMEndpointProfile Profile(string name, string model) =>
      new(Guid.NewGuid(), name, true, "http://127.0.0.1:11434/v1", [model], model);
  private static OpenAICompatibleLocalChatProvider Provider(LocalLLMEndpointProfile profile, LocalLLMEndpointProfile? other = null,
      Guid? profileId = null, InMemoryLocalLLMSecretStore? secrets = null, RecordingHandler? handler = null)
  {
    IReadOnlyList<LocalLLMEndpointProfile> profiles = other is null ? [profile] : [profile, other];
    return new(new InMemoryLocalLLMConfigurationStore(new(profiles, profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id))),
        secrets ?? new(), new(handler ?? new RecordingHandler()),
        new(new Dictionary<string, string?> { ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED"] = "true" }), profileId ?? profile.Id);
  }

  private sealed class RecordingHandler : HttpMessageHandler
  {
    public string? AuthorizationParameter { get; private set; }
    public string Body { get; private set; } = string.Empty; public int RequestCount { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { RequestCount++; AuthorizationParameter = request.Headers.Authorization?.Parameter; Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken); return new(HttpStatusCode.OK) { Content = new StringContent("""{"output_text":"ok"}""") }; }
  }
}
