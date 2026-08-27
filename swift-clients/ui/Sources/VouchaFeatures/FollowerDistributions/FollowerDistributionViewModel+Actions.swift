import VouchaAPI
import VouchaCore

public extension FollowerDistributionViewModel {
    func share() async -> Bool {
        guard !isSharing else { return false }
        let generation = contextGeneration
        let endpoint = target.shareEndpoint()
        isSharing = true
        defer {
            if generation == contextGeneration {
                isSharing = false
            }
        }
        do {
            let _: FollowerDistributionAcceptedResponse = try await client.send(endpoint)
            guard generation == contextGeneration else { return false }
            error = nil
            return true
        } catch {
            guard generation == contextGeneration else { return false }
            self.error = vouchaError(error)
            return false
        }
    }

    func send() async -> Bool {
        guard !isSending, canSend else { return false }
        let generation = contextGeneration
        isSending = true
        defer {
            if generation == contextGeneration {
                isSending = false
            }
        }
        do {
            let request: FollowerDistributionRequest = sendsToAllFollowers
                ? .allFollowers
                : try .init(selectedRecipientIds: selectedRecipientIds.sorted())
            let endpoint = target.sendEndpoint(request: request)
            let _: FollowerDistributionAcceptedResponse = try await client.send(endpoint)
            guard generation == contextGeneration else { return false }
            error = nil
            return true
        } catch {
            guard generation == contextGeneration else { return false }
            self.error = vouchaError(error)
            return false
        }
    }

    func clearError() {
        error = nil
    }

    func vouchaError(_ error: Error) -> VouchaError {
        (error as? VouchaError) ?? .api(statusCode: 0, preconditionCode: nil)
    }
}
