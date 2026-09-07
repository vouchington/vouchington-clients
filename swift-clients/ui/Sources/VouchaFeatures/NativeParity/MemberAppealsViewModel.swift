import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

enum MemberAppealSubmissionState: Equatable {
    case idle
    case submitting
    case succeeded(isDuplicate: Bool)
    case failed(UiVerbatimText)
}

enum MemberAppealInitialStream: Hashable {
    case pendingAppeals
    case resolvedAppeals
    case dismissedAppeals
    case warnings
    case bans
    case removedPosts
    case identity
}

@Observable
@MainActor
final class MemberAppealsViewModel {
    let client: APIClient?
    let isSignedIn: Bool
    let currentUserId: String?
    let route: MemberAppealsRoute
    let draftStore: MemberAppealDraftStore

    var pendingAppealPagination = CursorPaginationState<ModerationAppeal>()
    var resolvedAppealPagination = CursorPaginationState<ModerationAppeal>()
    var dismissedAppealPagination = CursorPaginationState<ModerationAppeal>()
    var warningPagination = CursorPaginationState<MemberWarningNotice>()
    var banPagination = CursorPaginationState<MemberCommunityBanNotice>()
    var removalPagination = CursorPaginationState<MemberRemovedPostNotice>()
    var suspensionDate: Date?
    var activeTarget: MemberAppealTarget?
    var isPresentingTurnstile = false
    var turnstileToken: String?
    var submissionState: MemberAppealSubmissionState = .idle
    var isLoading = false
    var errorMessage: UiVerbatimText?
    var loadMoreErrorMessage: UiVerbatimText?
    var failedInitialStreams: Set<MemberAppealInitialStream> = []
    var activeSubmissionRevision: Int?
    var submissionRevision = 0

    init(
        client: APIClient?,
        isSignedIn: Bool,
        currentUserId: String? = nil,
        route: MemberAppealsRoute,
        draftStore: MemberAppealDraftStore? = nil
    ) {
        self.client = client
        self.isSignedIn = isSignedIn
        self.currentUserId = currentUserId
        self.route = route
        self.draftStore = draftStore ?? .session
    }

    var appeals: [ModerationAppeal] {
        let values = pendingAppealPagination.items
            + resolvedAppealPagination.items
            + dismissedAppealPagination.items
        var seen = Set<String>()
        return values
            .filter { seen.insert($0.id).inserted }
            .sorted { $0.createdAt > $1.createdAt }
    }

    var eligibleTargets: [MemberAppealTarget] {
        switch route {
        case .tracking:
            activeWarnings.map(\.appealTarget)
                + banPagination.items.map(\.appealTarget)
                + removalPagination.items.map(\.appealTarget)
                + suspensionTargets
        case .warnings:
            activeWarnings.map(\.appealTarget)
        case .bans:
            banPagination.items.map(\.appealTarget)
        case .removedPosts:
            removalPagination.items.map(\.appealTarget)
        case .suspension:
            suspensionTargets
        }
    }

    var activeDraft: MemberAppealDraft {
        guard let activeTarget else { return MemberAppealDraft() }
        guard let currentUserId else { return MemberAppealDraft() }
        return draftStore.draft(for: activeTarget, currentUserId: currentUserId)
    }

    var isSubmitting: Bool {
        activeSubmissionRevision != nil
    }

    func appealPagination(status: ModerationAppealStatus) -> CursorPaginationState<ModerationAppeal> {
        switch status {
        case .pending: pendingAppealPagination
        case .resolved: resolvedAppealPagination
        case .dismissed: dismissedAppealPagination
        }
    }

    var submissionMessage: UiVerbatimText? {
        switch submissionState {
        case .idle, .submitting:
            nil
        case let .succeeded(isDuplicate):
            .message(
                isDuplicate
                    ? .nativeSwiftModerationAppealsMemberPendingExists
                    : .nativeSwiftModerationAppealsMemberSubmitted
            )
        case let .failed(message):
            message
        }
    }

    func canAppeal(_ target: MemberAppealTarget) -> Bool {
        pendingAppealsAreReconciled
            && !isSubmitting
            && !appeals.contains { $0.status == .pending && $0.matches(target) }
    }

    func beginAppeal(_ target: MemberAppealTarget) {
        guard canAppeal(target) else { return }
        activeTarget = target
        isPresentingTurnstile = false
        turnstileToken = nil
        submissionState = .idle
    }

    func cancelAppeal() {
        activeTarget = nil
        isPresentingTurnstile = false
        turnstileToken = nil
        submissionState = .idle
        submissionRevision += 1
    }

    func setReason(_ reason: ModerationAppealReason?) {
        guard let activeTarget else { return }
        guard let currentUserId else { return }
        draftStore.setReason(reason, for: activeTarget, currentUserId: currentUserId)
        submissionState = .idle
    }

    func setDetails(_ details: String) {
        guard let activeTarget else { return }
        guard let currentUserId else { return }
        draftStore.setDetails(details, for: activeTarget, currentUserId: currentUserId)
        submissionState = .idle
    }

    private var suspensionTargets: [MemberAppealTarget] {
        suspensionDate.map { [.suspension(date: $0)] } ?? []
    }

    private var activeWarnings: [MemberWarningNotice] {
        warningPagination.items.filter { $0.revokedAt == nil }
    }

    private var pendingAppealsAreReconciled: Bool {
        pendingAppealPagination.hasLoadedPage
            && !pendingAppealPagination.isLoading
            && !pendingAppealPagination.hasMore
            && pendingAppealPagination.lastError == nil
    }
}

private extension ModerationAppeal {
    func matches(_ target: MemberAppealTarget) -> Bool {
        switch target {
        case let .warning(id, _, _, _): return userWarningId == id
        case let .ban(id, _, _, _): return communityBanId == id
        case let .removal(id, _, _, _, _, kind, _):
            return postId == id && postRemovalKind == kind
        case let .suspension(date):
            guard let targetContext else { return userSuspensionId != nil }
            guard let userSuspensionId, case let .suspension(context) = targetContext else { return false }
            return context.id == userSuspensionId && context.createdAt == date
        }
    }
}
