using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private string PresentSendError(Exception error, bool isMcpProvider) =>
      error is McpUnauthorizedException or McpRateLimitException or McpRpcException ||
      isMcpProvider && error is InvalidDataException or InvalidOperationException or System.Text.Json.JsonException
      ? localization.Localize(UiMessageKey.NativeDotnetChatMcpConnectionFailed)
      : error.Message;
}
