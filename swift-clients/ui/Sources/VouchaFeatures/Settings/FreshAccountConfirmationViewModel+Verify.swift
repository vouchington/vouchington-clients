import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaLocalization

extension FreshAccountConfirmationViewModel {
    func verifyOperation(emailCode: Bool = false, _ operation: () async throws -> String?) async {
        guard !isWorking, isPresented, context != nil else { return }
        let request = generation
        isWorking = true
        errorMessage = nil
        defer {
            if generation == request {
                isWorking = false
                if isPresented, brokerFlowID != nil, broker?.result != nil {
                    Task { await self.consumeBrokerResult(self.broker?.result) }
                }
            }
        }
        do {
            guard await hasCurrentContext(request) else { return }
            let token = try await operation()
            guard await hasCurrentContext(request) else { return }
            if let token {
                guard !token.isEmpty else { throw AccountConfirmationError.invalidProof }
                let waiting = continuation
                continuation = nil
                // Another method won; discard any broker flow left pending so OAuth can start again.
                broker?.cancelFreshAccountConfirmation()
                broker?.hasFreshConfirmationOwner = false
                brokerFlowID = nil
                isPresented = false
                code = ""
                waiting?.resume(returning: token)
            }
        } catch is CancellationError {
            cancel(request: request)
        } catch {
            guard await hasCurrentContext(request) else { return }
            errorMessage = failureMessage(error, emailCode: emailCode)
        }
    }
}
