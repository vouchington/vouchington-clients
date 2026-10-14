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

  [Fact]
  public async Task TextOnlyOpenAiSchemaErrorUsesGenericLocalCopyInsteadOfMcpConnectionCopy()
  {
    var provider = new TestMcpLocalChatProvider
    {
      Failure = new InvalidDataException("Internal text response parser detail."),
    };
    provider.HideCatalog(disconnected: false);
    var locale = new UiLocaleController(new FrenchDeviceLanguage());
    var localization = new UiLocalization(locale);
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(), new TestChatProviderResolver(provider.Status), provider,
        localization, locale);

    Assert.False(await viewModel.TrySendAsync("Text only", TestContext.Current.CancellationToken));
    Assert.Equal(localization.Localize(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable),
        viewModel.ErrorMessage);
    Assert.NotEqual(localization.Localize(UiMessageKey.NativeDotnetChatMcpConnectionFailed),
        viewModel.ErrorMessage);
    Assert.DoesNotContain("parser detail", viewModel.ErrorMessage, StringComparison.Ordinal);
  }

  private sealed class FrenchDeviceLanguage : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["fr"];
  }
}
