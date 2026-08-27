import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels

@MainActor
enum LifecycleScenarioSwiftAdapter {
    static func run(
        _ adapter: String,
        input: LifecycleScenarioInput,
        client: () throws -> APIClient
    ) async throws -> LifecycleObservation {
        switch adapter {
        case "swift-cursor-pagination-state":
            return try LifecycleScenarioPaginationAdapter.run(input)
        case "swift-moderation-appeals-view-model":
            return try await LifecycleScenarioSwiftModerationAdapter.run(input, client: client())
        case "swift-integrity-reconciliation":
            return try await LifecycleScenarioSwiftIntegrityAdapter.run(input)
        default:
            throw LifecycleScenarioError.invalid("Unknown Swift adapter \(adapter)")
        }
    }
}

@MainActor
private enum LifecycleScenarioSwiftModerationAdapter {
    static func run(_ input: LifecycleScenarioInput, client: APIClient) async throws -> LifecycleObservation {
        let preconditions = input.preconditions
        let outcome = input.serverOutcome
        let id = "appeal-contract"
        let initial = appeal(id: id, source: preconditions)
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (ModerationAppealsTestSupport.list([initial]), 200)
        let role = try preconditions.string("viewerRole")
        let viewModel = ModerationAppealsViewModel(
            client: client, isSignedIn: true, isAdministrator: role == "administrator",
            isSiteModerator: role == "moderator",
            rerunPollDelay: {}
        )
        await viewModel.load()
        let before = try require(viewModel.appeals.first, "Expected loaded appeal")
        let action = try input.action.string("type")
        let authoritative = appeal(id: id, source: outcome, fallback: preconditions)
        switch action {
        case "rerun":
            CannedFeedURLProtocol.handlers["/api/v1/appeals/\(id)/resolution-drafts"] = (
                ModerationAppealsTestSupport.queued,
                202
            )
            CannedFeedURLProtocol.handlers["/api/v1/appeals/\(id)"] = (
                ModerationAppealsTestSupport.envelope(authoritative),
                200
            )
            await viewModel.rerunAI(for: before)
        case "approve":
            CannedFeedURLProtocol.handlers["/api/v1/appeals/\(id)/approval"] = (
                ModerationAppealsTestSupport.envelope(authoritative),
                200
            )
            await viewModel.approve(before)
        case "send":
            CannedFeedURLProtocol.handlers["/api/v1/appeals/\(id)/delivery"] = (
                ModerationAppealsTestSupport.envelope(authoritative),
                200
            )
            await viewModel.send(before)
        case "deny":
            CannedFeedURLProtocol.handlers["/api/v1/appeals/\(id)/resolution"] = (
                ModerationAppealsTestSupport.envelope(authoritative),
                200
            )
            await viewModel.resolve(before, action: .deny)
        case "inspect-actions": break
        default: throw LifecycleScenarioError.invalid("Unknown moderation action \(action)")
        }
        let current: ModerationAppeal
        if action == "deny" {
            guard viewModel.appeals.isEmpty else {
                throw LifecycleScenarioError.invalid(
                    "Resolved appeal \(id) must leave the pending moderation queue"
                )
            }
            current = try decode(authoritative)
        } else {
            current = try require(
                viewModel.appeals.first(where: { $0.id == id }),
                "Production moderation view model removed appeal \(id) after \(action)"
            )
        }
        let visible = try visibleState(current, role: role, before: before, action: action)
        return LifecycleObservation(
            visibleState: visible, availableActions: actions(viewModel, current),
            reconciliation: ["strategy": .string(action == "inspect-actions" ? "not-applicable" : "server-response")],
            cancellation: lifecycleNotApplicable
        )
    }

    private static func appeal(
        id: String,
        source: [String: LifecycleJSON],
        fallback: [String: LifecycleJSON] = [:]
    ) -> String {
        let status = (try? source.string("status")) ?? (try? fallback.string("status")) ?? "pending"
        let approved = (try? source.optionalString("approvedAt")) ?? (try? fallback.optionalString("approvedAt"))
        let sent = (try? source.optionalString("sentAt")) ?? (try? fallback.optionalString("sentAt"))
        let appealType = (try? source.string("appealType")) ?? (try? fallback.string("appealType"))
        let resolution = (try? source.optionalString("resolutionAction")) ??
            (try? fallback.optionalString("resolutionAction"))
        let changed = (try? source.bool("latestLifecycleChangeChanged")) ?? false
        return ModerationAppealsTestSupport.appeal(
            id: id, status: status, suspensionId: appealType == "suspension" ? "suspension" : nil,
            publicResponse: "Ready for delivery",
            aiDraftedAt: changed ? "2026-01-01T00:01:00Z" : "2026-01-01T00:00:00Z",
            approvedAt: approved, sentAt: sent, resolutionAction: resolution,
            lifecycleChangeId: changed ? "change-2" : "change-1"
        )
    }

    private static func decode(_ value: String) throws -> ModerationAppeal {
        try JSONDecoder.vouchaFixtureDecoder.decode(ModerationAppeal.self, from: Data(value.utf8))
    }

    private static func visibleState(
        _ appeal: ModerationAppeal, role: String, before: ModerationAppeal, action: String
    ) throws -> [String: LifecycleJSON] {
        let changed = LifecycleJSON.bool(appeal.latestLifecycleChangeId != before.latestLifecycleChangeId)
        switch action {
        case "rerun": return ["status": .string(appeal.status.rawValue), "latestLifecycleChangeChanged": changed]
        case "approve", "send": return [
                "status": .string(appeal.status.rawValue),
                "approved": .bool(appeal.approvedAt != nil),
                "sent": .bool(appeal.sentAt != nil),
                "latestLifecycleChangeChanged": changed
            ]
        case "deny": return try [
                "status": .string(appeal.status.rawValue),
                "resolutionAction": .string(require(appeal.resolutionAction, "Missing resolution").rawValue),
                "latestLifecycleChangeChanged": changed
            ]
        case "inspect-actions": return ["status": .string(appeal.status.rawValue), "viewerRole": .string(role)]
        default: throw LifecycleScenarioError.invalid("Unknown moderation action \(action)")
        }
    }

    private static func actions(_ viewModel: ModerationAppealsViewModel, _ appeal: ModerationAppeal) -> [String] {
        var result: [String] = []
        if viewModel.canRerun(appeal) {
            result.append("rerun")
        }
        if viewModel.canApprove(appeal) {
            result.append("approve")
        }
        if viewModel.canSend(appeal) {
            result.append("send")
        }
        if viewModel.canResolve(appeal, action: .accept) {
            result.append("accept")
        }
        if viewModel.canResolve(appeal, action: .reduce) {
            result.append("reduce")
        }
        if viewModel.canResolve(appeal, action: .deny) {
            result.append("deny")
        }
        return result
    }

    private static func require<Value>(_ value: Value?, _ message: String) throws -> Value {
        guard let value else { throw LifecycleScenarioError.invalid(message) }
        return value
    }
}

@MainActor
private enum LifecycleScenarioSwiftIntegrityAdapter {
    static func run(_ input: LifecycleScenarioInput) async throws -> LifecycleObservation {
        let action = try input.action.string("type")
        switch action {
        case "resolve-report", "apply-report-penalty":
            return try await report(input, action: action)
        case "apply-vote-penalty":
            return try await vote(input)
        case "revoke-report-penalty", "revoke-vote-penalty":
            return try await revoke(input, domain: action.contains("report") ? .report : .vote)
        default:
            throw LifecycleScenarioError.invalid("Unknown integrity action \(action)")
        }
    }

    private static func report(_ input: LifecycleScenarioInput, action: String) async throws -> LifecycleObservation {
        let id = "report-flag"
        let service = LifecycleReportService()
        let pending = try IntegrityTestSupport.reportFlag(id: id)
        let penaltyCommitted = (try? input.serverOutcome.number("exactReadPenaltyCount")) != 0
        let resolved = try IntegrityTestSupport.reportFlag(
            id: id,
            resolution: action == "apply-report-penalty" && !penaltyCommitted ? nil :
                (action == "apply-report-penalty" ? "penalized" : "dismissed")
        )
        service.exact = input.serverOutcome["exactRead"] != nil
            ? .failure(URLError(.notConnectedToInternet))
            : .success(resolved)
        service.mutation = .failure(URLError(.networkConnectionLost))
        service.penaltyMutation = .failure(URLError(.networkConnectionLost))
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [pending]
        if action == "resolve-report" {
            await viewModel.dismiss(pending)
        } else {
            await viewModel.penalizeReporters(pending)
        }
        let displayed = viewModel.flags.first { $0.id == id }
        let reconciling = viewModel.reconciliationRequiredFlagIds.contains(id)
        let hasReconciliationError = viewModel.mutationErrorMessages[id] != nil
        let confirmed = displayed?.resolvedAt != nil || displayed?.resolution == .penalized
            || (displayed == nil && !reconciling)
        let isPending = displayed?.resolvedAt == nil && displayed?.resolution == nil
        let canRetry = reconciling && hasReconciliationError
        let canApplyPenalty = displayed.map(viewModel.canApplyPenalty) ?? false
        let unknown = canRetry || (isPending && canApplyPenalty)
        let visible: [String: LifecycleJSON] = action == "resolve-report"
            ? [
                "reportStatus": .string(confirmed ? "resolved" : "pending"),
                "error": unknown ? .string("mutation-outcome-unknown") : .null
            ]
            : ["penaltyApplied": .bool(confirmed), "error": unknown ? .string("mutation-outcome-unknown") : .null]
        return LifecycleObservation(
            visibleState: visible,
            availableActions: canRetry ? ["retry"] :
                (confirmed ? (action == "resolve-report" ? [] : ["revoke"]) : ["apply-penalty"]),
            reconciliation: canRetry ? ["strategy": .string("fail-closed")] : [
                "strategy": .string("exact-read"),
                "committed": .bool(confirmed)
            ],
            cancellation: lifecycleNotApplicable
        )
    }

    private static func vote(_ input: LifecycleScenarioInput) async throws -> LifecycleObservation {
        let id = "vote-flag"
        let service = LifecycleVoteService()
        let baseline = try Set(input.preconditions.strings("baselinePenaltyIds"))
        let exact = try Set(input.serverOutcome.strings("exactReadPenaltyIds"))
        service.penaltySnapshots = [.success(baseline), .success(exact)]
        service.mutation = .failure(URLError(.networkConnectionLost))
        let flag = try IntegrityTestSupport.voteFlag(id: id)
        service.exactFlag = flag
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]
        await viewModel.applyPenalty(flag)
        let committed = viewModel.confirmedPenaltyFlagIds.contains(id)
        return LifecycleObservation(
            visibleState: [
                "penaltyApplied": .bool(committed),
                "error": committed ? .null : .string("mutation-outcome-unknown")
            ],
            availableActions: committed ? ["revoke"] : ["apply-penalty"],
            reconciliation: [
                "strategy": .string("exact-read"),
                "comparison": .string(committed ? "new-row" : "unchanged-baseline")
            ],
            cancellation: lifecycleNotApplicable
        )
    }

    private static func revoke(
        _ input: LifecycleScenarioInput,
        domain: IntegrityDomain
    ) async throws -> LifecycleObservation {
        let id = try input.preconditions.string("penaltyId")
        let service = LifecyclePenaltyService()
        let active = try penalty(id: id, domain: domain, revoked: false)
        let revoked = try penalty(id: id, domain: domain, revoked: true)
        service.revokeResult = .failure(URLError(.networkConnectionLost))
        service.exactResult = .success(revoked)
        let viewModel = IntegrityPenaltyLedgerViewModel(service: service, viewerTier: .administrator, domain: domain)
        viewModel.penalties = [active]
        await viewModel.revoke(active)
        let committed = !viewModel.reconciliationRequiredIds.contains(id) && viewModel.penalties.isEmpty
        return LifecycleObservation(
            visibleState: [
                "revoked": .bool(committed),
                "error": committed ? .null : .string("mutation-outcome-unknown")
            ],
            availableActions: [], reconciliation: ["strategy": .string("exact-read"), "committed": .bool(committed)],
            cancellation: lifecycleNotApplicable
        )
    }

    private static func penalty(id: String, domain: IntegrityDomain, revoked: Bool) throws -> IntegrityPenaltyRow {
        let date = revoked ? "\"2026-01-02T00:00:00.000Z\"" : "null"
        switch domain {
        case .report:
            return try .report(
                decode(
                    "{\"id\":\"\(id)\",\"user_id\":\"user\",\"reason\":\"reason\",\"source_flag_id\":null,\"created_by_id\":\"admin\",\"revoked_at\":\(date),\"revoked_by_id\":null,\"created_at\":\"2026-01-01T00:00:00.000Z\",\"updated_at\":\"2026-01-01T00:00:00.000Z\"}"
                )
            )
        case .vote:
            return try .vote(
                decode(
                    "{\"id\":\"\(id)\",\"user_id\":\"user\",\"penalty_multiplier\":0.5,\"reason\":\"reason\",\"source_flag_id\":null,\"created_by_id\":\"admin\",\"revoked_at\":\(date),\"revoked_by_id\":null,\"created_at\":\"2026-01-01T00:00:00.000Z\"}"
                )
            )
        }
    }

    private static func decode<Value: Decodable>(_ value: String) throws -> Value {
        try JSONDecoder.vouchaFixtureDecoder.decode(Value.self, from: Data(value.utf8))
    }
}

@MainActor
private final class LifecycleReportService: ReportIntegrityServicing {
    var mutation: Result<ReportIntegrityFlag, Error>!
    var penaltyMutation: Result<ReportIntegrityPenaltyResponse, Error>!
    var exact: Result<ReportIntegrityFlag, Error>!
    func flags(
        status _: IntegrityFlagStatus?,
        after _: String?,
        limit _: Int
    ) async throws -> ReportIntegrityFlagsResponse {
        fatalError()
    }

    func dismiss(flagId _: String) async throws -> ReportIntegrityFlag {
        try mutation.get()
    }

    func penalizeReporters(flagId _: String) async throws -> ReportIntegrityPenaltyResponse {
        try penaltyMutation.get()
    }

    func flag(id _: String) async throws -> ReportIntegrityFlag {
        try exact.get()
    }
}

@MainActor
private final class LifecycleVoteService: VoteIntegrityServicing {
    var mutation: Result<VoteIntegrityPenaltyResponse, Error>!
    var penaltySnapshots: [Result<Set<String>, Error>] = []
    var exactFlag: VoteIntegrityFlag!
    func flags(
        status _: IntegrityFlagStatus?,
        after _: String?,
        limit _: Int
    ) async throws -> VoteIntegrityFlagsResponse {
        fatalError()
    }

    func flag(id _: String) async throws -> VoteIntegrityFlag {
        exactFlag
    }

    func resolve(
        flagId _: String,
        resolution _: VoteIntegrityResolution
    ) async throws -> VoteIntegrityFlag {
        fatalError()
    }

    func applyPenalty(flagId _: String) async throws -> VoteIntegrityPenaltyResponse {
        try mutation.get()
    }

    func penaltyIds(flagId _: String) async throws -> Set<String> {
        try penaltySnapshots.removeFirst().get()
    }
}

@MainActor
private final class LifecyclePenaltyService: IntegrityPenaltyServicing {
    var revokeResult: Result<IntegrityPenaltyRow, Error>!
    var exactResult: Result<IntegrityPenaltyRow, Error>!
    func penalties(
        domain _: IntegrityDomain,
        status _: IntegrityPenaltyStatus?,
        after _: String?,
        limit _: Int
    ) async throws -> IntegrityPenaltyPage {
        fatalError()
    }

    func penalty(domain _: IntegrityDomain, id _: String) async throws -> IntegrityPenaltyRow {
        try exactResult.get()
    }

    func revoke(domain _: IntegrityDomain, id _: String) async throws -> IntegrityPenaltyRow {
        try revokeResult.get()
    }
}
