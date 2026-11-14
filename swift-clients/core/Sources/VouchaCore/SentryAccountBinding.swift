import Foundation

public enum SentryAccountUpdate: Equatable, Sendable {
    case unchanged
    case set(String)
    case clear
}

/// Signed-in events carry only the opaque account ID. Sign-out and account switch replace it.
public enum SentryAccountBinding {
    public static func update(from previous: String?, to next: String?) -> SentryAccountUpdate {
        let before = SentryTrimmedText.nonEmpty(previous)
        let after = SentryTrimmedText.nonEmpty(next)
        if before == after {
            return .unchanged
        }
        if let after {
            return .set(after)
        }
        return .clear
    }
}
