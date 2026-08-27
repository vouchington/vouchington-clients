import VouchaAPI
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func voteUserTrust(userId: String, choice: ElectionVoteChoice?) async {
        guard let client, !detailRelationIsSelfProfile, !userProfile.trustVoteInFlight else { return }
        let previous = userProfile.trustChoice
        userProfile.trustVoteInFlight = true
        userProfile.trustChoice = choice
        defer { userProfile.trustVoteInFlight = false }
        if let choice {
            let response: EmptyResponse? = try? await emailVerificationGate.perform(rollbackOnFailure: {
                userProfile.trustChoice = previous
            }, {
                try await client.send(.voteUserTrust(userId: userId, choice: choice))
            })
            if response != nil {
                await reloadDetailRelationBookmarks(client: client, entityType: "user", entityId: userId)
            }
        } else {
            do {
                let _: EmptyResponse = try await client.send(.clearUserTrustVote(userId: userId))
            } catch {
                userProfile.trustChoice = previous
            }
        }
    }
}
