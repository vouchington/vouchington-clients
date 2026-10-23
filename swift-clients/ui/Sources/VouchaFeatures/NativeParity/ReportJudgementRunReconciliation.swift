import VouchaAPI
import VouchaModels

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
            let run = try await read(reportId, runId)
            if run.completed || Task.isCancelled { return }
            if attempt + 1 < maximumReads {
                do {
                    try await delay()
                } catch is CancellationError {
                    return
                }
            }
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
