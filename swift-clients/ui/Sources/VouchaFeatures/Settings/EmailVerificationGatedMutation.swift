import Observation
import VouchaCore

@Observable
@MainActor
final class EmailVerificationGatedMutation {
    private(set) var isRecoveryPresented = false

    func perform<Value>(
        rollbackOnFailure: () -> Void,
        _ mutation: () async throws -> Value
    ) async throws -> Value {
        do {
            return try await mutation()
        } catch {
            rollbackOnFailure()
            if let vouchaError = error as? VouchaError, vouchaError.isEmailVerificationRequired {
                isRecoveryPresented = true
            }
            throw error
        }
    }

    func dismissRecovery() {
        isRecoveryPresented = false
    }
}
