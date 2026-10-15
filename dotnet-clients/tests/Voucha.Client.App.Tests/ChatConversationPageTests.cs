using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ChatConversationPageTests
{
  [Fact]
  public void PickerTransientNullDoesNotDiscardTheSelectedProvider()
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

    var viewModel = new ChatConversationViewModel(new UnusedChatService());
    var page = new ChatConversationPage(viewModel, new UnusedChatService());
    var picker = Assert.Single(VisualTreeElements.Descendants<Picker>(page));
    var selected = viewModel.SelectedProviderStatus;
    Assert.NotNull(picker.SelectedItem);

    picker.SelectedItem = null;

    Assert.Same(selected, viewModel.SelectedProviderStatus);
  }

  [Fact]
  public void IncompleteAssistantRendersFailureWithoutEmptyContentLabel()
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

    var viewModel = new ChatConversationViewModel(new UnusedChatService());
    var page = new ChatConversationPage(viewModel, new UnusedChatService());
    var collection = VisualTreeElements.Descendants<CollectionView>(page).Single(value => ReferenceEquals(value.ItemsSource, viewModel.Messages));
    var template = Assert.IsType<DataTemplate>(collection.ItemTemplate);
    var row = new ChatMessageRow(
        "message-incomplete",
        "assistant",
        string.Empty,
        DateTimeOffset.UtcNow,
        HasError: true,
        Error: "The response was interrupted. Please try again.",
        CompletionStatus: "incomplete");
    var rendered = Assert.IsAssignableFrom<Element>(template.CreateContent());
    rendered.BindingContext = row;
    var labels = VisualTreeElements.Descendants<Label>(rendered).ToArray();
    Assert.Equal(3, labels.Length);
    var content = labels[1];
    var error = labels[2];

    Assert.False(content.IsVisible);
    Assert.Equal(string.Empty, content.Text);
    Assert.True(error.IsVisible);
    Assert.Equal("The response was interrupted. Please try again.", error.Text);
  }

  private sealed class UnusedChatService : IChatService
  {
    public Task<ChatConversationListResponse> FetchMyConversationsAsync(string? after = null, int limit = 50, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ChatConversationResponse> CreateConversationAsync(CreateChatConversationBody body, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ChatMessagesResponse> FetchConversationMessagesAsync(string conversationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ChatConversationResponse> UpdateConversationTitleAsync(string conversationId, string title, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ChatConversationResponse> GenerateConversationTitleAsync(string conversationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ClientGeneratedChatResponse> CreateClientGeneratedChatAsync(string conversationId, CreateClientGeneratedChatBody body, CancellationToken cancellationToken = default) => throw new NotImplementedException();
  }
}
