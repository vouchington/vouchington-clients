import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

enum ReportJudgementRunEnded: Error, Equatable {
    case failed, expired

    var messageKey: UiMessageKey {
        switch self {
        case .failed: .nativeSwiftModerationReportsAiJudgementRunFailed
        case .expired: .nativeSwiftModerationReportsAiJudgementRunExpired
        }
    }
}

enum ReportJudgementRunReconciliation {
    static func read(
        reportId: String,
        runId: String,
        maximumReads: Int,
        delay: @Sendable () async throws -> Void,
        read: (String, String) async throws -> ModerationReportJudgementRunResponse
    ) async throws {
        guard maximumReads > 0, !reportId.isEmpty, !runId.isEmpty else { return }
        for attempt in 0 ..< maximumReads {
            if Task.isCancelled { return }
            let run: ModerationReportJudgementRunResponse
            do {
                run = try await read(reportId, runId)
            } catch {
                if isCancellation(error) { return }
                throw error
            }
            if try Task.isCancelled || isFinished(run.status) { return }
            if attempt + 1 < maximumReads {
                do {
                    try await delay()
                } catch {
                    if isCancellation(error) { return }
                    throw error
                }
            }
        }
    }

    private static func isFinished(_ status: ModerationAIRunStatus) throws -> Bool {
        switch status {
        case .failed: throw ReportJudgementRunEnded.failed
        case .expired: throw ReportJudgementRunEnded.expired
        case .succeeded: true
        case .active: false
        }
    }

    private static func isCancellation(_ error: Error) -> Bool {
        switch error {
        case is CancellationError:
            true
        case VouchaError.cancelled:
            true
        case let VouchaError.network(urlError) where urlError.code == .cancelled:
            true
        default:
            false
        }
    }
}

public extension ModerationReportsViewModel {
    func isJudgementPending(_ reportId: String) -> Bool {
        judgementAdmissions.admission(reportId, owner: judgementOwner) != nil
    }

    func hasAdmittedJudgementRun(_ reportId: String) -> Bool {
        judgementAdmissions.admission(reportId, owner: judgementOwner)?.runId != nil
    }

    func refreshPendingJudgement(_ reportId: String) async {
        guard viewerTier.isStaff, let client, report(id: reportId) != nil,
              let admission = judgementAdmissions.admission(reportId, owner: judgementOwner),
              let runId = admission.runId, !isQueueActionInProgress
        else { return }
        isMutatingQueue = true
        defer { isMutatingQueue = false }
        actionErrors[reportId] = nil
        do {
            try await readAdmittedJudgement(reportId, runId: runId, token: admission.token, client: client)
        } catch {
            actionErrors[reportId] = message(for: error)
        }
    }

    internal func reconcileAdmittedReportJudgement(
        _ report: StaffModerationReport,
        client: APIClient
    ) async throws {
        guard let reserved = judgementAdmissions.admit(report.id, owner: judgementOwner) else { return }
        let admission: ModerationReportJudgementResponse
        do {
            admission = try await client.send(.rerunModerationReportJudgement(reportId: report.id))
        } catch {
            let rejected: Bool = switch error {
            case VouchaError.unauthorized, VouchaError.forbidden, VouchaError.notFound:
                true
            case let VouchaError.api(statusCode, _), let VouchaError.apiMessage(statusCode, _, _):
                (400 ..< 500).contains(statusCode) && statusCode != 408
            default:
                false
            }
            if rejected {
                judgementAdmissions.remove(report.id, owner: judgementOwner, token: reserved.token)
            }
            throw error
        }
        guard admission.queued else {
            judgementAdmissions.remove(report.id, owner: judgementOwner, token: reserved.token)
            return
        }
        guard !admission.runId.isEmpty else { return }
        judgementAdmissions.record(admission.runId, reportId: report.id, owner: judgementOwner, token: reserved.token)
        try await readAdmittedJudgement(report.id, runId: admission.runId, token: reserved.token, client: client)
    }

    private func readAdmittedJudgement(_ reportId: String, runId: String, token: UUID, client: APIClient) async throws {
        let revision = generation
        try await ReportJudgementRunReconciliation.read(
            reportId: reportId, runId: runId, maximumReads: judgementRunReads, delay: judgementRunDelay
        ) { reportId, runId in
            let run: ModerationReportJudgementRunResponse = try await client.send(
                .moderationReportJudgementRun(reportId: reportId, runId: runId)
            )
            guard !Task.isCancelled, revision == self.generation, self.report(id: reportId) != nil,
                  self.judgementAdmissions.admission(reportId, owner: self.judgementOwner)?.token == token,
                  run.runId == runId else { throw CancellationError() }
            if run.status != .active {
                self.judgementAdmissions.remove(reportId, owner: self.judgementOwner, token: token)
            }
            return run
        }
    }
}
