using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed partial class ChatViewModelProviderTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CompletedMcpTurnPersistsAfterEndpointDeletionOnlyForItsOriginalMember(bool switchAccount)
  {
    var attempts = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        attempts.Add(body);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
      },
    };
    var provider = new TestMcpLocalChatProvider();
    var resolver = new DeletedMcpProviderResolver(provider);
    var viewModel = new ChatConversationViewModel(service, resolver, provider);
    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));
    var resolutions = resolver.Resolutions;
    resolver.IsPresent = false;
    if (switchAccount) provider.UseAccount("account-b");

    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));

    Assert.Equal(1, provider.Generations);
    Assert.Equal(resolutions, resolver.Resolutions);
    Assert.Equal(switchAccount ? 1 : 2, attempts.Count);
    if (!switchAccount) Assert.Equal(attempts[0], attempts[1]);
  }

  private sealed class DeletedMcpProviderResolver(ILocalChatProvider provider) : IChatProviderResolver, ILocalChatProviderResolver
  {
    public bool IsPresent { get; set; } = true;
    public int Resolutions { get; private set; }
    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [provider.Status];
    public ChatProviderStatus GetDefaultProviderStatus() => provider.Status;
    public ILocalChatProvider? GetLocalProvider(string providerId)
    {
      Resolutions++;
      return IsPresent ? provider : null;
    }
  }
}
