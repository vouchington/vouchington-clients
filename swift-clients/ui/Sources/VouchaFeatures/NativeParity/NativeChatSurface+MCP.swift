import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeChatSurface {
    @ViewBuilder
    func mcpControls() -> some View {
        if let mcpSession, let currentUserId, mcpSession.accountId == currentUserId {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                mcpActionButtons(mcpSession)
                if let error = mcpSession.lastError {
                    Text(UiMessages.string(error, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.negativeVote)
                }
                if mcpSession.status == .connected, !mcpToolNames(mcpSession.toolCatalog).isEmpty {
                    DisclosureGroup(UiMessages.string(.nativeSwiftSettingsMcp, locale: nativeUiLocale)) {
                        ScrollView {
                            VStack(alignment: .leading, spacing: Spacing.xs) {
                                ForEach(mcpToolNames(mcpSession.toolCatalog), id: \.self) { name in
                                    Text(UiMessages.string(.protocolValue(name), locale: nativeUiLocale))
                                        .font(Typography.caption)
                                }
                            }
                        }
                        .frame(maxHeight: 160)
                    }
                }
            }
            .buttonStyle(.bordered)
            .accessibilityIdentifier("native-chat-mcp-controls")
        }
    }

    private func mcpActionButtons(_ session: NativeChatMCPAgentSession) -> some View {
        HStack(spacing: Spacing.sm) {
            if session.status == .connected {
                Text(UiMessages.string(.nativeSwiftChatMcpConnected, locale: nativeUiLocale))
                    .font(Typography.caption)
                Button(UiMessages.string(.nativeSwiftChatMcpRefresh, locale: nativeUiLocale)) {
                    Task { do { try await session.refreshTools() } catch {} }
                }
                Button(UiMessages.string(.nativeSwiftChatMcpDisconnect, locale: nativeUiLocale)) {
                    Task { do { try await session.disconnect() } catch {} }
                }
            } else {
                Text(UiMessages.string(.nativeSwiftChatMcpAuthorizationRequired, locale: nativeUiLocale))
                    .font(Typography.caption)
                Button(UiMessages.string(.nativeSwiftChatMcpConnect, locale: nativeUiLocale)) {
                    Task {
                        do {
                            try await session.connect(presentationWindow: mcpPresentationWindow.value)
                        } catch {}
                    }
                }
                .disabled(session.status == .connecting || session.status == .switchingAccount)
                if session.status == .connecting || session.status == .failed {
                    Button(UiMessages.string(.nativeSwiftChatMcpDisconnect, locale: nativeUiLocale)) {
                        Task { do { try await session.disconnect() } catch {} }
                    }
                }
            }
        }
    }

    private func mcpToolNames(_ catalog: DecodedJSONValue?) -> [String] {
        guard case let .object(result)? = catalog,
              case let .array(tools)? = result["tools"] else { return [] }
        return tools.compactMap { tool in
            guard case let .object(fields) = tool,
                  case let .string(name)? = fields["name"] else { return nil }
            return name
        }
    }
}
