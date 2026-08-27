import VouchaAPI
import VouchaModels

extension FriendsListViewModel {
    public func toggleFollow(userId: String) async {
        guard self.userId != nil, !inFlightFollows.contains(userId) else { return }
        inFlightFollows.insert(userId)
        defer { inFlightFollows.remove(userId) }

        let wasFollowing = followingIds.contains(userId)
        let removedUser = wasFollowing ? following.first(where: { $0.id == userId }) : nil

        if wasFollowing {
            followingIds.remove(userId)
            followingPagination.remove { $0.id == userId }
        } else {
            followingIds.insert(userId)
            if let user = followers.first(where: { $0.id == userId }) {
                followingPagination.replaceItems(following + [user])
            }
        }

        do {
            if wasFollowing {
                let _: EmptyResponse = try await client.send(.unfollowUser(userId: userId))
            } else {
                let _: EmptyResponse = try await client.send(.followUser(userId: userId))
            }
        } catch {
            rollbackFollowToggle(userId: userId, wasFollowing: wasFollowing, removedUser: removedUser)
        }
    }

    private func rollbackFollowToggle(userId: String, wasFollowing: Bool, removedUser: PublicUser?) {
        if wasFollowing {
            followingIds.insert(userId)
            if let removedUser {
                followingPagination.replaceItems(following + [removedUser])
            }
        } else {
            followingIds.remove(userId)
            followingPagination.remove { $0.id == userId }
        }
    }
}
