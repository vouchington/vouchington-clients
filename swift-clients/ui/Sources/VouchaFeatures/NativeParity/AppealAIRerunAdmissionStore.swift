import Foundation
import Observation
import VouchaModels

struct AppealAIRerunOwner: Hashable {
    let userId: String
    let scheme: String
    let host: String
    let port: Int
    let basePath: String

    init?(userId: String?, baseURL: URL?) {
        guard let userId, !userId.isEmpty, let baseURL,
              let scheme = baseURL.scheme?.lowercased(),
              let host = baseURL.host?.lowercased(),
              scheme == "http" || scheme == "https"
        else { return nil }
        let port = baseURL.port ?? (scheme == "https" ? 443 : 80)
        let path = URLComponents(url: baseURL, resolvingAgainstBaseURL: false)?.percentEncodedPath ?? ""
        let normalizedPath = path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        self.userId = userId
        self.scheme = scheme
        self.host = host
        self.port = port
        basePath = normalizedPath
    }
}

@Observable
@MainActor
final class AppealAIRerunAdmissionStore {
    static let session = AppealAIRerunAdmissionStore()

    struct Admission: Equatable {
        let token: UUID
        let baseline: AppealAIRerunBaseline
    }

    private var pendingByOwner: [AppealAIRerunOwner: [String: Admission]] = [:]

    func pending(for owner: AppealAIRerunOwner?) -> [String: AppealAIRerunBaseline] {
        guard let owner else { return [:] }
        return pendingByOwner[owner]?.mapValues(\.baseline) ?? [:]
    }

    func admit(_ appeal: ModerationAppeal, for owner: AppealAIRerunOwner?) -> Admission? {
        guard let owner, pendingByOwner[owner]?[appeal.id] == nil else { return nil }
        let admission = Admission(token: UUID(), baseline: AppealAIRerunBaseline(appeal: appeal))
        pendingByOwner[owner, default: [:]][appeal.id] = admission
        return admission
    }

    func admission(_ appealId: String, for owner: AppealAIRerunOwner?) -> Admission? {
        guard let owner else { return nil }
        return pendingByOwner[owner]?[appealId]
    }

    func remove(_ appealId: String, for owner: AppealAIRerunOwner?, matching token: UUID) {
        guard let owner, pendingByOwner[owner]?[appealId]?.token == token
        else { return }
        pendingByOwner[owner]?.removeValue(forKey: appealId)
        if pendingByOwner[owner]?.isEmpty == true { pendingByOwner.removeValue(forKey: owner) }
    }

    func reconcile(_ appeals: [ModerationAppeal], for owner: AppealAIRerunOwner?) {
        guard let owner else { return }
        for appeal in appeals {
            if let admission = pendingByOwner[owner]?[appeal.id],
               admission.baseline.isReconciled(in: appeal) {
                remove(appeal.id, for: owner, matching: admission.token)
            }
        }
    }
}
