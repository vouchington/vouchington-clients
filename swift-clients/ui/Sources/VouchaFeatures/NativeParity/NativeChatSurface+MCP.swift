import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativeChatSurface {
    @ViewBuilder
    func mcpControls() -> some View {
        if let mcpSession, let currentUserId, mcpSession.accountId == currentUserId {
            HStack(spacing: Spacing.sm) {
                if mcpSession.status == .connected {
                    Text(UiMessages.string(.nativeSwiftChatMcpConnected, locale: nativeUiLocale))
                        .font(Typography.caption)
                    Button(UiMessages.string(.nativeSwiftChatMcpRefresh, locale: nativeUiLocale)) {
                        Task { try? await mcpSession.refreshTools() }
                    }
                    Button(UiMessages.string(.nativeSwiftChatMcpDisconnect, locale: nativeUiLocale)) {
                        Task { try? await mcpSession.disconnect() }
                    }
                } else {
                    Text(UiMessages.string(.nativeSwiftChatMcpAuthorizationRequired, locale: nativeUiLocale))
                        .font(Typography.caption)
                    Button(UiMessages.string(.nativeSwiftChatMcpConnect, locale: nativeUiLocale)) {
                        Task { try? await mcpSession.connect() }
                    }
                    .disabled(mcpSession.status == .connecting || mcpSession.status == .switchingAccount)
                }
            }
            .buttonStyle(.bordered)
            .accessibilityIdentifier("native-chat-mcp-controls")
        }
    }
}
