import Foundation

public enum MembershipVerificationPolling {
    public static func isTerminal(status: String, reasonCode: String?) -> Bool {
        if reasonCode == "purchase_pending" {
            return false
        }
        switch status {
        case "verified", "conflict", "rejected":
            return true
        default:
            return false
        }
    }

    public static func delaySeconds(attempt: Int, nextProcessingAt: String?, now: Date) -> TimeInterval {
        if let nextProcessingAt, let instant = parse(nextProcessingAt), instant > now {
            return instant.timeIntervalSince(now)
        }
        let schedule: [TimeInterval] = [1, 2, 4, 8, 15]
        let index = min(max(attempt, 0), schedule.count - 1)
        return schedule[index]
    }

    private static func parse(_ value: String) -> Date? {
        let fractional = ISO8601DateFormatter()
        fractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let date = fractional.date(from: value) {
            return date
        }
        let plain = ISO8601DateFormatter()
        plain.formatOptions = [.withInternetDateTime]
        return plain.date(from: value)
    }
}
