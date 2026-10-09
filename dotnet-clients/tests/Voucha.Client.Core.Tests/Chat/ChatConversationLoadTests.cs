using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatConversationLoadTests
{
  [Fact]
  public async Task LoadAsyncDisplaysLegacyStringContentMessages()
  {
    var service = new FakeChatService
    {
      ConversationMessagesResult = new ChatMessagesResponse(
          [
            new ChatMessage(
                "message-1",
                "conversation-1",
                DateTimeOffset.Parse("2026-07-01T10:00:01Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:00:01Z"),
                null,
                null,
                null,
                new ChatMessageContent("message", "Legacy plain text", null)),
          ],
          new PageInfo(null, false, null)),
    };
    var viewModel = new ChatConversationViewModel(service);

    await viewModel.LoadAsync("conversation-1", "Legacy", TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Messages);
    Assert.Equal("Legacy plain text", row.Content);
  }

  [Fact]
  public async Task LoadAsyncShowsIncompleteAssistantAsRetryableFailureWithoutEmptyBubble()
  {
    var response = JsonSerializer.Deserialize<ChatMessagesResponse>(
        ApiFixtureLoader.LoadResponse("native.chat.incomplete"), VouchaApiJson.Options)!;
    var service = new FakeChatService
    {
      ConversationMessagesResult = response,
    };
    var localeController = new UiLocaleController(new TestDeviceLanguageProvider("en-US"));
    var viewModel = new ChatConversationViewModel(
        service,
        new TestProviderResolver(),
        new TestLocalProvider(),
        new UiLocalization(localeController),
        localeController);

    await viewModel.LoadAsync("conversation-1", "Conversation", TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Messages);
    Assert.Equal("incomplete", row.CompletionStatus);
    Assert.True(row.IsIncomplete);
    Assert.False(row.HasContent);
    Assert.True(row.HasError);
    Assert.Equal("The response was interrupted. Please try again.", row.DisplayError);

    var displayErrorChanges = 0;
    row.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(ChatMessageRow.DisplayError)) displayErrorChanges++;
    };
    localeController.ApplySavedLocale("fr");

    Assert.Equal(1, displayErrorChanges);
    Assert.Equal("La réponse a été interrompue. Veuillez réessayer.", row.DisplayError);
  }

  [Fact]
  public async Task LoadAsyncIgnoresStaleConversationMessagesAfterRouteChange()
  {
    var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService
    {
      FetchConversationMessagesAsyncOverride = async (conversationId, _) =>
      {
        if (conversationId == "conversation-1")
        {
          firstStarted.SetResult();
          await continueFirst.Task.ConfigureAwait(true);
          return new ChatMessagesResponse(
              [
                new ChatMessage(
                    "message-1",
                    "conversation-1",
                    DateTimeOffset.Parse("2026-07-01T10:00:01Z"),
                    "user-1",
                    DateTimeOffset.Parse("2026-07-01T10:00:01Z"),
                    null,
                    null,
                    null,
                    new ChatMessageContent("message", "First conversation", null)),
              ],
              new PageInfo(null, false, null));
        }

        if (conversationId == "conversation-2")
        {
          secondStarted.SetResult();
          return new ChatMessagesResponse(
              [
                new ChatMessage(
                    "message-2",
                    "conversation-2",
                    DateTimeOffset.Parse("2026-07-01T10:00:02Z"),
                    "user-2",
                    DateTimeOffset.Parse("2026-07-01T10:00:02Z"),
                    null,
                    null,
                    null,
                    new ChatMessageContent("message", "Second conversation", null)),
              ],
              new PageInfo(null, false, null));
        }

        throw new InvalidOperationException($"Unexpected conversation {conversationId}");
      },
    };
    var viewModel = new ChatConversationViewModel(service);

    var firstLoad = viewModel.LoadAsync("conversation-1", "First", TestContext.Current.CancellationToken);
    await firstStarted.Task.ConfigureAwait(true);

    var secondLoad = viewModel.LoadAsync("conversation-2", "Second", TestContext.Current.CancellationToken);
    await secondStarted.Task.ConfigureAwait(true);
    await secondLoad.ConfigureAwait(true);

    continueFirst.SetResult();
    await firstLoad.ConfigureAwait(true);

    Assert.Equal("conversation-2", viewModel.ConversationId);
    Assert.Equal("Second", viewModel.Title);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    var row = Assert.Single(viewModel.Messages);
    Assert.Equal("Second conversation", row.Content);
  }

  private sealed class TestDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }

  private sealed class TestProviderResolver : IChatProviderResolver
  {
    private static readonly ChatProviderStatus Unavailable = new(ChatProviderKind.Local, "Local", false, "Unavailable.");

    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [Unavailable];

    public ChatProviderStatus GetDefaultProviderStatus() => Unavailable;
  }

  private sealed class TestLocalProvider : ILocalChatProvider
  {
    public string Id => "unused";

    public ChatProviderStatus Status => new(ChatProviderKind.Local, "Local", false, "Unavailable.");

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message,
        IReadOnlyList<LocalLLMResponseInput> history,
        CancellationToken cancellationToken = default) =>
        Task.FromException<LocalChatGenerationResult>(new InvalidOperationException("Not used by this test."));
  }
}
