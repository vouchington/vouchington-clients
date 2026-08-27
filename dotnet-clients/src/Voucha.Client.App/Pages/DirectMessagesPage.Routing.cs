using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public partial class DirectMessagesPage
{
  public Task ApplyRouteMatchAsync(NativeRouteMatch? match = null)
  {
    if (match is not null)
    {
      routeMatch = match;
    }

    return ApplyRouteMatchAsyncCore();
  }

  private async Task ApplyRouteMatchAsyncCore()
  {
    var match = routeMatch;
    if (match?.Path is not { Length: > 0 } path || !path.StartsWith("/messages", StringComparison.Ordinal))
    {
      return;
    }

    var conversationId = match.Param("conversationId");
    var routeKey = $"{path}|{conversationId}";
    if (string.Equals(path, "/messages/new", StringComparison.Ordinal))
    {
      if (!string.IsNullOrWhiteSpace(viewModel.SelectedConversationId))
      {
        viewModel.ClearSelectedConversationState();
        MessageEditor.Text = string.Empty;
      }

      RecipientSearchEntry.Focus();
      appliedRouteKey = routeKey;
      return;
    }

    if (string.IsNullOrWhiteSpace(conversationId))
    {
      if (string.Equals(path, "/messages", StringComparison.Ordinal) &&
          !string.IsNullOrWhiteSpace(viewModel.SelectedConversationId))
      {
        viewModel.ClearSelectedConversationState();
        MessageEditor.Text = string.Empty;
      }

      appliedRouteKey = routeKey;
      return;
    }

    if (string.Equals(routeKey, appliedRouteKey, StringComparison.Ordinal) &&
        (string.Equals(conversationId, "new", StringComparison.Ordinal) ||
        string.Equals(viewModel.SelectedConversationId, conversationId, StringComparison.Ordinal)))
    {
      return;
    }

    if (!string.Equals(conversationId, "new", StringComparison.Ordinal))
    {
      var previousConversationId = viewModel.SelectedConversationId;
      await viewModel.SelectConversationAsync(conversationId);
      if (!string.Equals(previousConversationId, conversationId, StringComparison.Ordinal))
      {
        MessageEditor.Text = string.Empty;
      }
      MessageEditor.Focus();
      appliedRouteKey = routeKey;
      return;
    }
  }
}
