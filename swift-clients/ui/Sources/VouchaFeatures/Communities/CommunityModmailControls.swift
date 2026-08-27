import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct CommunityModmailControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let canModerate: Bool
    let showSignIn: () -> Void

    @State
    private var conversationId = ""
    @State
    private var subjectUserId = ""
    @State
    private var message = ""

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesModmail, locale: nativeUiLocale)).font(Typography.headline)
            TextField(
                UiMessages.string(.nativeSwiftCommunitiesConversationId, locale: nativeUiLocale),
                text: $conversationId
            ).textFieldStyle(.roundedBorder)
            TextField(
                UiMessages.string(.nativeSwiftCommunitiesSubjectUserId, locale: nativeUiLocale),
                text: $subjectUserId
            ).textFieldStyle(.roundedBorder)
            TextField(UiMessages.string(.nativeSwiftCommunitiesMessage, locale: nativeUiLocale), text: $message)
                .textFieldStyle(.roundedBorder)
            HStack {
                modmailButton(.nativeSwiftCommunityActionsOpen, systemImage: "bubble.left.and.bubble.right") {
                    Task { await viewModel.openModmailThread(subjectUserId: subjectUserId) }
                }
                modmailButton(.nativeSwiftCommonSend, systemImage: "paperplane") {
                    Task { await viewModel.sendModmailMessage(conversationId: targetConversationId(), text: message) }
                }
                if canModerate {
                    modmailButton(.nativeSwiftCommunityActionsResolve, systemImage: "checkmark.circle") {
                        Task { await viewModel.resolveModmailThread(conversationId: targetConversationId()) }
                    }
                    modmailButton(.nativeSwiftCommunityActionsReopen, systemImage: "arrow.uturn.left") {
                        Task {
                            await viewModel.resolveModmailThread(
                                conversationId: targetConversationId(),
                                resolved: false
                            )
                        }
                    }
                }
            }
        }
    }

    private func modmailButton(
        _ title: UiMessageKey,
        systemImage: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(UiMessages.string(title, locale: nativeUiLocale), systemImage: systemImage) {
            guard isSignedIn else {
                showSignIn()
                return
            }
            action()
        }
    }

    private func targetConversationId() -> String {
        let trimmed = conversationId.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? viewModel.modmailThreadId ?? "" : trimmed
    }
}
