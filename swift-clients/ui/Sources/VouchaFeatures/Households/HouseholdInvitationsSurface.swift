import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct HouseholdInvitationsSurface: View {
    @Environment(\.locale)
    private var locale
    private let onAccepted: () async -> Void
    @State
    var viewModel: HouseholdInvitationsViewModel

    init(client: APIClient, householdId: String? = nil, onAccepted: @escaping () async -> Void = {}) {
        self.onAccepted = onAccepted
        _viewModel = State(initialValue: HouseholdInvitationsViewModel(
            service: HouseholdInvitationService(client: client), householdId: householdId
        ))
    }

    init(viewModel: HouseholdInvitationsViewModel, onAccepted: @escaping () async -> Void = {}) {
        self.onAccepted = onAccepted
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(copy(viewModel.householdId == nil
                    ? .settingsHouseholdInvitationsReceivedTitle : .settingsHouseholdInvitationsSentTitle))
                .font(Typography.headline)
            if viewModel.householdId != nil { invitationForm }
            if viewModel.pagination.isLoading { ProgressView() }
            if viewModel.pagination.lastError != nil {
                Text(copy(.settingsHouseholdInvitationsLoadFailed))
                Button(copy(.settingsHouseholdInvitationsRetry)) { Task { await viewModel.loadNext() } }
                    .accessibilityLabel(copy(.settingsHouseholdInvitationsRetryLoading))
            } else if viewModel.pagination.hasLoadedPage, viewModel.invitations.isEmpty {
                Text(copy(.settingsHouseholdInvitationsNoPendingInvitations))
            }
            ForEach(viewModel.invitations) { invitation in row(invitation) }
            if viewModel.pagination.hasMore, viewModel.pagination.hasLoadedPage {
                Button(copy(.settingsHouseholdInvitationsLoadMore)) { Task { await viewModel.loadNext() } }
                    .disabled(viewModel.pagination.isLoading)
            }
            if let message = viewModel.message { Text(copy(message)) }
        }
        .task { await viewModel.load() }
    }

    private var invitationForm: some View {
        @Bindable
        var viewModel = viewModel
        return VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(copy(.settingsHouseholdInvitationsSearchByUsername))
            TextField(copy(.settingsHouseholdInvitationsUsername), text: $viewModel.username)
            TextField(copy(.settingsHouseholdInvitationsRelationshipOptional), text: $viewModel.relationship)
            Button(copy(.settingsHouseholdInvitationsSendInvitation)) { Task { await viewModel.send() } }
                .disabled(viewModel.isSending || viewModel.username.trimmingCharacters(in: .whitespacesAndNewlines)
                    .isEmpty)
        }
    }

    private func row(_ invitation: HouseholdInvitation) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(summary(invitation))
            if let relationship = invitation.relationship, !relationship.isEmpty {
                Text(UiMessages.string(.userContent(relationship), locale: locale))
            }
            HStack {
                if viewModel.householdId != nil {
                    Button(copy(.settingsHouseholdInvitationsRevokeInvitation)) {
                        Task { await viewModel.update(invitation, action: .revoke) }
                    }
                } else {
                    Button(copy(.settingsHouseholdInvitationsAccept)) {
                        Task {
                            await viewModel.update(invitation, action: .accept)
                            if viewModel.dismissedIds.contains(invitation.id) { await onAccepted() }
                        }
                    }
                    Button(copy(.settingsHouseholdInvitationsDecline)) {
                        Task { await viewModel.update(invitation, action: .decline) }
                    }
                }
            }
            .disabled(viewModel.busyIds.contains(invitation.id))
        }
    }

    private func summary(_ invitation: HouseholdInvitation) -> String {
        if viewModel.householdId != nil {
            return UiMessages.string(
                .settingsHouseholdInvitationsSentFor,
                parameters: ["invitee": label(invitation.invitedUser)],
                locale: locale
            )
        }
        let owner = label(invitation.householdOwner)
        if invitation.invitedById == invitation.householdOwner.id {
            return UiMessages.string(
                .settingsHouseholdInvitationsReceivedFromOwner,
                parameters: ["owner": owner],
                locale: locale
            )
        }
        return UiMessages.string(
            .settingsHouseholdInvitationsReceivedFromOther,
            parameters: ["owner": owner, "inviter": label(invitation.invitedBy)],
            locale: locale
        )
    }

    private func label(_ user: HouseholdInvitationUser) -> String {
        guard let username = user.username?.trimmingCharacters(in: .whitespacesAndNewlines), !username.isEmpty else {
            return copy(.settingsHouseholdInvitationsMemberFallback)
        }
        return UiMessages.string(.userContent("@\(username)"), locale: locale)
    }

    private func copy(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: locale)
    }
}
