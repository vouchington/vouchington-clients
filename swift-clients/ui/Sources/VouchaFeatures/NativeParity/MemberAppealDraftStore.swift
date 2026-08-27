import Observation
import VouchaModels

struct MemberAppealDraft: Equatable {
    var reason: ModerationAppealReason?
    var details = ""
}

@Observable
@MainActor
final class MemberAppealDraftStore {
    static let session = MemberAppealDraftStore()

    private var drafts: [String: MemberAppealDraft] = [:]

    func draft(for target: MemberAppealTarget, currentUserId: String) -> MemberAppealDraft {
        drafts[key(for: target, currentUserId: currentUserId)] ?? MemberAppealDraft()
    }

    func setReason(_ reason: ModerationAppealReason?, for target: MemberAppealTarget, currentUserId: String) {
        drafts[key(for: target, currentUserId: currentUserId), default: MemberAppealDraft()].reason = reason
    }

    func setDetails(_ details: String, for target: MemberAppealTarget, currentUserId: String) {
        drafts[key(for: target, currentUserId: currentUserId), default: MemberAppealDraft()].details = details
    }

    func clear(target: MemberAppealTarget, currentUserId: String) {
        drafts.removeValue(forKey: key(for: target, currentUserId: currentUserId))
    }

    private func key(for target: MemberAppealTarget, currentUserId: String) -> String {
        "\(currentUserId):\(target.id)"
    }
}
