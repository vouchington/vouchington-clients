import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class HouseholdInvitationsViewModel {
    let service: any HouseholdInvitationServicing
    let householdId: String?
    var pagination = CursorPaginationState<HouseholdInvitation>()
    var username = ""
    var relationship = ""
    var busyIds: Set<String> = []
    var isSending = false
    var message: UiMessageKey?
    var dismissedIds: Set<String> = []

    var invitations: [HouseholdInvitation] {
        pagination.items.filter {
            !dismissedIds.contains($0.id) && $0.acceptedAt == nil && $0.declinedAt == nil && $0.revokedAt == nil
        }
    }

    init(service: any HouseholdInvitationServicing, householdId: String? = nil) {
        self.service = service
        self.householdId = householdId
    }

    func load() async {
        guard let request = pagination.beginInitialPageIfNeeded() else { return }
        await loadPage(request)
    }

    func loadNext() async {
        guard let request = pagination.beginNextPage() else { return }
        await loadPage(request)
    }

    private func loadPage(_ request: CursorPageRequest) async {
        do {
            let page: HouseholdInvitationPage = if let householdId {
                try await service.sent(householdId: householdId, after: request.cursor, limit: 25)
            } else {
                try await service.received(after: request.cursor, limit: 25)
            }
            pagination.complete(
                request,
                items: page.results,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
        } catch {
            if Task.isCancelled {
                pagination.cancel(request)
            } else {
                pagination.fail(request, error: error.asVouchaError)
            }
        }
    }

    func send() async {
        let username = username.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let householdId, !username.isEmpty, !isSending else { return }
        isSending = true
        message = nil
        defer { isSending = false }
        do {
            let relationship = relationship.trimmingCharacters(in: .whitespacesAndNewlines)
            let invitation = try await service.invite(
                householdId: householdId,
                username: username,
                relationship: relationship.isEmpty ? nil : relationship
            )
            self.username = ""
            self.relationship = ""
            message = .settingsHouseholdInvitationsInvitationSent
            pagination.reset()
            await load()
            pagination.replaceItems([invitation] + pagination.items)
        } catch {
            message = .settingsHouseholdInvitationsInvitationSendFailed
        }
    }

    enum Action {
        case accept, decline, revoke
    }

    func update(_ invitation: HouseholdInvitation, action: Action) async {
        guard invitations.contains(where: { $0.id == invitation.id }),
              (householdId != nil) == (action == .revoke),
              busyIds.insert(invitation.id).inserted
        else { return }
        message = nil
        defer { busyIds.remove(invitation.id) }
        do {
            switch action {
            case .accept:
                _ = try await service.accept(invitationId: invitation.id)
                message = .settingsHouseholdInvitationsInvitationAccepted
            case .decline:
                try await service.decline(invitationId: invitation.id)
                message = .settingsHouseholdInvitationsInvitationDeclined
            case .revoke:
                guard let householdId else { return }
                try await service.revoke(householdId: householdId, invitationId: invitation.id)
                message = .settingsHouseholdInvitationsInvitationRevoked
            }
            dismissedIds.insert(invitation.id)
        } catch {
            message = .settingsHouseholdInvitationsInvitationUpdateFailed
        }
    }
}
