import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension DirectMessagesSurface {
    var participantSection: some View {
        DisclosureGroup(UiMessages.string(.nativeSwiftDirectMessagesParticipants, locale: nativeUiLocale)) {
            ForEach(viewModel.participants, id: \.id) { participant in
                HStack {
                    VStack(alignment: .leading) {
                        Text(verbatim: UiMessages.string(
                            participant.username.map { .verbatim("@\($0)") }
                                ?? .app(UiMessage(.nativeSwiftDirectMessagesMember)),
                            locale: nativeUiLocale
                        ))
                        Text(verbatim: directMessageParticipantRoleLabel(
                            participant.role,
                            locale: nativeUiLocale
                        ))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                    }
                    Spacer()
                    participantRemovalButton(userId: participant.userId)
                }
            }
            if viewModel.canAddParticipants {
                participantSearchControls
            }
            if viewModel.isOwner {
                participantPolicyPicker
            }
        }
        .padding(.horizontal, Spacing.md)
    }

    @ViewBuilder
    private func participantRemovalButton(userId: String?) -> some View {
        if let userId,
           (viewModel.isOwner && userId != viewModel.currentUserId)
           || (!viewModel.isOwner && userId == viewModel.currentUserId) {
            Button(UiMessages.string(
                userId == viewModel.currentUserId
                    ? .nativeSwiftDirectMessagesLeave
                    : .nativeSwiftDirectMessagesRemove,
                locale: nativeUiLocale
            )) {
                Task {
                    await viewModel.removeParticipant(userId: userId)
                    if userId == viewModel.currentUserId, viewModel.selectedConversationId == nil {
                        await MainActor.run { returnToInbox() }
                    }
                }
            }
        }
    }

    private var participantSearchControls: some View {
        Group {
            TextField(UiMessages.string(.nativeSwiftDirectMessagesSearchUsers, locale: nativeUiLocale), text: Binding(
                get: { composeState.participantQuery },
                set: { composeState.participantQuery = $0 }
            ))
            .onSubmit { Task { await viewModel.searchParticipantUsers(query: composeState.participantQuery) } }
            Button(UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale)) {
                Task { await viewModel.searchParticipantUsers(query: composeState.participantQuery) }
            }
            ForEach(viewModel.participantUserResults, id: \.id) { user in
                Button(UiMessages.string(
                    .nativeSwiftDirectMessagesAddUser,
                    parameters: ["username": UiMessages.string(.verbatim(user.username), locale: nativeUiLocale)],
                    locale: nativeUiLocale
                )) {
                    Task { await viewModel.addParticipant(userId: user.id) }
                }
            }
        }
    }

    private var participantPolicyPicker: some View {
        Picker(
            UiMessages.string(.nativeSwiftDirectMessagesWhoCanAddParticipants, locale: nativeUiLocale),
            selection: Binding(
                get: { viewModel.participantAddPolicy },
                set: { policy in Task { await viewModel.updatePolicy(policy) } }
            )
        ) {
            Text(UiMessages.string(.nativeSwiftDirectMessagesOwnerOnly, locale: nativeUiLocale))
                .tag(ConversationParticipantAddPolicy.ownerOnly)
            Text(UiMessages.string(.nativeSwiftDirectMessagesAllMembers, locale: nativeUiLocale))
                .tag(ConversationParticipantAddPolicy.allMembers)
        }
    }
}

func directMessageParticipantRoleLabel(_ role: String, locale: Locale) -> String {
    UiMessages.string(communityMemberRoleText(role), locale: locale)
}
