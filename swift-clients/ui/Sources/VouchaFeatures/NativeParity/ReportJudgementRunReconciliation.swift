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

extension ModerationReportsViewModel {
    func reconcileAdmittedReportJudgement(
        _ report: StaffModerationReport,
        client: APIClient
    ) async throws {
        let admission: ModerationReportJudgementResponse = try await client.send(
            .rerunModerationReportJudgement(reportId: report.id)
        )
        guard admission.queued, !admission.runId.isEmpty else { return }
        try await ReportJudgementRunReconciliation.read(
            reportId: report.id,
            runId: admission.runId,
            maximumReads: judgementRunReads,
            delay: judgementRunDelay
        ) { reportId, runId in
            try await client.send(.moderationReportJudgementRun(reportId: reportId, runId: runId))
        }
    }
}
