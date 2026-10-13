using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class ChatConversationPagePendingRetryTests
{
  [Fact]
  public void ProgrammaticComposerClearKeepsThePossibleToolSideEffectGuard()
  {
    InstallReviewPageResources();
    var service = new AmbiguousFirstTurnService();
    var provider = new AvailableProvider { Failure = new PossibleMcpToolSideEffectException() };
    var viewModel = new ChatConversationViewModel(service, new ProviderResolver(provider.Status), provider);
    var page = new ChatConversationPage(viewModel, service);
    var editor = Assert.Single(Descendants<Editor>(page));
    editor.Text = "Mutate once";
    InvokePageHandler(page, "OnSendClicked", [null, EventArgs.Empty]);

    Assert.Equal(string.Empty, editor.Text);
    Assert.True(viewModel.PossibleToolSideEffect);
    Assert.Equal(1, provider.Generations);
    editor.Text = "Mutate once";
    InvokePageHandler(page, "OnSendClicked", [null, EventArgs.Empty]);
    Assert.Equal(1, provider.Generations);
    Assert.True(viewModel.PossibleToolSideEffect);

    editor.Text = "A different request";
    Assert.False(viewModel.PossibleToolSideEffect);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OldTitleLookupCannotLoadAnotherMembersConversation(bool attach)
  {
    InstallReviewPageResources();
    var service = new AmbiguousFirstTurnService();
    var provider = new AvailableProvider();
    var session = new MutableSessionStore(new(new User("member-a", "a")));
    var viewModel = new ChatConversationViewModel(service, new ProviderResolver(provider.Status), provider);
    var page = new ChatConversationPage(viewModel, service, sessionStore: session);
    if (attach) InvokePageHandler(page, "OnAppearing", null);
    var heldTitle = new TaskCompletionSource<ChatConversationListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.ConversationList = () => heldTitle.Task;
    var loading = page.ApplyRouteAsync("conversation-a");
    Assert.False(loading.IsCompleted);

    try
    {
      session.SetCurrent(new(new User("member-b", "b")));
      heldTitle.SetResult(new ChatConversationListResponse([
          new ChatConversation("conversation-a", "Private title from A", DateTimeOffset.UtcNow,
              "member-a", DateTimeOffset.UtcNow, null, null, null)
      ], new PageInfo(null, false, null)));
      await loading.WaitAsync(TestContext.Current.CancellationToken);

      Assert.Null(viewModel.ConversationId);
      Assert.Equal(string.Empty, viewModel.Title);
      Assert.Null(service.LastFetchedConversationId);
    }
    finally
    {
      heldTitle.TrySetResult(new ChatConversationListResponse([], new PageInfo(null, false, null)));
      await loading;
    }
  }

  private static void InvokePageHandler(ChatConversationPage page, string name, object?[]? arguments) =>
      typeof(ChatConversationPage).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic,
          binder: null, types: arguments is null ? Type.EmptyTypes : [typeof(object), typeof(EventArgs)], modifiers: null)!
          .Invoke(page, arguments);

  private static void InstallReviewPageResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application();
    foreach (var key in new[] { "Headline", "Metadata", "Eyebrow", "Body" })
      application.Resources[key] = new Style(typeof(Label));
  }
}
