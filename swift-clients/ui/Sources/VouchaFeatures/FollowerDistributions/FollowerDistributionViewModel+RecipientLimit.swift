import VouchaAPI
import VouchaModels

public extension FollowerDistributionViewModel {
    /// Fetches the configured recipient cap. On failure the picker stays uncapped and the server's
    /// validation decides, so a stale hardcoded value never blocks a selection the server allows.
    func loadRecipientLimit() async {
        do {
            let limits: ContentLimitsResponse = try await client.send(.contentLimits())
            recipientLimit = limits.followerDistributionRecipientsMax
        } catch {
            recipientLimit = nil
        }
    }

    internal var hasRecipientCapacity: Bool {
        guard let recipientLimit else { return true }
        return selectedRecipientIds.count < recipientLimit
    }
}
