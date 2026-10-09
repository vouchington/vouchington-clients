import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativeChatSurface {
    func chatDetail(viewModel: NativeChatViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            header(viewModel: viewModel)

            if let message = viewModel.detailErrorMessage ?? viewModel.streamErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.negativeVote)
            }

            ScrollView {
                LazyVStack(alignment: .leading, spacing: Spacing.md) {
                    if viewModel.canLoadOlderMessages {
                        Button(
                            UiMessages.string(
                                .nativeSwiftRebasedRouteSurfacesLoadOlderMessages,
                                locale: nativeUiLocale
                            ),
                            systemImage: "arrow.up.circle"
                        ) {
                            Task { await viewModel.loadOlderMessages() }
                        }
                        .disabled(viewModel.isLoadingOlderMessages)
                    }
                    ForEach(viewModel.messages) { message in
                        NativeChatMessageBubbleView(message: message)
                    }
                }
            }

            composer(viewModel: viewModel)
        }
    }

    private func header(viewModel: NativeChatViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return HStack(alignment: .firstTextBaseline, spacing: Spacing.sm) {
            VStack(alignment: .leading, spacing: 4) {
                Text(UiMessages.string(viewModel.selectedConversationTitle, locale: nativeUiLocale))
                    .font(Typography.headline)
                Text(UiMessages.string(
                    viewModel.selectedConversationId.map { .verbatim($0) }
                        ?? .message(.nativeSwiftChatNewConversation),
                    locale: nativeUiLocale
                ))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            }

            Spacer(minLength: 0)

            if viewModel.selectedConversationId != nil {
                selectedConversationHeaderControls(viewModel: viewModel)
            } else {
                providerPicker(viewModel: viewModel)

                Button {
                    Task { await viewModel.startNewConversation() }
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftChatReset, locale: nativeUiLocale),
                        systemImage: "arrow.counterclockwise"
                    )
                }
                .buttonStyle(.bordered)
            }
        }
    }

    private func selectedConversationHeaderControls(viewModel: NativeChatViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Group {
            TextField(
                UiMessages.string(.nativeSwiftChatConversationTitle, locale: nativeUiLocale),
                text: $viewModel.conversationTitleDraft
            )
            .textFieldStyle(.roundedBorder)
            .frame(width: 220)

            providerPicker(viewModel: viewModel)
            generateTitleButton(viewModel: viewModel)

            Button {
                Task { await viewModel.renameSelectedConversation() }
            } label: {
                Label(UiMessages.string(.nativeSwiftChatRename, locale: nativeUiLocale), systemImage: "pencil")
            }
            .buttonStyle(.bordered)

            Button(role: .destructive) {
                Task { await viewModel.deleteSelectedConversation() }
            } label: {
                Label(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), systemImage: "trash")
            }
            .buttonStyle(.bordered)
        }
    }

    private func providerPicker(viewModel: NativeChatViewModel) -> some View {
        Group {
            Picker(
                UiMessages.string(.nativeSwiftPresentationProvider, locale: nativeUiLocale),
                selection: Binding(
                    get: { viewModel.titleProviderSelection },
                    set: { viewModel.selectTitleProvider($0) }
                )
            ) {
                ForEach(viewModel.titleProviderResolver.providerDescriptors()) { provider in
                    Text(UiMessages.string(provider.displayName, locale: nativeUiLocale)).tag(provider.selection)
                }
            }
            .pickerStyle(.menu)
            .disabled(viewModel.isSendingDraft || viewModel.isStreaming)
            Text(UiMessages.string(viewModel.titleProviderSelection.detailText, locale: nativeUiLocale))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)

            if let detail = viewModel.titleProviderResolver.providerDescriptors()
                .first(where: { $0.selection == viewModel.titleProviderSelection })?.status.detail {
                Text(UiMessages.string(detail, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }

    @ViewBuilder
    private func generateTitleButton(viewModel: NativeChatViewModel) -> some View {
        if viewModel.conversationTitleDraft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            Button {
                Task {
                    await viewModel.generateTitleIfNeeded(
                        conversationId: viewModel.selectedConversationId ?? ""
                    )
                }
            } label: {
                Label(UiMessages.string(.nativeSwiftChatGenerateTitle, locale: nativeUiLocale), systemImage: "sparkles")
            }
            .buttonStyle(.bordered)
        }
    }

    private func composer(viewModel: NativeChatViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return VStack(alignment: .leading, spacing: Spacing.sm) {
            TextEditor(text: $viewModel.draftMessage)
                .frame(minHeight: 112)
                .overlay(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .strokeBorder(.quaternary, lineWidth: 1)
                )

            HStack {
                Button {
                    Task { await viewModel.sendDraftMessage() }
                } label: {
                    Label(UiMessages.string(.nativeSwiftCommonSend, locale: nativeUiLocale), systemImage: "paperplane")
                }
                .buttonStyle(.borderedProminent)
                .disabled(
                    viewModel.draftMessage
                        .trimmingCharacters(in: .whitespacesAndNewlines)
                        .isEmpty
                        || viewModel.isSendingDraft
                        || viewModel.isStreaming
                        || viewModel.isLoadingDetail
                )

                Button {
                    viewModel.abortStreaming()
                } label: {
                    Label(UiMessages.string(.nativeSwiftChatStop, locale: nativeUiLocale), systemImage: "stop.fill")
                }
                .buttonStyle(.bordered)
                .disabled(
                    !viewModel.isStreaming
                )

                if viewModel.isGeneratingTitle {
                    ProgressView()
                        .controlSize(.small)
                }
            }
        }
    }
}
