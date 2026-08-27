import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeChatSurface {
    func chatSidebar(viewModel: NativeChatViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            HStack {
                Text(UiMessages.string(.nativeSwiftChatChats, locale: nativeUiLocale))
                    .font(Typography.headline)
                Spacer()
                Button {
                    Task { await viewModel.startNewConversation() }
                } label: {
                    Label(UiMessages.string(.nativeSwiftChatNew, locale: nativeUiLocale), systemImage: "plus")
                }
                .buttonStyle(.bordered)
            }

            if viewModel.isLoadingList,
               viewModel.conversations.isEmpty {
                ProgressView()
                    .frame(maxWidth: .infinity, alignment: .center)
                    .padding(.vertical, Spacing.xl)
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                        ForEach(viewModel.conversations) { conversation in
                            chatConversationButton(conversation, viewModel: viewModel)
                        }

                        HybridPaginationControl(
                            hasMore: viewModel.conversationPagination.hasMore,
                            isLoading: viewModel.conversationPagination.isLoading,
                            hasError: viewModel.conversationPagination.lastError != nil
                        ) {
                            await viewModel.loadMoreConversations()
                        }
                    }
                }
            }

            if let message = viewModel.listErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
        .frame(width: 280, alignment: .topLeading)
    }

    private func chatConversationButton(
        _ conversation: ChatConversation,
        viewModel: NativeChatViewModel
    ) -> some View {
        Button {
            Task { await viewModel.selectConversation(id: conversation.id) }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(UiMessages.string(chatConversationTitle(conversation), locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.primary)
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
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(Spacing.sm)
            .background(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(viewModel.selectedConversationId == conversation.id ? Colors.primary.opacity(0.15) : .clear)
            )
        }
        .buttonStyle(.plain)
    }

    private func chatConversationTitle(_ conversation: ChatConversation) -> UiVerbatimText {
        let trimmed = conversation.title.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? .message(.nativeSwiftChatNewConversation) : .verbatim(trimmed)
    }
}
