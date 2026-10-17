import Foundation

/// A transport-local monotonic deadline. A refusal never schedules another request.
struct MemberMCPRetryCooldown {
    let uptime: @Sendable () -> TimeInterval
    let date: @Sendable () -> Date
    private var deadline: TimeInterval?

    init(uptime: @escaping @Sendable () -> TimeInterval, date: @escaping @Sendable () -> Date) {
        self.uptime = uptime
        self.date = date
    }

    mutating func limited(_ seconds: Int?) -> MemberMCPClient.Failure {
        let now = uptime()
        let remaining = (deadline ?? now) - now
        if let seconds, seconds > 0, Double(seconds) >= remaining {
            deadline = now + Double(seconds)
            return .rateLimited(retryAfterSeconds: seconds)
        }
        guard remaining > 0 else { return .rateLimited(retryAfterSeconds: nil) }
        let effectiveSeconds = remaining >= Double(Int.max) ? Int.max : Int(ceil(remaining))
        return .rateLimited(retryAfterSeconds: effectiveSeconds)
    }

    mutating func check() throws {
        guard let deadline else { return }
        let remaining = deadline - uptime()
        guard remaining > 0 else {
            self.deadline = nil
            return
        }
        let seconds = remaining >= Double(Int.max) ? Int.max : Int(ceil(remaining))
        throw MemberMCPClient.Failure.rateLimited(retryAfterSeconds: seconds)
    }
}
