import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension FriendRecommendationsViewModel {
    public func follow(_ recommendation: FriendRecommendation) async {
        await mutate(recommendation, endpoint: .followUser(userId: recommendation.id))
    }

    public func dismiss(_ recommendation: FriendRecommendation) async {
        await mutate(
            recommendation,
            endpoint: .bookmark(
                entityType: "user",
                entityId: recommendation.id,
                predicate: "dismiss_recommendation"
            )
        )
    }

    private func mutate(_ recommendation: FriendRecommendation, endpoint: Endpoint) async {
        guard mutationTokens[recommendation.id] == nil,
              let index = recommendations.firstIndex(where: { $0.id == recommendation.id })
        else { return }
        let token = UUID()
        mutationTokens[recommendation.id] = token
        tombstones[recommendation.id] = FriendRecommendationTombstone(
            token: token,
            recommendation: recommendation,
            originalIndex: index,
            status: .pending
        )
        mutationErrorMessage = nil
        pagination.remove { $0.id == recommendation.id }

        do {
            let _: EmptyResponse = try await client.send(endpoint)
            guard mutationTokens[recommendation.id] == token,
                  var tombstone = tombstones[recommendation.id],
                  tombstone.token == token
            else { return }
            tombstone.status = .succeeded
            tombstones[recommendation.id] = tombstone
        } catch {
            rollbackMutation(userId: recommendation.id, token: token)
            mutationErrorMessage = .verbatim(error.localizedDescription)
        }
        if mutationTokens[recommendation.id] == token {
            mutationTokens.removeValue(forKey: recommendation.id)
        }
    }

    private func rollbackMutation(userId: String, token: UUID) {
        guard let tombstone = tombstones[userId], tombstone.token == token else { return }
        tombstones.removeValue(forKey: userId)
        guard !recommendations.contains(where: { $0.id == userId }) else { return }
        var restored = recommendations
        restored.insert(tombstone.recommendation, at: min(tombstone.originalIndex, restored.count))
        pagination.replaceItems(restored)
    }
}
