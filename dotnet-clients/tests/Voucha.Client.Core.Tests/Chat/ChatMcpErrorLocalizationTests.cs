using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed partial class ChatViewModelProviderTests
{
  [Fact]
  public async Task InvalidMcpSchemaErrorUsesLocalizedPresentationCopy()
  {
    var provider = new TestMcpLocalChatProvider
    {
      Failure = new InvalidDataException("Internal schema keyword and tool name."),
    };
    var locale = new UiLocaleController(new FrenchDeviceLanguage());
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(), new TestChatProviderResolver(provider.Status), provider,
        new UiLocalization(locale), locale);

    Assert.False(await viewModel.TrySendAsync("Use my tools", TestContext.Current.CancellationToken));
    Assert.Equal("Un problème est survenu. Réessayez.", viewModel.ErrorMessage);
    Assert.DoesNotContain("schema keyword", viewModel.ErrorMessage, StringComparison.Ordinal);
  }

  private sealed class FrenchDeviceLanguage : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["fr"];
  }
}
