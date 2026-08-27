import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeSupportSurface {
    func supportDetail(viewModel: NativeSupportViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            supportDetailHeader(viewModel: viewModel)

            if let message = viewModel.detailErrorMessage ?? viewModel.createErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.negativeVote)
            }

            if viewModel.selectedThreadId == nil {
                supportCreateForm(viewModel: viewModel)
            } else {
                supportMessages(viewModel: viewModel)
            }
        }
    }

    private func supportDetailHeader(viewModel: NativeSupportViewModel) -> some View {
        let subject: UiVerbatimText = if let selectedSubject = viewModel.selectedThread?.subject {
            .verbatim(selectedSubject)
        } else {
            .message(.nativeSwiftSupportNewSupportRequest)
        }

        return HStack(alignment: .firstTextBaseline) {
            VStack(alignment: .leading, spacing: 4) {
                Text(UiMessages.string(subject, locale: nativeUiLocale))
                    .font(Typography.headline)
                Text(UiMessages.string(supportStatus(viewModel.selectedThread?.status), locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
            Spacer(minLength: 0)
            Button {
                Task { await viewModel.startNewThread() }
            } label: {
                Label(
                    UiMessages.string(.nativeSwiftSupportNewRequest, locale: nativeUiLocale),
                    systemImage: "plus.message"
                )
            }
            .buttonStyle(.bordered)
        }
    }

    private func supportMessages(viewModel: NativeSupportViewModel) -> some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: Spacing.md) {
                ForEach(viewModel.selectedThreadMessages) { message in
                    supportMessageBubble(message)
                }
                if viewModel.isLoadingDetail {
                    ProgressView()
                        .frame(maxWidth: .infinity, alignment: .center)
                        .padding(.vertical, Spacing.sm)
                } else if viewModel.canLoadMoreSelectedThreadMessages {
                    Button {
                        Task { await viewModel.loadMoreSelectedThreadMessages() }
                    } label: {
                        Label(
                            UiMessages.string(.nativeSwiftCommonLoadMore, locale: nativeUiLocale),
                            systemImage: "arrow.down"
                        )
                    }
                    .buttonStyle(.bordered)
                }
            }
        }
    }

    private func supportCreateForm(viewModel: NativeSupportViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return VStack(alignment: .leading, spacing: Spacing.md) {
            TextField(
                UiMessages.string(.nativeSwiftCrmContactsSubject, locale: nativeUiLocale),
                text: $viewModel.subject
            )
            .textFieldStyle(.roundedBorder)

            TextField(
                UiMessages.string(.nativeSwiftCommunitiesConversationId, locale: nativeUiLocale),
                text: $viewModel.conversationId
            )
            .textFieldStyle(.roundedBorder)

            TextEditor(text: $viewModel.message)
                .frame(minHeight: 140)
                .overlay(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .strokeBorder(.quaternary, lineWidth: 1)
                )

            HStack {
                Button {
                    Task { await viewModel.createThread() }
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftSupportCreateRequest, locale: nativeUiLocale),
                        systemImage: "paperplane"
                    )
                }
                .buttonStyle(.borderedProminent)
                .disabled(
                    viewModel.subject
                        .trimmingCharacters(in: .whitespacesAndNewlines)
                        .isEmpty
                        || viewModel.isCreating
                )

                if viewModel.isCreating {
                    ProgressView()
                        .controlSize(.small)
                }
            }
        }
    }

    private func supportMessageBubble(_ message: SupportMessage) -> some View {
        let senderKey: UiMessageKey = message.direction == .inbound
            ? .nativeSwiftSupportCustomer
            : .nativeSwiftSupportTeam

        return VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack {
                Text(UiMessages.string(senderKey, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                Spacer(minLength: 0)
                Text(UiMessages.date(
                    message.createdAt,
                    date: .abbreviated,
                    time: .shortened,
                    locale: nativeUiLocale,
                    timeZone: .current
                ))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            }

            Text(message.bodyText)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(Spacing.sm)
                .background(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .fill(message.direction == .inbound ? Colors.background : Colors.primary.opacity(0.12))
                )
        }
    }

    private func supportStatus(_ status: SupportThreadStatus?) -> UiVerbatimText {
        switch status {
        case .open:
            .message(.nativeSwiftSupportOpen)
        case .resolved:
            .message(.nativeSwiftRouteSurfaceResolved)
        case .assigned:
            .message(.nativeSwiftPresentationValuesAssigned)
        case .closed:
            .message(.nativeSwiftRouteSurfaceClosed)
        case .none:
            .message(.nativeSwiftSupportDraft)
        }
    }
}
