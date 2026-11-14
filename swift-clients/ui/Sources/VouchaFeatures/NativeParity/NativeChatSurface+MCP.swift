import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeChatSurface {
    @ViewBuilder
    func mcpControls() -> some View {
        if let mcpSession, let currentUserId, mcpSession.accountId == currentUserId {
            @Bindable var mcpSession = mcpSession
            VStack(alignment: .leading, spacing: Spacing.sm) {
                Toggle(
                    UiMessages.string(.nativeCredentialsWriteAction, locale: nativeUiLocale),
                    isOn: $mcpSession.requestWriteAccess
                )
                .disabled(mcpSession.status == .connecting || mcpSession.status == .switchingAccount)
                if mcpSession.requestWriteAccess {
                    Button(UiMessages.string(.nativeSwiftTagManagementViewPlans, locale: nativeUiLocale)) {
                        onNavigate("/plans")
                    }
                }
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
                Button(UiMessages.string(.nativeCredentialsChooseScopes, locale: nativeUiLocale)) {
                    consent(session)
                }
                Button(UiMessages.string(.nativeSwiftChatMcpDisconnect, locale: nativeUiLocale)) {
                    Task { do { try await session.disconnect() } catch {} }
                }
            } else {
                Text(UiMessages.string(.nativeSwiftChatMcpAuthorizationRequired, locale: nativeUiLocale))
                    .font(Typography.caption)
                Button(UiMessages.string(.nativeSwiftChatMcpConnect, locale: nativeUiLocale)) {
                    consent(session)
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

    private func consent(_ session: NativeChatMCPAgentSession) {
        let scope = session.requestWriteAccess ? "mcp.user:read mcp.user:write" : "mcp.user:read"
        Task {
            do { try await session.connect(presentationWindow: mcpPresentationWindow.value, scope: scope) } catch {}
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
