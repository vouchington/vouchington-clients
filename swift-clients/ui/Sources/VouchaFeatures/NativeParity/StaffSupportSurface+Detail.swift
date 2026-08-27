import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension StaffSupportSurface {
    @ViewBuilder
    func staffSupportDetail(
        _ viewModel: StaffSupportViewModel,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in }
    ) -> some View {
        switch viewModel.mode {
        case .threads:
            if let thread = viewModel.selectedThread {
                staffThreadDetail(viewModel, thread: thread)
            }
        case .contacts:
            if let contact = viewModel.selectedContact {
                staffContactDetail(viewModel, contact: contact, onNavigateToTargetPath: onNavigateToTargetPath)
            }
        }
    }

    private func staffThreadDetail(_ viewModel: StaffSupportViewModel, thread: SupportThread) -> some View {
        let canReply = thread.status == .open || thread.status == .assigned
        return ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                staffThreadHeader(viewModel, thread: thread)
                if viewModel.messagePageInfo?.hasNextPage == true {
                    Button {
                        Task { await viewModel.loadOlderMessages() }
                    } label: {
                        Text(UiMessages.string(.nativeSwiftCommonLoadMore, locale: nativeUiLocale))
                    }
                }
                ForEach(viewModel.messages) { message in
                    staffMessage(viewModel, message: message, canMutateDraft: canReply)
                }
                if canReply {
                    if viewModel.canGenerateDraft {
                        Button {
                            Task { await viewModel.generateDraft() }
                        } label: {
                            Text(UiMessages.string(
                                viewModel.isWaitingForDraft
                                    ? .nativeSwiftSupportWaitingForAiDraft
                                    : .nativeSwiftSupportGenerateAiDraft,
                                locale: nativeUiLocale
                            ))
                        }
                        .disabled(viewModel.isWaitingForDraft || viewModel.isMutating)
                    }
                    staffReplyComposer(viewModel)
                }
            }
        }
    }

    private func staffThreadHeader(_ viewModel: StaffSupportViewModel, thread: SupportThread) -> some View {
        HStack {
            VStack(alignment: .leading) {
                Text(thread.subject).font(Typography.headline)
                Text(UiMessages.string(supportThreadStatusText(thread.status), locale: nativeUiLocale))
                    .foregroundStyle(Colors.secondaryLabel)
            }
            Spacer()
            if thread.status == .open, thread.assignedToId != viewModel.administratorId {
                Button {
                    Task { await viewModel.assignToMe() }
                } label: {
                    Text(UiMessages.string(.nativeSwiftSupportAssignToMe, locale: nativeUiLocale))
                }
                .disabled(viewModel.isMutating)
            }
            if thread.status == .resolved {
                Button {
                    pendingConfirmation = .setResolved(false)
                } label: {
                    Text(UiMessages.string(.nativeSwiftCommunityActionsReopen, locale: nativeUiLocale))
                }
                .disabled(viewModel.isMutating)
            } else if thread.status == .open || thread.status == .assigned {
                Button {
                    pendingConfirmation = .setResolved(true)
                } label: {
                    Text(UiMessages.string(.nativeSwiftCommunityActionsResolve, locale: nativeUiLocale))
                }
                .disabled(viewModel.isMutating)
            }
        }
    }

    private func staffReplyComposer(_ viewModel: StaffSupportViewModel) -> some View {
        Group {
            TextEditor(text: Binding(get: { viewModel.replyText }, set: { viewModel.replyText = $0 }))
                .frame(minHeight: 90)
                .disabled(viewModel.isMutating)
            Text(UiMessages.string(.nativeSwiftSupportSavedReplyNotEmailed, locale: nativeUiLocale))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            Button {
                Task { await viewModel.saveOutboundReply() }
            } label: {
                Text(UiMessages.string(.nativeSwiftSupportSaveOutboundReply, locale: nativeUiLocale))
            }
            .disabled(
                viewModel.replyText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || viewModel.isMutating
            )
        }
    }

    private func staffMessage(
        _ viewModel: StaffSupportViewModel,
        message: SupportMessage,
        canMutateDraft: Bool
    ) -> some View {
        let senderKey: UiMessageKey = message.direction == .inbound
            ? .nativeSwiftSupportCustomer
            : .nativeSwiftSupportTeam

        return VStack(alignment: .leading, spacing: Spacing.xs) {
            staffMessageHeader(senderKey: senderKey, createdAt: message.createdAt)
            if message.draftedAt != nil, message.approvedAt == nil, message.sentAt == nil, canMutateDraft {
                TextField(
                    UiMessages.string(.nativeSwiftCommonMessage, locale: nativeUiLocale),
                    text: Binding(
                        get: { viewModel.draftText(for: message) },
                        set: { viewModel.setDraftText($0, messageId: message.id) }
                    ),
                    axis: .vertical
                )
                HStack {
                    Button {
                        Task { await viewModel.saveDraft(message) }
                    } label: {
                        Text(UiMessages.string(.nativeSwiftCommonSave, locale: nativeUiLocale))
                    }
                    .disabled(viewModel.isMutating)
                    Button {
                        pendingConfirmation = .approve(message)
                    } label: {
                        Text(UiMessages.string(.nativeSwiftCommonApprove, locale: nativeUiLocale))
                    }
                    .disabled(viewModel.isMutating || viewModel.isDraftDirty(message))
                }
            } else {
                Text(message.bodyText)
                if message.draftedAt != nil, message.approvedAt != nil, message.sentAt == nil, canMutateDraft {
                    Button {
                        pendingConfirmation = .send(message)
                    } label: {
                        Text(UiMessages.string(.nativeSwiftCommonSend, locale: nativeUiLocale))
                    }
                    .disabled(viewModel.isMutating)
                }
            }
        }
        .padding(Spacing.sm)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(message.direction == .inbound ? Colors.background : Colors.primary.opacity(0.12))
        )
    }

}
