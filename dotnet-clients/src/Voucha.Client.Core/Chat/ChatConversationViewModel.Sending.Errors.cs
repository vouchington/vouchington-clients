using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private string PresentSendError(Exception error, bool isMcpProvider, bool hasMcpSnapshot) => error switch
  {
    McpUnauthorizedException or McpRateLimitException or McpRpcException =>
        localization.Localize(UiMessageKey.NativeDotnetChatMcpConnectionFailed),
    InvalidDataException or InvalidOperationException or System.Text.Json.JsonException when isMcpProvider =>
        localization.Localize(hasMcpSnapshot
            ? UiMessageKey.NativeDotnetChatMcpConnectionFailed
            : UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable),
    _ => error.Message,
  };
}
