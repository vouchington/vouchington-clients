import Foundation

extension MembershipPurchaseSession {
    func poll(id: String) async throws -> NativeMembershipVerification {
        var attempt = 0
        while attempt < 8 {
            try Task.checkCancellation()
            let status = try await transport.verification(id: id).verification
            if MembershipVerificationPolling.isTerminal(status: status.status, reasonCode: status.reasonCode) {
                try await refreshMembership()
                return status
            }
            attempt += 1
            if attempt == 8 {
                break
            }
            let seconds = signals.consumeRefresh()
                ? 0
                : MembershipVerificationPolling.delaySeconds(attempt: attempt - 1)
            if seconds > 0 {
                try await clock.sleep(seconds: seconds)
                try Task.checkCancellation()
            }
        }
        throw MembershipPurchaseError.verificationDidNotFinish
    }
}
