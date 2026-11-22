import VouchaAPI
import VouchaModels

public extension FollowerDistributionViewModel {
    /// Fetches the configured recipient cap. On failure the picker stays uncapped and the server's
    /// validation decides, so a stale hardcoded value never blocks a selection the server allows.
    /// A response that finishes after the sheet was reset or its context replaced is dropped.
    func loadRecipientLimit() async {
        let generation = recipientLimitGeneration
        let limit: Int?
        do {
            let limits: ContentLimitsResponse = try await client.send(.contentLimits())
            limit = limits.followerDistributionRecipientsMax
        } catch {
            limit = nil
        }
        guard generation == recipientLimitGeneration else { return }
        recipientLimit = limit
    }

    internal var hasRecipientCapacity: Bool {
        guard let recipientLimit else { return true }
        return selectedRecipientIds.count < recipientLimit
    }

    /// A selection made before the cap loaded can exceed it; Send stays disabled until it fits.
    internal var selectionFitsRecipientLimit: Bool {
        guard let recipientLimit else { return true }
        return selectedRecipientIds.count <= recipientLimit
    }
}
