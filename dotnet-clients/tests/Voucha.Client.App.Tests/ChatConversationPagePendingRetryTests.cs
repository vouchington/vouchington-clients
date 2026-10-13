using System.Net.Http;
using System.Reflection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed partial class ChatConversationPagePendingRetryTests
{
  [Fact]
  public async Task RoutingTheSamePageToAnotherConversationClearsTheRestoredComposer()
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
    editor.Text = "Mutate once";
    Assert.False(await viewModel.TrySendAsync(editor.Text, TestContext.Current.CancellationToken));
    editor.Text = "Mutate once, edited";

    await page.ApplyRouteAsync("conversation-2", "Other");

    Assert.Equal(string.Empty, editor.Text);
    Assert.Equal("conversation-2", viewModel.ConversationId);
    Assert.Single(service.Attempts);
  }

  [Fact]
  public async Task MountedAndReturningChatPageClearsThePreviousMembersComposer()
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
    var session = new MutableSessionStore(new(new User("member-a", "a")));
    var viewModel = new ChatConversationViewModel(service, new ProviderResolver(provider.Status), provider);
    var page = new ChatConversationPage(viewModel, service, sessionStore: session);
    await page.ApplyRouteAsync("conversation-1", "First");
    var editor = Assert.Single(Descendants<Editor>(page));
    editor.Text = "Mutate once";
    Assert.False(await viewModel.TrySendAsync(editor.Text, TestContext.Current.CancellationToken));
    typeof(ChatConversationPage).GetMethod("OnAppearing", BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null, types: Type.EmptyTypes, modifiers: null)!
        .Invoke(page, null);

    session.SetCurrent(new(new User("member-b", "b")));
    Assert.Equal(string.Empty, editor.Text);
    Assert.Null(viewModel.ConversationId);
    Assert.Empty(viewModel.Messages);
    Assert.Single(service.Attempts);

    editor.Text = "Private draft from B";
    typeof(ChatConversationPage).GetMethod("OnDisappearing", BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null, types: Type.EmptyTypes, modifiers: null)!
        .Invoke(page, null);
    session.SetCurrent(new(new User("member-c", "c")));
    Assert.Equal("Private draft from B", editor.Text);
    typeof(ChatConversationPage).GetMethod("OnAppearing", BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null, types: Type.EmptyTypes, modifiers: null)!
        .Invoke(page, null);
    Assert.Equal(string.Empty, editor.Text);
    Assert.Single(service.Attempts);
  }

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

  [Fact]
  public async Task FailedInitialMessageReturnsToComposerForExactRetry()
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
    page.SetContext(initialMessage: "Hello");
    var editor = Assert.Single(Descendants<Editor>(page));

    var initialSend = Assert.IsAssignableFrom<Task>(typeof(ChatConversationPage)
        .GetMethod("SendInitialMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, null));
    await initialSend;

    Assert.Equal("Hello", editor.Text);
    Assert.Equal("conversation-1", viewModel.ConversationId);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Single(service.Attempts);

    Assert.False(await viewModel.TrySendAsync(editor.Text, TestContext.Current.CancellationToken));
    Assert.Equal(2, service.Attempts.Count);
    Assert.Equal(service.Attempts[0], service.Attempts[1]);
  }

  [Fact]
  public async Task PossibleToolSideEffectDoesNotRestoreInitialMessageForOneTapRetry()
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
    var provider = new AvailableProvider { Failure = new PossibleMcpToolSideEffectException() };
    var viewModel = new ChatConversationViewModel(service, new ProviderResolver(provider.Status), provider);
    var page = new ChatConversationPage(viewModel, service);
    page.SetContext(initialMessage: "Mutate once");
    var editor = Assert.Single(Descendants<Editor>(page));

    var initialSend = Assert.IsAssignableFrom<Task>(typeof(ChatConversationPage)
        .GetMethod("SendInitialMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, null));
    await initialSend;

    Assert.True(string.IsNullOrEmpty(editor.Text));
    Assert.True(viewModel.PossibleToolSideEffect);
    Assert.Empty(service.Attempts);
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

  private sealed class MutableSessionStore(SessionSnapshot initial) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = initial;

    public void SetCurrent(SessionSnapshot snapshot)
    {
      Current = snapshot;
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(snapshot));
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class AvailableProvider : ILocalChatProvider
  {
    public Exception? Failure { get; set; }
    public int Generations { get; private set; }
    public string Id => "windows-system-language-model";
    public ChatProviderStatus Status { get; } = new(
        ChatProviderKind.Local, "Windows local", true, "Ready", "windows-system-language-model", "windows-system-language-model");

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default)
    {
      Generations++;
      return Failure is null
          ? Task.FromResult(new LocalChatGenerationResult("Reply", "windows_foundry", "windows-system-language-model"))
          : Task.FromException<LocalChatGenerationResult>(Failure);
    }
  }

  private sealed class AmbiguousFirstTurnService : IChatService
  {
    public Func<Task<ChatConversationListResponse>>? ConversationList { get; set; }
    public string? LastFetchedConversationId { get; private set; }
    public List<CreateClientGeneratedChatBody> Attempts { get; } = [];

    public Task<ChatConversationListResponse> FetchMyConversationsAsync(
        string? after = null, int limit = 50, CancellationToken cancellationToken = default) =>
        ConversationList?.Invoke() ?? Task.FromResult(new ChatConversationListResponse([], new PageInfo(null, false, null)));

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
        string conversationId, CreateClientGeneratedChatBody body, CancellationToken cancellationToken = default)
    {
      Attempts.Add(body);
      return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
    }

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
