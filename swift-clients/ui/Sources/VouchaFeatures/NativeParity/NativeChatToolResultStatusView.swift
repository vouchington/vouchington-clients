import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeChatToolResultStatusView: View {
    @Environment(\.locale)
    private var locale
    let ledger: NativeChatMCPReceiptLedger

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(ledger.receipts) { receipt in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(UiMessages.string(
                        receipt.kind == .toolError ? .sharedChatMcpToolErrorReceived : .sharedChatMcpResultReceived,
                        locale: locale
                    ))
                    if case let .emailPreferences(preferences)? = receipt.fact {
                        NativeChatEmailPreferencesFactView(preferences: preferences, locale: locale)
                    }
                }
            }
            if ledger.continuation == .contextStopped {
                Text(UiMessages.string(.sharedChatMcpModelContextStopped, locale: locale))
            } else if ledger.continuation == .processingStopped || ledger.continuation == .cancelled {
                Text(UiMessages.string(.sharedChatMcpModelProcessingStopped, locale: locale))
            }
            Text(UiMessages.string(.sharedChatMcpResultNotSaved, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
        }
        .font(Typography.caption)
        .accessibilityIdentifier("native-chat-tool-result-status")
    }
}

extension NativeChatSurface {
    @ViewBuilder
    func toolResultStatus(viewModel: NativeChatViewModel, messageId: String) -> some View {
        if let ledger = viewModel.mcpReceiptLedger,
           !ledger.receipts.isEmpty, ledger.displayUserMessageId == messageId {
            NativeChatToolResultStatusView(ledger: ledger)
        }
    }
}
