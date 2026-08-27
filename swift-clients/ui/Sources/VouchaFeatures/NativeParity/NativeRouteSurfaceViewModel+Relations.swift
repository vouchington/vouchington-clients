import VouchaAPI
import VouchaModels

extension NativeRouteSurfaceViewModel {
    var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    public func isDetailRelationActive(_ predicate: String) -> Bool {
        detailRelationBookmarks[predicate] == true
    }

    func reloadDetailRelationBookmarks(client: APIClient, entityType: String, entityId: String) async {
        let response: EntityBookmarksResponse?
        do {
            response = try await client.send(.bookmarks(entityType: entityType, entityId: entityId))
        } catch {
            return
        }
        guard detailRelationEntityType == entityType, detailRelationEntityId == entityId else { return }
        detailRelationBookmarks = response?.bookmarks ?? [:]
    }

    func reloadUserTrustContext(client: APIClient, userId: String) async {
        guard let context: UserTrustContext = try? await client.send(.userTrustContext(userId: userId)),
              detailRelationEntityType == "user",
              detailRelationEntityId == userId
        else {
            return
        }
        userProfile.trustContext = context
        userProfile.trustChoice = context.electionVote?.choice
    }

    public func toggleDetailRelation(predicate: String) async {
        guard let client,
              let entityType = detailRelationEntityType,
              let entityId = detailRelationEntityId,
              !detailRelationIsSelfProfile
        else {
            return
        }
        guard predicate.isEmpty == false else { return }

        let key = "\(entityType)|\(entityId)|\(predicate)"
        guard !inFlightDetailRelationKeys.contains(key) else { return }
        inFlightDetailRelationKeys.insert(key)
        defer { inFlightDetailRelationKeys.remove(key) }

        let wasActive = detailRelationBookmarks[predicate] == true
        let wasFollowing = detailRelationBookmarks["follow"] == true
        let clearsFollowState = shouldClearFollowState(
            entityType: entityType,
            predicate: predicate,
            wasActive: wasActive
        )
        detailRelationBookmarks[predicate] = !wasActive
        if clearsFollowState {
            detailRelationBookmarks["follow"] = false
        }

        do {
            if wasActive {
                let _: EmptyResponse = try await client.send(.unbookmark(
                    entityType: entityType,
                    entityId: entityId,
                    predicate: predicate
                ))
            } else {
                let _: EmptyResponse = try await client.send(.bookmark(
                    entityType: entityType,
                    entityId: entityId,
                    predicate: predicate
                ))
                if entityType == "user", predicate == "follow" {
                    await reloadUserTrustContext(client: client, userId: entityId)
                }
            }
        } catch {
            detailRelationBookmarks[predicate] = wasActive
            if clearsFollowState, wasFollowing {
                detailRelationBookmarks["follow"] = true
            }
        }
    }
}

func shouldClearFollowState(entityType: String, predicate: String, wasActive: Bool) -> Bool {
    guard !wasActive else { return false }
    switch (entityType, predicate) {
    case ("topic", "mute"), ("rss_feed", "mute"), ("user", "block"):
        return true
    default:
        return false
    }
}
