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

    public static func delaySeconds(attempt: Int) -> TimeInterval {
        let schedule: [TimeInterval] = [1, 2, 4, 8, 15]
        let index = min(max(attempt, 0), schedule.count - 1)
        return schedule[index]
    }
}
