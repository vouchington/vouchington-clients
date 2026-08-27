import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension DirectMessagesSurface {
    var inboxView: some View {
        VStack(spacing: Spacing.md) {
            HStack {
                Spacer()
                Button(
                    UiMessages.string(.nativeSwiftDirectMessagesNewMessage, locale: nativeUiLocale),
                    systemImage: "square.and.pencil"
                ) {
                    openNewMessageComposer()
                }
            }
            .padding(.horizontal, Spacing.md)

            List {
                ForEach(viewModel.conversations, id: \.id) { conversation in
                    Button {
                        Task { await openConversation(conversation.id) }
                    } label: {
                        VStack(alignment: .leading, spacing: Spacing.xs) {
                            Text(label(for: conversation))
                                .font(Typography.headline)
                            Text(UiMessages.date(
                                conversation.updatedAt,
                                date: .abbreviated,
                                time: .shortened,
                                locale: nativeUiLocale,
                                timeZone: .current
                            ))
                            .font(Typography.caption)
                            .foregroundStyle(Colors.secondaryLabel)
                        }
                    }
                }
                HybridPaginationControl(
                    hasMore: viewModel.hasMoreConversations,
                    isLoading: viewModel.isLoadingConversations,
                    hasError: viewModel.conversationLoadError != nil
                ) {
                    await viewModel.loadMoreConversations()
                }
            }
            .overlay {
                if viewModel.conversations.isEmpty {
                    EmptyStateView(
                        icon: "message",
                        title: .message(.nativeSwiftEmptyStateNoMessages),
                        message: .message(.nativeSwiftEmptyStateNoMessagesMessage)
                    )
                }
            }
        }
    }

    func label(for conversation: DirectConversation) -> String {
        let names = conversation.participantUsernames?.filter { !$0.isEmpty } ?? []
        return names.isEmpty ? conversation.title : names.joined(separator: ", ")
    }
}
