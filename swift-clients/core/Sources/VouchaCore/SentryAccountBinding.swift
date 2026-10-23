import Foundation

public enum SentryAccountUpdate: Equatable, Sendable {
    case unchanged
    case set(String)
    case clear
}

/// Signed-in events carry only the opaque account ID. Sign-out and account switch replace it.
public enum SentryAccountBinding {
    public static func update(from previous: String?, to next: String?) -> SentryAccountUpdate {
        let before = normalized(previous)
        let after = normalized(next)
        if before == after {
            return .unchanged
        }
        if let after {
            return .set(after)
        }
        return .clear
    }

    private static func normalized(_ accountId: String?) -> String? {
        guard let accountId = accountId?.trimmingCharacters(in: .whitespacesAndNewlines), !accountId.isEmpty else {
            return nil
        }
        return accountId
    }
}
