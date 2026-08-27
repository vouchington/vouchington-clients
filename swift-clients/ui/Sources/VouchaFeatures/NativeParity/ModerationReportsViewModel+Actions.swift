import VouchaAPI
import VouchaLocalization
import VouchaModels

public enum ModerationReportAction: Sendable {
    case review
    case dismiss
    case rerunJudgement
    case warn(reason: String, publicMessage: String?)
    case confirmBanEvasion
    case dismissBanEvasion
    case removeTarget

    var refreshesGroupedQueue: Bool {
        if case .rerunJudgement = self {
            return false
        }
        return true
    }
}

extension ModerationReportsViewModel {
    public func perform(_ action: ModerationReportAction, reportId: String) async {
        guard viewerTier.isStaff,
              let client,
              let report = report(id: reportId),
              !inFlightReportIds.contains(reportId)
        else { return }
        guard actionIsAllowed(action, for: report) else {
            actionErrors[reportId] = .message(.nativeSwiftModerationReportsActionUnavailable)
            return
        }
        guard beginQueueMutation() else { return }
        defer { isMutatingQueue = false }
        inFlightReportIds.insert(reportId)
        actionErrors[reportId] = nil
        defer { inFlightReportIds.remove(reportId) }
        do {
            try await execute(action, for: report, client: client)
            await refreshGroupedQueueIfNeeded(after: action)
        } catch {
            actionErrors[reportId] = message(for: error)
        }
    }

    private func execute(
        _ action: ModerationReportAction,
        for report: StaffModerationReport,
        client: APIClient
    ) async throws {
        switch action {
        case .review:
            try await resolve(report, as: .reviewed, client: client)
        case .dismiss:
            try await resolve(report, as: .dismissed, client: client)
        case .rerunJudgement:
            let _: ModerationReportJudgementResponse = try await client.send(
                .rerunModerationReportJudgement(reportId: report.id)
            )
        case let .warn(reason, publicMessage):
            try await warn(report, reason: reason, publicMessage: publicMessage, client: client)
        case .confirmBanEvasion:
            try await decideBanEvasion(report, confirm: true, client: client)
        case .dismissBanEvasion:
            try await decideBanEvasion(report, confirm: false, client: client)
        case .removeTarget:
            try await removeTarget(report, client: client)
        }
    }

    private func refreshGroupedQueueIfNeeded(after action: ModerationReportAction) async {
        guard mode == .grouped, action.refreshesGroupedQueue else { return }
        await refreshGroupedAfterActions()
    }

    func actionIsAllowed(_ action: ModerationReportAction, for report: StaffModerationReport) -> Bool {
        guard viewerTier.isStaff else {
            return false
        }
        if case .rerunJudgement = action {
            return true
        }
        guard report.status == .pending else {
            return false
        }
        return pendingActionIsAllowed(action, for: report)
    }

    private func pendingActionIsAllowed(
        _ action: ModerationReportAction,
        for report: StaffModerationReport
    ) -> Bool {
        switch action {
        case .review, .dismiss:
            !report.isSystemGenerated
        case .rerunJudgement:
            true
        case let .warn(reason, _):
            report.targetUserId != nil && !reason.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                && !report.isSystemGenerated
        case .confirmBanEvasion, .dismissBanEvasion:
            report.communityBanEvasion != nil && report.isSystemGenerated
        case .removeTarget:
            viewerTier.canRemove && (report.entityType == "post" || report.entityType == "comment")
                && !report.isSystemGenerated
        }
    }

    func resolve(
        _ report: StaffModerationReport,
        as status: ModerationReportResolution,
        client: APIClient
    ) async throws {
        let _: ModerationReportResolutionResponse = try await client.send(
            .resolveModerationReport(reportId: report.id, status: status)
        )
        evict([report.id])
    }

    func warn(
        _ report: StaffModerationReport,
        reason: String,
        publicMessage: String?,
        client: APIClient
    ) async throws {
        guard let targetUserId = report.targetUserId else {
            throw ModerationReportLocalError.missingTarget
        }
        let endpoint = try Endpoint.issueAdminWarning(
            userId: targetUserId,
            reason: reason,
            publicMessage: publicMessage?.nilIfBlank,
            reportId: report.id
        )
        let _: AdminUserWarningResponse = try await client.send(endpoint)
        evict([report.id])
    }

    func decideBanEvasion(
        _ report: StaffModerationReport,
        confirm: Bool,
        client: APIClient
    ) async throws {
        guard let context = report.communityBanEvasion else {
            throw ModerationReportLocalError.missingBanEvasionContext
        }
        let endpoint = confirm
            ? Endpoint.confirmCommunityBanEvasion(communityIdOrSlug: context.communityId, userId: report.entityId)
            : Endpoint.dismissCommunityBanEvasion(communityIdOrSlug: context.communityId, userId: report.entityId)
        let _: EmptyResponse = try await client.send(endpoint)
        evict([report.id])
    }

    func removeTarget(_ report: StaffModerationReport, client: APIClient) async throws {
        let _: EmptyResponse = try await client.send(.deletePost(postId: report.entityId))
        let targetIds = Set(allLoadedStaffReports().filter {
            $0.entityType == report.entityType && $0.entityId == report.entityId
        }.map(\.id))
        evict(targetIds)
    }
}

private enum ModerationReportLocalError: Error {
    case missingTarget, missingBanEvasionContext
}

private extension String {
    var nilIfBlank: String? {
        trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : self
    }
}
