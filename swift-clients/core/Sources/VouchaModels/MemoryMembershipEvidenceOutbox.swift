import Foundation

/// Process-memory outbox for unsent verification evidence.
/// Platform secure storage can replace this type later. Evidence is not written to preferences.
public actor MemoryMembershipEvidenceOutbox: MembershipEvidenceOutbox {
    private var stored: [String: DecodedJSONValue] = [:]

    public init() {}

    public func save(key: String, evidence: DecodedJSONValue) {
        stored[key] = evidence
    }

    public func remove(key: String) {
        stored.removeValue(forKey: key)
    }

    public func contains(key: String) -> Bool {
        stored[key] != nil
    }
}
