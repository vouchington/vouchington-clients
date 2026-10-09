using System.Net.Http;
using System.Reflection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ChatConversationPagePendingRetryTests
{
  [Fact]
  public async Task FailedFirstTurnReloadsItsCreatedConversation()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
        ["Eyebrow"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
      },
    };
    var service = new AmbiguousFirstTurnService();
    var provider = new AvailableProvider();
    var viewModel = new ChatConversationViewModel(service, new ProviderResolver(provider.Status), provider);
    var page = new ChatConversationPage(viewModel, service);
    var editor = Assert.Single(Descendants<Editor>(page));
    editor.Text = "Hello";

    typeof(ChatConversationPage).GetMethod("OnSendClicked", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, [null, EventArgs.Empty]);
    Assert.Equal("conversation-1", viewModel.ConversationId);
    Assert.Equal(LoadState.Error, viewModel.State);

    var reload = Assert.IsAssignableFrom<Task>(typeof(ChatConversationPage)
        .GetMethod("LoadConversationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, null));
    await reload;
    Assert.Equal("conversation-1", service.LastFetchedConversationId);
  }

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private sealed class ProviderResolver(ChatProviderStatus status) : IChatProviderResolver
  {
    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [status];
    public ChatProviderStatus GetDefaultProviderStatus() => status;
  }

  private sealed class AvailableProvider : ILocalChatProvider
  {
    public string Id => "windows-system-language-model";
    public ChatProviderStatus Status { get; } = new(
        ChatProviderKind.Local, "Windows local", true, "Ready", "windows-system-language-model", "windows-system-language-model");

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LocalChatGenerationResult("Reply", "windows_foundry", "windows-system-language-model"));
  }

  private sealed class AmbiguousFirstTurnService : IChatService
  {
    public string? LastFetchedConversationId { get; private set; }

    public Task<ChatConversationListResponse> FetchMyConversationsAsync(
        string? after = null, int limit = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatConversationListResponse([], new PageInfo(null, false, null)));

    public Task<ChatConversationResponse> CreateConversationAsync(
        CreateChatConversationBody body, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatConversationResponse(new ChatConversation(
            "conversation-1", "", DateTimeOffset.UtcNow, "user-1", DateTimeOffset.UtcNow, null, null, null)));

    public Task<ChatMessagesResponse> FetchConversationMessagesAsync(
        string conversationId, CancellationToken cancellationToken = default)
    {
      LastFetchedConversationId = conversationId;
      return Task.FromResult(new ChatMessagesResponse([], new PageInfo(null, false, null)));
    }

    public Task<ClientGeneratedChatResponse> CreateClientGeneratedChatAsync(
        string conversationId, CreateClientGeneratedChatBody body, CancellationToken cancellationToken = default) =>
        Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));

    public Task<ChatConversationResponse> UpdateConversationTitleAsync(
        string conversationId, string title, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ChatConversationResponse> GenerateConversationTitleAsync(
        string conversationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
