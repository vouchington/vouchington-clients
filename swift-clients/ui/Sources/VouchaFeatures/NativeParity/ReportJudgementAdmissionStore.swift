import Foundation
import Observation

@Observable
@MainActor
public final class ReportJudgementAdmissionStore {
    static let session = ReportJudgementAdmissionStore()

    struct Admission: Equatable {
        let token: UUID
        var runId: String?
    }

    private var pending: [AppealAIRerunOwner: [String: Admission]] = [:]

    public init() {}

    func admission(_ reportId: String, owner: AppealAIRerunOwner?) -> Admission? {
        guard let owner else { return nil }
        return pending[owner]?[reportId]
    }

    func admit(_ reportId: String, owner: AppealAIRerunOwner?) -> Admission? {
        guard let owner, pending[owner]?[reportId] == nil else { return nil }
        let value = Admission(token: UUID())
        pending[owner, default: [:]][reportId] = value
        return value
    }

    func record(_ runId: String, reportId: String, owner: AppealAIRerunOwner?, token: UUID) {
        guard let owner, !runId.isEmpty, pending[owner]?[reportId]?.token == token else { return }
        pending[owner]?[reportId]?.runId = runId
    }

    func remove(_ reportId: String, owner: AppealAIRerunOwner?, token: UUID) {
        guard let owner, pending[owner]?[reportId]?.token == token else { return }
        pending[owner]?.removeValue(forKey: reportId)
        if pending[owner]?.isEmpty == true { pending.removeValue(forKey: owner) }
    }
}
