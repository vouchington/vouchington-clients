import Foundation
import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class ReviewDisputesViewModel {
    static let annotationBodyUTF16Limit = 2_000

    let client: APIClient?
    let isSignedIn: Bool
    let isAdministrator: Bool
    let isSiteModerator: Bool
    let rerunPollAttempts: Int
    let rerunPollDelay: @Sendable () async throws -> Void

    var disputePagination = CursorPaginationState<ReviewDispute>()
    var disputes: [ReviewDispute] {
        get { disputePagination.items }
        set { disputePagination.replaceItems(newValue) }
    }

    var isLoading = false
    var mutatingDisputeId: String?
    var errorMessage: UiVerbatimText?
    var loadMoreErrorMessage: UiVerbatimText?
    var ambiguousDisputeIds: Set<String> = []
    var ambiguousAIRerunBaselines: [String: AIRerunBaseline] = [:]
    var selectedStatus: ReviewDisputeStatus = .pending
    var publicResponseDrafts: [String: String] = [:]
    var annotationDrafts: [String: String] = [:]
    var locallyEditedPublicResponseIds: Set<String> = []
    var loadRevision = 0
    var targetedOutcomeRevision = 0
    var rerunTasks: [String: Task<Void, Never>] = [:]
    var rerunTaskGenerations: [String: Int] = [:]
    var rerunTaskGeneration = 0

    init(
        client: APIClient?,
        isSignedIn: Bool,
        isAdministrator: Bool,
        isSiteModerator: Bool,
        rerunPollAttempts: Int = 20,
        rerunPollDelay: @escaping @Sendable () async throws -> Void = {
            try await Task.sleep(for: .seconds(3))
        }
    ) {
        self.client = client
        self.isSignedIn = isSignedIn
        self.isAdministrator = isAdministrator
        self.isSiteModerator = isSiteModerator
        self.rerunPollAttempts = rerunPollAttempts
        self.rerunPollDelay = rerunPollDelay
    }

    var canAccess: Bool {
        isSignedIn && (isAdministrator || isSiteModerator)
    }

    var isMutating: Bool {
        mutatingDisputeId != nil
    }

    func canApprove(_ dispute: ReviewDispute) -> Bool {
        canMutate(dispute) && dispute.approvedAt == nil && dispute.sentAt == nil
            && !publicDraft(for: dispute).trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    func canDeliver(_ dispute: ReviewDispute) -> Bool {
        canMutate(dispute) && dispute.approvedAt != nil && dispute.sentAt == nil
            && !ambiguousDisputeIds.contains(dispute.id)
            && publicDraft(for: dispute) == (dispute.publicResponse ?? "")
    }

    func canResolve(_ dispute: ReviewDispute, action: ReviewDisputeResolutionAction) -> Bool {
        guard canMutate(dispute), dispute.sentAt != nil,
              !ambiguousDisputeIds.contains(dispute.id)
        else { return false }
        return action != .annotate || annotationBody(for: dispute) != nil
    }

    func canRerun(_ dispute: ReviewDispute) -> Bool {
        canMutate(dispute) && dispute.approvedAt == nil && dispute.sentAt == nil
    }

    func canEdit(_ dispute: ReviewDispute) -> Bool {
        canMutate(dispute) && dispute.sentAt == nil
    }

    func canMutate(_ dispute: ReviewDispute) -> Bool {
        canAccess && dispute.status == .pending && !isMutating
            && !ambiguousDisputeIds.contains(dispute.id)
    }

    func startRerunAI(for dispute: ReviewDispute) {
        cancelRerunAI(for: dispute.id)
        rerunTaskGeneration += 1
        let generation = rerunTaskGeneration
        rerunTaskGenerations[dispute.id] = generation
        rerunTasks[dispute.id] = Task { [weak self] in
            guard let self else { return }
            await rerunAI(for: dispute)
            clearRerunTask(for: dispute.id, generation: generation)
        }
    }

    func cancelRerunAI(for disputeId: String) {
        rerunTaskGenerations.removeValue(forKey: disputeId)
        rerunTasks.removeValue(forKey: disputeId)?.cancel()
    }

    private func clearRerunTask(for disputeId: String, generation: Int) {
        guard rerunTaskGenerations[disputeId] == generation else { return }
        rerunTaskGenerations.removeValue(forKey: disputeId)
        rerunTasks.removeValue(forKey: disputeId)
    }
}

struct AIRerunBaseline {
    let aiDraftedAt: Date?
    let lifecycleChangeId: String?

    init(dispute: ReviewDispute) {
        aiDraftedAt = dispute.aiDraftedAt
        lifecycleChangeId = dispute.latestLifecycleChangeId
    }

    func hasAdvanced(in dispute: ReviewDispute) -> Bool {
        dispute.aiDraftedAt != nil
            && dispute.aiDraftedAt != aiDraftedAt
            && dispute.latestLifecycleChangeId != lifecycleChangeId
    }

    func isReconciled(in dispute: ReviewDispute) -> Bool {
        hasAdvanced(in: dispute)
            || dispute.approvedAt != nil
            || dispute.sentAt != nil
            || dispute.status != .pending
    }
}
